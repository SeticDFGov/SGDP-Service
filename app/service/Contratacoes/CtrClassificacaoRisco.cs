using System.Linq.Expressions;
using System.Reflection;
using api.Contratacoes;
using Models.Contratacoes;
using Models.Pgia;
using service.Pgia;

namespace service.Contratacoes;

/// <summary>
/// Riscos da contratação: FONTE ÚNICA das regras da classificação de riscos do processo.
/// Validação do envio, nível de cada risco pela matriz 5 × 5 da CGDF, nível máximo,
/// reenvio idêntico e os predicados EF dos filtros.
///
/// Desde 2026-09-21 a classificação é SÓ a lista de riscos da contratação. O questionário
/// dos arts. 15 a 17 do Decreto nº 48.901/2026 (os riscos padrão do PGIA, feitos para
/// sistemas de IA) saiu do módulo; as colunas dele em ctr_processo ficaram no banco, sem
/// uso, e são zeradas quando os riscos do processo são alterados (ver CtrProcesso).
///
/// Reusa SÓ fontes estáticas de domínio do PGIA (EscalaCgdf e PgiaValidacoes.EmailValido),
/// sem alterar nada dele. Os riscos aceitam a escala COMPLETA da CGDF (no PGIA o grupo
/// "Outros" fica no quadrante baixo).
/// </summary>
public static class CtrClassificacaoRisco
{
    public const int MaximoRiscosDeclarados = 20;

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
    /// Nível do risco declarado pela célula da matriz. FONTE ÚNICA, nunca gravado.
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

    // ── Validação do envio ────────────────────────────────────────────────────

    /// <summary>Risco declarado já normalizado (trim) e validado.</summary>
    public sealed record RiscoAvaliado(string DescricaoRisco, string AcaoMitigacao, string ResponsavelNome,
        string ResponsavelEmail, string Probabilidade, string Consequencia);

    /// <summary>
    /// Valida a lista enviada por inteiro e devolve os riscos normalizados. É PURA (não
    /// toca entidade nenhuma), então quem chama só aplica depois de a validação inteira
    /// passar (a mesma disciplina do candidato do módulo). Lista vazia (ou nula) é válida:
    /// o processo fica sem riscos da contratação, que é como a tela tira o último risco.
    /// Probabilidade ou consequência fora da escala é CtrDominioInvalido; o resto (risco
    /// incompleto, e-mail, limite) é CtrClassificacaoRiscoInvalida.
    /// </summary>
    public static IReadOnlyList<RiscoAvaliado> Avaliar(CtrClassificacaoRiscoDTO dto)
    {
        var itens = dto.RiscosDeclarados ?? new List<CtrRiscoDeclaradoDTO>();

        if (itens.Count > MaximoRiscosDeclarados)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                $"Declare no máximo {MaximoRiscosDeclarados} riscos da contratação.");

        return itens.Select(ValidarRisco).ToList();
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

    // ── Reenvio idêntico ──────────────────────────────────────────────────────

    /// <summary>
    /// A lista enviada é IDÊNTICA à gravada? Compara os riscos como MULTICONJUNTO de tuplas
    /// normalizadas (descrição, ação e nome com trim; e-mail com trim e em minúsculas, forma
    /// em que ele é gravado; probabilidade e consequência), sem considerar a ordem da lista.
    /// Idêntica é reenvio, não alteração: quem chama não substitui os riscos (os Ids mudariam
    /// à toa e o registro de quem/quando passaria a refletir o último salvamento).
    /// </summary>
    public static bool MesmosRiscos(IEnumerable<CtrRiscoDeclarado> gravados, IEnumerable<RiscoAvaliado> enviados) =>
        MesmoMulticonjunto(
            gravados.Select(r => ChaveDoRisco(r.DescricaoRisco, r.AcaoMitigacao, r.ResponsavelNome,
                r.ResponsavelEmail, r.Probabilidade, r.Consequencia)),
            enviados.Select(r => ChaveDoRisco(r.DescricaoRisco, r.AcaoMitigacao, r.ResponsavelNome,
                r.ResponsavelEmail, r.Probabilidade, r.Consequencia)));

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
    /// Riscos gravados, para as leituras de UM processo; null quando o processo não tem
    /// nenhum. Quem registrou e quando saem do risco mais recente: a lista é substituída
    /// inteira a cada alteração, então os riscos de um salvamento têm o mesmo registro.
    /// </summary>
    public static CtrClassificacaoRiscoResponse? MapearClassificacao(IEnumerable<CtrRiscoDeclarado> riscos)
    {
        var ordenados = riscos.OrderBy(r => r.Id).ToList();
        if (ordenados.Count == 0) return null;

        var maisRecente = ordenados.OrderByDescending(r => r.CriadoEm).ThenByDescending(r => r.Id).First();

        return new CtrClassificacaoRiscoResponse
        {
            ClassificadoEm = maisRecente.CriadoEm,
            ClassificadoPor = maisRecente.CriadoPor,
            RiscosDeclarados = ordenados
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

    // ── Predicados dos filtros (rodam no banco) ───────────────────────────────

    /// <summary>
    /// Filtro pelo nível MÁXIMO declarado: o nível vira a lista de pares probabilidade ×
    /// consequência daquela cor. Tem ao menos um risco nesses pares e nenhum nos pares
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
