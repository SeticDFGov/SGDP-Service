using System.ComponentModel.DataAnnotations;
using Models;
using Projeto = demanda_service.Migrations.Projeto;

namespace Models;

public class Export
{
    [Key]
    public Guid Id { get; set; } = new Guid();
   
    public Projeto NM_PROJETO {get;set;}
    
    [Required]
    [StringLength(100)]
    public string descricao {get;set;} = "";

    [Required]
    [StringLength(100)]
    public string fase {get;set;}
    [Required]
    public ICollection<AtividadeExport> AtividadeExport {get;set;}
    
    [Required]
    public DateTime Data_criacao {get;set;}
    
    [Required]
    public DateTime Data_fim {get;set;}
}


public class AtividadeExport
{
    [Key] 
    public int AtividadeId { get; set; }
    
    public Guid ExportId { get; set; }   
    public Export Export { get; set; }   
    [Required]
    public string titulo {get;set;}
    [Required]
    public situacao situacao {get;set;}
    
    [Required]
    [StringLength(100)]
    public string categoria {get;set;}
    
    [Required]
    [StringLength(300)]
    public string descricao {get;set;} = "";
    
    [Required]
    public DateTime data_termino {get;set;}
    
}


