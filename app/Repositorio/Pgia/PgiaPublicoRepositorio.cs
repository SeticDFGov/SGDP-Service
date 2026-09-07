using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaPublicoRepositorio : IPgiaPublicoRepositorio
{
    private readonly AppDbContext _context;

    public PgiaPublicoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<PgiaSistemaIa> QuerySistemasPublicados()
    {
        // O filtro de publicação é a fronteira do que é público: fica aqui, na origem
        return _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .Where(s => s.PublicadoRegistroPublico);
    }

    public async Task<Dictionary<long, string>> ListarUrlsDeAiaPublicadaAsync(IEnumerable<long> sistemaIds)
    {
        var ids = sistemaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();

        var aias = await _context.PgiaAias
            .Where(a => ids.Contains(a.SistemaIaId) && a.PublicadaPortal && a.UrlPublicacao != null)
            .Select(a => new { a.SistemaIaId, a.Id, a.UrlPublicacao, a.DataPublicacaoPortal })
            .ToListAsync();

        // A publicada mais recentemente por sistema (Id desempata datas iguais ou nulas)
        return aias
            .GroupBy(a => a.SistemaIaId)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(a => a.DataPublicacaoPortal)
                .ThenByDescending(a => a.Id)
                .First().UrlPublicacao!);
    }

    public async Task<Dictionary<long, int>> ContarIndicadoresPublicadosAsync(IEnumerable<long> sistemaIds)
    {
        var ids = sistemaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, int>();

        var contagens = await _context.PgiaIndicadoresDesempenho
            .Where(i => ids.Contains(i.SistemaIaId) && i.PublicadoRegistroPublico)
            .GroupBy(i => i.SistemaIaId)
            .Select(g => new { SistemaIaId = g.Key, Total = g.Count() })
            .ToListAsync();

        return contagens.ToDictionary(c => c.SistemaIaId, c => c.Total);
    }

    public async Task<PgiaSistemaIa?> GetSistemaPublicadoAsync(long sistemaId)
    {
        return await QuerySistemasPublicados().FirstOrDefaultAsync(s => s.Id == sistemaId);
    }

    public async Task<PgiaSolicitacaoCidadao?> GetSolicitacaoPorProtocoloAsync(string protocolo)
    {
        return await _context.PgiaSolicitacoesCidadao
            .Include(s => s.Sistema)
                .ThenInclude(x => x!.Orgao)
            .FirstOrDefaultAsync(s => s.Protocolo == protocolo);
    }

    public async Task<bool> ProtocoloExisteAsync(string protocolo)
    {
        return await _context.PgiaSolicitacoesCidadao.AnyAsync(s => s.Protocolo == protocolo);
    }

    public async Task<PgiaSolicitacaoCidadao?> GetSolicitacaoByIdAsync(long id)
    {
        return await _context.PgiaSolicitacoesCidadao
            .Include(s => s.Sistema)
                .ThenInclude(x => x!.Orgao)
            .Include(s => s.RespondidoPorUser)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<PgiaSolicitacaoCidadao>> ListarSolicitacoesPorOrgaoAsync(long orgaoId)
    {
        return await _context.PgiaSolicitacoesCidadao
            .Include(s => s.Sistema)
                .ThenInclude(x => x!.Orgao)
            .Include(s => s.RespondidoPorUser)
            .Where(s => s.Sistema != null && s.Sistema.OrgaoId == orgaoId)
            .OrderByDescending(s => s.DataAbertura)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    public void AddSolicitacao(PgiaSolicitacaoCidadao solicitacao)
    {
        _context.PgiaSolicitacoesCidadao.Add(solicitacao);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
