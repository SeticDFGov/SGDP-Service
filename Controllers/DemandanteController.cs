using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class DemandanteController : ControllerBase
{
    public readonly DemandanteRepositorio _repositorio;

    public DemandanteController(DemandanteRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public Task<List<Dictionary<string, object>>> GetAllDemandantes()
    {
        var items = _repositorio.GetDemandanteListItemsAsync();
        return items;
    }

    [HttpPost]
    public IActionResult CreateDemandante([FromBody] AreaDemandante demandante)
    {
         _repositorio.CreateDemandante(demandante);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDemandante(int id)
    {
        _repositorio.DeleteDemandante(id);
        return Ok();
    }


}