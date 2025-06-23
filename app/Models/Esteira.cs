using System.ComponentModel.DataAnnotations;

namespace Models;

public class Esteira
{
    [Key]
    public Guid EsteiraId { get; set; } = Guid.NewGuid();
    [Required]
    [StringLength(100)]
    public string Nome { get; set; }
} 