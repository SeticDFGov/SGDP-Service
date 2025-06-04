namespace api;
public class EtapaDTO
{

    public int  NM_PROJETO { get; set; }

    public string NM_ETAPA { get; set; }

    public DateTime? DT_INICIO_PREVISTO { get; set; }

    public DateTime? DT_TERMINO_PREVISTO { get; set; }



    public string RESPONSAVEL_ETAPA { get; set; } = "";

    public decimal? PERCENT_TOTAL_ETAPA { get; set; }

}