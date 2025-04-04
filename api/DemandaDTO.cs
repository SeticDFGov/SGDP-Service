using System.ComponentModel.DataAnnotations;
using Microsoft.Graph.Drives.Item.Items.Item.GetActivitiesByInterval;

namespace api;

public class DemandaDTO
{
    [Key]
    public int DemandaId {get;set;}

    [StringLength(200)]
    public string NM_DEMANDA {get;set;}

    public DateTime? DT_SOLICITACAO {get;set;}

    public DateTime? DT_ABERTURA {get;set;}

    public DateTime? DT_CONCLUSAO {get;set;}

    public string CATEGORIA {get;set;}
    
    [StringLength(100)]
    public string STATUS {get;set;}

    [StringLength(200)]
    public string NM_PO_SUBTDCR {get;set;}

    [StringLength(200)]
    public string NM_PO_DEMANDANTE {get;set;}

    [StringLength(200)]
    public string PATROCINADOR {get;set;}

    [StringLength(100)]
    public string UNIDADE {get;set;}

    [StringLength(400)]
    public string NR_PROCESSO_SEI {get;set;}

    [StringLength(100)]
    public string PERIODICO {get;set;}
     
    [StringLength(100)]
    public string PERIODICIDADE {get;set;}

    public string NM_AREA_DEMANDANTE {get;set;}
}