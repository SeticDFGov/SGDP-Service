using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;
using service;

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
        var listItems = await _context.Categorias.ToListAsync() ?? throw new ApiException(ErrorCode.CategoriasNaoEncontradas);

        var result = listItems.Select(item => new Categoria
        {
            CategoriaId = item.CategoriaId,
            Nome = item.Nome
        }).ToList();

        return result;
    }
    catch (Exception)
    {
        throw new ApiException(ErrorCode.ErroAoBuscarCategorias);
    }
  }

public async Task CreateCategoriaAsync(Categoria categoria)
{
    try
    {
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
    }
    catch (Exception)
    {
        throw new ApiException(ErrorCode.ErroAoCriarCategoria);
    }
}


public async Task DeleteCategoriaAsync (int id)
{   
    var item = _context.Categorias.FirstOrDefault(e => e.CategoriaId == id) ?? throw new ApiException(ErrorCode.CategoriaNaoEncontrada);
    _context.Categorias.Remove(item);
    await _context.SaveChangesAsync();

}

    
}