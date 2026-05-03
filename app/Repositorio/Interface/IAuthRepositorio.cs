using api.Auth;
using app.Models;

namespace Repositorio
{
    public interface IAuthRepositorio
    {
        Task<User> GetOrCreateUserAsync(string keycloakId, string nome, string email, string perfil);
        Task CriarUnidade(UnidadeDTO unidade);
        Task<List<Unidade>> GetUnidadesAsync();
        Task InformarUnidadeUsuario(string email, string unidadeId);
        Task<List<User>> ListarUsuariosAsync();
        Task<bool> ModificarUnidadeUsuario(string email, string unidadeId);
    }
}
