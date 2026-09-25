using api.Planejamento;
using Models.Pgia;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Um degrau da régua do nível alcançado (F3): um nível ativo, a trilha dele sem os ajustes de
/// órgão (a régua é a mesma para todos os órgãos: <see cref="PeTrilhaOrgao.ResolverDefinido"/>,
/// com a regra do PETIC-DF sem vigente), os passos que contam e estão ligados nele (obrigatórios
/// ou opcionais: a forma que o nível dá a cada um vale como forma mais completa para os níveis de
/// baixo), os que contam e são obrigatórios nele e as seções que a análise usa (as dos passos
/// ligados que contam e as dos temas das ações), já montadas.
/// </summary>
public sealed class PeReguaNivel
{
    public required PeNivel Nivel { get; init; }

    public required PeTrilhaOrgao Trilha { get; init; }

    // Os passos que contam e são obrigatórios neste nível, na ordem da trilha
    public required List<PeTrilhaPasso> Obrigatorios { get; init; }

    // Os passos que contam e estão ligados neste nível (obrigatórios ou opcionais), pelo id, na forma deste nível
    public required IReadOnlyDictionary<long, PeTrilhaPasso> Ligados { get; init; }

    public required List<PeSecaoDoDono> Secoes { get; init; }

    public static PeReguaNivel Montar(PeModeloDados dados, PeNivel nivel, bool semPeticVigente)
    {
        // A régua não é de um órgão: o órgão da trilha só completa o objeto
        var trilha = PeTrilhaOrgao.ResolverDefinido(dados, new PgiaOrgao(), nivel.Id, new Dictionary<(string, long), string>(), semPeticVigente);
        var etapas = PeEdicaoPdtic.EtapasDosPassos(trilha);
        var contam = trilha.Passos.Where(p => PeNivelAlcancado.Conta(etapas.GetValueOrDefault(p.Id), p)).ToList();
        var secoes = contam.SelectMany(p => p.Secoes).ToList();
        // A conferência dos temas lê as ações e as justificativas dos temas sem ação
        foreach (var chave in new[] { PeDominios.TemaDecreto.SecaoAcoes, PeDominios.TemaDecreto.SecaoJustificativas })
            if (trilha.Secao(chave) is { } visivel && secoes.All(s => s.Id != visivel.Secao.Id))
                secoes.Add(visivel.Secao);
        return new PeReguaNivel
        {
            Nivel = nivel,
            Trilha = trilha,
            Obrigatorios = contam.Where(p => p.Situacao == PeDominios.Situacao.Obrigatorio).ToList(),
            Ligados = contam.ToDictionary(p => p.Id),
            Secoes = secoes.Select(trilha.Montar).ToList()
        };
    }
}

/// <summary>
/// O nível que o PDTIC alcançou (F3; nos dois modos dos níveis, a tela usa no modo livre).
/// <list type="bullet">
/// <item>Régua: para cada nível ativo, pela ordem, a trilha dele sem os ajustes de órgão
/// (<see cref="PeReguaNivel"/>), e o que falta em cada passo na forma que o nível dá a ele, pela
/// mesma análise da situação dos passos, sobre os mesmos dados lidos (os registros das seções que
/// a forma atual do órgão esconde também contam).</item>
/// <item>Contam os passos das etapas 1 a 3 do grupo da elaboração, dos tipos dados, conferência dos
/// temas e aprovação (documento, envio, deliberação e publicação são iguais em todos os níveis).</item>
/// <item>Um passo atende a um nível quando está completo na forma desse nível ou numa forma mais
/// completa: a de um nível ativo acima dele em que o passo está ligado (a forma mais completa
/// substitui a mais simples; no modelo inicial, as notas GUT do 2.9 no lugar da prioridade simples
/// do Básico). O comentário aberto da SGDI não muda a régua: o passo conta pelo que os dados
/// dele têm, com ou sem comentário (a situação do passo continua "atencao" no passo a passo e no
/// próximo passo; só a régua mede o conteúdo). O "não se aplica" não vale num passo obrigatório;
/// o PDTIC registrado fora do sistema não é calculado.</item>
/// <item>O PDTIC atende a um nível quando todo passo que conta e é obrigatório nele atende a ele; o
/// nível alcançado é o mais alto que o PDTIC atende. Com a substituição, atender a um nível já
/// cobre os de baixo nos passos em comum: exigir também os de baixo só mudaria o resultado num
/// passo obrigatório num nível e não obrigatório num nível acima (o modelo inicial não tem), e lá
/// só baixaria o nível de um PDTIC que tem tudo o que o nível mais alto pede.</item>
/// <item>O próximo é o nível seguinte ao alcançado (sem nível alcançado, o primeiro), com o que
/// falta: cada passo que conta, obrigatório nele e que não o atende, com o número no passo a passo
/// do órgão (nulo quando o órgão não vê o passo), o título e o motivo (o mesmo texto do envio). O
/// motivo sai da forma em uso no órgão quando ela é igual ou mais completa que a do próximo nível
/// (é a forma que a equipe vê); senão, da forma do próximo nível, e, no modo livre, com "Use a
/// forma do nível X neste passo.".</item>
/// </list>
/// Não lê o banco: tudo sai da <see cref="PeLeituraDaSituacao"/> e do modelo (a régua é montada uma
/// vez por leitura do modelo). O painel, a conformidade e a lista dos PDTICs calculam aqui o nível
/// de todos os órgãos (<see cref="Alcancado"/>, quando basta o nível).
/// </summary>
public static class PeNivelAlcancado
{
    public const string MotivoExterno = "O PDTIC foi aprovado fora do sistema: o nível não é calculado.";

    /// <summary>O passo conta para o nível: etapas 1 a 3 (grupo da elaboração), dos tipos dados, conferência dos temas e aprovação.</summary>
    public static bool Conta(string? etapaChave, PeTrilhaPasso passo) =>
        PeEdicaoPdtic.GrupoDe(etapaChave, passo.Tipo) == PeEdicaoPdtic.Grupo.Elaboracao
        && passo.Tipo is PeDominios.TipoPasso.Dados or PeDominios.TipoPasso.ConferenciaTemas or PeDominios.TipoPasso.Aprovacao;

    public static PeNivelDoPdticResponse Calcular(PePdtic pdtic, PeTrilhaOrgao trilhaDoOrgao, PeLeituraDaSituacao leitura)
    {
        var dados = trilhaDoOrgao.Dados;
        var resposta = new PeNivelDoPdticResponse { Modo = dados.ModoNiveis.Vigente };
        if (pdtic.RegistradoExternamente)
        {
            resposta.Motivo = MotivoExterno;
            return resposta;
        }

        var regua = dados.Regua(leitura.SemPeticVigente);
        if (regua.Count == 0) return resposta;
        var formas = new FormasNaRegua(pdtic.Id, regua, leitura);
        var alcancado = IndiceAlcancado(regua, formas);

        if (alcancado >= 0)
        {
            resposta.AlcancadoId = regua[alcancado].Nivel.Id;
            resposta.AlcancadoNome = regua[alcancado].Nivel.Nome;
        }
        if (alcancado + 1 >= regua.Count) return resposta;

        var indiceProximo = alcancado + 1;
        var proximo = regua[indiceProximo];
        resposta.ProximoId = proximo.Nivel.Id;
        resposta.ProximoNome = proximo.Nivel.Nome;
        resposta.Faltam = proximo.Obrigatorios
            .Where(p => !formas.PassoAtende(indiceProximo, p.Id))
            .Select(p =>
            {
                var doOrgao = trilhaDoOrgao.Passo(p.Id);
                return new PeNivelFaltaResponse
                {
                    PassoId = p.Id,
                    PassoNumero = doOrgao?.Numero,
                    PassoTitulo = p.Titulo,
                    Motivo = Motivo(trilhaDoOrgao, regua, formas, indiceProximo, p.Id, doOrgao)
                };
            })
            .ToList();
        return resposta;
    }

    /// <summary>
    /// Só o nível que o PDTIC alcançou, pela mesma régua (sem o próximo nem o que falta): nulo sem
    /// nível alcançado e no PDTIC registrado fora do sistema. Não precisa da trilha do órgão (a
    /// régua é a mesma para todos os órgãos): a lista dos PDTICs usa com uma leitura da situação
    /// para a página inteira.
    /// </summary>
    public static PeNivel? Alcancado(PePdtic pdtic, PeModeloDados dados, PeLeituraDaSituacao leitura)
    {
        if (pdtic.RegistradoExternamente) return null;
        var regua = dados.Regua(leitura.SemPeticVigente);
        var indice = IndiceAlcancado(regua, new FormasNaRegua(pdtic.Id, regua, leitura));
        return indice < 0 ? null : regua[indice].Nivel;
    }

    /// <summary>A posição na régua do mais alto nível que o PDTIC atende (de cima para baixo: o primeiro atendido), ou -1.</summary>
    private static int IndiceAlcancado(IReadOnlyList<PeReguaNivel> regua, FormasNaRegua formas)
    {
        for (var i = regua.Count - 1; i >= 0; i--)
            if (formas.Atende(i)) return i;
        return -1;
    }

    /// <summary>
    /// O motivo de um passo que não atende ao próximo nível: o que falta na forma em uso no órgão,
    /// quando ela é igual ou mais completa que a do próximo nível; senão o que falta na forma do
    /// próximo nível, com a nota da forma (<see cref="NotaDaForma"/>). O comentário aberto não é
    /// motivo: a régua mede o conteúdo.
    /// </summary>
    private static string Motivo(PeTrilhaOrgao trilha, IReadOnlyList<PeReguaNivel> regua, FormasNaRegua formas, int proximo, long passoId,
        PeTrilhaPasso? doOrgao)
    {
        var emUso = NivelDaFormaEmUso(trilha, regua, doOrgao);
        if (emUso >= proximo && regua[emUso].Ligados.ContainsKey(passoId) && formas.Falta(emUso, passoId) is { } daFormaEmUso)
            return daFormaEmUso;
        return (formas.Falta(proximo, passoId) ?? string.Empty) + NotaDaForma(trilha, doOrgao, regua[proximo].Nivel);
    }

    /// <summary>
    /// A posição na régua do nível da forma em uso no órgão (no modo livre, o Detalhe do passo; no
    /// definido, o nível do órgão), ou -1 quando o órgão não vê o passo ou o nível não está na régua.
    /// </summary>
    private static int NivelDaFormaEmUso(PeTrilhaOrgao trilha, IReadOnlyList<PeReguaNivel> regua, PeTrilhaPasso? doOrgao)
    {
        if (doOrgao == null) return -1;
        var nivelId = trilha.Livre ? doOrgao.Detalhe?.NivelId : trilha.Nivel.Id;
        for (var i = 0; nivelId != null && i < regua.Count; i++)
            if (regua[i].Nivel.Id == nivelId)
                return i;
        return -1;
    }

    /// <summary>
    /// No modo livre, quando o passo está numa forma mais simples que a do próximo nível (a forma
    /// que o próximo nível dá ao passo é outra, de um nível acima da forma em uso): " Use a forma
    /// do nível X neste passo."; senão, vazio.
    /// </summary>
    private static string NotaDaForma(PeTrilhaOrgao trilha, PeTrilhaPasso? passo, PeNivel proximo)
    {
        if (!trilha.Livre || passo?.Detalhe == null) return string.Empty;
        if (!passo.FormaPorNivel.TryGetValue(proximo.Id, out var forma) || forma == passo.Detalhe.NivelId) return string.Empty;
        var niveis = trilha.Dados.Niveis;
        var alvo = niveis.FirstOrDefault(n => n.Id == forma);
        var emUso = niveis.FirstOrDefault(n => n.Id == passo.Detalhe.NivelId);
        if (alvo == null || emUso == null || niveis.IndexOf(emUso) > niveis.IndexOf(alvo)) return string.Empty;
        return $" Use a forma do nível {alvo.Nome} neste passo.";
    }

    /// <summary>
    /// As formas dos passos na régua, para um PDTIC: a análise dos registros na forma de cada nível
    /// (feita uma vez, quando o nível é usado) e o que falta em cada passo em cada forma. Os
    /// comentários abertos não entram (a régua mede o conteúdo).
    /// </summary>
    private sealed class FormasNaRegua
    {
        private readonly long _pdticId;
        private readonly IReadOnlyList<PeReguaNivel> _regua;
        private readonly PeLeituraDaSituacao _leitura;
        private readonly PeAnaliseDono?[] _analises;
        private readonly Dictionary<long, PeSecaoAnalisada>?[] _porSecao;
        private readonly List<PeTemaResponse>?[] _temas;
        private readonly Dictionary<(int Nivel, long Passo), string?> _faltas = new();

        public FormasNaRegua(long pdticId, IReadOnlyList<PeReguaNivel> regua, PeLeituraDaSituacao leitura)
        {
            _pdticId = pdticId;
            _regua = regua;
            _leitura = leitura;
            _analises = new PeAnaliseDono?[regua.Count];
            _porSecao = new Dictionary<long, PeSecaoAnalisada>?[regua.Count];
            _temas = new List<PeTemaResponse>?[regua.Count];
        }

        /// <summary>O PDTIC atende ao nível: todo passo que conta e é obrigatório nele atende a ele.</summary>
        public bool Atende(int nivel) => _regua[nivel].Obrigatorios.All(p => PassoAtende(nivel, p.Id));

        /// <summary>
        /// O passo atende ao nível: completo na forma do nível ou numa forma mais completa (a de um
        /// nível ativo acima dele em que o passo está ligado), com ou sem comentário aberto.
        /// </summary>
        public bool PassoAtende(int nivel, long passoId)
        {
            for (var acima = nivel; acima < _regua.Count; acima++)
                if (_regua[acima].Ligados.ContainsKey(passoId) && Falta(acima, passoId) == null)
                    return true;
            return false;
        }

        /// <summary>O que falta no passo na forma do nível (o passo ligado nele), ou nulo quando está completo nela.</summary>
        public string? Falta(int nivel, long passoId)
        {
            if (_faltas.TryGetValue((nivel, passoId), out var guardada)) return guardada;
            var degrau = _regua[nivel];
            var passo = degrau.Ligados[passoId];
            var porSecao = PorSecao(nivel);
            var falta = passo.Tipo switch
            {
                PeDominios.TipoPasso.ConferenciaTemas =>
                    PePdticService.FaltaNaConferencia(passo, porSecao, _temas[nivel] ??= PePdticService.Temas(degrau.Trilha, _analises[nivel]!)),
                PeDominios.TipoPasso.Aprovacao => PePdticService.FaltaNaAprovacao(passo, porSecao, new List<string>()),
                _ => PePdticService.FaltaNasSecoes(passo, porSecao)
            };
            _faltas[(nivel, passoId)] = falta;
            return falta;
        }

        private Dictionary<long, PeSecaoAnalisada> PorSecao(int nivel)
        {
            if (_porSecao[nivel] is { } pronto) return pronto;
            var analise = _leitura.Analisar(_pdticId, _regua[nivel].Secoes);
            _analises[nivel] = analise;
            return _porSecao[nivel] = analise.Secoes.ToDictionary(s => s.Secao.Secao.Id);
        }
    }
}
