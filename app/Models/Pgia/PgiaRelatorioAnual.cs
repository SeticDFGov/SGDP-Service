namespace Models.Pgia;

/// <summary>
/// Relatório Anual de Governança de IA do DF, publicado pela SGDI até 31 de março
/// (arts. 7º, VII, 8º, VII e 33). Tabela pgia_relatorio_anual.
/// </summary>
public class PgiaRelatorioAnual
{
    public long Id { get; set; }

    public short Ano { get; set; }

    public DateOnly? DataPublicacao { get; set; }

    public string? UrlPublicacao { get; set; }

    public long? DocumentoId { get; set; }

    public DateOnly? ApreciadoCgticEm { get; set; }

    public string? Recomendacoes { get; set; }

    public string? AgendaInovacao { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
