using api.Entregavel;
using Models;

namespace service.Interface;

public interface IEtapaService
{
    Task<List<Etapa>> GetEntregaveisByDemandaAsync(int demandaId);
    Task<List<Etapa>> GetEntregaveisByCentralITAsync(string areaExecutoraNome);
    Task<Etapa> GetByIdAsync(int id);
    Task CreateEntregavelAsync(EntregavelCreateDTO dto);
    Task UpdateEntregavelAsync(int id, EntregavelUpdateDTO dto);
    Task UpdatePercentualAsync(int id, EntregavelUpdatePercentDTO dto);
    Task DeleteEntregavelAsync(int id);
}
