using api.Atividade; 
using demanda_service.Repositorio.Interface;
using Microsoft.AspNetCore.Mvc;
using Models;

[ApiController]
[Route("api/reports")] 
public class ReportsController : ControllerBase
{
    private readonly IAtividadeRepositorio _atividadeRepositorio;

   
    public ReportsController(IAtividadeRepositorio atividadeRepositorio)
    {
        _atividadeRepositorio = atividadeRepositorio;
    }

   
    [HttpPost]
   
    public async Task<IActionResult> IniciarReport([FromBody] InicioAtividadeDTO inicioDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        await _atividadeRepositorio.IniciarReport(inicioDto);
        return Ok(inicioDto);
    }

   
    [HttpPost("{reportId}/atividades")]
    public async Task<IActionResult> InserirAtividade(int reportId, [FromBody] AtividadeDTO atividadeDto)
    {
        await _atividadeRepositorio.InserirAtividade(atividadeDto, reportId);
        return Ok(atividadeDto);
    }

    [HttpGet("{reportId}/atividades")]
    public async Task<IActionResult> VisualizarAtividades(int reportId)
    {
        var atividades = await _atividadeRepositorio.VisualizarAtividades(reportId);
        return Ok(atividades);
    }
}


[ApiController]
[Route("api/atividades")]
public class AtividadesController : ControllerBase
{
    private readonly IAtividadeRepositorio _atividadeRepositorio;

    public AtividadesController(IAtividadeRepositorio atividadeRepositorio)
    {
        _atividadeRepositorio = atividadeRepositorio;
    }

    [HttpPut("{atividadeId}")]
    public async Task<IActionResult> AlterarAtividade(int atividadeId, [FromBody] AtividadeDTO atividadeDto)
    {
        
        await _atividadeRepositorio.AlterarAtividade(atividadeDto, atividadeId);
        return NoContent(); 
    }

   
    [HttpDelete("{atividadeId}")]
    
    public async Task<IActionResult> RemoverAtividade(int atividadeId)
    {
        await _atividadeRepositorio.RemoverAtividade(atividadeId);
        return NoContent(); 
    }
}