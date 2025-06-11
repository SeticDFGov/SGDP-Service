using System.ComponentModel.DataAnnotations;

namespace app.Models;

public class User
{   
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public string Nome { get; set; }
    [Required]
    public string Email { get; set; }
    
    public Unidade? Unidade {get;set;}

}