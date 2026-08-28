using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaPrazoRepositorio
{
    Task<PgiaPrazoConformidade?> GetByIdAsync(long id);
    Task<List<PgiaPrazoConformidade>> ListarPorOrgaoAsync(long orgaoId);
    // Linhas-modelo e obrigações da própria SGDI (orgao_id nulo)
    Task<List<PgiaPrazoConformidade>> ListarCentraisAsync();
    void AddRange(IEnumerable<PgiaPrazoConformidade> prazos);
    Task SaveChangesAsync();
}
