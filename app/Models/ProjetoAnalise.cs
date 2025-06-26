using System.ComponentModel.DataAnnotations;

namespace Models;

public class ProjetoAnalise 
{
    [Key]
    public int AnaliseId {get;set;}

    [Required]
    [StringLength(300)]
    public Projeto NM_PROJETO {get;set;}
    
    [Required]
    [StringLength(100)]
    public string ANALISE {get;set;} = "";

    [Required]
    [StringLength(100)]
    public bool ENTRAVE {get;set;}
}