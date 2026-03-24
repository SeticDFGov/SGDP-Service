using api.Common;
using api.Demanda;
using Models;

namespace service.Interface;

public interface IDemandaService
{
    Task<List<Demanda>> GetDemandasAsync();
    Task<Demanda> GetDemandaByIdAsync(int id);
    Task CreateDemandaAsync(DemandaCreateDTO dto);
    IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome);
}
