using app.Auth;
using Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Models.Planejamento;
using Repositorio.Pgia;
using service.Pgia;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Modo local (só Development): o login de teste com PapelPlanejamento grava papel,
/// concessão e histórico pelo mesmo serviço das telas; as personas de órgão ficam na
/// unidade SES com o órgão SES, e as globais na unidade central de teste.
/// </summary>
public class PeModoLocalTest : PeTestBase
{
    private AuthLocalController Controlador(bool modoLocal = true)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:ModoLocal"] = modoLocal ? "true" : "false" })
            .Build();
        return new AuthLocalController(
            configuracao,
            Options.Create(new AuthSettings { ClientId = "sgdp-teste" }),
            Context,
            new PgiaAdminService(new PgiaOrgaoRepositorio(Context), new PgiaPrazoRepositorio(Context), Context),
            Acessos);
    }

    private static AuthLocalController.LoginLocalRequest Login(string email, string? papel, List<string>? conceder = null) => new()
    {
        Email = email,
        Nome = "Pessoa de Teste",
        Perfil = Perfis.Basico,
        PapelPlanejamento = papel,
        ConcederModulos = conceder
    };

    private app.Models.User UsuarioDoModoLocal(string email) =>
        Context.Users.AsNoTracking().Include(u => u.Unidade)
            .Single(u => u.KeycloakId == ModoLocalTokens.SubjectDoEmail(email));

    [Fact]
    public async Task PapelDeOrgao_EntraNaSes_ComPapelConcessaoEHistorico()
    {
        const string email = "equipe.orgao@local.teste";

        Assert.IsType<OkObjectResult>(await Controlador().Token(Login(email, PapeisPlanejamento.Orgao)));

        var user = UsuarioDoModoLocal(email);
        Assert.Equal(UnidadeSes.id, user.Unidade!.id);
        Assert.Equal(PapeisPlanejamento.Orgao, PapelNoBanco(user)!.Papel);
        Assert.Equal("modo-local", PapelNoBanco(user)!.ConcedidoPor);
        Assert.Equal("modo-local", Assert.Single(ConcessoesDoModulo(user)).ConcedidoPor);

        var historico = Assert.Single(HistoricoDe(user));
        Assert.Equal(PeDominios.OrigemPapel.ModoLocal, historico.Origem);
        Assert.Equal(PapeisPlanejamento.Orgao, historico.PapelNovo);

        // meu-papel mostra o órgão SES
        var ctx = (await Permissoes.GetContextAsync(PrincipalDe(user.KeycloakId, email, Perfis.Basico)))!;
        Assert.Equal(OrgaoSes.Id, ctx.OrgaoId);
    }

    [Fact]
    public async Task PapelDeOrgao_SemOrgaoSes_CriaOOrgaoPeloServicoReal()
    {
        Context.PgiaOrgaos.Remove(OrgaoSes);
        Context.SaveChanges();

        Assert.IsType<OkObjectResult>(await Controlador().Token(Login("consulta@local.teste", PapeisPlanejamento.OrgaoConsulta)));

        var orgao = Context.PgiaOrgaos.AsNoTracking().Single(o => o.Ativo && o.Sigla == "SES");
        Assert.Equal("Secretaria de Estado de Saúde", orgao.Nome);
        Assert.Equal(UnidadeSes.id, orgao.UnidadeId);
        // Adesão do serviço real: o órgão nasce com os prazos do PGIA
        Assert.NotEmpty(Context.PgiaPrazosConformidade.AsNoTracking().Where(p => p.OrgaoId == orgao.Id));
        Assert.Equal(UnidadeSes.id, UsuarioDoModoLocal("consulta@local.teste").Unidade!.id);
    }

    [Theory]
    [InlineData(PapeisPlanejamento.Admin)]
    [InlineData(PapeisPlanejamento.Sgdi)]
    [InlineData(PapeisPlanejamento.Cgtic)]
    public async Task PapelGlobal_UsaAUnidadeCentral(string papel)
    {
        const string email = "global@local.teste";

        Assert.IsType<OkObjectResult>(await Controlador().Token(Login(email, papel)));

        var user = UsuarioDoModoLocal(email);
        Assert.Equal(UnidadeCentral.id, user.Unidade!.id);
        Assert.Equal(papel, PapelNoBanco(user)!.Papel);
    }

    [Fact]
    public async Task NovoLoginSemOCampo_NaoMexeNoPapel_ComOCampoTroca()
    {
        const string email = "admin.modulo@local.teste";
        await Controlador().Token(Login(email, PapeisPlanejamento.Admin));

        await Controlador().Token(Login(email, null));
        var user = UsuarioDoModoLocal(email);
        Assert.Equal(PapeisPlanejamento.Admin, PapelNoBanco(user)!.Papel);
        Assert.Single(HistoricoDe(user));

        // Com o módulo em ConcederModulos e sem papel: vale porque a pessoa já tem papel
        Assert.IsType<OkObjectResult>(await Controlador().Token(Login(email, null, new List<string> { ModulosSgdp.Planejamento })));
        Assert.Single(ConcessoesDoModulo(user));

        await Controlador().Token(Login(email, PapeisPlanejamento.Sgdi));
        Assert.Equal(PapeisPlanejamento.Sgdi, PapelNoBanco(user)!.Papel);
        Assert.Equal(2, HistoricoDe(user).Count);
    }

    [Fact]
    public async Task ConcederPlanejamentoSemPapel_ParaQuemNaoTem_400()
    {
        var resultado = await Controlador().Token(Login("sem.papel@local.teste", null, new List<string> { ModulosSgdp.Planejamento }));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.False(Context.Users.Any(u => u.Email == "sem.papel@local.teste"));
    }

    [Fact]
    public async Task PapelForaDoDominio_400_EModoLocalDesligado_404()
    {
        Assert.IsType<BadRequestObjectResult>(await Controlador().Token(Login("x@local.teste", "pgia_sgdi")));
        Assert.IsType<NotFoundResult>(await Controlador(modoLocal: false).Token(Login("x@local.teste", PapeisPlanejamento.Admin)));
        Assert.False(Context.Users.Any(u => u.Email == "x@local.teste"));
    }
}
