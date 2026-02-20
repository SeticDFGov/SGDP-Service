using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class Etapa
{
    [Key]
    public int EtapaProjetoId { get; set; }

    public Demanda NM_PROJETO { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string NM_ETAPA { get; set; } = string.Empty;

    public AreaExecutora? Responsavel { get; set; }

    public decimal PERCENT_EXECUTADO { get; set; }

    [StringLength(50)]
    public string TIPO_ENTREGA { get; set; } = string.Empty;

    public DateTime DT_INICIO { get; set; }

    public DateTime DT_FIM { get; set; }

    [StringLength(1000)]
    public string? Descricao { get; set; }

    [NotMapped]
    public string SITUACAO
    {
        get => CalcularSituacao();
    }

    private string CalcularSituacao()
    {
        if (PERCENT_EXECUTADO >= 100)
            return "Concluído";

        var hoje = DateTime.UtcNow;
        if (hoje > DT_FIM && PERCENT_EXECUTADO < 100)
            return "Atrasado";

        if (PERCENT_EXECUTADO > 0)
            return "Em Andamento";

        return "Não Iniciado";
    }
}
