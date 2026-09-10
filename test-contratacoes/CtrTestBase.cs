using api.Contratacoes;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Contratacoes;
using Repositorio.Contratacoes;
using service.Contratacoes;

namespace test.contratacoes;

/// <summary>
/// Base dos testes do módulo Análises de Contratações: AppDbContext InMemory com
/// um usuário por situação de acesso relevante (admin, analista com o papel,
/// básico sem papel e gestor sem papel) e helpers de fixture.
///
/// O provider InMemory ignora CHECKs e índices parciais (por isso o service valida
/// as mesmas regras); o índice único do número fica sob o flag indicesRelacionais.
/// </summary>
public abstract class CtrTestBase : IDisposable
{
    protected readonly AppDbContext Context;
    protected readonly CtrProcessoRepositorio Repositorio;
    protected readonly CtrPermissionService Permissoes;

    protected readonly Unidade UnidadeCentral;

    protected readonly User UserAdmin;      // perfil admin do SGDP, sem papel do módulo
    protected readonly User UserAnalista;   // basico + ctr_analise (o analista da SGDI)
    protected readonly User UserBasico;     // basico sem papel
    protected readonly User UserGestor;     // gestor sem papel

    private readonly Dictionary<string, string> PerfilPorEmail = new();

    protected CtrTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();

        UnidadeCentral = new Unidade { id = Guid.NewGuid(), Nome = "Unidade Central de Teste" };
        Context.Unidades.Add(UnidadeCentral);

        UserAdmin = NovoUser("admin@subgd.df.gov.br", "Ana Admin", Perfis.Admin, null);
        UserAnalista = NovoUser("contratacoes@local.teste", "Clara das Contratações",
            Perfis.Basico, PapeisContratacoes.Analise);
        UserBasico = NovoUser("basico@subgd.df.gov.br", "Bruno Básico", Perfis.Basico, null);
        UserGestor = NovoUser("gestor@subgd.df.gov.br", "Gina Gestora", Perfis.Gestor, null);

        Context.SaveChanges();

        Repositorio = new CtrProcessoRepositorio(Context);
        Permissoes = new CtrPermissionService(Context);
    }

    private User NovoUser(string email, string nome, string perfil, string? papelContratacoes)
    {
        var user = new User
        {
            Email = email,
            Nome = nome,
            PapelContratacoes = papelContratacoes,
            Unidade = UnidadeCentral
        };
        Context.Users.Add(user);
        PerfilPorEmail[email] = perfil;
        return user;
    }

    /// <summary>Perfil da persona (equivalente ao que um controller leria da claim do token).</summary>
    protected string PerfilDe(User user) => PerfilPorEmail[user.Email];

    /// <summary>Perfil da persona pelo e-mail (para testes parametrizados por [InlineData]).</summary>
    protected string PerfilDe(string email) => PerfilPorEmail[email];

    protected CtrProcessoService NovoProcessoService() => new(Repositorio);

    protected CtrManifestacaoService NovoManifestacaoService() => new(Repositorio);

    protected CtrImportacaoService NovoImportacaoService() => new(Repositorio);

    protected CtrAdminService NovoAdminService() => new(Context);

    /// <summary>Contexto do analista (o usuário típico do módulo).</summary>
    protected async Task<CtrUserContext> ContextoAnalistaAsync() =>
        (await Permissoes.GetContextAsync(UserAnalista.Email, PerfilDe(UserAnalista)))!;

    /// <summary>Hoje no fuso de Brasília — as validações recusam data futura.</summary>
    protected static DateOnly Hoje => CtrProcessoService.HojeBrasilia();

    protected static DateOnly DiasAtras(int dias) => Hoje.AddDays(-dias);

    /// <summary>
    /// DTO válido de processo, com o mínimo preenchido. Devolve o tipo de UPDATE,
    /// que herda o de create — serve para criar e para editar.
    /// </summary>
    protected static CtrProcessoUpdateDTO NovoProcessoDto(
        string numero = "04044-00002545/2024-62",
        string sigla = "SEEC",
        string categoria = CtrDominios.CategoriaObjeto.InfraestruturaRede) => new()
    {
        NumeroProcesso = numero,
        OrgaoNome = "Secretaria de Estado de Economia",
        OrgaoSigla = sigla,
        Objeto = "Aquisição de switches de acesso",
        CategoriaObjeto = categoria
    };

    /// <summary>Insere um processo direto no banco (sem passar pelo service).</summary>
    protected CtrProcesso SemearProcesso(string numero, Action<CtrProcesso>? ajustar = null)
    {
        var processo = new CtrProcesso
        {
            NumeroProcesso = numero,
            OrgaoNome = "Secretaria de Estado de Economia",
            OrgaoSigla = "SEEC",
            Objeto = "Aquisição de switches de acesso",
            CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = "contratacoes@local.teste"
        };
        ajustar?.Invoke(processo);
        Context.CtrProcessos.Add(processo);
        Context.SaveChanges();
        return processo;
    }

    /// <summary>Manifestação válida do inciso I (comunicada previamente).</summary>
    protected static CtrManifestacaoUpdateDTO NovaManifestacaoIncisoI(
        string resultado = CtrDominios.ResultadoAnalise.Alinhada) => new()
    {
        OficioTcdf = "123/2026-GAB",
        DataOficio = DiasAtras(5),
        SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
        ComunicadaDesde = DiasAtras(60),
        Criticidade = CtrDominios.Criticidade.Alta,
        ResultadoAnalise = resultado,
        DesfechoRisco = resultado == CtrDominios.ResultadoAnalise.RiscosSignificativos
            ? CtrDominios.DesfechoRisco.AguardandoResposta
            : null
    };

    /// <summary>Manifestação válida do inciso II (não comunicada previamente).</summary>
    protected static CtrManifestacaoUpdateDTO NovaManifestacaoIncisoII() => new()
    {
        OficioTcdf = "456/2026-GAB",
        DataOficio = DiasAtras(3),
        SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
        PrazoRegularizacaoDias = 30
    };

    /// <summary>Bytes de um CSV em Windows-1252, como a planilha real da equipe.</summary>
    protected static byte[] Bytes1252(string conteudo)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(1252).GetBytes(conteudo);
    }

    /// <summary>Bytes de um CSV em UTF-8 COM BOM (o que o nosso export escreve).</summary>
    protected static byte[] BytesUtf8ComBom(string conteudo) =>
        System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(conteudo)).ToArray();

    /// <summary>A planilha real da equipe, com os bytes originais (Windows-1252).</summary>
    protected static byte[] PlanilhaReal() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "planilha-analises-contratacoes.csv"));

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
