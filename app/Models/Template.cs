using System.ComponentModel.DataAnnotations;

namespace Models;

public enum Complexidade
{
    BAIXA,
    MEDIA,
    ALTA,
    NAO_SE_APLICA

}

public class Template
{
    [Key]
    public int TemplateId { get; set; }

    [Required]
    [StringLength(300)]
    public string NM_TEMPLATE { get; set; }

    [Required]
    [StringLength(100)]
    public string NM_ETAPA { get; set; }

    [Required]
    [StringLength(100)]
    public decimal PERCENT_TOTAL { get; set; }

    public Complexidade COMPLEXIDADE { get; set; }

    public int ORDER { get; set; }
}