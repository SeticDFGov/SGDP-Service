using api.Common;
using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaPublicoService
{
    // ── Superfície anônima (arts. 23 e 24) ────────────────────────────────────

    /// <summary>Registro Público: só sistemas publicados e só os campos do art. 24.</summary>
    Task<PagedResponse<PgiaRegistroPublicoItemResponse>> ListarRegistroPublicoAsync(PagedRequest request);

    /// <summary>Abertura de solicitação pelo cidadão; o protocolo é gerado no servidor.</summary>
    Task<PgiaSolicitacaoProtocoloResponse> CriarSolicitacaoAsync(PgiaSolicitacaoCreateDTO dto);

    /// <summary>Acompanhamento pelo protocolo, sem os dados pessoais do solicitante.</summary>
    Task<PgiaSolicitacaoPublicaResponse?> ConsultarPorProtocoloAsync(string protocolo);

    // ── Tratamento pelo órgão (área autenticada) ──────────────────────────────

    Task<List<PgiaSolicitacaoOrgaoResponse>> ListarSolicitacoesPorOrgaoAsync(long orgaoId);
    Task<PgiaSolicitacaoOrgaoResponse> ResponderAsync(long id, PgiaSolicitacaoRespostaDTO dto, PgiaUserContext ctx);
    Task<PgiaSolicitacaoOrgaoResponse> EncaminharAoDpoAsync(long id, PgiaUserContext ctx);
    Task<PgiaSolicitacaoOrgaoResponse> MarcarEmAnaliseAsync(long id, PgiaUserContext ctx);

    /// <summary>Entidade crua, para o controller checar o escopo de órgão pelo sistema.</summary>
    Task<PgiaSolicitacaoCidadao?> GetSolicitacaoEntidadeAsync(long id);
}
