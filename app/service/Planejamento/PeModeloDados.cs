using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O modelo inteiro lido de uma vez (é pequeno: dezenas de passos, poucas centenas de
/// campos), sem rastreamento, com os índices que o GET modelo, a trilha e as regras
/// usam. Monta as respostas no formato do contrato.
/// </summary>
public sealed class PeModeloDados
{
    public List<PeNivel> Niveis { get; private init; } = new();
    public List<PeEtapa> Etapas { get; private init; } = new();
    public List<PePasso> Passos { get; private init; } = new();
    public List<PeSecao> Secoes { get; private init; } = new();
    public List<PeCampo> Campos { get; private init; } = new();
    public List<PeOpcao> Opcoes { get; private init; } = new();

    // Situação por nível: id do item → id do nível → situação
    public Dictionary<long, Dictionary<long, string>> SituacaoPasso { get; private init; } = new();
    public Dictionary<long, Dictionary<long, string>> SituacaoSecao { get; private init; } = new();
    public Dictionary<long, Dictionary<long, string>> SituacaoCampo { get; private init; } = new();

    // Órgãos por nível escolhido
    public Dictionary<long, int> OrgaosPorNivel { get; private init; } = new();

    // Seções por ciclo (E7, rodada B): id da seção → monitoramento ou avaliacao. Vazio antes de o
    // carregador trazer a versão 6 (e no intervalo do deploy, quando a coluna ainda não existe)
    public Dictionary<long, string> PorCiclo { get; private init; } = new();

    // As configurações do acompanhamento e se ele já está ligado (versão 6 carregada)
    public PeAcompanhamentoAtivo Acompanhamento { get; private init; } = new(false, 0, 0, PeDominios.Periodicidade.Padrao);

    // F3: o modo dos níveis (livre ou definido) e a marca da versão 8 (a forma de cada passo e a validação da equipe)
    public PeModoNiveis ModoNiveis { get; private init; } = PeModoNiveis.Anterior;

    // A régua do nível alcançado (F3), montada uma vez por leitura do modelo: a trilha de cada nível
    // ativo sem os ajustes de órgão (com e sem PETIC-DF vigente)
    private readonly Dictionary<bool, List<PeReguaNivel>> _reguas = new();

    public static async Task<PeModeloDados> CarregarAsync(AppDbContext context)
    {
        var passoNivel = await context.PePassosNivel.AsNoTracking().ToListAsync();
        var secaoNivel = await context.PeSecoesNivel.AsNoTracking().ToListAsync();
        var campoNivel = await context.PeCamposNivel.AsNoTracking().ToListAsync();
        var orgaos = await context.PeOrgaosConfig.AsNoTracking()
            .GroupBy(c => c.NivelId)
            .Select(g => new { NivelId = g.Key, Total = g.Count() })
            .ToListAsync();
        // As configurações numa consulta só: a coluna por_ciclo só é lida com a versão 6 carregada
        // (que só carrega com a migration aplicada); a forma dos passos e a validação, com a versão 8
        var configuracoes = await context.PeConfiguracoes.AsNoTracking().ToListAsync();
        var acompanhamento = PeAcompanhamentoAtivo.De(configuracoes);
        var porCiclo = acompanhamento.Ativo
            ? await context.PeSecoesCiclo.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.PorCiclo)
            : new Dictionary<long, string>();

        return new PeModeloDados
        {
            PorCiclo = porCiclo,
            Acompanhamento = acompanhamento,
            ModoNiveis = PeModoNiveis.De(configuracoes),
            Niveis = await context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).ToListAsync(),
            Etapas = await context.PeEtapas.AsNoTracking().OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync(),
            Passos = await context.PePassos.AsNoTracking().OrderBy(p => p.Ordem).ThenBy(p => p.Id).ToListAsync(),
            Secoes = await context.PeSecoes.AsNoTracking().OrderBy(s => s.Ordem).ThenBy(s => s.Id).ToListAsync(),
            Campos = await context.PeCampos.AsNoTracking().OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync(),
            Opcoes = await context.PeOpcoes.AsNoTracking().OrderBy(o => o.Ordem).ThenBy(o => o.Id).ToListAsync(),
            SituacaoPasso = Agrupar(passoNivel.Select(n => (n.PassoId, n.NivelId, n.Situacao))),
            SituacaoSecao = Agrupar(secaoNivel.Select(n => (n.SecaoId, n.NivelId, n.Situacao))),
            SituacaoCampo = Agrupar(campoNivel.Select(n => (n.CampoId, n.NivelId, n.Situacao))),
            OrgaosPorNivel = orgaos.ToDictionary(o => o.NivelId, o => o.Total)
        };
    }

    private static Dictionary<long, Dictionary<long, string>> Agrupar(IEnumerable<(long Item, long Nivel, string Situacao)> linhas) =>
        linhas.GroupBy(l => l.Item).ToDictionary(g => g.Key, g => g.ToDictionary(l => l.Nivel, l => l.Situacao));

    // ── Índices ─────────────────────────────────────────────────────────────

    public IEnumerable<PePasso> PassosDaEtapa(long etapaId, bool incluirExcluidos) =>
        Passos.Where(p => p.EtapaId == etapaId && (incluirExcluidos || p.ExcluidoEm == null));

    public IEnumerable<PeSecao> SecoesDoPasso(long passoId, bool incluirExcluidos) =>
        Secoes.Where(s => s.PassoId == passoId && (incluirExcluidos || s.ExcluidoEm == null));

    public IEnumerable<PeCampo> CamposDaSecao(long secaoId, bool incluirExcluidos) =>
        Campos.Where(c => c.SecaoId == secaoId && (incluirExcluidos || c.ExcluidoEm == null));

    public IEnumerable<PeOpcao> OpcoesDoCampo(long campoId) => Opcoes.Where(o => o.CampoId == campoId);

    public PeSecao? SecaoPorChave(string chave) => Secoes.FirstOrDefault(s => s.Chave == chave);

    /// <summary>O tipo de ciclo da seção (monitoramento ou avaliacao), ou nulo quando ela não é por ciclo.</summary>
    public string? PorCicloDe(long secaoId) => PorCiclo.GetValueOrDefault(secaoId);

    /// <summary>Campo travado ou principal de seção travada: não desliga.</summary>
    public bool CampoTravado(PeCampo campo) =>
        campo.Travado || (campo.Principal && Secoes.FirstOrDefault(s => s.Id == campo.SecaoId)?.Travada == true);

    /// <summary>O primeiro nível ativo pela ordem (o padrão de quem não tem nível escolhido; no modo livre, o nível base).</summary>
    public PeNivel? NivelPadrao() => Niveis.FirstOrDefault(n => n.Ativo);

    /// <summary>
    /// A régua do nível alcançado (F3): para cada nível ativo, pela ordem, a trilha dele sem os
    /// ajustes de órgão (a mesma para todos os órgãos), com a regra do PETIC-DF sem vigente, e as
    /// seções dos passos que contam, já montadas. Montada uma vez por leitura do modelo.
    /// </summary>
    public IReadOnlyList<PeReguaNivel> Regua(bool semPeticVigente)
    {
        if (_reguas.TryGetValue(semPeticVigente, out var pronta)) return pronta;
        var regua = Niveis.Where(n => n.Ativo).Select(n => PeReguaNivel.Montar(this, n, semPeticVigente)).ToList();
        _reguas[semPeticVigente] = regua;
        return regua;
    }

    // ── Respostas do GET modelo ─────────────────────────────────────────────

    public PeModeloResponse Modelo(bool incluirExcluidos) => new()
    {
        ModoNiveis = ModoNiveis.Vigente,
        Niveis = Niveis.Select(Nivel).ToList(),
        Etapas = Etapas.Select(e => Etapa(e, incluirExcluidos)).ToList(),
        SecoesForaDoPdtic = Secoes
            .Where(s => s.Escopo != PeDominios.Escopo.Pdtic && (incluirExcluidos || s.ExcluidoEm == null))
            .OrderBy(s => s.Escopo).ThenBy(s => s.Ordem).ThenBy(s => s.Id)
            .Select(s => Secao(s, incluirExcluidos))
            .ToList()
    };

    public PeNivelResponse Nivel(PeNivel n) => new()
    {
        Id = n.Id,
        Codigo = n.Codigo,
        Nome = n.Nome,
        Descricao = n.Descricao,
        Ordem = n.Ordem,
        Ativo = n.Ativo,
        Orgaos = OrgaosPorNivel.GetValueOrDefault(n.Id)
    };

    public PeEtapaResponse Etapa(PeEtapa e, bool incluirExcluidos) => new()
    {
        Id = e.Id,
        Chave = e.Chave,
        Titulo = e.Titulo,
        Descricao = e.Descricao,
        ReferenciaGuia = e.ReferenciaGuia,
        Ordem = e.Ordem,
        Sistema = e.Sistema,
        Passos = PassosDaEtapa(e.Id, incluirExcluidos).Select(p => Passo(p, incluirExcluidos)).ToList()
    };

    public PePassoResponse Passo(PePasso p, bool incluirExcluidos) => new()
    {
        Id = p.Id,
        EtapaId = p.EtapaId,
        Chave = p.Chave,
        Titulo = p.Titulo,
        OQueFazer = p.OQueFazer,
        BaseLegal = p.BaseLegal,
        ReferenciaGuia = p.ReferenciaGuia,
        Tipo = p.Tipo,
        IncisoDecreto = p.IncisoDecreto,
        Travado = p.Travado,
        AceitaNaoSeAplica = p.AceitaNaoSeAplica,
        Ordem = p.Ordem,
        Sistema = p.Sistema,
        Excluido = p.ExcluidoEm != null,
        Niveis = MapaNiveis(SituacaoPasso.GetValueOrDefault(p.Id)),
        Secoes = SecoesDoPasso(p.Id, incluirExcluidos).Select(s => Secao(s, incluirExcluidos)).ToList()
    };

    public PeSecaoResponse Secao(PeSecao s, bool incluirExcluidos)
    {
        var doPdtic = s.Escopo == PeDominios.Escopo.Pdtic;
        return new PeSecaoResponse
        {
            Id = s.Id,
            PassoId = s.PassoId,
            Chave = s.Chave,
            Escopo = s.Escopo,
            Titulo = s.Titulo,
            Ajuda = s.Ajuda,
            Tipo = s.Tipo,
            PrefixoCodigo = s.PrefixoCodigo,
            Ordem = s.Ordem,
            NoDocumento = s.NoDocumento,
            NaPlanilha = s.NaPlanilha,
            Travada = s.Travada,
            IncisoDecreto = s.IncisoDecreto,
            PorCiclo = PorCicloDe(s.Id),
            Sistema = s.Sistema,
            Excluido = s.ExcluidoEm != null,
            SituacaoGeral = doPdtic ? null : s.SituacaoGeral,
            Niveis = doPdtic ? MapaNiveis(SituacaoSecao.GetValueOrDefault(s.Id)) : new Dictionary<string, string>(),
            Campos = CamposDaSecao(s.Id, incluirExcluidos).Select(c => Campo(c, doPdtic)).ToList()
        };
    }

    public PeCampoResponse Campo(PeCampo c, bool doPdtic) => new()
    {
        Id = c.Id,
        SecaoId = c.SecaoId,
        Chave = c.Chave,
        Rotulo = c.Rotulo,
        Ajuda = c.Ajuda,
        Tipo = c.Tipo,
        Config = Json(c.Config),
        Principal = c.Principal,
        Travado = CampoTravado(c),
        Ordem = c.Ordem,
        NoDocumento = c.NoDocumento,
        NaPlanilha = c.NaPlanilha,
        Largura = c.Largura,
        Sistema = c.Sistema,
        Excluido = c.ExcluidoEm != null,
        SituacaoGeral = doPdtic ? null : c.SituacaoGeral,
        Niveis = doPdtic ? MapaNiveis(SituacaoCampo.GetValueOrDefault(c.Id)) : new Dictionary<string, string>(),
        Opcoes = OpcoesDoCampo(c.Id).Select(Opcao).ToList()
    };

    public static PeOpcaoResponse Opcao(PeOpcao o) => new()
    {
        Id = o.Id,
        CampoId = o.CampoId,
        Valor = o.Valor,
        Rotulo = o.Rotulo,
        Ordem = o.Ordem,
        Ativa = o.Ativa,
        Cor = o.Cor,
        Travada = o.Travada,
        Sistema = o.Sistema
    };

    /// <summary>Todos os níveis, com "desligado" onde não há linha.</summary>
    private Dictionary<string, string> MapaNiveis(Dictionary<long, string>? situacoes) =>
        Niveis.ToDictionary(n => n.Id.ToString(),
            n => situacoes != null && situacoes.TryGetValue(n.Id, out var s) ? s : PeDominios.Situacao.Desligado);

    public static JsonElement Json(string? json)
    {
        using var documento = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? PeConfigCampo.Vazio : json);
        return documento.RootElement.Clone();
    }
}

/// <summary>
/// Resolução da trilha efetiva de um órgão (a tela diz "passo a passo"). Para cada seção e
/// campo: o ajuste do órgão, senão a situação no nível da forma do passo, senão desligado; item
/// travado (e o campo principal de seção travada) nunca desliga: vira obrigatório. A etapa
/// aparece se tiver passo visível; a seção, se o passo aparece; o campo, se a seção aparece (e o
/// campo de ligação, se a seção que ele liga também aparece). Itens apagados não entram. A
/// numeração é pela posição entre os visíveis: etapa N, passo N.M.
/// <list type="bullet">
/// <item>Modo definido (<see cref="Resolver"/>): o passo pela situação no nível do órgão (o
/// escolhido ou o padrão), com o ajuste por cima; a forma de todos os passos é esse nível.</item>
/// <item>Modo livre (<see cref="ResolverLivre"/>, F3): o passo aparece e cobra pelo ajuste do
/// órgão, senão pela situação no nível base (o padrão), senão fica opcional quando está ligado em
/// algum nível ativo; a forma do passo (o nível cujas seções e campos valem) é a escolhida pelo
/// órgão, quando é um nível ativo em que o passo está ligado, senão o nível base (quando ligado
/// nele), senão o primeiro nível ativo em que está ligado. As formas oferecidas são os níveis
/// ativos em que o passo está ligado, sem repetir forma (dois níveis que dão ao passo as mesmas
/// seções e os mesmos campos, com a mesma obrigatoriedade, viram uma opção só, a do mais baixo).</item>
/// </list>
/// </summary>
public static class PeTrilhaResolver
{
    // A situação do passo e o nível da forma dele (no livre, também o que a escolha usa)
    private sealed record PassoResolvido(string Situacao, long Forma, PeFormaLivre? Livre);

    // Modo livre: a forma padrão, a escolhida (quando vale) e os níveis ativos em que o passo está ligado
    private sealed record PeFormaLivre(long Padrao, long? Escolhida, List<PeNivel> LigadoEm);

    /// <summary>Modo definido: a trilha do nível dado, com os ajustes do órgão.</summary>
    public static List<PeTrilhaEtapa> Resolver(PeModeloDados dados, long nivelId,
        IReadOnlyDictionary<(string Tipo, long Id), string> ajustes)
    {
        var passos = new Dictionary<long, PassoResolvido>();
        foreach (var passo in dados.Passos.Where(p => p.ExcluidoEm == null))
        {
            var situacao = Efetiva(ajustes, PeDominios.AlvoAjuste.Passo, passo.Id, dados.SituacaoPasso.GetValueOrDefault(passo.Id),
                passo.Travado, nivelId);
            if (situacao != PeDominios.Situacao.Desligado) passos[passo.Id] = new PassoResolvido(situacao, nivelId, null);
        }
        return Montar(dados, ajustes, passos);
    }

    /// <summary>
    /// Modo livre (F3): a trilha do órgão a partir do nível base (o primeiro ativo), com os ajustes
    /// e a forma escolhida para cada passo (passo → nível).
    /// </summary>
    public static List<PeTrilhaEtapa> ResolverLivre(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes,
        IReadOnlyDictionary<long, long> escolhas)
    {
        var ativos = dados.Niveis.Where(n => n.Ativo).ToList();
        var nivelBase = ativos.FirstOrDefault() ?? throw PeModeloService.ModeloIndisponivel();

        var passos = new Dictionary<long, PassoResolvido>();
        foreach (var passo in dados.Passos.Where(p => p.ExcluidoEm == null))
        {
            var porNivel = dados.SituacaoPasso.GetValueOrDefault(passo.Id);
            string NoNivel(long nivelId) => Efetiva(Vazio, PeDominios.AlvoAjuste.Passo, passo.Id, porNivel, passo.Travado, nivelId);
            var ligadoEm = ativos.Where(n => NoNivel(n.Id) != PeDominios.Situacao.Desligado).ToList();
            var noBase = ligadoEm.Any(n => n.Id == nivelBase.Id);

            string situacao;
            if (ajustes.TryGetValue((PeDominios.AlvoAjuste.Passo, passo.Id), out var ajustada))
                situacao = passo.Travado && ajustada == PeDominios.Situacao.Desligado ? PeDominios.Situacao.Obrigatorio : ajustada;
            else if (noBase)
                situacao = NoNivel(nivelBase.Id);
            else
                situacao = ligadoEm.Count > 0 ? PeDominios.Situacao.Opcional : PeDominios.Situacao.Desligado;
            if (situacao == PeDominios.Situacao.Desligado) continue;

            var padrao = noBase ? nivelBase.Id : ligadoEm.FirstOrDefault()?.Id ?? nivelBase.Id;
            long? escolhida = escolhas.TryGetValue(passo.Id, out var nivelEscolhido) && ligadoEm.Any(n => n.Id == nivelEscolhido)
                ? nivelEscolhido
                : null;
            passos[passo.Id] = new PassoResolvido(situacao, escolhida ?? padrao, new PeFormaLivre(padrao, escolhida, ligadoEm));
        }
        return Montar(dados, ajustes, passos);
    }

    private static readonly IReadOnlyDictionary<(string Tipo, long Id), string> Vazio = new Dictionary<(string, long), string>();

    /// <summary>A situação de um item num nível: o ajuste, senão a do nível, senão desligado; o travado nunca desliga.</summary>
    private static string Efetiva(IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, string tipo, long id,
        Dictionary<long, string>? porNivel, bool travado, long nivelId)
    {
        var situacao = ajustes.TryGetValue((tipo, id), out var ajustada)
            ? ajustada
            : porNivel != null && porNivel.TryGetValue(nivelId, out var doNivel) ? doNivel : PeDominios.Situacao.Desligado;
        return travado && situacao == PeDominios.Situacao.Desligado ? PeDominios.Situacao.Obrigatorio : situacao;
    }

    /// <summary>
    /// As etapas visíveis, com os passos na forma resolvida: primeiro o que fica visível (as
    /// seções de cada passo, no nível da forma dele; as ligações dependem delas), depois os
    /// campos e, no modo livre, as formas oferecidas.
    /// </summary>
    private static List<PeTrilhaEtapa> Montar(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes,
        IReadOnlyDictionary<long, PassoResolvido> passos)
    {
        var secoesVisiveis = new Dictionary<long, string>();
        foreach (var (passoId, resolvido) in passos)
            foreach (var secao in SecoesDoPdtic(dados, passoId))
            {
                var situacao = SituacaoDaSecao(dados, ajustes, secao, resolvido.Forma);
                if (situacao != PeDominios.Situacao.Desligado) secoesVisiveis[secao.Id] = situacao;
            }
        var chavesVisiveis = dados.Secoes.Where(s => secoesVisiveis.ContainsKey(s.Id)).Select(s => s.Chave).ToHashSet();

        var etapas = new List<PeTrilhaEtapa>();
        foreach (var etapa in dados.Etapas)
        {
            var lista = new List<PeTrilhaPasso>();
            foreach (var passo in dados.PassosDaEtapa(etapa.Id, false).Where(p => passos.ContainsKey(p.Id)))
            {
                var resolvido = passos[passo.Id];
                var item = new PeTrilhaPasso
                {
                    Id = passo.Id,
                    Chave = passo.Chave,
                    Titulo = passo.Titulo,
                    OQueFazer = passo.OQueFazer,
                    BaseLegal = passo.BaseLegal,
                    ReferenciaGuia = passo.ReferenciaGuia,
                    Tipo = passo.Tipo,
                    IncisoDecreto = passo.IncisoDecreto,
                    Travado = passo.Travado,
                    AceitaNaoSeAplica = passo.AceitaNaoSeAplica,
                    Situacao = resolvido.Situacao,
                    AjustadoParaOrgao = ajustes.ContainsKey((PeDominios.AlvoAjuste.Passo, passo.Id)),
                    Secoes = SecoesDoPdtic(dados, passo.Id)
                        .Where(s => secoesVisiveis.ContainsKey(s.Id))
                        .Select(s => Secao(dados, ajustes, s, secoesVisiveis[s.Id], chavesVisiveis, resolvido.Forma))
                        .ToList()
                };
                if (resolvido.Livre != null) Formas(dados, ajustes, passo, resolvido, chavesVisiveis, item);
                lista.Add(item);
            }
            if (lista.Count == 0) continue;

            var numero = etapas.Count + 1;
            for (var i = 0; i < lista.Count; i++) lista[i].Numero = $"{numero}.{i + 1}";
            etapas.Add(new PeTrilhaEtapa
            {
                Numero = numero,
                Id = etapa.Id,
                Chave = etapa.Chave,
                Titulo = etapa.Titulo,
                Descricao = etapa.Descricao,
                ReferenciaGuia = etapa.ReferenciaGuia,
                Passos = lista
            });
        }
        return etapas;
    }

    private static IEnumerable<PeSecao> SecoesDoPdtic(PeModeloDados dados, long passoId) =>
        dados.SecoesDoPasso(passoId, false).Where(s => s.Escopo == PeDominios.Escopo.Pdtic);

    private static string SituacaoDaSecao(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, PeSecao secao,
        long nivelId) =>
        Efetiva(ajustes, PeDominios.AlvoAjuste.Secao, secao.Id, dados.SituacaoSecao.GetValueOrDefault(secao.Id), secao.Travada, nivelId);

    private static string SituacaoDoCampo(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, PeSecao secao,
        PeCampo campo, long nivelId) =>
        Efetiva(ajustes, PeDominios.AlvoAjuste.Campo, campo.Id, dados.SituacaoCampo.GetValueOrDefault(campo.Id),
            campo.Travado || (campo.Principal && secao.Travada), nivelId);

    /// <summary>O campo de ligação com uma seção que o órgão não vê some (e o campo sem alvo também).</summary>
    private static bool LigacaoSemAlvo(PeCampo campo, ISet<string> chavesVisiveis)
    {
        if (campo.Tipo != PeDominios.TipoCampo.LigacaoSecao) return false;
        var alvo = PeConfigCampo.SecaoDaLigacao(campo.Config);
        return alvo == null || !chavesVisiveis.Contains(alvo);
    }

    private static PeTrilhaSecao Secao(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, PeSecao secao,
        string situacao, ISet<string> chavesVisiveis, long nivelId)
    {
        var campos = new List<PeTrilhaCampo>();
        foreach (var campo in dados.CamposDaSecao(secao.Id, false))
        {
            var sit = SituacaoDoCampo(dados, ajustes, secao, campo, nivelId);
            if (sit == PeDominios.Situacao.Desligado || LigacaoSemAlvo(campo, chavesVisiveis)) continue;

            campos.Add(new PeTrilhaCampo
            {
                Id = campo.Id,
                Chave = campo.Chave,
                Rotulo = campo.Rotulo,
                Ajuda = campo.Ajuda,
                Tipo = campo.Tipo,
                Config = PeModeloDados.Json(campo.Config),
                Obrigatorio = sit == PeDominios.Situacao.Obrigatorio,
                Principal = campo.Principal,
                Largura = campo.Largura,
                Opcoes = dados.OpcoesDoCampo(campo.Id).Where(o => o.Ativa)
                    .Select(o => new PeTrilhaOpcao { Valor = o.Valor, Rotulo = o.Rotulo, Cor = o.Cor })
                    .ToList()
            });
        }

        return new PeTrilhaSecao
        {
            Id = secao.Id,
            Chave = secao.Chave,
            Titulo = secao.Titulo,
            Ajuda = secao.Ajuda,
            Tipo = secao.Tipo,
            PrefixoCodigo = secao.PrefixoCodigo,
            Situacao = situacao,
            Travada = secao.Travada,
            PorCiclo = dados.PorCicloDe(secao.Id),
            NaPlanilha = secao.NaPlanilha,
            Campos = campos
        };
    }

    /// <summary>
    /// Modo livre: a forma em uso (Detalhe), se o órgão escolheu uma forma que não é a padrão
    /// (DetalheEscolhido) e as formas oferecidas (OpcoesDetalhe, só com duas ou mais), com a
    /// quantidade de seções e de campos visíveis em cada uma. Cada forma é comparada pelo que dá
    /// ao passo (as seções e os campos visíveis, com a obrigatoriedade, e os ajustes do órgão por
    /// cima): níveis com a mesma forma viram a opção do mais baixo.
    /// </summary>
    private static void Formas(PeModeloDados dados, IReadOnlyDictionary<(string Tipo, long Id), string> ajustes, PePasso passo,
        PassoResolvido resolvido, ISet<string> chavesVisiveis, PeTrilhaPasso item)
    {
        var livre = resolvido.Livre!;
        var proprias = SecoesDoPdtic(dados, passo.Id).ToList();
        var deOutrosPassos = new HashSet<string>(chavesVisiveis);
        foreach (var secao in proprias) deOutrosPassos.Remove(secao.Chave);

        // A forma que o nível dá ao passo: a assinatura (para comparar) e as quantidades
        (string Assinatura, int Secoes, int Campos) FormaNoNivel(long nivelId)
        {
            var visiveis = proprias.Select(s => (Secao: s, Situacao: SituacaoDaSecao(dados, ajustes, s, nivelId)))
                .Where(x => x.Situacao != PeDominios.Situacao.Desligado)
                .ToList();
            var alvos = new HashSet<string>(deOutrosPassos);
            foreach (var (secao, _) in visiveis) alvos.Add(secao.Chave);
            var assinatura = new System.Text.StringBuilder();
            var campos = 0;
            foreach (var (secao, situacao) in visiveis)
            {
                assinatura.Append('s').Append(secao.Id).Append(':').Append(situacao).Append(';');
                foreach (var campo in dados.CamposDaSecao(secao.Id, false))
                {
                    var sit = SituacaoDoCampo(dados, ajustes, secao, campo, nivelId);
                    if (sit == PeDominios.Situacao.Desligado || LigacaoSemAlvo(campo, alvos)) continue;
                    assinatura.Append('c').Append(campo.Id).Append(':').Append(sit == PeDominios.Situacao.Obrigatorio ? '1' : '0').Append(';');
                    campos++;
                }
            }
            return (assinatura.ToString(), visiveis.Count, campos);
        }

        // Cada nível em que o passo está ligado, com o representante da forma dele (o mais baixo com a mesma forma)
        var representante = new Dictionary<long, long>();
        var opcoes = new List<(PeNivel Nivel, int Secoes, int Campos)>();
        var porAssinatura = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var nivel in livre.LigadoEm)
        {
            var forma = FormaNoNivel(nivel.Id);
            if (porAssinatura.TryGetValue(forma.Assinatura, out var mesmo))
            {
                representante[nivel.Id] = mesmo;
                continue;
            }
            porAssinatura[forma.Assinatura] = nivel.Id;
            representante[nivel.Id] = nivel.Id;
            opcoes.Add((nivel, forma.Secoes, forma.Campos));
        }

        long Representante(long nivelId) => representante.GetValueOrDefault(nivelId, nivelId);
        var emUso = Representante(resolvido.Forma);
        var nivelEmUso = dados.Niveis.FirstOrDefault(n => n.Id == emUso);
        item.Detalhe = nivelEmUso == null ? null : new PeTrilhaDetalhe { NivelId = nivelEmUso.Id, NivelNome = nivelEmUso.Nome };
        item.DetalheEscolhido = opcoes.Count >= 2 && livre.Escolhida is long escolhida && Representante(escolhida) != Representante(livre.Padrao);
        item.OpcoesDetalhe = opcoes.Count >= 2
            ? opcoes.Select(o => new PeTrilhaOpcaoDetalhe { NivelId = o.Nivel.Id, NivelNome = o.Nivel.Nome, Secoes = o.Secoes, Campos = o.Campos }).ToList()
            : new List<PeTrilhaOpcaoDetalhe>();
        item.FormaPorNivel = representante;
    }
}
