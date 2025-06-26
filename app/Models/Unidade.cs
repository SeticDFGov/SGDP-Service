using System.ComponentModel.DataAnnotations;

namespace app.Models;

public class Unidade
{
    [Key]
    public Guid id {get;set;} = Guid.NewGuid();
    [Required]
    public string Nome {get;set;}
}