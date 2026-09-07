using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaOrgaoRepositorio : IPgiaOrgaoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaOrgaoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaOrgao?> GetByIdAsync(long id)
    {
        return await _context.PgiaOrgaos
            .Include(o => o.Unidade)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<PgiaOrgao?> GetBySiglaAsync(string sigla)
    {
        return await _context.PgiaOrgaos
            .FirstOrDefaultAsync(o => o.Sigla == sigla);
    }

    public async Task<PgiaOrgao?> GetByUnidadeIdAsync(Guid unidadeId)
    {
        return await _context.PgiaOrgaos
            .Include(o => o.Unidade)
            .FirstOrDefaultAsync(o => o.UnidadeId == unidadeId);
    }

    public async Task<List<PgiaOrgao>> ListarAsync()
    {
        return await _context.PgiaOrgaos
            .Include(o => o.Unidade)
            .OrderBy(o => o.Sigla)
            .ToListAsync();
    }

    public IQueryable<PgiaOrgao> Query()
    {
        return _context.PgiaOrgaos
            .Include(o => o.Unidade)
            .AsQueryable();
    }

    public void Add(PgiaOrgao orgao)
    {
        _context.PgiaOrgaos.Add(orgao);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
