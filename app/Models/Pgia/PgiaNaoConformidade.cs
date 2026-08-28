namespace Models.Pgia;

/// <summary>
/// Situações de descumprimento identificadas pelo SGTIC do órgão ou pela supervisão
/// da SGDI (arts. 8º, X e 9º, II). Tabela pgia_nao_conformidade.
/// </summary>
public class PgiaNaoConformidade
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // D36
    public string Origem { get; set; } = string.Empty;

    public DateOnly DataRegistro { get; set; }

    public DateOnly? ReportadaSgdiEm { get; set; }

    public string Situacao { get; set; } = PgiaDominios.SituacaoNaoConformidade.Registrada;

    // Irregularidades comunicadas ao controle interno (art. 8º, X)
    public DateOnly? ComunicadaControleInternoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
