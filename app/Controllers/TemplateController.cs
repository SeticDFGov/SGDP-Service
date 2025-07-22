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
            DIAS_PREVISTOS = template.DIAS_PREVISTOS,
            PERCENT_TOTAL = template.PERCENT_TOTAL,
            ORDER = template.ORDER
        };
        await _templateRepositorio.CreateTemplate(newTemplate);
        return Ok();
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] TemplateDTO template)
    {
        var existingTemplate = await _templateRepositorio.GetTemplateById(id);
        if (existingTemplate == null)
        {
            return NotFound(new { Message = "Template not found" });
        }
        existingTemplate.NM_TEMPLATE = template.NM_TEMPLATE;
        existingTemplate.NM_ETAPA = template.NM_ETAPA;
        existingTemplate.DIAS_PREVISTOS = template.DIAS_PREVISTOS;
        existingTemplate.PERCENT_TOTAL = template.PERCENT_TOTAL;
        existingTemplate.ORDER = template.ORDER;
        await _templateRepositorio.UpdateTemplate(existingTemplate);
        return Ok();
    }

    [HttpGet("templates/name")]
    public async Task<IActionResult> GetNameTemplate()
    {
        var nameTemplate = await _templateRepositorio.GetNameTemplate();
        return Ok(nameTemplate);
    }
  

}