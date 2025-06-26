using api.Demanda;
using Models;

public interface IDetalhamentoRepositorio
{
    Task<Detalhamento> GetDetalhamentoById(int id);
    Task<List<Detalhamento>> GetAllDetalhamentos(int demandaId);
    Task CreateDetalhamento(DetalhamentoDTO detalhamentoDTO);
    
}