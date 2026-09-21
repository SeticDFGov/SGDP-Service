using System.Security.Claims;
using api.Acesso;
using app.Auth;
using app.Models;
using Controllers;
using Controllers.Contratacoes;
using Controllers.Pgia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Models.Acesso;
using Repositorio;
using Repositorio.Pgia;
using service;
using service.Acesso;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Isolamento real entre módulos: quem não tem acesso a um módulo não o enxerga
/// no front nem na API. O acesso soma o Keycloak (roles admin, gestor e "pgia" do
/// grupo) às concessões do sistema (acesso_modulo) e aos papéis de módulo; usuário
/// sem nenhuma dessas fontes não enxerga nada.
/// </summary>
public class AcessoModuloTest : PgiaTestBase
{
    private const string Autor = "admin@subgd.df.gov.br";

    private readonly AcessoModuloService _service;

    public AcessoModuloTest()
    {
        _service = new AcessoModuloService(Context);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>Principal como o pipeline do JWT monta: e-mail, sub e roles (perfil primeiro).</summary>
    private static ClaimsPrincipal Principal(string? email, params string[] roles) =>
        PrincipalComSub(null, email, roles);

    private static ClaimsPrincipal PrincipalComSub(string? keycloakId, string? email, params string[] roles)
    {
        var claims = new List<Claim>();
        if (keycloakId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, keycloakId));
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        foreach (var role in ModulosSgdp.RolesComPerfilPrimeiro(roles))
            claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"));
    }

    private void Conceder(User user, string modulo, string origem = OrigemAcesso.Sistema)
    {
        Context.AcessosModulo.Add(new AcessoModulo
        {
            UserId = user.Id,
            Modulo = modulo,
            Origem = origem,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = origem == OrigemAcesso.Keycloak ? OrigemAcesso.Keycloak : Autor
        });
        Context.SaveChanges();
    }

    private List<AcessoModulo> AcessosDe(User user) =>
        Context.AcessosModulo.AsNoTracking().Where(a => a.UserId == user.Id).ToList();

    private static object? Propriedade(object objeto, string nome) =>
        objeto.GetType().GetProperty(nome)!.GetValue(objeto);

    // ── Regra do acesso efetivo ───────────────────────────────────────────────

    [Fact]
    public void SemRoleSemPapelSemConcessao_NaoEnxergaModuloAlgum()
    {
        var modulos = AcessoModuloService.CalcularModulos(false, false, false, null, null, Array.Empty<string>());
        Assert.Empty(modulos);
    }

    [Fact]
    public void Admin_EnxergaTodosOsModulos_InclusiveAdministracao()
    {
        var modulos = AcessoModuloService.CalcularModulos(true, false, false, null, null, Array.Empty<string>());
        Assert.Equal(ModulosSgdp.Todos.OrderBy(m => m), modulos.OrderBy(m => m));
    }

    [Fact]
    public void Gestor_EnxergaSoDemandas()
    {
        var modulos = AcessoModuloService.CalcularModulos(false, true, false, null, null, Array.Empty<string>());
        Assert.Equal(new[] { ModulosSgdp.Demandas }, modulos);
    }

    [Fact]
    public void RolePgiaDoGrupo_EnxergaSoPgia()
    {
        var modulos = AcessoModuloService.CalcularModulos(false, false, true, null, null, Array.Empty<string>());
        Assert.Equal(new[] { ModulosSgdp.Pgia }, modulos);
    }

    [Theory]
    [InlineData(PapeisPgia.Orgao)]
    [InlineData(PapeisPgia.Sgdi)]
    [InlineData(PapeisPgia.Cgtic)]
    [InlineData(PapeisPgia.Auditoria)]
    public void PapelPgia_DaAcessoSoAoPgia(string papel)
    {
        var modulos = AcessoModuloService.CalcularModulos(false, false, false, papel, null, Array.Empty<string>());
        Assert.Equal(new[] { ModulosSgdp.Pgia }, modulos);
    }

    [Fact]
    public void PapelPgiaForaDoDominio_NaoDaAcesso()
    {
        var modulos = AcessoModuloService.CalcularModulos(false, false, false, "pgia_xpto", null, Array.Empty<string>());
        Assert.Empty(modulos);
    }

    [Theory]
    [InlineData(ModulosSgdp.Demandas)]
    [InlineData(ModulosSgdp.Pgia)]
    public void ConcessaoDoSistema_DaAcessoAoModulo(string modulo)
    {
        var modulos = AcessoModuloService.CalcularModulos(false, false, false, null, null, new[] { modulo });
        Assert.Equal(new[] { modulo }, modulos);
    }

    [Fact]
    public void PapelDeContratacoes_DaAcessoSoAContratacoes_EValorDesconhecidoNao()
    {
        Assert.Equal(new[] { ModulosSgdp.Contratacoes },
            AcessoModuloService.CalcularModulos(false, false, false, null, PapeisContratacoes.Analise, Array.Empty<string>()));
        Assert.Empty(AcessoModuloService.CalcularModulos(false, false, false, null, "ctr_xpto", Array.Empty<string>()));
    }

    [Fact]
    public void FontesSeSomam_SemDuplicar()
    {
        var modulos = AcessoModuloService.CalcularModulos(false, true, true, PapeisPgia.Orgao,
            PapeisContratacoes.Analise, new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia });
        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia, ModulosSgdp.Contratacoes }, modulos);
    }

    // ── Ordem das roles (perfil primeiro) ─────────────────────────────────────

    [Theory]
    [InlineData(new[] { "pgia", "gestor" }, new[] { "gestor", "pgia" })]
    [InlineData(new[] { "pgia" }, new[] { "basico", "pgia" })]
    [InlineData(new[] { "gestor", "admin" }, new[] { "admin", "gestor" })]
    [InlineData(new string[0], new[] { "basico" })]
    [InlineData(new[] { "pgia", "pgia", "basico" }, new[] { "basico", "pgia" })]
    public void RolesComPerfilPrimeiro_PoePerfilNaFrente(string[] roles, string[] esperado)
    {
        Assert.Equal(esperado, ModulosSgdp.RolesComPerfilPrimeiro(roles));
    }

    // ── Usuário da requisição (claims + banco) ────────────────────────────────

    [Fact]
    public async Task UsuarioSemRoleSemPapelSemConcessao_NaoEnxergaNada()
    {
        var modulos = await _service.ModulosDoPrincipalAsync(Principal(UserSemPapel.Email));
        Assert.Empty(modulos);
    }

    [Fact]
    public async Task PapelDoBancoSomaComRoleDoToken()
    {
        // Maria é pgia_orgao no banco e gestor no Keycloak
        var modulos = await _service.ModulosDoPrincipalAsync(Principal(UserOrgaoSes.Email, Perfis.Gestor));
        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia }, modulos);
    }

    [Fact]
    public async Task UsuarioAindaNaoCadastrado_SoOQueOTokenDa()
    {
        var modulos = await _service.ModulosDoPrincipalAsync(Principal("primeiro.login@df.gov.br", Perfis.Gestor));
        Assert.Equal(new[] { ModulosSgdp.Demandas }, modulos);
    }

    [Fact]
    public async Task ConcessaoDoSistema_ValeNaRequisicao()
    {
        Conceder(UserSemPapel, ModulosSgdp.Demandas);

        var modulos = await _service.ModulosDoPrincipalAsync(Principal(UserSemPapel.Email));
        Assert.Equal(new[] { ModulosSgdp.Demandas }, modulos);
    }

    [Fact]
    public async Task RetratoDoKeycloak_NaoAutorizaNada()
    {
        // O retrato é só informativo: a autorização lê as roles do próprio token
        Conceder(UserSemPapel, ModulosSgdp.Administracao, OrigemAcesso.Keycloak);
        Conceder(UserSemPapel, ModulosSgdp.Demandas, OrigemAcesso.Keycloak);

        var modulos = await _service.ModulosDoPrincipalAsync(Principal(UserSemPapel.Email));
        Assert.Empty(modulos);
    }

    [Fact]
    public async Task UsuarioEncontradoPeloKeycloakIdAntesDoEmail()
    {
        // Mesma ordem do GetOrCreateUserAsync: o sub decide quando os dois casam
        var peloSub = new User { KeycloakId = "kc-123", Nome = "Pelo Sub", Email = "sub@df.gov.br" };
        var peloEmail = new User { Nome = "Pelo E-mail", Email = "email@df.gov.br", PapelPgia = PapeisPgia.Sgdi };
        Context.Users.AddRange(peloSub, peloEmail);
        Context.SaveChanges();

        var modulos = await _service.ModulosDoPrincipalAsync(PrincipalComSub("kc-123", "email@df.gov.br"));
        Assert.Empty(modulos);
    }

    // ── Retrato do Keycloak ───────────────────────────────────────────────────

    [Fact]
    public async Task Retrato_GravaAsRolesQueAbremModulo_ESoReescreveQuandoMuda()
    {
        await _service.SincronizarRetratoKeycloakAsync(UserSemPapel.Id,
            Principal(UserSemPapel.Email, Perfis.Admin, ModulosSgdp.RolePgia));
        Assert.Equal(new[] { ModulosSgdp.Administracao, ModulosSgdp.Pgia },
            AcessosDe(UserSemPapel).Where(a => a.Origem == OrigemAcesso.Keycloak).Select(a => a.Modulo).OrderBy(m => m));

        // Perdeu admin e pgia no Keycloak, ganhou gestor
        await _service.SincronizarRetratoKeycloakAsync(UserSemPapel.Id, Principal(UserSemPapel.Email, Perfis.Gestor));
        var retrato = AcessosDe(UserSemPapel).Where(a => a.Origem == OrigemAcesso.Keycloak).ToList();
        Assert.Equal(new[] { ModulosSgdp.Demandas }, retrato.Select(a => a.Modulo));

        // Mesmo login de novo: nada muda (a linha é a mesma)
        await _service.SincronizarRetratoKeycloakAsync(UserSemPapel.Id, Principal(UserSemPapel.Email, Perfis.Gestor));
        Assert.Equal(retrato.Single().Id, AcessosDe(UserSemPapel).Single(a => a.Origem == OrigemAcesso.Keycloak).Id);
    }

    [Fact]
    public async Task Retrato_NaoMexeNasConcessoesDoSistema()
    {
        Conceder(UserSemPapel, ModulosSgdp.Demandas);

        await _service.SincronizarRetratoKeycloakAsync(UserSemPapel.Id, Principal(UserSemPapel.Email));

        Assert.Single(AcessosDe(UserSemPapel), a => a.Origem == OrigemAcesso.Sistema && a.Modulo == ModulosSgdp.Demandas);
    }

    // ── Gestão de acessos (tela do admin) ─────────────────────────────────────

    [Fact]
    public async Task DefinirAcessos_GravaConcessoesEPapeis()
    {
        var resposta = await _service.DefinirAcessosAsync(UserSemPapel.Id, new AcessoUsuarioUpdateDTO
        {
            Demandas = true,
            Pgia = true,
            PapelPgia = PapeisPgia.Sgdi,
            Contratacoes = true
        }, Autor);

        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia, ModulosSgdp.Contratacoes }, resposta.Modulos);
        Assert.Equal(PapeisPgia.Sgdi, resposta.PapelPgia);
        Assert.Equal(PapeisContratacoes.Analise, resposta.PapelContratacoes);

        var concessoes = AcessosDe(UserSemPapel).Where(a => a.Origem == OrigemAcesso.Sistema).ToList();
        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia }, concessoes.Select(a => a.Modulo).OrderBy(m => m));
        Assert.All(concessoes, a => Assert.Equal(Autor, a.ConcedidoPor));
    }

    [Fact]
    public async Task DefinirAcessos_DesligarPgia_TiraTambemOPapel()
    {
        var resposta = await _service.DefinirAcessosAsync(UserOrgaoSes.Id, new AcessoUsuarioUpdateDTO
        {
            Pgia = false,
            PapelPgia = null
        }, Autor);

        Assert.Null(resposta.PapelPgia);
        Assert.Empty(resposta.Modulos);
        Assert.Null(Context.Users.AsNoTracking().Single(u => u.Id == UserOrgaoSes.Id).PapelPgia);
    }

    [Fact]
    public async Task DefinirAcessos_PapelSemAcessoAoModulo_Recusa()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.DefinirAcessosAsync(UserSemPapel.Id,
            new AcessoUsuarioUpdateDTO { Pgia = false, PapelPgia = PapeisPgia.Orgao }, Autor));
        Assert.Equal((int)ErrorCode.AcessoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task DefinirAcessos_PapelForaDoDominio_Recusa()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.DefinirAcessosAsync(UserSemPapel.Id,
            new AcessoUsuarioUpdateDTO { Pgia = true, PapelPgia = "pgia_xpto" }, Autor));
        Assert.Equal((int)ErrorCode.AcessoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task DefinirAcessos_UsuarioInexistente()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.DefinirAcessosAsync(Guid.NewGuid(),
            new AcessoUsuarioUpdateDTO { Demandas = true }, Autor));
        Assert.Equal((int)ErrorCode.AcessoUsuarioNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task DefinirAcessos_RepetirNaoDuplicaNemRegravaAConcessao()
    {
        var dto = new AcessoUsuarioUpdateDTO { Demandas = true };
        await _service.DefinirAcessosAsync(UserSemPapel.Id, dto, Autor);
        var primeira = AcessosDe(UserSemPapel).Single();

        await _service.DefinirAcessosAsync(UserSemPapel.Id, dto, "outro.admin@df.gov.br");

        var atual = AcessosDe(UserSemPapel).Single();
        Assert.Equal(primeira.Id, atual.Id);
        Assert.Equal(Autor, atual.ConcedidoPor);
    }

    [Fact]
    public async Task DefinirAcessos_NaoMexeNaUnidadeNemNoRetrato()
    {
        Conceder(UserSemPapel, ModulosSgdp.Demandas, OrigemAcesso.Keycloak);

        await _service.DefinirAcessosAsync(UserSemPapel.Id, new AcessoUsuarioUpdateDTO(), Autor);

        var user = Context.Users.AsNoTracking().Include(u => u.Unidade).Single(u => u.Id == UserSemPapel.Id);
        Assert.Equal(UnidadeSes.id, user.Unidade!.id);
        Assert.Single(AcessosDe(UserSemPapel), a => a.Origem == OrigemAcesso.Keycloak);
    }

    [Fact]
    public async Task ListarUsuarios_FiltraPorNomeOuEmail_SemDiferenciarMaiusculas()
    {
        var porNome = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Filtro = "CARLOS" });
        Assert.Equal(new[] { UserSemPapel.Id }, porNome.Items.Select(u => u.Id));

        var porEmail = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Filtro = "@SGDI.df" });
        Assert.Equal(new[] { UserCgtic.Id, UserSgdi.Id }.OrderBy(id => id),
            porEmail.Items.Select(u => u.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ListarUsuarios_FiltraPorModulo_EPorQuemNaoTemNada()
    {
        Conceder(UserAdmin, ModulosSgdp.Administracao, OrigemAcesso.Keycloak);
        var agente = new User { Nome = "Agente com acesso", Email = "agente@df.gov.br" };
        Context.Users.Add(agente);
        Context.SaveChanges();
        Conceder(agente, ModulosSgdp.Pgia);

        var pgia = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Modulo = ModulosSgdp.Pgia, PageSize = 100 });
        var idsPgia = pgia.Items.Select(u => u.Id).ToHashSet();
        Assert.Contains(agente.Id, idsPgia);
        Assert.Contains(UserOrgaoSes.Id, idsPgia);   // papel PGIA
        Assert.Contains(UserAdmin.Id, idsPgia);      // admin vê tudo
        Assert.DoesNotContain(UserSemPapel.Id, idsPgia);

        var nenhum = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Modulo = AcessoModuloService.FiltroSemAcesso });
        Assert.Equal(new[] { UserSemPapel.Id }, nenhum.Items.Select(u => u.Id));
    }

    [Fact]
    public async Task ListarUsuarios_ModuloForaDoDominio_ListaVazia()
    {
        var resposta = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Modulo = "xpto" });
        Assert.Empty(resposta.Items);
        Assert.Equal(0, resposta.TotalItems);
    }

    [Fact]
    public async Task ListarUsuarios_PaginacaoSaneada()
    {
        var semTamanho = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { PageSize = 0 });
        Assert.Equal(20, semTamanho.PageSize);

        var paginaEnorme = await _service.ListarUsuariosAsync(new AcessoUsuariosConsulta { Page = int.MaxValue, PageSize = 100 });
        Assert.Empty(paginaEnorme.Items);
    }

    // ── Claims transformation e políticas ─────────────────────────────────────

    [Fact]
    public async Task Transformacao_AcrescentaOsModulosUmaVezSo()
    {
        var transformacao = new ModuloAcessoClaimsTransformation(_service);
        var principal = Principal(UserSemPapel.Email, Perfis.Gestor);

        principal = await transformacao.TransformAsync(principal);
        principal = await transformacao.TransformAsync(principal);

        Assert.Equal(new[] { ModulosSgdp.Demandas },
            principal.FindAll(ModulosSgdp.ClaimModulo).Select(c => c.Value));
    }

    [Fact]
    public async Task Transformacao_IgnoraRequisicaoAnonima()
    {
        var transformacao = new ModuloAcessoClaimsTransformation(_service);
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity());

        var resultado = await transformacao.TransformAsync(anonimo);

        Assert.Empty(resultado.FindAll(ModulosSgdp.ClaimModulo));
    }

    [Fact]
    public async Task Politica_ExigeAClaimDoModulo()
    {
        var servicos = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(ModulosSgdp.AdicionarPoliticas)
            .BuildServiceProvider();
        var autorizacao = servicos.GetRequiredService<IAuthorizationService>();

        var comDemandas = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ModulosSgdp.ClaimModulo, ModulosSgdp.Demandas) }, "Teste"));
        var semNada = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "Teste"));
        var anonimoComClaim = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ModulosSgdp.ClaimModulo, ModulosSgdp.Demandas) }));

        Assert.True((await autorizacao.AuthorizeAsync(comDemandas, ModulosSgdp.PoliticaDemandas)).Succeeded);
        Assert.False((await autorizacao.AuthorizeAsync(comDemandas, ModulosSgdp.PoliticaPgia)).Succeeded);
        Assert.False((await autorizacao.AuthorizeAsync(semNada, ModulosSgdp.PoliticaDemandas)).Succeeded);
        Assert.False((await autorizacao.AuthorizeAsync(anonimoComClaim, ModulosSgdp.PoliticaDemandas)).Succeeded);
    }

    /// <summary>
    /// Nenhum controller fica aberto a "qualquer autenticado": cada um exige a
    /// política do seu módulo ou o perfil admin. As únicas exceções são o /me
    /// (precisa responder a quem ainda não tem acesso, para a tela inicial), o modo
    /// local de testes e a transparência pública do PGIA. Controller novo fora desta
    /// lista derruba o teste — é a decisão de isolamento que ele obriga a tomar.
    /// </summary>
    [Fact]
    public void TodoControllerExigeOModuloOuOAdmin()
    {
        var esperado = new Dictionary<Type, string>
        {
            [typeof(DemandaController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(DashboardController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(EntregaveisController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(DemandanteController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(AreaExecutoraController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(EsteiraController)] = ModulosSgdp.PoliticaDemandas,
            [typeof(PgiaConformidadeController)] = ModulosSgdp.PoliticaPgia,
            [typeof(PgiaDocumentoController)] = ModulosSgdp.PoliticaPgia,
            [typeof(PgiaGovernancaController)] = ModulosSgdp.PoliticaPgia,
            [typeof(PgiaIncidenteController)] = ModulosSgdp.PoliticaPgia,
            [typeof(PgiaOrgaoController)] = ModulosSgdp.PoliticaPgia,
            [typeof(PgiaSistemaController)] = ModulosSgdp.PoliticaPgia,
            [typeof(CtrProcessoController)] = ModulosSgdp.PoliticaContratacoes,
            [typeof(CtrManifestacaoController)] = ModulosSgdp.PoliticaContratacoes,
            [typeof(CtrImportacaoController)] = ModulosSgdp.PoliticaContratacoes,
            [typeof(PgiaAdminController)] = "role:admin",
            [typeof(CtrAdminController)] = "role:admin",
            [typeof(AcessoController)] = "role:admin",
            [typeof(AuthController)] = "autenticado",
            // Pedir acesso é justamente para quem ainda não tem módulo algum
            [typeof(PedidoAcessoController)] = "autenticado",
            [typeof(AuthLocalController)] = "modo-local",
            [typeof(PgiaPublicoController)] = "publico"
        };

        var controllers = typeof(AcessoController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var semDecisao = controllers.Where(c => !esperado.ContainsKey(c)).Select(c => c.Name).ToList();
        Assert.True(semDecisao.Count == 0, "Controller sem regra de isolamento: " + string.Join(", ", semDecisao));

        foreach (var controller in controllers)
        {
            var autorizacoes = controller.GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>().ToList();
            var regra = esperado[controller];

            if (regra.StartsWith("modulo:"))
                Assert.Contains(autorizacoes, a => a.Policy == regra);
            else if (regra == "role:admin")
                Assert.Contains(autorizacoes, a => a.Roles == Perfis.Admin);
            else if (regra == "autenticado")
                Assert.Contains(autorizacoes, a => a.Policy == null && a.Roles == null);
            else
                Assert.Empty(autorizacoes);
        }
    }

    // ── GET /api/Auth/me ──────────────────────────────────────────────────────

    private AuthController ComoMe(ClaimsPrincipal principal)
    {
        var controller = new AuthController(
            new AuthRepositorio(Context), null!, Options.Create(new AuthSettings()), _service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    [Fact]
    public async Task Me_PrimeiroLoginSemAcesso_ModulosVazio()
    {
        var resultado = await ComoMe(PrincipalComSub("kc-novo", "novo.servidor@df.gov.br")).GetCurrentUser();

        var corpo = Assert.IsType<OkObjectResult>(resultado).Value!;
        Assert.Empty((List<string>)Propriedade(corpo, "Modulos")!);
        Assert.False((bool)Propriedade(corpo, "AcessoPgia")!);
        Assert.Equal(Perfis.Basico, Propriedade(corpo, "Perfil"));
    }

    [Fact]
    public async Task Me_SomaKeycloakEConcessoes_EGravaORetrato()
    {
        Conceder(UserSemPapel, ModulosSgdp.Pgia);

        var resultado = await ComoMe(PrincipalComSub("kc-carlos", UserSemPapel.Email, ModulosSgdp.RolePgia, Perfis.Gestor))
            .GetCurrentUser();

        var corpo = Assert.IsType<OkObjectResult>(resultado).Value!;
        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia }, (List<string>)Propriedade(corpo, "Modulos")!);
        Assert.True((bool)Propriedade(corpo, "AcessoPgia")!);
        Assert.Equal(Perfis.Gestor, Propriedade(corpo, "Perfil"));
        Assert.Equal(new[] { ModulosSgdp.Demandas, ModulosSgdp.Pgia },
            AcessosDe(UserSemPapel).Where(a => a.Origem == OrigemAcesso.Keycloak).Select(a => a.Modulo).OrderBy(m => m));
    }

    // ── Acesso de agente ao PGIA pela SGDI (Pessoas e acessos) ────────────────

    private PgiaGovernancaController ComoGovernanca(User quem)
    {
        var permissao = new PgiaPermissionService(Context);
        var controller = new PgiaGovernancaController(
            new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context)),
            new PgiaSistemaService(
                new PgiaSistemaRepositorio(Context),
                new PgiaOrgaoRepositorio(Context),
                new PgiaDesignacaoRepositorio(Context),
                permissao),
            new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context)),
            new PgiaAdminService(new PgiaOrgaoRepositorio(Context), new PgiaPrazoRepositorio(Context), Context),
            permissao,
            _service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = Principal(quem.Email, PerfilDe(quem)) }
        };
        return controller;
    }

    [Fact]
    public async Task Sgdi_LigaEDesligaOAcessoDeAgente_EAListaMostra()
    {
        var ligar = await ComoGovernanca(UserSgdi)
            .DefinirAcessoModuloPessoa(UserSemPapel.Id, new AcessoModuloToggleDTO { Ativo = true });
        Assert.IsType<OkObjectResult>(ligar);
        Assert.Contains(ModulosSgdp.Pgia, await _service.ModulosDoPrincipalAsync(Principal(UserSemPapel.Email)));

        var lista = Assert.IsType<OkObjectResult>(await ComoGovernanca(UserSgdi).ListarPessoas("carlos", null));
        var pessoa = Assert.Single((List<api.Pgia.PgiaPessoaAcesso>)lista.Value!);
        Assert.True(pessoa.AcessoModulo);
        Assert.False(pessoa.AcessoModuloKeycloak);

        await ComoGovernanca(UserSgdi)
            .DefinirAcessoModuloPessoa(UserSemPapel.Id, new AcessoModuloToggleDTO { Ativo = false });
        Assert.Empty(await _service.ModulosDoPrincipalAsync(Principal(UserSemPapel.Email)));
    }

    [Fact]
    public async Task Resumo_AdminDoKeycloakContaComoAcesso_ESemConcessao()
    {
        Conceder(UserAdmin, ModulosSgdp.Administracao, OrigemAcesso.Keycloak);
        Conceder(UserSemPapel, ModulosSgdp.Demandas);   // concessão de OUTRO módulo não conta

        var resumo = await _service.ResumoDoModuloAsync(ModulosSgdp.Pgia, new[] { UserAdmin.Id, UserSemPapel.Id });

        Assert.Equal((false, true), resumo[UserAdmin.Id]);
        Assert.Equal((false, false), resumo[UserSemPapel.Id]);
    }

    [Fact]
    public async Task PapelDeOrgao_NaoConcedeAcessoAoModulo()
    {
        var resultado = await ComoGovernanca(UserOrgaoSes)
            .DefinirAcessoModuloPessoa(UserSemPapel.Id, new AcessoModuloToggleDTO { Ativo = true });

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(AcessosDe(UserSemPapel));
    }

    [Fact]
    public async Task Sgdi_PessoaInexistente_404()
    {
        var resultado = await ComoGovernanca(UserSgdi)
            .DefinirAcessoModuloPessoa(Guid.NewGuid(), new AcessoModuloToggleDTO { Ativo = true });

        Assert.IsType<NotFoundObjectResult>(resultado);
    }
}
