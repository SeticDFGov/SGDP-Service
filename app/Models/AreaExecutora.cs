using System.ComponentModel.DataAnnotations;

namespace Models;

public class AreaExecutora
{
    [Key]
    public int AreaExecutoraId { get; set; }

    [Required]
    [StringLength(200)]
    public string Nome { get; set; } = string.Empty;
}
