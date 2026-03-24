using Models;

namespace Repositorio.Interface;

public interface IEtapaRepositorio
{
    Task<List<Etapa>> GetEntregaveisByDemandaIdAsync(int demandaId);
    Task<List<Etapa>> GetEntregaveisByAreaExecutoraAsync(string areaExecutoraNome);
    Task<Etapa?> GetByIdAsync(int id);
    Task<Demanda?> GetDemandaByIdAsync(int demandaId);
    Task<AreaExecutora?> GetAreaExecutoraByIdAsync(int areaExecutoraId);
    void Add(Etapa etapa);
    void Remove(Etapa etapa);
    Task SaveChangesAsync();
}
