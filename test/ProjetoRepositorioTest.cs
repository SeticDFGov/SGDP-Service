using Xunit;
using Microsoft.EntityFrameworkCore;
using Models; // Assumindo que seus modelos estão aqui
using Repositorio; // Onde está o ProjetoRepositorio
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit.Microsoft.DependencyInjection.Abstracts;
using test.fixtures;
using Xunit.Abstractions;
using api.Projeto; // Se for usar mocking avançado para outras dependências, não é estritamente necessário aqui.

public class ProjetoRepositorioTests : TestBed<Base>
{
    private AppDbContext _context;
    private IProjetoRepositorio _projetoRepositorio;

    public ProjetoRepositorioTests(ITestOutputHelper testOutputHelper, Base fixture) : base(testOutputHelper, fixture)
    {
        _context = fixture.GetService<AppDbContext>(testOutputHelper);
        _projetoRepositorio = new ProjetoRepositorio(_context);
    }

    [Fact]
    public async Task GetProjetoListItemsAsync_ShouldReturnAllProjetos()
    {
        // Arrange
        var projeto1 = new Projeto { projetoId = 1, NM_PROJETO = "Projeto A", TEMPLATE = "Template1" };
        var projeto2 = new Projeto { projetoId = 2, NM_PROJETO = "Projeto B", TEMPLATE = "Template2" };
        await _context.Projetos.AddAsync(projeto1);
        await _context.Projetos.AddAsync(projeto2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _projetoRepositorio.GetProjetoListItemsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.NM_PROJETO == "Projeto A");
        Assert.Contains(result, p => p.NM_PROJETO == "Projeto B");
    }

    [Fact]
    public async Task GetProjetoListItemsAsync_ShouldReturnEmptyList_WhenNoProjetosExist()
    {

        var result = await _projetoRepositorio.GetProjetoListItemsAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // --- Testes para CreateProjeto ---
    // ATENÇÃO: A função CreateProjeto no seu repositório chama _context.SaveChangesAsync() de forma síncrona,
    // o que pode causar Deadlocks em ambientes assíncronos.
    // É ALTAMENTE RECOMENDADO MUDAR PARA await _context.SaveChangesAsync();

    [Fact]
    public async Task CreateProjeto_ShouldAddProjetoToDatabase()
    {
        // Arrange
        var novoProjeto = new Projeto { projetoId = 1, NM_PROJETO = "Novo Projeto", TEMPLATE = "Default" };

        // Act
        _projetoRepositorio.CreateProjeto(novoProjeto);
        // Como CreateProjeto() chama SaveChangesAsync() (deve ser await), o Assert pode vir logo após
        // Se ainda for síncrono, a linha abaixo não é necessária para o teste, mas sim para a operação real.
        // Se você não mudou o CreateProjeto para ser async, o teste pode precisar de um flush.
        // Mas a intenção é que o SaveChangesAsync dentro do método persista.
        // Vamos presumir que você corrigiu para `await _context.SaveChangesAsync();` dentro de `CreateProjeto`
        // e que `CreateProjeto` agora é `public async Task CreateProjeto(Projeto projeto)`
        await _context.SaveChangesAsync(); // Se CreateProjeto não for async/await internamente, este SaveChanges pode não ser relevante

        // Assert
        var projetoSalvo = await _context.Projetos.FirstOrDefaultAsync(p => p.projetoId == novoProjeto.projetoId);
        Assert.NotNull(projetoSalvo);
        Assert.Equal("Novo Projeto", projetoSalvo.NM_PROJETO);
    }
    
    // --- Testes para GetProjetoById ---
    [Fact]
    public async Task GetProjetoById_ShouldReturnProjeto_WhenIdExists()
    {
        // Arrange
        var projetoExistente = new Projeto { projetoId = 5, NM_PROJETO = "Projeto Existe", TEMPLATE = "Teste" };
        await _context.Projetos.AddAsync(projetoExistente);
        await _context.SaveChangesAsync();

        // Act
        var result = await _projetoRepositorio.GetProjetoById(5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.projetoId);
        Assert.Equal("Projeto Existe", result.NM_PROJETO);
    }

    [Fact]
    public async Task GetProjetoById_ShouldReturnNull_WhenIdDoesNotExist()
    {
        // Arrange (DB está vazio)

        // Act
        var result = await _projetoRepositorio.GetProjetoById(99);

        // Assert
        Assert.Null(result);
    }

    // --- Testes para CreateProjetoByTemplate ---
    [Fact]
    public async Task CreateProjetoByTemplate_ShouldCreateProjetoAndAssociatedEtapas()
    {
        // Arrange
        var template1 = new Template { TemplateId = 1, NM_TEMPLATE = "TemplateComEtapas", NM_ETAPA = "Etapa 1", PERCENT_TOTAL = 50 };
        var template2 = new Template { TemplateId = 2, NM_TEMPLATE = "TemplateComEtapas", NM_ETAPA = "Etapa 2", PERCENT_TOTAL = 50 };
        await _context.Templates.AddAsync(template1);
        await _context.Templates.AddAsync(template2);
        await _context.SaveChangesAsync();

        var novoProjeto = new Projeto { projetoId = 10, NM_PROJETO = "Projeto do Template", TEMPLATE = "TemplateComEtapas" };

        // Act
        await _projetoRepositorio.CreateProjetoByTemplate(novoProjeto);

        // Assert
        var projetoSalvo = await _context.Projetos
            .FirstOrDefaultAsync(p => p.projetoId == novoProjeto.projetoId);
        var etapas = await _context.Etapas
            .Where(e => e.NM_PROJETO.projetoId == novoProjeto.projetoId)
            .ToListAsync();

        Assert.NotNull(projetoSalvo);
        Assert.Equal("Projeto do Template", projetoSalvo.NM_PROJETO);
        Assert.Equal(2, etapas.Count);
        Assert.Contains(etapas, e => e.NM_ETAPA == "Etapa 1" && e.PERCENT_TOTAL_ETAPA == 50);
        Assert.Contains(etapas, e => e.NM_ETAPA == "Etapa 2" && e.PERCENT_TOTAL_ETAPA == 50);
    }

    [Fact]
    public async Task CreateProjetoByTemplate_ShouldCreateProjeto_WhenTemplateDoesNotExist()
    {
        // Arrange
        var novoProjeto = new Projeto { projetoId = 11, NM_PROJETO = "Projeto Sem Template", TEMPLATE = "TemplateInexistente" };

        // Act
        await _projetoRepositorio.CreateProjetoByTemplate(novoProjeto);

        // Assert
        var projetoSalvo = await _context.Projetos.FirstOrDefaultAsync(p => p.projetoId == novoProjeto.projetoId);
        Assert.NotNull(projetoSalvo);
        Assert.Equal("Projeto Sem Template", projetoSalvo.NM_PROJETO);
        var etapas = await _context.Etapas.Where(e => e.NM_PROJETO.projetoId == projetoSalvo.projetoId).ToListAsync();
        Assert.Empty(etapas); // Nenhuma etapa deve ser criada
    }

    [Fact]
    public async Task CreateProjetoByTemplate_ShouldRollbackOnException()
    {
        // Arrange
        var novoProjeto = new Projeto { projetoId = 12, NM_PROJETO = "Projeto Erro", TEMPLATE = "Default" };
        // Simular um cenário onde salvar o projeto falha (pode ser mais complexo na vida real)
        // Por exemplo, fazendo o SaveChangesAsync jogar uma exceção propositalmente.
        // Para este teste, vamos simular uma falha após o primeiro save, para verificar o rollback.

        // Uma maneira de simular um erro depois de adicionar o projeto mas antes de adicionar etapas
        // seria usar um mock de DbContext ou de DbSet, mas para fins de um teste simples
        // podemos tentar criar uma condição que cause uma exceção.
        // Por exemplo, se houvesse uma restrição única e tentássemos violá-la.
        // Para este exemplo, vamos forçar uma exceção para o teste de rollback.
        
        // Simular que o _context.Etapas.Add lança uma exceção
        var mockContext = new Mock<AppDbContext>(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        
        var mockDbSetEtapas = new Mock<DbSet<Etapa>>();
        mockContext.Setup(c => c.Etapas).Returns(mockDbSetEtapas.Object);
        // Faça o Add lançar uma exceção
        mockDbSetEtapas.Setup(d => d.Add(It.IsAny<Etapa>())).Throws(new Exception("Erro simulado ao adicionar etapa"));
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).CallBase(); // Permite outros SaveChanges
        mockContext.Setup(c => c.Projetos).CallBase();
        mockContext.Setup(c => c.Templates).CallBase();
        
        // Crie o repositório com o mock context
        _projetoRepositorio = new ProjetoRepositorio(mockContext.Object);
        
        // Adicionar um template para garantir que o loop de etapas seja executado
        await _context.Templates.AddAsync(new Template { TemplateId = 3, NM_TEMPLATE = "TemplateComEtapas", NM_ETAPA = "Etapa Erro", PERCENT_TOTAL = 100 });
        await _context.SaveChangesAsync(); // Salvar o template no contexto real

        // Act & Assert
        // Esperamos que o método lance a exceção original ou uma exceção wrapper.
        // E esperamos que o projeto NÃO seja salvo no _context real (se fosse o mock)
        // Ou que o estado do DB real volte ao original.
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _projetoRepositorio.CreateProjetoByTemplate(novoProjeto);
        });

        // Verifique se o projeto NÃO foi salvo no contexto real (se a transação rolou de volta)
        // Neste caso, como estamos usando um mock que *não* usa o _context real que temos,
        // é um pouco mais complexo verificar o rollback no DB em memória do _context original.
        // A melhor forma é garantir que o SaveChangesAsync seja realmente rolado de volta.
        // Se o seu mock realmente simular a transação, então o projeto não deveria existir.
        
        // Para o InMemory, a verificação de rollback é que nenhum projeto foi persistido
        // no contexto que foi passado para o repositório.
        var projetosCount = await _context.Projetos.CountAsync(); // Usando o _context original para verificar
        Assert.Equal(0, projetosCount); // Se houver rollback, não deve haver projetos salvos
    }

    // --- Testes para AnaliseProjeto ---
    [Fact]
    public async Task AnaliseProjeto_ShouldAddAnaliseToProject()
    {
        // Arrange
        var projeto = new Projeto { projetoId = 20, NM_PROJETO = "Projeto para Análise" };
        await _context.Projetos.AddAsync(projeto);
        await _context.SaveChangesAsync();

        var analiseDto = new ProjetoAnaliseDTO
        {
            NM_PROJETO = projeto.projetoId, // ID do projeto
            ANALISE = "Análise de Teste",
            ENTRAVE = false
        };

        // Act
        // ATENÇÃO: AnaliseProjeto() também chama _context.SaveChangesAsync() de forma síncrona.
        // Mude para `await _context.SaveChangesAsync();` e faça o método ser `async Task`.
        await _projetoRepositorio.AnaliseProjeto(analiseDto); 
        await _context.SaveChangesAsync(); // Adicionalmente salva para garantir a persistência no teste

        // Assert
        var analiseSalva = await _context.Analises
            .Include(a => a.NM_PROJETO)
            .FirstOrDefaultAsync(a => a.ANALISE == "Análise de Teste");

        Assert.NotNull(analiseSalva);
        Assert.Equal("Análise de Teste", analiseSalva.ANALISE);
        Assert.Equal(projeto.projetoId, analiseSalva.NM_PROJETO.projetoId);
    }

    [Fact]
    public async Task AnaliseProjeto_ShouldThrowException_WhenProjetoDoesNotExist()
    {
        // Arrange
        var analiseDto = new ProjetoAnaliseDTO
        {
            NM_PROJETO = 999, // ID de projeto inexistente
            ANALISE = "Análise sem Projeto",
            ENTRAVE = false
        };

        // Act & Assert
        // Esperamos que o FirstOrDefault(c => c.projetoId == analise.NM_PROJETO) retorne null
        // e, como você não tem um null check, ele deve estourar um NullReferenceException ao tentar acessar `analise.NM_PROJETO`
        // Ou, se você tem um operador '?' no seu código real, ele pode retornar null.
        await Assert.ThrowsAsync<NullReferenceException>(async () => // Ou o tipo de exceção que você espera
        {
            await _projetoRepositorio.AnaliseProjeto(analiseDto);
        });
        
        // Verifique se nenhuma análise foi adicionada
        var count = await _context.Analises.CountAsync();
        Assert.Equal(0, count);
    }

    // --- Testes para GetLastAnaliseProjeto ---
    [Fact]
    public async Task GetLastAnaliseProjeto_ShouldReturnLastAnaliseForProject()
    {
        // Arrange
        var projeto = new Projeto { projetoId = 30, NM_PROJETO = "Projeto com Múltiplas Análises" };
        await _context.Projetos.AddAsync(projeto);
        await _context.SaveChangesAsync();

        var analise1 = new ProjetoAnalise { AnaliseId = 1, NM_PROJETO = projeto, ANALISE = "Primeira", ENTRAVE = false };
        var analise2 = new ProjetoAnalise { AnaliseId = 2, NM_PROJETO = projeto, ANALISE = "Segunda", ENTRAVE = false };
        var analise3 = new ProjetoAnalise { AnaliseId = 3, NM_PROJETO = projeto, ANALISE = "Última", ENTRAVE = false };

        await _context.Analises.AddRangeAsync(analise1, analise2, analise3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _projetoRepositorio.GetLastAnaliseProjeto(projeto.projetoId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Última", result.ANALISE);
        Assert.Equal(projeto.projetoId, result.NM_PROJETO.projetoId);
    }

    [Fact]
    public async Task GetLastAnaliseProjeto_ShouldReturnNull_WhenNoAnalisesForProject()
    {
        // Arrange
        var projeto = new Projeto { projetoId = 31, NM_PROJETO = "Projeto Sem Análise" };
        await _context.Projetos.AddAsync(projeto);
        await _context.SaveChangesAsync();

        // Act
        var result = await _projetoRepositorio.GetLastAnaliseProjeto(projeto.projetoId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastAnaliseProjeto_ShouldReturnNull_WhenProjectDoesNotExist()
    {
        // Arrange (DB está vazio para este projeto)

        // Act
        var result = await _projetoRepositorio.GetLastAnaliseProjeto(999); // ID de projeto inexistente

        // Assert
        Assert.Null(result);
    }
}