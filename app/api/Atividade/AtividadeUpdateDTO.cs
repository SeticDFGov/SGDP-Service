using System.ComponentModel.DataAnnotations;

namespace api.Atividade;

/// <summary>
/// DTO para atualização de uma Atividade existente
/// NOTA: Datas previstas (DT_INICIO_PREVISTO, DT_TERMINO_PREVISTO) são IMUTÁVEIS e não podem ser alteradas
/// </summary>
public class AtividadeUpdateDTO
{
    /// <summary>
    /// Data de início real (editável)
    /// Formato esperado: timezone de Brasília
    /// </summary>
    public DateTime? DT_INICIO_REAL { get; set; }

    /// <summary>
    /// Data de término real (editável)
    /// Formato esperado: timezone de Brasília
    /// </summary>
    public DateTime? DT_TERMINO_REAL { get; set; }

    /// <summary>
    /// Categoria da atividade (editável)
    /// </summary>
    [StringLength(100)]
    public string? Categoria { get; set; }

    /// <summary>
    /// Descrição detalhada da atividade (editável)
    /// </summary>
    [StringLength(1000)]
    public string? Descricao { get; set; }

    /// <summary>
    /// Responsável pela atividade (editável)
    /// </summary>
    [StringLength(200)]
    public string? RESPONSAVEL_ATIVIDADE { get; set; }
}
