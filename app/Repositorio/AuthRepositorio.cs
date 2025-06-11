using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Models;

namespace Repositorio;

public class AuthRepositorio :IAuthRepositorio
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
    public async Task<LdapResponseDto?> ConsultarUsuarioNoAdAsync(string username)
    {   
        var config = new ConfigAuth();
        var url = config.url; 

        var requestBody = new LdapRequestDto { Email = username };
        var response = await _http.PostAsJsonAsync(url, requestBody);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<LdapResponseDto>();
        return result;
    }

    public string GerarJwt(User usuario)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim("unidade", usuario.Unidade.ToString())
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
}