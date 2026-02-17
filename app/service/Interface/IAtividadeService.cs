using api.Atividade;
using Models;

namespace service.Interface;

/// <summary>
/// Interface do serviço de Atividade (lógica de negócio)
/// </summary>
public interface IAtividadeService
{
    /// <summary>
    /// Cria uma nova atividade
    /// </summary>
    Task<AtividadeResponseDTO> CreateAtividadeAsync(AtividadeCreateDTO dto);

    /// <summary>
    /// Atualiza uma atividade existente
    /// </summary>
    Task<AtividadeResponseDTO> UpdateAtividadeAsync(int id, AtividadeUpdateDTO dto);

    /// <summary>
    /// Busca uma atividade por ID
    /// </summary>
    Task<AtividadeResponseDTO> GetAtividadeByIdAsync(int id);

    /// <summary>
    /// Busca todas as atividades de uma etapa
    /// </summary>
    Task<List<AtividadeResponseDTO>> GetAtividadesByEtapaIdAsync(int etapaId);

    /// <summary>
    /// Busca todas as atividades de um projeto (através das etapas)
    /// </summary>
    Task<List<AtividadeResponseDTO>> GetAtividadesByProjetoIdAsync(int projetoId);

    /// <summary>
    /// Deleta uma atividade
    /// </summary>
    Task DeleteAtividadeAsync(int id);

    /// <summary>
    /// Inicia uma atividade (seta DT_INICIO_REAL)
    /// </summary>
    Task<AtividadeResponseDTO> IniciarAtividadeAsync(int id, DateTime? dtInicio = null);

    /// <summary>
    /// Conclui uma atividade (seta DT_TERMINO_REAL)
    /// </summary>
    Task<AtividadeResponseDTO> ConcluirAtividadeAsync(int id, DateTime? dtTermino = null);
}
