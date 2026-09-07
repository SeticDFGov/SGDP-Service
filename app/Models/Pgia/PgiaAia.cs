using app.Models;

namespace Models.Pgia;

/// <summary>
/// Avaliação de Impacto Algorítmico dos sistemas de Alto Risco
/// (arts. 2º, VII, 16, § 1º, 22, 25, I e 35, IV). Tabela pgia_aia.
/// </summary>
public class PgiaAia
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // D12
    public string Status { get; set; } = PgiaDominios.StatusAia.NaoIniciada;

    public DateOnly DataInicio { get; set; }

    // Sistemas em operação em 03/07/2026: concluir até 03/07/2027 (art. 35, IV)
    public DateOnly? DataConclusao { get; set; }

    public string ImpactosDireitosFundamentais { get; set; } = string.Empty;

    public string MedidasPreventivas { get; set; } = string.Empty;

    public string MedidasMitigadoras { get; set; } = string.Empty;

    public string MedidasReversao { get; set; } = string.Empty;

    // Condicional: nova aquisição exige AIA antes da licitação (art. 25, I, b)
    public bool? PreviaLicitacao { get; set; }

    // Elaborada com o RIPD da LGPD deve cobrir ambos os requisitos (art. 22)
    public bool ConjuntaRipd { get; set; }

    public long? RipdDocumentoId { get; set; }

    // Pessoa que elaborou (Users do SGDP)
    public Guid ElaboradaPor { get; set; }

    public User? ElaboradaPorUser { get; set; }

    // Arquivo completo; esperado quando concluída
    public long? DocumentoId { get; set; }

    // Implantação somente após deliberação favorável (art. 16, § 1º)
    public long? DeliberacaoCgticId { get; set; }

    public PgiaDeliberacaoCgtic? Deliberacao { get; set; }

    // Portal da Transparência (arts. 16, § 1º e 25, I, d)
    public bool PublicadaPortal { get; set; }

    public DateOnly? DataPublicacaoPortal { get; set; }

    // Respeitados segredos comerciais e industriais (art. 24, IV)
    public string? UrlPublicacao { get; set; }

    // A avaliação é prévia e contínua; obrigatória ao concluir (art. 2º, VII)
    public DateOnly? ProximaRevisao { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
