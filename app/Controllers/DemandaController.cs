using api.Demanda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DemandaController : ControllerBase
{
    public readonly IDemandaRepositorio _repositorio;

    public DemandaController(IDemandaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDemandandas()
    {
        var items = await _repositorio.GetDemandasListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDemanda([FromBody] DemandaDTO demanda)
    {
        await _repositorio.CreateDemanda(demanda);
       return Ok();

    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDemanda(int id)
    {
        await _repositorio.DeleteDemanda(id);
        return Ok();
    }

    [HttpPut()]
public async Task<IActionResult> EditDemanda([FromBody] DemandaDTO demanda)
{
        await _repositorio.EditDemanda(demanda); 
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