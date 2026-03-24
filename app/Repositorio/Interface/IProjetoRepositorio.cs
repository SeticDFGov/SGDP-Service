using Models;

namespace Repositorio.Interface;

public interface IDemandaRepositorio
{
    Task<List<Demanda>> GetDemandasAsync();
    Task<Demanda?> GetDemandaByIdAsync(int id);
    Task AddAsync(Demanda demanda);
}
