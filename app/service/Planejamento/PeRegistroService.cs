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

    // Seção por ciclo (E7, rodada B): o ciclo de cada registro (id do registro → id do ciclo)
    public Dictionary<long, long> CicloDoRegistro { get; init; } = new();

    // Seção por ciclo: os ciclos dos registros, pelo id (o rótulo e a ordem, nas planilhas)
    public Dictionary<long, PeCiclo> Ciclos { get; init; } = new();

    /// <summary>O ciclo de um registro da seção por ciclo, ou nulo.</summary>
    public PeCiclo? CicloDe(long registroId) =>
        CicloDoRegistro.TryGetValue(registroId, out var ciclo) ? Ciclos.GetValueOrDefault(ciclo) : null;
}

/// <summary>
/// Um passo do PDTIC pronto para a planilha (E8): o passo da trilha do órgão, o ciclo (quando o
/// passo tem seção por ciclo) e as seções visíveis e "na planilha", na ordem do passo.
/// </summary>
public sealed class PePassoExportado
{
    public required PeTrilhaPasso Passo { get; init; }

    public PeCiclo? Ciclo { get; init; }

    public required List<PeSecaoExportada> Secoes { get; init; }
}

/// <summary>
/// Uma linha das grades do ciclo (E7, rodada B), gravada pelo motor: o prefixo das chaves dos
/// erros ("12" vira "12.situacao"), o registro que ela muda (nulo = um novo) e os dados (nulo =
/// apagar o registro).
/// </summary>
public sealed class PeLinhaDoCiclo
{
    public required string Prefixo { get; init; }

    public long? RegistroId { get; init; }

    public PeRegistroSalvarDTO? Dados { get; init; }
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

    public async Task<PeRegistrosResponse> ListarAsync(PeDono dono, string secaoChave, PeUserContext ctx, long? cicloId = null)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: false);
        var secao = await SecaoAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: false);
        return await RespostaDaSecaoAsync(aberto, secao, ciclo);
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

    public async Task<List<PePeticPendenciaResponse>> PendenciasAsync(PeDono dono)
    {
        var trilha = await TrilhaDoDonoAsync(dono);
        var secoes = trilha != null
            ? trilha.SecoesMontadas()
            : await MontarAsync(await SecoesForaDoPdticAsync(dono.Escopo, soNaPlanilha: false));
        var analise = await AnalisarAsync(dono, secoes);

        var pendencias = new List<PePeticPendenciaResponse>();
        foreach (var secao in analise.Secoes)
        {
            PePeticPendenciaResponse Pendencia(string motivo) => new()
            {
                SecaoChave = secao.Secao.Secao.Chave,
                SecaoTitulo = secao.Secao.Secao.Titulo,
                Motivo = motivo
            };
            if (secao.Registros.Count == 0)
            {
                if (secao.Secao.Obrigatoria)
                    pendencias.Add(Pendencia(secao.Secao.EhFormulario
                        ? $"Preencha \"{secao.Secao.Secao.Titulo}\"."
                        : $"Inclua pelo menos um item em \"{secao.Secao.Secao.Titulo}\"."));
                continue;
            }
            foreach (var (registro, faltando) in secao.Incompletos)
                pendencias.Add(Pendencia(
                    $"{registro.Codigo ?? secao.Secao.Secao.Titulo}: preencha {string.Join(", ", faltando.Select(c => $"\"{c.Rotulo}\""))}."));
        }
        return pendencias;
    }

    public async Task<PeAnaliseDono> AnalisarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes, long? cicloId = null)
    {
        var ids = secoes.Select(s => s.Secao.Id).ToList();
        var registros = ids.Count == 0
            ? new List<PeRegistro>()
            : await RegistrosDo(dono).AsNoTracking()
                .Where(r => ids.Contains(r.SecaoId))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();
        // Seção por ciclo: só os registros do ciclo dado (sem ciclo, nenhum)
        var (doCiclo, ciclos) = await FiltrarPorCicloAsync(secoes, registros, cicloId, todos: false);
        var idsRegistros = doCiclo.Select(r => r.Id).ToList();
        var vinculos = idsRegistros.Count == 0
            ? new List<PeVinculo>()
            : await _context.PeVinculos.AsNoTracking().Where(v => idsRegistros.Contains(v.RegistroOrigemId)).ToListAsync();
        var semVigente = secoes.Any(s => s.Visiveis.Any(v => EhCatalogoDoPetic(v.Campo)))
                         && await PeTrilhaOrgao.SemPeticVigenteAsync(_context);
        return AnalisarLidos(secoes, doCiclo, ciclos, cicloId, vinculos.ToLookup(v => v.RegistroOrigemId), semVigente);
    }

    /// <summary>
    /// A análise com tudo já lido (a mesma regra do <see cref="AnalisarAsync"/>): os registros do
    /// dono na ordem da seção (os de outras seções ficam de fora), o ciclo de cada registro das
    /// seções por ciclo (na seção por ciclo, só os do cicloId; sem ele, nenhum), as ligações pela
    /// origem e se falta o PETIC-DF vigente. A situação de vários PDTICs de uma vez (E8) lê tudo
    /// numa consulta por tipo e analisa cada um aqui, em memória.
    /// </summary>
    internal static PeAnaliseDono AnalisarLidos(IReadOnlyList<PeSecaoDoDono> secoes, IEnumerable<PeRegistro> registrosDoDono,
        IReadOnlyDictionary<long, long> cicloDoRegistro, long? cicloId, ILookup<long, PeVinculo> vinculosPelaOrigem, bool semPeticVigente)
    {
        var ids = secoes.Select(s => s.Secao.Id).ToHashSet();
        var porCiclo = secoes.Where(s => s.PorCiclo != null).Select(s => s.Secao.Id).ToHashSet();
        var registros = registrosDoDono
            .Where(r => ids.Contains(r.SecaoId))
            .Where(r => !porCiclo.Contains(r.SecaoId) || (cicloDoRegistro.TryGetValue(r.Id, out var ciclo) && ciclo == cicloId))
            .ToList();
        var vinculos = registros.SelectMany(r => vinculosPelaOrigem[r.Id]).ToList();
        var ligacoes = vinculos.Select(v => (v.RegistroOrigemId, v.CampoId)).ToHashSet();
        var semVigente = semPeticVigente && secoes.Any(s => s.Visiveis.Any(v => EhCatalogoDoPetic(v.Campo)));

        var porSecao = registros.ToLookup(r => r.SecaoId);
        var analisadas = secoes.Select(secao =>
        {
            var doSecao = porSecao[secao.Secao.Id].ToList();
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

    public async Task<PePassoExportado> ExportarPassoAsync(long pdticId, string passoChave, long? cicloId, PeUserContext ctx)
    {
        var dono = PeDono.DoPdtic(pdticId);
        var aberto = await AbrirAsync(dono, ctx, escrita: false);
        var trilha = aberto.Trilha!;
        var chave = passoChave?.Trim() ?? string.Empty;
        var passo = trilha.Passos.FirstOrDefault(p => p.Chave == chave)
            ?? throw new ApiException(ErrorCode.PePassoIndisponivel, "Este passo não está na trilha do órgão. Atualize a tela.");
        var secoes = passo.Secoes.Select(trilha.Montar).Where(s => s.Secao.NaPlanilha).ToList();
        if (secoes.Count == 0)
            throw new ApiException(ErrorCode.PePassoSemPlanilha,
                $"O passo {passo.Numero} não tem dados para a planilha: ele não tem seção que o órgão preencha (ou o administrador deixou as seções dele fora da planilha).");

        // Seção por ciclo: o ciclo é obrigatório (400), do PDTIC (404) e do tipo de cada seção por ciclo (400)
        PeCiclo? ciclo = null;
        foreach (var secao in secoes.Where(s => s.PorCiclo != null))
            ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: false);

        var exportadas = new List<PeSecaoExportada>();
        foreach (var secao in secoes)
            exportadas.Add(await ExportadaAsync(dono, secao, secao.PorCiclo == null ? null : ciclo));
        return new PePassoExportado { Passo = passo, Ciclo = ciclo, Secoes = exportadas };
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
            return porPdtic[g.Key].Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas, PeNomes.Vazio)).ToList();
        });
    }

    public async Task<List<PeSecaoExportada>> ExportarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes, long? cicloId = null)
    {
        var ids = secoes.Select(s => s.Secao.Id).Distinct().ToList();
        var registros = ids.Count == 0
            ? new List<PeRegistro>()
            : await RegistrosDo(dono).AsNoTracking()
                .Where(r => ids.Contains(r.SecaoId))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();
        // Seção por ciclo: com cicloId, só os registros dele; sem, os de todos os ciclos (o RR e o PDTIC)
        var (filtrados, ciclos) = await FiltrarPorCicloAsync(secoes, registros, cicloId, todos: cicloId == null);
        registros = filtrados;
        var dosCiclos = await CiclosAsync(ciclos.Values);

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
            Registros = porSecao[secao.Secao.Id].Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas, PeNomes.Vazio)).ToList(),
            CicloDoRegistro = secao.PorCiclo == null
                ? new Dictionary<long, long>()
                : porSecao[secao.Secao.Id].Where(r => ciclos.ContainsKey(r.Id)).ToDictionary(r => r.Id, r => ciclos[r.Id]),
            Ciclos = secao.PorCiclo == null ? new Dictionary<long, PeCiclo>() : dosCiclos
        }).ToList();
    }

    /// <summary>Os ciclos pelo id (só lê quando há algum).</summary>
    private async Task<Dictionary<long, PeCiclo>> CiclosAsync(IEnumerable<long> ids)
    {
        var lista = ids.Distinct().ToList();
        return lista.Count == 0
            ? new Dictionary<long, PeCiclo>()
            : await _context.PeCiclos.AsNoTracking().Where(c => lista.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
    }

    /// <summary>
    /// O ciclo de cada registro das seções por ciclo e o filtro: com cicloId, só os registros dele;
    /// com todos, os de qualquer ciclo; sem nenhum dos dois, nenhum (o registro sem ciclo de uma
    /// seção por ciclo, de antes da rodada B, também fica de fora). As outras seções não mudam. Só
    /// lê o ciclo dos registros quando alguma seção é por ciclo (a versão 6 carregada).
    /// </summary>
    private async Task<(List<PeRegistro> Registros, Dictionary<long, long> Ciclos)> FiltrarPorCicloAsync(
        IReadOnlyList<PeSecaoDoDono> secoes, List<PeRegistro> registros, long? cicloId, bool todos)
    {
        var porCiclo = secoes.Where(s => s.PorCiclo != null).Select(s => s.Secao.Id).ToHashSet();
        if (porCiclo.Count == 0) return (registros, new Dictionary<long, long>());
        var ids = registros.Where(r => porCiclo.Contains(r.SecaoId)).Select(r => r.Id).ToList();
        var ciclos = ids.Count == 0
            ? new Dictionary<long, long>()
            : await _context.PeRegistrosCiclo.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CicloId);
        var filtrados = registros
            .Where(r => !porCiclo.Contains(r.SecaoId)
                        || (ciclos.TryGetValue(r.Id, out var ciclo) && (todos || ciclo == cicloId)))
            .ToList();
        return (filtrados, ciclos);
    }

    public async Task<Dictionary<long, (long CicloId, string Rotulo)>> CiclosDosRegistrosAsync(IReadOnlyCollection<long> registroIds)
    {
        if (registroIds.Count == 0) return new Dictionary<long, (long, string)>();
        var ids = registroIds.Distinct().ToList();
        var linhas = await (from r in _context.PeRegistrosCiclo.AsNoTracking()
                            join c in _context.PeCiclos.AsNoTracking() on r.CicloId equals c.Id
                            where ids.Contains(r.Id)
                            select new { r.Id, CicloId = c.Id, c.Rotulo })
            .ToListAsync();
        return linhas.ToDictionary(l => l.Id, l => (l.CicloId, l.Rotulo));
    }

    // ── Escrita ─────────────────────────────────────────────────────────────

    public async Task<PeRegistroResponse> CriarAsync(PeDono dono, string secaoChave, PeRegistroSalvarDTO dto, PeUserContext ctx, long? cicloId = null)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: true);
        var doDono = DaSecao(dono, secao, ciclo);
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
        if (ciclo != null) _context.PeRegistrosCiclo.Add(new PeRegistroCiclo { Registro = registro, CicloId = ciclo.Id });
        Aplicar(registro, gravacao);
        Tocar(aberto, ctx, agora, ciclo);
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

    public async Task<PeRegistroResponse> AtualizarAsync(PeDono dono, string secaoChave, long id, PeRegistroSalvarDTO dto, PeUserContext ctx,
        long? cicloId = null)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: true);
        var registro = await RegistroParaEscritaAsync(dono, secao, id, ciclo);

        var gravacao = await ValidarAsync(aberto, secao, registro, dto, ctx);

        var agora = DateTime.UtcNow;
        Aplicar(registro, gravacao);
        DarDono(gravacao, registro.Id, ctx, agora);
        registro.AlteradoEm = agora;
        registro.AlteradoPor = ctx.Email;
        Tocar(aberto, ctx, agora, ciclo);
        CopiarVigencia(aberto, secao, gravacao.Dados);
        IniciarAcompanhamento(aberto, secao, gravacao.Dados);
        await _context.SaveChangesAsync();

        return await UmaRespostaAsync(secao, registro.Id);
    }

    public async Task ExcluirAsync(PeDono dono, string secaoChave, long id, PeUserContext ctx, long? cicloId = null)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: true);
        var registro = await RegistroParaEscritaAsync(dono, secao, id, ciclo);

        var ligadoPor = await _context.PeVinculos.AsNoTracking()
            .Where(v => v.RegistroDestinoId == id)
            .Select(v => v.RegistroOrigemId)
            .Distinct()
            .ToListAsync();
        if (ligadoPor.Count > 0)
            throw new ApiException(ErrorCode.PeRegistroLigado, await MensagemLigadoPorAsync(ligadoPor));

        _context.PeVinculos.RemoveRange(await _context.PeVinculos.Where(v => v.RegistroOrigemId == id).ToListAsync());
        await RemoverAsync(registro, ciclo);
        Tocar(aberto, ctx, DateTime.UtcNow, ciclo);
        CopiarVigencia(aberto, secao, null);
        await _context.SaveChangesAsync();
    }

    /// <summary>Tira o registro (e, na seção por ciclo, a parte com o ciclo, que divide a mesma linha).</summary>
    private async Task RemoverAsync(PeRegistro registro, PeCiclo? ciclo)
    {
        if (ciclo != null && await _context.PeRegistrosCiclo.FirstOrDefaultAsync(c => c.Id == registro.Id) is { } doCiclo)
            _context.PeRegistrosCiclo.Remove(doCiclo);
        _context.PeRegistros.Remove(registro);
    }

    /// <summary>
    /// Grava de uma vez as linhas de uma seção por ciclo (as grades da situação das ações e das
    /// medições, E7 rodada B): cada linha passa pela mesma validação de uma gravação avulsa (tipo,
    /// obrigatório no nível, opção, ligação), os erros de todas voltam juntos com o prefixo da linha
    /// ("12.situacao") e, sem erro, cria, atualiza e apaga tudo numa gravação só.
    /// </summary>
    public async Task SalvarNoCicloAsync(PeDono dono, string secaoChave, long cicloId, IReadOnlyList<PeLinhaDoCiclo> linhas, PeUserContext ctx)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: true)
                    ?? throw new ApiException(ErrorCode.PeCicloInvalido, "Esta seção não é registrada por ciclo.");
        var existentes = await DaSecao(dono, secao, ciclo).ToListAsync();

        var erros = new Dictionary<string, string>();
        var validadas = new List<(PeRegistro? Atual, PeGravacao? Gravacao)>();
        foreach (var linha in linhas)
        {
            PeRegistro? atual = null;
            if (linha.RegistroId is long registroId)
            {
                atual = existentes.FirstOrDefault(r => r.Id == registroId);
                if (atual == null)
                {
                    erros[$"{linha.Prefixo}.registro"] = "Este registro não é deste ciclo. Atualize a tela.";
                    continue;
                }
            }
            if (linha.Dados == null)
            {
                if (atual != null) validadas.Add((atual, null));
                continue;
            }
            try
            {
                validadas.Add((atual, await ValidarAsync(aberto, secao, atual, linha.Dados, ctx)));
            }
            catch (PeValidacaoException ex)
            {
                foreach (var (chave, mensagem) in ex.Campos) erros[$"{linha.Prefixo}.{chave}"] = mensagem;
            }
        }
        if (erros.Count > 0) throw new PeValidacaoException(erros);

        if (validadas.Count == 0) return;
        var agora = DateTime.UtcNow;
        var ordem = existentes.Select(r => r.Ordem).DefaultIfEmpty(0).Max();
        PeRegistroSequencia? sequencia = null;
        var novos = new List<(PeRegistro Registro, PeGravacao Gravacao)>();
        foreach (var (atual, gravacao) in validadas)
        {
            if (gravacao == null)
            {
                _context.PeVinculos.RemoveRange(await _context.PeVinculos.Where(v => v.RegistroOrigemId == atual!.Id).ToListAsync());
                await RemoverAsync(atual!, ciclo);
                continue;
            }
            var registro = atual;
            if (registro == null)
            {
                sequencia ??= await SequenciaAsync(secao.Secao.Id, dono);
                sequencia.Ultimo++;
                registro = new PeRegistro
                {
                    SecaoId = secao.Secao.Id,
                    PdticId = dono.PdticId,
                    Codigo = CodigoDe(secao, sequencia.Ultimo),
                    Ordem = ++ordem,
                    Sistema = false,
                    CriadoEm = agora,
                    CriadoPor = ctx.Email
                };
                _context.PeRegistros.Add(registro);
                _context.PeRegistrosCiclo.Add(new PeRegistroCiclo { Registro = registro, CicloId = ciclo.Id });
                novos.Add((registro, gravacao));
            }
            else
            {
                registro.AlteradoEm = agora;
                registro.AlteradoPor = ctx.Email;
                DarDono(gravacao, registro.Id, ctx, agora);
            }
            Aplicar(registro, gravacao);
            IniciarAcompanhamento(aberto, secao, gravacao.Dados);
        }
        Tocar(aberto, ctx, agora, ciclo);

        // Como na inclusão avulsa: o id do registro novo sai na primeira gravação e os arquivos ganham o dono na segunda
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await _context.SaveChangesAsync();
        if (novos.Any(n => n.Gravacao.Arquivos.Count > 0))
        {
            foreach (var (registro, gravacao) in novos) DarDono(gravacao, registro.Id, ctx, agora);
            await _context.SaveChangesAsync();
        }
        if (transacao != null) await transacao.CommitAsync();
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

    public async Task<PeRegistrosResponse> OrdenarAsync(PeDono dono, string secaoChave, PeOrdemDTO dto, PeUserContext ctx, long? cicloId = null)
    {
        var aberto = await AbrirAsync(dono, ctx, escrita: true);
        var secao = await SecaoParaEscritaAsync(aberto, secaoChave);
        var ciclo = await CicloDaSecaoAsync(aberto, secao, cicloId, escrita: true);
        var registros = await DaSecao(dono, secao, ciclo).ToListAsync();

        var ids = dto.Ids ?? new List<long>();
        if (ids.Count != registros.Count || ids.Distinct().Count() != ids.Count || !registros.Select(r => r.Id).ToHashSet().SetEquals(ids))
            throw new ApiException(ErrorCode.PeOrdemInvalida, "Mande todos os registros da seção, cada um uma vez, na nova ordem.");

        for (var i = 0; i < ids.Count; i++)
            registros.Single(r => r.Id == ids[i]).Ordem = i + 1;
        Tocar(aberto, ctx, DateTime.UtcNow, ciclo);
        await _context.SaveChangesAsync();

        return await RespostaDaSecaoAsync(aberto, secao, ciclo);
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

    /// <summary>
    /// Gravar um registro da versão do PETIC-DF ou do PDTIC marca o dono como alterado (e confere
    /// a situação); na seção por ciclo, também o ciclo (a situação dele é token de concorrência:
    /// gravar um dado e fechar o ciclo ao mesmo tempo não passam os dois).
    /// </summary>
    private static void Tocar(PeDonoAberto aberto, PeUserContext ctx, DateTime agora, PeCiclo? ciclo = null)
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
        if (ciclo != null)
        {
            ciclo.AlteradoEm = agora;
            ciclo.AlteradoPor = ctx.Email;
        }
    }

    /// <summary>
    /// O acompanhamento começa (E7): num PDTIC publicado, o primeiro dado gravado num ciclo de
    /// monitoramento (rodada B) ou a aprovação do plano de acompanhamento (passo 4.6) gravada com
    /// a decisão "aprovado", o que vier primeiro, o põe em acompanhamento. A situação é token de
    /// concorrência: a mudança vai na mesma gravação do registro.
    /// </summary>
    private static void IniciarAcompanhamento(PeDonoAberto aberto, PeSecaoDoDono secao, JsonObject dados)
    {
        if (aberto.Pdtic is not { Situacao: PeDominios.SituacaoPdtic.Publicado } pdtic) return;
        var doMonitoramento = secao.PorCiclo == PeDominios.TipoCiclo.Monitoramento;
        var planoAprovado = secao.Secao.Chave == PeDominios.ChavePdtic.SecaoAprovacaoPlanoAcompanhamento
                            && PeRegistroDados.Texto(dados[PeDominios.ChavePdtic.CampoDecisao]) == PeDominios.Decisao.Aprovado;
        if (doMonitoramento || planoAprovado) pdtic.Situacao = PeDominios.SituacaoPdtic.EmAcompanhamento;
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

    private async Task<PeRegistro> RegistroParaEscritaAsync(PeDono dono, PeSecaoDoDono secao, long id, PeCiclo? ciclo = null)
    {
        var registro = await DaSecao(dono, secao, ciclo).FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new ApiException(ErrorCode.PeRegistroNaoEncontrado, "Registro não encontrado. Atualize a tela.");
        if (registro.Sistema)
            throw new ApiException(ErrorCode.PeRegistroDoSistema,
                "Este registro veio do decreto (registro do sistema) e não pode ser mudado nem apagado.");
        return registro;
    }

    /// <summary>Os registros do dono numa seção; na seção por ciclo, só os do ciclo.</summary>
    private IQueryable<PeRegistro> DaSecao(PeDono dono, PeSecaoDoDono secao, PeCiclo? ciclo)
    {
        var secaoId = secao.Secao.Id;
        var registros = RegistrosDo(dono).Where(r => r.SecaoId == secaoId);
        if (ciclo == null) return registros;
        var cicloId = ciclo.Id;
        return registros.Where(r => _context.PeRegistrosCiclo.Any(c => c.Id == r.Id && c.CicloId == cicloId));
    }

    /// <summary>
    /// O ciclo de uma seção por ciclo (E7, rodada B): o cicloId é obrigatório (400
    /// PeCicloObrigatorio), do mesmo PDTIC (404) e do tipo da seção (400 PeCicloInvalido); para
    /// gravar, o ciclo precisa ter começado e não estar fechado (409 PeCicloFechado). Seção que
    /// não é por ciclo: nulo, e o cicloId é ignorado.
    /// </summary>
    private async Task<PeCiclo?> CicloDaSecaoAsync(PeDonoAberto aberto, PeSecaoDoDono secao, long? cicloId, bool escrita)
    {
        if (secao.PorCiclo == null || aberto.Pdtic == null) return null;
        if (cicloId == null)
            throw new ApiException(ErrorCode.PeCicloObrigatorio,
                $"\"{secao.Secao.Titulo}\" é registrada a cada ciclo: escolha o ciclo (cicloId).");
        var pdticId = aberto.Pdtic.Id;
        var consulta = escrita ? _context.PeCiclos : _context.PeCiclos.AsNoTracking();
        var ciclo = await consulta.FirstOrDefaultAsync(c => c.Id == cicloId && c.PdticId == pdticId) ?? throw PeCiclos.NaoEncontrado();
        if (ciclo.Tipo != secao.PorCiclo)
            throw new ApiException(ErrorCode.PeCicloInvalido, secao.PorCiclo == PeDominios.TipoCiclo.Monitoramento
                ? $"\"{secao.Secao.Titulo}\" é do monitoramento: escolha um ciclo de monitoramento."
                : $"\"{secao.Secao.Titulo}\" é da avaliação intermediária: escolha uma avaliação.");
        if (escrita && PeCiclos.RecusaDeDados(ciclo, PeCiclos.Hoje()) is string recusa)
            throw new ApiException(ErrorCode.PeCicloFechado, recusa);
        return ciclo;
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

    private async Task<PeRegistrosResponse> RespostaDaSecaoAsync(PeDonoAberto aberto, PeSecaoDoDono secao, PeCiclo? ciclo = null)
    {
        var registros = await DaSecao(aberto.Dono, secao, ciclo).AsNoTracking()
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
                PorCiclo = secao.PorCiclo,
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
            // No PDTIC, pela situação e pela etapa do passo da seção (E7); na seção por ciclo, e o ciclo aceitando dados
            PodeEditar = aberto.PodeEditar && RecusaDaSecao(aberto, secao) == null
                         && (ciclo == null || PeCiclos.RecusaDeDados(ciclo, PeCiclos.Hoje()) == null)
        };
    }

    private async Task<PeSecaoExportada> ExportadaAsync(PeDono dono, PeSecaoDoDono secao, PeCiclo? soDoCiclo = null)
    {
        var registros = await RegistrosDo(dono).AsNoTracking()
            .Where(r => r.SecaoId == secao.Secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        // Seção por ciclo: os registros de todos os ciclos, com o ciclo de cada um (a planilha mostra);
        // na planilha de um passo por ciclo (E8), só os do ciclo pedido
        var (filtrados, ciclos) = await FiltrarPorCicloAsync(new[] { secao }, registros, soDoCiclo?.Id, todos: soDoCiclo == null);
        return new PeSecaoExportada
        {
            Modelo = secao,
            Colunas = secao.Visiveis.Where(v => v.Campo.NaPlanilha).ToList(),
            Registros = await ResponderAsync(secao, filtrados),
            CicloDoRegistro = ciclos,
            Ciclos = await CiclosAsync(ciclos.Values)
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

        // Os nomes de quem incluiu e de quem alterou (F1, C19): só nas respostas da tela (as
        // exportações para o documento, as planilhas e os painéis não os usam)
        var nomes = await PeNomes.CarregarAsync(_context, registros.SelectMany(r => new[] { r.CriadoPor, r.AlteradoPor }));
        return registros.Select(r => Resposta(secao, r, porOrigem[r.Id], destinos, sistemas, nomes)).ToList();
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
        IReadOnlyDictionary<long, PeVinculoResponse> destinos, IReadOnlyDictionary<long, PeVinculoResponse> sistemas, PeNomes nomes)
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
            CriadoPorNome = nomes.DeObrigatorio(registro.CriadoPor),
            AlteradoEm = registro.AlteradoEm,
            AlteradoPor = registro.AlteradoPor,
            AlteradoPorNome = nomes.De(registro.AlteradoPor)
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
