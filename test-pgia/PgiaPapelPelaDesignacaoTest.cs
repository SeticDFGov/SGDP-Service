using System.Security.Claims;
using api.Auth;
using app.Auth;
using app.Models;
using Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Models.Pgia;
using Repositorio;
using service.Acesso;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Papel de órgão pela designação vigente (carga da planilha da SGDI pelo Keycloak):
/// a pessoa entra no PGIA pela role pgia do Keycloak, e quem é Responsável de IA,
/// Encarregado de Dados ou Curador de Dados de Negócio vigente no órgão da própria
/// unidade age como pgia_orgao sem nada gravado em Users.PapelPgia.
/// </summary>
public class PgiaPapelPelaDesignacaoTest : PgiaTestBase
{
    private const string Titular = PgiaDominios.PosicaoDesignacao.Titular;
    private const string Suplente = PgiaDominios.PosicaoDesignacao.Suplente;

    private readonly PgiaPermissionService _permissionService;

    // Pessoa da carga: linha em Users com o KeycloakId que o script do Keycloak devolveu,
    // a unidade do grupo e nenhum papel gravado
    private readonly User _daCarga;

    public PgiaPapelPelaDesignacaoTest()
    {
        _permissionService = new PgiaPermissionService(Context);

        _daCarga = new User
        {
            Email = "maria.souza@saude.df.gov.br",
            Nome = "Maria Souza",
            KeycloakId = "kc-maria",
            Unidade = UnidadeSes
        };
        Context.Users.Add(_daCarga);
        Context.SaveChanges();
    }

    private T Designar<T>(T designacao, PgiaOrgao orgao, User agente, string posicao = Titular, bool ativo = true)
        where T : class, IPgiaDesignacao
    {
        designacao.OrgaoId = orgao.Id;
        designacao.AgenteId = agente.Id;
        designacao.Posicao = posicao;
        designacao.AtoPendente = true;
        designacao.Ativo = ativo;
        designacao.CriadoEm = DateTime.UtcNow;
        designacao.CriadoPor = "carga-planilha-sgdi";
        Context.Set<T>().Add(designacao);
        Context.SaveChanges();
        return designacao;
    }

    private Task<PgiaUserContext?> Contexto(User user) =>
        _permissionService.GetContextAsync(user.Email, Perfis.Basico);

    private AuthController Me(ClaimsPrincipal principal) =>
        new(new AuthRepositorio(Context), null!, Options.Create(new AuthSettings()),
            new AcessoModuloService(Context), _permissionService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

    private static ClaimsPrincipal Login(string keycloakId, string email, string? grupo, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, keycloakId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, "Maria Souza")
        };
        foreach (var role in ModulosSgdp.RolesComPerfilPrimeiro(roles))
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (grupo != null) claims.Add(new Claim("unidade_codigo", grupo));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"));
    }

    private static object? Propriedade(object objeto, string nome) =>
        objeto.GetType().GetProperty(nome)!.GetValue(objeto);

    // ── Regra ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, false, null)]
    [InlineData(null, true, PapeisPgia.Orgao)]
    [InlineData(PapeisPgia.Sgdi, true, PapeisPgia.Sgdi)]
    [InlineData(PapeisPgia.Auditoria, false, PapeisPgia.Auditoria)]
    public void Efetivo_GravadoPrimeiroDepoisADesignacao(string? gravado, bool designado, string? esperado)
    {
        Assert.Equal(esperado, PapeisPgia.Efetivo(gravado, designado));
    }

    // ── Contexto do PGIA ──────────────────────────────────────────────────────

    [Fact]
    public async Task SemDesignacao_ContinuaSemPapel()
    {
        var ctx = await Contexto(_daCarga);

        Assert.NotNull(ctx);
        Assert.Null(ctx!.PapelPgia);
        Assert.Equal(OrgaoSes.Id, ctx.OrgaoId);
        Assert.False(_permissionService.CanView(ctx, PgiaResources.Sistema));
    }

    public static IEnumerable<object[]> Designacoes() => new[]
    {
        new object[] { new PgiaResponsavelIa(), Titular },
        new object[] { new PgiaResponsavelIa(), Suplente },
        new object[] { new PgiaEncarregadoDados(), Titular },
        new object[] { new PgiaEncarregadoDados(), Suplente },
        new object[] { new PgiaCuradorDados(), Titular },
        new object[] { new PgiaCuradorDados(), Suplente }
    };

    [Theory]
    [MemberData(nameof(Designacoes))]
    public async Task DesignacaoVigenteNoProprioOrgao_AgeComoPapelDeOrgao(IPgiaDesignacao designacao, string posicao)
    {
        switch (designacao)
        {
            case PgiaResponsavelIa r: Designar(r, OrgaoSes, _daCarga, posicao); break;
            case PgiaEncarregadoDados e: Designar(e, OrgaoSes, _daCarga, posicao); break;
            case PgiaCuradorDados c: Designar(c, OrgaoSes, _daCarga, posicao); break;
        }

        var ctx = (await Contexto(_daCarga))!;

        Assert.Equal(PapeisPgia.Orgao, ctx.PapelEfetivo);
        Assert.True(_permissionService.CanView(ctx, PgiaResources.Sistema));
        Assert.True(_permissionService.CanEdit(ctx, PgiaResources.Designacao));
        Assert.True(_permissionService.CanAccessOrgao(ctx, OrgaoSes.Id));
        Assert.False(_permissionService.CanAccessOrgao(ctx, OrgaoSeec.Id));

        // Nada foi gravado no banco: o papel é só calculado
        Assert.Null(Context.Users.Single(u => u.Id == _daCarga.Id).PapelPgia);
    }

    [Fact]
    public async Task DesignacaoEncerrada_NaoDaPapel()
    {
        Designar(new PgiaResponsavelIa(), OrgaoSes, _daCarga, ativo: false);

        Assert.Null((await Contexto(_daCarga))!.PapelPgia);
    }

    [Fact]
    public async Task DesignacaoEmOutroOrgao_NaoDaPapel()
    {
        // A pessoa está na unidade da SES (grupo do Keycloak) e a designação é da SEEC:
        // o papel nunca abre o escopo de um órgão que não é o da própria unidade
        Designar(new PgiaEncarregadoDados(), OrgaoSeec, _daCarga);

        Assert.Null((await Contexto(_daCarga))!.PapelPgia);
        Assert.Null(await _permissionService.PapelEfetivoAsync(_daCarga.Id, null, UnidadeSes.id));
    }

    [Fact]
    public async Task OrgaoDesativado_NaoDaPapel()
    {
        Designar(new PgiaResponsavelIa(), OrgaoSes, _daCarga);
        OrgaoSes.Ativo = false;
        Context.SaveChanges();

        Assert.Null((await Contexto(_daCarga))!.PapelPgia);
    }

    [Fact]
    public async Task PapelGravado_ValeSobreADesignacao()
    {
        Designar(new PgiaResponsavelIa(), OrgaoSes, UserSgdi);

        Assert.Equal(PapeisPgia.Sgdi, (await Contexto(UserSgdi))!.PapelPgia);
        Assert.Equal(PapeisPgia.Sgdi, await _permissionService.PapelEfetivoAsync(UserSgdi.Id, UserSgdi.PapelPgia, UnidadeSes.id));
    }

    [Fact]
    public async Task SemUnidade_NaoDaPapel()
    {
        Assert.Null(await _permissionService.PapelEfetivoAsync(_daCarga.Id, null, null));
    }

    // ── GET /api/Auth/me (primeiro login de quem veio da carga) ─────────────────

    [Fact]
    public async Task PrimeiroLogin_ComRolePgia_EntraNoPgiaComPapelDeOrgao_SemPedirAcesso()
    {
        UnidadeSes.CodigoExterno = "SES";
        Context.SaveChanges();
        Designar(new PgiaResponsavelIa(), OrgaoSes, _daCarga);

        var corpo = Assert.IsType<OkObjectResult>(
            await Me(Login("kc-maria", _daCarga.Email, "SES", Perfis.Basico, ModulosSgdp.RolePgia)).GetCurrentUser()).Value!;

        Assert.Equal(PapeisPgia.Orgao, Propriedade(corpo, "PapelPgia"));
        Assert.True((bool)Propriedade(corpo, "AcessoPgia")!);
        Assert.Contains(ModulosSgdp.Pgia, (List<string>)Propriedade(corpo, "Modulos")!);
        Assert.Null(Context.Users.Single(u => u.Id == _daCarga.Id).PapelPgia);
    }

    [Fact]
    public async Task SemARolePgia_NaoEntraNoPgia_MesmoDesignada()
    {
        // O acesso ao módulo é do Keycloak: tirar a role tira o PGIA
        Designar(new PgiaResponsavelIa(), OrgaoSes, _daCarga);

        var corpo = Assert.IsType<OkObjectResult>(
            await Me(Login("kc-maria", _daCarga.Email, null, Perfis.Basico)).GetCurrentUser()).Value!;

        Assert.False((bool)Propriedade(corpo, "AcessoPgia")!);
        Assert.DoesNotContain(ModulosSgdp.Pgia, (List<string>)Propriedade(corpo, "Modulos")!);
    }

    [Fact]
    public async Task LinhaDaCargaComKeycloakId_EhAMesmaPessoaNoLogin()
    {
        Designar(new PgiaCuradorDados(), OrgaoSes, _daCarga);
        var antes = Context.Users.Count();

        var corpo = Assert.IsType<OkObjectResult>(
            await Me(Login("kc-maria", _daCarga.Email, null, Perfis.Basico, ModulosSgdp.RolePgia)).GetCurrentUser()).Value!;

        Assert.Equal(antes, Context.Users.Count());
        Assert.Equal(_daCarga.Id, Propriedade(corpo, "Id"));
        Assert.Equal(PapeisPgia.Orgao, Propriedade(corpo, "PapelPgia"));
    }
}
