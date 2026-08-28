using api.Common;
using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaOperacaoService
{
    // ── Incidentes graves (arts. 13, II e 30) ─────────────────────────────────

    /// <summary>Aviso do agente ao Responsável de IA, sem comunicação formal à SGDI.</summary>
    Task<PgiaIncidenteResponse> CriarAvisoAsync(PgiaIncidenteAvisoDTO dto, PgiaUserContext ctx);

    /// <summary>Registro formal pelo Responsável de IA, já comunicado à SGDI pelo SEI.</summary>
    Task<PgiaIncidenteResponse> CriarIncidenteFormalAsync(PgiaIncidenteCreateDTO dto, PgiaUserContext ctx);

    Task<PgiaIncidenteResponse> ComunicarAsync(long id, PgiaIncidenteComunicarDTO dto, PgiaUserContext ctx);
    Task<PgiaIncidenteResponse> AtualizarMedidasAsync(long id, PgiaIncidenteMedidasDTO dto, PgiaUserContext ctx);
    Task<PgiaIncidenteResponse> ApurarAsync(long id, PgiaIncidenteApuracaoDTO dto, PgiaUserContext ctx);

    Task<List<PgiaIncidenteResponse>> ListarIncidentesPorOrgaoAsync(long orgaoId);
    Task<List<PgiaIncidenteResponse>> ListarComunicadosAsync(string? status);

    /// <summary>Entidade crua, para o controller checar o escopo de órgão.</summary>
    Task<PgiaIncidente?> GetIncidenteEntidadeAsync(long id);

    /// <summary>Órgão dono do sistema (null se não existe), para a checagem de escopo no controller.</summary>
    Task<long?> GetSistemaOrgaoAsync(long sistemaId);

    // ── Não conformidades (arts. 8º, X e 9º, II) ──────────────────────────────

    Task<PgiaNaoConformidadeResponse> CriarNaoConformidadeAsync(PgiaNaoConformidadeCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaNaoConformidadeResponse> AtualizarNaoConformidadeAsync(long id, PgiaNaoConformidadeUpdateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaNaoConformidadeResponse>> ListarNaoConformidadesPorOrgaoAsync(long orgaoId);
    Task<List<PgiaNaoConformidadeResponse>> ListarNaoConformidadesAsync();
    Task<PgiaNaoConformidade?> GetNaoConformidadeEntidadeAsync(long id);

    // ── Capacitação ProCapIA/DF (arts. 28 e 29) ───────────────────────────────

    Task<PgiaCapacitacaoResponse> CriarCapacitacaoAsync(long orgaoId, PgiaCapacitacaoCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaCapacitacaoResponse> AtualizarCapacitacaoAsync(long id, PgiaCapacitacaoUpdateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaCapacitacaoResponse>> ListarCapacitacoesPorOrgaoAsync(long orgaoId);
    Task<PgiaCapacitacao?> GetCapacitacaoEntidadeAsync(long id);

    // ── Registro de uso de IA (art. 13, IV e V) ───────────────────────────────

    Task<PgiaRegistroUsoResponse> CriarUsoAsync(PgiaRegistroUsoCreateDTO dto, PgiaUserContext ctx);
    Task<PagedResponse<PgiaRegistroUsoResponse>> ListarMeusUsosAsync(PgiaUserContext ctx, PagedRequest request);
    Task<PagedResponse<PgiaRegistroUsoResponse>> ListarUsosPorOrgaoAsync(long orgaoId, PagedRequest request);

    /// <summary>
    /// Sistemas do órgão e plataformas homologadas que alimentam os selects do
    /// registro de uso e do aviso de incidente (art. 13), sem exigir papel PGIA.
    /// </summary>
    Task<PgiaUsoFontesResponse> ListarFontesDeUsoAsync(PgiaUserContext ctx);
}
