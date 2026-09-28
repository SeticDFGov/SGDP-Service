using System.Text.Json;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Controllers da E3 pelas rotas do contrato: status (201 no POST, 204 no DELETE e na
/// vigente ausente, 400 com Campos na validação, 403, 404 e 409 com { Code, Message }),
/// permissões por papel e as planilhas como arquivo com o nome no Content-Disposition.
/// </summary>
public class PeReferenciaisControllerTest : PeReferenciaisTestBase
{
    private static IReadOnlyDictionary<string, string> CamposDe(object? corpo) =>
        (IReadOnlyDictionary<string, string>)corpo!.GetType().GetProperty("Campos")!.GetValue(corpo)!;

    private static string MensagemDe(object? corpo) => (string)corpo!.GetType().GetProperty("Message")!.GetValue(corpo)!;

    [Fact]
    public async Task Registros_Post201_Put200_Delete204_Ordem_E400ComCamposPelaChave()
    {
        var controller = ControladorRegistros(UserPeAdmin);

        var (status, corpo) = Resultado(await controller.CriarNoDf("principio",
            Corpo(new { Dados = new { texto = "Novo princípio", fundamento = "Resolução CGTIC nº 1/2026" } })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var criado = Assert.IsType<PeRegistroResponse>(corpo);
        Assert.Equal("PR12", criado.Codigo);

        (status, corpo) = Resultado(await controller.CriarNoDf("principio", Corpo(new { Dados = new { texto = "" } })));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PeRegistroInvalido, CodigoDe(corpo));
        Assert.Equal(new[] { "fundamento", "texto" }, CamposDe(corpo).Keys.OrderBy(k => k));

        (status, corpo) = Resultado(await controller.CriarNoDf("principio", Corpo(new[] { 1, 2 })));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, CodigoDe(corpo));
        (status, corpo) = Resultado(await controller.CriarNoDf("principio", Corpo(new { Dados = new { texto = "x" }, Vinculos = new { a = "b" } })));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, CodigoDe(corpo));

        (status, corpo) = Resultado(await controller.AtualizarNoDf("principio", criado.Id,
            Corpo(new { Dados = new { texto = "Mudado", fundamento = "Resolução" } })));
        Assert.Equal(200, status);
        Assert.Equal("Mudado", Assert.IsType<PeRegistroResponse>(corpo).Dados["texto"].GetString());

        var ids = (await Registros.ListarAsync(PeDono.Df, "principio", await Admin())).Registros.Select(r => r.Id).Reverse().ToList();
        (status, corpo) = Resultado(await controller.OrdenarNoDf("principio", new PeOrdemDTO { Ids = ids }));
        Assert.Equal(200, status);
        Assert.Equal("PR12", Assert.IsType<PeRegistrosResponse>(corpo).Registros[0].Codigo);

        Assert.Equal(StatusCodes.Status204NoContent, Resultado(await controller.ExcluirDoDf("principio", criado.Id)).Status);

        // Do sistema: 409 com a mensagem
        var pr01 = Context.PeRegistros.Single(r => r.Codigo == "PR01");
        (status, corpo) = Resultado(await controller.ExcluirDoDf("principio", pr01.Id));
        Assert.Equal(409, status);
        Assert.Equal((int)ErrorCode.PeRegistroDoSistema, CodigoDe(corpo));
        Assert.Contains("sistema", MensagemDe(corpo));

        // Seção que não existe e registro que não existe: 404
        Assert.Equal(404, Resultado(await controller.ListarDoDf("nao_existe")).Status);
        Assert.Equal(404, Resultado(await controller.ExcluirDoDf("principio", 999999)).Status);
    }

    [Fact]
    public async Task Registros_QuemNaoAdministra403_EQuemNaoTemCadastro404()
    {
        var (status, corpo) = Resultado(await ControladorRegistros(UserPeSgdi).CriarNoDf("principio",
            Corpo(new { Dados = new { texto = "x", fundamento = "y" } })));
        Assert.Equal(403, status);
        Assert.Equal((int)ErrorCode.PeSemPermissao, CodigoDe(corpo));

        Assert.Equal(200, Resultado(await ControladorRegistros(UserConsultaSes).ListarDoDf("principio")).Status);
        Assert.Equal(403, Resultado(await ControladorRegistros(UserSemPapel).ListarDoDf("principio")).Status);

        var semCadastro = new Controllers.Planejamento.PeRegistrosController(Registros, Planilhas, Permissoes)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = PrincipalDe("kc-ninguem", "ninguem@df.gov.br", app.Auth.Perfis.Basico) }
            }
        };
        (status, corpo) = Resultado(await semCadastro.ListarDoDf("principio"));
        Assert.Equal(404, status);
        Assert.Equal((int)ErrorCode.PeUsuarioNaoEncontrado, CodigoDe(corpo));
    }

    [Fact]
    public async Task Registros_DoPetic_EFormularioJaPreenchido409()
    {
        var rascunho = await RascunhoAsync();
        var controller = ControladorRegistros(UserPeAdmin);

        var (status, _) = Resultado(await controller.CriarNoPetic(rascunho.Id, "petic_identidade",
            Corpo(new { Dados = new { missao = "Missão", visao = "Visão" } })));
        Assert.Equal(201, status);
        var (repetido, corpo) = Resultado(await controller.CriarNoPetic(rascunho.Id, "petic_identidade",
            Corpo(new { Dados = new { missao = "Outra", visao = "Outra" } })));
        Assert.Equal(409, repetido);
        Assert.Equal((int)ErrorCode.PeFormularioJaPreenchido, CodigoDe(corpo));

        var lista = Assert.IsType<PeRegistrosResponse>(Resultado(await controller.ListarDoPetic(rascunho.Id, "petic_identidade")).Valor);
        Assert.True(lista.PodeEditar);
        Assert.Single(lista.Registros);
        Assert.Equal(404, Resultado(await controller.ListarDoPetic(999, "petic_identidade")).Status);
    }

    [Fact]
    public async Task Petic_Rotas_Vigente204_Criar201_EnviarEApagar()
    {
        var controller = ControladorPetic(UserPeAdmin);
        Assert.Equal(204, Resultado(await controller.Vigente()).Status);
        Assert.Empty(Assert.IsType<List<PePeticResponse>>(Resultado(await controller.Listar()).Valor));

        var (status, corpo) = Resultado(await controller.Criar(Corpo(new { Titulo = "PETIC-DF 2027-2030", VigenciaInicio = "2027-01-01", VigenciaFim = "2030-12-31" })));
        Assert.Equal(201, status);
        var versao = Assert.IsType<PePeticResponse>(corpo);
        Assert.Equal(409, Resultado(await controller.Criar(Corpo(new { Titulo = "Outra" }))).Status);
        Assert.Equal(400, Resultado(await controller.Criar(Corpo(new { Titulo = "X", VigenciaInicio = 20270101 }))).Status);

        (status, corpo) = Resultado(await controller.Atualizar(versao.Id, Corpo(new { Titulo = "PETIC-DF novo" })));
        Assert.Equal(200, status);
        Assert.Equal(new DateOnly(2030, 12, 31), Assert.IsType<PePeticResponse>(corpo).VigenciaFim);

        (status, corpo) = Resultado(await controller.Enviar(versao.Id));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PePeticIncompleto, CodigoDe(corpo));

        Assert.Equal(403, Resultado(await ControladorPetic(UserPeSgdi).Criar(Corpo(new { Titulo = "X" }))).Status);
        Assert.Equal(403, Resultado(await ControladorPetic(UserPeCgtic).Excluir(versao.Id)).Status);
        Assert.Empty(Assert.IsType<List<PePeticResponse>>(Resultado(await ControladorPetic(UserOrgaoSes).Listar()).Valor));
        Assert.Equal(404, Resultado(await ControladorPetic(UserOrgaoSes).Obter(versao.Id)).Status);

        Assert.Equal(204, Resultado(await controller.Excluir(versao.Id)).Status);
        Assert.Equal(404, Resultado(await controller.Obter(versao.Id)).Status);
    }

    [Fact]
    public async Task Deliberacoes_GlobaisLeem_SoACgticEOAdminGeralDecidem()
    {
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);
        var id = (await Petics.EnviarAsync(rascunho.Id, await Admin())).Deliberacao!.Id;

        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserPeCgtic, UserAdminGeral })
            Assert.Equal(200, Resultado(await ControladorDeliberacoes(user).Listar(new PeDeliberacoesConsulta())).Status);
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes })
            Assert.Equal(403, Resultado(await ControladorDeliberacoes(user).Listar(new PeDeliberacoesConsulta())).Status);

        var aprovar = Corpo(new { Decisao = "aprovado", AtoNumero = "5/2026", AtoData = "2026-09-20", Sei = "00040-00012345/2026-11" });
        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserOrgaoSes })
            Assert.Equal(403, Resultado(await ControladorDeliberacoes(user).Decidir(id, aprovar)).Status);

        var (status, corpo) = Resultado(await ControladorDeliberacoes(UserPeCgtic).Decidir(id, Corpo(new { Decisao = "aprovado" })));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PeDecisaoInvalida, CodigoDe(corpo));

        (status, corpo) = Resultado(await ControladorDeliberacoes(UserPeCgtic).Decidir(id, aprovar));
        Assert.Equal(200, status);
        Assert.Equal("aprovado", Assert.IsType<PeDeliberacaoResponse>(corpo).Situacao);
        Assert.Equal(409, Resultado(await ControladorDeliberacoes(UserAdminGeral).Decidir(id, aprovar)).Status);
        Assert.Equal(404, Resultado(await ControladorDeliberacoes(UserAdminGeral).Decidir(999, aprovar)).Status);
    }

    [Fact]
    public async Task Planilhas_ArquivoComNome_EErroComoJson()
    {
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);

        var secao = Assert.IsType<FileContentResult>(await ControladorPetic(UserPeSgdi).PlanilhaDaSecao(rascunho.Id, "petic_objetivo", "csv"));
        Assert.Equal(PePlanilhaService.MimeCsv, secao.ContentType);
        Assert.StartsWith("PETIC-DF_1.0_petic_objetivo_", secao.FileDownloadName);
        var completa = Assert.IsType<FileContentResult>(await ControladorPetic(UserPeSgdi).PlanilhaCompleta(rascunho.Id, null));
        Assert.EndsWith(".xlsx", completa.FileDownloadName);
        var df = Assert.IsType<FileContentResult>(await ControladorRegistros(UserConsultaSes).PlanilhaDoDf("diretriz_ciclo", "csv"));
        Assert.StartsWith("DF_diretriz_ciclo_", df.FileDownloadName);
        var atalho = Assert.IsType<FileContentResult>(await ControladorRegistros(UserConsultaSes).PlanilhaDosPrincipios("xlsx"));
        Assert.StartsWith("DF_principio_", atalho.FileDownloadName);

        var (status, corpo) = Resultado(await ControladorPetic(UserPeSgdi).PlanilhaCompleta(rascunho.Id, "csv"));
        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PePlanilhaInvalida, CodigoDe(corpo));
        Assert.Equal(404, Resultado(await ControladorPetic(UserOrgaoSes).PlanilhaCompleta(rascunho.Id, "xlsx")).Status);
    }

    [Fact]
    public async Task Catalogos_Rota()
    {
        var (status, corpo) = Resultado(await ControladorRegistros(UserOrgaoSes).Catalogo("principio"));
        Assert.Equal(200, status);
        Assert.Equal(11, Assert.IsType<List<PeCatalogoItemResponse>>(corpo).Count);
        Assert.Equal(404, Resultado(await ControladorRegistros(UserOrgaoSes).Catalogo("inventado")).Status);
    }

    [Fact]
    public async Task CorpoComTextoQueNaoEUtf8_400ComCorpo_Nunca500()
    {
        var bytes = "{\"Dados\":{\"texto\":\"Prefer"u8.ToArray().Append((byte)0xEA)
            .Concat("ncia\",\"fundamento\":\"x\"}}"u8.ToArray()).ToArray();
        using var documento = JsonDocument.Parse(bytes);

        var (status, corpo) = Resultado(await ControladorRegistros(UserPeAdmin).CriarNoDf("principio", documento.RootElement.Clone()));

        Assert.Equal(400, status);
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, CodigoDe(corpo));
        Assert.Contains("UTF-8", MensagemDe(corpo));
        var (statusPut, _) = Resultado(await ControladorPetic(UserPeAdmin).Atualizar(1, documento.RootElement.Clone()));
        Assert.Equal(400, statusPut);
    }

    [Fact]
    public void Contrato_RespostaDoRegistroEmPascalCase()
    {
        var json = JsonSerializer.Serialize(new PeRegistroResponse { Codigo = "OE01" });
        foreach (var nome in new[] { "\"Id\"", "\"Codigo\"", "\"Ordem\"", "\"Sistema\"", "\"Dados\"", "\"Rotulos\"", "\"Vinculos\"",
                     "\"CriadoEm\"", "\"CriadoPor\"", "\"AlteradoEm\"", "\"AlteradoPor\"" })
            Assert.Contains(nome, json);
        Assert.DoesNotContain("\"Ordem\":0,\"Resumo\"", JsonSerializer.Serialize(new PeVinculoResponse()));
        Assert.Equal("{\"RegistroId\":0,\"Codigo\":null,\"Resumo\":\"\"}", JsonSerializer.Serialize(new PeVinculoResponse()));
    }
}
