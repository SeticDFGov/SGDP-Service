using app.Models;
using service.Interface;
using Xunit;
using demanda_service.service;
using Microsoft.EntityFrameworkCore;
using Models;
using Moq;
using Repositorio.Interface;
using service;

namespace test;

public class ProjetoServiceTest
{
    private readonly Mock<IProjetoRepositorio> _repo;
    private readonly AppDbContext _context;
    private readonly ProjetoService _service;

    public ProjetoServiceTest()
    {
        _repo = new Mock<IProjetoRepositorio>();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        _context = new AppDbContext(options);
        
        _context.Projetos.Add(new Projeto
        {
            projetoId = 1,
            NM_PROJETO = "teste",
            NR_PROCESSO_SEI = "12312312312",
            PTD2427 = false,
            PROFISCOII = false,
            PDTIC2427 = false,
            AREA_DEMANDANTE = new AreaDemandante{AreaDemandanteID = Random.Shared.Next(), NM_DEMANDANTE = "TESTE", NM_SIGLA = "SIGLATESTE"},
            Esteira = new Esteira{EsteiraId = Guid.NewGuid(), Nome = "TESTE"},
            GERENTE_PROJETO = "GERENTETESTE",
            ANO = "2020",
            Unidade = new Unidade{id = Guid.NewGuid(), Nome = "TESTE"}
            
        });
        _context.SaveChanges();
        _service = new ProjetoService(_repo.Object, _context); 
    }
    
    [Fact]
    public void ListarProjetosInformandoUnidade()
    {
        var resultado = _service.GetProjetoListItemsAsync("TESTE");
        Assert.NotNull(resultado);
    }

    [Fact]
    public async Task ListarProjetosCasoNaoEncontreRetorneApiException()
    {
        _repo.Setup(r => r.GetProjetoListItemsAsync("NAOEXISTE"))
            .ReturnsAsync(new List<Projeto>()); 
        var exception = await Assert.ThrowsAsync<ApiException>(()=>_service.GetProjetoListItemsAsync("NAOEXISTE"));
        Assert.IsType<ApiException>(exception);
    }

    [Fact]

    public void RetornarProjetoCasoIdExista()
    {
        var resultado =  _service.GetProjetoById(1);
        Assert.NotNull(resultado);
    }
    [Fact]
    public void CriarProjetoComTemplate()
    {
        _service.CreateProjetoByTemplate(new Projeto
        {
            projetoId = 1,
            NM_PROJETO = "teste",
            NR_PROCESSO_SEI = "12312312312",
            PTD2427 = false,
            PROFISCOII = false,
            PDTIC2427 = false,
            AREA_DEMANDANTE = new AreaDemandante
                { AreaDemandanteID = Random.Shared.Next(), NM_DEMANDANTE = "TESTE", NM_SIGLA = "SIGLATESTE" },
            Esteira = new Esteira { EsteiraId = Guid.NewGuid(), Nome = "TESTE" },
            GERENTE_PROJETO = "GERENTETESTE",
            ANO = "2020",
            Unidade = new Unidade { id = Guid.NewGuid(), Nome = "TESTE" }

        });
        Assert.Equal(1, _context.Projetos.Count());
    }
    
    
    
    
}

