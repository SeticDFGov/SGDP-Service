using app.Models;

namespace Models.Pgia;

/// <summary>
/// Auditorias técnicas de sistemas de Alto Risco (arts. 12, I, 25, § 2º, 33, II e 34).
/// Tabela pgia_auditoria_tecnica.
/// </summary>
public class PgiaAuditoriaTecnica
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // D26: periódica na forma do CGTIC (art. 34) ou independente contratual (art. 25, § 2º)
    public string Tipo { get; set; } = string.Empty;

    public string EntidadeAuditora { get; set; } = string.Empty;

    /// <summary>
    /// Adaptação registrada: designa a auditoria a um usuário de papel pgia_auditoria.
    /// É assim que o escopo "a auditora externa só vê as auditorias designadas a ela"
    /// se resolve, já que o schema só guarda o nome da entidade auditora.
    /// </summary>
    public Guid? AuditorUserId { get; set; }

    public User? AuditorUser { get; set; }

    // Entidade externa ao fornecedor (art. 25, § 2º)
    public bool ExternaFornecedor { get; set; }

    // Suporte técnico-científico da FAPDF (art. 12, I)
    public bool? ApoioFapdf { get; set; }

    public DateOnly DataInicio { get; set; }

    public DateOnly? DataFim { get; set; }

    public string? Parecer { get; set; }

    // Resultado no Portal da Transparência (art. 34)
    public bool PublicadoPortal { get; set; }

    public DateOnly? DataPublicacao { get; set; }

    public string? Url { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
