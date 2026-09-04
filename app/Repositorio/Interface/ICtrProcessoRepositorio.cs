using Models.Contratacoes;

namespace Repositorio.Interface;

/// <summary>
/// Acesso a dados do módulo Análises de Contratações (processos e manifestações
/// ao TCDF — o mesmo agregado, por isso um repositório só).
/// </summary>
public interface ICtrProcessoRepositorio
{
    /// <summary>Processos ATIVOS, como IQueryable para os filtros do service.</summary>
    IQueryable<CtrProcesso> QueryAtivos();

    /// <summary>Manifestações de processos ATIVOS, com o processo incluído.</summary>
    IQueryable<CtrManifestacaoTcdf> QueryManifestacoesDeProcessosAtivos();

    Task<CtrProcesso?> GetByIdAsync(long id);

    Task<bool> NumeroDuplicadoAsync(string numeroProcesso, long? idAtual);

    /// <summary>Todos os processos ativos (usado pela importação, que faz upsert em lote).</summary>
    Task<List<CtrProcesso>> ListarAtivosAsync();

    Task<List<string>> ListarSiglasAsync();

    void Add(CtrProcesso processo);

    Task<CtrManifestacaoTcdf?> GetManifestacaoByIdAsync(long id);

    /// <summary>Manifestações de um processo, mais recentes primeiro.</summary>
    Task<List<CtrManifestacaoTcdf>> ListarManifestacoesDoProcessoAsync(long processoId);

    /// <summary>
    /// Manifestações dos processos informados, em UMA consulta (resolve
    /// TotalManifestacoes/UltimoEstagioTcdf da listagem sem N+1).
    /// </summary>
    Task<List<CtrManifestacaoTcdf>> ListarManifestacoesPorProcessosAsync(IReadOnlyCollection<long> processoIds);

    void AddManifestacao(CtrManifestacaoTcdf manifestacao);

    Task SaveChangesAsync();
}
