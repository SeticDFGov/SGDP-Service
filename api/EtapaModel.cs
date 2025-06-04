namespace api;

public class EtapaModel
{
    public int EtapaProjetoId { get; set; }
    public string NM_ETAPA { get; set; }
    public string RESPONSAVEL_ETAPA { get; set; }
    public string ANALISE { get; set; }
    public decimal? PERCENT_TOTAL_ETAPA { get; set; }
    public decimal? PERCENT_EXEC_ETAPA { get; set; }
    public DateTime? DT_INICIO_PREVISTO { get; set; }
    public DateTime? DT_TERMINO_PREVISTO { get; set; }
    public DateTime? DT_INICIO_REAL { get; set; }
    public DateTime? DT_TERMINO_REAL { get; set; }

    public int Order { get; set; }

    // === Campos calculados ===
    public string SITUACAO => DefinirSituacao(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO, DT_INICIO_REAL, DT_TERMINO_REAL);

    public decimal? PERCENT_EXEC_REAL => CalcularPercentualReal(PERCENT_EXEC_ETAPA, PERCENT_TOTAL_ETAPA);

    public decimal PERCENT_PLANEJADO => CalcularPercentualPlanejado(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO);

    // Métodos auxiliares
    private decimal? CalcularPercentualReal(decimal? executadoEtapa, decimal? percentTota)
    {
        if (executadoEtapa == null || percentTota == null)
            return 0;
        return percentTota * executadoEtapa / 100;
    }

    private decimal CalcularPercentualPlanejado(DateTime? dtInicioPrevisto, DateTime? dtTerminoPrevisto)
    {
        if (!dtInicioPrevisto.HasValue || !dtTerminoPrevisto.HasValue)
            return 0;

        var diffDays = (dtTerminoPrevisto.Value - dtInicioPrevisto.Value).Days;
        if (diffDays > 0 && dtInicioPrevisto.Value < DateTime.Now)
        {
            var diffToday = DateTime.Now > dtTerminoPrevisto.Value
                ? (dtTerminoPrevisto.Value - dtInicioPrevisto.Value).Days
                : (DateTime.Now - dtInicioPrevisto.Value).Days;

            return (decimal)(diffToday * 100.0 / diffDays);
        }

        return 0;
    }

    private string DefinirSituacao(DateTime? dtInicioPrevisto, DateTime? dtTerminoPrevisto, DateTime? dtInicioReal, DateTime? dtTerminoReal)
    {
        DateTime hoje = DateTime.Now;
        bool inicioReal = dtInicioReal.HasValue;
        bool terminoReal = dtTerminoReal.HasValue;

        if (!inicioReal && hoje < dtInicioPrevisto)
            return "não iniciada";
        else if (hoje > dtInicioPrevisto && !inicioReal)
            return "atrasado para inicio";
        else if (inicioReal && !terminoReal && hoje < dtTerminoPrevisto)
            return "Em andamento";
        else if (inicioReal && !terminoReal && hoje > dtTerminoPrevisto)
            return "atrasado para conclusão";
        else if (inicioReal && terminoReal)
            return "Concluído";

        return "";
    }
}
