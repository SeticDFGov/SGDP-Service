using api.Auth;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Controllers;

/// <summary>
/// MODO LOCAL (execução independente, sem Keycloak) — somente testes.
/// Só responde quando Auth:ModoLocal=true; fora disso, 404 em tudo.
/// O Program.cs recusa a inicialização com a flag ligada fora de Development,
/// então este controller nunca opera em produção.
/// </summary>
[ApiController]
[Route("api/authlocal")]
public class AuthLocalController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AuthSettings _authSettings;

    public AuthLocalController(IConfiguration configuration, IOptions<AuthSettings> authSettings)
    {
        _configuration = configuration;
        _authSettings = authSettings.Value;
    }

    private bool ModoLocalAtivo => _configuration.GetValue<bool>("Auth:ModoLocal");

    public class LoginLocalRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
    }

    /// <summary>
    /// Emite um token local com o mesmo formato dos do Keycloak, para o usuário
    /// de teste informado. O restante do fluxo (/me, perfis, guards) é o real.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public IActionResult Token([FromBody] LoginLocalRequest request)
    {
        if (!ModoLocalAtivo) return NotFound();

        var perfis = new[] { Perfis.Admin, Perfis.Gestor, Perfis.CentralIT, Perfis.Parceiro, Perfis.Basico };
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nome)
            || !perfis.Contains(request.Perfil))
            return BadRequest("Informe e-mail, nome e um perfil válido (admin, gestor, centralit, parceiro ou basico).");

        var validade = TimeSpan.FromHours(8);
        var token = ModoLocalTokens.EmitirToken(
            request.Email.Trim(), request.Nome.Trim(), request.Perfil,
            _authSettings.ClientId, validade);

        // Mesmo formato do token do Keycloak que o front já consome
        return Ok(new
        {
            access_token = token,
            token_type = "Bearer",
            expires_in = (int)validade.TotalSeconds,
            refresh_token = token,
            refresh_expires_in = (int)validade.TotalSeconds,
            scope = "modo-local"
        });
    }
}
