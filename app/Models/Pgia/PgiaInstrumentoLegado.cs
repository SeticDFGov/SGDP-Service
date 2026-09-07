using app.Models;

namespace Models.Pgia;

/// <summary>
/// Triagem e revisão de contratos e atos anteriores ao decreto; prazo 30/12/2026
/// (art. 37). Tabela pgia_instrumento_legado.
/// </summary>
public class PgiaInstrumentoLegado
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // D30
    public string TipoInstrumento { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string? Numero { get; set; }

    // D29. Incerto é tratado como sim até confirmação do fornecedor
    public string EnvolveIa { get; set; } = PgiaDominios.EnvolveIa.Incerto;

    public DateOnly? DataTriagem { get; set; }

    public Guid? TriadoPor { get; set; }

    public User? TriadoPorUser { get; set; }

    public bool Revisado { get; set; }

    public DateOnly? DataRevisao { get; set; }

    // Condicional: se envolve IA, termo aditivo com a cláusula de vedação (arts. 21 e 37)
    public bool? AditivoClausulaTreinamento { get; set; }

    public DateOnly? DataAditivo { get; set; }

    public string? ProcessoSei { get; set; }

    public long? ComprovacaoDocId { get; set; }

    // Condicional: se envolve IA, registro criado no inventário (arts. 2º, XI e 37)
    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
