using Models;
using Repositorio.Interface;
using test.fixtures;
using Xunit;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace test;

public class DemandandeRepositorioTest : TestBed<Base>, IDisposable
{
    IDemandanteRepositorio _demandandanteRepositorio;
    AppDbContext _context;
    public DemandandeRepositorioTest(ITestOutputHelper testOutputHelper, Base fixture) : base(testOutputHelper, fixture)
    {
        _demandandanteRepositorio = fixture.GetService<IDemandanteRepositorio>(testOutputHelper);
        _context = fixture.GetService<AppDbContext>(testOutputHelper);

    }

    [Fact]
    public async Task GetDemandanteListItemsAsync_ReturnsDemandantes()
    {
        var areaDemandante = new AreaDemandante
        {
            AreaDemandanteID = 1,
            NM_SIGLA = "TestSIGLA",
            NM_DEMANDANTE = "Teste"
        };
        _context.AreaDemandantes.Add(areaDemandante);
        await _context.SaveChangesAsync();

        var result = await _demandandanteRepositorio.GetDemandanteListItemsAsync();
        Assert.NotEmpty(result);
        _context.AreaDemandantes.Remove(areaDemandante);
        await _context.SaveChangesAsync();
    }
    [Fact]
    public async Task CreateDemandanteAsync_AddsDemandante()
    {
        var novaArea = new AreaDemandante { AreaDemandanteID = 2, NM_SIGLA = "NovaSIGLA", NM_DEMANDANTE = "Nova" };

        await _demandandanteRepositorio.CreateDemandanteAsync(novaArea);

        var areaSalva = await _context.AreaDemandantes.FindAsync(2);
        Assert.NotNull(areaSalva);
        Assert.Equal("Nova", areaSalva.NM_DEMANDANTE);
        Assert.Equal("NovaSIGLA", areaSalva.NM_SIGLA);
        _context.AreaDemandantes.Remove(areaSalva);
        await _context.SaveChangesAsync();
    }
}