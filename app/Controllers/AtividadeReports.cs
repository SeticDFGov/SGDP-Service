using api.Atividade;
using api.Projeto;
using demanda_service.Repositorio.Interface;
using Microsoft.AspNetCore.Mvc;
using Models;
using Microsoft.EntityFrameworkCore;
using AtividadeDTO = api.Atividade.AtividadeDTO;

[ApiController]
[Route("api/reports")] 
public class ReportsController : ControllerBase
{
    private readonly IAtividadeRepositorio _atividadeRepositorio;
    private readonly AppDbContext _context;
   
    public ReportsController(IAtividadeRepositorio atividadeRepositorio, AppDbContext appDbContext)
    {
        _atividadeRepositorio = atividadeRepositorio;
        _context = appDbContext;
    }

    [HttpGet("{projetoId}")]
    public async Task<ActionResult> VisualizarReport(int projetoId)
    {
        var reports = await _context.Reports
            .Where(c => c.NM_PROJETO.projetoId == projetoId).ToListAsync();
        return Ok(reports);
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

    
    [HttpPost("{projetoId}/atividades")]
    public async Task<IActionResult> InserirAtividade(int projetoId, [FromBody] AtividadeDTO atividadeDto)
    {
        await _atividadeRepositorio.InserirAtividade(atividadeDto, projetoId);
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
    private readonly AppDbContext _context;
    public AtividadesController(IAtividadeRepositorio atividadeRepositorio,  AppDbContext appDbContext)
    {
        _atividadeRepositorio = atividadeRepositorio;
        _context = appDbContext;
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
    
    [HttpGet("export/{reportId}")]
    public IActionResult GerarExport(int reportId)
    {
        var pdf = _atividadeRepositorio.GerarReportPDF(reportId);
        return File(pdf, "application/pdf", "report.pdf");
    }
    
}