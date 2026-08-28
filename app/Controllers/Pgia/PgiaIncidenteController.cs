using System.Security.Claims;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Pgia;

namespace Controllers.Pgia;

/// <summary>
/// Incidentes graves: aviso do agente ao Responsável de IA (art. 13, II),
/// comunicação formal à SGDI e apuração (arts. 8º, IX e 30).
/// </summary>
[ApiController]
[Authorize]
[Route("api/pgia/incidente")]
public class PgiaIncidenteController : ControllerBase
{
    private readonly IPgiaOperacaoService _service;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaIncidenteController(IPgiaOperacaoService service, IPgiaPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    private async Task<PgiaUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email);
    }

    private static bool EhEscopoCentral(PgiaUserContext ctx) =>
        ctx.PapelEfetivo is Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic;

    /// <summary>
    /// Aviso de problema ao Responsável de IA, aberto a qualquer agente do órgão (art. 13, II)
    /// </summary>
    [HttpPost("aviso")]
    public async Task<IActionResult> CriarAviso([FromBody] PgiaIncidenteAvisoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        // Sem exigir papel PGIA: o dever do art. 13 é de todo agente público
        if (!_permissionService.PodeRegistrarUso(ctx)) return Forbid();

        return Ok(await _service.CriarAvisoAsync(dto, ctx));
    }

    /// <summary>
    /// Registro formal do incidente pelo Responsável de IA, já comunicado à SGDI (art. 30)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CriarIncidente([FromBody] PgiaIncidenteCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Incidente)) return Forbid();

        var orgaoDoSistema = await _service.GetSistemaOrgaoAsync(dto.SistemaIaId);
        if (orgaoDoSistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoDoSistema.Value)) return Forbid();

        return Ok(await _service.CriarIncidenteFormalAsync(dto, ctx));
    }

    /// <summary>
    /// Completa a comunicação formal de um aviso já registrado (hipótese, SEI e data)
    /// </summary>
    [HttpPut("{id:long}/comunicar")]
    public async Task<IActionResult> Comunicar(long id, [FromBody] PgiaIncidenteComunicarDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Incidente)) return Forbid();

        var incidente = await _service.GetIncidenteEntidadeAsync(id);
        if (incidente == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, incidente.OrgaoId)) return Forbid();

        return Ok(await _service.ComunicarAsync(id, dto, ctx));
    }

    /// <summary>
    /// Atualiza as medidas adotadas pelo órgão (compõem o relatório semestral, art. 32, II)
    /// </summary>
    [HttpPut("{id:long}/medidas")]
    public async Task<IActionResult> AtualizarMedidas(long id, [FromBody] PgiaIncidenteMedidasDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Incidente)) return Forbid();

        var incidente = await _service.GetIncidenteEntidadeAsync(id);
        if (incidente == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, incidente.OrgaoId)) return Forbid();

        return Ok(await _service.AtualizarMedidasAsync(id, dto, ctx));
    }

    /// <summary>
    /// Apuração do incidente pela SGDI (art. 30, § único)
    /// </summary>
    [HttpPut("{id:long}/apuracao")]
    public async Task<IActionResult> Apurar(long id, [FromBody] PgiaIncidenteApuracaoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Apuracao)) return Forbid();

        var incidente = await _service.GetIncidenteEntidadeAsync(id);
        if (incidente == null) return NotFound();

        return Ok(await _service.ApurarAsync(id, dto, ctx));
    }

    /// <summary>
    /// Incidentes do órgão, comunicados ou não
    /// </summary>
    [HttpGet("orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarPorOrgao(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Incidente)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarIncidentesPorOrgaoAsync(orgaoId));
    }

    /// <summary>
    /// Fila de incidentes comunicados à SGDI, opcionalmente por situação da apuração
    /// </summary>
    [HttpGet("comunicados")]
    public async Task<IActionResult> ListarComunicados([FromQuery] string? status)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Incidente)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _service.ListarComunicadosAsync(status));
    }
}
