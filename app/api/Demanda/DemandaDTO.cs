namespace api.Demanda;

public class DemandaCreateDTO
{
    public string NM_PROJETO { get; set; } = string.Empty;
    public string? NR_PROCESSO_SEI { get; set; }
    public int NM_AREA_DEMANDANTE { get; set; }
    public Guid EsteiraId { get; set; }
}
