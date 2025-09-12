using api.Projeto;
using Models;

namespace Repositorio.Interface;

public interface IProjetoRepositorio
{
    Task<List<Projeto>> GetProjetoListItemsAsync(string unidade);
    Task<Projeto> GetProjetoById(int id);
    Task CreateProjeto(Projeto projeto);
    Task CreateProjetoByTemplate(Projeto projeto);
    Task<List<Report>> GetListReport(int projetoId);
    Task AnaliseProjeto(ReportDTO analise);
}