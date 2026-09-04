using System.Security.Claims;
using api.Contratacoes;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;

namespace Controllers.Contratacoes;

/// <summary>
/// Administração do módulo Análises de Contratações: atribuição do papel de acesso.
/// Restrito ao perfil admin do SGDP (mesmo padrão do PgiaAdminController).
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/contratacoes/admin")]
public class CtrAdminController : ControllerBase
{
    private readonly ICtrAdminService _service;

    public CtrAdminController(ICtrAdminService service)
    {
        _service = service;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Lista os papéis válidos do módulo (para a tela de atribuição)
    /// </summary>
    [HttpGet("papeis")]
    public IActionResult GetPapeis()
    {
        return Ok(PapeisContratacoes.Todos);
    }

    /// <summary>
    /// Atribui (ou remove, com PapelContratacoes nulo) o papel do módulo a um usuário
    /// </summary>
    [HttpPut("papel")]
    public async Task<IActionResult> AtribuirPapel([FromBody] AtribuirPapelContratacoesDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        await _service.AtribuirPapelAsync(dto, email);
        return Ok();
    }
}
