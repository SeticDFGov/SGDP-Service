using api.Demanda;
using Models;

namespace Repositorio.Interface;

public interface IDemandaRepositorio
{
    Task<List<Demanda>> GetDemandasListItemsAsync();
    Task<Demanda?> GetDemandaById(int id);
    Task CreateDemanda(DemandaDTO demanda);
    Task EditDemanda(DemandaDTO demanda);
    Task DeleteDemanda(int id);
    Task<Dictionary<string, int>> GetQuantidadeTipo();
}