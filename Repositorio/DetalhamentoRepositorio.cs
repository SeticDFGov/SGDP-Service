using api;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositorio;

public class DetalhamentoRepositorio
{
    private readonly AppDbContext _context;

    public DetalhamentoRepositorio(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<Detalhamento>> GetAllDetalhamentos(int demandaId)
    {
        return await _context.Detalhamentos.Where(d => d.DEMANDA.DemandaId == demandaId).ToListAsync();
    }

    public async Task<Detalhamento> GetDetalhamentoById(int id)
    {
        return await _context.Detalhamentos.FindAsync(id);
    }

    public async Task<Detalhamento> CreateDetalhamento(DetalhamentoDTO detalhamentoDTO)
    {
        var detalhamento = new Detalhamento
        {
            DEMANDA = await _context.Demandas.FindAsync(detalhamentoDTO.DEMANDA),
            DETALHAMENTO = detalhamentoDTO.DETALHAMENTO
        };
        await _context.Detalhamentos.AddAsync(detalhamento);
        await _context.SaveChangesAsync();
        return detalhamento;
    }
    
}
