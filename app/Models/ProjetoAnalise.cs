using System.ComponentModel.DataAnnotations;

namespace Models;

public class Report 
{
    [Key]
    public int ReportId {get;set;}

    [Required]
    [StringLength(300)]
    public Projeto NM_PROJETO {get;set;}
    
    [Required]
    [StringLength(100)]
    public string descricao {get;set;} = "";

    [Required]
    [StringLength(100)]
    public string fase {get;set;}
    
    [Required]
    public List<Atividade> Atividades {get;set;} = new List<Atividade>();
    
    [Required]
    public DateTime Data_criacao {get;set;}
    
    [Required]
    public DateTime Data_fim {get;set;}
}

public class Atividade
{
    [Key] 
    public int AtividadeId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string situacao {get;set;}
    
    [Required]
    [StringLength(100)]
    public string categoria {get;set;}
    
    [Required]
    [StringLength(300)]
    public string descricao {get;set;} = "";
    
    [Required]
    public DateTime data_termino {get;set;}
    
    

}