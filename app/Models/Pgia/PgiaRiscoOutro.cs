namespace Models.Pgia;

/// <summary>
/// Risco declarado pelo órgão no grupo "Outros" do questionário de classificação,
/// avaliado pela matriz de riscos da CGDF (probabilidade × consequência).
/// Registro COMPLEMENTAR: não entra no cálculo do risco do sistema (arts. 15 a 18)
/// nem na pontuação — serve para o órgão declarar o que enxerga e como mitiga.
/// Tabela pgia_risco_outro, filha de pgia_classificacao_risco (cascade).
/// </summary>
public class PgiaRiscoOutro
{
    public long Id { get; set; }

    public long ClassificacaoRiscoId { get; set; }

    public PgiaClassificacaoRisco? Classificacao { get; set; }

    public string DescricaoRisco { get; set; } = string.Empty;

    public string AcaoMitigacao { get; set; } = string.Empty;

    // Quem responde pela mitigação no órgão
    public string ResponsavelNome { get; set; } = string.Empty;

    public string ResponsavelEmail { get; set; } = string.Empty;

    // Escala da CGDF, restrita ao subconjunto permitido neste grupo
    // (PgiaDominios.EscalaCgdf.Probabilidade.Permitidos)
    public string Probabilidade { get; set; } = string.Empty;

    public string Consequencia { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }
}
