using app.Models;
using Models;

namespace service.Interface;

public interface IPermissionService
{
    Task<string> GetUserPerfilAsync(string email);
    Task<Unidade?> GetUserUnidadeAsync(string email);
    bool CanCreate(string perfil, string resource);
    bool CanEdit(string perfil, string resource);
    bool CanDelete(string perfil, string resource);
    IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome);
}
