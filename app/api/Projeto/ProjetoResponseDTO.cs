namespace api.Projeto;

/// <summary>
/// DTO de resposta para Projeto (evita exposição de estrutura interna)
/// </summary>
public class ProjetoResponseDTO
{
    public int ProjetoId { get; set; }
    public string NM_PROJETO { get; set; } = string.Empty;
    public string? GERENTE_PROJETO { get; set; }
    public string? SITUACAO { get; set; }
    public string? NR_PROCESSO_SEI { get; set; }
    public string? ANO { get; set; }
    public string? TEMPLATE { get; set; }
    public bool? PROFISCOII { get; set; }
    public bool? PDTIC2427 { get; set; }
    public bool? PTD2427 { get; set; }
    public decimal? ValorEstimado { get; set; }
    public DateTime DT_INICIO { get; set; }
    public DateTime DT_TERMINO { get; set; }

    // Relacionamentos simplificados (evita circular references)
    public string? UnidadeNome { get; set; }
    public Guid? UnidadeId { get; set; }
    public string? EsteiraNome { get; set; }
    public Guid? EsteiraId { get; set; }
    public string? AreaDemandanteNome { get; set; }
    public string? AreaDemandanteSigla { get; set; }
    public int? AreaDemandanteId { get; set; }

    // Estatísticas (opcional)
    public int? TotalEtapas { get; set; }
    public int? EtapasConcluidas { get; set; }
    public decimal? PercentualConclusao { get; set; }
}
