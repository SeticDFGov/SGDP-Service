namespace service.Interface;

public interface IEtapaService
{
    Task<List<EtapaModel>> GetEtapaListItemsAsync(int projetoId);
    Task CreateEtapa(EtapaDTO etapa);
    Task EditEtapa(AfericaoEtapaDTO etapa, int etapaId);
    Task<Etapa> GetById(int id);
}