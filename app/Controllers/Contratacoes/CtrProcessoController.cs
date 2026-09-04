using System.Security.Claims;
using api.Contratacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Contratacoes;
using service.Interface;

namespace Controllers.Contratacoes;

/// <summary>
/// Processos de contratação de TIC em análise pela SGDI: CRUD, checkpoints,
/// export CSV e painel de indicadores. Toda action valida papel no
/// CtrPermissionService (o front é só usabilidade).
/// </summary>
[ApiController]
[Authorize]
[Route("api/contratacoes/processo")]
public class CtrProcessoController : ControllerBase
{
    private readonly ICtrProcessoService _service;
    private readonly ICtrManifestacaoService _manifestacaoService;
    private readonly ICtrPermissionService _permissionService;

    public CtrProcessoController(
        ICtrProcessoService service,
        ICtrManifestacaoService manifestacaoService,
        ICtrPermissionService permissionService)
    {
        _service = service;
        _manifestacaoService = manifestacaoService;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    private async Task<CtrUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email);
    }

    /// <summary>
    /// Lista paginada dos processos ativos, com busca e filtros
    /// </summary>
    // ARMADILHA: o parâmetro NÃO pode se chamar "filtro". O model binder de tipo
    // complexo procura primeiro o prefixo com o nome do parâmetro; achando a chave
    // "Filtro" na query (que é uma PROPRIEDADE do modelo), ele passa a exigir
    // "filtro.Filtro", "filtro.Page"... e todas as propriedades ficam no default —
    // a listagem devolvia tudo, ignorando busca, filtros e paginação.
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] CtrProcessoFiltro consulta)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Processo)) return Forbid();

        return Ok(await _service.ListarAsync(consulta));
    }

    /// <summary>
    /// Siglas já usadas nos processos ativos (autocompletar)
    /// </summary>
    [HttpGet("siglas")]
    public async Task<IActionResult> ListarSiglas()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Processo)) return Forbid();

        return Ok(await _service.ListarSiglasAsync());
    }

    /// <summary>
    /// Export CSV da listagem filtrada (UTF-8 com BOM, ";" — reabre no Excel)
    /// </summary>
    // Mesmo cuidado do Listar: o parâmetro não pode se chamar "filtro" (ver ARMADILHA acima)
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar([FromQuery] CtrProcessoFiltro consulta)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Processo)) return Forbid();

        var bytes = await _service.ExportarCsvAsync(consulta);
        return File(bytes, "text/csv; charset=utf-8", "analises-contratacoes.csv");
    }

    /// <summary>
    /// Painel de indicadores dos processos ativos
    /// </summary>
    // Rota própria (api/contratacoes/painel), fora do prefixo do controller: o painel
    // é leitura agregada dos mesmos processos e não justifica um controller só dele.
    [HttpGet("~/api/contratacoes/painel")]
    public async Task<IActionResult> Painel([FromQuery] int diasSemMovimento = 15)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Painel)) return Forbid();

        return Ok(await _service.MontarPainelAsync(diasSemMovimento));
    }

    /// <summary>
    /// Detalha um processo ativo
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Processo)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _service.GetAsync(id));
    }

    /// <summary>
    /// Cadastra um processo em análise
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CtrProcessoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Processo)) return Forbid();

        return Ok(await _service.CriarAsync(dto, ctx));
    }

    /// <summary>
    /// Edita um processo
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] CtrProcessoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Processo)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _service.AtualizarAsync(id, dto, ctx));
    }

    /// <summary>
    /// Exclui (soft delete) o processo: some das listas, a linha fica auditável
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Processo)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        await _service.ExcluirAsync(id, ctx);
        return NoContent();
    }

    /// <summary>
    /// Registra (ou limpa) a data de um checkpoint do trâmite
    /// </summary>
    [HttpPut("{id:long}/checkpoint")]
    public async Task<IActionResult> RegistrarCheckpoint(long id, [FromBody] CtrCheckpointDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Processo)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _service.RegistrarCheckpointAsync(id, dto, ctx));
    }

    /// <summary>
    /// Manifestações ao TCDF do processo, mais recente primeiro
    /// </summary>
    [HttpGet("{id:long}/manifestacoes")]
    public async Task<IActionResult> ListarManifestacoes(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, CtrResources.Manifestacao)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _manifestacaoService.ListarDoProcessoAsync(id));
    }

    /// <summary>
    /// Registra uma manifestação da SGDI ao TCDF sobre o processo
    /// </summary>
    [HttpPost("{id:long}/manifestacao")]
    public async Task<IActionResult> CriarManifestacao(long id, [FromBody] CtrManifestacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Manifestacao)) return Forbid();

        if (await _service.GetEntidadeAsync(id) == null) return NotFound();

        return Ok(await _manifestacaoService.CriarAsync(id, dto, ctx));
    }
}
