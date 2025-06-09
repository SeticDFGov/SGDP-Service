using Microsoft.AspNetCore.Mvc;
using Models;
using Repositorio;
using Repositorio.Interface;

namespace Controllers;
[ApiController]
[Route("api/[controller]")]
public class CategoriaController : ControllerBase
{
    public readonly ICategoriaRepositorio _repositorio;

    public CategoriaController(ICategoriaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult GetAllCategorias()
    {
        var items = _repositorio.GetCategoriaListItemsAsync();
        return Ok(items);
    }

    [HttpPost]
    public IActionResult CreateCategoria([FromBody] Categoria categoria)
    {
         _repositorio.CreateCategoriaAsync(categoria);
       return Ok();

    }

    [HttpDelete("{id}")]
    public IActionResult DeleteCategoria(int id)
    {
        _repositorio.DeleteCategoriaAsync(id);
        return Ok();
    }


}