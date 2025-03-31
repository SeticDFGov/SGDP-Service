using System.Threading.Tasks;
using api;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class ProjetoController : ControllerBase
{
    public readonly ProjetoRepositorio _repositorio;

    public ProjetoController(ProjetoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public Task<List<Dictionary<string, object>>> GetAllDemandantes()
    {
        var items = _repositorio.GetProjetoListItemsAsync();
        return items;
    }

    [HttpGet("{id}")]
    public Task<Dictionary<string, object>> GetAllDemandantes(int id)
    {
        var items = _repositorio.GetProjetoById(id);
        return items;
    }

    [HttpPost]
    public IActionResult CreateDemandante([FromBody] Projeto projeto)
    {
         _repositorio.CreateProjeto(projeto);
       return Ok();

    }

    [HttpPost("template")]
    public async Task<IActionResult> CreateProjetoBtTemplate([FromBody] Projeto projeto)
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
    public async Task<ProjetoAnalise> CreateAnalise(int id)
    {
       ProjetoAnalise analise =  await _repositorio.GetLastAnaliseProjeto(id);
       return analise;

    }


}