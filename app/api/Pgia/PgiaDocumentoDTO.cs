using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Metadados de documento. O arquivo em si sobe depois, por
/// <c>POST api/pgia/documento/{id}/arquivo</c>; o nº do processo SEI continua
/// sendo o canal oficial de tramitação.
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

/// <summary>
/// Criação de documento fora da rota do sistema (ata do CGTIC, relatório da SGDI,
/// certificado de capacitação, comprovações de contratação...). Escolhe o escopo:
/// <see cref="SistemaIaId"/> (herda o órgão do sistema), <see cref="OrgaoId"/>
/// (documento do órgão) ou nenhum dos dois (documento central, só SGDI/CGTIC/admin).
/// </summary>
public class PgiaDocumentoAvulsoCreateDTO : PgiaDocumentoCreateDTO
{
    public long? OrgaoId { get; set; }

    public long? SistemaIaId { get; set; }
}

public class PgiaDocumentoResponse
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    // Nulo em documento de escopo central (ata do CGTIC, relatório anual)
    public long? OrgaoId { get; set; }

    public long? SistemaIaId { get; set; }

    public string? ProcessoSei { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public string? UrlStorage { get; set; }

    public string? ContentType { get; set; }

    public long? TamanhoBytes { get; set; }

    // Atalho para o front decidir se mostra o botão de download
    public bool TemArquivo { get; set; }

    public DateTime DataEnvio { get; set; }

    public string EnviadoPorNome { get; set; } = string.Empty;
}

/// <summary>Resposta do upload do anexo.</summary>
public class PgiaArquivoResponse
{
    public long DocumentoId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }
}
