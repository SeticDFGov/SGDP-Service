using System.Security.Claims;
using api.Acesso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service;
using service.Interface;

namespace Controllers;

/// <summary>
/// Gestão de acessos por módulo (isolamento entre módulos): quem enxerga Demandas,
/// PGIA, Supervisão Contínua das Contratações e Governança Estratégica. Restrito ao
/// perfil admin do SGDP. O que vem do Keycloak (roles admin, gestor e "pgia" do grupo)
/// aparece só para leitura, porque é gerido lá; aqui se concede e se retira o que é do
/// sistema.
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/acessos")]
public class AcessoController : ControllerBase
{
    private readonly IAcessoModuloService _service;

    public AcessoController(IAcessoModuloService service)
    {
        _service = service;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Lista paginada de usuários com os acessos de cada um. Filtros: parte do nome
    /// ou do e-mail e módulo ("nenhum" = sem acesso a módulo algum).
    /// </summary>
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios([FromQuery] AcessoUsuariosConsulta consulta)
    {
        return Ok(await _service.ListarUsuariosAsync(consulta));
    }

    /// <summary>Acessos de um usuário.</summary>
    [HttpGet("usuario/{userId:guid}")]
    public async Task<IActionResult> ObterUsuario(Guid userId)
    {
        try
        {
            return Ok(await _service.ObterUsuarioAsync(userId));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    /// <summary>
    /// Grava de uma vez os acessos do usuário concedidos no sistema: Demandas, PGIA
    /// (com o papel dentro do módulo), Supervisão Contínua das Contratações e, quando
    /// vierem os campos opcionais, Governança Estratégica (com o papel do módulo).
    /// </summary>
    [HttpPut("usuario/{userId:guid}")]
    public async Task<IActionResult> DefinirAcessos(Guid userId, [FromBody] AcessoUsuarioUpdateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        try
        {
            return Ok(await _service.DefinirAcessosAsync(userId, dto, email));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    // Tela nova: erro de regra volta com código e mensagem (400/404), não como o
    // 500 sem corpo herdado dos demais controllers
    private IActionResult Erro(ApiException ex) =>
        ex.Error.Code == (int)ErrorCode.AcessoUsuarioNaoEncontrado
            ? NotFound(new { ex.Error.Code, ex.Error.Message })
            : BadRequest(new { ex.Error.Code, ex.Error.Message });
}
