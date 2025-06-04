using api.Etapa;

namespace service.Interface;

public interface IEtapaService
{
    Task<PercentualEtapaDTO> GetPercentEtapas(int projetoId);
    Task CreateEtapa(EtapaDTO etapa);
    Task EditEtapa(AfericaoEtapaDTO etapa, int etapaId);
    Task<Etapa> GetById(int id);
}