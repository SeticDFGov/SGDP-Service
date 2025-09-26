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
        var report = await _context.Reports
            .FirstOrDefaultAsync(c => c.NM_PROJETO.projetoId == projetoId);

        if (report == null)
            return NotFound();

        return Ok(report);
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
    
    [HttpGet("export/{exportId}")]
    public IActionResult GerarExport(Guid exportId)
    {
        var pdf = _atividadeRepositorio.GerarRelatorioPedidos(exportId);
        return File(pdf, "application/pdf", "report.pdf");
    }

    [HttpPost("export/create/{reportId})")]

    public async Task<IActionResult> GerarReport(int reportId)
    {
        await _atividadeRepositorio.GerarStatusReport(reportId);
        return Ok();
    }

    [HttpGet("export/list/{projectId}")]
    public IActionResult ListarExports(int projectId)
    {
        List<Export> exports = _context.Exports.Where(e => e.NM_PROJETO.projetoId == projectId).ToList();
        return Ok(exports);
    }
}