using api.Projeto;
using Models;

namespace service.Interface;

public interface IProjetoService
{
    Task<List<Projeto>> GetProjetoListItemsAsync(string unidade);
    Task<Projeto> GetProjetoById(int id);
    
    Task CreateProjetoByTemplate(Projeto projeto);
    Task<QuantidadeProjetoDTO> GetQuantidadeProjetos();
    
}