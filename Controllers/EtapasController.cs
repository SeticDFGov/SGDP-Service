using api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using service;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class EtapaController : ControllerBase
{
    public readonly EtapaRepositorio _repositorio;
    public readonly EtapaService _service;

    public EtapaController(EtapaRepositorio repositorio, EtapaService service)
    {
        _repositorio = repositorio;
        _service = service;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAllEtapas(int id)
    {
        List<Etapa> items = await _repositorio.GetEtapaListItemsAsync(id);
        if(items.Count() == 0)
        {
            return NotFound(new {message = "Etapas não encontradas para este projeto"});
        }
        return Ok(items);
    }
    [HttpGet("api/byid/{id}")]
    public Task<Etapa> GetEtapaById(int id)
    {
        var items = _repositorio.GetById(id);
        return items;
    }

    [HttpPost("{id}")]
    public IActionResult CreateEtapas([FromBody] Etapa etapa, int id)
    {
         _repositorio.CreateEtapa(etapa, id);
       return Ok();

    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEtapas([FromBody] AfericaoEtapaDTO etapa, int id)
    {
        await _repositorio.EditEtapa(etapa,id);
        return Ok();
    }
    [HttpGet("percent/{projetoid}")]
    public async Task<IActionResult> GetPercentEtapas( int projetoid)
    {
        PercentualEtapaDTO response = await _service.GetPercentEtapas(projetoid);
        return Ok(response);
    }

}