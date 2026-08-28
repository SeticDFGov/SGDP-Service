using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaContratoService
{
    // Contratos de IA (arts. 21, 25 a 27)
    Task<PgiaContratoResponse> CriarContratoAsync(long orgaoId, PgiaContratoCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaContratoResponse> AtualizarContratoAsync(long id, PgiaContratoUpdateDTO dto, PgiaUserContext ctx);
    Task<PgiaContratoResponse> GetContratoAsync(long id);
    Task<List<PgiaContratoResponse>> ListarContratosPorOrgaoAsync(long orgaoId);

    /// <summary>Entidade crua, para o controller checar o escopo de órgão.</summary>
    Task<PgiaContratoIa?> GetContratoEntidadeAsync(long id);

    // Triagem de instrumentos anteriores ao decreto (art. 37)
    Task<PgiaLegadoResponse> CriarLegadoAsync(long orgaoId, PgiaLegadoCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaLegadoResponse> AtualizarLegadoAsync(long id, PgiaLegadoUpdateDTO dto, PgiaUserContext ctx);
    Task<List<PgiaLegadoResponse>> ListarLegadosPorOrgaoAsync(long orgaoId);
    Task<PgiaInstrumentoLegado?> GetLegadoEntidadeAsync(long id);
}
