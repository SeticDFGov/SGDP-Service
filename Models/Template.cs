using System.ComponentModel.DataAnnotations;

namespace Models;

public class Template 
{
    [Key]
    public int TemplateId {get;set;}

    [Required]
    [StringLength(300)]
    public string NM_TEMPLATE {get;set;}
    
    [Required]
    [StringLength(100)]
    public string NM_ETAPA {get;set;}

    [Required]
    [StringLength(100)]
    public decimal PERCENT_TOTAL {get;set;}
}