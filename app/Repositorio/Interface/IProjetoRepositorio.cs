using api.Projeto;
using Models;

namespace Repositorio.Interface;

public interface IProjetoRepositorio
{
    Task<List<Projeto>> GetProjetoListItemsAsync(string unidade);
    Task<Projeto> GetProjetoById(int id);
   
}