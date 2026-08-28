using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaGovernancaRepositorio : IPgiaGovernancaRepositorio
{
    private readonly AppDbContext _context;

    public PgiaGovernancaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    // ── Deliberações ──────────────────────────────────────────────────────────

    public async Task<PgiaDeliberacaoCgtic?> GetDeliberacaoByIdAsync(long id)
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesAsync()
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .OrderByDescending(d => d.DataDeliberacao)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesPorSistemaAsync(long sistemaId)
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .Where(d => d.SistemaIaId == sistemaId)
            .OrderByDescending(d => d.DataDeliberacao)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public void AddDeliberacao(PgiaDeliberacaoCgtic deliberacao)
    {
        _context.PgiaDeliberacoesCgtic.Add(deliberacao);
    }

    // ── Plataformas ───────────────────────────────────────────────────────────

    public async Task<PgiaPlataformaIaGenerativa?> GetPlataformaByIdAsync(long id)
    {
        return await _context.PgiaPlataformasIaGenerativa.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PgiaPlataformaIaGenerativa?> GetPlataformaByNomeAsync(string nome)
    {
        return await _context.PgiaPlataformasIaGenerativa.FirstOrDefaultAsync(p => p.Nome == nome);
    }

    public async Task<List<PgiaPlataformaIaGenerativa>> ListarPlataformasAsync()
    {
        return await _context.PgiaPlataformasIaGenerativa
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public void AddPlataforma(PgiaPlataformaIaGenerativa plataforma)
    {
        _context.PgiaPlataformasIaGenerativa.Add(plataforma);
    }

    // ── Normas ────────────────────────────────────────────────────────────────

    public async Task<PgiaNormaComplementar?> GetNormaByIdAsync(long id)
    {
        return await _context.PgiaNormasComplementares.FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<List<PgiaNormaComplementar>> ListarNormasAsync()
    {
        return await _context.PgiaNormasComplementares
            .OrderByDescending(n => n.DataPublicacao)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }

    public void AddNorma(PgiaNormaComplementar norma)
    {
        _context.PgiaNormasComplementares.Add(norma);
    }

    // ── Autorizações ──────────────────────────────────────────────────────────

    public async Task<PgiaAutorizacaoExcepcional?> GetAutorizacaoByIdAsync(long id)
    {
        return await _context.PgiaAutorizacoesExcepcionais
            .Include(a => a.Orgao)
            .Include(a => a.Plataforma)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<PgiaAutorizacaoExcepcional>> ListarAutorizacoesAsync(long? orgaoId)
    {
        var query = _context.PgiaAutorizacoesExcepcionais
            .Include(a => a.Orgao)
            .Include(a => a.Plataforma)
            .AsQueryable();

        if (orgaoId != null)
            query = query.Where(a => a.OrgaoId == orgaoId);

        return await query
            .OrderByDescending(a => a.DataAutorizacao)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public void AddAutorizacao(PgiaAutorizacaoExcepcional autorizacao)
    {
        _context.PgiaAutorizacoesExcepcionais.Add(autorizacao);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    public async Task<List<PgiaSistemaIa>> ListarSistemasAsync()
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .OrderBy(s => s.Orgao!.Sigla)
            .ThenBy(s => s.Denominacao)
            .ToListAsync();
    }

    public async Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id)
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PgiaOrgao?> GetOrgaoByIdAsync(long id)
    {
        return await _context.PgiaOrgaos.FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long id)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<PgiaContratoIa?> GetContratoByIdAsync(long id)
    {
        return await _context.PgiaContratosIa.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
