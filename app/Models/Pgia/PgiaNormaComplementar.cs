namespace Models.Pgia;

/// <summary>
/// Resoluções, instruções, guias e recomendações técnicas
/// (arts. 7º, VIII, 8º, III e VIII, 26 e 36). Tabela pgia_norma_complementar.
/// </summary>
public class PgiaNormaComplementar
{
    public long Id { get; set; }

    // D24
    public string Tipo { get; set; } = string.Empty;

    public string? Numero { get; set; }

    public string Ementa { get; set; } = string.Empty;

    public string Emissor { get; set; } = string.Empty;

    // Guia de Contratações: até 30/12/2026 (art. 26, § 3º)
    public DateOnly DataPublicacao { get; set; }

    public string? Url { get; set; }

    public bool Vigente { get; set; } = true;

    // Guia e atualizações de requisitos obrigatórios exigem aprovação prévia do CGTIC (art. 26, §§ 1º e 2º)
    public DateOnly? AprovadaCgticEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
