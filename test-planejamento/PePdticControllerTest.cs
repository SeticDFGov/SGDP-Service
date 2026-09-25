using System.Text.Json;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas da E4 como o front chama (PePdticController e as rotas do PDTIC no
/// PeRegistrosController): 201 ao abrir e ao comentar, 204 sem PDTIC atual, 200 nas leituras e
/// erros sempre como { Code, Message } com 400, 403, 404 ou 409.
/// </summary>
public class PePdticControllerTest : PePdticTestBase
{
    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);

    [Fact]
    public async Task Abrir201_Repetir409_Atual200Ou204()
    {
        var orgao = ControladorPdtic(UserOrgaoSes);

        Assert.Equal(StatusCodes.Status204NoContent, Resultado(await orgao.Atual(null)).Status);

        // Corpo vazio vale como { OrgaoId: null }
        var (status, corpo) = Resultado(await orgao.Abrir(default));
        Assert.Equal(StatusCodes.Status201Created, status);
        var pdtic = Assert.IsType<PePdticResponse>(corpo);
        Assert.Equal("1.0", pdtic.Versao);

        (status, corpo) = Resultado(await orgao.Abrir(Json(new { OrgaoId = (long?)null })));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PePdticJaExiste), CodigoDe(corpo));

        (status, corpo) = Resultado(await orgao.Atual(null));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(pdtic.Id, Assert.IsType<PePdticResponse>(corpo).Id);

        // Corpo que não é objeto: 400 com mensagem
        (status, corpo) = Resultado(await ControladorPdtic(UserAdminGeral).Abrir(Json(new[] { 1 })));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), CodigoDe(corpo));
    }

    [Fact]
    public async Task Leituras_EOsErrosComCorpo()
    {
        var pdtic = await AbrirSesAsync();

        Assert.Equal(StatusCodes.Status200OK, Resultado(await ControladorPdtic(UserConsultaSes).Obter(pdtic.Id)).Status);
        var (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSeec).Obter(pdtic.Id));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(corpo));
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Obter(999));
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado), CodigoDe(corpo));

        var situacao = Assert.IsType<PePdticSituacaoResponse>(Resultado(await ControladorPdtic(UserPeSgdi).Situacao(pdtic.Id)).Valor);
        Assert.Equal("1.1", situacao.ProximoPasso);
        Assert.IsType<PeTemasResponse>(Resultado(await ControladorPdtic(UserPeSgdi).Temas(pdtic.Id)).Valor);
        SistemaPgia(OrgaoSes, "Triagem");
        Assert.Single(Assert.IsType<List<PeSistemaIaPgiaResponse>>(Resultado(await ControladorPdtic(UserConsultaSes).SistemasIa(pdtic.Id)).Valor));

        // A lista dos atuais é dos papéis globais
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Listar(new PePdticConsulta()));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(StatusCodes.Status200OK, Resultado(await ControladorPdtic(UserPeCgtic).Listar(new PePdticConsulta())).Status);
    }

    [Fact]
    public async Task NaoSeAplica_409ComCorpo_EComentarios201()
    {
        var pdtic = await AbrirSesAsync();
        var passo = Passo("preparacao.equipe").Id;

        var (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).MarcarNaoSeAplica(pdtic.Id, passo, Json(new { Justificativa = "Não há" })));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PeNaoSeAplicaRecusado), CodigoDe(corpo));
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).MarcarNaoSeAplica(pdtic.Id, 999_999, Json(new { Justificativa = "x" })));
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), CodigoDe(corpo));

        (status, corpo) = Resultado(await ControladorPdtic(UserPeSgdi).Comentar(pdtic.Id,
            Json(new { PassoId = Passo("preparacao.abrangencia").Id, PaiId = (long?)null, Texto = "Confira." })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var conversa = Assert.IsType<PeComentarioResponse>(corpo);
        Assert.Single(Assert.IsType<List<PeComentarioResponse>>(Resultado(await ControladorPdtic(UserConsultaSes).Comentarios(pdtic.Id, null)).Valor));

        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Resolver(conversa.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(Assert.IsType<PeComentarioResponse>(corpo).ResolvidoEm);
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Comentar(pdtic.Id, Json(new { PaiId = conversa.Id, Texto = "Mais" })));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PeComentarioResolvido), CodigoDe(corpo));

        // A Secretaria do CGTIC lê, mas não comenta
        (status, corpo) = Resultado(await ControladorPdtic(UserPeCgtic).Comentar(pdtic.Id,
            Json(new { PassoId = Passo("preparacao.abrangencia").Id, Texto = "Oi" })));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(corpo));
    }

    [Fact]
    public async Task Registros_DoPdtic_PelasRotasDoMotor_EPlanilhas()
    {
        var pdtic = await AbrirSesAsync();
        var registros = ControladorRegistros(UserOrgaoSes);

        var (status, corpo) = Resultado(await registros.CriarNoPdtic(pdtic.Id, "ativos", Json(new { Dados = new { nome = "Rede", tipo = "rede", situacao = "em_operacao" } })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var ativo = Assert.IsType<PeRegistroResponse>(corpo);
        Assert.Equal("AT01", ativo.Codigo);

        // Validação com Campos
        (status, corpo) = Resultado(await registros.CriarNoPdtic(pdtic.Id, "ativos", Json(new { Dados = new { nome = "Sem tipo" } })));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        var campos = (IReadOnlyDictionary<string, string>)corpo!.GetType().GetProperty("Campos")!.GetValue(corpo)!;
        Assert.Contains("tipo", campos.Keys);

        Assert.Equal(StatusCodes.Status200OK, Resultado(await registros.ListarDoPdtic(pdtic.Id, "ativos")).Status);
        Assert.Equal(StatusCodes.Status200OK, Resultado(await registros.AtualizarNoPdtic(pdtic.Id, "ativos", ativo.Id,
            Json(new { Dados = new { nome = "Rede nova", tipo = "rede", situacao = "em_operacao" } }))).Status);
        Assert.Equal(StatusCodes.Status200OK, Resultado(await registros.OrdenarNoPdtic(pdtic.Id, "ativos", new PeOrdemDTO { Ids = new List<long> { ativo.Id } })).Status);
        (status, corpo) = Resultado(await ControladorRegistros(UserConsultaSes).ExcluirDoPdtic(pdtic.Id, "ativos", ativo.Id));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(StatusCodes.Status204NoContent, Resultado(await registros.ExcluirDoPdtic(pdtic.Id, "ativos", ativo.Id)).Status);

        // Catálogo do PGIA pelo PDTIC; sem ele, 400
        SistemaPgia(OrgaoSes, "Triagem");
        Assert.Single(Assert.IsType<List<PeCatalogoItemResponse>>(Resultado(await registros.Catalogo("pgia_sistema", pdtic.Id)).Valor));
        Assert.Equal(StatusCodes.Status400BadRequest, Resultado(await registros.Catalogo("pgia_sistema", null)).Status);

        // Planilhas: o arquivo com o nome; o consolidado só para os globais
        var arquivo = Assert.IsType<FileContentResult>(await ControladorPdtic(UserOrgaoSes).PlanilhaDaSecao(pdtic.Id, "necessidades", "csv"));
        Assert.StartsWith("PDTIC_SES_necessidades_", arquivo.FileDownloadName);
        Assert.IsType<FileContentResult>(await ControladorPdtic(UserOrgaoSes).PlanilhaCompleta(pdtic.Id, null));
        Assert.IsType<FileContentResult>(await ControladorPdtic(UserPeSgdi).ConsolidadoDaSecao("necessidades", "xlsx"));
        Assert.IsType<FileContentResult>(await ControladorPdtic(UserPeSgdi).ConsolidadoCompleto(null));
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).ConsolidadoDaSecao("necessidades", "xlsx"));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(corpo));
    }

    [Fact]
    public async Task SemCadastro_404_ComCorpo()
    {
        var principal = PrincipalDe("kc-ninguem", "ninguem@df.gov.br", app.Auth.Perfis.Basico);
        var controlador = new Controllers.Planejamento.PePdticController(Pdtics, Comentarios, Planilhas, Permissoes, Aprovacao)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
        var (status, corpo) = Resultado(await controlador.Atual(null));
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal(Codigo(ErrorCode.PeUsuarioNaoEncontrado), CodigoDe(corpo));
    }
}
