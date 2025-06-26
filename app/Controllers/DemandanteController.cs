using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio.Interface;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DemandanteController : ControllerBase
{
    public readonly IDemandanteRepositorio _repositorio;

    public DemandanteController(IDemandanteRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDemandantes()
    {
        var items = await _repositorio.GetDemandanteListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDemandante([FromBody] AreaDemandante demandante)
    {
       await  _repositorio.CreateDemandanteAsync(demandante);
       return Ok();

    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDemandante(int id)
    {
        await _repositorio.DeleteDemandanteAsync(id);
        return Ok();
    }


}