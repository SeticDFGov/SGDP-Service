using api.Planejamento;
using Models.Pgia;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Um degrau da régua do nível alcançado (F3): um nível ativo, a trilha dele sem os ajustes de
/// órgão (a régua é a mesma para todos os órgãos: <see cref="PeTrilhaOrgao.ResolverDefinido"/>,
/// com a regra do PETIC-DF sem vigente), os passos que contam e são obrigatórios nele e as seções
/// que a análise usa (as desses passos e as dos temas das ações), já montadas.
/// </summary>
public sealed class PeReguaNivel
{
    public required PeNivel Nivel { get; init; }

    public required PeTrilhaOrgao Trilha { get; init; }

    // Os passos que contam e são obrigatórios neste nível, na ordem da trilha
    public required List<PeTrilhaPasso> Obrigatorios { get; init; }

    public required List<PeSecaoDoDono> Secoes { get; init; }

    public static PeReguaNivel Montar(PeModeloDados dados, PeNivel nivel, bool semPeticVigente)
    {
        // A régua não é de um órgão: o órgão da trilha só completa o objeto
        var trilha = PeTrilhaOrgao.ResolverDefinido(dados, new PgiaOrgao(), nivel.Id, new Dictionary<(string, long), string>(), semPeticVigente);
        var etapas = PeEdicaoPdtic.EtapasDosPassos(trilha);
        var obrigatorios = trilha.Passos
            .Where(p => PeNivelAlcancado.Conta(etapas.GetValueOrDefault(p.Id), p) && p.Situacao == PeDominios.Situacao.Obrigatorio)
            .ToList();
        var secoes = obrigatorios.SelectMany(p => p.Secoes).ToList();
        // A conferência dos temas lê as ações e as justificativas dos temas sem ação
        foreach (var chave in new[] { PeDominios.TemaDecreto.SecaoAcoes, PeDominios.TemaDecreto.SecaoJustificativas })
            if (trilha.Secao(chave) is { } visivel && secoes.All(s => s.Id != visivel.Secao.Id))
                secoes.Add(visivel.Secao);
        return new PeReguaNivel
        {
            Nivel = nivel,
            Trilha = trilha,
            Obrigatorios = obrigatorios,
            Secoes = secoes.Select(trilha.Montar).ToList()
        };
    }
}

/// <summary>
/// O nível que o PDTIC alcançou (F3; nos dois modos dos níveis, a tela usa no modo livre).
/// <list type="bullet">
/// <item>Régua: para cada nível ativo, pela ordem, a trilha dele sem os ajustes de órgão
/// (<see cref="PeReguaNivel"/>), e a situação dos passos do PDTIC nessa trilha, pela mesma análise
/// da situação dos passos, sobre os mesmos dados lidos (os registros das seções que a forma atual
/// do órgão esconde também contam).</item>
/// <item>Contam os passos das etapas 1 a 3 do grupo da elaboração, dos tipos dados, conferência dos
/// temas e aprovação (documento, envio, deliberação e publicação são iguais em todos os níveis).</item>
/// <item>O PDTIC atende a um nível quando todo passo que conta e é obrigatório nele está feito (o
/// passo com comentário aberto não está: é a mesma análise da situação). O "não se aplica" não vale
/// num passo obrigatório, e o PDTIC registrado fora do sistema não é calculado.</item>
/// <item>O nível alcançado é o mais alto que o PDTIC atende junto com todos os de antes; o próximo, o
/// seguinte (sem nível alcançado, o primeiro), com o que falta: cada passo que conta, obrigatório
/// nele e não resolvido, com o número no passo a passo do órgão (nulo quando o órgão não vê o passo),
/// o título e o que falta (o mesmo texto do envio). No modo livre, quando o passo está numa forma
/// mais simples que a do próximo nível, o texto manda usar a forma dele.</item>
/// </list>
/// Não lê o banco: tudo sai da <see cref="PeLeituraDaSituacao"/> e do modelo (a régua é montada uma
/// vez por leitura do modelo). O painel e a conformidade calculam aqui o nível de todos os órgãos.
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
        var abertos = leitura.Abertos(pdtic.Id);

        var alcancado = -1;
        List<(PeTrilhaPasso Passo, string Falta)>? doProximo = null;
        for (var i = 0; i < regua.Count; i++)
        {
            var faltam = Faltam(pdtic, regua[i], leitura, abertos);
            if (faltam.Count > 0)
            {
                doProximo = faltam;
                break;
            }
            alcancado = i;
        }

        if (alcancado >= 0)
        {
            resposta.AlcancadoId = regua[alcancado].Nivel.Id;
            resposta.AlcancadoNome = regua[alcancado].Nivel.Nome;
        }
        if (alcancado + 1 >= regua.Count) return resposta;

        var proximo = regua[alcancado + 1];
        resposta.ProximoId = proximo.Nivel.Id;
        resposta.ProximoNome = proximo.Nivel.Nome;
        resposta.Faltam = (doProximo ?? new List<(PeTrilhaPasso, string)>())
            .Select(f =>
            {
                var doOrgao = trilhaDoOrgao.Passo(f.Passo.Id);
                return new PeNivelFaltaResponse
                {
                    PassoId = f.Passo.Id,
                    PassoNumero = doOrgao?.Numero,
                    PassoTitulo = f.Passo.Titulo,
                    Motivo = f.Falta + NotaDaForma(trilhaDoOrgao, doOrgao, proximo.Nivel)
                };
            })
            .ToList();
        return resposta;
    }

    /// <summary>Os passos que contam, obrigatórios no nível e não resolvidos nele, com o que falta.</summary>
    private static List<(PeTrilhaPasso Passo, string Falta)> Faltam(PePdtic pdtic, PeReguaNivel nivel, PeLeituraDaSituacao leitura,
        IReadOnlyDictionary<long, int> abertos)
    {
        var faltam = new List<(PeTrilhaPasso, string)>();
        if (nivel.Obrigatorios.Count == 0) return faltam;
        var analise = leitura.Analisar(pdtic.Id, nivel.Secoes);
        var porSecao = analise.Secoes.ToDictionary(s => s.Secao.Secao.Id);
        List<PeTemaResponse>? temas = null;

        foreach (var passo in nivel.Obrigatorios)
        {
            string? falta;
            if (abertos.GetValueOrDefault(passo.Id) > 0)
                falta = PePdticService.FaltaComentarioAberto;
            else
                falta = passo.Tipo switch
                {
                    PeDominios.TipoPasso.ConferenciaTemas =>
                        PePdticService.FaltaNaConferencia(passo, porSecao, temas ??= PePdticService.Temas(nivel.Trilha, analise)),
                    PeDominios.TipoPasso.Aprovacao => PePdticService.FaltaNaAprovacao(passo, porSecao, new List<string>()),
                    _ => PePdticService.FaltaNasSecoes(passo, porSecao)
                };
            if (falta != null) faltam.Add((passo, falta));
        }
        return faltam;
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
}
