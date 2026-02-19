using app.Models;
using Models;

namespace service.Interface;

public interface IPermissionService
{
    /// <summary>
    /// Obtém o perfil do usuário pelo email
    /// </summary>
    Task<string> GetUserPerfilAsync(string email);

    /// <summary>
    /// Obtém a unidade do usuário pelo email
    /// </summary>
    Task<Unidade?> GetUserUnidadeAsync(string email);

    /// <summary>
    /// Verifica se o perfil pode criar o recurso especificado
    /// </summary>
    bool CanCreate(string perfil, string resource);

    /// <summary>
    /// Verifica se o perfil pode editar o recurso especificado
    /// </summary>
    bool CanEdit(string perfil, string resource);

    /// <summary>
    /// Verifica se o perfil pode excluir o recurso especificado
    /// </summary>
    bool CanDelete(string perfil, string resource);

    /// <summary>
    /// Obtém query de projetos filtrada por perfil
    /// </summary>
    IQueryable<Projeto> GetFilteredProjetosQuery(string perfil, string? unidadeNome);
}
