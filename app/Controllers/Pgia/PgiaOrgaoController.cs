using System.Security.Claims;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Pgia;

namespace Controllers.Pgia;

/// <summary>
/// Área do órgão no PGIA: dados do órgão, pessoas, designações (etapa 1 do
/// formulário) e painel de prazos. Toda ação valida papel e escopo de órgão
/// no PgiaPermissionService.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pgia/orgao")]
public class PgiaOrgaoController : ControllerBase
{
    private readonly IPgiaOrgaoService _service;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaOrgaoController(IPgiaOrgaoService service, IPgiaPermissionService permissionService)
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
    /// Órgão vinculado à unidade do usuário, com as designações vigentes
    /// </summary>
    [HttpGet("meu-orgao")]
    public async Task<IActionResult> GetMeuOrgao()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        // Qualquer papel PGIA (ou admin) pode ver o contexto do próprio órgão;
        // devolve Orgao nulo quando a unidade do usuário não tem órgão vinculado.
        if (string.IsNullOrEmpty(ctx.PapelEfetivo)) return Forbid();

        return Ok(await _service.GetMeuOrgaoAsync(ctx));
    }

    /// <summary>
    /// Órgãos visíveis ao usuário (todos para SGDI/CGTIC/admin; o próprio para o órgão)
    /// </summary>
    [HttpGet("orgaos")]
    public async Task<IActionResult> ListarOrgaos()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Orgao)) return Forbid();

        return Ok(await _service.ListarOrgaosAsync(ctx));
    }

    /// <summary>
    /// Atualiza os dados do órgão (etapa 1: identificação do órgão ou entidade)
    /// </summary>
    [HttpPut("{orgaoId:long}/dados")]
    public async Task<IActionResult> AtualizarDados(long orgaoId, [FromBody] PgiaOrgaoDadosDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.OrgaoDados)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.AtualizarDadosOrgaoAsync(orgaoId, dto, ctx.Email));
    }

    /// <summary>
    /// Pessoas do órgão (usuários da unidade vinculada, com dados de agente público)
    /// </summary>
    [HttpGet("{orgaoId:long}/pessoas")]
    public async Task<IActionResult> ListarPessoas(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.AgenteInfo)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarPessoasAsync(orgaoId));
    }

    /// <summary>
    /// Salva matrícula, cargo e vínculo de uma pessoa do órgão (art. 3º)
    /// </summary>
    [HttpPut("{orgaoId:long}/pessoas/{userId:guid}/info")]
    public async Task<IActionResult> SalvarAgenteInfo(long orgaoId, Guid userId, [FromBody] PgiaAgenteInfoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.AgenteInfo)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        await _service.SalvarAgenteInfoAsync(orgaoId, userId, dto, ctx.Email);
        return Ok();
    }

    /// <summary>
    /// Histórico de designações de Responsável de IA do órgão (art. 10)
    /// </summary>
    [HttpGet("{orgaoId:long}/responsavel-ia")]
    public async Task<IActionResult> ListarResponsaveisIa(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Designacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarResponsaveisIaAsync(orgaoId));
    }

    /// <summary>
    /// Designa o Responsável de IA do órgão; a designação anterior fica no histórico
    /// </summary>
    [HttpPost("{orgaoId:long}/responsavel-ia")]
    public async Task<IActionResult> DesignarResponsavelIa(long orgaoId, [FromBody] PgiaResponsavelIaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Designacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.DesignarResponsavelIaAsync(orgaoId, dto, ctx.Email));
    }

    /// <summary>
    /// Histórico de designações de Encarregado de Dados do órgão (art. 11)
    /// </summary>
    [HttpGet("{orgaoId:long}/encarregado-dados")]
    public async Task<IActionResult> ListarEncarregadosDados(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Designacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarEncarregadosDadosAsync(orgaoId));
    }

    /// <summary>
    /// Designa o Encarregado de Dados (DPO) do órgão
    /// </summary>
    [HttpPost("{orgaoId:long}/encarregado-dados")]
    public async Task<IActionResult> DesignarEncarregadoDados(long orgaoId, [FromBody] PgiaDesignacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Designacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.DesignarEncarregadoDadosAsync(orgaoId, dto, ctx.Email));
    }

    /// <summary>
    /// Painel de prazos de conformidade do órgão (arts. 26, § 3º, 35 e 37)
    /// </summary>
    [HttpGet("{orgaoId:long}/prazos")]
    public async Task<IActionResult> ListarPrazos(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Prazo)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarPrazosOrgaoAsync(orgaoId));
    }

    /// <summary>
    /// Obrigações centrais (linhas-modelo e obrigações da SGDI), visíveis a SGDI/CGTIC/admin
    /// </summary>
    [HttpGet("prazos/centrais")]
    public async Task<IActionResult> ListarPrazosCentrais()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Prazo)) return Forbid();
        // Escopo central: só quem enxerga todos os órgãos
        if (ctx.PapelEfetivo is not (Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic)) return Forbid();

        return Ok(await _service.ListarPrazosCentraisAsync());
    }

    /// <summary>
    /// Marca (ou desmarca) o cumprimento de uma obrigação com prazo
    /// </summary>
    [HttpPut("prazos/{prazoId:long}/cumprimento")]
    public async Task<IActionResult> MarcarCumprimento(long prazoId, [FromBody] PgiaMarcarCumprimentoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Prazo)) return Forbid();

        var prazo = await _service.GetPrazoAsync(prazoId);
        if (prazo == null) return NotFound();

        // Obrigações centrais (orgao_id nulo) só pela SGDI ou admin;
        // as do órgão, por quem tem acesso àquele órgão.
        if (prazo.OrgaoId == null)
        {
            if (ctx.PapelEfetivo is not (Perfis.Admin or PapeisPgia.Sgdi)) return Forbid();
        }
        else if (!_permissionService.CanAccessOrgao(ctx, prazo.OrgaoId.Value))
        {
            return Forbid();
        }

        return Ok(await _service.MarcarCumprimentoAsync(prazoId, dto, ctx.Email));
    }
}
