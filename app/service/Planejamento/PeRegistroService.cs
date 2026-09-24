using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>Uma seção pronta para a planilha: as colunas (visíveis e "na planilha") e os registros com os rótulos.</summary>
public sealed class PeSecaoExportada
{
    public required PeSecaoDoDono Modelo { get; init; }

    public required List<PeCampoVisivel> Colunas { get; init; }

    public required List<PeRegistroResponse> Registros { get; init; }
}

/// <summary>
/// Motor de registros (E3). Para o PETIC-DF e o catálogo do DF, a seção e os campos
/// aparecem pela situação geral (desligado some); a E4 acrescenta o PDTIC, que aparece
/// pelo nível do órgão. Regras de cada gravação:
/// <list type="bullet">
/// <item>só os campos visíveis entram; campo escondido (desligado ou apagado) guarda o valor
/// que já tinha e não é exigido (decisão 13: guarda e esconde);</item>
/// <item>obrigatório, tipo, tamanho, faixa, casas, opção ativa (a desativada só vale se já
/// era a guardada), arquivo enviado pela mesma pessoa e ainda sem dono;</item>
/// <item>ligações só com registros do mesmo dono (ligação com seção) ou do catálogo (os do
/// PETIC-DF, da vigente; sem vigente, a ligação fica opcional); múltipla ou uma só;</item>
/// <item>calculados calculados aqui e guardados no jsonb;</item>
/// <item>código pelo prefixo da seção e pela sequência do dono (nunca reaproveita);</item>
/// <item>registro do sistema não se edita nem se apaga; registro ligado por outro não se apaga.</item>
/// </list>
/// Gravar um registro do PETIC-DF toca a versão (alterado em e por) com a situação como token
/// de concorrência: enviar ao CGTIC e gravar ao mesmo tempo não passam os dois.
/// </summary>
public class PeRegistroService : IPeRegistroService
{
    private readonly AppDbContext _context;
    private readonly IPePermissionService _permissoes;

    public PeRegistroService(AppDbContext context, IPePermissionService permissoes)
    {
        _context = context;
        _permissoes = permissoes;
    }

    private sealed record PeDonoAberto(PeDono Dono, PePetic? Petic, bool PodeEditar);

    private sealed record PeFonteCatalogo(PeDono Dono, PeSecaoDoDono Secao);

    /// <summary>O que uma gravação muda: os dados, as ligações e os arquivos que passam a ter dono.</summary>
    private sealed class PeGravacao
    {
        public JsonObject Dados { get; init; } = new();

        public List<(long CampoId, long DestinoId)> VinculosNovos { get; } = new();

        public List<PeVinculo> VinculosRemovidos { get; } = new();

        public List<PeArquivo> Arquivos { get; } = new();
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<PeRegistrosResponse> ListarAsync(PeDono dono, string secaoChave, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: false);
        var secao = await SecaoAsync(dono, secaoChave);
        return await RespostaDaSecaoAsync(aberto, secao);
    }

    public async Task<List<PeCatalogoItemResponse>> CatalogoAsync(string catalogo, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerReferenciais(ctx)) throw SemPermissaoDeLer();
        var chave = catalogo?.Trim() ?? string.Empty;
        if (!PeDominios.Catalogo.SecaoDoCatalogo.ContainsKey(chave))
            throw new ApiException(ErrorCode.PeCatalogoNaoEncontrado,
                "Catálogo não encontrado. Os catálogos são petic_objetivo, petic_eixo e principio.");

        var fonte = await CatalogoFonteAsync(chave);
        if (fonte == null) return new List<PeCatalogoItemResponse>();

        var registros = await RegistrosDo(fonte.Dono).AsNoTracking()
            .Where(r => r.SecaoId == fonte.Secao.Secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        return registros
            .Select(r => new PeCatalogoItemResponse { Id = r.Id, Codigo = r.Codigo, Rotulo = ResumoDe(fonte.Secao, r) })
            .ToList();
    }

    public async Task<List<string>> PendenciasAsync(PeDono dono)
    {
        var montadas = await MontarAsync(await SecoesDoDonoAsync(dono, soNaPlanilha: false));
        var ids = montadas.Select(s => s.Secao.Id).ToList();
        var registros = await RegistrosDo(dono).AsNoTracking()
            .Where(r => ids.Contains(r.SecaoId))
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        var idsRegistros = registros.Select(r => r.Id).ToList();
        var ligacoes = new HashSet<(long Origem, long Campo)>();
        if (idsRegistros.Count > 0)
        {
            var linhas = await _context.PeVinculos.AsNoTracking()
                .Where(v => idsRegistros.Contains(v.RegistroOrigemId))
                .Select(v => new { v.RegistroOrigemId, v.CampoId })
                .ToListAsync();
            foreach (var linha in linhas) ligacoes.Add((linha.RegistroOrigemId, linha.CampoId));
        }
        var semVigente = !await _context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado);

        var pendencias = new List<string>();
        foreach (var secao in montadas)
        {
            var doSecao = registros.Where(r => r.SecaoId == secao.Secao.Id).ToList();
            if (doSecao.Count == 0)
            {
                if (secao.Secao.SituacaoGeral == PeDominios.Situacao.Obrigatorio)
                    pendencias.Add(secao.EhFormulario
                        ? $"Preencha \"{secao.Secao.Titulo}\"."
                        : $"Inclua pelo menos um item em \"{secao.Secao.Titulo}\".");
                continue;
            }

            foreach (var registro in doSecao)
            {
                var dados = PeRegistroDados.Ler(registro.Dados);
                var faltando = secao.Visiveis
                    .Where(v => ObrigatorioEfetivo(v, semVigente) && v.Campo.Tipo != PeDominios.TipoCampo.Calculado)
                    .Where(v => PeRegistroDados.EhLigacao(v.Campo)
                        ? !ligacoes.Contains((registro.Id, v.Campo.Id))
                        : PeRegistroDados.EhVazio(dados[v.Campo.Chave]))
                    .Select(v => $"\"{v.Campo.Rotulo}\"")
                    .ToList();
                if (faltando.Count > 0)
                    pendencias.Add($"{registro.Codigo ?? secao.Secao.Titulo}: preencha {string.Join(", ", faltando)}.");
            }
        }
        return pendencias;
    }

    public async Task<PeSecaoExportada> ExportarSecaoAsync(PeDono dono, string secaoChave, PeUserContext ctx)
    {
        await AbrirAsync(dono, ctx, escrita: false);
        var secao = await SecaoAsync(dono, secaoChave);
        if (!secao.Secao.NaPlanilha)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não vai para a planilha: o administrador a deixou de fora.");
        return await ExportadaAsync(dono, secao);
    }

    public async Task<List<PeSecaoExportada>> ExportarSecoesAsync(PeDono dono, PeUserContext ctx)
    {
        await AbrirAsync(dono, ctx, escrita: false);
        var saida = new List<PeSecaoExportada>();
        foreach (var secao in await MontarAsync(await SecoesDoDonoAsync(dono, soNaPlanilha: true)))
            saida.Add(await ExportadaAsync(dono, secao));
        return saida;
    }

    // ── Escrita ─────────────────────────────────────────────────────────────

    public async Task<PeRegistroResponse> CriarAsync(PeDono dono, string secaoChave, PeRegistroSalvarDTO dto, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoAsync(dono, secaoChave);
        var doDono = RegistrosDo(dono).Where(r => r.SecaoId == secao.Secao.Id);
        if (secao.EhFormulario && await doDono.AnyAsync())
            throw new ApiException(ErrorCode.PeFormularioJaPreenchido,
                "Este formulário já foi preenchido. Atualize a tela e salve por cima.");

        var gravacao = await ValidarAsync(aberto, secao, null, dto, ctx);

        var agora = DateTime.UtcNow;
        var sequencia = await SequenciaAsync(secao.Secao.Id, dono);
        sequencia.Ultimo++;
        var registro = new PeRegistro
        {
            SecaoId = secao.Secao.Id,
            PeticId = dono.PeticId,
            Codigo = CodigoDe(secao, sequencia.Ultimo),
            Ordem = (await doDono.MaxAsync(r => (int?)r.Ordem) ?? 0) + 1,
            Sistema = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeRegistros.Add(registro);
        Aplicar(registro, gravacao);
        Tocar(aberto.Petic, ctx, agora);

        // O id do registro sai na primeira gravação; os arquivos ganham o dono na segunda
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await _context.SaveChangesAsync();
        if (gravacao.Arquivos.Count > 0)
        {
            DarDono(gravacao, registro.Id, ctx, agora);
            await _context.SaveChangesAsync();
        }
        if (transacao != null) await transacao.CommitAsync();

        return await UmaRespostaAsync(secao, registro.Id);
    }

    public async Task<PeRegistroResponse> AtualizarAsync(PeDono dono, string secaoChave, long id, PeRegistroSalvarDTO dto, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoAsync(dono, secaoChave);
        var registro = await RegistroParaEscritaAsync(dono, secao, id);

        var gravacao = await ValidarAsync(aberto, secao, registro, dto, ctx);

        var agora = DateTime.UtcNow;
        Aplicar(registro, gravacao);
        DarDono(gravacao, registro.Id, ctx, agora);
        registro.AlteradoEm = agora;
        registro.AlteradoPor = ctx.Email;
        Tocar(aberto.Petic, ctx, agora);
        await _context.SaveChangesAsync();

        return await UmaRespostaAsync(secao, registro.Id);
    }

    public async Task ExcluirAsync(PeDono dono, string secaoChave, long id, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoAsync(dono, secaoChave);
        var registro = await RegistroParaEscritaAsync(dono, secao, id);

        var ligadoPor = await _context.PeVinculos.AsNoTracking()
            .Where(v => v.RegistroDestinoId == id)
            .Select(v => v.RegistroOrigemId)
            .Distinct()
            .ToListAsync();
        if (ligadoPor.Count > 0)
            throw new ApiException(ErrorCode.PeRegistroLigado, await MensagemLigadoPorAsync(ligadoPor));

        _context.PeVinculos.RemoveRange(await _context.PeVinculos.Where(v => v.RegistroOrigemId == id).ToListAsync());
        _context.PeRegistros.Remove(registro);
        Tocar(aberto.Petic, ctx, DateTime.UtcNow);
        await _context.SaveChangesAsync();
    }

    public async Task<PeRegistrosResponse> OrdenarAsync(PeDono dono, string secaoChave, PeOrdemDTO dto, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoAsync(dono, secaoChave);
        var registros = await RegistrosDo(dono).Where(r => r.SecaoId == secao.Secao.Id).ToListAsync();

        var ids = dto.Ids ?? new List<long>();
        if (ids.Count != registros.Count || ids.Distinct().Count() != ids.Count || !registros.Select(r => r.Id).ToHashSet().SetEquals(ids))
            throw new ApiException(ErrorCode.PeOrdemInvalida, "Mande todos os registros da seção, cada um uma vez, na nova ordem.");

        for (var i = 0; i < ids.Count; i++)
            registros.Single(r => r.Id == ids[i]).Ordem = i + 1;
        Tocar(aberto.Petic, ctx, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        return await RespostaDaSecaoAsync(aberto, secao);
    }

    // ── Validação de uma gravação ───────────────────────────────────────────

    private async Task<PeGravacao> ValidarAsync(PeDonoAberto aberto, PeSecaoDoDono secao, PeRegistro? atual,
        PeRegistroSalvarDTO dto, PeUserContext ctx)
    {
        var novo = atual == null;
        var erros = new Dictionary<string, string>();
        var guardados = PeRegistroDados.Ler(atual?.Dados);
        var dados = (JsonObject)guardados.DeepClone();
        var todos = secao.Campos.ToDictionary(c => c.Chave);
        var visiveis = secao.Visiveis.ToDictionary(v => v.Campo.Chave, v => v.Campo);
        var semVigente = await SemVigenteAsync(secao);

        // Chaves que não são campos da seção (as de campo escondido, apagado ou calculado são ignoradas)
        if (dto.Dados != null)
            foreach (var chave in dto.Dados.Keys)
            {
                if (!todos.TryGetValue(chave, out var campo))
                    erros[chave] = "Este campo não existe nesta seção. Atualize a tela.";
                else if (PeRegistroDados.EhLigacao(campo) && visiveis.ContainsKey(chave))
                    erros[chave] = "A ligação vai em Vinculos, não em Dados.";
            }
        if (dto.Vinculos != null)
            foreach (var chave in dto.Vinculos.Keys)
                if (!todos.TryGetValue(chave, out var campo) || !PeRegistroDados.EhLigacao(campo))
                    erros[chave] = "Este campo de ligação não existe nesta seção. Atualize a tela.";

        // Valores (no PUT, Dados ausente mantém o que estava)
        var arquivos = new List<(PeCampo Campo, long Id)>();
        foreach (var visivel in secao.Visiveis)
        {
            var campo = visivel.Campo;
            if (PeRegistroDados.EhLigacao(campo) || campo.Tipo == PeDominios.TipoCampo.Calculado || erros.ContainsKey(campo.Chave))
                continue;

            if (dto.Dados != null || novo)
            {
                var entrada = dto.Dados != null && dto.Dados.TryGetValue(campo.Chave, out var e) ? e : default;
                var resultado = PeValores.Normalizar(campo, secao.OpcoesDe(campo), entrada, guardados[campo.Chave]);
                if (resultado.Erro != null)
                {
                    erros[campo.Chave] = resultado.Erro;
                    continue;
                }

                if (resultado.ArquivoId is long idArquivo)
                {
                    // O mesmo arquivo que já estava: fica como está
                    if (PeRegistroDados.ArquivoId(guardados[campo.Chave]) != idArquivo)
                    {
                        arquivos.Add((campo, idArquivo));
                        dados[campo.Chave] = new JsonObject { ["ArquivoId"] = idArquivo, ["Nome"] = string.Empty };
                    }
                }
                else if (resultado.Valor == null)
                    dados.Remove(campo.Chave);
                else
                    dados[campo.Chave] = resultado.Valor;
            }

            if (visivel.Obrigatorio && PeRegistroDados.EhVazio(dados[campo.Chave]))
                erros[campo.Chave] = PeValores.MensagemObrigatorio(campo);
        }

        var gravacao = new PeGravacao { Dados = dados };

        // Arquivos novos: enviados por quem grava, ainda sem dono, do tipo e do tamanho do campo
        foreach (var (campo, id) in arquivos)
        {
            var arquivo = await _context.PeArquivos.FirstOrDefaultAsync(a => a.Id == id);
            if (arquivo == null)
            {
                erros[campo.Chave] = "O arquivo não foi encontrado. Envie de novo.";
                continue;
            }
            if (arquivo.DonoTipo != null || !MesmaPessoa(arquivo.CriadoPor, ctx.Email))
            {
                erros[campo.Chave] = "Este arquivo não pode ser usado aqui. Envie o arquivo de novo.";
                continue;
            }
            var tipos = PeValores.TiposDeArquivo(campo.Config);
            var tipo = PeArquivoService.TipoDoArquivo(arquivo.Nome);
            if (tipo == null || !tipos.Contains(tipo))
            {
                erros[campo.Chave] = $"Este campo aceita só {string.Join(", ", tipos.Select(t => t.ToUpperInvariant()))}.";
                continue;
            }
            var maximoMb = PeValores.Inteiro(campo.Config, "maxMb") ?? PeDominios.TipoArquivo.MaximoMb;
            if (arquivo.Tamanho > maximoMb * 1024L * 1024L)
            {
                erros[campo.Chave] = $"O arquivo passa de {maximoMb} MB.";
                continue;
            }
            dados[campo.Chave] = new JsonObject { ["ArquivoId"] = arquivo.Id, ["Nome"] = arquivo.Nome };
            gravacao.Arquivos.Add(arquivo);
        }

        // Ligações (no PUT, Vinculos ausente mantém as que estavam)
        var guardadas = novo
            ? new List<PeVinculo>()
            : await _context.PeVinculos.Where(v => v.RegistroOrigemId == atual!.Id).ToListAsync();
        foreach (var visivel in secao.Visiveis.Where(v => PeRegistroDados.EhLigacao(v.Campo)))
        {
            var campo = visivel.Campo;
            if (erros.ContainsKey(campo.Chave)) continue;

            var atuais = guardadas.Where(v => v.CampoId == campo.Id).ToList();
            var desejados = dto.Vinculos == null && !novo
                ? atuais.Select(v => v.RegistroDestinoId).ToList()
                : (dto.Vinculos != null && dto.Vinculos.TryGetValue(campo.Chave, out var ids) ? ids : new List<long>()).Distinct().ToList();

            var erro = await ValidarLigacaoAsync(aberto.Dono, campo, desejados, atual?.Id);
            if (erro != null)
            {
                erros[campo.Chave] = erro;
                continue;
            }
            if (ObrigatorioEfetivo(visivel, semVigente) && desejados.Count == 0)
            {
                erros[campo.Chave] = PeValores.MensagemObrigatorio(campo);
                continue;
            }

            gravacao.VinculosRemovidos.AddRange(atuais.Where(v => !desejados.Contains(v.RegistroDestinoId)));
            gravacao.VinculosNovos.AddRange(desejados
                .Where(d => atuais.All(v => v.RegistroDestinoId != d))
                .Select(d => (campo.Id, d)));
        }

        if (erros.Count > 0) throw new PeValidacaoException(erros);

        // Calculados, com os valores já validados
        foreach (var visivel in secao.Visiveis.Where(v => v.Campo.Tipo == PeDominios.TipoCampo.Calculado))
        {
            var valor = PeValores.Calcular(visivel.Campo, dados, visiveis);
            if (valor == null) dados.Remove(visivel.Campo.Chave);
            else dados[visivel.Campo.Chave] = valor;
        }

        return gravacao;
    }

    /// <summary>Confere os destinos de um campo de ligação; devolve a mensagem do erro ou nulo.</summary>
    private async Task<string?> ValidarLigacaoAsync(PeDono dono, PeCampo campo, List<long> ids, long? registroId)
    {
        if (ids.Count == 0) return null;
        if (PeValores.Booleano(campo.Config, "multipla") != true && ids.Count > 1) return "Escolha só um item.";
        if (registroId != null && ids.Contains(registroId.Value)) return "O registro não pode ligar a si mesmo.";

        IQueryable<PeRegistro> validos;
        if (campo.Tipo == PeDominios.TipoCampo.LigacaoSecao)
        {
            var chave = PeConfigCampo.SecaoDaLigacao(campo.Config);
            var alvo = await _context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Chave == chave);
            if (alvo == null) return "A seção ligada não existe mais. Atualize a tela.";
            // Só registros do mesmo dono (a mesma versão do PETIC-DF, o catálogo do DF)
            validos = RegistrosDo(dono).Where(r => r.SecaoId == alvo.Id);
        }
        else
        {
            var catalogo = PeValores.Texto(campo.Config, "catalogo");
            if (catalogo == PeDominios.Catalogo.PgiaSistema) return "Este catálogo chega numa próxima entrega.";
            var fonte = await CatalogoFonteAsync(catalogo);
            if (fonte == null) return "O catálogo está vazio: não há o que ligar.";
            validos = RegistrosDo(fonte.Dono).Where(r => r.SecaoId == fonte.Secao.Secao.Id);
        }

        var encontrados = await validos.Where(r => ids.Contains(r.Id)).CountAsync();
        return encontrados == ids.Count ? null : "Um dos itens escolhidos não pode ser ligado aqui. Atualize a tela.";
    }

    private void Aplicar(PeRegistro registro, PeGravacao gravacao)
    {
        registro.Dados = gravacao.Dados.ToJsonString(PeModeloService.JsonHistorico);
        _context.PeVinculos.RemoveRange(gravacao.VinculosRemovidos);
        foreach (var (campoId, destinoId) in gravacao.VinculosNovos)
            _context.PeVinculos.Add(new PeVinculo { RegistroOrigem = registro, CampoId = campoId, RegistroDestinoId = destinoId });
    }

    private static void DarDono(PeGravacao gravacao, long registroId, PeUserContext ctx, DateTime agora)
    {
        foreach (var arquivo in gravacao.Arquivos)
        {
            arquivo.DonoTipo = PeDominios.DonoArquivo.Registro;
            arquivo.DonoId = registroId;
            arquivo.AlteradoEm = agora;
            arquivo.AlteradoPor = ctx.Email;
        }
    }

    /// <summary>Gravar um registro da versão do PETIC-DF marca a versão como alterada (e confere a situação).</summary>
    private static void Tocar(PePetic? petic, PeUserContext ctx, DateTime agora)
    {
        if (petic == null) return;
        petic.AlteradoEm = agora;
        petic.AlteradoPor = ctx.Email;
    }

    // ── Dono, seção e registros ─────────────────────────────────────────────

    /// <summary>
    /// Confere quem chama e o dono. Ler: qualquer papel do módulo. Escrever: pe_admin e admin
    /// geral, e, no PETIC-DF, só a versão em rascunho.
    /// </summary>
    private async Task<PeDonoAberto> AbrirAsync(PeDono dono, PeUserContext ctx, bool escrita)
    {
        if (!_permissoes.PodeLerReferenciais(ctx)) throw SemPermissaoDeLer();

        PePetic? petic = null;
        if (dono.Tipo == PeDominios.DonoRegistro.Petic)
        {
            var consulta = escrita ? _context.PePetics : _context.PePetics.AsNoTracking();
            petic = await consulta.FirstOrDefaultAsync(p => p.Id == dono.PeticId);
            // Papel de órgão só vê as versões aprovadas: rascunho e em deliberação "não existem" para ele
            if (petic == null || !_permissoes.PodeVerVersaoPetic(ctx, petic.Situacao))
                throw new ApiException(ErrorCode.PePeticNaoEncontrado, "Versão do PETIC-DF não encontrada. Atualize a tela.");
        }

        var papelEdita = _permissoes.PodeEditarReferenciais(ctx);
        var aberta = petic == null || petic.Situacao == PeDominios.SituacaoPetic.Rascunho;
        if (escrita)
        {
            if (!papelEdita)
                throw new ApiException(ErrorCode.PeSemPermissao, "Só o administrador do módulo edita o PETIC-DF, os princípios e as diretrizes do ciclo.");
            if (!aberta) throw VersaoFechada(petic!);
        }
        return new PeDonoAberto(dono, petic, papelEdita && aberta);
    }

    /// <summary>Versão do PETIC-DF fora do rascunho: 409 com a mensagem da situação.</summary>
    internal static ApiException VersaoFechada(PePetic petic) => new(ErrorCode.PeVersaoFechada,
        petic.Situacao == PeDominios.SituacaoPetic.EmDeliberacao
            ? "Esta versão está com o CGTIC e não muda até a decisão."
            : "Esta versão do PETIC-DF já foi aprovada e não muda. Para mudar, crie uma versão nova.");

    /// <summary>Registros de um dono. A E4 acrescenta o PDTIC (e o catálogo do DF passa a exigir pdtic_id nulo).</summary>
    public IQueryable<PeRegistro> RegistrosDo(PeDono dono) => dono.Tipo == PeDominios.DonoRegistro.Petic
        ? _context.PeRegistros.Where(r => r.PeticId == dono.PeticId)
        : _context.PeRegistros.Where(r => r.PeticId == null);

    private async Task<PeRegistro> RegistroParaEscritaAsync(PeDono dono, PeSecaoDoDono secao, long id)
    {
        var registro = await RegistrosDo(dono).FirstOrDefaultAsync(r => r.Id == id && r.SecaoId == secao.Secao.Id)
            ?? throw new ApiException(ErrorCode.PeRegistroNaoEncontrado, "Registro não encontrado. Atualize a tela.");
        if (registro.Sistema)
            throw new ApiException(ErrorCode.PeRegistroDoSistema,
                "Este registro veio do decreto (registro do sistema) e não pode ser mudado nem apagado.");
        return registro;
    }

    /// <summary>A seção do dono pela chave: do escopo do dono, não apagada e não desligada.</summary>
    private async Task<PeSecaoDoDono> SecaoAsync(PeDono dono, string chave)
    {
        var texto = chave?.Trim() ?? string.Empty;
        var secao = await _context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Chave == texto);
        // Antes de o carregador trazer os referenciais (versão 2 do modelo inicial), a seção
        // ainda não existe: é o intervalo da atualização, não um endereço errado
        if (secao == null && await ReferenciaisAindaNaoCarregadosAsync()) throw PeModeloService.ModeloIndisponivel();
        if (secao == null || secao.Escopo != dono.Escopo || secao.PassoId != null || secao.ExcluidoEm != null)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não existe aqui. Atualize a tela.");
        if (!SecaoVisivel(secao))
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção está desligada no modelo.");
        return (await MontarAsync(new List<PeSecao> { secao }))[0];
    }

    /// <summary>A versão do modelo inicial gravada é anterior à que traz as seções do DF e do PETIC-DF.</summary>
    private async Task<bool> ReferenciaisAindaNaoCarregadosAsync()
    {
        var valor = await _context.PeConfiguracoes.AsNoTracking()
            .Where(c => c.Chave == PeConfiguracao.ChaveVersaoModelo)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync();
        return !int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var versao)
               || versao < PeCarregadorModelo.VersaoDosReferenciais;
    }

    /// <summary>As seções visíveis do dono, na ordem (as do PETIC-DF ou as do DF).</summary>
    private async Task<List<PeSecao>> SecoesDoDonoAsync(PeDono dono, bool soNaPlanilha)
    {
        var escopo = dono.Escopo;
        var secoes = await _context.PeSecoes.AsNoTracking()
            .Where(s => s.Escopo == escopo && s.PassoId == null && s.ExcluidoEm == null)
            .OrderBy(s => s.Ordem).ThenBy(s => s.Id)
            .ToListAsync();
        return secoes.Where(s => SecaoVisivel(s) && (!soNaPlanilha || s.NaPlanilha)).ToList();
    }

    private static bool SecaoVisivel(PeSecao secao) =>
        secao.ExcluidoEm == null && secao.SituacaoGeral is not (null or PeDominios.Situacao.Desligado);

    /// <summary>
    /// Campo visível fora do PDTIC: não apagado, situação geral ligada e, se for ligação com
    /// seção, a seção ligada também aparece (tabela do mesmo escopo, não apagada, ligada).
    /// </summary>
    private static bool CampoVisivel(PeCampo campo, PeSecao secao, IReadOnlyList<PeSecao> alvos)
    {
        if (campo.ExcluidoEm != null || campo.SituacaoGeral is null or PeDominios.Situacao.Desligado) return false;
        if (campo.Tipo != PeDominios.TipoCampo.LigacaoSecao) return true;
        var chave = PeConfigCampo.SecaoDaLigacao(campo.Config);
        return alvos.Any(a => a.Chave == chave && a.Escopo == secao.Escopo && a.Tipo == PeDominios.TipoSecao.Tabela && SecaoVisivel(a));
    }

    private async Task<List<PeSecaoDoDono>> MontarAsync(List<PeSecao> secoes)
    {
        if (secoes.Count == 0) return new List<PeSecaoDoDono>();

        var ids = secoes.Select(s => s.Id).ToList();
        var campos = await _context.PeCampos.AsNoTracking()
            .Where(c => ids.Contains(c.SecaoId))
            .OrderBy(c => c.Ordem).ThenBy(c => c.Id)
            .ToListAsync();
        var idsCampos = campos.Select(c => c.Id).ToList();
        var opcoes = await _context.PeOpcoes.AsNoTracking()
            .Where(o => idsCampos.Contains(o.CampoId))
            .OrderBy(o => o.Ordem).ThenBy(o => o.Id)
            .ToListAsync();
        var chavesAlvo = campos.Where(c => c.Tipo == PeDominios.TipoCampo.LigacaoSecao)
            .Select(c => PeConfigCampo.SecaoDaLigacao(c.Config))
            .Where(k => k != null).Select(k => k!).Distinct().ToList();
        var alvos = chavesAlvo.Count == 0
            ? new List<PeSecao>()
            : await _context.PeSecoes.AsNoTracking().Where(s => chavesAlvo.Contains(s.Chave)).ToListAsync();

        return secoes.Select(secao =>
        {
            var daSecao = campos.Where(c => c.SecaoId == secao.Id).ToList();
            var idsDaSecao = daSecao.Select(c => c.Id).ToHashSet();
            return new PeSecaoDoDono
            {
                Secao = secao,
                Campos = daSecao,
                Visiveis = daSecao.Where(c => CampoVisivel(c, secao, alvos))
                    .Select(c => new PeCampoVisivel(c, c.SituacaoGeral == PeDominios.Situacao.Obrigatorio))
                    .ToList(),
                Opcoes = opcoes.Where(o => idsDaSecao.Contains(o.CampoId))
                    .GroupBy(o => o.CampoId)
                    .ToDictionary(g => g.Key, g => g.ToList())
            };
        }).ToList();
    }

    /// <summary>
    /// De onde saem os itens de um catálogo: os do PETIC-DF, da versão vigente (sem vigente,
    /// nulo); o de princípios, do catálogo do DF. Seção apagada ou desligada: nulo.
    /// </summary>
    private async Task<PeFonteCatalogo?> CatalogoFonteAsync(string? catalogo)
    {
        if (catalogo == null || !PeDominios.Catalogo.SecaoDoCatalogo.TryGetValue(catalogo, out var chaveSecao)) return null;

        PeDono dono;
        if (PeDominios.Catalogo.DoPetic(catalogo))
        {
            var vigente = await _context.PePetics.AsNoTracking()
                .Where(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado)
                .Select(p => (long?)p.Id)
                .FirstOrDefaultAsync();
            if (vigente == null) return null;
            dono = PeDono.DoPetic(vigente.Value);
        }
        else
        {
            dono = PeDono.Df;
        }

        var secao = await _context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Chave == chaveSecao);
        if (secao == null || secao.Escopo != dono.Escopo || secao.PassoId != null || !SecaoVisivel(secao)) return null;
        return new PeFonteCatalogo(dono, (await MontarAsync(new List<PeSecao> { secao }))[0]);
    }

    /// <summary>A seção tem ligação visível com um catálogo do PETIC-DF e ainda não há versão vigente.</summary>
    private async Task<bool> SemVigenteAsync(PeSecaoDoDono secao)
    {
        if (!secao.Visiveis.Any(v => EhCatalogoDoPetic(v.Campo))) return false;
        return !await _context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado);
    }

    private static bool EhCatalogoDoPetic(PeCampo campo) =>
        campo.Tipo == PeDominios.TipoCampo.LigacaoCatalogo && PeDominios.Catalogo.DoPetic(PeValores.Texto(campo.Config, "catalogo"));

    /// <summary>Sem PETIC-DF vigente, a ligação com os catálogos dele fica opcional em todos os níveis.</summary>
    private static bool ObrigatorioEfetivo(PeCampoVisivel visivel, bool semVigente) =>
        visivel.Obrigatorio && !(semVigente && EhCatalogoDoPetic(visivel.Campo));

    // ── Código e sequência ──────────────────────────────────────────────────

    private async Task<PeRegistroSequencia> SequenciaAsync(long secaoId, PeDono dono)
    {
        var chave = dono.Chave;
        var sequencia = await _context.PeRegistroSequencias.FirstOrDefaultAsync(s => s.SecaoId == secaoId && s.Dono == chave);
        if (sequencia != null) return sequencia;

        // Sem linha ainda: começa depois do maior código que já exista
        var codigos = await RegistrosDo(dono)
            .Where(r => r.SecaoId == secaoId && r.Codigo != null)
            .Select(r => r.Codigo!)
            .ToListAsync();
        sequencia = new PeRegistroSequencia
        {
            SecaoId = secaoId,
            Dono = chave,
            Ultimo = codigos.Select(NumeroDoCodigo).DefaultIfEmpty(0).Max()
        };
        _context.PeRegistroSequencias.Add(sequencia);
        return sequencia;
    }

    /// <summary>Código do registro: prefixo + número com dois dígitos no mínimo (OE01, N12, A100).</summary>
    private static string? CodigoDe(PeSecaoDoDono secao, int numero) =>
        secao.EhFormulario || string.IsNullOrEmpty(secao.Secao.PrefixoCodigo)
            ? null
            : secao.Secao.PrefixoCodigo + numero.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>O número no fim do código ("OE12" = 12; sem número = 0).</summary>
    public static int NumeroDoCodigo(string codigo)
    {
        var fim = codigo.Length;
        while (fim > 0 && char.IsAsciiDigit(codigo[fim - 1])) fim--;
        return fim < codigo.Length && int.TryParse(codigo[fim..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    // ── Respostas ───────────────────────────────────────────────────────────

    private async Task<PeRegistrosResponse> RespostaDaSecaoAsync(PeDonoAberto aberto, PeSecaoDoDono secao)
    {
        var registros = await RegistrosDo(aberto.Dono).AsNoTracking()
            .Where(r => r.SecaoId == secao.Secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        var semVigente = await SemVigenteAsync(secao);

        return new PeRegistrosResponse
        {
            Secao = new PeRegistroSecaoResponse
            {
                Id = secao.Secao.Id,
                Chave = secao.Secao.Chave,
                Titulo = secao.Secao.Titulo,
                Ajuda = secao.Secao.Ajuda,
                Tipo = secao.Secao.Tipo,
                PrefixoCodigo = secao.Secao.PrefixoCodigo,
                Campos = secao.Visiveis.Select(v => new PeTrilhaCampo
                {
                    Id = v.Campo.Id,
                    Chave = v.Campo.Chave,
                    Rotulo = v.Campo.Rotulo,
                    Ajuda = v.Campo.Ajuda,
                    Tipo = v.Campo.Tipo,
                    Config = PeModeloDados.Json(v.Campo.Config),
                    Obrigatorio = ObrigatorioEfetivo(v, semVigente),
                    Principal = v.Campo.Principal,
                    Largura = v.Campo.Largura,
                    Opcoes = secao.OpcoesDe(v.Campo).Where(o => o.Ativa)
                        .Select(o => new PeTrilhaOpcao { Valor = o.Valor, Rotulo = o.Rotulo, Cor = o.Cor })
                        .ToList()
                }).ToList()
            },
            Registros = await ResponderAsync(secao, registros),
            PodeEditar = aberto.PodeEditar
        };
    }

    private async Task<PeSecaoExportada> ExportadaAsync(PeDono dono, PeSecaoDoDono secao)
    {
        var registros = await RegistrosDo(dono).AsNoTracking()
            .Where(r => r.SecaoId == secao.Secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        return new PeSecaoExportada
        {
            Modelo = secao,
            Colunas = secao.Visiveis.Where(v => v.Campo.NaPlanilha).ToList(),
            Registros = await ResponderAsync(secao, registros)
        };
    }

    private async Task<PeRegistroResponse> UmaRespostaAsync(PeSecaoDoDono secao, long id)
    {
        var registro = await _context.PeRegistros.AsNoTracking().FirstAsync(r => r.Id == id);
        return (await ResponderAsync(secao, new List<PeRegistro> { registro }))[0];
    }

    private async Task<List<PeRegistroResponse>> ResponderAsync(PeSecaoDoDono secao, List<PeRegistro> registros)
    {
        var ids = registros.Select(r => r.Id).ToList();
        var temLigacao = secao.Visiveis.Any(v => PeRegistroDados.EhLigacao(v.Campo));
        var ligacoes = temLigacao && ids.Count > 0
            ? await _context.PeVinculos.AsNoTracking().Where(v => ids.Contains(v.RegistroOrigemId)).ToListAsync()
            : new List<PeVinculo>();
        var destinos = await ResumosAsync(ligacoes.Select(v => v.RegistroDestinoId));
        var porOrigem = ligacoes.ToLookup(v => v.RegistroOrigemId);

        return registros.Select(r => Resposta(secao, r, porOrigem[r.Id], destinos)).ToList();
    }

    private static PeRegistroResponse Resposta(PeSecaoDoDono secao, PeRegistro registro, IEnumerable<PeVinculo> ligacoes,
        IReadOnlyDictionary<long, PeVinculoResponse> destinos)
    {
        var dados = PeRegistroDados.Ler(registro.Dados);
        var resposta = new PeRegistroResponse
        {
            Id = registro.Id,
            Codigo = registro.Codigo,
            Ordem = registro.Ordem,
            Sistema = registro.Sistema,
            CriadoEm = registro.CriadoEm,
            CriadoPor = registro.CriadoPor,
            AlteradoEm = registro.AlteradoEm,
            AlteradoPor = registro.AlteradoPor
        };
        var lista = ligacoes.ToList();

        foreach (var visivel in secao.Visiveis)
        {
            var campo = visivel.Campo;
            if (PeRegistroDados.EhLigacao(campo))
            {
                var ligados = lista.Where(v => v.CampoId == campo.Id)
                    .Select(v => destinos.GetValueOrDefault(v.RegistroDestinoId))
                    .Where(d => d != null).Select(d => d!)
                    .OrderBy(d => d.Ordem).ThenBy(d => d.RegistroId)
                    .ToList();
                resposta.Vinculos[campo.Chave] = ligados;
                if (ligados.Count > 0)
                    resposta.Rotulos[campo.Chave] = string.Join(", ", ligados.Select(d => d.Codigo ?? d.Resumo));
                continue;
            }

            var valor = dados[campo.Chave];
            if (PeRegistroDados.EhVazio(valor)) continue;
            resposta.Dados[campo.Chave] = JsonSerializer.SerializeToElement(valor);
            if (PeValores.Rotulo(campo, secao.OpcoesDe(campo), valor) is string rotulo)
                resposta.Rotulos[campo.Chave] = rotulo;
        }
        return resposta;
    }

    /// <summary>Código e resumo (o campo principal) de cada registro ligado, em lote.</summary>
    private async Task<Dictionary<long, PeVinculoResponse>> ResumosAsync(IEnumerable<long> ids)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<long, PeVinculoResponse>();

        var alvos = await _context.PeRegistros.AsNoTracking().Where(r => lista.Contains(r.Id)).ToListAsync();
        var idsSecoes = alvos.Select(a => a.SecaoId).Distinct().ToList();
        var secoes = await _context.PeSecoes.AsNoTracking().Where(s => idsSecoes.Contains(s.Id)).ToListAsync();
        var montadas = (await MontarAsync(secoes)).ToDictionary(m => m.Secao.Id);

        return alvos.ToDictionary(a => a.Id, a => new PeVinculoResponse
        {
            RegistroId = a.Id,
            Codigo = a.Codigo,
            Resumo = ResumoDe(montadas[a.SecaoId], a),
            Ordem = a.Ordem
        });
    }

    private static string ResumoDe(PeSecaoDoDono secao, PeRegistro registro)
    {
        var campo = secao.CampoDoResumo();
        var texto = campo == null
            ? null
            : PeValores.Rotulo(campo, secao.OpcoesDe(campo), PeRegistroDados.Ler(registro.Dados)[campo.Chave]);
        return PeValores.Resumo(texto ?? registro.Codigo ?? string.Empty);
    }

    /// <summary>"Não dá para apagar: este registro está ligado a OE02 (Objetivos estratégicos, PETIC-DF 2.0)..."</summary>
    private async Task<string> MensagemLigadoPorAsync(List<long> origens)
    {
        var registros = await _context.PeRegistros.AsNoTracking()
            .Where(r => origens.Contains(r.Id))
            .Select(r => new { r.Id, r.Codigo, r.SecaoId, r.PeticId, r.Ordem })
            .ToListAsync();
        var idsSecoes = registros.Select(r => r.SecaoId).Distinct().ToList();
        var secoes = await _context.PeSecoes.AsNoTracking().Where(s => idsSecoes.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Titulo);
        var idsPetic = registros.Where(r => r.PeticId != null).Select(r => r.PeticId!.Value).Distinct().ToList();
        var versoes = await _context.PePetics.AsNoTracking().Where(p => idsPetic.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Versao);

        var nomes = registros
            .OrderBy(r => r.PeticId ?? 0).ThenBy(r => r.SecaoId).ThenBy(r => r.Ordem)
            .Select(r =>
            {
                var onde = secoes.GetValueOrDefault(r.SecaoId, "outra seção");
                if (r.PeticId != null) onde += $", PETIC-DF {versoes.GetValueOrDefault(r.PeticId.Value, "?")}";
                return r.Codigo != null ? $"{r.Codigo} ({onde})" : onde;
            })
            .Distinct()
            .ToList();
        var texto = nomes.Count <= 5
            ? string.Join(", ", nomes)
            : string.Join(", ", nomes.Take(5)) + $" e mais {nomes.Count - 5}";
        return $"Não dá para apagar: este registro está ligado a {texto}. Tire essas ligações antes.";
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static bool MesmaPessoa(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static ApiException SemPermissaoDeLer() => new(ErrorCode.PeSemPermissao,
        "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");
}
