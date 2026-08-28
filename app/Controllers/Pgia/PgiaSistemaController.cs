using System.Security.Claims;
using api.Common;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Pgia;

namespace Controllers.Pgia;

/// <summary>
/// Inventário de sistemas de IA, classificação de risco e documentos vinculados
/// (etapa 2 do formulário; arts. 9º, I, 14 a 20, 24 e 27). Toda ação valida papel
/// e escopo de órgão no PgiaPermissionService.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pgia/sistema")]
public class PgiaSistemaController : ControllerBase
{
    private readonly IPgiaSistemaService _service;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaSistemaController(IPgiaSistemaService service, IPgiaPermissionService permissionService)
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

    /// <summary>
    /// Lista paginada dos sistemas de IA do órgão (art. 8º, II)
    /// </summary>
    [HttpGet("orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarSistemas(long orgaoId, [FromQuery] PagedRequest request)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Sistema)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarSistemasAsync(ctx, orgaoId, request));
    }

    /// <summary>
    /// Detalha um sistema do inventário
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetSistema(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Sistema)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.GetSistemaAsync(id));
    }

    /// <summary>
    /// Cadastra um sistema no inventário do órgão, com a classificação inicial de risco
    /// </summary>
    [HttpPost("orgao/{orgaoId:long}")]
    public async Task<IActionResult> CriarSistema(long orgaoId, [FromBody] PgiaSistemaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Sistema)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.CriarSistemaAsync(orgaoId, dto, ctx));
    }

    /// <summary>
    /// Atualiza os dados de inventário do sistema (a reclassificação é endpoint próprio)
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> AtualizarSistema(long id, [FromBody] PgiaSistemaUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Sistema)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.AtualizarSistemaAsync(id, dto, ctx));
    }

    /// <summary>
    /// Reclassifica o risco do sistema, preservando o histórico (arts. 14 a 18)
    /// </summary>
    [HttpPost("{id:long}/classificacao")]
    public async Task<IActionResult> Reclassificar(long id, [FromBody] PgiaClassificacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Classificacao)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.ReclassificarAsync(id, dto, ctx));
    }

    /// <summary>
    /// Histórico de classificações de risco do sistema
    /// </summary>
    [HttpGet("{id:long}/classificacoes")]
    public async Task<IActionResult> ListarClassificacoes(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Classificacao)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        // A pontuação dos quesitos é métrica de acompanhamento do órgão central:
        // o órgão preenche o checklist, mas não enxerga o número.
        var incluirPontuacao = ctx.PapelEfetivo is Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic;

        return Ok(await _service.ListarClassificacoesAsync(id, incluirPontuacao));
    }

    /// <summary>
    /// Documentos vinculados ao sistema (metadados e nº do processo SEI)
    /// </summary>
    [HttpGet("{id:long}/documentos")]
    public async Task<IActionResult> ListarDocumentos(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Documento)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.ListarDocumentosAsync(id));
    }

    /// <summary>
    /// Registra os metadados de um documento do sistema (arts. 9º, III, 30 e 32)
    /// </summary>
    [HttpPost("{id:long}/documentos")]
    public async Task<IActionResult> CriarDocumento(long id, [FromBody] PgiaDocumentoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Documento)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.CriarDocumentoAsync(id, dto, ctx));
    }

    // ── AIA (art. 22) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Avaliações de Impacto Algorítmico do sistema (art. 22)
    /// </summary>
    [HttpGet("{id:long}/aia")]
    public async Task<IActionResult> ListarAias(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Aia)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.ListarAiasAsync(id));
    }

    /// <summary>
    /// Registra a AIA do sistema, elaborada pelo órgão (arts. 2º, VII e 22)
    /// </summary>
    [HttpPost("{id:long}/aia")]
    public async Task<IActionResult> CriarAia(long id, [FromBody] PgiaAiaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Aia)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.CriarAiaAsync(id, dto, ctx));
    }

    /// <summary>
    /// Atualiza a AIA (a publicação e o vínculo com deliberação são atos centrais)
    /// </summary>
    [HttpPut("aia/{aiaId:long}")]
    public async Task<IActionResult> AtualizarAia(long aiaId, [FromBody] PgiaAiaUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Aia)) return Forbid();

        var aia = await _service.GetAiaEntidadeAsync(aiaId);
        if (aia == null) return NotFound();
        if (aia.Sistema == null || !_permissionService.CanAccessOrgao(ctx, aia.Sistema.OrgaoId)) return Forbid();

        return Ok(await _service.AtualizarAiaAsync(aiaId, dto, ctx));
    }

    /// <summary>
    /// Marca a publicação do resultado da AIA no Portal da Transparência (arts. 16, § 1º e 24, IV)
    /// </summary>
    [HttpPut("aia/{aiaId:long}/publicacao")]
    public async Task<IActionResult> PublicarAia(long aiaId, [FromBody] PgiaAiaPublicacaoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Publicacao)) return Forbid();

        var aia = await _service.GetAiaEntidadeAsync(aiaId);
        if (aia == null) return NotFound();
        if (aia.Sistema == null || !_permissionService.CanAccessOrgao(ctx, aia.Sistema.OrgaoId)) return Forbid();

        return Ok(await _service.PublicarAiaAsync(aiaId, dto, ctx));
    }

    /// <summary>
    /// Vincula a AIA à deliberação do CGTIC que autoriza a implantação (art. 16, § 1º)
    /// </summary>
    [HttpPut("aia/{aiaId:long}/deliberacao/{deliberacaoId:long}")]
    public async Task<IActionResult> VincularDeliberacaoAia(long aiaId, long deliberacaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Publicacao)) return Forbid();

        var aia = await _service.GetAiaEntidadeAsync(aiaId);
        if (aia == null) return NotFound();
        if (aia.Sistema == null || !_permissionService.CanAccessOrgao(ctx, aia.Sistema.OrgaoId)) return Forbid();

        return Ok(await _service.VincularDeliberacaoAiaAsync(aiaId, deliberacaoId, ctx));
    }
}
