using System.ComponentModel.DataAnnotations;

namespace Models;

public class Detalhamento
{
    [Key]
    public int detalheId { get; set; }
    
    public Demanda DEMANDA { get; set; }

    public string DETALHAMENTO { get; set; }
}