using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;

namespace Controllers;
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CategoriaController : ControllerBase
{
    public readonly ICategoriaRepositorio _repositorio;

    public CategoriaController(ICategoriaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCategorias()
    {
        var items = await _repositorio.GetCategoriaListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategoria([FromBody] Categoria categoria)
    {
       await  _repositorio.CreateCategoriaAsync(categoria);
       return Ok();

    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategoria(int id)
    {
        await _repositorio.DeleteCategoriaAsync(id);
        return Ok();
    }


}