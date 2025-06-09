namespace api.Etapa;

public class AfericaoEtapaDTO
{
     
    public DateTime? DT_INICIO_REAL { get; set; }

    public DateTime? DT_TERMINO_REAL { get; set; }

    public string ANALISE { get; set; } = ""; 

     public decimal? PERCENT_EXEC_ETAPA { get; set; }

}