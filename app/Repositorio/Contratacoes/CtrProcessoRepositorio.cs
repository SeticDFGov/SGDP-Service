using Microsoft.EntityFrameworkCore;
using Models;
using Models.Contratacoes;
using Repositorio.Interface;

namespace Repositorio.Contratacoes;

public class CtrProcessoRepositorio : ICtrProcessoRepositorio
{
    private readonly AppDbContext _context;

    public CtrProcessoRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<CtrProcesso> QueryAtivos() =>
        _context.CtrProcessos.Where(p => p.Ativo);

    public IQueryable<CtrManifestacaoTcdf> QueryManifestacoesDeProcessosAtivos() =>
        _context.CtrManifestacoesTcdf
            .Include(m => m.Processo)
            .Where(m => m.Processo!.Ativo);

    public async Task<CtrProcesso?> GetByIdAsync(long id) =>
        await _context.CtrProcessos.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<bool> NumeroDuplicadoAsync(string numeroProcesso, long? idAtual) =>
        await _context.CtrProcessos
            .AnyAsync(p => p.Ativo && p.NumeroProcesso == numeroProcesso && (idAtual == null || p.Id != idAtual));

    public async Task<List<CtrProcesso>> ListarAtivosAsync() =>
        await _context.CtrProcessos.Where(p => p.Ativo).ToListAsync();

    public async Task<List<string>> ListarSiglasAsync() =>
        await _context.CtrProcessos
            .Where(p => p.Ativo)
            .Select(p => p.OrgaoSigla)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

    public void Add(CtrProcesso processo) => _context.CtrProcessos.Add(processo);

    public async Task<CtrManifestacaoTcdf?> GetManifestacaoByIdAsync(long id) =>
        await _context.CtrManifestacoesTcdf
            .Include(m => m.Processo)
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<List<CtrManifestacaoTcdf>> ListarManifestacoesDoProcessoAsync(long processoId) =>
        await _context.CtrManifestacoesTcdf
            .Include(m => m.Processo)
            // Só de processo ATIVO, como as demais leituras: manifestação de
            // processo excluído não aparece em lista nenhuma
            .Where(m => m.ProcessoId == processoId && m.Processo!.Ativo)
            .OrderByDescending(m => m.DataOficio)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

    public async Task<List<CtrManifestacaoTcdf>> ListarManifestacoesPorProcessosAsync(
        IReadOnlyCollection<long> processoIds)
    {
        if (processoIds.Count == 0) return new List<CtrManifestacaoTcdf>();

        return await _context.CtrManifestacoesTcdf
            .Where(m => processoIds.Contains(m.ProcessoId))
            .ToListAsync();
    }

    public void AddManifestacao(CtrManifestacaoTcdf manifestacao) =>
        _context.CtrManifestacoesTcdf.Add(manifestacao);

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}
