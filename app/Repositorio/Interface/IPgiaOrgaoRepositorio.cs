using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaOrgaoRepositorio
{
    Task<PgiaOrgao?> GetByIdAsync(long id);
    Task<PgiaOrgao?> GetBySiglaAsync(string sigla);
    Task<PgiaOrgao?> GetByUnidadeIdAsync(Guid unidadeId);
    Task<List<PgiaOrgao>> ListarAsync();
    IQueryable<PgiaOrgao> Query();
    void Add(PgiaOrgao orgao);
    Task SaveChangesAsync();
}
