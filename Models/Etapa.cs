using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Models;


public class Etapa
{
    [Key]
    public int EtapaProjetoId { get; set; }

    public Projeto NM_PROJETO { get; set; }

    [StringLength(200)]
    public string NM_ETAPA { get; set; }

    [AllowNull]
    public DateTime? DT_INICIO_PREVISTO { get; set; }

    [AllowNull]
    public DateTime? DT_TERMINO_PREVISTO { get; set; }

    [AllowNull]
    public DateTime? DT_INICIO_REAL { get; set; }

    [AllowNull]
    public DateTime? DT_TERMINO_REAL { get; set; }

  
    [NotMapped]
    public string SITUACAO { 
        get {
            return DefinirSituacao(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO,DT_INICIO_REAL, DT_TERMINO_REAL);
        }
    }

   
    [StringLength(200)]
    [Required] 
    public string RESPONSAVEL_ETAPA { get; set; } = "";

  
    [StringLength(500)]
    [Required] 
    public string ANALISE { get; set; } = ""; 

    [AllowNull]
    [Range(0, 100)]
    public decimal? PERCENT_TOTAL_ETAPA { get; set; }

    [AllowNull]
    [Range(0, 100)]
    public decimal? PERCENT_EXEC_ETAPA { get; set; }

    [NotMapped]
    public decimal? PERCENT_EXEC_REAL
    {
        get
        {
            return CalcularPercentualReal(PERCENT_EXEC_ETAPA, PERCENT_TOTAL_ETAPA);
        }
    }

    [NotMapped]
    public decimal PERCENT_PLANEJADO
    {
        get
        {
            return CalcularPercentualPlanejado(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO);
        }
    }

    private decimal? CalcularPercentualReal(decimal? executadoEtapa, decimal? percentTota)
    {
        if (executadoEtapa == null || percentTota == null)
        {
           return 0;
        }

        var executado = percentTota * executadoEtapa / 100;
        return executado;
    }

    private decimal CalcularPercentualPlanejado(DateTime? dtInicioPrevisto, DateTime? dtTerminoPrevisto)
{
 
    if (!dtInicioPrevisto.HasValue || !dtTerminoPrevisto.HasValue)
    {
        return 0;
    }

    // Calcula a diferença de dias
    var diffDays = (dtTerminoPrevisto.Value - dtInicioPrevisto.Value).Days;

    // Verifica se a diferença de dias é positiva
    if (diffDays > 0 && dtInicioPrevisto.Value < DateTime.Now)    
    {
        var diffToday = 0;
        if(DateTime.Now > dtTerminoPrevisto.Value)
            diffToday = (dtTerminoPrevisto.Value - dtInicioPrevisto.Value).Days;
        else{
            diffToday = (DateTime.Now - dtInicioPrevisto.Value).Days;
            
        }
        return (decimal)(diffToday * 100.0 / diffDays);
    }

    return 0;
}


    // Método para definir a situação automaticamente com base nas datas
    private string DefinirSituacao(DateTime? dtInicioPrevisto, DateTime? dtTerminoPrevisto, DateTime? dtInicioReal, DateTime? dtTerminoReal)
    {
        DateTime hoje = DateTime.Now;

        bool inicioReal = dtInicioReal.HasValue;
        bool terminoReal = dtTerminoReal.HasValue;
        DateTime? inicioPrevisto = dtInicioPrevisto;
        DateTime? terminoPrevisto = dtTerminoPrevisto;

        if (!inicioReal && hoje < inicioPrevisto)
            return "não iniciada";
        else if (hoje > inicioPrevisto && !inicioReal)
            return "atrasado para inicio";
        else if (inicioReal && !terminoReal && hoje < terminoPrevisto)
            return "Em andamento";
        else if (inicioReal && !terminoReal && hoje > terminoPrevisto)
            return "atrasado para conclusão";
        else if (inicioReal && terminoReal)
            return "Concluído";
        return "";
    }
}
