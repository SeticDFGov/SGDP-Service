using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class EtapaController : ControllerBase
{
    public readonly EtapaRepositorio _repositorio;

    public EtapaController(EtapaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet("{id}")]
    public Task<List<Dictionary<string, object>>> GetAllEtapas(int id)
    {
        var items = _repositorio.GetEtapaListItemsAsync(id);
        return items;
    }

    [HttpPost("{id}")]
    public IActionResult CreateEtapas([FromBody] Etapa etapa, int id)
    {
         _repositorio.CreateEtapa(etapa, id);
       return Ok();

    }

}