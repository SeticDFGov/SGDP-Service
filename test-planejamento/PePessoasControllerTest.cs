using System.Reflection;
using api.Common;
using api.Planejamento;
using app.Auth;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Contrato HTTP de api/planejamento que o front consome: rotas, política do módulo,
/// quem recebe 403 e os erros como { Code, Message } (400, 403, 404 e 409), nunca
/// um 500 sem corpo.
/// </summary>
public class PePessoasControllerTest : PeTestBase
{
    // ── Contrato ──────────────────────────────────────────────────────────────

    [Fact]
    public void Controller_ExigeAPoliticaDoModulo_NaRotaDoModulo()
    {
        var tipo = typeof(PePessoasController);

        var autorizacao = Assert.Single(tipo.GetCustomAttributes<AuthorizeAttribute>(false));
        Assert.Equal(ModulosSgdp.PoliticaPlanejamento, autorizacao.Policy);
        Assert.Equal("api/planejamento", tipo.GetCustomAttribute<RouteAttribute>()!.Template);

        // Nenhuma action foge da política
        Assert.DoesNotContain(tipo.GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Theory]
    [InlineData(nameof(PePessoasController.MeuPapel), "GET", "meu-papel")]
    [InlineData(nameof(PePessoasController.ListarPessoas), "GET", "pessoas")]
    [InlineData(nameof(PePessoasController.ListarCandidatas), "GET", "pessoas/candidatas")]
    [InlineData(nameof(PePessoasController.DefinirPapel), "PUT", "pessoas/{userId:guid}/papel")]
    public void Rotas_DoContrato(string action, string metodo, string rota)
    {
        var atributo = typeof(PePessoasController).GetMethod(action)!.GetCustomAttribute<HttpMethodAttribute>()!;

        Assert.Equal(new[] { metodo }, atributo.HttpMethods);
        Assert.Equal(rota, atributo.Template);
    }

    [Fact]
    public void ModeloDeQuery_NaoTemPropriedadeComONomeDoParametro()
    {
        // Armadilha do model binding (CtrBindingQueryTest): parâmetro com o nome de uma
        // propriedade do modelo faz o binder ignorar todos os filtros em silêncio
        var parametro = typeof(PePessoasController).GetMethod(nameof(PePessoasController.ListarPessoas))!
            .GetParameters().Single();

        Assert.DoesNotContain(typeof(PePessoasConsulta).GetProperties(),
            p => string.Equals(p.Name, parametro.Name, StringComparison.OrdinalIgnoreCase));
    }

    // ── meu-papel ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br", PapeisPlanejamento.Admin)]
    [InlineData("sergio@sgdi.df.gov.br", PapeisPlanejamento.Sgdi)]
    [InlineData("carla@cgtic.df.gov.br", PapeisPlanejamento.Cgtic)]
    [InlineData("otavio@saude.df.gov.br", PapeisPlanejamento.Orgao)]
    [InlineData("cecilia@saude.df.gov.br", PapeisPlanejamento.OrgaoConsulta)]
    public async Task MeuPapel_QualquerPapel_200(string email, string papel)
    {
        var resultado = await Controlador(UserPorEmail[email]).MeuPapel();

        var meu = Assert.IsType<PeMeuPapelResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(papel, meu.Papel);
    }

    [Fact]
    public async Task MeuPapel_AdminGeral_200()
    {
        var meu = (PeMeuPapelResponse)Assert.IsType<OkObjectResult>(await Controlador(UserAdminGeral).MeuPapel()).Value!;

        Assert.True(meu.EhAdminGeral);
        Assert.Null(meu.Papel);
    }

    [Fact]
    public async Task CadastroNaoEncontrado_404ComCodigo()
    {
        var desconhecido = ControladorDe(PrincipalDe("kc-x", "ninguem@df.gov.br", Perfis.Basico));

        var resultado = Assert.IsType<NotFoundObjectResult>(await desconhecido.MeuPapel());
        Assert.Equal((int)ErrorCode.PeUsuarioNaoEncontrado, CodigoDe(resultado.Value));
        Assert.IsType<NotFoundObjectResult>(await desconhecido.ListarPessoas(new PePessoasConsulta()));
    }

    // ── Quem gere pessoas ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("sergio@sgdi.df.gov.br")]     // pe_sgdi
    [InlineData("carla@cgtic.df.gov.br")]     // pe_cgtic
    [InlineData("otavio@saude.df.gov.br")]    // pe_orgao
    [InlineData("cecilia@saude.df.gov.br")]   // pe_orgao_consulta
    [InlineData("bruno@economia.df.gov.br")]  // com a claim do módulo, mas sem papel
    public async Task Pessoas_QuemNaoEAdmin_Recebe403ComCodigo(string email)
    {
        var controlador = Controlador(UserPorEmail[email]);

        foreach (var resultado in new[]
                 {
                     await controlador.ListarPessoas(new PePessoasConsulta()),
                     await controlador.ListarCandidatas("bruno"),
                     await controlador.DefinirPapel(UserSemPapel.Id, new PePapelDTO { Papel = PapeisPlanejamento.Orgao })
                 })
        {
            var objeto = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(403, objeto.StatusCode);
            Assert.Equal((int)ErrorCode.PeSemPermissao, CodigoDe(objeto.Value));
        }

        Assert.Null(PapelNoBanco(UserSemPapel));
    }

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br")]  // pe_admin
    [InlineData("admin@subgd.df.gov.br")]       // admin geral, sem papel
    public async Task Pessoas_AdminDoModuloEAdminGeral_Recebem200(string email)
    {
        var controlador = Controlador(UserPorEmail[email]);

        var lista = Assert.IsType<OkObjectResult>(await controlador.ListarPessoas(new PePessoasConsulta()));
        Assert.Equal(5, ((PagedResponse<PePessoaResponse>)lista.Value!).TotalItems);

        var candidatas = Assert.IsType<OkObjectResult>(await controlador.ListarCandidatas("bruno"));
        Assert.Single((List<PeCandidataResponse>)candidatas.Value!);

        var definido = Assert.IsType<OkObjectResult>(
            await controlador.DefinirPapel(UserSemPapel.Id, new PePapelDTO { Papel = PapeisPlanejamento.Cgtic }));
        Assert.Equal(PapeisPlanejamento.Cgtic, ((PePessoaResponse)definido.Value!).Papel);
    }

    // ── Erros do PUT ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DefinirPapel_ErrosVoltamComCodigoEMensagem()
    {
        var admin = Controlador(UserPeAdmin);

        var invalido = Assert.IsType<BadRequestObjectResult>(
            await admin.DefinirPapel(UserSemPapel.Id, new PePapelDTO { Papel = "pgia_sgdi" }));
        Assert.Equal((int)ErrorCode.PePapelInvalido, CodigoDe(invalido.Value));

        var inexistente = Assert.IsType<NotFoundObjectResult>(
            await admin.DefinirPapel(Guid.NewGuid(), new PePapelDTO { Papel = PapeisPlanejamento.Orgao }));
        Assert.Equal((int)ErrorCode.PeUsuarioNaoEncontrado, CodigoDe(inexistente.Value));

        var rebaixamento = Assert.IsType<ConflictObjectResult>(
            await admin.DefinirPapel(UserPeAdmin.Id, new PePapelDTO { Papel = null }));
        Assert.Equal((int)ErrorCode.PeAutoRebaixamento, CodigoDe(rebaixamento.Value));
        var mensagem = (string)rebaixamento.Value!.GetType().GetProperty("Message")!.GetValue(rebaixamento.Value)!;
        Assert.Contains("próprio papel", mensagem);
    }

    [Fact]
    public async Task DefinirPapel_Retirar_200ComPapelNulo()
    {
        var resultado = Assert.IsType<OkObjectResult>(
            await Controlador(UserPeAdmin).DefinirPapel(UserOrgaoSes.Id, new PePapelDTO { Papel = null }));

        var pessoa = (PePessoaResponse)resultado.Value!;
        Assert.Equal(UserOrgaoSes.Id, pessoa.UserId);
        Assert.Null(pessoa.Papel);
        Assert.Empty(ConcessoesDoModulo(UserOrgaoSes));
    }
}
