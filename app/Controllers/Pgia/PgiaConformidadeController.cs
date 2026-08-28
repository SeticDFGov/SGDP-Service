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
/// Conformidade e operação contínua: não conformidades (arts. 8º, X e 9º, II),
/// trilhas ProCapIA/DF (arts. 28 e 29) e registro de uso de IA (art. 13, IV e V).
/// </summary>
[ApiController]
[Authorize]
[Route("api/pgia/conformidade")]
public class PgiaConformidadeController : ControllerBase
{
    private readonly IPgiaOperacaoService _service;
    private readonly IPgiaContratoService _contratoService;
    private readonly IPgiaRelatorioService _relatorioService;
    private readonly IPgiaPublicoService _publicoService;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaConformidadeController(
        IPgiaOperacaoService service,
        IPgiaContratoService contratoService,
        IPgiaRelatorioService relatorioService,
        IPgiaPublicoService publicoService,
        IPgiaPermissionService permissionService)
    {
        _service = service;
        _contratoService = contratoService;
        _relatorioService = relatorioService;
        _publicoService = publicoService;
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

    // ── Não conformidades ─────────────────────────────────────────────────────

    /// <summary>
    /// Registra uma não conformidade (o órgão registra as suas; a SGDI, as que supervisiona)
    /// </summary>
    [HttpPost("nao-conformidade")]
    public async Task<IActionResult> CriarNaoConformidade([FromBody] PgiaNaoConformidadeCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.NaoConformidade)) return Forbid();

        return Ok(await _service.CriarNaoConformidadeAsync(dto, ctx));
    }

    /// <summary>
    /// Atualiza a situação e o tratamento de uma não conformidade
    /// </summary>
    [HttpPut("nao-conformidade/{id:long}")]
    public async Task<IActionResult> AtualizarNaoConformidade(long id, [FromBody] PgiaNaoConformidadeUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.NaoConformidade)) return Forbid();

        var naoConformidade = await _service.GetNaoConformidadeEntidadeAsync(id);
        if (naoConformidade == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, naoConformidade.OrgaoId)) return Forbid();

        return Ok(await _service.AtualizarNaoConformidadeAsync(id, dto, ctx));
    }

    /// <summary>
    /// Não conformidades do órgão
    /// </summary>
    [HttpGet("nao-conformidade/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarNaoConformidadesPorOrgao(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.NaoConformidade)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarNaoConformidadesPorOrgaoAsync(orgaoId));
    }

    /// <summary>
    /// Todas as não conformidades (visão central de supervisão)
    /// </summary>
    [HttpGet("nao-conformidade/todas")]
    public async Task<IActionResult> ListarNaoConformidades()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.NaoConformidade)) return Forbid();
        if (!EhEscopoCentral(ctx)) return Forbid();

        return Ok(await _service.ListarNaoConformidadesAsync());
    }

    // ── Capacitação ProCapIA/DF ───────────────────────────────────────────────

    /// <summary>
    /// Registra a trilha de capacitação de uma pessoa do órgão (art. 29)
    /// </summary>
    [HttpPost("orgao/{orgaoId:long}/capacitacao")]
    public async Task<IActionResult> CriarCapacitacao(long orgaoId, [FromBody] PgiaCapacitacaoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Capacitacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.CriarCapacitacaoAsync(orgaoId, dto, ctx));
    }

    /// <summary>
    /// Atualiza a situação da trilha (agente e trilha identificam o registro e não mudam)
    /// </summary>
    [HttpPut("capacitacao/{id:long}")]
    public async Task<IActionResult> AtualizarCapacitacao(long id, [FromBody] PgiaCapacitacaoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Capacitacao)) return Forbid();

        var capacitacao = await _service.GetCapacitacaoEntidadeAsync(id);
        if (capacitacao == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, capacitacao.OrgaoId)) return Forbid();

        return Ok(await _service.AtualizarCapacitacaoAsync(id, dto, ctx));
    }

    /// <summary>
    /// Trilhas registradas no órgão (art. 32, IV)
    /// </summary>
    [HttpGet("orgao/{orgaoId:long}/capacitacoes")]
    public async Task<IActionResult> ListarCapacitacoes(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Capacitacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarCapacitacoesPorOrgaoAsync(orgaoId));
    }

    // ── Registro de uso de IA ─────────────────────────────────────────────────

    /// <summary>
    /// Registra o uso de IA em um produto, documento ou atividade (art. 13, IV e V)
    /// </summary>
    [HttpPost("uso")]
    public async Task<IActionResult> CriarUso([FromBody] PgiaRegistroUsoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        // Dever de todo agente público: não exige papel PGIA
        if (!_permissionService.PodeRegistrarUso(ctx)) return Forbid();

        return Ok(await _service.CriarUsoAsync(dto, ctx));
    }

    /// <summary>
    /// Sistemas do órgão e plataformas homologadas para os campos de escolha do
    /// registro de uso e do aviso de incidente (art. 13)
    /// </summary>
    [HttpGet("uso/fontes")]
    public async Task<IActionResult> ListarFontesDeUso()
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        // Mesmo alcance do POST uso: dever de todo agente público, sem papel PGIA
        if (!_permissionService.PodeRegistrarUso(ctx)) return Forbid();

        return Ok(await _service.ListarFontesDeUsoAsync(ctx));
    }

    /// <summary>
    /// Registros de uso do próprio agente
    /// </summary>
    [HttpGet("uso/meus")]
    public async Task<IActionResult> ListarMeusUsos([FromQuery] PagedRequest request)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.PodeRegistrarUso(ctx)) return Forbid();

        return Ok(await _service.ListarMeusUsosAsync(ctx, request));
    }

    /// <summary>
    /// Registros de uso do órgão (rastreabilidade e supervisão administrativa)
    /// </summary>
    [HttpGet("uso/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarUsosPorOrgao(long orgaoId, [FromQuery] PagedRequest request)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.RegistroUso)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _service.ListarUsosPorOrgaoAsync(orgaoId, request));
    }

    // ── Contratos de IA (arts. 21, 25 a 27) ───────────────────────────────────

    /// <summary>
    /// Registra um contrato de aquisição de IA com os requisitos obrigatórios (art. 25)
    /// </summary>
    [HttpPost("orgao/{orgaoId:long}/contrato")]
    public async Task<IActionResult> CriarContrato(long orgaoId, [FromBody] PgiaContratoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Contrato)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _contratoService.CriarContratoAsync(orgaoId, dto, ctx));
    }

    /// <summary>
    /// Atualiza um contrato de IA
    /// </summary>
    [HttpPut("contrato/{id:long}")]
    public async Task<IActionResult> AtualizarContrato(long id, [FromBody] PgiaContratoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Contrato)) return Forbid();

        var contrato = await _contratoService.GetContratoEntidadeAsync(id);
        if (contrato == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, contrato.OrgaoId)) return Forbid();

        return Ok(await _contratoService.AtualizarContratoAsync(id, dto, ctx));
    }

    /// <summary>
    /// Detalha um contrato de IA
    /// </summary>
    [HttpGet("contrato/{id:long}")]
    public async Task<IActionResult> GetContrato(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Contrato)) return Forbid();

        var contrato = await _contratoService.GetContratoEntidadeAsync(id);
        if (contrato == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, contrato.OrgaoId)) return Forbid();

        return Ok(await _contratoService.GetContratoAsync(id));
    }

    /// <summary>
    /// Contratos de IA do órgão
    /// </summary>
    [HttpGet("contrato/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarContratos(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Contrato)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _contratoService.ListarContratosPorOrgaoAsync(orgaoId));
    }

    // ── Instrumentos anteriores ao decreto (art. 37) ──────────────────────────

    /// <summary>
    /// Registra a triagem de um contrato ou ato anterior ao decreto (art. 37)
    /// </summary>
    [HttpPost("orgao/{orgaoId:long}/legado")]
    public async Task<IActionResult> CriarLegado(long orgaoId, [FromBody] PgiaLegadoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Legado)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _contratoService.CriarLegadoAsync(orgaoId, dto, ctx));
    }

    /// <summary>
    /// Atualiza a triagem ou a revisão de um instrumento anterior ao decreto
    /// </summary>
    [HttpPut("legado/{id:long}")]
    public async Task<IActionResult> AtualizarLegado(long id, [FromBody] PgiaLegadoUpdateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Legado)) return Forbid();

        var legado = await _contratoService.GetLegadoEntidadeAsync(id);
        if (legado == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, legado.OrgaoId)) return Forbid();

        return Ok(await _contratoService.AtualizarLegadoAsync(id, dto, ctx));
    }

    /// <summary>
    /// Instrumentos anteriores ao decreto triados pelo órgão
    /// </summary>
    [HttpGet("legado/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarLegados(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Legado)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _contratoService.ListarLegadosPorOrgaoAsync(orgaoId));
    }

    // ── Indicadores de desempenho (arts. 24, V, 31 e 32, III) ─────────────────

    /// <summary>
    /// Registra um indicador de desempenho do sistema (art. 31)
    /// </summary>
    [HttpPost("sistema/{sistemaId:long}/indicadores")]
    public async Task<IActionResult> CriarIndicador(long sistemaId, [FromBody] PgiaIndicadorCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Indicador)) return Forbid();

        var orgaoDoSistema = await _service.GetSistemaOrgaoAsync(sistemaId);
        if (orgaoDoSistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoDoSistema.Value)) return Forbid();

        return Ok(await _relatorioService.CriarIndicadorAsync(sistemaId, dto, ctx));
    }

    /// <summary>
    /// Atualiza um indicador de desempenho
    /// </summary>
    [HttpPut("indicador/{id:long}")]
    public async Task<IActionResult> AtualizarIndicador(long id, [FromBody] PgiaIndicadorCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Indicador)) return Forbid();

        var indicador = await _relatorioService.GetIndicadorEntidadeAsync(id);
        if (indicador == null) return NotFound();
        if (indicador.Sistema == null || !_permissionService.CanAccessOrgao(ctx, indicador.Sistema.OrgaoId))
            return Forbid();

        return Ok(await _relatorioService.AtualizarIndicadorAsync(id, dto, ctx));
    }

    /// <summary>
    /// Indicadores de desempenho do sistema
    /// </summary>
    [HttpGet("sistema/{sistemaId:long}/indicadores")]
    public async Task<IActionResult> ListarIndicadores(long sistemaId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Indicador)) return Forbid();

        var orgaoDoSistema = await _service.GetSistemaOrgaoAsync(sistemaId);
        if (orgaoDoSistema == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoDoSistema.Value)) return Forbid();

        return Ok(await _relatorioService.ListarIndicadoresPorSistemaAsync(sistemaId));
    }

    // ── Relatório semestral (art. 32) ─────────────────────────────────────────

    /// <summary>
    /// Abre o relatório semestral do órgão, com o prazo de envio calculado
    /// </summary>
    [HttpPost("orgao/{orgaoId:long}/relatorio-semestral")]
    public async Task<IActionResult> CriarRelatorioSemestral(long orgaoId, [FromBody] PgiaRelatorioSemestralCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Relatorio)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _relatorioService.CriarRelatorioSemestralAsync(orgaoId, dto, ctx));
    }

    /// <summary>
    /// Relatórios semestrais do órgão (sem o conteúdo agregado)
    /// </summary>
    [HttpGet("relatorio-semestral/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarRelatoriosSemestrais(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Relatorio)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _relatorioService.ListarRelatoriosSemestraisAsync(orgaoId));
    }

    /// <summary>
    /// Relatório semestral com o conteúdo agregado do período (incisos I a V)
    /// </summary>
    [HttpGet("relatorio-semestral/{id:long}")]
    public async Task<IActionResult> GetRelatorioSemestral(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Relatorio)) return Forbid();

        var relatorio = await _relatorioService.GetRelatorioSemestralEntidadeAsync(id);
        if (relatorio == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, relatorio.OrgaoId)) return Forbid();

        return Ok(await _relatorioService.GetRelatorioSemestralAsync(id));
    }

    /// <summary>
    /// Registra o envio do relatório à SGDI pelo SEI; o prazo define no prazo ou em atraso
    /// </summary>
    [HttpPut("relatorio-semestral/{id:long}/envio")]
    public async Task<IActionResult> RegistrarEnvioRelatorio(long id, [FromBody] PgiaRelatorioEnvioDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Relatorio)) return Forbid();

        var relatorio = await _relatorioService.GetRelatorioSemestralEntidadeAsync(id);
        if (relatorio == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, relatorio.OrgaoId)) return Forbid();

        return Ok(await _relatorioService.RegistrarEnvioAsync(id, dto, ctx));
    }

    /// <summary>
    /// Atos da SGDI sobre o relatório: inadimplência, painel e controle interno (art. 32, § único)
    /// </summary>
    [HttpPut("relatorio-semestral/{id:long}/situacao")]
    public async Task<IActionResult> AtualizarSituacaoRelatorio(long id, [FromBody] PgiaRelatorioSituacaoDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Relatorio)) return Forbid();
        // Situação é ato do órgão central, não do órgão que presta contas
        if (!EhEscopoCentral(ctx)) return Forbid();

        var relatorio = await _relatorioService.GetRelatorioSemestralEntidadeAsync(id);
        if (relatorio == null) return NotFound();

        return Ok(await _relatorioService.AtualizarSituacaoAsync(id, dto, ctx));
    }

    /// <summary>
    /// PDF do relatório semestral para juntada ao processo SEI
    /// </summary>
    [HttpGet("relatorio-semestral/{id:long}/pdf")]
    public async Task<IActionResult> GerarPdfRelatorioSemestral(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Relatorio)) return Forbid();

        var relatorio = await _relatorioService.GetRelatorioSemestralEntidadeAsync(id);
        if (relatorio == null) return NotFound();
        if (!_permissionService.CanAccessOrgao(ctx, relatorio.OrgaoId)) return Forbid();

        var bytes = await _relatorioService.GerarPdfRelatorioSemestralAsync(id);
        var sigla = relatorio.Orgao?.Sigla ?? "orgao";
        return File(bytes, "application/pdf",
            $"relatorio-semestral-{sigla}-{relatorio.Ano}-{relatorio.Semestre}.pdf");
    }

    // ── Solicitações do cidadão (arts. 11, IV e 23) ───────────────────────────

    /// <summary>
    /// Solicitações abertas sobre os sistemas do órgão, com os dados do solicitante
    /// </summary>
    [HttpGet("solicitacoes/orgao/{orgaoId:long}")]
    public async Task<IActionResult> ListarSolicitacoes(long orgaoId)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Solicitacao)) return Forbid();
        if (!_permissionService.CanAccessOrgao(ctx, orgaoId)) return Forbid();

        return Ok(await _publicoService.ListarSolicitacoesPorOrgaoAsync(orgaoId));
    }

    /// <summary>
    /// Responde ao cidadão em linguagem simples (art. 23, II e III)
    /// </summary>
    [HttpPut("solicitacao/{id:long}/resposta")]
    public async Task<IActionResult> ResponderSolicitacao(long id, [FromBody] PgiaSolicitacaoRespostaDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Solicitacao)) return Forbid();

        var solicitacao = await _publicoService.GetSolicitacaoEntidadeAsync(id);
        if (solicitacao == null) return NotFound();
        if (solicitacao.Sistema == null || !_permissionService.CanAccessOrgao(ctx, solicitacao.Sistema.OrgaoId))
            return Forbid();

        return Ok(await _publicoService.ResponderAsync(id, dto, ctx));
    }

    /// <summary>
    /// Encaminha a solicitação ao Encarregado de Dados (art. 11, IV)
    /// </summary>
    [HttpPut("solicitacao/{id:long}/encaminhar-dpo")]
    public async Task<IActionResult> EncaminharSolicitacaoAoDpo(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Solicitacao)) return Forbid();

        var solicitacao = await _publicoService.GetSolicitacaoEntidadeAsync(id);
        if (solicitacao == null) return NotFound();
        if (solicitacao.Sistema == null || !_permissionService.CanAccessOrgao(ctx, solicitacao.Sistema.OrgaoId))
            return Forbid();

        return Ok(await _publicoService.EncaminharAoDpoAsync(id, ctx));
    }

    /// <summary>
    /// Marca a solicitação como em análise pelo órgão
    /// </summary>
    [HttpPut("solicitacao/{id:long}/em-analise")]
    public async Task<IActionResult> MarcarSolicitacaoEmAnalise(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Solicitacao)) return Forbid();

        var solicitacao = await _publicoService.GetSolicitacaoEntidadeAsync(id);
        if (solicitacao == null) return NotFound();
        if (solicitacao.Sistema == null || !_permissionService.CanAccessOrgao(ctx, solicitacao.Sistema.OrgaoId))
            return Forbid();

        return Ok(await _publicoService.MarcarEmAnaliseAsync(id, ctx));
    }
}
