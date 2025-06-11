using api.Auth;
using Microsoft.AspNetCore.Mvc;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController :ControllerBase
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
    return Ok(new { token });
}

[HttpPost("unidade")]
public async Task<IActionResult> CriarUnidade([FromBody] UnidadeDTO unidade)
{
    await  _authRepositorio.CriarUnidade(unidade);
    return Ok();
}

}