using System.Numerics;
using Interface.Repositorio;
using Microsoft.AspNetCore.Mvc;
using Models;

namespace Controllers;


public class TemplateController : ControllerBase
{
    private readonly ITemplateRepositorio _templateRepositorio;

    public TemplateController(ITemplateRepositorio templateRepositorio)
    {
        _templateRepositorio = templateRepositorio;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _templateRepositorio.GetTemplateListItemsAsync();
        return Ok(templates);
    }

    [HttpGet("templates/{id}")]
    public async Task<IActionResult> GetTemplateById(int id)
    {
        var template = await _templateRepositorio.GetTemplateById(id);
        if (template == null)
        {
            return NotFound(new { Message = "Template not found" });
        }
        return Ok(template);
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateDTO template)
    {
        if (template == null)
        {
            return BadRequest(new { Message = "Invalid template data" });
        }
        var newTemplate = new Template
        {
            NM_TEMPLATE = template.NM_TEMPLATE,
            NM_ETAPA = template.NM_ETAPA,
            COMPLEXIDADE = MapearPrioridade(template.COMPLEXIDADE),
            PERCENT_TOTAL = template.PERCENT_TOTAL,
            ORDER = template.ORDER
        };
        await _templateRepositorio.CreateTemplate(newTemplate);
        return Ok();
    }
    private Complexidade MapearPrioridade(string prioridade)
    {
        return prioridade.ToLower().Trim() switch
        {
            "baixa" => Complexidade.BAIXA,
            "média" or "media" => Complexidade.MEDIA,
            "alta" => Complexidade.ALTA,
            "não se aplica" or "nao se aplica" => Complexidade.NAO_SE_APLICA,
            _ => throw new ArgumentException("Prioridade inválida.")
        };
    }

}