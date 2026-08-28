using System.Security.Claims;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Pgia;

namespace Controllers.Pgia;

/// <summary>
/// Governança central do PGIA: fila de homologação do inventário, deliberações do
/// CGTIC (art. 7º), plataformas públicas de IA generativa (arts. 8º, IV, 18 e 20),
/// normas complementares (arts. 8º, III e 26) e autorizações excepcionais
/// (arts. 18, § 2º e 21). Toda ação valida papel no PgiaPermissionService.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pgia/governanca")]
public class PgiaGovernancaController : ControllerBase
{
    private readonly IPgiaGovernancaService _service;
    private readonly IPgiaSistemaService _sistemaService;
    private readonly IPgiaRelatorioService _relatorioService;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaGovernancaController(
        IPgiaGovernancaService service,
        IPgiaSistemaService sistemaService,
        IPgiaRelatorioService relatorioService,
        IPgiaPermissionService permissionService)
    {
        _service = service;
        _sistemaService = sistemaService;
        _relatorioService = relatorioService;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;

    private async Task<PgiaUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email);
    }

    /// <summary>Instâncias centrais: SGDI, CGTIC e admin veem o painel inteiro.</summary>
    private static bool EhEscopoCentral(PgiaUserContext ctx) =>
        ctx.PapelEfetivo is Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic;

    // ── Homologação do inventário ─────────────────────────────────────────────

    /// <summary>
    /// Fila de sistemas aguardando avaliação da SGDI ou deliberação do CGTIC
    /// </summary>
    [HttpGet("homologacoes")]
    public async Task<IActionResult> ListarHomologacoes([FromQuery] string? situacao)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Homologacao)) return Forbid();
        // O comitê acompanha a fila mesmo sem poder editar a avaliação da SGDI
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _sistemaService.ListarHomologacoesPendentesAsync(ctx, situacao));
    }

    /// <summary>
    /// Avaliação da SGDI sobre um sistema de risco Baixo ou Moderado (aprovar ou vetar)
    /// </summary>
    [HttpPut("sistema/{id:long}/avaliacao")]
    public async Task<IActionResult> AvaliarHomologacao(long id, [FromBody] PgiaAvaliacaoHomologacaoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Homologacao)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();

        return Ok(await _sistemaService.AvaliarHomologacaoAsync(id, dto, ctx));
    }

    /// <summary>
    /// Marca a publicação do sistema no Registro Público do Portal (art. 24)
    /// </summary>
    [HttpPut("sistema/{id:long}/registro-publico")]
    public async Task<IActionResult> AtualizarRegistroPublico(long id, [FromBody] PgiaRegistroPublicoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Publicacao)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(id);
        if (sistema == null) return NotFound();

        return Ok(await _sistemaService.AtualizarRegistroPublicoAsync(id, dto, ctx));
    }

    /// <summary>
    /// Resumo de todos os sistemas do inventário, para o comitê escolher sobre quem deliberar
    /// </summary>
    [HttpGet("sistemas")]
    public async Task<IActionResult> ListarSistemasResumo()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Sistema)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _service.ListarSistemasResumoAsync());
    }

    // ── Deliberações do CGTIC (art. 7º) ───────────────────────────────────────

    /// <summary>
    /// Deliberações do comitê (visão central)
    /// </summary>
    [HttpGet("deliberacoes")]
    public async Task<IActionResult> ListarDeliberacoes()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Deliberacao)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _service.ListarDeliberacoesAsync());
    }

    /// <summary>
    /// Deliberações que tratam de um sistema do inventário
    /// </summary>
    [HttpGet("deliberacoes/sistema/{sistemaId:long}")]
    public async Task<IActionResult> ListarDeliberacoesDoSistema(long sistemaId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Deliberacao)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(sistemaId);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _service.ListarDeliberacoesPorSistemaAsync(sistemaId));
    }

    /// <summary>
    /// Registra uma deliberação do CGTIC; sobre sistema delegado ao comitê, decide a homologação
    /// </summary>
    [HttpPost("deliberacao")]
    public async Task<IActionResult> CriarDeliberacao([FromBody] PgiaDeliberacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Deliberacao)) return Forbid();

        return Ok(await _service.CriarDeliberacaoAsync(dto, ctx));
    }

    // ── Plataformas públicas de IA generativa ─────────────────────────────────

    /// <summary>
    /// Relação de plataformas e sua situação de homologação (art. 8º, IV)
    /// </summary>
    [HttpGet("plataformas")]
    public async Task<IActionResult> ListarPlataformas()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Plataforma)) return Forbid();

        return Ok(await _service.ListarPlataformasAsync());
    }

    /// <summary>
    /// Cadastra plataforma pública de IA generativa (ato da SGDI, arts. 18 e 20)
    /// </summary>
    [HttpPost("plataforma")]
    public async Task<IActionResult> CriarPlataforma([FromBody] PgiaPlataformaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Plataforma)) return Forbid();

        return Ok(await _service.CriarPlataformaAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza a homologação de uma plataforma pública de IA generativa
    /// </summary>
    [HttpPut("plataforma/{id:long}")]
    public async Task<IActionResult> AtualizarPlataforma(long id, [FromBody] PgiaPlataformaUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Plataforma)) return Forbid();

        return Ok(await _service.AtualizarPlataformaAsync(id, dto, ctx));
    }

    // ── Normas complementares ─────────────────────────────────────────────────

    /// <summary>
    /// Normas complementares publicadas pela SGDI e pelo CGTIC (arts. 8º, III e 26)
    /// </summary>
    [HttpGet("normas")]
    public async Task<IActionResult> ListarNormas()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Norma)) return Forbid();

        return Ok(await _service.ListarNormasAsync());
    }

    /// <summary>
    /// Publica uma norma complementar
    /// </summary>
    [HttpPost("norma")]
    public async Task<IActionResult> CriarNorma([FromBody] PgiaNormaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Norma)) return Forbid();

        return Ok(await _service.CriarNormaAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza uma norma complementar (inclusive a vigência)
    /// </summary>
    [HttpPut("norma/{id:long}")]
    public async Task<IActionResult> AtualizarNorma(long id, [FromBody] PgiaNormaUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Norma)) return Forbid();

        return Ok(await _service.AtualizarNormaAsync(id, dto, ctx));
    }

    // ── Autorizações excepcionais ─────────────────────────────────────────────

    /// <summary>
    /// Autorizações excepcionais (arts. 18, § 2º e 21); o órgão vê apenas as suas
    /// </summary>
    [HttpGet("autorizacoes")]
    public async Task<IActionResult> ListarAutorizacoes()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Autorizacao)) return Forbid();

        if (EhEscopoCentral(ctx))
            return Ok(await _service.ListarAutorizacoesAsync(null));

        // Papel de órgão sem órgão vinculado não tem autorização alguma para ver
        if (ctx.OrgaoId == null)
            return Ok(new List<PgiaAutorizacaoResponse>());

        return Ok(await _service.ListarAutorizacoesAsync(ctx.OrgaoId));
    }

    /// <summary>
    /// Registra uma autorização excepcional
    /// </summary>
    [HttpPost("autorizacao")]
    public async Task<IActionResult> CriarAutorizacao([FromBody] PgiaAutorizacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Autorizacao)) return Forbid();

        return Ok(await _service.CriarAutorizacaoAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza uma autorização excepcional, inclusive para revogá-la (Ativo = false)
    /// </summary>
    [HttpPut("autorizacao/{id:long}")]
    public async Task<IActionResult> AtualizarAutorizacao(long id, [FromBody] PgiaAutorizacaoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Autorizacao)) return Forbid();

        return Ok(await _service.AtualizarAutorizacaoAsync(id, dto, ctx));
    }

    // ── Relatório Anual de Governança (arts. 7º, VII e 33) ────────────────────

    /// <summary>
    /// Relatórios anuais publicados pela SGDI (art. 33)
    /// </summary>
    [HttpGet("relatorio-anual")]
    public async Task<IActionResult> ListarRelatoriosAnuais()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Relatorio)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.ListarRelatoriosAnuaisAsync());
    }

    /// <summary>
    /// Abre o Relatório Anual de Governança de IA do exercício
    /// </summary>
    [HttpPost("relatorio-anual")]
    public async Task<IActionResult> CriarRelatorioAnual([FromBody] PgiaRelatorioAnualCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Relatorio)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.CriarRelatorioAnualAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza o Relatório Anual (publicação, apreciação do CGTIC, recomendações)
    /// </summary>
    [HttpPut("relatorio-anual/{id:long}")]
    public async Task<IActionResult> AtualizarRelatorioAnual(long id, [FromBody] PgiaRelatorioAnualUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Relatorio)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.AtualizarRelatorioAnualAsync(id, dto, ctx));
    }

    // ── Auditorias técnicas (arts. 25, § 2º e 34) ─────────────────────────────

    /// <summary>
    /// Designa uma auditoria técnica sobre um sistema (art. 34)
    /// </summary>
    [HttpPost("auditorias")]
    public async Task<IActionResult> CriarAuditoria([FromBody] PgiaAuditoriaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();
        // A designação é ato do órgão central, não da entidade auditora
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.CriarAuditoriaAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza os dados da designação da auditoria
    /// </summary>
    [HttpPut("auditorias/{id:long}")]
    public async Task<IActionResult> AtualizarAuditoria(long id, [FromBody] PgiaAuditoriaCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        var auditoria = await _relatorioService.GetAuditoriaEntidadeAsync(id);
        if (auditoria == null) return NotFound();

        return Ok(await _relatorioService.AtualizarAuditoriaAsync(id, dto, ctx));
    }

    /// <summary>
    /// Registra o parecer e as datas dos trabalhos (entidade designada ou órgão central)
    /// </summary>
    [HttpPut("auditorias/{id:long}/parecer")]
    public async Task<IActionResult> RegistrarParecerAuditoria(long id, [FromBody] PgiaAuditoriaParecerDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();

        var auditoria = await _relatorioService.GetAuditoriaEntidadeAsync(id);
        if (auditoria == null) return NotFound();

        // A restrição fina (designada a quem?) fica no service
        return Ok(await _relatorioService.RegistrarParecerAsync(id, dto, ctx));
    }

    /// <summary>
    /// Publica o resultado da auditoria no Portal da Transparência (art. 34)
    /// </summary>
    [HttpPut("auditorias/{id:long}/publicacao")]
    public async Task<IActionResult> PublicarAuditoria(long id, [FromBody] PgiaAuditoriaPublicacaoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Publicacao)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        var auditoria = await _relatorioService.GetAuditoriaEntidadeAsync(id);
        if (auditoria == null) return NotFound();

        return Ok(await _relatorioService.PublicarAuditoriaAsync(id, dto, ctx));
    }

    /// <summary>
    /// Auditorias designadas à entidade auditora autenticada
    /// </summary>
    [HttpGet("auditorias/minhas")]
    public async Task<IActionResult> ListarMinhasAuditorias()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();

        return Ok(await _relatorioService.ListarMinhasAuditoriasAsync(ctx));
    }

    /// <summary>
    /// Todas as auditorias técnicas (visão central)
    /// </summary>
    [HttpGet("auditorias")]
    public async Task<IActionResult> ListarAuditorias()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.ListarAuditoriasAsync());
    }

    /// <summary>
    /// Auditorias técnicas de um sistema (órgão do sistema e instâncias centrais)
    /// </summary>
    [HttpGet("auditorias/sistema/{sistemaId:long}")]
    public async Task<IActionResult> ListarAuditoriasDoSistema(long sistemaId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();

        var sistema = await _service.GetSistemaEntidadeAsync(sistemaId);
        if (sistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();

        return Ok(await _relatorioService.ListarAuditoriasPorSistemaAsync(sistemaId));
    }

    /// <summary>
    /// Usuários com papel de auditoria externa, para a SGDI designar
    /// </summary>
    [HttpGet("auditores")]
    public async Task<IActionResult> ListarAuditores()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.AuditoriaTecnica)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _relatorioService.ListarAuditoresAsync());
    }
}
