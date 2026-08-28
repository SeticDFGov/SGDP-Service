using app.Models;
using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaDesignacaoRepositorio
{
    Task<PgiaResponsavelIa?> GetResponsavelVigenteAsync(long orgaoId);
    Task<List<PgiaResponsavelIa>> ListarResponsaveisAsync(long orgaoId);
    void AddResponsavel(PgiaResponsavelIa designacao);

    Task<PgiaEncarregadoDados?> GetEncarregadoVigenteAsync(long orgaoId);
    Task<List<PgiaEncarregadoDados>> ListarEncarregadosAsync(long orgaoId);
    void AddEncarregado(PgiaEncarregadoDados designacao);

    Task<User?> GetUserByIdAsync(Guid userId);
    Task<List<User>> ListarUsersDaUnidadeAsync(Guid unidadeId);
    Task<PgiaAgenteInfo?> GetAgenteInfoAsync(Guid userId);
    Task<List<PgiaAgenteInfo>> ListarAgenteInfosAsync(IEnumerable<Guid> userIds);
    void AddAgenteInfo(PgiaAgenteInfo info);

    Task SaveChangesAsync();
}
