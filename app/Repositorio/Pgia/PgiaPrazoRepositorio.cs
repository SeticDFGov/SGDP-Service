using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaPrazoRepositorio : IPgiaPrazoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaPrazoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaPrazoConformidade?> GetByIdAsync(long id)
    {
        return await _context.PgiaPrazosConformidade
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<PgiaPrazoConformidade>> ListarPorOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaPrazosConformidade
            .Where(p => p.OrgaoId == orgaoId)
            .OrderBy(p => p.DataLimite)
            .ToListAsync();
    }

    public async Task<List<PgiaPrazoConformidade>> ListarCentraisAsync()
    {
        return await _context.PgiaPrazosConformidade
            .Where(p => p.OrgaoId == null)
            .OrderBy(p => p.DataLimite)
            .ToListAsync();
    }

    public void AddRange(IEnumerable<PgiaPrazoConformidade> prazos)
    {
        _context.PgiaPrazosConformidade.AddRange(prazos);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
