using System.Security.Claims;
using System.Threading.Tasks;
using api.Common;
using api.Projeto;
using demanda_service.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio;
using Repositorio.Interface;
using service;
using service.Interface;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjetoController : ControllerBase
{
    public readonly IProjetoService _service;
    public readonly IProjetoService _projetoService;
    public readonly AppDbContext _context;
    private readonly IPermissionService _permissionService;

    public ProjetoController(
        IProjetoService service,
        IProjetoService projetoService,
        AppDbContext context,
        IPermissionService permissionService)
    {
        _service = service;
        _projetoService = projetoService;
        _context = context;
        _permissionService = permissionService;
    }

    private string? GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Lista todos os projetos filtrados por perfil do usuário (sem paginação - mantido para compatibilidade)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Projeto>>> GetAllProjetos([FromQuery] string unidade)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        var userUnidade = await _permissionService.GetUserUnidadeAsync(email);
        var unidadeNome = userUnidade?.Nome ?? unidade;

        var query = _permissionService.GetFilteredProjetosQuery(perfil, unidadeNome);
        var projetos = await query.ToListAsync();

        // Calcula situação para cada projeto
        foreach (var projeto in projetos)
        {
            var etapas = projeto.Etapas ?? new List<Etapa>();
            projeto.SITUACAO = CalcularSituacaoProjeto(etapas);
        }

        return Ok(projetos);
    }

    /// <summary>
    /// Lista projetos filtrados por perfil com paginação
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResponse<Projeto>>> GetProjetosPaged(
        [FromQuery] string unidade,
        [FromQuery] PagedRequest request)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        var userUnidade = await _permissionService.GetUserUnidadeAsync(email);
        var unidadeNome = userUnidade?.Nome ?? unidade;

        var query = _permissionService.GetFilteredProjetosQuery(perfil, unidadeNome);

        var totalItems = await query.CountAsync();

        var projetos = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync();

        // Calcula situação para cada projeto
        foreach (var projeto in projetos)
        {
            var etapas = projeto.Etapas ?? new List<Etapa>();
            projeto.SITUACAO = CalcularSituacaoProjeto(etapas);
        }

        return Ok(PagedResponse<Projeto>.Create(projetos, totalItems, request));
    }

    private string CalcularSituacaoProjeto(ICollection<Etapa> etapas)
    {
        if (!etapas.Any())
            return "Não Iniciado";

        if (etapas.Any(e => e.SITUACAO == "Atrasado"))
            return "Atrasado";

        if (etapas.Any(e => e.SITUACAO == "Em Andamento"))
            return "Em Andamento";

        if (etapas.All(e => e.SITUACAO == "Concluído"))
            return "Concluído";

        return "Não Iniciado";
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjetosById(int id)
    {
        Projeto items = await _service.GetProjetoById(id);
        return Ok(items);
    }
    

    [HttpPost("")]
    public async Task<IActionResult> CreateProjetoByTemplate([FromBody] ProjetoDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanCreate(perfil, "projeto"))
            return Forbid();

        var unidade = await _context.Unidades.FindAsync(dto.UnidadeId);
        var esteira = await _context.Esteiras.FindAsync(dto.EsteiraId);
        var demandante = await _context.AreaDemandantes.FindAsync(dto.NM_AREA_DEMANDANTE);
        if (unidade == null || esteira == null || demandante == null)
            return BadRequest("Unidade ou Esteira não encontrada");

        // Usando DateTimeHelper para conversão de timezone
        dto.DT_INICIO = DateTimeHelper.ToUtc(dto.DT_INICIO);
        dto.DT_TERMINO = DateTimeHelper.ToUtc(dto.DT_TERMINO);
        var projeto = new Projeto
        {
            NM_PROJETO = dto.NM_PROJETO,
            GERENTE_PROJETO = dto.GERENTE_PROJETO,
            SITUACAO = "",
            NR_PROCESSO_SEI = dto.NR_PROCESSO_SEI,
            ANO = dto.ANO,
            TEMPLATE = dto.TEMPLATE,
            PROFISCOII = dto.PROFISCOII,
            PDTIC2427 = dto.PDTIC2427,
            PTD2427 = dto.PTD2427,
            valorEstimado = dto.valorEstimado,
            Unidade = unidade,
            Esteira = esteira,
            AREA_DEMANDANTE = demandante,
            DT_INICIO = dto.DT_INICIO,
            DT_TERMINO = dto.DT_TERMINO,
        };
        await _service.CreateProjetoByTemplate(projeto);
        return Ok();
    }

    [HttpGet("quantidade")]
    public async Task<IActionResult> GetQuantidadeProjetos()
    {
        var quantidade = await _projetoService.GetQuantidadeProjetos();
        return Ok(quantidade);
    }
}