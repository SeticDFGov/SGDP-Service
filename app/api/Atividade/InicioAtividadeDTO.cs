namespace api.Atividade;
public class InicioAtividadeDTO 
{
    public int NM_PROJETO {get;set;}
    public string descricao {get;set;} = "";
    public string fase { get; set; }
    public DateTime data_fim { get; set; }
}

