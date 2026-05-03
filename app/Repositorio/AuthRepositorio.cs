using api.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositorio;

public class AuthRepositorio : IAuthRepositorio
{
    private readonly AppDbContext _context;

    public AuthRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> GetOrCreateUserAsync(string keycloakId, string nome, string email, string perfil)
    {
        var user = await _context.Users.Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (user == null)
            user = await _context.Users.Include(u => u.Unidade)
                .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User { KeycloakId = keycloakId, Nome = nome, Email = email, Perfil = perfil };
            _context.Users.Add(user);
        }
        else
        {
            user.KeycloakId = keycloakId;
            user.Nome = nome;
            user.Perfil = perfil;
            _context.Users.Update(user);
        }

        await _context.SaveChangesAsync();
        return user;
    }

    public async Task CriarUnidade(UnidadeDTO unidade)
    {
        _context.Unidades.Add(new Unidade { Nome = unidade.nome });
        await _context.SaveChangesAsync();
    }

    public async Task<List<Unidade>> GetUnidadesAsync()
    {
        return await _context.Unidades.ToListAsync();
    }

    public async Task InformarUnidadeUsuario(string email, string unidadeId)
    {
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == Guid.Parse(unidadeId));
        if (usuario == null || unidade == null) return;
        usuario.Unidade = unidade;
        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task<List<User>> ListarUsuariosAsync()
    {
        return await _context.Users.Include(u => u.Unidade).ToListAsync();
    }

    public async Task<bool> ModificarUnidadeUsuario(string email, string unidadeId)
    {
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (usuario == null) return false;

        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == Guid.Parse(unidadeId));
        if (unidade == null) return false;

        usuario.Unidade = unidade;
        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();
        return true;
    }
}
