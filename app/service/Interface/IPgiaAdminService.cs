using api.Pgia;

namespace service.Interface;

public interface IPgiaAdminService
{
    Task AtribuirPapelAsync(AtribuirPapelPgiaDTO dto, string adminEmail);
    Task<List<PgiaOrgaoResponse>> ListarOrgaosAsync();
    Task<PgiaOrgaoResponse> CriarOrgaoAsync(PgiaOrgaoCreateDTO dto, string adminEmail);
    Task<PgiaOrgaoResponse> EditarOrgaoAsync(long id, PgiaOrgaoUpdateDTO dto, string adminEmail);
}
