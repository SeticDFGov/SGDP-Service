using api.Etapa;
using api.Projeto;
namespace service.Interface;

public interface IEtapaService
{
    Task<PercentualEtapaDTO> GetPercentEtapas(int projetoId);
    Task CreateEtapa(EtapaDTO etapa);
    Task EditEtapa(AfericaoEtapaDTO etapa, int etapaId);
    Task<Etapa> GetById(int id);
    Task IniciarEtapa(int id, DateTime dtInicioPrevisto);
    Task<List<Etapa>> GetEtapaListItemsAsync(int projetoId);
    Task<SituacaoProjetoDTO> GetSituacao();
    Task<TagsDTO> GetTags();
}