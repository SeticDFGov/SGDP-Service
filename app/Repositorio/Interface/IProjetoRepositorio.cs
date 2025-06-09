using api.Projeto;
using Models;

namespace Repositorio.Interface;

public interface IProjetoRepositorio
{
    Task<List<Projeto>> GetProjetoListItemsAsync();
    Task<Projeto> GetProjetoById(int id);
    Task CreateProjeto(Projeto projeto);
    Task CreateProjetoByTemplate(Projeto projeto);
    Task<ProjetoAnalise> GetLastAnaliseProjeto(int projetoid);
    Task AnaliseProjeto(ProjetoAnaliseDTO analise);
}