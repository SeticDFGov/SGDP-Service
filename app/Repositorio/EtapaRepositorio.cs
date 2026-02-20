using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio.Interface;

namespace Repositorio;

public class EtapaRepositorio : IEtapaRepositorio
{
    private readonly AppDbContext _context;

    public EtapaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Etapa>> GetEntregaveisByDemandaIdAsync(int demandaId)
    {
        return await _context.Etapas
            .Include(e => e.Responsavel)
            .Where(e => e.NM_PROJETO.demandaId == demandaId)
            .ToListAsync();
    }

    public async Task<List<Etapa>> GetEntregaveisByAreaExecutoraAsync(string areaExecutoraNome)
    {
        return await _context.Etapas
            .Include(e => e.Responsavel)
            .Include(e => e.NM_PROJETO)
            .Where(e => e.Responsavel != null && e.Responsavel.Nome == areaExecutoraNome)
            .ToListAsync();
    }

    public async Task<Etapa?> GetByIdAsync(int id)
    {
        return await _context.Etapas
            .Include(e => e.Responsavel)
            .Include(e => e.NM_PROJETO)
            .FirstOrDefaultAsync(e => e.EtapaProjetoId == id);
    }

    public async Task<Demanda?> GetDemandaByIdAsync(int demandaId)
    {
        return await _context.Demandas
            .FirstOrDefaultAsync(d => d.demandaId == demandaId);
    }

    public async Task<AreaExecutora?> GetAreaExecutoraByIdAsync(int areaExecutoraId)
    {
        return await _context.AreasExecutoras
            .FirstOrDefaultAsync(a => a.AreaExecutoraId == areaExecutoraId);
    }

    public void Add(Etapa etapa)
    {
        _context.Etapas.Add(etapa);
    }

    public void Remove(Etapa etapa)
    {
        _context.Etapas.Remove(etapa);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
