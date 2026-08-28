using System.Security.Claims;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;

namespace Controllers.Pgia;

/// <summary>
/// Administração do módulo PGIA: papéis e cadastro de órgãos.
/// Restrito ao perfil admin do SGDP (mesmo padrão do modificar-unidade).
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/pgia/admin")]
public class PgiaAdminController : ControllerBase
{
    private readonly IPgiaAdminService _service;

    public PgiaAdminController(IPgiaAdminService service)
    {
        _service = service;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Lista os papéis PGIA válidos (para a tela de atribuição)
    /// </summary>
    [HttpGet("papeis")]
    public IActionResult GetPapeis()
    {
        return Ok(PapeisPgia.Todos);
    }

    /// <summary>
    /// Atribui (ou remove, com PapelPgia nulo) o papel PGIA de um usuário
    /// </summary>
    [HttpPut("papel")]
    public async Task<IActionResult> AtribuirPapel([FromBody] AtribuirPapelPgiaDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        await _service.AtribuirPapelAsync(dto, email);
        return Ok();
    }

    /// <summary>
    /// Lista os órgãos PGIA cadastrados
    /// </summary>
    [HttpGet("orgaos")]
    public async Task<IActionResult> ListarOrgaos()
    {
        var orgaos = await _service.ListarOrgaosAsync();
        return Ok(orgaos);
    }

    /// <summary>
    /// Cadastra um órgão PGIA e instancia as obrigações com prazo do art. 35
    /// </summary>
    [HttpPost("orgao")]
    public async Task<IActionResult> CriarOrgao([FromBody] PgiaOrgaoCreateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var orgao = await _service.CriarOrgaoAsync(dto, email);
        return Ok(orgao);
    }

    /// <summary>
    /// Atualiza um órgão PGIA (dados, unidade vinculada e status)
    /// </summary>
    [HttpPut("orgao/{id:long}")]
    public async Task<IActionResult> EditarOrgao(long id, [FromBody] PgiaOrgaoUpdateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var orgao = await _service.EditarOrgaoAsync(id, dto, email);
        return Ok(orgao);
    }
}
