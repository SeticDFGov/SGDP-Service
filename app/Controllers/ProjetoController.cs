using System.Threading.Tasks;
using api.Projeto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;
using service;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjetoController : ControllerBase
{
    public readonly IProjetoRepositorio _repositorio;
    public readonly ProjetoService _projetoService;
    public readonly AppDbContext _context;

    public ProjetoController(IProjetoRepositorio repositorio, ProjetoService projetoService, AppDbContext context)
    {
        _repositorio = repositorio;
        _projetoService = projetoService;
        _context = context;
    }

    [HttpGet]
    public Task<List<Projeto>> GetAllProjetos([FromQuery] string unidade)
    {
        var items = _repositorio.GetProjetoListItemsAsync(unidade);
        return items;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjetosById(int id)
    {
        Projeto? items = await _repositorio.GetProjetoById(id);

        if(items == null)
        {
            return NotFound(new {message = "Projeto não encontrado"});
        }
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProjeto([FromBody] ProjetoDTO dto)
    {
        var unidade = await _context.Unidades.FindAsync(dto.UnidadeId);
        var esteira = await _context.Esteiras.FindAsync(dto.EsteiraId);
        var demandante = await _context.AreaDemandantes.FindAsync(dto.NM_AREA_DEMANDANTE);
        if (unidade == null || esteira == null)
            return BadRequest("Unidade ou Esteira não encontrada");
        var projeto = new Projeto
        {
            NM_PROJETO = dto.NM_PROJETO,
            GERENTE_PROJETO = dto.GERENTE_PROJETO,
            SITUACAO = dto.SITUACAO,
            NR_PROCESSO_SEI = dto.NR_PROCESSO_SEI,
            ANO = dto.ANO,
            TEMPLATE = dto.TEMPLATE,
            PROFISCOII = dto.PROFISCOII,
            PDTIC2427 = dto.PDTIC2427,
            PTD2427 = dto.PTD2427,
            valorEstimado = dto.valorEstimado,
            Unidade = unidade,
            Esteira = esteira,
            AREA_DEMANDANTE = demandante
            
        };
        await _repositorio.CreateProjeto(projeto);
        return Ok();
    }

    [HttpPost("template")]
    public async Task<IActionResult> CreateProjetoByTemplate([FromBody] ProjetoDTO dto)
    {
        var unidade = await _context.Unidades.FindAsync(dto.UnidadeId);
        var esteira = await _context.Esteiras.FindAsync(dto.EsteiraId);
        var demandante = await _context.AreaDemandantes.FindAsync(dto.NM_AREA_DEMANDANTE);
        if (unidade == null || esteira == null || demandante == null)
            return BadRequest("Unidade ou Esteira não encontrada");
        var projeto = new Projeto
        {
            NM_PROJETO = dto.NM_PROJETO,
            GERENTE_PROJETO = dto.GERENTE_PROJETO,
            SITUACAO = dto.SITUACAO,
            NR_PROCESSO_SEI = dto.NR_PROCESSO_SEI,
            ANO = dto.ANO,
            TEMPLATE = dto.TEMPLATE,
            PROFISCOII = dto.PROFISCOII,
            PDTIC2427 = dto.PDTIC2427,
            PTD2427 = dto.PTD2427,
            valorEstimado = dto.valorEstimado,
            Unidade = unidade,
            Esteira = esteira,
            AREA_DEMANDANTE = demandante
        };
        await _repositorio.CreateProjetoByTemplate(projeto);
        return Ok();
    }
    [HttpPost("analise/{id}")]
    public async Task<IActionResult> CreateAnalise([FromBody] ReportDTO projeto)
    {
        await _repositorio.AnaliseProjeto(projeto);
       return Ok();

    }
    [HttpGet("analise/{id}")]
    public async Task<IActionResult> Analise(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "ID inválido" }); 

        List<Report?> analise = await _repositorio.GetListReport(id);

        if (analise == null)
            return NotFound(new { message = "Análise não encontrada" }); 

        return Ok(analise); 
    }

    [HttpGet("quantidade")]
    public async Task<IActionResult> GetQuantidadeProjetos()
    {
        var quantidade = await _projetoService.GetQuantidadeProjetos();
        return Ok(quantidade);
    }
}