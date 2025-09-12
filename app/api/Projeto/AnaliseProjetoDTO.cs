namespace api.Projeto;
public class ReportDTO 
{
    public int NM_PROJETO {get;set;}
    public string descricao {get;set;} = "";
    public string fase { get; set; }
    public List<AtividadeDTO> atividades { get; set; }
    public DateTime data_fim { get; set; }
}

public class AtividadeDTO
{
    public string situacao {get;set;} = "";
    public string categoria {get;set;} = "";
    public string descricao {get;set;} = "";
    public DateTime data_fim {get;set;}
    
}