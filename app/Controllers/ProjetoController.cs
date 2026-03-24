using System.Security.Claims;
using api.Common;
using api.Demanda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace Controllers;

[ApiController]
[Authorize]
[Route("api/demanda")]
public class DemandaController : ControllerBase
{
    private readonly IDemandaService _service;
    private readonly IPermissionService _permissionService;

    public DemandaController(IDemandaService service, IPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Lista demandas paginadas filtradas por perfil
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<Demanda>>> GetDemandas([FromQuery] PagedRequest request)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        var userUnidade = await _permissionService.GetUserUnidadeAsync(email);

        var query = _permissionService.GetFilteredDemandasQuery(perfil, userUnidade?.Nome);

        var totalItems = await query.CountAsync();
        var demandas = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync();

        // Calcular situação para cada demanda
        foreach (var demanda in demandas)
        {
            var entregaveis = demanda.Entregaveis ?? new List<Etapa>();
            demanda.SITUACAO = CalcularSituacaoDemanda(entregaveis);
        }

        return Ok(PagedResponse<Demanda>.Create(demandas, totalItems, request));
    }

    /// <summary>
    /// Retorna uma demanda por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDemandaById(int id)
    {
        var demanda = await _service.GetDemandaByIdAsync(id);
        return Ok(demanda);
    }

    /// <summary>
    /// Cria uma nova demanda
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDemanda([FromBody] DemandaCreateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanCreate(perfil, "demanda"))
            return Forbid();

        await _service.CreateDemandaAsync(dto);
        return Ok();
    }

    private string CalcularSituacaoDemanda(ICollection<Etapa> entregaveis)
    {
        if (!entregaveis.Any()) return "Não Iniciado";
        if (entregaveis.Any(e => e.SITUACAO == "Atrasado")) return "Atrasado";
        if (entregaveis.Any(e => e.SITUACAO == "Em Andamento")) return "Em Andamento";
        if (entregaveis.All(e => e.SITUACAO == "Concluído")) return "Concluído";
        return "Não Iniciado";
    }
}
