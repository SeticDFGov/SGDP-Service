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

/// <summary>Uma seção do dono com os registros e o que falta em cada um, pelo modelo de hoje.</summary>
public sealed class PeSecaoAnalisada
{
    public required PeSecaoDoDono Secao { get; init; }

    // Na ordem da seção
    public required List<PeRegistro> Registros { get; init; }

    // Registros com campo obrigatório vazio e os campos que faltam
    public required List<(PeRegistro Registro, List<PeCampo> Faltando)> Incompletos { get; init; }

    // Seção obrigatória ainda sem registro
    public bool FaltaRegistro => Secao.Obrigatoria && Registros.Count == 0;

    public bool Completa => !FaltaRegistro && Incompletos.Count == 0;
}

/// <summary>
/// Uma linha sugerida pelo sistema (o cronograma que vem dos fluxos): os textos pela chave do
/// campo e, por campo de ligação com a própria seção, as posições (na lista da sugestão) das
/// linhas anteriores ligadas.
/// </summary>
public sealed class PeRegistroSugerido
{
    public Dictionary<string, string> Textos { get; init; } = new();

    public Dictionary<string, List<int>> Ligacoes { get; init; } = new();
}

/// <summary>A análise de um dono: as seções e as ligações que saem dos registros delas.</summary>
public sealed class PeAnaliseDono
{
    public required List<PeSecaoAnalisada> Secoes { get; init; }

    public required List<PeVinculo> Vinculos { get; init; }

    public PeSecaoAnalisada? Secao(string chave) => Secoes.FirstOrDefault(s => s.Secao.Secao.Chave == chave);
}

/// <summary>
/// Motor de registros (E3). Para o PETIC-DF e o catálogo do DF, a seção e os campos
/// aparecem pela situação geral (desligado some); para o PDTIC de um órgão (E4), pela trilha
/// do órgão (nível e ajustes; <see cref="PeTrilhaOrgao"/>). Regras de cada gravação:
/// <list type="bullet">
/// <item>só os campos visíveis entram; campo escondido (desligado ou apagado) guarda o valor
/// que já tinha e não é exigido (decisão 13: guarda e esconde);</item>
/// <item>obrigatório, tipo, tamanho, faixa, casas, opção ativa (a desativada só vale se já
/// era a guardada), arquivo enviado pela mesma pessoa e ainda sem dono, texto rico pela
/// lista fechada (<see cref="PeTextoRico"/>) com as imagens do próprio módulo;</item>
/// <item>ligações só com registros do mesmo dono (ligação com seção) ou do catálogo (os do
/// PETIC-DF, da vigente; sem vigente, a ligação fica opcional; os sistemas de IA do PGIA,
/// só do órgão do PDTIC, com os ids no jsonb); múltipla ou uma só;</item>
/// <item>calculados calculados aqui e guardados no jsonb;</item>
/// <item>código pelo prefixo da seção e pela sequência do dono (nunca reaproveita);</item>
/// <item>registro do sistema não se edita nem se apaga; registro ligado por outro não se apaga.</item>
/// </list>
/// Gravar um registro do PETIC-DF ou do PDTIC toca a versão (alterado em e por) com a
/// situação como token de concorrência: enviar e gravar ao mesmo tempo não passam os dois.
/// No PDTIC, a vigência da seção abrangencia (passo 1.1) é copiada para o pe_pdtic.
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

    private sealed record PeDonoAberto(PeDono Dono, PePetic? Petic, PePdtic? Pdtic, PeTrilhaOrgao? Trilha, bool PodeEditar);

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
        var secao = await SecaoAsync(aberto, secaoChave);
        return await RespostaDaSecaoAsync(aberto, secao);
    }

    public async Task<List<PeCatalogoItemResponse>> CatalogoAsync(string catalogo, PeUserContext ctx, long? pdticId = null)
    {
        if (!_permissoes.PodeLerReferenciais(ctx)) throw SemPermissaoDeLer();
        var chave = catalogo?.Trim() ?? string.Empty;

        // Sistemas de IA do inventário do PGIA, do órgão daquele PDTIC (E4)
        if (PeDominios.Catalogo.DoPgia(chave))
        {
            if (pdticId == null)
                throw new ApiException(ErrorCode.PeDadosInvalidos, "Diga de qual PDTIC (pdticId) são os sistemas de IA.");
            var orgaoId = (await PePdticService.LerAsync(_context, _permissoes, pdticId.Value, ctx)).OrgaoId;
            return await _context.PgiaSistemasIa.AsNoTracking()
                .Where(s => s.OrgaoId == orgaoId)
                .OrderBy(s => s.Denominacao).ThenBy(s => s.Id)
                .Select(s => new PeCatalogoItemResponse { Id = s.Id, Codigo = null, Rotulo = s.Denominacao })
                .ToListAsync();
        }

        if (!PeDominios.Catalogo.SecaoDoCatalogo.ContainsKey(chave))
            throw new ApiException(ErrorCode.PeCatalogoNaoEncontrado,
                "Catálogo não encontrado. Os catálogos são petic_objetivo, petic_eixo, principio e pgia_sistema.");

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
        var trilha = await TrilhaDoDonoAsync(dono);
        var secoes = trilha != null
            ? trilha.SecoesMontadas()
            : await MontarAsync(await SecoesForaDoPdticAsync(dono.Escopo, soNaPlanilha: false));
        var analise = await AnalisarAsync(dono, secoes);

        var pendencias = new List<string>();
        foreach (var secao in analise.Secoes)
        {
            if (secao.Registros.Count == 0)
            {
                if (secao.Secao.Obrigatoria)
                    pendencias.Add(secao.Secao.EhFormulario
                        ? $"Preencha \"{secao.Secao.Secao.Titulo}\"."
                        : $"Inclua pelo menos um item em \"{secao.Secao.Secao.Titulo}\".");
                continue;
            }
            foreach (var (registro, faltando) in secao.Incompletos)
                pendencias.Add($"{registro.Codigo ?? secao.Secao.Secao.Titulo}: preencha {string.Join(", ", faltando.Select(c => $"\"{c.Rotulo}\""))}.");
        }
        return pendencias;
    }

    public async Task<PeAnaliseDono> AnalisarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes)
    {
        var ids = secoes.Select(s => s.Secao.Id).ToList();
        var registros = ids.Count == 0
            ? new List<PeRegistro>()
            : await RegistrosDo(dono).AsNoTracking()
                .Where(r => ids.Contains(r.SecaoId))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();
        var idsRegistros = registros.Select(r => r.Id).ToList();
        var vinculos = idsRegistros.Count == 0
            ? new List<PeVinculo>()
            : await _context.PeVinculos.AsNoTracking().Where(v => idsRegistros.Contains(v.RegistroOrigemId)).ToListAsync();
        var ligacoes = vinculos.Select(v => (v.RegistroOrigemId, v.CampoId)).ToHashSet();
        var semVigente = secoes.Any(s => s.Visiveis.Any(v => EhCatalogoDoPetic(v.Campo)))
                         && await PeTrilhaOrgao.SemPeticVigenteAsync(_context);

        var analisadas = secoes.Select(secao =>
        {
            var doSecao = registros.Where(r => r.SecaoId == secao.Secao.Id).ToList();
            var incompletos = doSecao
                .Select(r => (Registro: r, Faltando: Faltando(secao, r, ligacoes, semVigente)))
                .Where(x => x.Faltando.Count > 0)
                .ToList();
            return new PeSecaoAnalisada { Secao = secao, Registros = doSecao, Incompletos = incompletos };
        }).ToList();
        return new PeAnaliseDono { Secoes = analisadas, Vinculos = vinculos };
    }

    /// <summary>Os campos obrigatórios (visíveis, fora os calculados) que o registro deixou vazios.</summary>
    private static List<PeCampo> Faltando(PeSecaoDoDono secao, PeRegistro registro, ISet<(long, long)> ligacoes, bool semVigente)
    {
        var dados = PeRegistroDados.Ler(registro.Dados);
        return secao.Visiveis
            .Where(v => ObrigatorioEfetivo(v, semVigente) && v.Campo.Tipo != PeDominios.TipoCampo.Calculado)
            .Where(v => PeRegistroDados.EhLigacaoPorVinculo(v.Campo)
                ? !ligacoes.Contains((registro.Id, v.Campo.Id))
                : PeRegistroDados.EhVazio(dados[v.Campo.Chave]))
            .Select(v => v.Campo)
            .ToList();
    }

    public async Task<PeSecaoExportada> ExportarSecaoAsync(PeDono dono, string secaoChave, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: false);
        var secao = await SecaoAsync(aberto, secaoChave);
        if (!secao.Secao.NaPlanilha)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não vai para a planilha: o administrador a deixou de fora.");
        return await ExportadaAsync(dono, secao);
    }

    public async Task<List<PeSecaoExportada>> ExportarSecoesAsync(PeDono dono, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: false);
        var saida = new List<PeSecaoExportada>();
        foreach (var secao in await SecoesDoDonoAsync(aberto, soNaPlanilha: true))
            saida.Add(await ExportadaAsync(dono, secao));
        return saida;
    }

    public async Task<Dictionary<long, List<PeRegistroResponse>>> ExportarDosPdticsAsync(long secaoId,
        IReadOnlyList<(long PdticId, PeSecaoDoDono Secao)> pdtics)
    {
        var ids = pdtics.Select(p => p.PdticId).Distinct().ToList();
        var registros = ids.Count == 0
            ? new List<PeRegistro>()
            : await _context.PeRegistros.AsNoTracking()
                .Where(r => r.SecaoId == secaoId && r.PdticId != null && ids.Contains(r.PdticId.Value))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();

        // Uma consulta por tipo de dado para todos os órgãos (o consolidado passa por dezenas de órgãos)
        var idsRegistros = registros.Select(r => r.Id).ToList();
        var comLigacao = pdtics.Any(p => p.Secao.Visiveis.Any(v => PeRegistroDados.EhLigacaoPorVinculo(v.Campo)));
        var ligacoes = comLigacao && idsRegistros.Count > 0
            ? await _context.PeVinculos.AsNoTracking().Where(v => idsRegistros.Contains(v.RegistroOrigemId)).ToListAsync()
            : new List<PeVinculo>();
        var destinos = await ResumosAsync(ligacoes.Select(v => v.RegistroDestinoId));
        var porOrigem = ligacoes.ToLookup(v => v.RegistroOrigemId);
        var camposPgia = pdtics.SelectMany(p => p.Secao.Visiveis)
            .Where(v => PeRegistroDados.EhLigacaoPgia(v.Campo))
            .Select(v => v.Campo.Chave)
            .Distinct()
            .ToList();
        var sistemas = await SistemasPgiaAsync(camposPgia, registros);

        var porPdtic = registros.ToLookup(r => r.PdticId!.Value);
        return pdtics.GroupBy(p => p.PdticId).ToDictionary(g => g.Key, g =>
        {
            var secao = g.First().Secao;
            return porPdtic[g.Key].Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas)).ToList();
        });
    }

    public async Task<List<PeSecaoExportada>> ExportarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes)
    {
        var ids = secoes.Select(s => s.Secao.Id).Distinct().ToList();
        var registros = ids.Count == 0
            ? new List<PeRegistro>()
            : await RegistrosDo(dono).AsNoTracking()
                .Where(r => ids.Contains(r.SecaoId))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();

        // Poucas consultas para todas as seções: ligações, resumos dos ligados e sistemas do PGIA
        var idsRegistros = registros.Select(r => r.Id).ToList();
        var comLigacao = secoes.Any(s => s.Visiveis.Any(v => PeRegistroDados.EhLigacaoPorVinculo(v.Campo)));
        var ligacoes = comLigacao && idsRegistros.Count > 0
            ? await _context.PeVinculos.AsNoTracking().Where(v => idsRegistros.Contains(v.RegistroOrigemId)).ToListAsync()
            : new List<PeVinculo>();
        var destinos = await ResumosAsync(ligacoes.Select(v => v.RegistroDestinoId));
        var porOrigem = ligacoes.ToLookup(v => v.RegistroOrigemId);
        var camposPgia = secoes.SelectMany(s => s.Visiveis)
            .Where(v => PeRegistroDados.EhLigacaoPgia(v.Campo))
            .Select(v => v.Campo.Chave)
            .Distinct()
            .ToList();
        var sistemas = await SistemasPgiaAsync(camposPgia, registros);

        var porSecao = registros.ToLookup(r => r.SecaoId);
        return secoes.Select(secao => new PeSecaoExportada
        {
            Modelo = secao,
            Colunas = secao.Visiveis.ToList(),
            Registros = porSecao[secao.Secao.Id].Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas)).ToList()
        }).ToList();
    }

    // ── Escrita ─────────────────────────────────────────────────────────────

    public async Task<PeRegistroResponse> CriarAsync(PeDono dono, string secaoChave, PeRegistroSalvarDTO dto, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
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
            PdticId = dono.PdticId,
            Codigo = CodigoDe(secao, sequencia.Ultimo),
            Ordem = (await doDono.MaxAsync(r => (int?)r.Ordem) ?? 0) + 1,
            Sistema = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeRegistros.Add(registro);
        Aplicar(registro, gravacao);
        Tocar(aberto, ctx, agora);
        CopiarVigencia(aberto, secao, gravacao.Dados);
        IniciarAcompanhamento(aberto, secao, gravacao.Dados);

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
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var registro = await RegistroParaEscritaAsync(dono, secao, id);

        var gravacao = await ValidarAsync(aberto, secao, registro, dto, ctx);

        var agora = DateTime.UtcNow;
        Aplicar(registro, gravacao);
        DarDono(gravacao, registro.Id, ctx, agora);
        registro.AlteradoEm = agora;
        registro.AlteradoPor = ctx.Email;
        Tocar(aberto, ctx, agora);
        CopiarVigencia(aberto, secao, gravacao.Dados);
        IniciarAcompanhamento(aberto, secao, gravacao.Dados);
        await _context.SaveChangesAsync();

        return await UmaRespostaAsync(secao, registro.Id);
    }

    public async Task ExcluirAsync(PeDono dono, string secaoChave, long id, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
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
        Tocar(aberto, ctx, DateTime.UtcNow);
        CopiarVigencia(aberto, secao, null);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Linhas sugeridas pelo sistema numa tabela vazia (o cronograma que vem dos fluxos, E6): só
    /// os textos dados (nos campos de texto visíveis) e as ligações com linhas anteriores da
    /// mesma sugestão (nos campos de ligação visíveis com a própria seção). Os obrigatórios que
    /// ficam vazios (as datas) o órgão completa depois; até lá o passo fica pendente. Tabela com
    /// linhas: 409 PeCronogramaPreenchido. Tudo numa gravação só.
    /// </summary>
    public async Task<List<PeRegistroResponse>> CriarSugeridosAsync(PeDono dono, string secaoChave, IReadOnlyList<PeRegistroSugerido> linhas,
        PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        if (secao.EhFormulario)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "A sugestão só entra numa tabela.");
        var doDono = RegistrosDo(dono).Where(r => r.SecaoId == secao.Secao.Id);
        if (await doDono.AnyAsync())
            throw new ApiException(ErrorCode.PeCronogramaPreenchido,
                "O cronograma já tem linhas. A sugestão só entra no cronograma vazio: apague as linhas para sugerir de novo.");

        var visiveis = secao.Visiveis.ToDictionary(v => v.Campo.Chave, v => v.Campo);
        var agora = DateTime.UtcNow;
        var sequencia = await SequenciaAsync(secao.Secao.Id, dono);
        var criados = new List<PeRegistro>();
        foreach (var linha in linhas)
        {
            var dados = new JsonObject();
            foreach (var (chave, texto) in linha.Textos)
            {
                if (!visiveis.TryGetValue(chave, out var campo) || campo.Tipo is not (PeDominios.TipoCampo.TextoCurto or PeDominios.TipoCampo.TextoLongo))
                    continue;
                var maximo = PeValores.Inteiro(campo.Config, "max") ?? (campo.Tipo == PeDominios.TipoCampo.TextoCurto ? 1000 : 50000);
                var valor = (texto ?? string.Empty).Trim();
                if (valor.Length > maximo) valor = valor[..maximo].TrimEnd();
                if (valor.Length > 0) dados[chave] = valor;
            }
            sequencia.Ultimo++;
            var registro = new PeRegistro
            {
                SecaoId = secao.Secao.Id,
                PeticId = dono.PeticId,
                PdticId = dono.PdticId,
                Codigo = CodigoDe(secao, sequencia.Ultimo),
                Ordem = criados.Count + 1,
                Dados = dados.ToJsonString(PeModeloService.JsonHistorico),
                Sistema = false,
                CriadoEm = agora,
                CriadoPor = ctx.Email
            };
            _context.PeRegistros.Add(registro);

            // Ligações com as linhas anteriores da sugestão (a própria seção, sem ligar a si mesma)
            foreach (var (chave, indices) in linha.Ligacoes)
            {
                if (!visiveis.TryGetValue(chave, out var campo) || campo.Tipo != PeDominios.TipoCampo.LigacaoSecao
                    || PeConfigCampo.SecaoDaLigacao(campo.Config) != secao.Secao.Chave)
                    continue;
                var multipla = PeValores.Booleano(campo.Config, "multipla") == true;
                foreach (var i in indices.Where(i => i >= 0 && i < criados.Count).Distinct().Take(multipla ? int.MaxValue : 1))
                    _context.PeVinculos.Add(new PeVinculo { RegistroOrigem = registro, CampoId = campo.Id, RegistroDestino = criados[i] });
            }
            criados.Add(registro);
        }
        Tocar(aberto, ctx, agora);
        await _context.SaveChangesAsync();

        var ids = criados.Select(r => r.Id).ToList();
        var gravados = await _context.PeRegistros.AsNoTracking().Where(r => ids.Contains(r.Id)).OrderBy(r => r.Ordem).ToListAsync();
        return await ResponderAsync(secao, gravados);
    }

    public async Task<PeRegistrosResponse> OrdenarAsync(PeDono dono, string secaoChave, PeOrdemDTO dto, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var registros = await RegistrosDo(dono).Where(r => r.SecaoId == secao.Secao.Id).ToListAsync();

        var ids = dto.Ids ?? new List<long>();
        if (ids.Count != registros.Count || ids.Distinct().Count() != ids.Count || !registros.Select(r => r.Id).ToHashSet().SetEquals(ids))
            throw new ApiException(ErrorCode.PeOrdemInvalida, "Mande todos os registros da seção, cada um uma vez, na nova ordem.");

        for (var i = 0; i < ids.Count; i++)
            registros.Single(r => r.Id == ids[i]).Ordem = i + 1;
        Tocar(aberto, ctx, DateTime.UtcNow);
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
        var imagens = new List<(PeCampo Campo, IReadOnlyList<long> Ids)>();
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

                if (resultado.Imagens is { Count: > 0 } ids) imagens.Add((campo, ids));
            }

            if (visivel.Obrigatorio && PeRegistroDados.EhVazio(dados[campo.Chave]))
                erros[campo.Chave] = PeValores.MensagemObrigatorio(campo);
        }
        ValidarVigencia(dados, visiveis, erros);

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

        // Imagens do texto rico: PNG ou JPEG do próprio módulo, enviadas por quem grava e ainda
        // sem dono, ou já deste registro. Ganham este registro como dono (quem vê o registro vê a imagem).
        // Desde a E7, também a imagem de outra versão do PDTIC do mesmo órgão (a revisão copia os
        // textos com as imagens da versão revista): fica com o dono que já tem
        foreach (var (campo, ids) in imagens)
            foreach (var id in ids)
            {
                var arquivo = await _context.PeArquivos.FirstOrDefaultAsync(a => a.Id == id);
                string? erro = null;
                if (arquivo == null)
                    erro = "Uma das imagens não foi encontrada. Envie a imagem de novo.";
                else if (PeArquivoService.TipoDoArquivo(arquivo.Nome) is not ("png" or "jpg"))
                    erro = "No texto formatado entram só imagens PNG ou JPEG.";
                else if (atual != null && arquivo.DonoTipo == PeDominios.DonoArquivo.Registro && arquivo.DonoId == atual.Id)
                    continue;
                else if (aberto.Pdtic != null && await DoMesmoOrgaoAsync(_context, arquivo, aberto.Pdtic.OrgaoId))
                    continue;
                else if (arquivo.DonoTipo != null || !MesmaPessoa(arquivo.CriadoPor, ctx.Email))
                    erro = "Uma das imagens não pode ser usada aqui. Envie a imagem de novo.";
                if (erro != null)
                {
                    erros[campo.Chave] = erro;
                    break;
                }
                if (!gravacao.Arquivos.Contains(arquivo!)) gravacao.Arquivos.Add(arquivo!);
            }

        // Ligações (no PUT, Vinculos ausente mantém as que estavam)
        var guardadas = novo
            ? new List<PeVinculo>()
            : await _context.PeVinculos.Where(v => v.RegistroOrigemId == atual!.Id).ToListAsync();
        foreach (var visivel in secao.Visiveis.Where(v => PeRegistroDados.EhLigacao(v.Campo)))
        {
            var campo = visivel.Campo;
            if (erros.ContainsKey(campo.Chave)) continue;

            // Sistemas de IA do PGIA: os ids ficam no jsonb do registro
            if (PeRegistroDados.EhLigacaoPgia(campo))
            {
                var atuaisPgia = PeRegistroDados.Ids(guardados[campo.Chave]);
                var desejadosPgia = dto.Vinculos == null && !novo
                    ? atuaisPgia
                    : (dto.Vinculos != null && dto.Vinculos.TryGetValue(campo.Chave, out var idsPgia) ? idsPgia : new List<long>()).Distinct().ToList();
                var erroPgia = await ValidarLigacaoPgiaAsync(aberto, campo, desejadosPgia);
                if (erroPgia != null)
                {
                    erros[campo.Chave] = erroPgia;
                    continue;
                }
                if (visivel.Obrigatorio && desejadosPgia.Count == 0)
                {
                    erros[campo.Chave] = PeValores.MensagemObrigatorio(campo);
                    continue;
                }
                if (desejadosPgia.Count == 0) dados.Remove(campo.Chave);
                else dados[campo.Chave] = new JsonArray(desejadosPgia.Select(i => (JsonNode)JsonValue.Create(i)).ToArray());
                continue;
            }

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

    /// <summary>
    /// Vigência (início e fim): o fim não vem antes do início. Vale para toda seção com os dois
    /// campos de data (a abrangência do PDTIC, cuja vigência vai para o pe_pdtic).
    /// </summary>
    private static void ValidarVigencia(JsonObject dados, IReadOnlyDictionary<string, PeCampo> visiveis, Dictionary<string, string> erros)
    {
        var inicio = PeValores.DataGuardada(dados[PeDominios.ChavePdtic.CampoVigenciaInicio]);
        var fim = PeValores.DataGuardada(dados[PeDominios.ChavePdtic.CampoVigenciaFim]);
        if (inicio == null || fim == null || fim >= inicio) return;

        var alvo = visiveis.ContainsKey(PeDominios.ChavePdtic.CampoVigenciaFim) ? PeDominios.ChavePdtic.CampoVigenciaFim
            : visiveis.ContainsKey(PeDominios.ChavePdtic.CampoVigenciaInicio) ? PeDominios.ChavePdtic.CampoVigenciaInicio
            : null;
        if (alvo != null && !erros.ContainsKey(alvo)) erros[alvo] = "O fim da vigência não pode ser antes do início.";
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
            // Só registros do mesmo dono (a mesma versão do PETIC-DF, o mesmo PDTIC, o catálogo do DF)
            validos = RegistrosDo(dono).Where(r => r.SecaoId == alvo.Id);
        }
        else
        {
            var fonte = await CatalogoFonteAsync(PeValores.Texto(campo.Config, "catalogo"));
            if (fonte == null) return "O catálogo está vazio: não há o que ligar.";
            validos = RegistrosDo(fonte.Dono).Where(r => r.SecaoId == fonte.Secao.Secao.Id);
        }

        var encontrados = await validos.Where(r => ids.Contains(r.Id)).CountAsync();
        return encontrados == ids.Count ? null : "Um dos itens escolhidos não pode ser ligado aqui. Atualize a tela.";
    }

    /// <summary>Ligação com os sistemas de IA do PGIA: só no PDTIC e só os sistemas do órgão dele.</summary>
    private async Task<string?> ValidarLigacaoPgiaAsync(PeDonoAberto aberto, PeCampo campo, List<long> ids)
    {
        if (ids.Count == 0) return null;
        if (PeValores.Booleano(campo.Config, "multipla") != true && ids.Count > 1) return "Escolha só um item.";
        if (aberto.Pdtic == null) return "Os sistemas de IA do PGIA só se ligam no PDTIC de um órgão.";

        var orgaoId = aberto.Pdtic.OrgaoId;
        var encontrados = await _context.PgiaSistemasIa.AsNoTracking().CountAsync(s => s.OrgaoId == orgaoId && ids.Contains(s.Id));
        return encontrados == ids.Count ? null : "Um dos sistemas escolhidos não está no inventário do PGIA do órgão. Atualize a tela.";
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

    /// <summary>Gravar um registro da versão do PETIC-DF ou do PDTIC marca o dono como alterado (e confere a situação).</summary>
    private static void Tocar(PeDonoAberto aberto, PeUserContext ctx, DateTime agora)
    {
        if (aberto.Petic != null)
        {
            aberto.Petic.AlteradoEm = agora;
            aberto.Petic.AlteradoPor = ctx.Email;
        }
        if (aberto.Pdtic != null)
        {
            aberto.Pdtic.AlteradoEm = agora;
            aberto.Pdtic.AlteradoPor = ctx.Email;
        }
    }

    /// <summary>
    /// O acompanhamento começa (E7): gravar a aprovação do plano de acompanhamento (passo 4.6)
    /// com a decisão "aprovado" num PDTIC publicado o põe em acompanhamento (a rodada B
    /// acrescenta o outro gatilho, o primeiro ciclo de monitoramento com dado gravado). A
    /// situação é token de concorrência: a mudança vai na mesma gravação do registro.
    /// </summary>
    private static void IniciarAcompanhamento(PeDonoAberto aberto, PeSecaoDoDono secao, JsonObject dados)
    {
        if (aberto.Pdtic is not { Situacao: PeDominios.SituacaoPdtic.Publicado } pdtic
            || secao.Secao.Chave != PeDominios.ChavePdtic.SecaoAprovacaoPlanoAcompanhamento
            || PeRegistroDados.Texto(dados[PeDominios.ChavePdtic.CampoDecisao]) != PeDominios.Decisao.Aprovado)
            return;
        pdtic.Situacao = PeDominios.SituacaoPdtic.EmAcompanhamento;
    }

    /// <summary>
    /// O arquivo pertence a um registro ou a um PDTIC do órgão (qualquer versão): a revisão
    /// copia os textos e os campos com as imagens e os arquivos da versão revista, que ficam
    /// com o dono de antes e servem às duas versões.
    /// </summary>
    internal static async Task<bool> DoMesmoOrgaoAsync(AppDbContext context, PeArquivo arquivo, long orgaoId)
    {
        long? pdticDono = arquivo.DonoTipo switch
        {
            PeDominios.DonoArquivo.Registro when arquivo.DonoId != null => await context.PeRegistros.AsNoTracking()
                .Where(r => r.Id == arquivo.DonoId)
                .Select(r => r.PdticId)
                .FirstOrDefaultAsync(),
            PeDominios.DonoArquivo.Pdtic => arquivo.DonoId,
            _ => null
        };
        return pdticDono != null && await context.PePdtics.AsNoTracking().AnyAsync(p => p.Id == pdticDono && p.OrgaoId == orgaoId);
    }

    /// <summary>
    /// No PDTIC, a vigência da seção abrangencia (passo 1.1) vai para o pe_pdtic a cada
    /// gravação (nula quando o registro é apagado). Par invertido não é copiado (a validação
    /// já recusa quando os campos aparecem).
    /// </summary>
    private static void CopiarVigencia(PeDonoAberto aberto, PeSecaoDoDono secao, JsonObject? dados)
    {
        if (aberto.Pdtic == null || secao.Secao.Chave != PeDominios.ChavePdtic.SecaoAbrangencia) return;
        var inicio = dados == null ? null : PeValores.DataGuardada(dados[PeDominios.ChavePdtic.CampoVigenciaInicio]);
        var fim = dados == null ? null : PeValores.DataGuardada(dados[PeDominios.ChavePdtic.CampoVigenciaFim]);
        if (inicio != null && fim != null && fim < inicio) return;
        aberto.Pdtic.VigenciaInicio = inicio;
        aberto.Pdtic.VigenciaFim = fim;
    }

    // ── Dono, seção e registros ─────────────────────────────────────────────

    /// <summary>
    /// Confere quem chama e o dono. PETIC-DF e catálogo do DF: ler, qualquer papel do módulo;
    /// escrever, pe_admin e admin geral, e, no PETIC-DF, só a versão em rascunho. PDTIC (E4):
    /// ler, quem vê o órgão (papéis globais, admin geral e os dois papéis do próprio órgão);
    /// escrever, a equipe do órgão (e o admin geral); desde a E7, a situação é conferida na
    /// seção, pelo passo dela (<see cref="SecaoParaEscritaAsync"/> e <see cref="PeEdicaoPdtic"/>).
    /// </summary>
    private async Task<PeDonoAberto> AbrirAsync(PeDono dono, PeUserContext ctx, bool escrita)
    {
        if (!_permissoes.PodeLerReferenciais(ctx)) throw SemPermissaoDeLer();
        if (dono.EhPdtic) return await AbrirPdticAsync(dono, ctx, escrita);

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
        return new PeDonoAberto(dono, petic, null, null, papelEdita && aberta);
    }

    private async Task<PeDonoAberto> AbrirPdticAsync(PeDono dono, PeUserContext ctx, bool escrita)
    {
        var consulta = escrita ? _context.PePdtics : _context.PePdtics.AsNoTracking();
        var pdtic = await consulta.FirstOrDefaultAsync(p => p.Id == dono.PdticId) ?? throw PePdticService.NaoEncontrado();
        if (!_permissoes.PodeVerOrgao(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê o PDTIC do seu próprio órgão.");

        // O papel é conferido aqui; a situação, na seção (depende da etapa do passo dela)
        var papelEdita = _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId);
        if (escrita && !papelEdita) throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão edita o PDTIC.");
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        return new PeDonoAberto(dono, null, pdtic, trilha, papelEdita);
    }

    /// <summary>A trilha do órgão do PDTIC (sem conferir quem chama), ou nulo fora do PDTIC.</summary>
    private async Task<PeTrilhaOrgao?> TrilhaDoDonoAsync(PeDono dono)
    {
        if (!dono.EhPdtic) return null;
        var pdtic = await _context.PePdtics.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dono.PdticId)
            ?? throw PePdticService.NaoEncontrado();
        return await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
    }

    /// <summary>
    /// A seção para gravar: no PDTIC, só a do passo que aceita edição agora, pela situação do
    /// PDTIC e pela etapa (<see cref="PeEdicaoPdtic"/>); senão 409 PePdticFechado com a
    /// mensagem. Fora do PDTIC, a regra de sempre (o AbrirAsync já conferiu).
    /// </summary>
    private async Task<PeSecaoDoDono> SecaoParaEscritaAsync(PeDonoAberto aberto, string chave)
    {
        var secao = await SecaoAsync(aberto, chave);
        if (RecusaDaSecao(aberto, secao) is string recusa) throw PeEdicaoPdtic.Fechado(recusa);
        return secao;
    }

    /// <summary>No PDTIC, por que a seção não aceita edição agora (pelo passo dela), ou nulo; fora do PDTIC, nulo.</summary>
    private static string? RecusaDaSecao(PeDonoAberto aberto, PeSecaoDoDono secao)
    {
        if (aberto.Pdtic == null || aberto.Trilha == null) return null;
        var passo = aberto.Trilha.Secao(secao.Secao.Id)?.Passo;
        if (passo == null) return "Esta seção não aparece no nível do órgão.";
        return PeEdicaoPdtic.Recusa(aberto.Pdtic, PeEdicaoPdtic.GrupoDoPasso(aberto.Trilha, passo), passo.Chave);
    }

    /// <summary>Versão do PETIC-DF fora do rascunho: 409 com a mensagem da situação.</summary>
    internal static ApiException VersaoFechada(PePetic petic) => new(ErrorCode.PeVersaoFechada,
        petic.Situacao == PeDominios.SituacaoPetic.EmDeliberacao
            ? "Esta versão está com o CGTIC e não muda até a decisão."
            : "Esta versão do PETIC-DF já foi aprovada e não muda. Para mudar, crie uma versão nova.");

    /// <summary>Registros de um dono: a versão do PETIC-DF, o PDTIC ou o catálogo do DF (sem nenhum dos dois).</summary>
    public IQueryable<PeRegistro> RegistrosDo(PeDono dono) => dono.Tipo switch
    {
        PeDominios.DonoRegistro.Petic => _context.PeRegistros.Where(r => r.PeticId == dono.PeticId),
        PeDominios.DonoRegistro.Pdtic => _context.PeRegistros.Where(r => r.PdticId == dono.PdticId),
        _ => _context.PeRegistros.Where(r => r.PeticId == null && r.PdticId == null)
    };

    private async Task<PeRegistro> RegistroParaEscritaAsync(PeDono dono, PeSecaoDoDono secao, long id)
    {
        var registro = await RegistrosDo(dono).FirstOrDefaultAsync(r => r.Id == id && r.SecaoId == secao.Secao.Id)
            ?? throw new ApiException(ErrorCode.PeRegistroNaoEncontrado, "Registro não encontrado. Atualize a tela.");
        if (registro.Sistema)
            throw new ApiException(ErrorCode.PeRegistroDoSistema,
                "Este registro veio do decreto (registro do sistema) e não pode ser mudado nem apagado.");
        return registro;
    }

    /// <summary>
    /// A seção do dono pela chave. Fora do PDTIC: do escopo do dono, não apagada e não
    /// desligada. No PDTIC: da trilha do órgão (o passo e a seção aparecem no nível dele e
    /// nos ajustes), com os campos e a obrigatoriedade da trilha.
    /// </summary>
    private async Task<PeSecaoDoDono> SecaoAsync(PeDonoAberto aberto, string chave)
    {
        var texto = chave?.Trim() ?? string.Empty;
        if (aberto.Trilha != null)
        {
            var entidade = aberto.Trilha.Dados.SecaoPorChave(texto);
            if (entidade == null || entidade.Escopo != PeDominios.Escopo.Pdtic || entidade.ExcluidoEm != null)
                throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não existe aqui. Atualize a tela.");
            var visivel = aberto.Trilha.Secao(entidade.Id)
                ?? throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não aparece no nível do órgão.");
            return aberto.Trilha.Montar(visivel.Secao);
        }

        var dono = aberto.Dono;
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

    /// <summary>As seções visíveis do dono, na ordem (as do PDTIC pela trilha do órgão).</summary>
    private async Task<List<PeSecaoDoDono>> SecoesDoDonoAsync(PeDonoAberto aberto, bool soNaPlanilha) =>
        aberto.Trilha != null
            ? aberto.Trilha.SecoesMontadas(soNaPlanilha)
            : await MontarAsync(await SecoesForaDoPdticAsync(aberto.Dono.Escopo, soNaPlanilha));

    /// <summary>As seções visíveis de um escopo fora do PDTIC (as do PETIC-DF ou as do DF), na ordem.</summary>
    private async Task<List<PeSecao>> SecoesForaDoPdticAsync(string escopo, bool soNaPlanilha)
    {
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

    /// <summary>
    /// Monta seções pela situação geral (PETIC-DF e DF). Serve também para o resumo das
    /// ligações de qualquer seção (o resumo usa o campo principal, visível ou não).
    /// </summary>
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
                    .ToDictionary(g => g.Key, g => g.ToList()),
                Obrigatoria = secao.SituacaoGeral == PeDominios.Situacao.Obrigatorio
            };
        }).ToList();
    }

    /// <summary>
    /// De onde saem os itens de um catálogo feito de registros: os do PETIC-DF, da versão
    /// vigente (sem vigente, nulo); o de princípios, do catálogo do DF. Seção apagada ou
    /// desligada, ou o catálogo do PGIA (que não é feito de registros): nulo.
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
        return await PeTrilhaOrgao.SemPeticVigenteAsync(_context);
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
            // No PDTIC, pela situação e pela etapa do passo da seção (E7)
            PodeEditar = aberto.PodeEditar && RecusaDaSecao(aberto, secao) == null
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
        var temLigacao = secao.Visiveis.Any(v => PeRegistroDados.EhLigacaoPorVinculo(v.Campo));
        var ligacoes = temLigacao && ids.Count > 0
            ? await _context.PeVinculos.AsNoTracking().Where(v => ids.Contains(v.RegistroOrigemId)).ToListAsync()
            : new List<PeVinculo>();
        var destinos = await ResumosAsync(ligacoes.Select(v => v.RegistroDestinoId));
        var porOrigem = ligacoes.ToLookup(v => v.RegistroOrigemId);
        var sistemas = await SistemasPgiaAsync(
            secao.Visiveis.Where(v => PeRegistroDados.EhLigacaoPgia(v.Campo)).Select(v => v.Campo.Chave).ToList(), registros);

        return registros.Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas)).ToList();
    }

    /// <summary>Nome dos sistemas de IA do PGIA ligados nos campos dados (ligação com o PGIA), em lote.</summary>
    private async Task<Dictionary<long, PeVinculoResponse>> SistemasPgiaAsync(IReadOnlyList<string> campos, List<PeRegistro> registros)
    {
        if (campos.Count == 0 || registros.Count == 0) return new Dictionary<long, PeVinculoResponse>();

        var ids = registros.SelectMany(r =>
            {
                var dados = PeRegistroDados.Ler(r.Dados);
                return campos.SelectMany(c => PeRegistroDados.Ids(dados[c]));
            })
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new Dictionary<long, PeVinculoResponse>();

        return await _context.PgiaSistemasIa.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new PeVinculoResponse
            {
                RegistroId = s.Id,
                Codigo = null,
                Resumo = s.Denominacao
            });
    }

    private static PeRegistroResponse Resposta(PeSecaoDoDono secao, PeRegistro registro, IEnumerable<PeVinculo> ligacoes,
        IReadOnlyDictionary<long, PeVinculoResponse> destinos, IReadOnlyDictionary<long, PeVinculoResponse> sistemas)
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
            if (PeRegistroDados.EhLigacaoPgia(campo))
            {
                // Na ordem em que foram escolhidos (sistema que saiu do inventário some)
                var doPgia = PeRegistroDados.Ids(dados[campo.Chave])
                    .Select(id => sistemas.GetValueOrDefault(id))
                    .Where(s => s != null).Select(s => s!)
                    .Select(s => new PeVinculoResponse { RegistroId = s.RegistroId, Codigo = null, Resumo = PeValores.Resumo(s.Resumo) })
                    .ToList();
                resposta.Vinculos[campo.Chave] = doPgia;
                if (doPgia.Count > 0) resposta.Rotulos[campo.Chave] = string.Join(", ", doPgia.Select(s => s.Resumo));
                continue;
            }
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

    internal static string ResumoDe(PeSecaoDoDono secao, PeRegistro registro)
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
