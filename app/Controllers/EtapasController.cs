using api.Etapa;
using api.Projeto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;
using service;
using TimeZoneConverter;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EtapaController : ControllerBase
{
    public readonly EtapaService _service;

    public EtapaController(IEtapaRepositorio repositorio, EtapaService service)
    {
        _service = service;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAllEtapas(int id)
    {
        List<Etapa> items = await _service.GetEtapaListItemsAsync(id);
        if(items.Count() == 0)
        {
            return NotFound(new {message = "Etapas não encontradas para este projeto"});
        }
        return Ok(items);
    }
    [HttpGet("api/byid/{id}")]
    public Task<Etapa> GetEtapaById(int id)
    {
        var items = _service.GetById(id);
        return items;
    }

    [HttpPost()]
    public async Task<IActionResult> CreateEtapas([FromBody] EtapaDTO etapa)
    {
       await _service.CreateEtapa(etapa);
       return Ok();

    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEtapas([FromBody] AfericaoEtapaDTO etapa, int id)
    {
        await _service.EditEtapa(etapa,id);
        return Ok();
    }
    [HttpGet("percent/{projetoid}")]
    public async Task<IActionResult> GetPercentEtapas( int projetoid)
    {
        PercentualEtapaDTO response = await _service.GetPercentEtapas(projetoid);
        return Ok(response);
    }

     [HttpGet("situacao")]
    public async Task<ActionResult<SituacaoProjetoDTO>> GetSituacaoProjetos()
    {
        var resultado = await _service.GetSituacao();
        return Ok(resultado);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags()
    {
        var tags = await _service.GetTags();
        return Ok(tags);
    }

    [HttpPut("iniciar/{id}")]
    public async Task<IActionResult> IniciarEtapa(int id, [FromBody] DateTime dtInicioPrevisto)
    {
        TimeZoneInfo brasilia = TZConvert.GetTimeZoneInfo("E. South America Standard Time");
        dtInicioPrevisto =
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dtInicioPrevisto, DateTimeKind.Unspecified), brasilia);
            
        await _service.IniciarEtapa(id, dtInicioPrevisto);
        return Ok();
    }
}