namespace Models.Pgia;

/// <summary>
/// Controle das obrigações com prazo do decreto (arts. 26, § 3º, 35 e 37).
/// OrgaoId nulo indica obrigação da SGDI ou modelo a instanciar por órgão na adesão.
/// </summary>
public class PgiaPrazoConformidade
{
    public long Id { get; set; }

    public string Obrigacao { get; set; } = string.Empty;

    public string BaseLegal { get; set; } = string.Empty;

    public long? OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public DateOnly DataLimite { get; set; }

    public DateOnly? CumpridoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
