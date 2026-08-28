using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaSistemaRepositorio : IPgiaSistemaRepositorio
{
    private readonly AppDbContext _context;

    public PgiaSistemaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaSistemaIa?> GetByIdAsync(long id)
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .Include(s => s.Responsavel)
                .ThenInclude(r => r!.Agente)
            .Include(s => s.AvaliadoPorUser)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PgiaSistemaIa?> GetByDenominacaoAsync(long orgaoId, string denominacao)
    {
        return await _context.PgiaSistemasIa
            .FirstOrDefaultAsync(s => s.OrgaoId == orgaoId && s.Denominacao == denominacao);
    }

    public void AddSistema(PgiaSistemaIa sistema)
    {
        _context.PgiaSistemasIa.Add(sistema);
    }

    public async Task<List<PgiaClassificacaoRisco>> ListarClassificacoesAsync(long sistemaId)
    {
        // Histórico do mais recente para o mais antigo (arts. 14 e 16, § 2º)
        return await _context.PgiaClassificacoesRisco
            .Include(c => c.ClassificadoPorUser)
            .Where(c => c.SistemaIaId == sistemaId)
            .OrderByDescending(c => c.Id)
            .ToListAsync();
    }

    public async Task<PgiaClassificacaoRisco?> GetClassificacaoVigenteAsync(long sistemaId)
    {
        return await _context.PgiaClassificacoesRisco
            .Include(c => c.ClassificadoPorUser)
            .Where(c => c.SistemaIaId == sistemaId)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<long, PgiaClassificacaoRisco>> ListarClassificacoesVigentesAsync(IEnumerable<long> sistemaIds)
    {
        var ids = sistemaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, PgiaClassificacaoRisco>();

        // Uma consulta para todos os sistemas da fila; a vigente é a de maior Id
        var classificacoes = await _context.PgiaClassificacoesRisco
            .Include(c => c.ClassificadoPorUser)
            .Where(c => ids.Contains(c.SistemaIaId))
            .ToListAsync();

        return classificacoes
            .GroupBy(c => c.SistemaIaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
    }

    public void AddClassificacao(PgiaClassificacaoRisco classificacao)
    {
        _context.PgiaClassificacoesRisco.Add(classificacao);
    }

    public async Task<PgiaAia?> GetAiaByIdAsync(long aiaId)
    {
        return await _context.PgiaAias
            .Include(a => a.Sistema)
            .Include(a => a.ElaboradaPorUser)
            .Include(a => a.Deliberacao)
            .FirstOrDefaultAsync(a => a.Id == aiaId);
    }

    public async Task<List<PgiaAia>> ListarAiasAsync(long sistemaId)
    {
        return await _context.PgiaAias
            .Include(a => a.ElaboradaPorUser)
            .Include(a => a.Deliberacao)
            .Where(a => a.SistemaIaId == sistemaId)
            .OrderByDescending(a => a.Id)
            .ToListAsync();
    }

    public void AddAia(PgiaAia aia)
    {
        _context.PgiaAias.Add(aia);
    }

    public async Task<PgiaDeliberacaoCgtic?> GetDeliberacaoAsync(long deliberacaoId)
    {
        return await _context.PgiaDeliberacoesCgtic
            .FirstOrDefaultAsync(d => d.Id == deliberacaoId);
    }

    public async Task<List<PgiaDocumento>> ListarDocumentosAsync(long sistemaId)
    {
        return await _context.PgiaDocumentos
            .Include(d => d.EnviadoPorUser)
            .Where(d => d.SistemaIaId == sistemaId)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<PgiaDocumento?> GetDocumentoAsync(long documentoId)
    {
        return await _context.PgiaDocumentos
            .Include(d => d.EnviadoPorUser)
            .FirstOrDefaultAsync(d => d.Id == documentoId);
    }

    public void AddDocumento(PgiaDocumento documento)
    {
        _context.PgiaDocumentos.Add(documento);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
