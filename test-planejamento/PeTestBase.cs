using System.Security.Claims;
using app.Auth;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Acesso;
using Models.Pgia;
using Models.Planejamento;
using service.Acesso;
using service.Planejamento;

namespace test.planejamento;

/// <summary>
/// Base dos testes do módulo Governança Estratégica: AppDbContext InMemory com dois
/// órgãos (SES e SEEC, cada um ligado à sua unidade), uma unidade central sem órgão e
/// uma pessoa por papel do módulo, mais o admin geral do SGDP (sem papel), uma pessoa
/// sem papel e uma sem unidade. Os papéis da semente são gravados como o serviço grava:
/// papel em pe_papel_usuario e concessão do módulo em acesso_modulo.
///
/// O provider InMemory ignora CHECKs e índices; as regras que importam são validadas no
/// serviço (e o script da migration é conferido à parte, no PeMigrationTest).
/// </summary>
public abstract class PeTestBase : IDisposable
{
    protected const string AutorSemente = "semente@sgdi.df.gov.br";

    protected readonly AppDbContext Context;
    protected readonly string NomeBanco;

    protected readonly AcessoModuloService Acessos;
    protected readonly PePermissionService Permissoes;
    protected readonly PePessoaService Service;

    protected readonly Unidade UnidadeSes;
    protected readonly Unidade UnidadeSeec;
    protected readonly Unidade UnidadeCentral;
    protected readonly PgiaOrgao OrgaoSes;
    protected readonly PgiaOrgao OrgaoSeec;

    protected readonly User UserPeAdmin;       // pe_admin, unidade central (sem órgão)
    protected readonly User UserPeSgdi;        // pe_sgdi, unidade central
    protected readonly User UserPeCgtic;       // pe_cgtic, unidade central
    protected readonly User UserOrgaoSes;      // pe_orgao na SES
    protected readonly User UserConsultaSes;   // pe_orgao_consulta na SES
    protected readonly User UserAdminGeral;    // perfil admin do SGDP, sem papel no módulo
    protected readonly User UserSemPapel;      // já entrou no SGDP, SEEC, sem papel
    protected readonly User UserSemUnidade;    // já entrou no SGDP, sem unidade, sem papel

    // Perfil nunca é persistido (vem da claim do token a cada requisição)
    private readonly Dictionary<Guid, string> _perfis = new();
    protected readonly Dictionary<string, User> UserPorEmail = new();

    protected PeTestBase()
    {
        NomeBanco = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: NomeBanco)
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();

        UnidadeSes = new Unidade { id = Guid.NewGuid(), Nome = "SES", CodigoExterno = "SES" };
        UnidadeSeec = new Unidade { id = Guid.NewGuid(), Nome = "SEEC", CodigoExterno = "SEEC" };
        UnidadeCentral = new Unidade { id = Guid.NewGuid(), Nome = "Unidade Central de Teste" };
        Context.Unidades.AddRange(UnidadeSes, UnidadeSeec, UnidadeCentral);

        OrgaoSes = NovoOrgao("SES", "Secretaria de Estado de Saúde", UnidadeSes);
        OrgaoSeec = NovoOrgao("SEEC", "Secretaria de Estado de Economia", UnidadeSeec);

        UserPeAdmin = NovoUser("paula.admin@sgdi.df.gov.br", "Paula Administradora", Perfis.Basico, UnidadeCentral);
        UserPeSgdi = NovoUser("sergio@sgdi.df.gov.br", "Sérgio da SGDI", Perfis.Basico, UnidadeCentral);
        UserPeCgtic = NovoUser("carla@cgtic.df.gov.br", "Carla do CGTIC", Perfis.Basico, UnidadeCentral);
        UserOrgaoSes = NovoUser("otavio@saude.df.gov.br", "Otávio da Saúde", Perfis.Basico, UnidadeSes);
        UserConsultaSes = NovoUser("cecilia@saude.df.gov.br", "Cecília Consulta", Perfis.Basico, UnidadeSes);
        UserAdminGeral = NovoUser("admin@subgd.df.gov.br", "Ana Admin", Perfis.Admin, UnidadeCentral);
        UserSemPapel = NovoUser("bruno@economia.df.gov.br", "Bruno Sem Papel", Perfis.Gestor, UnidadeSeec);
        UserSemUnidade = NovoUser("davi@df.gov.br", "Davi Sem Unidade", Perfis.Basico, null);
        Context.SaveChanges();

        DarPapel(UserPeAdmin, PapeisPlanejamento.Admin);
        DarPapel(UserPeSgdi, PapeisPlanejamento.Sgdi);
        DarPapel(UserPeCgtic, PapeisPlanejamento.Cgtic);
        DarPapel(UserOrgaoSes, PapeisPlanejamento.Orgao);
        DarPapel(UserConsultaSes, PapeisPlanejamento.OrgaoConsulta);

        Acessos = new AcessoModuloService(Context);
        Permissoes = new PePermissionService(Context);
        Service = new PePessoaService(Context, Acessos, Permissoes);
    }

    private PgiaOrgao NovoOrgao(string sigla, string nome, Unidade unidade)
    {
        var orgao = new PgiaOrgao
        {
            Sigla = sigla,
            Nome = nome,
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaOrgaos.Add(orgao);
        return orgao;
    }

    /// <summary>Usuário que já entrou no SGDP (tem KeycloakId).</summary>
    protected User NovoUser(string email, string nome, string perfil, Unidade? unidade, bool jaEntrou = true)
    {
        var user = new User
        {
            KeycloakId = jaEntrou ? "kc-" + email : null,
            Email = email,
            Nome = nome,
            Unidade = unidade
        };
        Context.Users.Add(user);
        _perfis[user.Id] = perfil;
        UserPorEmail[email] = user;
        return user;
    }

    /// <summary>Papel e concessão do módulo, gravados juntos (como o serviço grava).</summary>
    protected void DarPapel(User user, string papel)
    {
        Context.PePapeisUsuario.Add(new PePapelUsuario
        {
            UserId = user.Id,
            Papel = papel,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = AutorSemente
        });
        Context.AcessosModulo.Add(new AcessoModulo
        {
            UserId = user.Id,
            Modulo = ModulosSgdp.Planejamento,
            Origem = OrigemAcesso.Sistema,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = AutorSemente
        });
        Context.SaveChanges();
    }

    /// <summary>Retrato do Keycloak de quem é admin geral (gravado no /me).</summary>
    protected void RetratoAdmin(User user)
    {
        Context.AcessosModulo.Add(new AcessoModulo
        {
            UserId = user.Id,
            Modulo = ModulosSgdp.Administracao,
            Origem = OrigemAcesso.Keycloak,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = OrigemAcesso.Keycloak
        });
        Context.SaveChanges();
    }

    protected string PerfilDe(User user) => _perfis[user.Id];

    /// <summary>
    /// Principal como o pipeline monta: sub, e-mail, roles (perfil primeiro) e a claim do
    /// módulo que a ModuloAcessoClaimsTransformation acrescenta a quem o tem.
    /// </summary>
    protected ClaimsPrincipal Principal(User user) => PrincipalDe(user.KeycloakId, user.Email, PerfilDe(user));

    protected static ClaimsPrincipal PrincipalDe(string? keycloakId, string? email, string perfil)
    {
        var claims = new List<Claim>();
        if (keycloakId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, keycloakId));
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        foreach (var role in ModulosSgdp.RolesComPerfilPrimeiro(new[] { perfil }))
            claims.Add(new Claim(ClaimTypes.Role, role));
        claims.Add(new Claim(ModulosSgdp.ClaimModulo, ModulosSgdp.Planejamento));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"));
    }

    protected async Task<PeUserContext> ContextoDe(User user) =>
        (await Permissoes.GetContextAsync(Principal(user)))!;

    protected PePessoasController Controlador(User user) => ControladorDe(Principal(user));

    protected PePessoasController ControladorDe(ClaimsPrincipal principal) => new(Service, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
    };

    protected PePapelUsuario? PapelNoBanco(User user) =>
        Context.PePapeisUsuario.AsNoTracking().SingleOrDefault(p => p.UserId == user.Id);

    protected List<PePapelUsuarioHistorico> HistoricoDe(User user) =>
        Context.PePapeisUsuarioHistorico.AsNoTracking()
            .Where(h => h.UserId == user.Id)
            .OrderBy(h => h.Id)
            .ToList();

    protected List<AcessoModulo> ConcessoesDoModulo(User user) =>
        Context.AcessosModulo.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.Modulo == ModulosSgdp.Planejamento && a.Origem == OrigemAcesso.Sistema)
            .ToList();

    /// <summary>Código do { Code, Message } devolvido pelo controller.</summary>
    protected static int CodigoDe(object? corpo) =>
        (int)corpo!.GetType().GetProperty("Code")!.GetValue(corpo)!;

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
