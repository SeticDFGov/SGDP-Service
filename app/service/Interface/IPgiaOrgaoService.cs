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
    Task<PgiaDesignacaoResponse> AtualizarResponsavelIaAsync(long orgaoId, long designacaoId, PgiaResponsavelIaAtualizarDTO dto, string userEmail);
    Task<PgiaDesignacaoResponse> DesignarEncarregadoDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail);
    Task<List<PgiaDesignacaoResponse>> ListarEncarregadosDadosAsync(long orgaoId);
    Task<PgiaDesignacaoResponse> AtualizarEncarregadoDadosAsync(long orgaoId, long designacaoId, PgiaDesignacaoAtualizarDTO dto, string userEmail);
    Task<PgiaDesignacaoResponse> DesignarCuradorDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail);
    Task<List<PgiaDesignacaoResponse>> ListarCuradoresDadosAsync(long orgaoId);
    Task<PgiaDesignacaoResponse> AtualizarCuradorDadosAsync(long orgaoId, long designacaoId, PgiaDesignacaoAtualizarDTO dto, string userEmail);

    Task<List<PgiaPrazoResponse>> ListarPrazosOrgaoAsync(long orgaoId);
    Task<List<PgiaPrazoResponse>> ListarPrazosCentraisAsync();
    Task<PgiaPrazoResponse> MarcarCumprimentoAsync(long prazoId, PgiaMarcarCumprimentoDTO dto, string userEmail);
    Task<PgiaPrazoConformidade?> GetPrazoAsync(long prazoId);
}
