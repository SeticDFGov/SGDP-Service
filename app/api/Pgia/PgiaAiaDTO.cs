using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Avaliação de Impacto Algorítmico (arts. 2º, VII, 16, § 1º, 22 e 35, IV).
/// Elaborada pelo órgão; publicação e vínculo com deliberação são atos centrais.
/// </summary>
public class PgiaAiaCreateDTO
{
    // D12 (PgiaDominios.StatusAia)
    [StringLength(30)]
    public string Status { get; set; } = "Não iniciada";

    public DateOnly DataInicio { get; set; }

    // Obrigatória quando Status = Concluída
    public DateOnly? DataConclusao { get; set; }

    public string ImpactosDireitosFundamentais { get; set; } = string.Empty;

    public string MedidasPreventivas { get; set; } = string.Empty;

    public string MedidasMitigadoras { get; set; } = string.Empty;

    public string MedidasReversao { get; set; } = string.Empty;

    // Condicional: nova aquisição exige AIA antes da licitação (art. 25, I, b)
    public bool? PreviaLicitacao { get; set; }

    // Elaborada com o RIPD da LGPD deve cobrir ambos os requisitos (art. 22)
    public bool ConjuntaRipd { get; set; }

    // Metadados de documento já cadastrados (pgia_documento)
    public long? RipdDocumentoId { get; set; }

    public long? DocumentoId { get; set; }

    // A avaliação é prévia e contínua; obrigatória ao concluir (art. 2º, VII)
    public DateOnly? ProximaRevisao { get; set; }
}

public class PgiaAiaUpdateDTO : PgiaAiaCreateDTO
{
}

/// <summary>
/// Publicação do resultado da AIA no Portal da Transparência (arts. 16, § 1º e 24, IV) — ato da SGDI.
/// </summary>
public class PgiaAiaPublicacaoDTO
{
    public bool PublicadaPortal { get; set; }

    public DateOnly? DataPublicacaoPortal { get; set; }

    public string? UrlPublicacao { get; set; }
}

public class PgiaAiaResponse
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateOnly DataInicio { get; set; }

    public DateOnly? DataConclusao { get; set; }

    public string ImpactosDireitosFundamentais { get; set; } = string.Empty;

    public string MedidasPreventivas { get; set; } = string.Empty;

    public string MedidasMitigadoras { get; set; } = string.Empty;

    public string MedidasReversao { get; set; } = string.Empty;

    public bool? PreviaLicitacao { get; set; }

    public bool ConjuntaRipd { get; set; }

    public long? RipdDocumentoId { get; set; }

    public long? DocumentoId { get; set; }

    public string ElaboradaPorNome { get; set; } = string.Empty;

    public long? DeliberacaoCgticId { get; set; }

    public string? DeliberacaoResultado { get; set; }

    public bool PublicadaPortal { get; set; }

    public DateOnly? DataPublicacaoPortal { get; set; }

    public string? UrlPublicacao { get; set; }

    public DateOnly? ProximaRevisao { get; set; }

    public DateTime CriadoEm { get; set; }
}
