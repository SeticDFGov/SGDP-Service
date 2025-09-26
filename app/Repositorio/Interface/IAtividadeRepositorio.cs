using api.Atividade;
using api.Projeto;
using Models;
using AtividadeDTO = api.Atividade.AtividadeDTO;

namespace demanda_service.Repositorio.Interface;

public interface IAtividadeRepositorio
{
    public Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO);
    public Task InserirAtividade(AtividadeDTO atividadeDTO, int reportId);
    public Task AlterarAtividade(AtividadeDTO atividadeDTO, int atividadeId);
    public Task RemoverAtividade(int atividadeId);
    public Task<List<Atividade>> VisualizarAtividades(int reportId);
    public byte[] GerarRelatorioPedidos(Guid reportId);
    public Task GerarStatusReport(int  reportId);
}