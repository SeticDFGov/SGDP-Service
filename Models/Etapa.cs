using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Models;

public class Etapa
{
    [Key]
    public int EtapaProjetoId { get; set; } 

    public Projeto NM_PROJETO { get; set; } 

    [StringLength(200)]
    public string NM_ETAPA { get; set; } 
    [AllowNull]
    public DateTime DT_INICIO_PREVISTO { get; set; }
    [AllowNull]
    public DateTime DT_TERMINO_PREVISTO { get; set; } 
    [AllowNull]
    public DateTime? DT_INICIO_REAL { get; set; } 

    [AllowNull]
    public DateTime? DT_TERMINO_REAL { get; set; } 
    [AllowNull]
    [StringLength(100)]
    public string SITUACAO { get; set; } 
    [AllowNull]
    [StringLength(200)]
    public string RESPONSAVEL_ETAPA { get; set; } 

    [AllowNull]
    [StringLength(500)]
    public string ANALISE { get; set; } 
    [AllowNull]
    [Range(0, 100)]
    public decimal PERCENT_TOTAL_ETAPA { get; set; } 
    [AllowNull]
    [Range(0, 100)]
    public decimal PERCENT_EXEC_ETAPA { get; set; } 
    [NotMapped]

    public decimal PERCENT_EXEC_REAL 
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

    private decimal CalcularPercentualReal(decimal executadoEtapa, decimal percentTota)
    {
        if( executadoEtapa == null || percentTota == null)
        {
            return 0;
        }

        var executado = percentTota*executadoEtapa/100;
        return executado;
    }
    private decimal CalcularPercentualPlanejado(DateTime dtInicioPrevisto, DateTime dtTerminoPrevisto)
    {
        if (dtInicioPrevisto == DateTime.MinValue || dtTerminoPrevisto == DateTime.MinValue)
        {
            return 0;
        }

        var diffDays = (dtTerminoPrevisto - dtInicioPrevisto).Days;

        if (diffDays > 0)
        {
            var diffToday = (DateTime.Now - dtInicioPrevisto).Days;
            return (decimal)(diffToday * 100.0 / diffDays);
        }

        return 0;
    }
}
