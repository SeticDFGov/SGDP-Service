using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaRelatorioRepositorio : IPgiaRelatorioRepositorio
{
    private readonly AppDbContext _context;

    public PgiaRelatorioRepositorio(AppDbContext context)
    {
        _context = context;
    }

    // ── Indicadores ───────────────────────────────────────────────────────────

    public async Task<PgiaIndicadorDesempenho?> GetIndicadorByIdAsync(long id)
    {
        return await _context.PgiaIndicadoresDesempenho
            .Include(i => i.Sistema)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<PgiaIndicadorDesempenho>> ListarIndicadoresPorSistemaAsync(long sistemaId)
    {
        return await _context.PgiaIndicadoresDesempenho
            .Include(i => i.Sistema)
            .Where(i => i.SistemaIaId == sistemaId)
            .OrderByDescending(i => i.PeriodoFim)
            .ThenBy(i => i.Nome)
            .ToListAsync();
    }

    public async Task<List<PgiaIndicadorDesempenho>> ListarIndicadoresDoPeriodoAsync(
        IEnumerable<long> sistemaIds, DateOnly inicio, DateOnly fim)
    {
        var ids = sistemaIds.Distinct().ToList();
        if (ids.Count == 0) return new List<PgiaIndicadorDesempenho>();

        // Interseção de períodos: entra o indicador que cobre qualquer parte do semestre
        return await _context.PgiaIndicadoresDesempenho
            .Include(i => i.Sistema)
            .Where(i => ids.Contains(i.SistemaIaId) && i.PeriodoInicio <= fim && i.PeriodoFim >= inicio)
            .OrderBy(i => i.Nome)
            .ToListAsync();
    }

    public void AddIndicador(PgiaIndicadorDesempenho indicador)
    {
        _context.PgiaIndicadoresDesempenho.Add(indicador);
    }

    // ── Relatório semestral ───────────────────────────────────────────────────

    public async Task<PgiaRelatorioSemestral?> GetRelatorioSemestralByIdAsync(long id)
    {
        return await _context.PgiaRelatoriosSemestrais
            .Include(r => r.Orgao)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PgiaRelatorioSemestral?> GetRelatorioSemestralAsync(long orgaoId, short ano, short semestre)
    {
        return await _context.PgiaRelatoriosSemestrais
            .FirstOrDefaultAsync(r => r.OrgaoId == orgaoId && r.Ano == ano && r.Semestre == semestre);
    }

    public async Task<List<PgiaRelatorioSemestral>> ListarRelatoriosSemestraisPorOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaRelatoriosSemestrais
            .Include(r => r.Orgao)
            .Where(r => r.OrgaoId == orgaoId)
            .OrderByDescending(r => r.Ano)
            .ThenByDescending(r => r.Semestre)
            .ToListAsync();
    }

    public void AddRelatorioSemestral(PgiaRelatorioSemestral relatorio)
    {
        _context.PgiaRelatoriosSemestrais.Add(relatorio);
    }

    // ── Relatório anual ───────────────────────────────────────────────────────

    public async Task<PgiaRelatorioAnual?> GetRelatorioAnualByIdAsync(long id)
    {
        return await _context.PgiaRelatoriosAnuais.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PgiaRelatorioAnual?> GetRelatorioAnualPorAnoAsync(short ano)
    {
        return await _context.PgiaRelatoriosAnuais.FirstOrDefaultAsync(r => r.Ano == ano);
    }

    public async Task<List<PgiaRelatorioAnual>> ListarRelatoriosAnuaisAsync()
    {
        return await _context.PgiaRelatoriosAnuais
            .OrderByDescending(r => r.Ano)
            .ToListAsync();
    }

    public void AddRelatorioAnual(PgiaRelatorioAnual relatorio)
    {
        _context.PgiaRelatoriosAnuais.Add(relatorio);
    }

    // ── Auditorias técnicas ───────────────────────────────────────────────────

    private IQueryable<PgiaAuditoriaTecnica> AuditoriasComVinculos() =>
        _context.PgiaAuditoriasTecnicas
            .Include(a => a.Sistema)
                .ThenInclude(s => s!.Orgao)
            .Include(a => a.AuditorUser);

    public async Task<PgiaAuditoriaTecnica?> GetAuditoriaByIdAsync(long id)
    {
        return await AuditoriasComVinculos().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasAsync()
    {
        return await AuditoriasComVinculos()
            .OrderByDescending(a => a.DataInicio)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasPorAuditorAsync(Guid auditorUserId)
    {
        return await AuditoriasComVinculos()
            .Where(a => a.AuditorUserId == auditorUserId)
            .OrderByDescending(a => a.DataInicio)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasPorSistemaAsync(long sistemaId)
    {
        return await AuditoriasComVinculos()
            .Where(a => a.SistemaIaId == sistemaId)
            .OrderByDescending(a => a.DataInicio)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public void AddAuditoria(PgiaAuditoriaTecnica auditoria)
    {
        _context.PgiaAuditoriasTecnicas.Add(auditoria);
    }

    public async Task<List<User>> ListarAuditoresAsync()
    {
        return await _context.Users
            .Where(u => u.PapelPgia == PapeisPgia.Auditoria)
            .OrderBy(u => u.Nome)
            .ToListAsync();
    }

    // ── Fontes do conteúdo agregado (art. 32, I a V) ──────────────────────────

    public async Task<List<PgiaSistemaIa>> ListarSistemasDoOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaSistemasIa
            .Where(s => s.OrgaoId == orgaoId)
            .OrderBy(s => s.Denominacao)
            .ToListAsync();
    }

    public async Task<List<PgiaIncidente>> ListarIncidentesComunicadosNoPeriodoAsync(
        long orgaoId, DateTime inicio, DateTime fim)
    {
        return await _context.PgiaIncidentes
            .Include(i => i.Sistema)
            .Where(i => i.OrgaoId == orgaoId
                && i.DataComunicacaoSgdi != null
                && i.DataComunicacaoSgdi >= inicio
                && i.DataComunicacaoSgdi <= fim)
            .OrderBy(i => i.DataComunicacaoSgdi)
            .ToListAsync();
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

    public async Task<List<long>> ListarSistemasReclassificadosNoPeriodoAsync(
        long orgaoId, DateTime inicio, DateTime fim)
    {
        return await _context.PgiaClassificacoesRisco
            .Where(c => c.Sistema != null && c.Sistema.OrgaoId == orgaoId
                && c.CriadoEm >= inicio && c.CriadoEm <= fim)
            .Select(c => c.SistemaIaId)
            .Distinct()
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

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long id)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
