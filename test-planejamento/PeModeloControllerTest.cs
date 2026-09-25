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
/// Contrato HTTP do modelo (api/planejamento/modelo) e dos órgãos (api/planejamento/orgaos)
/// que o front consome: rotas, política do módulo, quem lê e quem altera, a trilha de cada
/// papel e os erros como { Code, Message } (400, 403, 404, 409), nunca um 500 sem corpo.
/// </summary>
public class PeModeloControllerTest : PeModeloTestBase
{
    // ── Contrato ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(PeModeloController), "api/planejamento/modelo")]
    [InlineData(typeof(PeOrgaosController), "api/planejamento/orgaos")]
    public void Controllers_ExigemAPoliticaDoModulo(Type tipo, string rota)
    {
        var autorizacao = Assert.Single(tipo.GetCustomAttributes<AuthorizeAttribute>(false));
        Assert.Equal(ModulosSgdp.PoliticaPlanejamento, autorizacao.Policy);
        Assert.Equal(rota, tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.DoesNotContain(tipo.GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Theory]
    [InlineData(nameof(PeModeloController.Obter), "GET", "")]
    [InlineData(nameof(PeModeloController.Trilha), "GET", "trilha")]
    [InlineData(nameof(PeModeloController.Historico), "GET", "historico")]
    [InlineData(nameof(PeModeloController.CriarNivel), "POST", "niveis")]
    [InlineData(nameof(PeModeloController.AtualizarNivel), "PUT", "niveis/{id:long}")]
    [InlineData(nameof(PeModeloController.OrdenarNiveis), "PUT", "niveis/ordem")]
    [InlineData(nameof(PeModeloController.AtualizarEtapa), "PUT", "etapas/{id:long}")]
    [InlineData(nameof(PeModeloController.CriarPasso), "POST", "passos")]
    [InlineData(nameof(PeModeloController.AtualizarPasso), "PUT", "passos/{id:long}")]
    [InlineData(nameof(PeModeloController.SituacaoPasso), "PUT", "passos/{id:long}/niveis")]
    [InlineData(nameof(PeModeloController.ExcluirPasso), "DELETE", "passos/{id:long}")]
    [InlineData(nameof(PeModeloController.OrdenarPassos), "PUT", "passos/ordem")]
    [InlineData(nameof(PeModeloController.CriarSecao), "POST", "secoes")]
    [InlineData(nameof(PeModeloController.AtualizarSecao), "PUT", "secoes/{id:long}")]
    [InlineData(nameof(PeModeloController.SituacaoSecao), "PUT", "secoes/{id:long}/niveis")]
    [InlineData(nameof(PeModeloController.ExcluirSecao), "DELETE", "secoes/{id:long}")]
    [InlineData(nameof(PeModeloController.OrdenarSecoes), "PUT", "secoes/ordem")]
    [InlineData(nameof(PeModeloController.CriarCampo), "POST", "campos")]
    [InlineData(nameof(PeModeloController.AtualizarCampo), "PUT", "campos/{id:long}")]
    [InlineData(nameof(PeModeloController.SituacaoCampo), "PUT", "campos/{id:long}/niveis")]
    [InlineData(nameof(PeModeloController.ExcluirCampo), "DELETE", "campos/{id:long}")]
    [InlineData(nameof(PeModeloController.OrdenarCampos), "PUT", "campos/ordem")]
    [InlineData(nameof(PeModeloController.CriarOpcao), "POST", "campos/{id:long}/opcoes")]
    [InlineData(nameof(PeModeloController.OrdenarOpcoes), "PUT", "campos/{id:long}/opcoes/ordem")]
    [InlineData(nameof(PeModeloController.AtualizarOpcao), "PUT", "opcoes/{id:long}")]
    [InlineData(nameof(PeModeloController.ExcluirOpcao), "DELETE", "opcoes/{id:long}")]
    public void RotasDoModelo(string action, string metodo, string rota)
    {
        var atributo = typeof(PeModeloController).GetMethod(action)!.GetCustomAttribute<HttpMethodAttribute>()!;

        Assert.Equal(new[] { metodo }, atributo.HttpMethods);
        Assert.Equal(rota, atributo.Template ?? "");
    }

    [Theory]
    [InlineData(nameof(PeOrgaosController.Listar), "GET", null)]
    [InlineData(nameof(PeOrgaosController.DefinirNivel), "PUT", "{id:long}/nivel")]
    [InlineData(nameof(PeOrgaosController.HistoricoNivel), "GET", "{id:long}/nivel/historico")]
    [InlineData(nameof(PeOrgaosController.Ajustes), "GET", "{id:long}/ajustes")]
    [InlineData(nameof(PeOrgaosController.DefinirAjustes), "PUT", "{id:long}/ajustes")]
    public void RotasDosOrgaos(string action, string metodo, string? rota)
    {
        var atributo = typeof(PeOrgaosController).GetMethod(action)!.GetCustomAttribute<HttpMethodAttribute>()!;

        Assert.Equal(new[] { metodo }, atributo.HttpMethods);
        Assert.Equal(rota, atributo.Template);
    }

    [Theory]
    [InlineData(typeof(PeModeloController), nameof(PeModeloController.Historico), typeof(PeHistoricoConsulta))]
    [InlineData(typeof(PeOrgaosController), nameof(PeOrgaosController.Listar), typeof(PeOrgaosConsulta))]
    public void ModelosDeQuery_NaoTemPropriedadeComONomeDoParametro(Type controller, string action, Type modelo)
    {
        var parametro = controller.GetMethod(action)!.GetParameters().Single();

        Assert.DoesNotContain(modelo.GetProperties(), p => string.Equals(p.Name, parametro.Name, StringComparison.OrdinalIgnoreCase));
    }

    // ── Leitura ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br")]
    [InlineData("sergio@sgdi.df.gov.br")]
    [InlineData("carla@cgtic.df.gov.br")]
    [InlineData("otavio@saude.df.gov.br")]
    [InlineData("cecilia@saude.df.gov.br")]
    [InlineData("admin@subgd.df.gov.br")]
    public async Task GetModelo_QualquerPapelEOAdminGeral_200(string email)
    {
        var (status, valor) = Resultado(await ControladorModelo(UserPorEmail[email]).Obter());

        Assert.Equal(200, status);
        Assert.Equal(7, Assert.IsType<PeModeloResponse>(valor).Etapas.Count);
    }

    [Fact]
    public async Task GetModelo_SemPapel_403ComCorpo()
    {
        var (status, valor) = Resultado(await ControladorModelo(UserSemPapel).Obter());

        Assert.Equal(403, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(valor));
    }

    [Fact]
    public async Task GetModelo_IncluirExcluidos_SoValeParaQuemAltera()
    {
        var passo = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId, Titulo = "Registre os parceiros", OQueFazer = "Registre."
        }, EmailAdmin);
        await Modelo.ExcluirPassoAsync(passo.Id, EmailAdmin);

        var sgdi = (PeModeloResponse)Resultado(await ControladorModelo(UserPeSgdi).Obter(incluirExcluidos: true)).Valor!;
        var admin = (PeModeloResponse)Resultado(await ControladorModelo(UserPeAdmin).Obter(incluirExcluidos: true)).Valor!;

        Assert.DoesNotContain(sgdi.Etapas.SelectMany(e => e.Passos), p => p.Id == passo.Id);
        Assert.Contains(admin.Etapas.SelectMany(e => e.Passos), p => p.Id == passo.Id && p.Excluido);
    }

    // ── Trilha ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("otavio@saude.df.gov.br")]
    [InlineData("cecilia@saude.df.gov.br")]
    public async Task Trilha_PapelDeOrgao_SemOrgaoId_EhADoProprioOrgao(string email)
    {
        var (status, valor) = Resultado(await ControladorModelo(UserPorEmail[email]).Trilha(null));

        Assert.Equal(200, status);
        Assert.Equal(OrgaoSes.Id, Assert.IsType<PeTrilhaResponse>(valor).OrgaoId);
        Assert.Equal(200, Resultado(await ControladorModelo(UserPorEmail[email]).Trilha(OrgaoSes.Id)).Status);
    }

    [Fact]
    public async Task Trilha_PapelDeOrgao_OutroOrgao_403()
    {
        var (status, valor) = Resultado(await ControladorModelo(UserOrgaoSes).Trilha(OrgaoSeec.Id));

        Assert.Equal(403, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(valor));
    }

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br")]
    [InlineData("sergio@sgdi.df.gov.br")]
    [InlineData("carla@cgtic.df.gov.br")]
    [InlineData("admin@subgd.df.gov.br")]
    public async Task Trilha_PapelGlobal_QualquerOrgao_ESemOrgaoId400(string email)
    {
        var user = UserPorEmail[email];
        var (status, valor) = Resultado(await ControladorModelo(user).Trilha(OrgaoSeec.Id));
        Assert.Equal(200, status);
        Assert.Equal("SEEC", Assert.IsType<PeTrilhaResponse>(valor).OrgaoSigla);

        // A unidade central não tem órgão: sem orgaoId não há de quem mostrar
        var (semOrgao, corpo) = Resultado(await ControladorModelo(user).Trilha(null));
        Assert.Equal(400, semOrgao);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoObrigatorio), CodigoDe(corpo));
    }

    [Fact]
    public async Task Trilha_PapelDeOrgaoSemOrgao_404()
    {
        DarPapel(UserSemUnidade, PapeisPlanejamento.Orgao);

        var (status, valor) = Resultado(await ControladorModelo(UserSemUnidade).Trilha(null));

        Assert.Equal(404, status);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), CodigoDe(valor));
    }

    [Fact]
    public async Task Historico_PapeisGlobais_200_OrgaoNao()
    {
        Assert.Equal(200, Resultado(await ControladorModelo(UserPeSgdi).Historico(new PeHistoricoConsulta())).Status);
        Assert.IsType<PagedResponse<PeHistoricoResponse>>(Resultado(await ControladorModelo(UserPeCgtic).Historico(new PeHistoricoConsulta())).Valor);
        Assert.Equal(403, Resultado(await ControladorModelo(UserOrgaoSes).Historico(new PeHistoricoConsulta())).Status);
    }

    // ── Escrita ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sergio@sgdi.df.gov.br")]
    [InlineData("carla@cgtic.df.gov.br")]
    [InlineData("otavio@saude.df.gov.br")]
    [InlineData("cecilia@saude.df.gov.br")]
    public async Task Escrita_SoAdministradorDoModuloEAdminGeral(string email)
    {
        var controller = ControladorModelo(UserPorEmail[email]);
        var id = Passo("preparacao.nomes").Id;

        var (status, valor) = Resultado(await controller.AtualizarPasso(id, Corpo(new { Titulo = "Outro" })));
        Assert.Equal(403, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(valor));
        Assert.Equal(403, Resultado(await controller.SituacaoPasso(id, Todos("opcional"))).Status);
        Assert.Equal(403, Resultado(await controller.CriarNivel(new PeNivelCriarDTO { Nome = "Outro" })).Status);
        Assert.Equal("Defina os nomes do órgão: comitê, equipes, autoridade máxima, unidade de TIC", Passo("preparacao.nomes").Titulo);
    }

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br")]
    [InlineData("admin@subgd.df.gov.br")]
    public async Task Escrita_AdministradorDoModuloEAdminGeral_200E201(string email)
    {
        var controller = ControladorModelo(UserPorEmail[email]);

        var (atualizado, passo) = Resultado(await controller.AtualizarPasso(Passo("preparacao.nomes").Id, Corpo(new { Titulo = "Novo título" })));
        Assert.Equal(200, atualizado);
        Assert.Equal("Novo título", Assert.IsType<PePassoResponse>(passo).Titulo);

        var (criado, nivel) = Resultado(await controller.CriarNivel(new PeNivelCriarDTO { Nome = "Piloto" }));
        Assert.Equal(201, criado);
        Assert.Equal("piloto", Assert.IsType<PeNivelResponse>(nivel).Codigo);
        Assert.Equal(email, HistoricoDe("nivel", ((PeNivelResponse)nivel!).Id).Single().AlteradoPor);
    }

    [Fact]
    public async Task Put_CorpoJson_AusenteNaoMuda_NuloLimpa()
    {
        var controller = ControladorModelo(UserPeAdmin);
        var id = Passo("preparacao.sgtic").Id;

        var soTitulo = (PePassoResponse)Resultado(await controller.AtualizarPasso(id, Corpo(new { Titulo = "Registre o SGTIC" }))).Valor!;
        Assert.Equal("art. 8º, §§ 1º e 2º", soTitulo.BaseLegal);

        var limpa = (PePassoResponse)Resultado(await controller.AtualizarPasso(id, Corpo(new { BaseLegal = (string?)null }))).Valor!;
        Assert.Null(limpa.BaseLegal);
        Assert.Equal("Registre o SGTIC", limpa.Titulo);

        // Nome da propriedade sem diferenciar maiúsculas, como o resto da API
        var minusculas = (PePassoResponse)Resultado(await controller.AtualizarPasso(id, Corpo(new { titulo = "Registre o SGTIC do órgão" }))).Valor!;
        Assert.Equal("Registre o SGTIC do órgão", minusculas.Titulo);

        var (status, valor) = Resultado(await controller.AtualizarPasso(id, Corpo(new[] { 1, 2 })));
        Assert.Equal(400, status);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), CodigoDe(valor));
    }

    [Fact]
    public async Task Erros_ViramCodeEMessage_ComOStatusDoContrato()
    {
        var controller = ControladorModelo(UserPeAdmin);

        var (travado, corpoTravado) = Resultado(await controller.SituacaoPasso(Passo("diagnostico.ativos").Id, Todos("desligado")));
        Assert.Equal(409, travado);
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), CodigoDe(corpoTravado));

        var (naoEncontrado, corpoNaoEncontrado) = Resultado(await controller.AtualizarPasso(999_999, Corpo(new { Titulo = "X" })));
        Assert.Equal(404, naoEncontrado);
        Assert.Equal(Codigo(ErrorCode.PeItemNaoEncontrado), CodigoDe(corpoNaoEncontrado));

        var (sistema, corpoSistema) = Resultado(await controller.ExcluirPasso(Passo("preparacao.nomes").Id));
        Assert.Equal(409, sistema);
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), CodigoDe(corpoSistema));

        var (invalido, corpoInvalido) = Resultado(await controller.CriarCampo(new PeCampoCriarDTO
        {
            SecaoId = Secao("nomes").Id, Rotulo = "Nota", Tipo = "calculado", Config = Corpo(new { calculo = "livre" })
        }));
        Assert.Equal(400, invalido);
        Assert.Equal(Codigo(ErrorCode.PeConfigInvalida), CodigoDe(corpoInvalido));

        var (ordem, corpoOrdem) = Resultado(await controller.OrdenarNiveis(new PeOrdemDTO { Ids = new List<long> { NivelId("basico") } }));
        Assert.Equal(400, ordem);
        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida), CodigoDe(corpoOrdem));
    }

    [Fact]
    public async Task ExcluirOpcao_204QuandoApaga_200QuandoSoDesativa()
    {
        var controller = ControladorModelo(UserPeAdmin);
        var criada = await Modelo.CriarOpcaoAsync(Campo("ativos", "tipo").Id, new PeOpcaoCriarDTO { Rotulo = "Plataforma" }, EmailAdmin);

        Assert.Equal(204, Resultado(await controller.ExcluirOpcao(criada.Id)).Status);

        var (status, valor) = Resultado(await controller.ExcluirOpcao(Opcao("ativos", "tipo", "rede").Id));
        Assert.Equal(200, status);
        Assert.False(Assert.IsType<PeOpcaoResponse>(valor).Ativa);
    }

    [Fact]
    public async Task CadastroQueNaoExiste_404()
    {
        var principal = PrincipalDe("kc-fantasma", "fantasma@df.gov.br", Perfis.Basico);

        var (status, valor) = Resultado(await ControladorModeloDe(principal).Obter());

        Assert.Equal(404, status);
        Assert.Equal(Codigo(ErrorCode.PeUsuarioNaoEncontrado), CodigoDe(valor));
    }

    // ── Órgãos ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Orgaos_ListaDosPapeisGlobais_OrgaoNao()
    {
        var (status, valor) = Resultado(await ControladorOrgaos(UserPeSgdi).Listar(new PeOrgaosConsulta()));
        Assert.Equal(200, status);
        Assert.Equal(2, Assert.IsType<List<PeOrgaoNivelResponse>>(valor).Count);

        Assert.Equal(403, Resultado(await ControladorOrgaos(UserOrgaoSes).Listar(new PeOrgaosConsulta())).Status);
    }

    [Fact]
    public async Task Orgaos_TrocarNivel_SoAdministrador_ComJustificativa()
    {
        var dto = new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "Maturidade alta" };

        Assert.Equal(403, Resultado(await ControladorOrgaos(UserPeSgdi).DefinirNivel(OrgaoSes.Id, dto)).Status);

        var (semJustificativa, corpo) = Resultado(await ControladorOrgaos(UserPeAdmin).DefinirNivel(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("avancado") }));
        Assert.Equal(400, semJustificativa);
        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria), CodigoDe(corpo));

        var (status, valor) = Resultado(await ControladorOrgaos(UserPeAdmin).DefinirNivel(OrgaoSes.Id, dto));
        Assert.Equal(200, status);
        Assert.Equal("Avançado", Assert.IsType<PeOrgaoNivelResponse>(valor).NivelNome);

        var (inativo, corpoInativo) = Resultado(await ControladorOrgaos(UserPeAdmin).DefinirNivel(999_999, dto));
        Assert.Equal(404, inativo);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), CodigoDe(corpoInativo));
    }

    [Fact]
    public async Task Orgaos_HistoricoEAjustes_DoProprioOrgaoOuGlobal()
    {
        Assert.Equal(200, Resultado(await ControladorOrgaos(UserConsultaSes).HistoricoNivel(OrgaoSes.Id)).Status);
        Assert.Equal(200, Resultado(await ControladorOrgaos(UserOrgaoSes).Ajustes(OrgaoSes.Id)).Status);
        Assert.Equal(200, Resultado(await ControladorOrgaos(UserPeCgtic).Ajustes(OrgaoSeec.Id)).Status);
        Assert.Equal(403, Resultado(await ControladorOrgaos(UserOrgaoSes).Ajustes(OrgaoSeec.Id)).Status);
        Assert.Equal(403, Resultado(await ControladorOrgaos(UserConsultaSes).HistoricoNivel(OrgaoSeec.Id)).Status);
    }

    [Fact]
    public async Task Orgaos_Ajustes_SoAdministrador_ETravadoVolta409()
    {
        var ajustes = new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "passo", AlvoId = Passo("diagnostico.ativos").Id, Situacao = "desligado" }
        };

        Assert.Equal(403, Resultado(await ControladorOrgaos(UserOrgaoSes).DefinirAjustes(OrgaoSes.Id, ajustes)).Status);

        var (status, valor) = Resultado(await ControladorOrgaos(UserAdminGeral).DefinirAjustes(OrgaoSes.Id, ajustes));
        Assert.Equal(409, status);
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), CodigoDe(valor));
    }
}
