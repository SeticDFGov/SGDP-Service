using System.Security.Claims;
using System.Text.RegularExpressions;
using api.Contratacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Contratacoes;
using service.Interface;

namespace Controllers.Contratacoes;

/// <summary>
/// Manifestações da SGDI ao TCDF (despacho padrão da supervisão contínua):
/// lista geral com filtro por estágio, edição e geração do despacho em PDF.
/// A criação é pela rota do processo (POST api/contratacoes/processo/{id}/manifestacao).
/// </summary>
[ApiController]
[Authorize]
[Route("api/contratacoes/manifestacao")]
public class CtrManifestacaoController : ControllerBase
{
    private readonly ICtrManifestacaoService _service;
    private readonly ICtrPermissionService _permissionService;

    public CtrManifestacaoController(ICtrManifestacaoService service, ICtrPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;
    private string GetUserPerfil() => User.FindFirst(ClaimTypes.Role)?.Value ?? "basico";

    private async Task<CtrUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email, GetUserPerfil());
    }

    /// <summary>
    /// Lista paginada das manifestações (processos ativos), com filtro por estágio
    /// </summary>
    // ARMADILHA: o parâmetro NÃO pode se chamar "filtro". O model binder de tipo
    // complexo procura primeiro o prefixo com o nome do parâmetro; achando a chave
    // "Filtro" na query (que é uma PROPRIEDADE do modelo), ele passa a exigir
    // "filtro.Filtro", "filtro.Estagio"... e todas as propriedades ficam no default.
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] CtrManifestacaoFiltro consulta)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Manifestacao)) return Forbid();

        return Ok(await _service.ListarAsync(consulta));
    }

    /// <summary>
    /// Detalha uma manifestação
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Manifestacao)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _service.GetAsync(id));
    }

    /// <summary>
    /// Edita a manifestação (a evolução do desfecho é atualização da mesma peça)
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] CtrManifestacaoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Manifestacao)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _service.AtualizarAsync(id, dto, ctx));
    }

    /// <summary>
    /// Despacho padrão preenchido, em PDF. Local, nome e cargo são informados na
    /// hora da geração e NÃO são persistidos.
    /// </summary>
    [HttpGet("{id:long}/despacho/pdf")]
    public async Task<IActionResult> GerarDespacho(long id,
        [FromQuery] string? local, [FromQuery] string? nome, [FromQuery] string? cargo)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Manifestacao)) return Forbid();

        if (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cargo))
            return BadRequest("Informe local, nome e cargo para o fecho do despacho.");

        var manifestacao = await _service.GetEntidadeAsync(id);
        if (manifestacao == null) return NotFound();

        var bytes = await _service.GerarDespachoPdfAsync(id, local, nome, cargo);
        var numero = Regex.Replace(manifestacao.Processo?.NumeroProcesso ?? "processo", @"[^0-9A-Za-z]", "-");

        return File(bytes, "application/pdf", $"despacho-tcdf-{numero}-{id}.pdf");
    }
}
