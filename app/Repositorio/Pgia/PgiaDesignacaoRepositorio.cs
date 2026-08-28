using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaDesignacaoRepositorio : IPgiaDesignacaoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaDesignacaoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaResponsavelIa?> GetResponsavelVigenteAsync(long orgaoId)
    {
        return await _context.PgiaResponsaveisIa
            .Include(r => r.Agente)
            .FirstOrDefaultAsync(r => r.OrgaoId == orgaoId && r.Ativo);
    }

    public async Task<List<PgiaResponsavelIa>> ListarResponsaveisAsync(long orgaoId)
    {
        return await _context.PgiaResponsaveisIa
            .Include(r => r.Agente)
            .Where(r => r.OrgaoId == orgaoId)
            .OrderByDescending(r => r.InicioVigencia)
            .ThenByDescending(r => r.Id)
            .ToListAsync();
    }

    public void AddResponsavel(PgiaResponsavelIa designacao)
    {
        _context.PgiaResponsaveisIa.Add(designacao);
    }

    public async Task<PgiaEncarregadoDados?> GetEncarregadoVigenteAsync(long orgaoId)
    {
        return await _context.PgiaEncarregadosDados
            .Include(e => e.Agente)
            .FirstOrDefaultAsync(e => e.OrgaoId == orgaoId && e.Ativo);
    }

    public async Task<List<PgiaEncarregadoDados>> ListarEncarregadosAsync(long orgaoId)
    {
        return await _context.PgiaEncarregadosDados
            .Include(e => e.Agente)
            .Where(e => e.OrgaoId == orgaoId)
            .OrderByDescending(e => e.InicioVigencia)
            .ThenByDescending(e => e.Id)
            .ToListAsync();
    }

    public void AddEncarregado(PgiaEncarregadoDados designacao)
    {
        _context.PgiaEncarregadosDados.Add(designacao);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<List<User>> ListarUsersDaUnidadeAsync(Guid unidadeId)
    {
        return await _context.Users
            .Include(u => u.Unidade)
            .Where(u => u.Unidade != null && u.Unidade.id == unidadeId)
            .OrderBy(u => u.Nome)
            .ToListAsync();
    }

    public async Task<PgiaAgenteInfo?> GetAgenteInfoAsync(Guid userId)
    {
        return await _context.PgiaAgenteInfos
            .FirstOrDefaultAsync(a => a.UserId == userId);
    }

    public async Task<List<PgiaAgenteInfo>> ListarAgenteInfosAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.ToList();
        return await _context.PgiaAgenteInfos
            .Where(a => ids.Contains(a.UserId))
            .ToListAsync();
    }

    public void AddAgenteInfo(PgiaAgenteInfo info)
    {
        _context.PgiaAgenteInfos.Add(info);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
