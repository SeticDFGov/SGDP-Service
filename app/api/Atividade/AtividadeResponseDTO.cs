namespace api.Atividade;

/// <summary>
/// DTO de resposta com todos os dados de uma Atividade (incluindo campos calculados)
/// </summary>
public class AtividadeResponseDTO
{
    /// <summary>
    /// ID da atividade
    /// </summary>
    public int AtividadeId { get; set; }

    /// <summary>
    /// ID da Etapa à qual pertence
    /// </summary>
    public int EtapaProjetoId { get; set; }

    /// <summary>
    /// Nome da Etapa (relacionamento simplificado)
    /// </summary>
    public string? EtapaNome { get; set; }

    /// <summary>
    /// ID do Projeto (relacionamento através da Etapa)
    /// </summary>
    public int? ProjetoId { get; set; }

    /// <summary>
    /// Nome do Projeto (relacionamento simplificado)
    /// </summary>
    public string? ProjetoNome { get; set; }

    /// <summary>
    /// Título da atividade
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Categoria da atividade
    /// </summary>
    public string? Categoria { get; set; }

    /// <summary>
    /// Descrição detalhada
    /// </summary>
    public string? Descricao { get; set; }

    /// <summary>
    /// Data de início prevista (imutável) - em timezone de Brasília
    /// </summary>
    public DateTime? DT_INICIO_PREVISTO { get; set; }

    /// <summary>
    /// Data de término prevista (imutável) - em timezone de Brasília
    /// </summary>
    public DateTime? DT_TERMINO_PREVISTO { get; set; }

    /// <summary>
    /// Data de início real (editável) - em timezone de Brasília
    /// </summary>
    public DateTime? DT_INICIO_REAL { get; set; }

    /// <summary>
    /// Data de término real (editável) - em timezone de Brasília
    /// </summary>
    public DateTime? DT_TERMINO_REAL { get; set; }

    /// <summary>
    /// Situação calculada da atividade
    /// Valores possíveis: "Concluído", "Em Andamento", "Atrasado para Início", "Atrasado para Conclusão", "Não Iniciada"
    /// </summary>
    public string SITUACAO { get; set; } = string.Empty;

    /// <summary>
    /// Responsável pela atividade
    /// </summary>
    public string? RESPONSAVEL_ATIVIDADE { get; set; }

    /// <summary>
    /// Percentual planejado (calculado com base no tempo decorrido)
    /// </summary>
    public decimal PERCENT_PLANEJADO { get; set; }

    /// <summary>
    /// Dias previstos (diferença entre DT_TERMINO_PREVISTO e DT_INICIO_PREVISTO)
    /// </summary>
    public int DIAS_PREVISTOS { get; set; }

    /// <summary>
    /// Dias executados (diferença entre DT_TERMINO_REAL e DT_INICIO_REAL, se ambos existirem)
    /// </summary>
    public int? DIAS_EXECUTADOS { get; set; }

    /// <summary>
    /// Ordem de exibição dentro da etapa
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Peso da atividade em relação ao total de dias do entregável (0-100%)
    /// Calculado como: (DIAS_PREVISTOS desta atividade / Total de DIAS_PREVISTOS do entregável) * 100
    /// </summary>
    public decimal PESO_ATIVIDADE { get; set; }

    /// <summary>
    /// Percentual de execução da atividade (0-100%)
    /// 0% = Não iniciada
    /// 100% = Concluída
    /// Entre 0-100% = baseado em DT_INICIO_REAL e tempo decorrido
    /// </summary>
    public decimal PERCENT_EXECUTADO { get; set; }
}
