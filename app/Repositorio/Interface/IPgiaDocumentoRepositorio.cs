using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaDocumentoRepositorio
{
    Task<PgiaDocumento?> GetByIdAsync(long id);
    void Add(PgiaDocumento documento);

    // Apoio: existência e escopo das entidades referenciadas
    Task<PgiaOrgao?> GetOrgaoByIdAsync(long id);
    Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id);

    Task SaveChangesAsync();
}
