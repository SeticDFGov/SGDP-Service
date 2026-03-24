using api.AreaExecutora;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;

namespace Controllers;

[ApiController]
[Authorize]
[Route("api/area-executora")]
public class AreaExecutoraController : ControllerBase
{
    private readonly IAreaExecutoraRepositorio _repositorio;

    public AreaExecutoraController(IAreaExecutoraRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var areas = await _repositorio.GetAllAsync();
        return Ok(areas);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var area = await _repositorio.GetByIdAsync(id);
        if (area == null) return NotFound();
        return Ok(area);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AreaExecutoraDTO dto)
    {
        var area = new AreaExecutora { Nome = dto.Nome };
        await _repositorio.AddAsync(area);
        return Ok(area);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AreaExecutoraDTO dto)
    {
        var area = await _repositorio.GetByIdAsync(id);
        if (area == null) return NotFound();

        area.Nome = dto.Nome;
        await _repositorio.UpdateAsync(area);
        return Ok(area);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var area = await _repositorio.GetByIdAsync(id);
        if (area == null) return NotFound();

        await _repositorio.DeleteAsync(area);
        return Ok();
    }
}
