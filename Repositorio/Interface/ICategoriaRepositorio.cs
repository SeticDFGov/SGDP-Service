using Models;
using api;
namespace Repositorio.Interface;

public interface ICategoriaRepositorio
{
    Task<List<Categoria>> GetCategoriaListItemsAsync();
    Task CreateCategoria(Categoria categoria);
    Task DeleteCategoria(int id);
}