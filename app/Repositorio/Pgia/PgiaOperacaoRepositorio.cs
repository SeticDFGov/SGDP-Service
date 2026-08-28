using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaOperacaoRepositorio : IPgiaOperacaoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaOperacaoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    // ── Incidentes ────────────────────────────────────────────────────────────

    private IQueryable<PgiaIncidente> IncidentesComVinculos() =>
        _context.PgiaIncidentes
            .Include(i => i.Sistema)
            .Include(i => i.Orgao)
            .Include(i => i.ComunicadoPorUser);

    public async Task<PgiaIncidente?> GetIncidenteByIdAsync(long id)
    {
        return await IncidentesComVinculos().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<PgiaIncidente>> ListarIncidentesPorOrgaoAsync(long orgaoId)
    {
        return await IncidentesComVinculos()
            .Where(i => i.OrgaoId == orgaoId)
            .OrderByDescending(i => i.DataDeteccao)
            .ThenByDescending(i => i.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaIncidente>> ListarIncidentesComunicadosAsync(string? status)
    {
        var query = IncidentesComVinculos().Where(i => i.DataComunicacaoSgdi != null);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.StatusApuracao == status);

        return await query
            .OrderByDescending(i => i.DataComunicacaoSgdi)
            .ThenByDescending(i => i.Id)
            .ToListAsync();
    }

    public void AddIncidente(PgiaIncidente incidente)
    {
        _context.PgiaIncidentes.Add(incidente);
    }

    // ── Não conformidades ─────────────────────────────────────────────────────

    public async Task<PgiaNaoConformidade?> GetNaoConformidadeByIdAsync(long id)
    {
        return await _context.PgiaNaoConformidades
            .Include(n => n.Orgao)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<List<PgiaNaoConformidade>> ListarNaoConformidadesPorOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaNaoConformidades
            .Include(n => n.Orgao)
            .Where(n => n.OrgaoId == orgaoId)
            .OrderByDescending(n => n.DataRegistro)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaNaoConformidade>> ListarNaoConformidadesAsync()
    {
        return await _context.PgiaNaoConformidades
            .Include(n => n.Orgao)
            .OrderByDescending(n => n.DataRegistro)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }

    public void AddNaoConformidade(PgiaNaoConformidade naoConformidade)
    {
        _context.PgiaNaoConformidades.Add(naoConformidade);
    }

    // ── Capacitação ───────────────────────────────────────────────────────────

    public async Task<PgiaCapacitacao?> GetCapacitacaoByIdAsync(long id)
    {
        return await _context.PgiaCapacitacoes
            .Include(c => c.Agente)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<PgiaCapacitacao?> GetCapacitacaoPorAgenteTrilhaAsync(Guid agenteId, string trilha)
    {
        return await _context.PgiaCapacitacoes
            .FirstOrDefaultAsync(c => c.AgenteId == agenteId && c.Trilha == trilha);
    }

    public async Task<List<PgiaCapacitacao>> ListarCapacitacoesPorOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaCapacitacoes
            .Include(c => c.Agente)
            .Where(c => c.OrgaoId == orgaoId)
            .OrderBy(c => c.Agente!.Nome)
            .ThenBy(c => c.Trilha)
            .ToListAsync();
    }

    public void AddCapacitacao(PgiaCapacitacao capacitacao)
    {
        _context.PgiaCapacitacoes.Add(capacitacao);
    }

    // ── Registro de uso ───────────────────────────────────────────────────────

    private IQueryable<PgiaRegistroUsoIa> UsosComVinculos() =>
        _context.PgiaRegistrosUsoIa
            .Include(u => u.Agente)
            .Include(u => u.Sistema)
            .Include(u => u.Plataforma);

    public IQueryable<PgiaRegistroUsoIa> QueryUsosPorAgente(Guid agenteId)
    {
        return UsosComVinculos().Where(u => u.AgenteId == agenteId);
    }

    public IQueryable<PgiaRegistroUsoIa> QueryUsosPorOrgao(long orgaoId)
    {
        return UsosComVinculos().Where(u => u.OrgaoId == orgaoId);
    }

    public void AddUso(PgiaRegistroUsoIa uso)
    {
        _context.PgiaRegistrosUsoIa.Add(uso);
    }

    public async Task<List<PgiaSistemaIa>> ListarSistemasDoOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaSistemasIa
            .Where(s => s.OrgaoId == orgaoId)
            .OrderBy(s => s.Denominacao)
            .ToListAsync();
    }

    public async Task<List<PgiaPlataformaIaGenerativa>> ListarPlataformasHomologadasAsync()
    {
        // "Homologada" e "Homologada apta a dados pessoais e sigilosos" (art. 20)
        return await _context.PgiaPlataformasIaGenerativa
            .Where(p => p.StatusHomologacao.StartsWith("Homologada"))
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

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

    public async Task<PgiaPlataformaIaGenerativa?> GetPlataformaByIdAsync(long id)
    {
        return await _context.PgiaPlataformasIaGenerativa.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long id)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
