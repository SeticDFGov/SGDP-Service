namespace Models.Pgia;

/// <summary>
/// Homologação de plataformas públicas de IA generativa pela SGDI, por ato próprio
/// (arts. 2º, III, 8º, IV, 18 e 20). Tabela pgia_plataforma_ia_generativa.
/// </summary>
public class PgiaPlataformaIaGenerativa
{
    public long Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Fornecedor { get; set; }

    public string? Url { get; set; }

    // D17
    public string StatusHomologacao { get; set; } = "Em avaliação";

    // A relação publicada indica as aptas a informações sigilosas ou dados pessoais (art. 8º, IV)
    public bool AptaDadosPessoaisSigilosos { get; set; }

    public string? AtoHomologacao { get; set; }

    public DateOnly? DataAto { get; set; }

    public DateOnly? PublicadaRelacaoEm { get; set; }

    public string? DiretrizesUso { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
