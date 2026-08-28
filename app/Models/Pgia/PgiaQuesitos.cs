namespace Models.Pgia;

/// <summary>
/// Matriz de quesitos do checklist de risco (arts. 15 a 17) com o peso de cada inciso.
/// A soma dos pesos dos incisos marcados vira a pontuação da classificação — métrica
/// de acompanhamento da SGDI, que não substitui o resultado dos arts. 15 a 18.
/// </summary>
public static class PgiaQuesitos
{
    public sealed record Quesito(int Artigo, string Inciso, int Peso);

    public static readonly IReadOnlyList<Quesito> Todos = new List<Quesito>
    {
        // Art. 15 — uso vedado: todos os incisos com o peso máximo
        new(15, "I", 5),
        new(15, "II", 5),
        new(15, "III", 5),
        new(15, "IV", 5),
        new(15, "V", 5),
        new(15, "VI", 5),

        // Art. 16 — Alto Risco
        new(16, "I", 4),
        new(16, "II", 3),
        new(16, "III", 3),
        new(16, "IV", 4),
        new(16, "V", 4),
        new(16, "VI", 3),
        new(16, "VII", 3),
        new(16, "VIII", 4),
        new(16, "IX", 4),

        // Art. 17 — Risco Moderado
        new(17, "I", 2),
        new(17, "II", 1),
        new(17, "III", 1),
        new(17, "IV", 2)
    };

    /// <summary>
    /// Soma os pesos dos incisos marcados nos três artigos. Os incisos já vêm
    /// validados pelo checklist; qualquer valor fora da matriz é ignorado.
    /// </summary>
    public static int CalcularPontuacao(
        IEnumerable<string>? q15, IEnumerable<string>? q16, IEnumerable<string>? q17)
    {
        return Somar(15, q15) + Somar(16, q16) + Somar(17, q17);
    }

    private static int Somar(int artigo, IEnumerable<string>? incisos)
    {
        if (incisos == null) return 0;

        var total = 0;
        foreach (var inciso in incisos.Distinct())
        {
            var quesito = Todos.FirstOrDefault(q => q.Artigo == artigo && q.Inciso == inciso);
            if (quesito != null) total += quesito.Peso;
        }
        return total;
    }
}
