using api.Demanda;
using Microsoft.EntityFrameworkCore;
using Models;
using service;

namespace Repositorio;

public class DetalhamentoRepositorio: IDetalhamentoRepositorio
{
    private readonly AppDbContext _context;

    public DetalhamentoRepositorio(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<Detalhamento>> GetAllDetalhamentos(int demandaId)
    {
        return await _context.Detalhamentos.Where(d => d.DEMANDA.DemandaId == demandaId).ToListAsync() ?? throw new ApiException(ErrorCode.DetalhamentoNaoEncontrado);
    }

    public async Task<Detalhamento> GetDetalhamentoById(int id)
    {
        return await _context.Detalhamentos.FindAsync(id) ?? throw new ApiException(ErrorCode.DetalhamentoNaoEncontrado);
    }

    public async Task CreateDetalhamento(DetalhamentoDTO detalhamentoDTO)
    {
        try
        {
            var detalhamento = new Detalhamento
            {
                DEMANDA = await _context.Demandas.FindAsync(detalhamentoDTO.DEMANDA) ?? throw new ApiException(ErrorCode.DemandasNaoEncontradas),
                DETALHAMENTO = detalhamentoDTO.DETALHAMENTO
            };
            await _context.Detalhamentos.AddAsync(detalhamento);
            await _context.SaveChangesAsync();
        }catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoCriarDetalhamento);
        }
        
    }
    
}
