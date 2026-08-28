using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace app.Auth;

/// <summary>
/// Emissão de tokens do MODO LOCAL (execução sem Keycloak, só para testes).
/// Habilitado apenas com Auth:ModoLocal=true e nunca em produção (o Program.cs
/// recusa a inicialização). A chave é gerada por processo: reiniciou a API,
/// os tokens antigos morrem. Os tokens carregam o mesmo formato dos do Keycloak
/// (sub, email, name e resource_access com as roles do client), então todo o
/// restante do sistema — GetOrCreateUserAsync, perfis, guards — funciona intacto.
/// </summary>
public static class ModoLocalTokens
{
    public const string Issuer = "sgdp-modo-local";

    private static readonly byte[] KeyBytes = RandomNumberGenerator.GetBytes(64);

    public static SymmetricSecurityKey Key { get; } = new(KeyBytes);

    /// <summary>KeycloakId determinístico por e-mail: o mesmo usuário de teste
    /// reencontra o próprio cadastro a cada login.</summary>
    public static string SubjectDoEmail(string email)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return new Guid(hash).ToString();
    }

    public static string EmitirToken(string email, string nome, string perfil, string clientId, TimeSpan validade)
    {
        // resource_access serializado: o fallback do OnTokenValidated (Program.cs)
        // desserializa a claim e copia as roles — mesmo caminho dos tokens reais.
        var resourceAccess = $"{{\"{clientId}\":{{\"roles\":[\"{perfil}\"]}}}}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, SubjectDoEmail(email)),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, nome),
            new Claim("preferred_username", email),
            new Claim("resource_access", resourceAccess, JsonClaimValueTypes.Json)
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(validade),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
