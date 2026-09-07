using app.Models;
using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaRelatorioRepositorio
{
    // Indicadores de desempenho (arts. 24, V, 31 e 32, III)
    Task<PgiaIndicadorDesempenho?> GetIndicadorByIdAsync(long id);
    Task<List<PgiaIndicadorDesempenho>> ListarIndicadoresPorSistemaAsync(long sistemaId);
    /// <summary>Indicadores dos sistemas informados com interseção no período (relatório semestral).</summary>
    Task<List<PgiaIndicadorDesempenho>> ListarIndicadoresDoPeriodoAsync(
        IEnumerable<long> sistemaIds, DateOnly inicio, DateOnly fim);
    void AddIndicador(PgiaIndicadorDesempenho indicador);

    // Relatório semestral (art. 32)
    Task<PgiaRelatorioSemestral?> GetRelatorioSemestralByIdAsync(long id);
    Task<PgiaRelatorioSemestral?> GetRelatorioSemestralAsync(long orgaoId, short ano, short semestre);
    Task<List<PgiaRelatorioSemestral>> ListarRelatoriosSemestraisPorOrgaoAsync(long orgaoId);
    void AddRelatorioSemestral(PgiaRelatorioSemestral relatorio);

    // Relatório Anual de Governança (art. 33)
    Task<PgiaRelatorioAnual?> GetRelatorioAnualByIdAsync(long id);
    Task<PgiaRelatorioAnual?> GetRelatorioAnualPorAnoAsync(short ano);
    Task<List<PgiaRelatorioAnual>> ListarRelatoriosAnuaisAsync();
    void AddRelatorioAnual(PgiaRelatorioAnual relatorio);

    // Auditorias técnicas (arts. 25, § 2º e 34)
    Task<PgiaAuditoriaTecnica?> GetAuditoriaByIdAsync(long id);
    Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasAsync();
    Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasPorAuditorAsync(Guid auditorUserId);
    Task<List<PgiaAuditoriaTecnica>> ListarAuditoriasPorSistemaAsync(long sistemaId);
    void AddAuditoria(PgiaAuditoriaTecnica auditoria);
    Task<List<User>> ListarAuditoresAsync();

    // Fontes do conteúdo agregado do relatório semestral (art. 32, I a V)
    Task<List<PgiaSistemaIa>> ListarSistemasDoOrgaoAsync(long orgaoId);
    Task<List<PgiaIncidente>> ListarIncidentesComunicadosNoPeriodoAsync(long orgaoId, DateTime inicio, DateTime fim);
    Task<List<PgiaCapacitacao>> ListarCapacitacoesPorOrgaoAsync(long orgaoId);
    Task<List<long>> ListarSistemasReclassificadosNoPeriodoAsync(long orgaoId, DateTime inicio, DateTime fim);

    // Apoio
    Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id);
    Task<PgiaOrgao?> GetOrgaoByIdAsync(long id);
    Task<PgiaDocumento?> GetDocumentoByIdAsync(long id);
    Task<User?> GetUserByIdAsync(Guid userId);

    Task SaveChangesAsync();
}
