using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaRelatorioService
{
    // ── Indicadores de desempenho (arts. 24, V, 31 e 32, III) ─────────────────

    Task<PgiaIndicadorResponse> CriarIndicadorAsync(long sistemaId, PgiaIndicadorCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaIndicadorResponse> AtualizarIndicadorAsync(long id, PgiaIndicadorCreateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaIndicadorResponse>> ListarIndicadoresPorSistemaAsync(long sistemaId);
    Task<PgiaIndicadorDesempenho?> GetIndicadorEntidadeAsync(long id);

    // ── Relatório semestral (art. 32) ─────────────────────────────────────────

    Task<PgiaRelatorioSemestralResponse> CriarRelatorioSemestralAsync(
        long orgaoId, PgiaRelatorioSemestralCreateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaRelatorioSemestralResponse>> ListarRelatoriosSemestraisAsync(long orgaoId);
    /// <summary>Relatório com o conteúdo agregado do período (incisos I a V).</summary>
    Task<PgiaRelatorioSemestralResponse> GetRelatorioSemestralAsync(long id);
    Task<PgiaRelatorioSemestralResponse> RegistrarEnvioAsync(long id, PgiaRelatorioEnvioDTO dto, PgiaUserContext ctx);
    Task<PgiaRelatorioSemestralResponse> AtualizarSituacaoAsync(long id, PgiaRelatorioSituacaoDTO dto, PgiaUserContext ctx);
    Task<PgiaRelatorioSemestral?> GetRelatorioSemestralEntidadeAsync(long id);

    /// <summary>PDF do relatório semestral para juntar ao processo SEI (art. 32).</summary>
    Task<byte[]> GerarPdfRelatorioSemestralAsync(long id);

    // ── Relatório Anual de Governança (art. 33) ───────────────────────────────

    Task<PgiaRelatorioAnualResponse> CriarRelatorioAnualAsync(PgiaRelatorioAnualCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaRelatorioAnualResponse> AtualizarRelatorioAnualAsync(long id, PgiaRelatorioAnualUpdateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaRelatorioAnualResponse>> ListarRelatoriosAnuaisAsync();

    // ── Auditorias técnicas (arts. 25, § 2º e 34) ─────────────────────────────

    Task<PgiaAuditoriaResponse> CriarAuditoriaAsync(PgiaAuditoriaCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaAuditoriaResponse> AtualizarAuditoriaAsync(long id, PgiaAuditoriaCreateDTO dto, PgiaUserContext ctx);
    /// <summary>Parecer e datas dos trabalhos: só a entidade designada ou o órgão central.</summary>
    Task<PgiaAuditoriaResponse> RegistrarParecerAsync(long id, PgiaAuditoriaParecerDTO dto, PgiaUserContext ctx);
    Task<PgiaAuditoriaResponse> PublicarAuditoriaAsync(long id, PgiaAuditoriaPublicacaoDTO dto, PgiaUserContext ctx);
    Task<List<PgiaAuditoriaResponse>> ListarMinhasAuditoriasAsync(PgiaUserContext ctx);
    Task<List<PgiaAuditoriaResponse>> ListarAuditoriasAsync();
    Task<List<PgiaAuditoriaResponse>> ListarAuditoriasPorSistemaAsync(long sistemaId);
    Task<PgiaAuditoriaTecnica?> GetAuditoriaEntidadeAsync(long id);

    Task<List<PgiaAuditorResponse>> ListarAuditoresAsync();
}
