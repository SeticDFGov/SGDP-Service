using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using app.Models;

namespace Models;

public class Demanda
{
    [Key] public int demandaId { get; set; }

    [Required]
    [StringLength(200)] public string NM_PROJETO { get; set; } = string.Empty;

    [StringLength(300)] public string? NR_PROCESSO_SEI { get; set; }

    public AreaDemandante? AREA_DEMANDANTE { get; set; }

    public Esteira? Esteira { get; set; }

    public ICollection<Etapa>? Entregaveis { get; set; }

    [NotMapped] public string? SITUACAO { get; set; }
}
