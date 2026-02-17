namespace api.Etapa;

/// <summary>
/// DTO de resposta para Etapa
/// </summary>
public class EtapaResponseDTO
{
    public int EtapaProjetoId { get; set; }
    public string NM_ETAPA { get; set; } = string.Empty;
    public string? RESPONSAVEL_ETAPA { get; set; }
    public string? ANALISE { get; set; }
    public string? SITUACAO { get; set; }

    // Datas
    public DateTime? DT_INICIO_PREVISTO { get; set; }
    public DateTime? DT_TERMINO_PREVISTO { get; set; }
    public DateTime? DT_INICIO_REAL { get; set; }
    public DateTime? DT_TERMINO_REAL { get; set; }

    // Percentuais
    public decimal PERCENT_TOTAL_ETAPA { get; set; }
    public decimal PERCENT_EXEC_ETAPA { get; set; }
    public decimal PERCENT_EXEC_REAL { get; set; }
    public decimal PERCENT_PLANEJADO { get; set; }

    // Outros
    public int DIAS_PREVISTOS { get; set; }
    public int Order { get; set; }

    // Projeto simplificado (evita circular reference)
    public int ProjetoId { get; set; }
    public string? ProjetoNome { get; set; }
}
