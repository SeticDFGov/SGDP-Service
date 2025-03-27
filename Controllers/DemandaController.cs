using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class DemandaController : ControllerBase
{
    public readonly DemandaRepositorio _repositorio;

    public DemandaController(DemandaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public Task<List<Dictionary<string, object>>> GetAllDemandandas()
    {
        var items = _repositorio.GetDemandaListItemsAsync();
        return items;
    }

    [HttpPost]
    public IActionResult CreateDemanda([FromBody] Demanda demanda)
    {
         _repositorio.CreateDemanda(demanda);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDemanda(int id)
    {
        _repositorio.DeleteDemanda(id);
        return Ok();
    }

    [HttpPut("{id}")]
    public IActionResult EditDemanda ([FromBody] Demanda demanda, int id)
    {
        try{
              _repositorio.EditDemanda(id, demanda);
              return Ok();
        }catch(Exception e){
            Console.Write(e);
            return StatusCode(500);
        }

      
    }
}