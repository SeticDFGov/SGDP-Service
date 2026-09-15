using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Contratacoes;
using Models.Contratacoes;
using Models.Pgia;
using service.Pgia;

namespace service.Contratacoes;

/// <summary>
/// Classificação de riscos do processo — FONTE ÚNICA das regras do módulo: validação
/// do envio, resultado e enquadramento, pontuação, nível de cada risco declarado pela
/// matriz 5 × 5 da CGDF e os predicados EF dos filtros.
///
/// Reusa SÓ fontes estáticas de domínio do PGIA (PgiaQuesitos, ChecklistIncisos,
/// ResultadoRisco, EscalaCgdf e PgiaValidacoes.EmailValido), sem alterar nada dele. A
/// regra de resultado/enquadramento do PGIA é privada do PgiaSistemaService, por isso
/// está REPLICADA aqui — a equivalência é provada por teste (CtrClassificacaoRiscoTest).
/// Diferença deliberada: os riscos declarados aceitam a escala COMPLETA da CGDF (no
/// PGIA o grupo "Outros" fica no quadrante baixo, porque lá os riscos altos já estão
/// nos arts. 15 a 17).
/// </summary>
public static class CtrClassificacaoRisco
{
    public const int MaximoRiscosDeclarados = 20;

    public const string MensagemTudoNenhuma =
        "Como nenhuma das situações dos três grupos se aplica, declare ao menos um risco da contratação.";

    // ── Matriz da CGDF ────────────────────────────────────────────────────────

    private const string B = CtrDominios.NivelRisco.Baixo;
    private const string M = CtrDominios.NivelRisco.Medio;
    private const string A = CtrDominios.NivelRisco.Alto;
    private const string E = CtrDominios.NivelRisco.Extremo;

    // Colunas: probabilidade, do Improvável ao Quase certo
    private static string[] Probabilidades => PgiaDominios.EscalaCgdf.Probabilidade.Todos;

    private static string[] Consequencias => PgiaDominios.EscalaCgdf.Consequencia.Todos;

    // Linhas: consequência, da Catastrófica (topo) à Desprezível
    private static readonly string[] LinhasConsequencia =
    {
        PgiaDominios.EscalaCgdf.Consequencia.Catastrofica,
        PgiaDominios.EscalaCgdf.Consequencia.Maior,
        PgiaDominios.EscalaCgdf.Consequencia.Moderada,
        PgiaDominios.EscalaCgdf.Consequencia.Menor,
        PgiaDominios.EscalaCgdf.Consequencia.Desprezivel
    };

    // EXATAMENTE a disposição de MATRIZ_RISCO_CGDF do front (v=Baixo, a=Médio, l=Alto, r=Extremo)
    private static readonly string[][] Matriz =
    {
        //                  Improvável Raro Possível Provável Quase certo
        /* Catastrófica */ new[] { M, A, E, E, E },
        /* Maior        */ new[] { M, M, A, E, E },
        /* Moderada     */ new[] { B, M, M, A, E },
        /* Menor        */ new[] { B, B, M, A, A },
        /* Desprezível  */ new[] { B, B, B, M, A }
    };

    /// <summary>
    /// Nível do risco declarado pela célula da matriz — FONTE ÚNICA, nunca gravado.
    /// Null fora da escala (a validação recusa antes e o CHECK do banco também).
    /// </summary>
    public static string? CalcularNivel(string? probabilidade, string? consequencia)
    {
        var coluna = Array.IndexOf(Probabilidades, probabilidade);
        var linha = Array.IndexOf(LinhasConsequencia, consequencia);
        return coluna < 0 || linha < 0 ? null : Matriz[linha][coluna];
    }

    /// <summary>Posição do nível na ordem crescente (Baixo = 0 … Extremo = 3); -1 fora do domínio.</summary>
    public static int OrdemDoNivel(string? nivel) => Array.IndexOf(CtrDominios.NivelRisco.Todos, nivel);

    /// <summary>Maior nível entre os riscos informados; null quando não há nenhum.</summary>
    public static string? CalcularNivelMaximo(IEnumerable<(string Probabilidade, string Consequencia)> riscos)
    {
        string? maximo = null;
        foreach (var (probabilidade, consequencia) in riscos)
        {
            var nivel = CalcularNivel(probabilidade, consequencia);
            if (nivel != null && OrdemDoNivel(nivel) > OrdemDoNivel(maximo)) maximo = nivel;
        }
        return maximo;
    }

    /// <summary>Pares (probabilidade, consequência) cujas células têm o nível; vazio fora do domínio.</summary>
    public static IReadOnlyList<(string Probabilidade, string Consequencia)> ParesDoNivel(string? nivel)
    {
        var pares = new List<(string Probabilidade, string Consequencia)>();
        for (var linha = 0; linha < LinhasConsequencia.Length; linha++)
            for (var coluna = 0; coluna < Probabilidades.Length; coluna++)
                if (Matriz[linha][coluna] == nivel)
                    pares.Add((Probabilidades[coluna], LinhasConsequencia[linha]));
        return pares;
    }

    // ── Resultado, enquadramento e pontuação ──────────────────────────────────

    /// <summary>
    /// Réplica da regra do PGIA (PgiaSistemaService.AvaliarClassificacao): art. 15 vence o
    /// 16, que vence o 17; sem inciso marcado, Baixo Risco sem enquadramento. O
    /// enquadramento aponta o PRIMEIRO inciso na ordem do artigo, não o primeiro digitado.
    /// </summary>
    public static (string Resultado, string? Enquadramento) CalcularResultado(
        IEnumerable<string>? q15, IEnumerable<string>? q16, IEnumerable<string>? q17)
    {
        var inciso15 = PrimeiroNaOrdemDoArtigo(q15, PgiaDominios.ChecklistIncisos.Art15);
        if (inciso15 != null) return (PgiaDominios.ResultadoRisco.Excessivo, $"art. 15, {inciso15}");

        var inciso16 = PrimeiroNaOrdemDoArtigo(q16, PgiaDominios.ChecklistIncisos.Art16);
        if (inciso16 != null) return (PgiaDominios.ResultadoRisco.Alto, $"art. 16, {inciso16}");

        var inciso17 = PrimeiroNaOrdemDoArtigo(q17, PgiaDominios.ChecklistIncisos.Art17);
        if (inciso17 != null) return (PgiaDominios.ResultadoRisco.Moderado, $"art. 17, {inciso17}");

        return (PgiaDominios.ResultadoRisco.Baixo, null);
    }

    private static string? PrimeiroNaOrdemDoArtigo(IEnumerable<string>? marcados, string[] validos)
    {
        if (marcados == null) return null;
        var lista = marcados.ToList();
        return validos.FirstOrDefault(lista.Contains);
    }

    // ── Validação do envio ────────────────────────────────────────────────────

    /// <summary>Risco declarado já normalizado (trim) e validado.</summary>
    public sealed record RiscoAvaliado(string DescricaoRisco, string AcaoMitigacao, string ResponsavelNome,
        string ResponsavelEmail, string Probabilidade, string Consequencia);

    /// <summary>Tudo o que um envio válido grava no processo.</summary>
    public sealed record Avaliacao(
        IReadOnlyList<string> Q15, IReadOnlyList<string> Q16, IReadOnlyList<string> Q17,
        bool Q15Nenhuma, bool Q16Nenhuma, bool Q17Nenhuma,
        string Resultado, string? Enquadramento, int Pontuacao, string ChecklistJson,
        IReadOnlyList<RiscoAvaliado> RiscosDeclarados);

    /// <summary>
    /// Valida o envio por inteiro e calcula resultado, enquadramento, pontuação e o jsonb.
    /// É PURA — não toca entidade nenhuma —, então quem chama só aplica depois de a
    /// validação inteira passar (a mesma disciplina do candidato do módulo).
    /// Domínio fora da lista (inciso, probabilidade, consequência) é CtrDominioInvalido;
    /// o resto (completude, riscos, limite) é CtrClassificacaoRiscoInvalida.
    /// </summary>
    public static Avaliacao Avaliar(CtrClassificacaoRiscoDTO dto)
    {
        var q15 = NormalizarIncisos(dto.Q15, PgiaDominios.ChecklistIncisos.Art15, 15);
        var q16 = NormalizarIncisos(dto.Q16, PgiaDominios.ChecklistIncisos.Art16, 16);
        var q17 = NormalizarIncisos(dto.Q17, PgiaDominios.ChecklistIncisos.Art17, 17);

        // Completude: cada grupo respondido com incisos XOR "nenhuma das alternativas"
        ValidarCompletudeDoGrupo(15, q15, dto.Q15Nenhuma);
        ValidarCompletudeDoGrupo(16, q16, dto.Q16Nenhuma);
        ValidarCompletudeDoGrupo(17, q17, dto.Q17Nenhuma);

        var itens = dto.RiscosDeclarados ?? new List<CtrRiscoDeclaradoDTO>();

        // Contratação sem risco algum não existe (regra do PGIA, transferida intacta)
        if (dto.Q15Nenhuma && dto.Q16Nenhuma && dto.Q17Nenhuma && itens.Count == 0)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida, MensagemTudoNenhuma);

        if (itens.Count > MaximoRiscosDeclarados)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                $"Declare no máximo {MaximoRiscosDeclarados} riscos da contratação.");

        var riscos = itens.Select(ValidarRisco).ToList();

        var (resultado, enquadramento) = CalcularResultado(q15, q16, q17);

        // Os "nenhuma" e os riscos declarados não pontuam
        var pontuacao = PgiaQuesitos.CalcularPontuacao(q15, q16, q17);

        var json = JsonSerializer.Serialize(new ChecklistPersistido
        {
            Q15 = q15,
            Q16 = q16,
            Q17 = q17,
            Q15Nenhuma = dto.Q15Nenhuma,
            Q16Nenhuma = dto.Q16Nenhuma,
            Q17Nenhuma = dto.Q17Nenhuma
        });

        return new Avaliacao(q15, q16, q17, dto.Q15Nenhuma, dto.Q16Nenhuma, dto.Q17Nenhuma,
            resultado, enquadramento, pontuacao, json, riscos);
    }

    /// <summary>Descarta repetições e devolve os incisos na ordem do artigo.</summary>
    private static List<string> NormalizarIncisos(List<string>? marcados, string[] validos, int artigo)
    {
        if (marcados == null || marcados.Count == 0) return new List<string>();

        foreach (var inciso in marcados)
        {
            if (!validos.Contains(inciso))
                throw new ApiException(ErrorCode.CtrDominioInvalido,
                    $"Inciso inválido no grupo do art. {artigo}: {inciso}");
        }

        return validos.Where(marcados.Contains).ToList();
    }

    private static void ValidarCompletudeDoGrupo(int artigo, List<string> incisos, bool nenhuma)
    {
        if (incisos.Count > 0 && nenhuma)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                $"No grupo do art. {artigo}, marque os incisos aplicáveis ou \"Nenhuma das alternativas acima\", não os dois.");

        if (incisos.Count == 0 && !nenhuma)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                $"Responda o grupo do art. {artigo}: marque os incisos aplicáveis ou \"Nenhuma das alternativas acima\".");
    }

    private static RiscoAvaliado ValidarRisco(CtrRiscoDeclaradoDTO? item)
    {
        var descricao = (item?.DescricaoRisco ?? string.Empty).Trim();
        var mitigacao = (item?.AcaoMitigacao ?? string.Empty).Trim();
        var nome = (item?.ResponsavelNome ?? string.Empty).Trim();
        // A caixa do e-mail não é informação: gravado SEMPRE em minúsculas (a comparação
        // do reenvio idêntico também é em minúsculas, então trocar só a caixa não é
        // descartado em silêncio). Normalizado ANTES da validação: o que se grava é
        // exatamente o que passou pela regra do PGIA.
        var email = (item?.ResponsavelEmail ?? string.Empty).Trim().ToLowerInvariant();
        var probabilidade = (item?.Probabilidade ?? string.Empty).Trim();
        var consequencia = (item?.Consequencia ?? string.Empty).Trim();

        if (descricao.Length == 0 || mitigacao.Length == 0)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                "Em cada risco declarado, a descrição do risco e a ação de mitigação são obrigatórias.");

        if (nome.Length == 0)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                "Informe o nome do responsável pela ação de mitigação de cada risco declarado.");

        // Mesma regra de e-mail do PGIA (pré-cadastro e riscos declarados)
        if (!PgiaValidacoes.EmailValido(email))
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                $"E-mail do responsável pelo risco declarado inválido: {email}");

        if (probabilidade.Length == 0 || consequencia.Length == 0)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                "Informe a probabilidade e a consequência de cada risco declarado.");

        if (!Probabilidades.Contains(probabilidade))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Probabilidade inválida para risco declarado: {probabilidade}. Use {string.Join(", ", Probabilidades)}.");

        if (!Consequencias.Contains(consequencia))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Consequência inválida para risco declarado: {consequencia}. Use {string.Join(", ", Consequencias)}.");

        return new RiscoAvaliado(descricao, mitigacao, nome, email, probabilidade, consequencia);
    }

    // ── Idempotência do reenvio ───────────────────────────────────────────────

    /// <summary>
    /// A classificação enviada é IDÊNTICA à gravada? Compara o conteúdo normalizado:
    /// incisos de cada grupo como conjuntos ordenados (ordem do artigo), os três
    /// "nenhuma" e os riscos declarados como MULTICONJUNTO de tuplas normalizadas
    /// (descrição, ação e nome com trim; e-mail com trim e em minúsculas — forma em que
    /// ele é gravado; probabilidade e consequência), sem considerar a ordem da lista. Idêntica é reenvio, não
    /// reclassificação: quem chama não regrava quem/quando nem substitui os riscos (os
    /// Ids mudariam à toa e a auditoria passaria a refletir o último salvamento).
    /// </summary>
    public static bool MesmaClassificacao(CtrProcesso processo, IEnumerable<CtrRiscoDeclarado> riscosGravados,
        Avaliacao enviada)
    {
        if (processo.ChecklistRisco == null) return false;

        var gravado = JsonSerializer.Deserialize<ChecklistPersistido>(processo.ChecklistRisco);
        if (gravado == null) return false;

        if (gravado.Q15Nenhuma != enviada.Q15Nenhuma
            || gravado.Q16Nenhuma != enviada.Q16Nenhuma
            || gravado.Q17Nenhuma != enviada.Q17Nenhuma)
            return false;

        if (!MesmosIncisos(gravado.Q15, enviada.Q15, PgiaDominios.ChecklistIncisos.Art15)
            || !MesmosIncisos(gravado.Q16, enviada.Q16, PgiaDominios.ChecklistIncisos.Art16)
            || !MesmosIncisos(gravado.Q17, enviada.Q17, PgiaDominios.ChecklistIncisos.Art17))
            return false;

        return MesmoMulticonjunto(
            riscosGravados.Select(r => ChaveDoRisco(r.DescricaoRisco, r.AcaoMitigacao, r.ResponsavelNome,
                r.ResponsavelEmail, r.Probabilidade, r.Consequencia)),
            enviada.RiscosDeclarados.Select(r => ChaveDoRisco(r.DescricaoRisco, r.AcaoMitigacao, r.ResponsavelNome,
                r.ResponsavelEmail, r.Probabilidade, r.Consequencia)));
    }

    private static bool MesmosIncisos(IEnumerable<string>? gravados, IEnumerable<string>? enviados, string[] ordemDoArtigo)
    {
        var conjuntoGravado = new HashSet<string>(gravados ?? Enumerable.Empty<string>());
        var conjuntoEnviado = new HashSet<string>(enviados ?? Enumerable.Empty<string>());
        return ordemDoArtigo.Where(conjuntoGravado.Contains)
            .SequenceEqual(ordemDoArtigo.Where(conjuntoEnviado.Contains));
    }

    private static (string, string, string, string, string, string) ChaveDoRisco(string? descricao, string? acao,
        string? nome, string? email, string? probabilidade, string? consequencia) =>
        ((descricao ?? string.Empty).Trim(),
            (acao ?? string.Empty).Trim(),
            (nome ?? string.Empty).Trim(),
            (email ?? string.Empty).Trim().ToLowerInvariant(),
            (probabilidade ?? string.Empty).Trim(),
            (consequencia ?? string.Empty).Trim());

    /// <summary>Mesmos itens com as mesmas multiplicidades, em qualquer ordem.</summary>
    private static bool MesmoMulticonjunto<T>(IEnumerable<T> primeiro, IEnumerable<T> segundo) where T : notnull
    {
        var contagem = new Dictionary<T, int>();
        var totalPrimeiro = 0;
        foreach (var item in primeiro)
        {
            contagem[item] = contagem.GetValueOrDefault(item) + 1;
            totalPrimeiro++;
        }

        var totalSegundo = 0;
        foreach (var item in segundo)
        {
            if (!contagem.TryGetValue(item, out var restantes) || restantes == 0) return false;
            contagem[item] = restantes - 1;
            totalSegundo++;
        }

        return totalPrimeiro == totalSegundo;
    }

    // ── Leitura ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Classificação gravada, para as leituras de UM processo; null quando o processo
    /// não é classificado (checklist nulo).
    /// </summary>
    public static CtrClassificacaoRiscoResponse? MapearClassificacao(CtrProcesso processo,
        IEnumerable<CtrRiscoDeclarado> riscos)
    {
        if (processo.ChecklistRisco == null) return null;

        var checklist = JsonSerializer.Deserialize<ChecklistPersistido>(processo.ChecklistRisco)
                        ?? new ChecklistPersistido();

        return new CtrClassificacaoRiscoResponse
        {
            Q15 = checklist.Q15 ?? new List<string>(),
            Q16 = checklist.Q16 ?? new List<string>(),
            Q17 = checklist.Q17 ?? new List<string>(),
            Q15Nenhuma = checklist.Q15Nenhuma,
            Q16Nenhuma = checklist.Q16Nenhuma,
            Q17Nenhuma = checklist.Q17Nenhuma,
            RiscoClassificado = processo.RiscoClassificado ?? string.Empty,
            EnquadramentoRisco = processo.EnquadramentoRisco,
            PontuacaoRisco = processo.PontuacaoRisco ?? 0,
            ClassificadoEm = processo.RiscoClassificadoEm ?? default,
            ClassificadoPor = processo.RiscoClassificadoPor,
            RiscosDeclarados = riscos
                .OrderBy(r => r.Id)
                .Select(r => new CtrRiscoDeclaradoResponse
                {
                    Id = r.Id,
                    DescricaoRisco = r.DescricaoRisco,
                    AcaoMitigacao = r.AcaoMitigacao,
                    ResponsavelNome = r.ResponsavelNome,
                    ResponsavelEmail = r.ResponsavelEmail,
                    Probabilidade = r.Probabilidade,
                    Consequencia = r.Consequencia,
                    Nivel = CalcularNivel(r.Probabilidade, r.Consequencia) ?? string.Empty
                })
                .ToList()
        };
    }

    /// <summary>
    /// Mesma forma e mapeamento do jsonb de pgia_classificacao_risco.respostas_checklist
    /// (o ChecklistPersistido do PGIA é privado, por isso a réplica).
    /// </summary>
    private sealed class ChecklistPersistido
    {
        [JsonPropertyName("q15")] public List<string> Q15 { get; set; } = new();
        [JsonPropertyName("q16")] public List<string> Q16 { get; set; } = new();
        [JsonPropertyName("q17")] public List<string> Q17 { get; set; } = new();
        [JsonPropertyName("q15_nenhuma")] public bool Q15Nenhuma { get; set; }
        [JsonPropertyName("q16_nenhuma")] public bool Q16Nenhuma { get; set; }
        [JsonPropertyName("q17_nenhuma")] public bool Q17Nenhuma { get; set; }
    }

    // ── Predicados dos filtros (rodam no banco) ───────────────────────────────

    /// <summary>
    /// Filtro pelo risco classificado: um dos quatro resultados ou "Não classificado".
    /// Null quando o valor não é do domínio (quem chama devolve lista vazia).
    /// </summary>
    public static Expression<Func<CtrProcesso, bool>>? PredicadoRiscoClassificado(string? valor)
    {
        if (valor == CtrDominios.RiscoClassificado.NaoClassificado) return p => p.RiscoClassificado == null;
        if (valor == null || !CtrDominios.RiscoClassificado.Todos.Contains(valor)) return null;
        return p => p.RiscoClassificado == valor;
    }

    /// <summary>
    /// Filtro pelo nível MÁXIMO declarado: o nível vira a lista de pares probabilidade ×
    /// consequência daquela cor — tem ao menos um risco nesses pares e nenhum nos pares
    /// dos níveis acima. "Sem riscos declarados" = nenhum risco. Null fora do domínio.
    /// </summary>
    public static Expression<Func<CtrProcesso, bool>>? PredicadoNivelRiscoDeclarado(string? nivel)
    {
        if (nivel == CtrDominios.NivelRisco.SemRiscosDeclarados) return p => !p.RiscosDeclarados.Any();

        var ordem = OrdemDoNivel(nivel);
        if (ordem < 0) return null;

        var processo = Expression.Parameter(typeof(CtrProcesso), "p");
        var colecao = Expression.Property(processo, nameof(CtrProcesso.RiscosDeclarados));

        Expression corpo = Expression.Call(AnyComPredicado, colecao, PredicadoRiscoNosPares(ParesDoNivel(nivel)));

        var acima = CtrDominios.NivelRisco.Todos.Skip(ordem + 1).SelectMany(ParesDoNivel).ToList();
        if (acima.Count > 0)
            corpo = Expression.AndAlso(corpo,
                Expression.Not(Expression.Call(AnyComPredicado, colecao, PredicadoRiscoNosPares(acima))));

        return Expression.Lambda<Func<CtrProcesso, bool>>(corpo, processo);
    }

    private static readonly MethodInfo AnyComPredicado = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(CtrRiscoDeclarado));

    /// <summary>r => (r.Probabilidade == p1 AND r.Consequencia == c1) OR ... (constantes inline).</summary>
    private static Expression<Func<CtrRiscoDeclarado, bool>> PredicadoRiscoNosPares(
        IEnumerable<(string Probabilidade, string Consequencia)> pares)
    {
        var risco = Expression.Parameter(typeof(CtrRiscoDeclarado), "r");
        var probabilidade = Expression.Property(risco, nameof(CtrRiscoDeclarado.Probabilidade));
        var consequencia = Expression.Property(risco, nameof(CtrRiscoDeclarado.Consequencia));

        Expression? corpo = null;
        foreach (var par in pares)
        {
            var celula = Expression.AndAlso(
                Expression.Equal(probabilidade, Expression.Constant(par.Probabilidade)),
                Expression.Equal(consequencia, Expression.Constant(par.Consequencia)));
            corpo = corpo == null ? celula : Expression.OrElse(corpo, celula);
        }

        return Expression.Lambda<Func<CtrRiscoDeclarado, bool>>(corpo ?? Expression.Constant(false), risco);
    }
}
