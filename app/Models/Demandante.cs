using System.ComponentModel.DataAnnotations;

namespace Models;

public class AreaDemandante
{
    [Key]
    public int AreaDemandanteID {get;set;}

    [Required]
    [StringLength(300)]
    public string NM_DEMANDANTE {get;set;}
    
    [Required]
    [StringLength(100)]
    public string NM_SIGLA {get;set;}
}