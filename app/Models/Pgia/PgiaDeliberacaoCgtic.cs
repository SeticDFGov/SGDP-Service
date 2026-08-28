namespace Models.Pgia;

/// <summary>
/// Deliberações e resoluções do CGTIC (arts. 7º, 16, §§ 1º e 2º, 21, 25, I, 26 e 30, § único).
/// Tabela pgia_deliberacao_cgtic. O vínculo com contrato chega na fase 4.
/// </summary>
public class PgiaDeliberacaoCgtic
{
    public long Id { get; set; }

    // D13
    public string Tipo { get; set; } = string.Empty;

    // Condicional: deliberações sobre um sistema específico do inventário
    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // Condicional: deliberações cujo objeto é um contrato de IA (arts. 7º, IV e 25, I)
    public long? ContratoId { get; set; }

    public DateOnly DataDeliberacao { get; set; }

    // D14. Contratação de Alto Risco é vedada sem aprovação prévia (art. 7º, IV)
    public string Resultado { get; set; } = string.Empty;

    public string? NumeroAto { get; set; }

    public string? Ementa { get; set; }

    // Metadados da ata ou ato formal (pgia_documento; anexo binário em fase futura)
    public long? DocumentoId { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
