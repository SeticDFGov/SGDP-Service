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
    public ICollection<Atividade> Atividades {get;set;}
    
    [Required]
    public DateTime Data_criacao {get;set;}
    
    [Required]
    public DateTime Data_fim {get;set;}
}