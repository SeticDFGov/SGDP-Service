using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Models;

/// <summary>
/// Representa uma atividade dentro de uma Etapa de um Projeto.
/// As atividades controlam datas e situação, não mais as Etapas.
/// </summary>
public class Atividade
{
    [Key]
    public int AtividadeId { get; set; }

    /// <summary>
    /// FK para a Etapa (Entregável) à qual esta atividade pertence
    /// </summary>
    [Required]
    public int EtapaProjetoId { get; set; }

    public Etapa Etapa { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Categoria { get; set; }

    [StringLength(500)]
    public string? Descricao { get; set; }

    /// <summary>
    /// Data de início prevista (IMUTÁVEL após criação)
    /// </summary>
    [Required]
    public DateTime DT_INICIO_PREVISTO { get; set; }

    /// <summary>
    /// Data de término prevista (IMUTÁVEL após criação)
    /// </summary>
    [Required]
    public DateTime DT_TERMINO_PREVISTO { get; set; }

    /// <summary>
    /// Data de início real (EDITÁVEL)
    /// </summary>
    [AllowNull]
    public DateTime? DT_INICIO_REAL { get; set; }

    /// <summary>
    /// Data de término real (EDITÁVEL)
    /// </summary>
    [AllowNull]
    public DateTime? DT_TERMINO_REAL { get; set; }

    [StringLength(200)]
    public string? RESPONSAVEL_ATIVIDADE { get; set; }

    /// <summary>
    /// Ordem de exibição da atividade dentro da etapa
    /// </summary>
    [AllowNull]
    public int Order { get; set; }

    /// <summary>
    /// Situação calculada automaticamente baseada nas datas
    /// </summary>
    [NotMapped]
    public string SITUACAO
    {
        get
        {
            return DefinirSituacao(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO,
                                   DT_INICIO_REAL, DT_TERMINO_REAL);
        }
    }

    /// <summary>
    /// Percentual de dias decorridos em relação ao prazo previsto
    /// </summary>
    [NotMapped]
    public decimal PERCENT_PLANEJADO
    {
        get
        {
            return CalcularPercentualPlanejado(DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO);
        }
    }

    /// <summary>
    /// Duração prevista em dias
    /// </summary>
    [NotMapped]
    public int DIAS_PREVISTOS
    {
        get
        {
            return (DT_TERMINO_PREVISTO - DT_INICIO_PREVISTO).Days;
        }
    }

    /// <summary>
    /// Duração real em dias (se já iniciada)
    /// </summary>
    [NotMapped]
    public int? DIAS_EXECUTADOS
    {
        get
        {
            if (!DT_INICIO_REAL.HasValue)
                return null;

            DateTime fim = DT_TERMINO_REAL ?? DateTime.UtcNow;
            return (fim - DT_INICIO_REAL.Value).Days;
        }
    }

    /// <summary>
    /// Define a situação da atividade baseada nas datas
    /// </summary>
    private string DefinirSituacao(DateTime dtInicioPrevisto, DateTime dtTerminoPrevisto,
                                    DateTime? dtInicioReal, DateTime? dtTerminoReal)
    {
        DateTime hoje = DateTime.UtcNow;

        bool inicioReal = dtInicioReal.HasValue;
        bool terminoReal = dtTerminoReal.HasValue;

        // Concluída
        if (inicioReal && terminoReal)
            return "Concluído";

        // Atrasada para conclusão (iniciou mas passou do prazo)
        if (inicioReal && !terminoReal && hoje > dtTerminoPrevisto)
            return "Atrasado para Conclusão";

        // Em andamento (iniciou e ainda no prazo)
        if (inicioReal && !terminoReal && hoje <= dtTerminoPrevisto)
            return "Em Andamento";

        // Atrasada para início (não iniciou e já passou da data prevista)
        if (!inicioReal && hoje > dtInicioPrevisto)
            return "Atrasado para Início";

        // Não iniciada (não iniciou e ainda não chegou a data)
        if (!inicioReal && hoje < dtInicioPrevisto)
            return "Não Iniciada";

        return "Não Definido";
    }

    /// <summary>
    /// Calcula o percentual de tempo decorrido em relação ao prazo previsto
    /// </summary>
    private decimal CalcularPercentualPlanejado(DateTime dtInicioPrevisto, DateTime dtTerminoPrevisto)
    {
        var diffDays = (dtTerminoPrevisto - dtInicioPrevisto).Days;

        if (diffDays <= 0)
            return 0;

        var hoje = DateTime.UtcNow;

        // Se ainda não começou
        if (hoje < dtInicioPrevisto)
            return 0;

        // Calcula dias decorridos
        int diffToday;
        if (hoje > dtTerminoPrevisto)
            diffToday = (dtTerminoPrevisto - dtInicioPrevisto).Days;
        else
            diffToday = (hoje - dtInicioPrevisto).Days;

        return (decimal)(diffToday * 100.0 / diffDays);
    }
}
