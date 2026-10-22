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
            .Where(r => r.OrgaoId == orgaoId && r.Ativo)
            .OrderBy(r => r.Posicao == PgiaDominios.PosicaoDesignacao.Titular ? 0 : 1)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync();
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
            .Where(e => e.OrgaoId == orgaoId && e.Ativo)
            .OrderBy(e => e.Posicao == PgiaDominios.PosicaoDesignacao.Titular ? 0 : 1)
            .ThenBy(e => e.Id)
            .FirstOrDefaultAsync();
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

    // ── Genéricos das três designações ──────────────────────────────────────
    // Include por nome: a navegação vem da interface IPgiaDesignacao, e o Include
    // com expressão sobre um tipo genérico não é aceito pelo EF.

    public async Task<T?> GetVigenteAsync<T>(long orgaoId, string posicao) where T : class, IPgiaDesignacao
    {
        return await _context.Set<T>()
            .Include(nameof(IPgiaDesignacao.Agente))
            .Where(d => d.OrgaoId == orgaoId && d.Ativo && d.Posicao == posicao)
            .OrderBy(d => d.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<T>> ListarVigentesAsync<T>(long orgaoId) where T : class, IPgiaDesignacao
    {
        return await _context.Set<T>()
            .Include(nameof(IPgiaDesignacao.Agente))
            .Where(d => d.OrgaoId == orgaoId && d.Ativo)
            .OrderBy(d => d.Id)
            .ToListAsync();
    }

    public async Task<List<T>> ListarAsync<T>(long orgaoId) where T : class, IPgiaDesignacao
    {
        // Mesma ordem de sempre (início da vigência, depois o Id). Sem início da
        // vigência (ato pendente da carga), o PostgreSQL põe a linha no topo (DESC
        // com NULLS FIRST), que é onde a tela quer o que falta completar.
        return await _context.Set<T>()
            .Include(nameof(IPgiaDesignacao.Agente))
            .Where(d => d.OrgaoId == orgaoId)
            .OrderByDescending(d => d.InicioVigencia)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<T?> GetByIdAsync<T>(long orgaoId, long id) where T : class, IPgiaDesignacao
    {
        return await _context.Set<T>()
            .Include(nameof(IPgiaDesignacao.Agente))
            .FirstOrDefaultAsync(d => d.Id == id && d.OrgaoId == orgaoId);
    }

    public void Add<T>(T designacao) where T : class, IPgiaDesignacao
    {
        _context.Set<T>().Add(designacao);
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

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long documentoId)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == documentoId);
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
