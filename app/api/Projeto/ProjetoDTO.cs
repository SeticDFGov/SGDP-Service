namespace api.Projeto;

public class ProjetoDTO
{
    public string NM_PROJETO {get;set;}
    public string? GERENTE_PROJETO {get;set;}
    public string? SITUACAO {get;set;}
    public string? NR_PROCESSO_SEI {get;set;}
    public string? NM_AREA_DEMANDANTE {get;set;}
    public string? ANO {get;set;}
    public string? TEMPLATE {get;set;}
    public bool? PROFISCOII {get;set;}
    public bool? PDTIC2427 {get;set;}
    public bool? PTD2427 {get;set;}
    public decimal? valorEstimado {get;set;}
    public Guid UnidadeId {get;set;}
    public Guid EsteiraId {get;set;}
}
