using api.Atividade;
using Models;

namespace demanda_service.Repositorio.Interface;

/// <summary>
/// Interface do repositório de Atividade (apenas acesso a dados)
/// </summary>
public interface IAtividadeRepositorio
{
    // Novos métodos (após refatoração)
    Task<List<Atividade>> GetAtividadesByEtapaIdAsync(int etapaId);
    Task<List<Atividade>> GetAtividadesByProjetoIdAsync(int projetoId);
    Task<Atividade?> GetByIdAsync(int id);
    Task<Etapa?> GetEtapaByIdAsync(int etapaId);
    void Add(Atividade atividade);
    void Remove(Atividade atividade);
    Task SaveChangesAsync();

    // Métodos legados (compatibilidade com Reports)
    Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO);
    Task RemoverAtividade(int atividadeId);
    Task<List<Atividade>> VisualizarAtividades(int projetoId);
}