using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Metadados de documento (sem upload binário; o canal legal é o SEI — seção 4.2 da análise).
/// </summary>
public class PgiaDocumentoCreateDTO
{
    // D28 (PgiaDominios.TipoDocumento)
    [StringLength(40)]
    public string Tipo { get; set; } = string.Empty;

    [StringLength(25)]
    public string? ProcessoSei { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public string? UrlStorage { get; set; }
}

public class PgiaDocumentoResponse
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public long? SistemaIaId { get; set; }

    public string? ProcessoSei { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public string? UrlStorage { get; set; }

    public DateTime DataEnvio { get; set; }

    public string EnviadoPorNome { get; set; } = string.Empty;
}
