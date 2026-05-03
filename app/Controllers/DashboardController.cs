using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public DashboardController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetDashboardData()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        var userUnidade = await _permissionService.GetUserUnidadeAsync(email);

        var query = _permissionService.GetFilteredDemandasQuery(perfil, userUnidade?.Nome);
        var demandas = await query.ToListAsync();

        foreach (var demanda in demandas)
        {
            var entregaveis = demanda.Entregaveis ?? new List<Etapa>();
            demanda.SITUACAO = CalcularSituacaoDemanda(entregaveis);
        }

        return Ok(demandas);
    }

    private static string CalcularSituacaoDemanda(ICollection<Etapa> entregaveis)
    {
        if (!entregaveis.Any()) return "Não Iniciado";
        if (entregaveis.Any(e => e.SITUACAO == "Atrasado")) return "Atrasado";
        if (entregaveis.Any(e => e.SITUACAO == "Em Andamento")) return "Em Andamento";
        if (entregaveis.All(e => e.SITUACAO == "Concluído")) return "Concluído";
        return "Não Iniciado";
    }
}
