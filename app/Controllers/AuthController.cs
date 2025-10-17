using api.Auth;
using Microsoft.AspNetCore.Mvc;
using Repositorio;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public readonly IAuthRepositorio _authRepositorio;

    public AuthController(IAuthRepositorio authRepositorio)
    {
        _authRepositorio = authRepositorio;
    }

    [HttpPost]
    public async Task<IActionResult> LoginAd([FromBody] LdapRequestDto request)
    {
        var adResponse = await _authRepositorio.ConsultarUsuarioNoAdAsync(request.Email, request.Senha);

        if (adResponse == null)
            return Unauthorized("Usuário não encontrado no AD.");

        await _authRepositorio.CriarOuAtualizarUsuarioAsync(
            adResponse.Nome,
            adResponse.Email
        );
        var user = _authRepositorio.GetUser(adResponse.Email);
        var token = _authRepositorio.GerarJwt(user);
        return Ok(new { token, user });
    }

    [HttpPost("unidade")]
    public async Task<IActionResult> CriarUnidade([FromBody] UnidadeDTO unidade)
    {
        await _authRepositorio.CriarUnidade(unidade);
        return Ok();
    }

    [HttpPost("informar-unidade")]
    public async Task<IActionResult> InformarUnidadeUsuario([FromBody] InformUnidadeUsuario request)
    {
        await _authRepositorio.InformarUnidadeUsuario(request.email, request.unidadeId);
        return Ok();
    }

    [HttpGet("unidades")]
    public async Task<IActionResult> GetUnidadesAsync()
    {
        var unidades = await _authRepositorio.GetUnidadesAsync();
        return Ok(unidades);
    }

    [HttpGet("user/{email}")]
    public async Task<IActionResult> GetUserByEmail(string email)
    {
        var user = _authRepositorio.GetUser(email);
        if (user == null)
        {
            return NotFound("Usuário não encontrado.");
        }
        return Ok(user);
    }

    [HttpPut("alterar-perfil")]
    public async Task<IActionResult> AlterarPerfilUsuario([FromBody] PerfilDTO request, [FromHeader] string adminEmail)
    {
        if (string.IsNullOrEmpty(adminEmail))
        {
            return BadRequest("Email do administrador é obrigatório.");
        }

        var sucesso = await _authRepositorio.AlterarPerfilUsuarioAsync(request.Email, request.Perfil, adminEmail);
        
        if (!sucesso)
        {
            return BadRequest("Não foi possível alterar o perfil. Verifique se você é admin e se o usuário existe.");
        }

        return Ok("Perfil alterado com sucesso.");
    }

    [HttpGet("verificar-admin/{email}")]
    public async Task<IActionResult> VerificarSeAdmin(string email)
    {
        var isAdmin = await _authRepositorio.VerificarSeAdminAsync(email);
        return Ok(new { isAdmin });
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios([FromHeader] string adminEmail)
    {
        if (string.IsNullOrEmpty(adminEmail))
        {
            return BadRequest("Email do administrador é obrigatório.");
        }

        var usuarios = await _authRepositorio.ListarUsuariosAsync(adminEmail);
        
        if (usuarios == null)
        {
           return Unauthorized("Apenas administradores podem listar usuários.");
 
        }

        return Ok(usuarios);
    }

    [HttpPut("modificar-unidade")]
    public async Task<IActionResult> ModificarUnidadeUsuario([FromBody] InformUnidadeUsuario request, [FromHeader] string adminEmail)
    {
        if (string.IsNullOrEmpty(adminEmail))
        {
            return BadRequest("Email do administrador é obrigatório.");
        }
        
        var sucesso = await _authRepositorio.ModificarUnidadeUsuario(request.email, request.unidadeId, adminEmail);
        
        if (!sucesso)
        {
            return BadRequest("Não foi possível alterar a unidade do usuário. Verifique se você é admin e se o usuário e a unidade existem.");
        }

        return Ok("Unidade do usuário alterada com sucesso.");
    }
}