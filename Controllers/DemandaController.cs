using api;
using api.Demanda;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class DemandaController : ControllerBase
{
    public readonly IDemandaRepositorio _repositorio;

    public DemandaController(IDemandaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult GetAllDemandandas()
    {
        var items = _repositorio.GetDemandasListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDemanda([FromBody] DemandaDTO demanda)
    {
        await _repositorio.CreateDemanda(demanda);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDemanda(int id)
    {
        _repositorio.DeleteDemanda(id);
        return Ok();
    }

    [HttpPut()]
public IActionResult EditDemanda([FromBody] DemandaDTO demanda)
{
        _repositorio.EditDemanda(demanda); 
        return Ok(demanda);
}

[HttpGet("{id}")]
public async Task<IActionResult> GetDemandaById(int id)
{
    Demanda demanda = await _repositorio.GetDemandaById(id);
    return Ok(demanda);
}

[HttpGet("rank")]
public async Task<IActionResult> GetPercent ()
{
    var response = await _repositorio.GetQuantidadeTipo();
    return Ok(response);
}

}