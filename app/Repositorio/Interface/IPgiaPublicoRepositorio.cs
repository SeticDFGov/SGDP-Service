using Models.Pgia;

namespace Repositorio.Interface;

/// <summary>
/// Repositório próprio da superfície pública (arts. 23 e 24). Isolado dos demais
/// de propósito: o que o cidadão anônimo alcança é só o que está declarado aqui.
/// </summary>
public interface IPgiaPublicoRepositorio
{
    /// <summary>Sistemas publicados no Registro Público (art. 24) — base da listagem paginada.</summary>
    IQueryable<PgiaSistemaIa> QuerySistemasPublicados();

    /// <summary>URL da AIA publicada mais recente de cada sistema informado (art. 24, IV).</summary>
    Task<Dictionary<long, string>> ListarUrlsDeAiaPublicadaAsync(IEnumerable<long> sistemaIds);

    /// <summary>Quantidade de indicadores publicados por sistema (art. 24, V).</summary>
    Task<Dictionary<long, int>> ContarIndicadoresPublicadosAsync(IEnumerable<long> sistemaIds);

    /// <summary>Sistema publicado (null quando não existe ou não está publicado).</summary>
    Task<PgiaSistemaIa?> GetSistemaPublicadoAsync(long sistemaId);

    Task<PgiaSolicitacaoCidadao?> GetSolicitacaoPorProtocoloAsync(string protocolo);
    Task<bool> ProtocoloExisteAsync(string protocolo);
    Task<PgiaSolicitacaoCidadao?> GetSolicitacaoByIdAsync(long id);
    Task<List<PgiaSolicitacaoCidadao>> ListarSolicitacoesPorOrgaoAsync(long orgaoId);
    void AddSolicitacao(PgiaSolicitacaoCidadao solicitacao);

    Task SaveChangesAsync();
}
