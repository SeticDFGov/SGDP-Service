using api.Common;
using api.Projeto;
using Models;

namespace service.Interface;

public interface IProjetoService
{
    Task<List<Projeto>> GetProjetoListItemsAsync(string unidade);
    Task<PagedResponse<Projeto>> GetProjetosPaginatedAsync(string unidade, PagedRequest request);
    Task<Projeto> GetProjetoById(int id);

    Task CreateProjetoByTemplate(Projeto projeto);
    Task<QuantidadeProjetoDTO> GetQuantidadeProjetos();

}