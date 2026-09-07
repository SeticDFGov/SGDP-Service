namespace Models.Pgia;

/// <summary>
/// Indicadores por sistema (arts. 24, V, 25, § 2º, 31 e 32, III); obrigatórios
/// para Alto Risco no relatório semestral. Tabela pgia_indicador_desempenho.
/// Adaptação registrada: o period_referencia daterange do schema vira duas colunas
/// date (periodo_inicio/periodo_fim), equivalentes e portáveis no EF.
/// </summary>
public class PgiaIndicadorDesempenho
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public string Nome { get; set; } = string.Empty;

    // D23
    public string Categoria { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public string? Unidade { get; set; }

    public DateOnly PeriodoInicio { get; set; }

    public DateOnly PeriodoFim { get; set; }

    // Condicional: quando houver ANS (art. 25, § 2º)
    public decimal? Meta { get; set; }

    public bool PublicadoRegistroPublico { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
