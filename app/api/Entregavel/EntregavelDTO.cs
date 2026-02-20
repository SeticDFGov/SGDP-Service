namespace api.Entregavel;

public class EntregavelCreateDTO
{
    public int DemandaId { get; set; }
    public string NM_ETAPA { get; set; } = string.Empty;
    public int AreaExecutoraId { get; set; }
    public string TIPO_ENTREGA { get; set; } = string.Empty;
    public DateTime DT_INICIO { get; set; }
    public DateTime DT_FIM { get; set; }
    public string? Descricao { get; set; }
}

public class EntregavelUpdateDTO
{
    public string NM_ETAPA { get; set; } = string.Empty;
    public int AreaExecutoraId { get; set; }
    public string TIPO_ENTREGA { get; set; } = string.Empty;
    public DateTime DT_INICIO { get; set; }
    public DateTime DT_FIM { get; set; }
    public string? Descricao { get; set; }
}

public class EntregavelUpdatePercentDTO
{
    public int PERCENT_EXECUTADO { get; set; }
    public string? Descricao { get; set; }
}
