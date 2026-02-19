using System.Security.Claims;
using api.Etapa;
using api.Projeto;
using demanda_service.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;

namespace Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EtapaController : ControllerBase
{
    private readonly IEtapaService _service;
    private readonly IPermissionService _permissionService;

    public EtapaController(IEtapaService service, IPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAllEtapas(int id)
    {
        List<Etapa> items = await _service.GetEtapaListItemsAsync(id);
        return Ok(items);
    }
    [HttpGet("api/byid/{id}")]
    public Task<Etapa> GetEtapaById(int id)
    {
        var items = _service.GetById(id);
        return items;
    }

    [HttpPost()]
    public async Task<IActionResult> CreateEtapas([FromBody] EtapaDTO etapa)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanCreate(perfil, "etapa"))
            return Forbid();

        await _service.CreateEtapa(etapa);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEtapas([FromBody] AfericaoEtapaDTO etapa, int id)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "etapa"))
            return Forbid();

        await _service.EditEtapa(etapa, id);
        return Ok();
    }
    [HttpGet("percent/{projetoid}")]
    public async Task<IActionResult> GetPercentEtapas(int projetoid)
    {
        PercentualEtapaDTO response = await _service.GetPercentEtapas(projetoid);
        return Ok(response);
    }

    [HttpGet("situacao")]
    public async Task<ActionResult<SituacaoProjetoDTO>> GetSituacaoProjetos()
    {
        var resultado = await _service.GetSituacao();
        return Ok(resultado);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags()
    {
        var tags = await _service.GetTags();
        return Ok(tags);
    }

    [HttpPut("iniciar/{id}")]
    public async Task<IActionResult> IniciarEtapa(int id, [FromBody] DateTime dtInicioPrevisto)
    {
        // Conversão de timezone movida para o Service layer
        await _service.IniciarEtapa(id, dtInicioPrevisto);
        return Ok();
    }
}
