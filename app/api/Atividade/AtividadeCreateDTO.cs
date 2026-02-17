using System.ComponentModel.DataAnnotations;

namespace api.Atividade;

/// <summary>
/// DTO para criação de uma nova Atividade
/// </summary>
public class AtividadeCreateDTO
{
    /// <summary>
    /// ID da Etapa à qual a atividade pertence (obrigatório)
    /// </summary>
    [Required(ErrorMessage = "EtapaProjetoId é obrigatório")]
    public int EtapaProjetoId { get; set; }

    /// <summary>
    /// Título da atividade (obrigatório)
    /// </summary>
    [Required(ErrorMessage = "Título é obrigatório")]
    [StringLength(200, ErrorMessage = "Título deve ter no máximo 200 caracteres")]
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Categoria da atividade (opcional)
    /// </summary>
    [StringLength(100)]
    public string? Categoria { get; set; }

    /// <summary>
    /// Descrição detalhada da atividade (opcional)
    /// </summary>
    [StringLength(1000)]
    public string? Descricao { get; set; }

    /// <summary>
    /// Data de início prevista (IMUTÁVEL após criação)
    /// Formato esperado: timezone de Brasília
    /// </summary>
    [Required(ErrorMessage = "Data de início prevista é obrigatória")]
    public DateTime DT_INICIO_PREVISTO { get; set; }

    /// <summary>
    /// Data de término prevista (IMUTÁVEL após criação)
    /// Formato esperado: timezone de Brasília
    /// </summary>
    [Required(ErrorMessage = "Data de término prevista é obrigatória")]
    public DateTime DT_TERMINO_PREVISTO { get; set; }

    /// <summary>
    /// Responsável pela atividade (opcional)
    /// </summary>
    [StringLength(200)]
    public string? RESPONSAVEL_ATIVIDADE { get; set; }

    /// <summary>
    /// Ordem de exibição dentro da etapa (opcional, padrão 0)
    /// </summary>
    public int Order { get; set; } = 0;
}
