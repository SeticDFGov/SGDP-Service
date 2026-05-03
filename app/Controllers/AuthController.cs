using System.Security.Claims;
using System.Text.Json;
using api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Repositorio;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthRepositorio _authRepositorio;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthSettings _authSettings;

    public AuthController(IAuthRepositorio authRepositorio, IHttpClientFactory httpClientFactory, IOptions<AuthSettings> authSettings)
    {
        _authRepositorio = authRepositorio;
        _httpClientFactory = httpClientFactory;
        _authSettings = authSettings.Value;
    }

    private string KeycloakTokenUrl => $"{_authSettings.Authority}/protocol/openid-connect/token";

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] KeycloakLoginRequest request)
    {
        var client = _httpClientFactory.CreateClient("keycloak");
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _authSettings.ClientId,
            ["username"] = request.Email,
            ["password"] = request.Senha
        });

        var response = await client.PostAsync(KeycloakTokenUrl, body);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return Unauthorized(JsonSerializer.Deserialize<JsonElement>(content));

        return Content(content, "application/json");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] KeycloakRefreshRequest request)
    {
        var client = _httpClientFactory.CreateClient("keycloak");
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _authSettings.ClientId,
            ["refresh_token"] = request.RefreshToken
        });

        var response = await client.PostAsync(KeycloakTokenUrl, body);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return Unauthorized(JsonSerializer.Deserialize<JsonElement>(content));

        return Content(content, "application/json");
    }

    [HttpGet("claims")]
    public IActionResult GetClaims()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(claims);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var nome = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value
            ?? User.FindFirst("preferred_username")?.Value ?? "";
        var perfil = User.FindFirst(ClaimTypes.Role)?.Value ?? "basico";

        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized();

        var user = await _authRepositorio.GetOrCreateUserAsync(keycloakId, nome, email ?? "", perfil);
        return Ok(new { user.Id, user.Nome, user.Email, user.Perfil, user.Unidade });
    }

    [HttpPost("unidade")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CriarUnidade([FromBody] UnidadeDTO unidade)
    {
        await _authRepositorio.CriarUnidade(unidade);
        return Ok();
    }

    [HttpGet("unidades")]
    public async Task<IActionResult> GetUnidades()
    {
        var unidades = await _authRepositorio.GetUnidadesAsync();
        return Ok(unidades);
    }

    [HttpPost("informar-unidade")]
    public async Task<IActionResult> InformarUnidade([FromBody] InformUnidadeUsuario request)
    {
        await _authRepositorio.InformarUnidadeUsuario(request.email, request.unidadeId);
        return Ok();
    }

    [HttpGet("usuarios")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ListarUsuarios()
    {
        var usuarios = await _authRepositorio.ListarUsuariosAsync();
        return Ok(usuarios);
    }

    [HttpPut("modificar-unidade")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ModificarUnidade([FromBody] InformUnidadeUsuario request)
    {
        var sucesso = await _authRepositorio.ModificarUnidadeUsuario(request.email, request.unidadeId);
        if (!sucesso)
            return BadRequest("Usuário ou unidade não encontrados.");
        return Ok();
    }
}
