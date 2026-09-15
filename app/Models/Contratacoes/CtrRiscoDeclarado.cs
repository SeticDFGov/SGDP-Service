namespace Models.Contratacoes;

/// <summary>
/// Risco declarado da contratação, avaliado pela matriz COMPLETA da CGDF
/// (5 probabilidades × 5 consequências). Parte da classificação de riscos do
/// processo: some com ela (ON DELETE CASCADE e remoção explícita ao limpar ou
/// substituir). Tabela ctr_risco_declarado.
///
/// O nível (Baixo/Médio/Alto/Extremo) é DERIVADO da célula da matriz
/// (CtrClassificacaoRisco.CalcularNivel) e nunca gravado.
/// </summary>
public class CtrRiscoDeclarado
{
    public long Id { get; set; }

    public long ProcessoId { get; set; }

    public CtrProcesso? Processo { get; set; }

    public string DescricaoRisco { get; set; } = string.Empty;

    public string AcaoMitigacao { get; set; } = string.Empty;

    public string ResponsavelNome { get; set; } = string.Empty;

    // Mesma validação de e-mail do PGIA (PgiaValidacoes.EmailValido); gravado sempre em minúsculas
    public string ResponsavelEmail { get; set; } = string.Empty;

    // PgiaDominios.EscalaCgdf.Probabilidade.Todos (escala completa)
    public string Probabilidade { get; set; } = string.Empty;

    // PgiaDominios.EscalaCgdf.Consequencia.Todos (escala completa)
    public string Consequencia { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;
}

/// <summary>
/// Projeção enxuta de um risco declarado — só o que o nível precisa, sem os textos
/// longos. Usada para resolver o nível máximo EM LOTE na lista e no painel.
/// </summary>
public sealed record CtrEscalaRiscoDeclarado(long ProcessoId, string Probabilidade, string Consequencia);
