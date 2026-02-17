using Models;

namespace Repositorio.Interface;

/// <summary>
/// Interface do repositório de Etapa (apenas acesso a dados)
/// </summary>
public interface IEtapaRepositorio
{
    Task<List<Etapa>> GetEtapasByProjetoIdAsync(int projetoId);
    Task<Etapa?> GetByIdAsync(int id);
    Task<Projeto?> GetProjetoByIdAsync(int projetoId);
    void Add(Etapa etapa);
    Task SaveChangesAsync();
}