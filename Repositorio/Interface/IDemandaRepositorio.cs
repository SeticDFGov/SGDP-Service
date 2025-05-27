using Models;

namespace Repositorio.Interface;

public interface IDemandaRepositorio
{
    Task<List<Demanda>> GetDemandasListItemsAsync();
    Task<Demanda?> GetDemandaById(int id);
    Task CreateDemanda(Demanda demanda);
    Task EditDemanda(Demanda demanda);
    Task DeleteDemanda(int id);
    Task<Dictionary<string, int>> GetQuantidadeTipo();
}