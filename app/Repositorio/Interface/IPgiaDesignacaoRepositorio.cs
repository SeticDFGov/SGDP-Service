using app.Models;
using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaDesignacaoRepositorio
{
    /// <summary>
    /// Responsável de IA vigente do órgão: o titular e, sem ele, o suplente (é quem
    /// responde pelo inventário enquanto o titular não é designado).
    /// </summary>
    Task<PgiaResponsavelIa?> GetResponsavelVigenteAsync(long orgaoId);
    Task<List<PgiaResponsavelIa>> ListarResponsaveisAsync(long orgaoId);
    void AddResponsavel(PgiaResponsavelIa designacao);

    /// <summary>Encarregado de Dados vigente do órgão: o titular e, sem ele, o suplente.</summary>
    Task<PgiaEncarregadoDados?> GetEncarregadoVigenteAsync(long orgaoId);
    Task<List<PgiaEncarregadoDados>> ListarEncarregadosAsync(long orgaoId);
    void AddEncarregado(PgiaEncarregadoDados designacao);

    // ── Genéricos das três designações (Responsável, Encarregado e Curador) ──

    /// <summary>A designação vigente do órgão naquela posição (titular ou suplente).</summary>
    Task<T?> GetVigenteAsync<T>(long orgaoId, string posicao) where T : class, IPgiaDesignacao;

    /// <summary>As designações vigentes do órgão (no máximo um titular e um suplente).</summary>
    Task<List<T>> ListarVigentesAsync<T>(long orgaoId) where T : class, IPgiaDesignacao;

    /// <summary>Histórico do órgão: a mais recente primeiro.</summary>
    Task<List<T>> ListarAsync<T>(long orgaoId) where T : class, IPgiaDesignacao;

    Task<T?> GetByIdAsync<T>(long orgaoId, long id) where T : class, IPgiaDesignacao;

    void Add<T>(T designacao) where T : class, IPgiaDesignacao;

    Task<User?> GetUserByIdAsync(Guid userId);
    Task<List<User>> ListarUsersDaUnidadeAsync(Guid unidadeId);
    Task<PgiaDocumento?> GetDocumentoByIdAsync(long documentoId);
    Task<PgiaAgenteInfo?> GetAgenteInfoAsync(Guid userId);
    Task<List<PgiaAgenteInfo>> ListarAgenteInfosAsync(IEnumerable<Guid> userIds);
    void AddAgenteInfo(PgiaAgenteInfo info);

    Task SaveChangesAsync();
}
