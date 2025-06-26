using System.ComponentModel.DataAnnotations;

namespace Models;

public class Categoria
{
    [Key]
    public int CategoriaId {get; set;}
    [StringLength(200)]
    public string Nome {get; set;}
}