using app.Models;

namespace Models.Pgia;

/// <summary>
/// Trilhas ProCapIA/DF por agente (arts. 10, III, 28, 29, 32, IV e 35, III).
/// Tabela pgia_capacitacao; uma linha por par agente + trilha.
/// </summary>
public class PgiaCapacitacao
{
    public long Id { get; set; }

    public Guid AgenteId { get; set; }

    public User? Agente { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // D19 (art. 29, I a IV)
    public string Trilha { get; set; } = string.Empty;

    // D20
    public string Status { get; set; } = "Prevista";

    public DateOnly? DataConclusao { get; set; }

    // Inclusão nos planos até 30/12/2026 (arts. 29, § único e 35, III)
    public bool PrevistaPlanoCapacitacao { get; set; }

    public long? CertificadoDocId { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
