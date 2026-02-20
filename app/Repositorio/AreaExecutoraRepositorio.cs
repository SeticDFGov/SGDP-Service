using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositorio;

public class AreaExecutoraRepositorio : IAreaExecutoraRepositorio
{
    private readonly AppDbContext _context;

    public AreaExecutoraRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AreaExecutora>> GetAllAsync()
    {
        return await _context.AreasExecutoras.OrderBy(a => a.Nome).ToListAsync();
    }

    public async Task<AreaExecutora?> GetByIdAsync(int id)
    {
        return await _context.AreasExecutoras.FindAsync(id);
    }

    public async Task AddAsync(AreaExecutora area)
    {
        _context.AreasExecutoras.Add(area);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AreaExecutora area)
    {
        _context.AreasExecutoras.Update(area);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(AreaExecutora area)
    {
        _context.AreasExecutoras.Remove(area);
        await _context.SaveChangesAsync();
    }
}
