using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaOrgaoService
{
    Task<PgiaMeuOrgaoResponse> GetMeuOrgaoAsync(PgiaUserContext ctx);
    Task<List<PgiaOrgaoResponse>> ListarOrgaosAsync(PgiaUserContext ctx);
    Task<PgiaOrgaoResponse> AtualizarDadosOrgaoAsync(long orgaoId, PgiaOrgaoDadosDTO dto, string userEmail);

    Task<List<PgiaPessoaResponse>> ListarPessoasAsync(long orgaoId);
    Task SalvarAgenteInfoAsync(long orgaoId, Guid userId, PgiaAgenteInfoDTO dto, string userEmail);

    Task<PgiaDesignacaoResponse> DesignarResponsavelIaAsync(long orgaoId, PgiaResponsavelIaCreateDTO dto, string userEmail);
    Task<List<PgiaDesignacaoResponse>> ListarResponsaveisIaAsync(long orgaoId);
    Task<PgiaDesignacaoResponse> DesignarEncarregadoDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail);
    Task<List<PgiaDesignacaoResponse>> ListarEncarregadosDadosAsync(long orgaoId);

    Task<List<PgiaPrazoResponse>> ListarPrazosOrgaoAsync(long orgaoId);
    Task<List<PgiaPrazoResponse>> ListarPrazosCentraisAsync();
    Task<PgiaPrazoResponse> MarcarCumprimentoAsync(long prazoId, PgiaMarcarCumprimentoDTO dto, string userEmail);
    Task<PgiaPrazoConformidade?> GetPrazoAsync(long prazoId);
}
