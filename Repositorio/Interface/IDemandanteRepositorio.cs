using Models;

namespace Repositorio.Interface;

public interface IDemandanteRepositorio
{
    Task<List<AreaDemandante>> GetDemandanteListItemsAsync();
    Task CreateDemandante(AreaDemandante demandante);
    Task DeleteDemandante(int id);
}