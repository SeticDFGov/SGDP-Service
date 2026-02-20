using System.Security.Claims;
using api.Entregavel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;

namespace Controllers;

[ApiController]
[Authorize]
[Route("api/entregavel")]
public class EntregaveisController : ControllerBase
{
    private readonly IEtapaService _service;
    private readonly IPermissionService _permissionService;

    public EntregaveisController(IEtapaService service, IPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Lista entregáveis de uma demanda
    /// </summary>
    [HttpGet("{demandaId}")]
    public async Task<IActionResult> GetEntregaveis(int demandaId)
    {
        var items = await _service.GetEntregaveisByDemandaAsync(demandaId);
        return Ok(items);
    }

    /// <summary>
    /// Lista entregáveis da AreaExecutora do usuário CentralIT
    /// </summary>
    [HttpGet("centralit")]
    public async Task<IActionResult> GetEntregaveisCentralIT([FromQuery] string areaExecutoraNome)
    {
        var items = await _service.GetEntregaveisByCentralITAsync(areaExecutoraNome);
        return Ok(items);
    }

    /// <summary>
    /// Retorna um entregável por ID
    /// </summary>
    [HttpGet("byid/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        return Ok(item);
    }

    /// <summary>
    /// Cria um novo entregável
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateEntregavel([FromBody] EntregavelCreateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanCreate(perfil, "entregavel"))
            return Forbid();

        await _service.CreateEntregavelAsync(dto);
        return Ok();
    }

    /// <summary>
    /// Atualiza um entregável
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntregavel(int id, [FromBody] EntregavelUpdateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "entregavel"))
            return Forbid();

        await _service.UpdateEntregavelAsync(id, dto);
        return Ok();
    }

    /// <summary>
    /// Atualiza percentual executado e descrição (CentralIT)
    /// </summary>
    [HttpPut("{id}/percentual")]
    public async Task<IActionResult> UpdatePercentual(int id, [FromBody] EntregavelUpdatePercentDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "percentual"))
            return Forbid();

        await _service.UpdatePercentualAsync(id, dto);
        return Ok();
    }

    /// <summary>
    /// Remove um entregável
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntregavel(int id)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanDelete(perfil, "entregavel"))
            return Forbid();

        await _service.DeleteEntregavelAsync(id);
        return Ok();
    }
}
