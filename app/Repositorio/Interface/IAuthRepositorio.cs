using api.Auth;
using app.Models;

namespace Repositorio
{
    public interface IAuthRepositorio
    {
        Task<User?> GetUsuarioByAdUsernameAsync(string adUsername);
        Task<User> CriarOuAtualizarUsuarioAsync(string nome, string email);
        string GerarJwt(User usuario);
        Task<LdapResponseDto?> ConsultarUsuarioNoAdAsync(string username, string senha);
        Task CriarUnidade(UnidadeDTO unidade);
        User GetUser(string email);
        Task InformarUnidadeUsuario(string email, string unidadeId);
        Task<List<Unidade>> GetUnidadesAsync();
        Task<bool> AlterarPerfilUsuarioAsync(string emailUsuario, string novoPerfil, string emailAdmin);
        Task<bool> VerificarSeAdminAsync(string email);
        Task<List<User>?> ListarUsuariosAsync(string emailAdmin);
    }
}
