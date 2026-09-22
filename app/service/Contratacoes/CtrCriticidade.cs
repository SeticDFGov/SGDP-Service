using System.Text.Encodings.Web;
using System.Text.Json;
using api.Common;
using api.Contratacoes;
using Models.Contratacoes;

namespace service.Contratacoes;

/// <summary>
/// Criticidade da contratação DERIVADA das respostas aos critérios do art. 11, § 3º, da
/// IN SGDI nº 1/2026 (FONTE ÚNICA; o front repete a conta só para a prévia ao vivo, em
/// <c>src/api/contratacoes/criticidade.ts</c>). A IN não fixa fórmula: a regra é a da
/// SGDI (rascunho do dono do produto, 2026-09-22), toda em constantes desta classe.
///
///  - I (alinhamento à EGD/DF): estar alinhado é BOM e baixa a criticidade: Sim tira 1 ponto;
///  - III (compartilhamento): Sim vale 1 ponto;
///  - II, IV e VI (impactos e riscos): Nenhum 0, Baixo 1, Médio 2, Alto 3;
///  - IV aceita ainda "Não foi possível avaliar com as informações apresentadas", que vale 0
///    (como Nenhum; decisão do usuário, 2026-09-22);
///  - V (tecnologias emergentes, nuvem ou IA): Sim vale 3, é crítico por si;
///  - VII (valor estimado no limite das alíneas a, b ou c): Sim é Alta, seja qual for o resto.
///
/// A soma nunca fica negativa. Baixa até 2 pontos, Média de 3 a 5, Alta com 6 ou mais.
/// </summary>
public static class CtrCriticidade
{
    public const int PontosSim = 1;

    /// <summary>O único critério que baixa a criticidade: alinhado à EGD/DF tira um ponto.</summary>
    public const int PontosAlinhamentoEgd = -1;

    public const int PontosTecnologiasEmergentes = 3;

    /// <summary>Critério IV sem avaliação possível: não pesa na criticidade, como "Nenhum".</summary>
    public const int PontosNaoFoiPossivelAvaliar = 0;

    public const int PontosMinimosMedia = 3;

    public const int PontosMinimosAlta = 6;

    // Guarda "Não" e "Médio" legíveis no jsonb (o padrão escaparia os acentos)
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Pontos de UMA resposta. Resposta fora do domínio do critério conta zero.</summary>
    public static int Pontos(string codigo, string resposta)
    {
        switch (codigo)
        {
            case CtrDominios.CriterioCriticidade.AlinhamentoEgd:
                return resposta == CtrDominios.CriterioCriticidade.Sim ? PontosAlinhamentoEgd : 0;
            case CtrDominios.CriterioCriticidade.Compartilhamento:
                return resposta == CtrDominios.CriterioCriticidade.Sim ? PontosSim : 0;
            case CtrDominios.CriterioCriticidade.TecnologiasEmergentes:
                return resposta == CtrDominios.CriterioCriticidade.Sim ? PontosTecnologiasEmergentes : 0;
            case CtrDominios.CriterioCriticidade.ImpactoArquitetura
                when resposta == CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliar:
                return PontosNaoFoiPossivelAvaliar;
            case CtrDominios.CriterioCriticidade.ImpactoServicos:
            case CtrDominios.CriterioCriticidade.ImpactoArquitetura:
            case CtrDominios.CriterioCriticidade.RiscosSeguranca:
                var grau = Array.IndexOf(CtrDominios.CriterioCriticidade.RespostasGrau, resposta);
                return grau < 0 ? 0 : grau;
            default:
                // VII não pontua: decide sozinho (ValorNoLimite)
                return 0;
        }
    }

    /// <summary>
    /// Soma dos pontos dos sete critérios (critério ausente vale a resposta padrão), nunca
    /// negativa: o alinhamento à EGD só desconta o que os outros critérios somaram.
    /// </summary>
    public static int PontosTotais(IReadOnlyDictionary<string, string> respostas) =>
        Math.Max(0, CtrDominios.CriterioCriticidade.Todos.Sum(c => Pontos(c, RespostaOuPadrao(respostas, c))));

    /// <summary>O valor estimado atinge o limite do inciso VII (alíneas a, b ou c).</summary>
    public static bool ValorNoLimite(IReadOnlyDictionary<string, string> respostas) =>
        RespostaOuPadrao(respostas, CtrDominios.CriterioCriticidade.ValorEstimado)
        == CtrDominios.CriterioCriticidade.Sim;

    /// <summary>A criticidade das respostas (já normalizadas).</summary>
    public static string Calcular(IReadOnlyDictionary<string, string> respostas)
    {
        if (ValorNoLimite(respostas)) return CtrDominios.Criticidade.Alta;

        var pontos = PontosTotais(respostas);
        if (pontos >= PontosMinimosAlta) return CtrDominios.Criticidade.Alta;
        if (pontos >= PontosMinimosMedia) return CtrDominios.Criticidade.Media;
        return CtrDominios.Criticidade.Baixa;
    }

    /// <summary>
    /// Respostas como o cliente mandou (chave = código do critério) viram o dicionário
    /// canônico: os sete critérios, códigos e respostas na grafia do domínio (comparação sem
    /// acento nem caixa), critério ausente com a resposta padrão. Código ou resposta
    /// desconhecidos são recusados com a mensagem que nomeia o problema.
    /// </summary>
    public static Dictionary<string, string> Normalizar(IReadOnlyDictionary<string, string> respostas)
    {
        var canonico = new Dictionary<string, string>();

        foreach (var (chave, valor) in respostas)
        {
            var codigo = CtrDominios.CriterioCriticidade.Todos
                .FirstOrDefault(c => CtrCsv.Normalizar(c) == CtrCsv.Normalizar(chave))
                ?? throw new ApiException(ErrorCode.CtrDominioInvalido,
                    $"Critério de criticidade desconhecido: {chave}");

            // Resposta vazia = não respondido (vale o padrão), como célula vazia na planilha
            if (string.IsNullOrWhiteSpace(valor)) continue;

            var resposta = ResolverResposta(codigo, valor)
                ?? throw new ApiException(ErrorCode.CtrDominioInvalido,
                    $"Resposta inválida no critério {codigo} da criticidade: {valor}");

            canonico[codigo] = resposta;
        }

        foreach (var codigo in CtrDominios.CriterioCriticidade.Todos)
            canonico.TryAdd(codigo, CtrDominios.CriterioCriticidade.RespostaPadrao(codigo));

        return canonico;
    }

    /// <summary>
    /// Resposta de UM critério na grafia do domínio (comparação sem acento nem caixa), ou null
    /// quando não é resposta daquele critério. No IV aceita também o atalho "Não foi possível
    /// avaliar", que vira sempre a frase completa. Fonte única da API e da planilha.
    /// </summary>
    public static string? ResolverResposta(string codigo, string valor)
    {
        var alvo = CtrCsv.Normalizar(valor);

        var resposta = CtrDominios.CriterioCriticidade.RespostasDe(codigo)
            .FirstOrDefault(r => CtrCsv.Normalizar(r) == alvo);
        if (resposta != null) return resposta;

        return codigo == CtrDominios.CriterioCriticidade.ImpactoArquitetura
               && alvo == CtrCsv.Normalizar(CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliarAtalho)
            ? CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliar
            : null;
    }

    /// <summary>Jsonb da coluna, sempre na ordem dos critérios (comparável byte a byte).</summary>
    public static string Serializar(IReadOnlyDictionary<string, string> respostas)
    {
        var ordenado = new Dictionary<string, string>();
        foreach (var codigo in CtrDominios.CriterioCriticidade.Todos)
        {
            if (respostas.TryGetValue(codigo, out var resposta)) ordenado[codigo] = resposta;
        }

        // Chaves fora do domínio (antes da normalização) vão junto, para a validação recusá-las
        foreach (var (chave, valor) in respostas)
        {
            ordenado.TryAdd(chave, valor);
        }

        return JsonSerializer.Serialize(ordenado, Json);
    }

    /// <summary>Nulo/vazio = critérios não avaliados.</summary>
    public static Dictionary<string, string>? Desserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json);
    }

    private static string RespostaOuPadrao(IReadOnlyDictionary<string, string> respostas, string codigo) =>
        respostas.TryGetValue(codigo, out var resposta) && !string.IsNullOrWhiteSpace(resposta)
            ? resposta
            : CtrDominios.CriterioCriticidade.RespostaPadrao(codigo);
}
