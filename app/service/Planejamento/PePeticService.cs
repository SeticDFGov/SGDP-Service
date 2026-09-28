using System.Globalization;
using System.Text.RegularExpressions;
using api.Common;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Versões do PETIC-DF (art. 11 do Decreto nº 48.900/2026). Uma versão em rascunho ou em
/// deliberação por vez (índice único parcial em pe_petic.situacao). O rascunho nasce vazio
/// ou copiado da vigente: os registros com os mesmos códigos e a mesma ordem, as ligações
/// entre eles refeitas para as cópias (as ligações com catálogo continuam apontando para o
/// mesmo item) e a sequência dos códigos, para nunca repetir um código apagado. Só o
/// rascunho muda; o envio ao CGTIC confere as pendências e cria a deliberação; apagar só
/// vale para o rascunho que nunca foi enviado.
/// </summary>
public class PePeticService : IPePeticService
{
    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;

    public PePeticService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<List<PePeticResponse>> ListarAsync(PeUserContext ctx)
    {
        var versoes = (await _context.PePetics.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync())
            .Where(p => _permissoes.PodeVerVersaoPetic(ctx, p.Situacao))
            .ToList();
        var deliberacoes = await UltimasDeliberacoesAsync(versoes.Select(p => p.Id));
        var nomes = await NomesAsync(deliberacoes.Values);
        return versoes.Select(p => Resposta(p, deliberacoes.GetValueOrDefault(p.Id), nomes)).ToList();
    }

    public async Task<PePeticResponse?> VigenteAsync()
    {
        var vigente = await _context.PePetics.AsNoTracking()
            .Where(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();
        return vigente == null ? null : await RespostaAsync(vigente);
    }

    public async Task<PePeticResponse> ObterAsync(long id, PeUserContext ctx)
    {
        var petic = await VersaoAsync(id, rastrear: false);
        // Papel de órgão só vê as versões aprovadas
        if (!_permissoes.PodeVerVersaoPetic(ctx, petic.Situacao))
            throw new ApiException(ErrorCode.PePeticNaoEncontrado, "Versão do PETIC-DF não encontrada. Atualize a tela.");
        return await RespostaAsync(petic);
    }

    // ── Escrita ─────────────────────────────────────────────────────────────

    public async Task<PePeticResponse> CriarAsync(PePeticCriarDTO dto, PeUserContext ctx)
    {
        var titulo = PeModeloService.Obrigatorio(dto.Titulo, 200, "o título da versão");
        var inicio = Data(dto.VigenciaInicio, "O início da vigência");
        var fim = Data(dto.VigenciaFim, "O fim da vigência");
        ValidarVigencia(inicio, fim);

        if (await _context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Rascunho
                                                 || p.Situacao == PeDominios.SituacaoPetic.EmDeliberacao))
            throw new ApiException(ErrorCode.PeVersaoEmAndamento,
                "Já existe uma versão do PETIC-DF em rascunho ou com o CGTIC. Termine aquela antes de começar outra.");

        var vigente = await _context.PePetics.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado);
        var versoes = await _context.PePetics.AsNoTracking().Select(p => p.Versao).ToListAsync();
        var agora = DateTime.UtcNow;

        var petic = new PePetic
        {
            Versao = ProximaVersao(versoes),
            Titulo = titulo,
            VigenciaInicio = inicio,
            VigenciaFim = fim,
            Situacao = PeDominios.SituacaoPetic.Rascunho,
            AnteriorId = vigente?.Id,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PePetics.Add(petic);

        var copiar = dto.CopiarDaVigente ?? true;
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        if (copiar && vigente != null) await CopiarRegistrosAsync(vigente.Id, petic, ctx.Email, agora);
        await _context.SaveChangesAsync();
        // A sequência usa o id da versão nova, que só existe depois da primeira gravação
        if (copiar && vigente != null)
        {
            await CopiarSequenciasAsync(vigente.Id, petic.Id);
            await _context.SaveChangesAsync();
        }
        if (transacao != null) await transacao.CommitAsync();

        return await RespostaAsync(petic);
    }

    public async Task<PePeticResponse> AtualizarAsync(long id, PePeticAtualizarDTO dto, PeUserContext ctx)
    {
        var petic = await VersaoAsync(id, rastrear: true);
        if (petic.Situacao != PeDominios.SituacaoPetic.Rascunho) throw PeRegistroService.VersaoFechada(petic);

        if (dto.Informou(nameof(dto.Titulo))) petic.Titulo = PeModeloService.Obrigatorio(dto.Titulo, 200, "o título da versão");
        if (dto.Informou(nameof(dto.VigenciaInicio))) petic.VigenciaInicio = Data(dto.VigenciaInicio, "O início da vigência");
        if (dto.Informou(nameof(dto.VigenciaFim))) petic.VigenciaFim = Data(dto.VigenciaFim, "O fim da vigência");
        ValidarVigencia(petic.VigenciaInicio, petic.VigenciaFim);

        petic.AlteradoEm = DateTime.UtcNow;
        petic.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();
        return await RespostaAsync(petic);
    }

    public async Task<PePeticEnvioResponse> EnvioAsync(long id, PeUserContext ctx)
    {
        var petic = await VersaoAsync(id, rastrear: false);
        if (!_permissoes.PodeVerVersaoPetic(ctx, petic.Situacao))
            throw new ApiException(ErrorCode.PePeticNaoEncontrado, "Versão do PETIC-DF não encontrada. Atualize a tela.");
        if (petic.Situacao != PeDominios.SituacaoPetic.Rascunho)
            return new PePeticEnvioResponse { PodeEnviar = false, Motivo = PeRegistroService.VersaoFechada(petic).Error.Message };
        var pendencias = await PendenciasDoEnvioAsync(petic);
        string? motivo = pendencias.Count > 0 ? "Resolva o que falta na lista antes de enviar o PETIC-DF ao CGTIC." : null;
        if (!_permissoes.PodeEditarReferenciais(ctx)) motivo = "Quem envia o PETIC-DF ao CGTIC é o administrador do módulo.";
        return new PePeticEnvioResponse { PodeEnviar = motivo == null, Pendencias = pendencias, Motivo = motivo };
    }

    public async Task<PePeticResponse> EnviarAsync(long id, PeUserContext ctx)
    {
        var petic = await VersaoAsync(id, rastrear: true);
        if (petic.Situacao != PeDominios.SituacaoPetic.Rascunho) throw PeRegistroService.VersaoFechada(petic);

        // O que falta vai em lista, com a seção de cada item (F1, A06): a tela mostra e leva a cada seção
        var pendencias = await PendenciasDoEnvioAsync(petic);
        if (pendencias.Count > 0) throw new PePeticPendenciasException(pendencias);

        var agora = DateTime.UtcNow;
        petic.Situacao = PeDominios.SituacaoPetic.EmDeliberacao;
        petic.AlteradoEm = agora;
        petic.AlteradoPor = ctx.Email;
        _context.PeDeliberacoes.Add(new PeDeliberacao
        {
            ObjetoTipo = PeDominios.ObjetoDeliberacao.Petic,
            ObjetoId = petic.Id,
            VersaoObjeto = petic.Versao,
            EnviadoEm = agora,
            EnviadoPor = ctx.Email,
            Situacao = PeDominios.SituacaoDeliberacao.Aguardando,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        });
        await _context.SaveChangesAsync();
        return await RespostaAsync(petic);
    }

    public async Task ExcluirAsync(long id, PeUserContext ctx)
    {
        var petic = await VersaoAsync(id, rastrear: true);
        if (petic.Situacao != PeDominios.SituacaoPetic.Rascunho) throw PeRegistroService.VersaoFechada(petic);
        if (await _context.PeDeliberacoes.AnyAsync(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Petic && d.ObjetoId == id))
            throw new ApiException(ErrorCode.PeVersaoJaEnviada,
                "Esta versão já foi ao CGTIC e fica guardada no histórico. Ajuste o rascunho e envie de novo.");

        // Ligações, registros, sequências e a versão (os arquivos ficam guardados). Só há
        // ligação para um registro do rascunho vinda do próprio rascunho (a ligação com seção
        // fica no mesmo dono e os catálogos leem a vigente)
        var registros = await _context.PeRegistros.Where(r => r.PeticId == id).ToListAsync();
        var ids = registros.Select(r => r.Id).ToList();
        _context.PeVinculos.RemoveRange(await _context.PeVinculos.Where(v => ids.Contains(v.RegistroOrigemId)).ToListAsync());
        _context.PeRegistros.RemoveRange(registros);
        var dono = PeDono.DoPetic(id).Chave;
        _context.PeRegistroSequencias.RemoveRange(await _context.PeRegistroSequencias.Where(s => s.Dono == dono).ToListAsync());
        _context.PePetics.Remove(petic);
        await _context.SaveChangesAsync();
    }

    // ── Cópia da vigente ────────────────────────────────────────────────────

    private async Task CopiarRegistrosAsync(long origemId, PePetic destino, string autor, DateTime agora)
    {
        var registros = await _context.PeRegistros.AsNoTracking().Where(r => r.PeticId == origemId).ToListAsync();
        var copias = new Dictionary<long, PeRegistro>();
        foreach (var registro in registros)
        {
            var copia = new PeRegistro
            {
                SecaoId = registro.SecaoId,
                Petic = destino,
                Codigo = registro.Codigo,
                Ordem = registro.Ordem,
                Dados = registro.Dados,
                Sistema = registro.Sistema,
                CriadoEm = agora,
                CriadoPor = autor
            };
            copias[registro.Id] = copia;
            _context.PeRegistros.Add(copia);
        }
        if (copias.Count == 0) return;

        // Ligação entre registros da própria versão aponta para a cópia; ligação com catálogo
        // (princípios, por exemplo) continua no mesmo item
        var ids = copias.Keys.ToList();
        var vinculos = await _context.PeVinculos.AsNoTracking().Where(v => ids.Contains(v.RegistroOrigemId)).ToListAsync();
        var idsCampos = vinculos.Select(v => v.CampoId).Distinct().ToList();
        var comSecao = (await _context.PeCampos.AsNoTracking()
                .Where(c => idsCampos.Contains(c.Id) && c.Tipo == PeDominios.TipoCampo.LigacaoSecao)
                .Select(c => c.Id)
                .ToListAsync())
            .ToHashSet();
        foreach (var vinculo in vinculos)
        {
            var vinculoNovo = new PeVinculo { RegistroOrigem = copias[vinculo.RegistroOrigemId], CampoId = vinculo.CampoId };
            if (comSecao.Contains(vinculo.CampoId) && copias.TryGetValue(vinculo.RegistroDestinoId, out var destinoCopiado))
                vinculoNovo.RegistroDestino = destinoCopiado;
            else
                vinculoNovo.RegistroDestinoId = vinculo.RegistroDestinoId;
            _context.PeVinculos.Add(vinculoNovo);
        }
    }

    private async Task CopiarSequenciasAsync(long origemId, long destinoId)
    {
        var origem = PeDono.DoPetic(origemId).Chave;
        var destino = PeDono.DoPetic(destinoId).Chave;
        foreach (var sequencia in await _context.PeRegistroSequencias.AsNoTracking().Where(s => s.Dono == origem).ToListAsync())
            _context.PeRegistroSequencias.Add(new PeRegistroSequencia { SecaoId = sequencia.SecaoId, Dono = destino, Ultimo = sequencia.Ultimo });
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>A vigência (título e vigência da versão) e o que falta nas seções, na ordem das seções.</summary>
    private async Task<List<PePeticPendenciaResponse>> PendenciasDoEnvioAsync(PePetic petic)
    {
        var pendencias = new List<PePeticPendenciaResponse>();
        if (petic.VigenciaInicio == null || petic.VigenciaFim == null)
            pendencias.Add(new PePeticPendenciaResponse { SecaoChave = null, SecaoTitulo = "Título e vigência", Motivo = "Informe o início e o fim da vigência." });
        pendencias.AddRange(await _registros.PendenciasAsync(PeDono.DoPetic(petic.Id)));
        return pendencias;
    }

    private async Task<PePetic> VersaoAsync(long id, bool rastrear)
    {
        var consulta = rastrear ? _context.PePetics : _context.PePetics.AsNoTracking();
        return await consulta.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new ApiException(ErrorCode.PePeticNaoEncontrado, "Versão do PETIC-DF não encontrada. Atualize a tela.");
    }

    /// <summary>"1.0" para a primeira; depois, o maior número de versão mais um ("2.0", "3.0").</summary>
    internal static string ProximaVersao(IEnumerable<string> versoes)
    {
        var maior = versoes
            .Select(v => int.TryParse(v.Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{maior + 1}.0";
    }

    private static DateOnly? Data(string? valor, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            || data.Year < 2000 || data.Year > 2199)
            throw new ApiException(ErrorCode.PeDadosInvalidos, $"{oQue} precisa ser uma data válida, no formato aaaa-mm-dd.");
        return data;
    }

    private static void ValidarVigencia(DateOnly? inicio, DateOnly? fim)
    {
        if (inicio != null && fim != null && fim < inicio)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "O fim da vigência não pode ser antes do início.");
    }

    private async Task<Dictionary<long, PeDeliberacao>> UltimasDeliberacoesAsync(IEnumerable<long> idsPetic)
    {
        var ids = idsPetic.ToList();
        if (ids.Count == 0) return new Dictionary<long, PeDeliberacao>();
        // Só as colunas que a deliberação do PETIC-DF usa, sem a do PDF enviado (doc_versao_id, da
        // E7, só do PDTIC): assim a lista, a versão e a criação do PETIC-DF não dependem dela no
        // intervalo do deploy (código novo antes da migration), e criar um rascunho nesse intervalo
        // não grava para depois responder 409
        var todas = await _context.PeDeliberacoes.AsNoTracking()
            .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Petic && ids.Contains(d.ObjetoId))
            .Select(d => new PeDeliberacao
            {
                Id = d.Id,
                ObjetoTipo = d.ObjetoTipo,
                ObjetoId = d.ObjetoId,
                VersaoObjeto = d.VersaoObjeto,
                EnviadoEm = d.EnviadoEm,
                EnviadoPor = d.EnviadoPor,
                Situacao = d.Situacao,
                DecididoEm = d.DecididoEm,
                DecididoPor = d.DecididoPor,
                AtoTipo = d.AtoTipo,
                AtoNumero = d.AtoNumero,
                AtoData = d.AtoData,
                Sei = d.Sei,
                Observacao = d.Observacao
            })
            .ToListAsync();
        return todas.GroupBy(d => d.ObjetoId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Id).First());
    }

    private async Task<PePeticResponse> RespostaAsync(PePetic petic)
    {
        var deliberacao = (await UltimasDeliberacoesAsync(new[] { petic.Id })).GetValueOrDefault(petic.Id);
        return Resposta(petic, deliberacao, await NomesAsync(deliberacao == null ? Array.Empty<PeDeliberacao>() : new[] { deliberacao }));
    }

    /// <summary>Os nomes de quem enviou e de quem decidiu (F1, C19), de uma vez.</summary>
    private Task<PeNomes> NomesAsync(IEnumerable<PeDeliberacao> deliberacoes) =>
        PeNomes.CarregarAsync(_context, deliberacoes.SelectMany(d => new[] { d.EnviadoPor, d.DecididoPor }));

    private static PePeticResponse Resposta(PePetic petic, PeDeliberacao? deliberacao, PeNomes nomes) => new()
    {
        Id = petic.Id,
        Versao = petic.Versao,
        Titulo = petic.Titulo,
        VigenciaInicio = petic.VigenciaInicio,
        VigenciaFim = petic.VigenciaFim,
        Situacao = petic.Situacao,
        AprovadoEm = petic.AprovadoEm,
        AnteriorId = petic.AnteriorId,
        Deliberacao = deliberacao == null ? null : PeDeliberacaoService.Resposta(deliberacao, nomes)
    };
}

/// <summary>
/// Deliberações do CGTIC (Secretaria Executiva). Decidir é uma vez só: a situação da
/// deliberação é token de concorrência, e duas decisões ao mesmo tempo não passam as duas.
/// PETIC-DF: aprovar marca a versão vigente anterior como substituída numa gravação e aprova
/// a nova na seguinte, dentro da mesma transação (o índice único de "aprovado" não admite
/// duas vigentes nem por um instante). PDTIC (E7): aprovar põe o PDTIC em "aprovado" (com a
/// data), marca a versão do documento enviada como aprovada e, na revisão, a versão vigente
/// do órgão como substituída; devolver põe o PDTIC em "devolvido" (volta a ser editável). O
/// item do PDTIC na fila traz "PDTIC SES 1.0", o órgão e o PDF enviado (Documento).
/// </summary>
public partial class PeDeliberacaoService : IPeDeliberacaoService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;

    [GeneratedRegex(@"^\d{5}-\d{8}/\d{4}-\d{2}$")]
    private static partial Regex FormatoSei();

    private readonly AppDbContext _context;

    public PeDeliberacaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<PeDeliberacaoResponse>> ListarAsync(PeDeliberacoesConsulta consulta)
    {
        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = _context.PeDeliberacoes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(consulta.Situacao))
        {
            // Fora do domínio: lista vazia, nunca "todas" em silêncio
            var situacao = consulta.Situacao.Trim();
            query = PeDominios.SituacaoDeliberacao.Todas.Contains(situacao)
                ? query.Where(d => d.Situacao == situacao)
                : query.Where(d => false);
        }
        if (!string.IsNullOrWhiteSpace(consulta.ObjetoTipo))
        {
            var objeto = consulta.ObjetoTipo.Trim();
            query = PeDominios.ObjetoDeliberacao.Todos.Contains(objeto)
                ? query.Where(d => d.ObjetoTipo == objeto)
                : query.Where(d => false);
        }

        const string aguardando = PeDominios.SituacaoDeliberacao.Aguardando;
        var total = await query.CountAsync();
        var linhas = await query
            // Aguardando primeiro, por ordem de chegada; depois as decididas, da mais nova
            .OrderBy(d => d.Situacao == aguardando ? 0 : 1)
            .ThenBy(d => d.Situacao == aguardando ? d.Id : 0)
            .ThenByDescending(d => d.DecididoEm)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<PeDeliberacaoResponse>(await RespostasAsync(_context, linhas), total, page, pageSize);
    }

    public async Task<PeDeliberacaoResponse> DecidirAsync(long id, PeDecidirDTO dto, PeUserContext ctx)
    {
        var deliberacao = await _context.PeDeliberacoes.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new ApiException(ErrorCode.PeDeliberacaoNaoEncontrada, "Deliberação não encontrada. Atualize a tela.");
        if (deliberacao.Situacao != PeDominios.SituacaoDeliberacao.Aguardando)
            throw new ApiException(ErrorCode.PeDeliberacaoJaDecidida, "Esta deliberação já foi registrada e não muda.");

        var decisao = dto.Decisao?.Trim();
        if (decisao == null || !PeDominios.SituacaoDeliberacao.Decisoes.Contains(decisao))
            throw Invalida("Diga a decisão do CGTIC: aprovado ou devolvido.");

        var atoTipo = Texto(dto.AtoTipo, 60, "O tipo do ato");
        var atoNumero = Texto(dto.AtoNumero, 60, "O número do ato");
        var atoData = DataDoAto(dto.AtoData);
        var sei = Sei(dto.Sei);
        var observacao = Texto(dto.Observacao, 2000, "A observação");

        if (decisao == PeDominios.SituacaoDeliberacao.Aprovado && (atoNumero == null || atoData == null))
            throw Invalida("Para registrar a aprovação, informe o número e a data do ato do CGTIC.");
        if (decisao == PeDominios.SituacaoDeliberacao.Devolvido && observacao == null)
            throw Invalida("Para devolver, escreva na observação o que precisa ser ajustado.");

        var agora = DateTime.UtcNow;
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;

        switch (deliberacao.ObjetoTipo)
        {
            case PeDominios.ObjetoDeliberacao.Petic:
                await AplicarAoPeticAsync(deliberacao, decisao, ctx, agora);
                break;
            case PeDominios.ObjetoDeliberacao.Pdtic:
                await AplicarAoPdticAsync(deliberacao, decisao, ctx, agora);
                break;
            default:
                throw Invalida("Esta deliberação é de um objeto que o módulo não conhece.");
        }

        deliberacao.Situacao = decisao;
        deliberacao.DecididoEm = agora;
        deliberacao.DecididoPor = ctx.Email;
        deliberacao.AtoTipo = atoTipo;
        deliberacao.AtoNumero = atoNumero;
        deliberacao.AtoData = atoData;
        deliberacao.Sei = sei;
        deliberacao.Observacao = observacao;
        deliberacao.AlteradoEm = agora;
        deliberacao.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();
        if (transacao != null) await transacao.CommitAsync();

        return (await RespostasAsync(_context, new[] { deliberacao }))[0];
    }

    /// <summary>
    /// A decisão sobre um PDTIC (E7): aprovado põe o PDTIC em "aprovado" (aprovado_em), a versão
    /// do documento enviada em "aprovada" e, quando é uma revisão, a versão vigente do órgão em
    /// "substituída" (a anterior segue em acompanhamento até aqui); devolvido volta o PDTIC a
    /// ser editável ("devolvido"; a versão enviada fica como estava).
    /// </summary>
    private async Task AplicarAoPdticAsync(PeDeliberacao deliberacao, string decisao, PeUserContext ctx, DateTime agora)
    {
        var pdtic = await _context.PePdtics.FirstOrDefaultAsync(p => p.Id == deliberacao.ObjetoId)
            ?? throw new ApiException(ErrorCode.PePdticNaoEncontrado, "O PDTIC desta deliberação não existe mais.");
        if (pdtic.Situacao != PeDominios.SituacaoPdtic.EmAprovacao)
            throw new ApiException(ErrorCode.PeConflitoGravacao, "O PDTIC não está mais aguardando a deliberação. Atualize a tela.");

        if (decisao == PeDominios.SituacaoDeliberacao.Aprovado)
        {
            // A vigente do órgão (a versão que a revisão revê) sai quando a nova é aprovada
            var vigentes = await _context.PePdtics
                .Where(p => p.OrgaoId == pdtic.OrgaoId && p.Id != pdtic.Id && PeDominios.SituacaoPdtic.Vigentes.Contains(p.Situacao))
                .ToListAsync();
            foreach (var anterior in vigentes)
            {
                anterior.Situacao = PeDominios.SituacaoPdtic.Substituido;
                anterior.AlteradoEm = agora;
                anterior.AlteradoPor = ctx.Email;
            }

            pdtic.Situacao = PeDominios.SituacaoPdtic.Aprovado;
            pdtic.AprovadoEm = agora;
            if (deliberacao.DocVersaoId is long versaoId
                && await _context.PeDocVersoes.FirstOrDefaultAsync(v => v.Id == versaoId) is { } versao)
                versao.Situacao = PeDominios.SituacaoVersaoDoc.Aprovada;
        }
        else
        {
            pdtic.Situacao = PeDominios.SituacaoPdtic.Devolvido;
        }
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
    }

    private async Task AplicarAoPeticAsync(PeDeliberacao deliberacao, string decisao, PeUserContext ctx, DateTime agora)
    {
        var petic = await _context.PePetics.FirstOrDefaultAsync(p => p.Id == deliberacao.ObjetoId)
            ?? throw new ApiException(ErrorCode.PePeticNaoEncontrado, "A versão do PETIC-DF desta deliberação não existe mais.");
        if (petic.Situacao != PeDominios.SituacaoPetic.EmDeliberacao)
            throw new ApiException(ErrorCode.PeConflitoGravacao, "A versão do PETIC-DF não está mais em deliberação. Atualize a tela.");

        if (decisao == PeDominios.SituacaoDeliberacao.Aprovado)
        {
            // Primeiro a vigente anterior sai; depois a nova entra (só uma vigente)
            var anteriores = await _context.PePetics
                .Where(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado && p.Id != petic.Id)
                .ToListAsync();
            foreach (var anterior in anteriores)
            {
                anterior.Situacao = PeDominios.SituacaoPetic.Substituido;
                anterior.AlteradoEm = agora;
                anterior.AlteradoPor = ctx.Email;
            }
            if (anteriores.Count > 0) await _context.SaveChangesAsync();

            petic.Situacao = PeDominios.SituacaoPetic.Aprovado;
            petic.AprovadoEm = agora;
        }
        else
        {
            petic.Situacao = PeDominios.SituacaoPetic.Rascunho;
        }
        petic.AlteradoEm = agora;
        petic.AlteradoPor = ctx.Email;
    }

    /// <summary>
    /// As respostas de uma lista de deliberações, com o órgão e o PDF enviado de cada PDTIC
    /// (duas consultas para a lista inteira, não uma por item).
    /// </summary>
    internal static async Task<List<PeDeliberacaoResponse>> RespostasAsync(AppDbContext context, IReadOnlyList<PeDeliberacao> deliberacoes)
    {
        var pdtics = deliberacoes.Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic).Select(d => d.ObjetoId).Distinct().ToList();
        var orgaos = pdtics.Count == 0
            ? new Dictionary<long, (string Sigla, string Nome, bool Externo)>()
            : (await (from p in context.PePdtics.AsNoTracking()
                      join o in context.PgiaOrgaos.AsNoTracking() on p.OrgaoId equals o.Id
                      where pdtics.Contains(p.Id)
                      select new { p.Id, o.Sigla, o.Nome, p.RegistradoExternamente })
                .ToListAsync())
                .ToDictionary(x => x.Id, x => (x.Sigla, x.Nome, Externo: x.RegistradoExternamente));
        var idsVersoes = deliberacoes.Where(d => d.DocVersaoId != null).Select(d => d.DocVersaoId!.Value).Distinct().ToList();
        var versoes = idsVersoes.Count == 0
            ? new Dictionary<long, PeDeliberacaoDocumentoResponse>()
            : await context.PeDocVersoes.AsNoTracking()
                .Where(v => idsVersoes.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => new PeDeliberacaoDocumentoResponse { PdticId = v.PdticId, Numero = v.Numero });

        var nomes = await PeNomes.CarregarAsync(context, deliberacoes.SelectMany(d => new[] { d.EnviadoPor, d.DecididoPor }));

        return deliberacoes.Select(d =>
        {
            var resposta = Resposta(d, nomes);
            if (d.ObjetoTipo != PeDominios.ObjetoDeliberacao.Pdtic) return resposta;
            if (orgaos.TryGetValue(d.ObjetoId, out var orgao))
            {
                resposta.OrgaoSigla = orgao.Sigla;
                resposta.OrgaoNome = orgao.Nome;
                resposta.Titulo = $"PDTIC {orgao.Sigla} {d.VersaoObjeto}";
                // O PDTIC registrado fora do sistema não passa pelo envio: a deliberação dele é a
                // do registro (C40), decidida na data do ato (também nas linhas gravadas antes da F1,
                // que guardaram o dia do registro)
                if (orgao.Externo)
                {
                    resposta.RegistradaForaDoSistema = true;
                    if (d.AtoData is DateOnly ato) resposta.DecididoEm = PePdticAprovacaoService.MeioDia(ato);
                }
            }
            resposta.Documento = d.DocVersaoId is long versao ? versoes.GetValueOrDefault(versao) : null;
            return resposta;
        }).ToList();
    }

    /// <summary>A deliberação mais recente de cada PDTIC (pelo id), já com o órgão e o PDF enviado.</summary>
    internal static async Task<Dictionary<long, PeDeliberacaoResponse>> UltimasDosPdticsAsync(AppDbContext context, IReadOnlyCollection<long> pdticIds)
    {
        if (pdticIds.Count == 0) return new Dictionary<long, PeDeliberacaoResponse>();
        var ids = pdticIds.Distinct().ToList();
        var todas = await context.PeDeliberacoes.AsNoTracking()
            .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && ids.Contains(d.ObjetoId))
            .ToListAsync();
        var ultimas = todas.GroupBy(d => d.ObjetoId).Select(g => g.OrderByDescending(d => d.Id).First()).ToList();
        return (await RespostasAsync(context, ultimas)).ToDictionary(r => r.ObjetoId);
    }

    internal static PeDeliberacaoResponse Resposta(PeDeliberacao d, PeNomes nomes) => new()
    {
        Id = d.Id,
        ObjetoTipo = d.ObjetoTipo,
        ObjetoId = d.ObjetoId,
        VersaoObjeto = d.VersaoObjeto,
        Titulo = d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Petic ? $"PETIC-DF {d.VersaoObjeto}" : $"PDTIC {d.VersaoObjeto}",
        OrgaoSigla = null,
        EnviadoEm = d.EnviadoEm,
        EnviadoPor = d.EnviadoPor,
        EnviadoPorNome = nomes.DeObrigatorio(d.EnviadoPor),
        Situacao = d.Situacao,
        DecididoEm = d.DecididoEm,
        DecididoPor = d.DecididoPor,
        DecididoPorNome = nomes.De(d.DecididoPor),
        AtoTipo = d.AtoTipo,
        AtoNumero = d.AtoNumero,
        AtoData = d.AtoData,
        Sei = d.Sei,
        Observacao = d.Observacao
    };

    private static string? Texto(string? valor, int maximo, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw Invalida($"{oQue} tem no máximo {maximo} caracteres.");
        return texto;
    }

    private static DateOnly? DataDoAto(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            || data.Year < 2000)
            throw Invalida("A data do ato precisa ser uma data válida, no formato aaaa-mm-dd.");
        if (data > DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia()))
            throw Invalida("A data do ato não pode ser depois de hoje.");
        return data;
    }

    private static string? Sei(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!FormatoSei().IsMatch(texto))
            throw Invalida("Informe o processo SEI no formato 00000-00000000/0000-00.");
        return texto;
    }

    private static ApiException Invalida(string mensagem) => new(ErrorCode.PeDecisaoInvalida, mensagem);
}
