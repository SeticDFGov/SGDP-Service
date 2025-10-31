using api.Etapa;
using api.Projeto;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using service;
using Xunit;

namespace test;

public class EtapaServiceTest
{
    private readonly AppDbContext _context;
    private readonly EtapaService _service;

    public EtapaServiceTest()
    {
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
        _service = new EtapaService(_context);
    }
    
    [Fact]
    public void CriarEtapaTest()
    {
        _service.CreateEtapa(new EtapaDTO
        {
            DT_INICIO_PREVISTO = DateTime.UtcNow,
            DT_TERMINO_PREVISTO = DateTime.UtcNow,
            NM_PROJETO = 1,
            PERCENT_TOTAL_ETAPA = 10,
            RESPONSAVEL_ETAPA = "TESTE",
            NM_ETAPA = "TESTE"

        });

        Assert.Equal(_context.Etapas.Count(), 1);

    }

    [Fact]
    public void ListarTodasAsEtapasDadoIdProjeto()
    {
        _service.CreateEtapa(new EtapaDTO
        {
            DT_INICIO_PREVISTO = DateTime.UtcNow,
            DT_TERMINO_PREVISTO = DateTime.UtcNow,
            NM_PROJETO = 1,
            PERCENT_TOTAL_ETAPA = 10,
            RESPONSAVEL_ETAPA = "TESTE",
            NM_ETAPA = "TESTE"

        });

        var etapas = _service.GetEtapaListItemsAsync(1);
        Assert.Equal(etapas.Result.Count(), 1);
    }

    [Fact]
    public async Task ReceberSituacaoTodasEtapas()
    {
        _service.CreateEtapa(new EtapaDTO
        {
            DT_INICIO_PREVISTO = DateTime.UtcNow,
            DT_TERMINO_PREVISTO = DateTime.UtcNow,
            NM_PROJETO = 1,
            PERCENT_TOTAL_ETAPA = 10,
            RESPONSAVEL_ETAPA = "TESTE",
            NM_ETAPA = "TESTE"

        });
        
        var resultado = await _service.GetSituacao();
        Assert.IsType<SituacaoProjetoDTO>(resultado);
    }

    [Fact]
    public void IniciarEtapaDeveTerDataPreenchid()
    {
        _service.CreateEtapa(new EtapaDTO
        {
            DT_INICIO_PREVISTO = DateTime.UtcNow,
            DT_TERMINO_PREVISTO = DateTime.UtcNow,
            NM_PROJETO = 1,
            PERCENT_TOTAL_ETAPA = 10,
            RESPONSAVEL_ETAPA = "TESTE",
            NM_ETAPA = "TESTE"

        });
        var etapa = _context.Etapas.FirstOrDefault();
        _service.IniciarEtapa(etapa.EtapaProjetoId, DateTime.UtcNow);
        Assert.NotNull(etapa.DT_INICIO_PREVISTO);
        Assert.NotNull(etapa.DT_TERMINO_PREVISTO);
    }
}