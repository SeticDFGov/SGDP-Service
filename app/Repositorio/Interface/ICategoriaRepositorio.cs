using Models;
using api;
namespace Repositorio.Interface;

public interface ICategoriaRepositorio
{
    Task<List<Categoria>> GetCategoriaListItemsAsync();
    Task CreateCategoriaAsync(Categoria categoria);
    Task DeleteCategoriaAsync(int id);
}