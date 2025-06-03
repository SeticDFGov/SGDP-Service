namespace Repositorio.Interface;

public interface IProjetoRepositorio
{
    Task<List<Projeto>> GetProjetosListItemsAsync();
    Task<Projeto?> GetProjetoById(int id);
    Task CreateProjeto(Projeto projeto);
    Task EditProjeto(Projeto projeto);
    Task DeleteProjeto(int id);
    Task<Dictionary<string, int>> GetQuantidadeTipo();
}