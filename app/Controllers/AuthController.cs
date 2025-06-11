using Microsoft.AspNetCore.Mvc;
using Repositorio;

namespace Controllers;

public class AuthController :ControllerBase
{
    public readonly IAuthRepositorio _authRepositorio;

    public AuthController(IAuthRepositorio authRepositorio)
    {
        _authRepositorio = authRepositorio;
    }

    [HttpPost("login-ad")]
public async Task<IActionResult> LoginAd([FromBody] LdapRequestDto request)
{
    var adResponse = await _authRepositorio.ConsultarUsuarioNoAdAsync(request.Email);

    if (adResponse is null || !adResponse.Ok)
        return Unauthorized("Usuário não encontrado no AD.");

    var usuario = await _authRepositorio.CriarOuAtualizarUsuarioAsync(
        adResponse.Nome,
        adResponse.Email
    );

    var token = _authRepositorio.GerarJwt(usuario);
    return Ok(new { token });
}

}