using app.Models;
using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaOperacaoRepositorio
{
    // Incidentes graves (arts. 13, II e 30)
    Task<PgiaIncidente?> GetIncidenteByIdAsync(long id);
    Task<List<PgiaIncidente>> ListarIncidentesPorOrgaoAsync(long orgaoId);
    /// <param name="status">null lista todos os comunicados; com valor, filtra a situação da apuração.</param>
    Task<List<PgiaIncidente>> ListarIncidentesComunicadosAsync(string? status);
    void AddIncidente(PgiaIncidente incidente);

    // Não conformidades (arts. 8º, X e 9º, II)
    Task<PgiaNaoConformidade?> GetNaoConformidadeByIdAsync(long id);
    Task<List<PgiaNaoConformidade>> ListarNaoConformidadesPorOrgaoAsync(long orgaoId);
    Task<List<PgiaNaoConformidade>> ListarNaoConformidadesAsync();
    void AddNaoConformidade(PgiaNaoConformidade naoConformidade);

    // Capacitação ProCapIA/DF (arts. 28 e 29)
    Task<PgiaCapacitacao?> GetCapacitacaoByIdAsync(long id);
    Task<PgiaCapacitacao?> GetCapacitacaoPorAgenteTrilhaAsync(Guid agenteId, string trilha);
    Task<List<PgiaCapacitacao>> ListarCapacitacoesPorOrgaoAsync(long orgaoId);
    void AddCapacitacao(PgiaCapacitacao capacitacao);

    // Registro de uso de IA (art. 13, IV e V) — paginado no service
    IQueryable<PgiaRegistroUsoIa> QueryUsosPorAgente(Guid agenteId);
    IQueryable<PgiaRegistroUsoIa> QueryUsosPorOrgao(long orgaoId);
    void AddUso(PgiaRegistroUsoIa uso);

    // Fontes do registro de uso e do aviso de incidente (art. 13)
    Task<List<PgiaSistemaIa>> ListarSistemasDoOrgaoAsync(long orgaoId);
    Task<List<PgiaPlataformaIaGenerativa>> ListarPlataformasHomologadasAsync();

    // Apoio: existência das entidades referenciadas
    Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id);
    Task<PgiaOrgao?> GetOrgaoByIdAsync(long id);
    Task<PgiaPlataformaIaGenerativa?> GetPlataformaByIdAsync(long id);
    Task<PgiaDocumento?> GetDocumentoByIdAsync(long id);
    Task<User?> GetUserByIdAsync(Guid userId);

    Task SaveChangesAsync();
}
