using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio.Interface;

namespace Repositorio;

public class DemandaRepositorio : IDemandaRepositorio
{
    public readonly AppDbContext _context;

    public DemandaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Demanda>> GetDemandasAsync()
    {
        return await _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Demanda?> GetDemandaByIdAsync(int id)
    {
        return await _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .FirstOrDefaultAsync(d => d.demandaId == id);
    }

    public async Task AddAsync(Demanda demanda)
    {
        _context.Demandas.Add(demanda);
        await _context.SaveChangesAsync();
    }
}
