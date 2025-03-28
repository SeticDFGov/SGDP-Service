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

    [HttpPost]
    public IActionResult CreateDemandante([FromBody] Projeto projeto)
    {
         _repositorio.CreateProjeto(projeto);
       return Ok();

    }

}