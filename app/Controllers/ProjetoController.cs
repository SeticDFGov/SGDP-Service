using System.Threading.Tasks;
using api.Projeto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using service;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class ProjetoController : ControllerBase
{
    public readonly ProjetoRepositorio _repositorio;

    public readonly ProjetoService _projetoService;
    public ProjetoController(ProjetoRepositorio repositorio, ProjetoService projetoService)
    {
        _repositorio = repositorio;
        _projetoService = projetoService;
    }

    [HttpGet]
    public Task<List<Projeto>> GetAllProjetos()
    {
        var items = _repositorio.GetProjetoListItemsAsync();
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
    public IActionResult CreateProjeto([FromBody] Projeto projeto)
    {
         _repositorio.CreateProjeto(projeto);
       return Ok();

    }

    [HttpPost("template")]
    public async Task<IActionResult> CreateProjetoByTemplate([FromBody] Projeto projeto)
    {
        await _repositorio.CreateProjetoByTemplate(projeto);
       return Ok();

    }
    [HttpPost("analise/{id}")]
    public async Task<IActionResult> CreateAnalise([FromBody] ProjetoAnaliseDTO projeto)
    {
        await _repositorio.AnaliseProjeto(projeto);
       return Ok();

    }
    [HttpGet("analise/{id}")]
    public async Task<IActionResult> Analise(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "ID inválido" }); 

        ProjetoAnalise? analise = await _repositorio.GetLastAnaliseProjeto(id);

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