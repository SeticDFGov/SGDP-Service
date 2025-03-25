using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;

namespace Repositorio;

public class CategoriaRepositorio 
{
    public readonly AppDbContext _context;
    public CategoriaRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Dictionary<string, object>>> GetCategoriaListItemsAsync()
{
    try
    {
        var listItems = await _context.Categorias.ToListAsync();

        if (listItems == null || !listItems.Any())
        {
            return new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "message", "Categorias não encontradas." } }
            };
        }

        var result = listItems.Select(item => new Dictionary<string, object>
        {
            { "CategoriaId", item.CategoriaId },
            { "Nome", item.Nome },
        }).ToList();

        return result;
    }
    catch (Exception ex)
    {
        return new List<Dictionary<string, object>> {
            new Dictionary<string, object> { {  "details", ex.Message } }
        };
    }
}

public void CreateCategoria (Categoria categoria)
{
    _context.Categorias.Add(categoria);
    _context.SaveChangesAsync();
}

public async Task<IResult> DeleteCategoria (int id)
{   
    var item = _context.Categorias.FirstOrDefault(e => e.CategoriaId == id);

    if(item == null)  return Results.NotFound();

    _context.Categorias.Remove(item);
    await _context.SaveChangesAsync();

    return Results.Ok();
}

    
}