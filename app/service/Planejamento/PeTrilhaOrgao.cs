using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// A trilha resolvida de um órgão (<see cref="PeTrilhaResolver"/>, da E2) com o que a E4
/// precisa em volta: o modelo inteiro (as entidades), o nível efetivo e a regra do PETIC-DF
/// sem versão vigente (a ligação com os catálogos dele fica opcional em todos os níveis,
/// também no Obrigatorio da trilha). Usada pelo GET modelo/trilha, pelo motor de registros
/// no PDTIC (seções e campos visíveis e obrigatórios do nível e dos ajustes), pela situação
/// dos passos e pelas planilhas do PDTIC e consolidadas. Desde a F3, no modo livre, a trilha
/// parte do nível base (o passo fora dele aparece opcional) e cada passo tem a forma dele (a
/// escolhida pelo órgão em pe_orgao_passo_detalhe, senão a padrão); a tela diz "passo a passo".
/// </summary>
public sealed class PeTrilhaOrgao
{
    public required PgiaOrgao Orgao { get; init; }

    public required PeModeloDados Dados { get; init; }

    // No modo livre (F3), o nível base (o padrão)
    public required PeNivel Nivel { get; init; }

    // O órgão não escolheu nível: vale o padrão (o primeiro ativo pela ordem); no modo livre, sempre
    public bool NivelPadrao { get; init; }

    // F3: a trilha é do modo livre (cada passo com a forma dele; o passo fora do nível base, opcional)
    public bool Livre { get; init; }

    // Não há PETIC-DF vigente: a ligação com os catálogos dele já vem opcional
    public bool SemPeticVigente { get; init; }

    public required List<PeTrilhaEtapa> Etapas { get; init; }

    // O PDTIC cujas regras próprias já foram aplicadas nesta trilha (AjustarAoPdtic), ou nulo
    public long? AjustadaParaPdtic { get; private set; }

    private Dictionary<long, PeTrilhaPasso>? _passos;
    private Dictionary<long, (PeTrilhaPasso Passo, PeTrilhaSecao Secao)>? _secoes;

    /// <summary>Os passos visíveis, na ordem da trilha.</summary>
    public IEnumerable<PeTrilhaPasso> Passos => Etapas.SelectMany(e => e.Passos);

    public PeTrilhaPasso? Passo(long passoId)
    {
        _passos ??= Passos.ToDictionary(p => p.Id);
        return _passos.GetValueOrDefault(passoId);
    }

    /// <summary>A seção visível (e o passo dela) pelo id, ou nulo quando o órgão não a vê.</summary>
    public (PeTrilhaPasso Passo, PeTrilhaSecao Secao)? Secao(long secaoId)
    {
        _secoes ??= Passos.SelectMany(p => p.Secoes.Select(s => (p, s))).ToDictionary(x => x.s.Id, x => (x.p, x.s));
        return _secoes.TryGetValue(secaoId, out var achada) ? achada : null;
    }

    /// <summary>A seção visível pela chave, ou nulo.</summary>
    public (PeTrilhaPasso Passo, PeTrilhaSecao Secao)? Secao(string chave) =>
        Dados.SecaoPorChave(chave) is PeSecao secao ? Secao(secao.Id) : null;

    /// <summary>O campo visível de uma seção visível, pela chave, ou nulo.</summary>
    public PeTrilhaCampo? Campo(string secaoChave, string campoChave) =>
        Secao(secaoChave)?.Secao.Campos.FirstOrDefault(c => c.Chave == campoChave);

    /// <summary>
    /// A seção como o motor de registros usa: todos os campos (inclusive os apagados e os
    /// escondidos, que guardam valor), os visíveis na ordem, com a obrigatoriedade do nível e
    /// dos ajustes, e as opções de cada campo.
    /// </summary>
    public PeSecaoDoDono Montar(PeTrilhaSecao visivel)
    {
        var secao = Dados.Secoes.First(s => s.Id == visivel.Id);
        var campos = Dados.CamposDaSecao(secao.Id, incluirExcluidos: true).ToList();
        var porId = campos.ToDictionary(c => c.Id);
        var ids = porId.Keys.ToHashSet();
        return new PeSecaoDoDono
        {
            Secao = secao,
            Campos = campos,
            Visiveis = visivel.Campos.Select(c => new PeCampoVisivel(porId[c.Id], c.Obrigatorio)).ToList(),
            Opcoes = Dados.Opcoes.Where(o => ids.Contains(o.CampoId))
                .GroupBy(o => o.CampoId)
                .ToDictionary(g => g.Key, g => g.ToList()),
            Obrigatoria = visivel.Situacao == PeDominios.Situacao.Obrigatorio,
            PorCiclo = Dados.PorCicloDe(secao.Id)
        };
    }

    /// <summary>Todas as seções visíveis, na ordem da trilha (etapa, passo e seção).</summary>
    public List<PeSecaoDoDono> SecoesMontadas(bool soNaPlanilha = false) =>
        Passos.SelectMany(p => p.Secoes)
            .Select(Montar)
            .Where(s => !soNaPlanilha || s.Secao.NaPlanilha)
            .ToList();

    /// <summary>
    /// As regras que valem só para um PDTIC (E7): no registrado fora do sistema, o campo de
    /// ligação com uma seção de passo "externo" (etapas 1 a 3, menos 3.3 e 3.9) deixa de ser
    /// obrigatório, porque essas seções não são preenchidas (por exemplo, a meta sem a
    /// necessidade ligada). Muda esta trilha; a trilha deve ser do órgão do PDTIC.
    /// </summary>
    public void AjustarAoPdtic(PePdtic pdtic)
    {
        AjustadaParaPdtic = pdtic.Id;
        if (!pdtic.RegistradoExternamente) return;

        var externas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var etapa in Etapas)
            foreach (var passo in etapa.Passos.Where(p => PeEdicaoPdtic.Externo(pdtic, PeEdicaoPdtic.GrupoDe(etapa.Chave, p.Tipo), p.Chave)))
                foreach (var secao in passo.Secoes) externas.Add(secao.Chave);
        if (externas.Count == 0) return;

        var configs = Dados.Campos.ToDictionary(c => c.Id, c => c.Config);
        foreach (var campo in Passos.SelectMany(p => p.Secoes).SelectMany(s => s.Campos))
            if (campo.Tipo == PeDominios.TipoCampo.LigacaoSecao && configs.TryGetValue(campo.Id, out var config)
                && PeConfigCampo.SecaoDaLigacao(config) is string alvo && externas.Contains(alvo))
                campo.Obrigatorio = false;
    }

    // ── Carga ───────────────────────────────────────────────────────────────

    /// <summary>A trilha de um órgão (ativo, ou qualquer um quando o PDTIC já existe).</summary>
    public static async Task<PeTrilhaOrgao> CarregarAsync(AppDbContext context, long orgaoId, bool soAtivo = true)
    {
        var orgao = await context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && (!soAtivo || o.Ativo))
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");
        var dados = await PeModeloDados.CarregarAsync(context);
        return await DoOrgaoAsync(context, dados, orgao, await SemPeticVigenteAsync(context));
    }

    /// <summary>A trilha do órgão de um PDTIC, já com as regras próprias dele (<see cref="AjustarAoPdtic"/>).</summary>
    public static async Task<PeTrilhaOrgao> DoPdticAsync(AppDbContext context, PePdtic pdtic)
    {
        var trilha = await CarregarAsync(context, pdtic.OrgaoId, soAtivo: false);
        trilha.AjustarAoPdtic(pdtic);
        return trilha;
    }

    /// <summary>
    /// A trilha de um órgão com o modelo já carregado (o consolidado passa por vários órgãos). No
    /// modo livre (F3), com a forma que o órgão escolheu para cada passo.
    /// </summary>
    public static async Task<PeTrilhaOrgao> DoOrgaoAsync(AppDbContext context, PeModeloDados dados, PgiaOrgao orgao, bool semVigente)
    {
        var escolhido = await context.PeOrgaosConfig.AsNoTracking()
            .Where(c => c.OrgaoId == orgao.Id)
            .Select(c => (long?)c.NivelId)
            .FirstOrDefaultAsync();
        var ajustes = await context.PeOrgaosAjuste.AsNoTracking()
            .Where(a => a.OrgaoId == orgao.Id)
            .Select(a => new { a.AlvoTipo, a.AlvoId, a.Situacao })
            .ToListAsync();
        // A forma de cada passo só é lida no modo livre (a versão 8 carregada: a tabela existe)
        var detalhes = dados.ModoNiveis.Livre
            ? await context.PeOrgaosPassoDetalhe.AsNoTracking()
                .Where(d => d.OrgaoId == orgao.Id)
                .ToDictionaryAsync(d => d.PassoId, d => d.NivelId)
            : null;
        return Resolver(dados, orgao, escolhido, ajustes.ToDictionary(a => (a.AlvoTipo, a.AlvoId), a => a.Situacao), semVigente, detalhes);
    }

    /// <summary>
    /// A trilha do órgão no modo de hoje: no definido, pelo nível escolhido (ou o padrão) e os
    /// ajustes; no livre (F3), a partir do nível base, com os ajustes e a forma escolhida de cada
    /// passo (detalhes: passo → nível; o nível escolhido do órgão não vale, fica guardado).
    /// </summary>
    public static PeTrilhaOrgao Resolver(PeModeloDados dados, PgiaOrgao orgao, long? escolhido,
        IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, bool semVigente, IReadOnlyDictionary<long, long>? detalhes = null)
    {
        if (!dados.ModoNiveis.Livre) return ResolverDefinido(dados, orgao, escolhido, ajustes, semVigente);

        var nivelBase = dados.NivelPadrao() ?? throw PeModeloService.ModeloIndisponivel();
        var etapas = PeTrilhaResolver.ResolverLivre(dados, ajustes, detalhes ?? new Dictionary<long, long>());
        if (semVigente) DispensarPetic(dados, etapas);
        return new PeTrilhaOrgao
        {
            Orgao = orgao,
            Dados = dados,
            Nivel = nivelBase,
            NivelPadrao = true,
            Livre = true,
            SemPeticVigente = semVigente,
            Etapas = etapas
        };
    }

    /// <summary>
    /// A trilha no modo definido (a de sempre): o nível escolhido (ou o padrão) com os ajustes.
    /// Também a régua do nível alcançado (F3), que usa cada nível sem os ajustes de órgão.
    /// </summary>
    public static PeTrilhaOrgao ResolverDefinido(PeModeloDados dados, PgiaOrgao orgao, long? escolhido,
        IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, bool semVigente)
    {
        var nivel = (escolhido != null ? dados.Niveis.FirstOrDefault(n => n.Id == escolhido) : null)
            ?? dados.NivelPadrao()
            ?? throw PeModeloService.ModeloIndisponivel();
        var etapas = PeTrilhaResolver.Resolver(dados, nivel.Id, ajustes);
        if (semVigente) DispensarPetic(dados, etapas);
        return new PeTrilhaOrgao
        {
            Orgao = orgao,
            Dados = dados,
            Nivel = nivel,
            NivelPadrao = escolhido != nivel.Id,
            SemPeticVigente = semVigente,
            Etapas = etapas
        };
    }

    /// <summary>
    /// A trilha de um nível sem ajuste de órgão (a união das colunas do consolidado considera
    /// o que cada nível mostra).
    /// </summary>
    public static List<PeTrilhaEtapa> DoNivel(PeModeloDados dados, long nivelId) =>
        PeTrilhaResolver.Resolver(dados, nivelId, new Dictionary<(string, long), string>());

    public static async Task<bool> SemPeticVigenteAsync(AppDbContext context) =>
        !await context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado);

    /// <summary>
    /// A forma escolhida de cada passo dos órgãos dados (órgão → passo → nível), em lote. Só lê no
    /// modo livre (a versão 8 carregada); no definido, vazio.
    /// </summary>
    public static async Task<Dictionary<long, Dictionary<long, long>>> DetalhesDosOrgaosAsync(AppDbContext context, PeModeloDados dados,
        IReadOnlyCollection<long> orgaoIds)
    {
        if (!dados.ModoNiveis.Livre || orgaoIds.Count == 0) return new Dictionary<long, Dictionary<long, long>>();
        var ids = orgaoIds.Distinct().ToList();
        return (await context.PeOrgaosPassoDetalhe.AsNoTracking().Where(d => ids.Contains(d.OrgaoId)).ToListAsync())
            .GroupBy(d => d.OrgaoId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.PassoId, d => d.NivelId));
    }

    /// <summary>
    /// O nível de hoje de cada órgão (o escolhido ou o padrão; nulo sem modelo), em lote. No modo
    /// livre (F3), o nível base para todos (o escolhido não vale nele).
    /// </summary>
    public static async Task<Dictionary<long, PeNivel?>> NiveisDosOrgaosAsync(AppDbContext context, IEnumerable<long> orgaoIds)
    {
        var ids = orgaoIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, PeNivel?>();
        var niveis = await context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).ToListAsync();
        var padrao = niveis.FirstOrDefault(n => n.Ativo);
        if ((await PeModoNiveis.LerAsync(context)).Livre) return ids.ToDictionary(id => id, _ => padrao);
        var escolhidos = await context.PeOrgaosConfig.AsNoTracking()
            .Where(c => ids.Contains(c.OrgaoId))
            .ToDictionaryAsync(c => c.OrgaoId, c => c.NivelId);
        return ids.ToDictionary(id => id,
            id => (escolhidos.TryGetValue(id, out var nivelId) ? niveis.FirstOrDefault(n => n.Id == nivelId) : null) ?? padrao);
    }

    /// <summary>Sem PETIC-DF vigente, a ligação com os catálogos dele fica opcional em todos os níveis.</summary>
    private static void DispensarPetic(PeModeloDados dados, List<PeTrilhaEtapa> etapas)
    {
        var doPetic = dados.Campos
            .Where(c => c.Tipo == PeDominios.TipoCampo.LigacaoCatalogo
                        && PeDominios.Catalogo.DoPetic(PeValores.Texto(c.Config, "catalogo")))
            .Select(c => c.Id)
            .ToHashSet();
        foreach (var campo in etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).SelectMany(s => s.Campos))
            if (doPetic.Contains(campo.Id)) campo.Obrigatorio = false;
    }
}
