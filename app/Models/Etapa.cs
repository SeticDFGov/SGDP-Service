using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Models;

/// <summary>
/// Representa uma Etapa (Entregável) de um Projeto.
/// Agora as datas e situação são calculadas a partir das Atividades.
/// </summary>
public class Etapa
{
    [Key]
    public int EtapaProjetoId { get; set; }

    public Projeto NM_PROJETO { get; set; }

    [Required]
    [StringLength(200)]
    public string NM_ETAPA { get; set; } = string.Empty;

    [AllowNull]
    public int Order { get; set; }

    /// <summary>
    /// Relacionamento com Atividades (eager loading)
    /// </summary>
    public ICollection<Atividade>? Atividades { get; set; }

    [StringLength(200)]
    public string? RESPONSAVEL_ETAPA { get; set; }

    [StringLength(500)]
    public string? ANALISE { get; set; }

    /// <summary>
    /// Situação calculada agregando as situações das atividades
    /// </summary>
    [NotMapped]
    public string SITUACAO
    {
        get
        {
            return CalcularSituacaoDasAtividades();
        }
    }

    /// <summary>
    /// Data de início prevista (mínima entre as atividades)
    /// </summary>
    [NotMapped]
    public DateTime? DT_INICIO_PREVISTO
    {
        get => Atividades?.OrderBy(a => a.DT_INICIO_PREVISTO).FirstOrDefault()?.DT_INICIO_PREVISTO;
    }

    /// <summary>
    /// Data de término prevista (máxima entre as atividades)
    /// </summary>
    [NotMapped]
    public DateTime? DT_TERMINO_PREVISTO
    {
        get => Atividades?.OrderByDescending(a => a.DT_TERMINO_PREVISTO).FirstOrDefault()?.DT_TERMINO_PREVISTO;
    }

    /// <summary>
    /// Data de início real (mínima entre as atividades que iniciaram)
    /// </summary>
    [NotMapped]
    public DateTime? DT_INICIO_REAL
    {
        get => Atividades?.Where(a => a.DT_INICIO_REAL.HasValue)
                          .OrderBy(a => a.DT_INICIO_REAL)
                          .FirstOrDefault()?.DT_INICIO_REAL;
    }

    /// <summary>
    /// Data de término real (se TODAS as atividades foram concluídas)
    /// </summary>
    [NotMapped]
    public DateTime? DT_TERMINO_REAL
    {
        get
        {
            if (Atividades == null || !Atividades.Any())
                return null;

            // Só retorna data se TODAS as atividades foram concluídas
            if (Atividades.All(a => a.DT_TERMINO_REAL.HasValue))
                return Atividades.OrderByDescending(a => a.DT_TERMINO_REAL).FirstOrDefault()?.DT_TERMINO_REAL;

            return null;
        }
    }

    /// <summary>
    /// Dias previstos (do início ao término previsto)
    /// </summary>
    [NotMapped]
    public int DIAS_PREVISTOS
    {
        get
        {
            if (!DT_INICIO_PREVISTO.HasValue || !DT_TERMINO_PREVISTO.HasValue)
                return 0;

            return (DT_TERMINO_PREVISTO.Value - DT_INICIO_PREVISTO.Value).Days;
        }
    }

    /// <summary>
    /// Percentual de conclusão (atividades concluídas / total)
    /// </summary>
    [NotMapped]
    public decimal PERCENT_EXEC_ETAPA
    {
        get
        {
            if (Atividades == null || !Atividades.Any())
                return 0;

            int concluidas = Atividades.Count(a => a.SITUACAO == "Concluído");
            return (decimal)concluidas / Atividades.Count * 100;
        }
    }

    /// <summary>
    /// Percentual planejado (média dos percentuais das atividades)
    /// </summary>
    [NotMapped]
    public decimal PERCENT_PLANEJADO
    {
        get
        {
            if (Atividades == null || !Atividades.Any())
                return 0;

            return Atividades.Average(a => a.PERCENT_PLANEJADO);
        }
    }

    /// <summary>
    /// Calcula a situação da etapa baseada nas situações das atividades
    /// </summary>
    private string CalcularSituacaoDasAtividades()
    {
        if (Atividades == null || !Atividades.Any())
            return "Sem Atividades";

        // Prioridade 1: Se alguma atividade está atrasada
        if (Atividades.Any(a => a.SITUACAO.Contains("Atrasado")))
            return "Atrasado";

        // Prioridade 2: Se alguma está em andamento
        if (Atividades.Any(a => a.SITUACAO == "Em Andamento"))
            return "Em Andamento";

        // Prioridade 3: Se todas concluídas
        if (Atividades.All(a => a.SITUACAO == "Concluído"))
            return "Concluído";

        // Padrão: Não iniciada
        return "Não Iniciada";
    }
}
