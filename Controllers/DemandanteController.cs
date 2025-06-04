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
    public IActionResult GetAllDemandantes()
    {
        var items = _repositorio.GetDemandanteListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public IActionResult CreateDemandante([FromBody] AreaDemandante demandante)
    {
         _repositorio.CreateDemandanteAsync(demandante);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDemandante(int id)
    {
        _repositorio.DeleteDemandanteAsync(id);
        return Ok();
    }


}