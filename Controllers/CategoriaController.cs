using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class CategoriaController : ControllerBase
{
    public readonly CategoriaRepositorio _repositorio;

    public CategoriaController(CategoriaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public Task<List<Dictionary<string, object>>> GetAllCategorias()
    {
        var items = _repositorio.GetCategoriaListItemsAsync();
        return items;
    }

    [HttpPost]
    public IActionResult CreateCategoria([FromBody] Categoria categoria)
    {
         _repositorio.CreateCategoria(categoria);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteCategoria(int id)
    {
        _repositorio.DeleteCategoria(id);
        return Ok();
    }


}