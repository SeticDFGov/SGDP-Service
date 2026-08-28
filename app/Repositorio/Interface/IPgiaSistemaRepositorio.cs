using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaSistemaRepositorio
{
    Task<PgiaSistemaIa?> GetByIdAsync(long id);
    Task<PgiaSistemaIa?> GetByDenominacaoAsync(long orgaoId, string denominacao);
    void AddSistema(PgiaSistemaIa sistema);

    Task<List<PgiaClassificacaoRisco>> ListarClassificacoesAsync(long sistemaId);
    Task<PgiaClassificacaoRisco?> GetClassificacaoVigenteAsync(long sistemaId);

    /// <summary>Classificação vigente de vários sistemas numa consulta só (fila de homologação).</summary>
    Task<Dictionary<long, PgiaClassificacaoRisco>> ListarClassificacoesVigentesAsync(IEnumerable<long> sistemaIds);
    void AddClassificacao(PgiaClassificacaoRisco classificacao);

    // AIA (art. 22): elaborada pelo órgão, publicada e deliberada pelas instâncias centrais
    Task<PgiaAia?> GetAiaByIdAsync(long aiaId);
    Task<List<PgiaAia>> ListarAiasAsync(long sistemaId);
    void AddAia(PgiaAia aia);
    Task<PgiaDeliberacaoCgtic?> GetDeliberacaoAsync(long deliberacaoId);

    Task<List<PgiaDocumento>> ListarDocumentosAsync(long sistemaId);
    Task<PgiaDocumento?> GetDocumentoAsync(long documentoId);
    void AddDocumento(PgiaDocumento documento);

    Task SaveChangesAsync();
}
