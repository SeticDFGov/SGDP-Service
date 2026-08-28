using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaContratoRepositorio : IPgiaContratoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaContratoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    // ── Contratos ─────────────────────────────────────────────────────────────

    private IQueryable<PgiaContratoIa> ContratosComVinculos() =>
        _context.PgiaContratosIa
            .Include(c => c.Orgao)
            .Include(c => c.Sistema);

    public async Task<PgiaContratoIa?> GetContratoByIdAsync(long id)
    {
        return await ContratosComVinculos().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<PgiaContratoIa>> ListarContratosPorOrgaoAsync(long orgaoId)
    {
        return await ContratosComVinculos()
            .Where(c => c.OrgaoId == orgaoId)
            .OrderBy(c => c.NumeroContrato)
            .ToListAsync();
    }

    public void AddContrato(PgiaContratoIa contrato)
    {
        _context.PgiaContratosIa.Add(contrato);
    }

    // ── Instrumentos legados ──────────────────────────────────────────────────

    private IQueryable<PgiaInstrumentoLegado> LegadosComVinculos() =>
        _context.PgiaInstrumentosLegados
            .Include(l => l.Orgao)
            .Include(l => l.Sistema)
            .Include(l => l.TriadoPorUser);

    public async Task<PgiaInstrumentoLegado?> GetLegadoByIdAsync(long id)
    {
        return await LegadosComVinculos().FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<List<PgiaInstrumentoLegado>> ListarLegadosPorOrgaoAsync(long orgaoId)
    {
        return await LegadosComVinculos()
            .Where(l => l.OrgaoId == orgaoId)
            .OrderByDescending(l => l.Id)
            .ToListAsync();
    }

    public void AddLegado(PgiaInstrumentoLegado legado)
    {
        _context.PgiaInstrumentosLegados.Add(legado);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    public async Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id)
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long id)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<PgiaAutorizacaoExcepcional?> GetAutorizacaoByIdAsync(long id)
    {
        return await _context.PgiaAutorizacoesExcepcionais.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
