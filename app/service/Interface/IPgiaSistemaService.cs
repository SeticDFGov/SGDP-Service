using api.Common;
using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaSistemaService
{
    Task<PagedResponse<PgiaSistemaResponse>> ListarSistemasAsync(PgiaUserContext ctx, long orgaoId, PagedRequest request);
    Task<PgiaSistemaResponse> GetSistemaAsync(long id);

    /// <summary>Entidade crua, para o controller checar o escopo de órgão antes de agir.</summary>
    Task<PgiaSistemaIa?> GetSistemaEntidadeAsync(long id);

    Task<PgiaSistemaResponse> CriarSistemaAsync(long orgaoId, PgiaSistemaCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaSistemaResponse> AtualizarSistemaAsync(long id, PgiaSistemaUpdateDTO dto, PgiaUserContext ctx);

    Task<PgiaClassificacaoResponse> ReclassificarAsync(long id, PgiaClassificacaoCreateDTO dto, PgiaUserContext ctx);
    /// <param name="incluirPontuacao">
    /// Pontuação dos quesitos é métrica da SGDI: só vai preenchida para SGDI, CGTIC e admin.
    /// </param>
    Task<List<PgiaClassificacaoResponse>> ListarClassificacoesAsync(long id, bool incluirPontuacao);

    Task<List<PgiaDocumentoResponse>> ListarDocumentosAsync(long sistemaId);
    Task<PgiaDocumentoResponse> CriarDocumentoAsync(long sistemaId, PgiaDocumentoCreateDTO dto, PgiaUserContext ctx);

    // ── Fase 2: homologação do inventário e AIA ───────────────────────────────

    /// <summary>Fila de homologação das instâncias centrais, com a classificação vigente aberta.</summary>
    Task<List<PgiaHomologacaoPendenteResponse>> ListarHomologacoesPendentesAsync(PgiaUserContext ctx, string? situacao);

    /// <summary>Avaliação da SGDI (Aprovar/Vetar com parecer) dos sistemas de rota SGDI.</summary>
    Task<PgiaSistemaResponse> AvaliarHomologacaoAsync(long sistemaId, PgiaAvaliacaoHomologacaoDTO dto, PgiaUserContext ctx);

    /// <summary>Marca o sistema como publicado no Registro Público (art. 24).</summary>
    Task<PgiaSistemaResponse> AtualizarRegistroPublicoAsync(long sistemaId, PgiaRegistroPublicoDTO dto, PgiaUserContext ctx);

    Task<List<PgiaAiaResponse>> ListarAiasAsync(long sistemaId);
    Task<PgiaAiaResponse> CriarAiaAsync(long sistemaId, PgiaAiaCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaAiaResponse> AtualizarAiaAsync(long aiaId, PgiaAiaUpdateDTO dto, PgiaUserContext ctx);

    /// <summary>Entidade crua da AIA, para o controller checar o escopo de órgão pelo sistema.</summary>
    Task<PgiaAia?> GetAiaEntidadeAsync(long aiaId);

    Task<PgiaAiaResponse> PublicarAiaAsync(long aiaId, PgiaAiaPublicacaoDTO dto, PgiaUserContext ctx);
    Task<PgiaAiaResponse> VincularDeliberacaoAiaAsync(long aiaId, long deliberacaoId, PgiaUserContext ctx);
}
