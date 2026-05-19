using api.Entregavel;
using Models;

namespace service.Interface;

public interface IEtapaService
{
    Task<List<Etapa>> GetEntregaveisByDemandaAsync(int demandaId);
    Task<List<Etapa>> GetEntregaveisByCentralITAsync(string areaExecutoraNome);
    Task<Etapa> GetByIdAsync(int id);
    Task CreateEntregavelAsync(EntregavelCreateDTO dto, string userEmail);
    Task UpdateEntregavelAsync(int id, EntregavelUpdateDTO dto, string userEmail);
    Task UpdateEntregavelBasicoAsync(int id, EntregavelUpdateBasicoDTO dto, string userEmail);
    Task UpdatePercentualAsync(int id, EntregavelUpdatePercentDTO dto, string userEmail);
    Task DeleteEntregavelAsync(int id);
}
