using Models;

namespace Repositorio.Interface;

public interface IDemandanteRepositorio
{
    Task<List<AreaDemandante>> GetDemandanteListItemsAsync();
    Task CreateDemandanteAsync(AreaDemandante demandante);
    Task DeleteDemandanteAsync(int id);
}