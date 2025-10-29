using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Repositorio;
using Models;
using service;
using Repositorio.Interface;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;
using test.fixtures;

public class CategoriaRepositorioTests: TestBed<Base>, IDisposable 
{
    ICategoriaRepositorio _categoriaRepositorio;
    AppDbContext _context;
    public CategoriaRepositorioTests(ITestOutputHelper testOutputHelper, Base fixture) : base(testOutputHelper, fixture)
    {
        _categoriaRepositorio = fixture.GetService<ICategoriaRepositorio>(testOutputHelper);
        _context = fixture.GetService<AppDbContext>(testOutputHelper);

    }

    [Fact]
    public async Task GetCategoriaListItemsAsync_ReturnsCategorias()
    {

        var categoria = new Categoria
        {
            CategoriaId = 1,
            Nome = "Teste"
        };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        var result = await _categoriaRepositorio.GetCategoriaListItemsAsync();


        Assert.Equal("Teste", result.First().Nome);
        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
    }


    [Fact]
    public async Task CreateCategoriaAsync_AddsCategoria()
    {
        var novaCategoria = new Categoria { CategoriaId = 2, Nome = "Nova" };

        await _categoriaRepositorio.CreateCategoriaAsync(novaCategoria);

        var categoriaSalva = await _context.Categorias.FindAsync(2);
        Assert.NotNull(categoriaSalva);
        Assert.Equal("Nova", categoriaSalva.Nome);
        _context.Categorias.Remove(categoriaSalva);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteCategoriaAsync_DeletesCategoria()
    {
        _context.Categorias.Add(new Categoria { CategoriaId = 3, Nome = "Excluir" });
        await _context.SaveChangesAsync();

        await _categoriaRepositorio.DeleteCategoriaAsync(3);

        var categoria = await _context.Categorias.FindAsync(3);
        Assert.Null(categoria);
    }

    [Fact]
    public async Task DeleteCategoriaAsync_ThrowsApiException_WhenNotFound()
    {

        var ex = await Assert.ThrowsAsync<ApiException>(() => _categoriaRepositorio.DeleteCategoriaAsync(999));
        Assert.Equal(ErrorCode.CategoriaNaoEncontrada.ToString(), ex.Error.CodeStr);
    }
}
