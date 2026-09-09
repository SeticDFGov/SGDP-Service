using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaDocumentoRepositorio : IPgiaDocumentoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaDocumentoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaDocumento?> GetByIdAsync(long id)
    {
        return await _context.PgiaDocumentos
            .Include(d => d.EnviadoPorUser)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public void Add(PgiaDocumento documento)
    {
        _context.PgiaDocumentos.Add(documento);
    }

    public async Task<PgiaOrgao?> GetOrgaoByIdAsync(long id)
    {
        return await _context.PgiaOrgaos.FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id)
    {
        return await _context.PgiaSistemasIa.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
