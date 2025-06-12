using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Models;

namespace Repositorio;

public class AuthRepositorio : IAuthRepositorio
{
    private readonly AppDbContext _context;
    private readonly HttpClient _http;
    private readonly ConfigAuth _auth;
    public AuthRepositorio(AppDbContext context, HttpClient http, ConfigAuth auth)
    {
        _context = context;
        _http = http;
        _auth = auth;
    }

    public async Task<User?> GetUsuarioByAdUsernameAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CriarOuAtualizarUsuarioAsync(string nome, string email)
    {
        var usuario = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (usuario == null)
        {
            usuario = new User
            {
                Nome = nome,
                Email = email,
            };

            _context.Users.Add(usuario);
            await _context.SaveChangesAsync();
        }

        return usuario;
    }
    public User GetUser(string email)
    {
        var result = _context.Users.FirstOrDefault(e => e.Email == email);
        return result;
    }
    public async Task<LdapResponseDto?> ConsultarUsuarioNoAdAsync(string username, string senha)
    {


        var requestBody = new LdapRequestDto { Email = username, Senha = senha };
        var response = await _http.PostAsJsonAsync(_auth.url, requestBody);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<LdapResponseDto>();
        return result;
    }

    public string GerarJwt(User usuario)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, usuario.Nome??""),
            new Claim(ClaimTypes.Email, usuario.Email ?? ""),
            new Claim("Unidade", usuario.Unidade?.ToString() ?? "")
        };


        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_auth.Key));
        var creds = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _auth.issuer,
            audience: _auth.audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    public async Task CriarUnidade(UnidadeDTO unidade)
    {
        var unidade_add = new Unidade
        {
            Nome = unidade.nome
        };
        _context.Unidades.Add(unidade_add);
        await _context.SaveChangesAsync();
    }

    public async Task InformarUnidadeUsuario(string email, string unidadeId)
    {
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == Guid.Parse(unidadeId));
        usuario.Unidade = unidade;
        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Unidade>> GetUnidadesAsync()
    {
        return await _context.Unidades.ToListAsync();
    }

}