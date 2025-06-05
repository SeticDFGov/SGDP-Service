using api.Demanda;
using Models;
using Repositorio.Interface;
using test.fixtures;
using Xunit;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace test;

public class DemandaRepositorioTest : TestBed<Base>, IDisposable
{
    IDemandaRepositorio _demandaRepositorio;
    AppDbContext _context;

    public DemandaRepositorioTest(ITestOutputHelper testOutputHelper, Base fixture) : base(testOutputHelper, fixture)
    {
        _demandaRepositorio = fixture.GetService<IDemandaRepositorio>(testOutputHelper);
        _context = fixture.GetService<AppDbContext>(testOutputHelper);
    }

    [Fact]
    public async Task GetDemandasAsync_ReturnsDemandas()
    {
        var demandaSalva = new Demanda
        {
            DemandaId = 1,
            NM_DEMANDA = "Teste Demanda",
            DT_ABERTURA = DateTime.UtcNow,
            DT_SOLICITACAO = DateTime.UtcNow,
            CATEGORIA = new Categoria { CategoriaId = 1, Nome = "Categoria Teste" },
            NM_AREA_DEMANDANTE = new AreaDemandante { AreaDemandanteID = 1, NM_SIGLA = "Area Teste", NM_DEMANDANTE = "Demandante Teste" },
            NM_PO_DEMANDANTE = "PO Teste",
            NM_PO_SUBTDCR = "Subtdcr Teste",
            NR_PROCESSO_SEI = "123456",
            PATROCINADOR = "Patrocinador Teste",
            PERIODICO = "Periodico Teste",
            PERIODICIDADE = "Mensal",
            STATUS = "Em Andamento",
            UNIDADE = "Unidade Teste"
        };
        _context.Demandas.Add(demandaSalva);
        await _context.SaveChangesAsync();

        var result = await _demandaRepositorio.GetDemandasListItemsAsync();
        Assert.NotEmpty(result);
        Assert.Equal("Teste Demanda", result.First().NM_DEMANDA);
        _context.Demandas.Remove(demandaSalva);
        await _context.SaveChangesAsync();

    }

    [Fact]
    public async Task CreateDemanda()
    {
        var categoria = new Categoria { CategoriaId = 100, Nome = "Categoria Teste" };
        var areaDemandante = new AreaDemandante { AreaDemandanteID = 100, NM_SIGLA = "Area Teste", NM_DEMANDANTE = "Demandante Teste" };
        _context.Categorias.Add(categoria);
        _context.AreaDemandantes.Add(areaDemandante);
        var novaDemanda = new DemandaDTO
        {
            NM_DEMANDA = "Nova Demanda",
            DT_ABERTURA = DateTime.UtcNow,
            DT_SOLICITACAO = DateTime.UtcNow,
            DT_CONCLUSAO = DateTime.UtcNow,
            CATEGORIA = "Categoria Teste", 
            NM_AREA_DEMANDANTE = "Demandante Teste", 
            NM_PO_DEMANDANTE = "PO Teste",
            NM_PO_SUBTDCR = "Subtdcr Teste",
            NR_PROCESSO_SEI = "123456",
            PATROCINADOR = "Patrocinador Teste",
            PERIODICO = "Periodico Teste",
            PERIODICIDADE = "Mensal",
            STATUS = "Em Andamento",
            UNIDADE = "Unidade Teste"
        };

        await _demandaRepositorio.CreateDemanda(novaDemanda);

        var demandaSalva = _context.Demandas.FirstOrDefault(d => d.NM_DEMANDA == "Nova Demanda");
        Assert.NotNull(demandaSalva);
        _context.Demandas.Remove(demandaSalva);
        _context.SaveChanges();
    
    }
}