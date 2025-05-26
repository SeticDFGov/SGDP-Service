using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;

namespace Repositorio;

public class CategoriaRepositorio: ICategoriaRepositorio 
{
    public readonly AppDbContext _context;
    public CategoriaRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Categoria>> GetCategoriaListItemsAsync()
   {
    try
    {
        var listItems = await _context.Categorias.ToListAsync();

        if (listItems == null || !listItems.Any())
        {
            return new List<Categoria> {
                new Categoria { CategoriaId = 0, Nome = "Categorias não encontradas." }
            };
        }

        var result = listItems.Select(item => new Categoria
        {
            CategoriaId = item.CategoriaId,
            Nome = item.Nome
        }).ToList();

        return result;
    }
    catch (Exception ex)
    {
        return new List<Categoria> {
        };
    }
  }

public async Task CreateCategoria (Categoria categoria)
{
    _context.Categorias.Add(categoria);
    _context.SaveChangesAsync();
}

public async Task DeleteCategoria (int id)
{   
    var item = _context.Categorias.FirstOrDefault(e => e.CategoriaId == id) ?? throw new Exception("Categoria não encontrada.");
        _context.Categorias.Remove(item);
    await _context.SaveChangesAsync();

}

    
}