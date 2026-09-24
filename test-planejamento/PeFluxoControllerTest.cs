using System.Text.Json;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas da E6 como o front chama (PeFluxosController): 200 nas leituras e gravações, o
/// desenho como image/svg+xml, 201 no cronograma sugerido, a validação da definição como 400
/// { Code, Message, Erros } e os outros erros como { Code, Message } com 403, 404 ou 409.
/// </summary>
public class PeFluxoControllerTest : PeFluxoTestBase
{
    private static ContentResult Svg(IActionResult resultado)
    {
        var conteudo = Assert.IsType<ContentResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, conteudo.StatusCode);
        Assert.StartsWith("image/svg+xml", conteudo.ContentType);
        Assert.StartsWith("<svg", conteudo.Content);
        return conteudo;
    }

    private static JsonElement CorpoDoFluxo(PeFluxoDefinicao definicao, string? nome = null) =>
        JsonSerializer.SerializeToElement(new { Nome = nome, Definicao = ComoJson(definicao) });

    private static List<string> ErrosDoCorpo(object? corpo) =>
        ((IEnumerable<string>)corpo!.GetType().GetProperty("Erros")!.GetValue(corpo)!).ToList();

    [Fact]
    public async Task Modelos_LerGravarEDesenhar()
    {
        var sgdi = ControladorFluxos(UserPeSgdi);
        var (status, corpo) = Resultado(await sgdi.Modelos());
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(10, Assert.IsType<List<PeFluxoModeloResponse>>(corpo).Count);
        Svg(await sgdi.SvgDoModelo("diagnostico"));

        // Só o administrador do módulo grava
        var definicao = DefinicaoDoModelo("monitoramento");
        (status, corpo) = Resultado(await sgdi.SalvarModelo("monitoramento", CorpoDoFluxo(definicao)));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        var admin = ControladorFluxos(UserPeAdmin);
        (status, corpo) = Resultado(await admin.SalvarModelo("monitoramento", CorpoDoFluxo(definicao, "Monitoramento do PDTIC")));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Monitoramento do PDTIC", Assert.IsType<PeFluxoModeloResponse>(corpo).Nome);

        // Definição inválida: 400 com a lista de erros
        definicao.Elementos.RemoveAll(e => e.Tipo == "fim");
        definicao.Ligacoes.RemoveAll(l => l.Para == "fim");
        (status, corpo) = Resultado(await admin.SalvarModelo("monitoramento", CorpoDoFluxo(definicao)));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeFluxoInvalido), (status, CodigoDe(corpo)));
        Assert.Contains("O fluxo precisa de pelo menos um fim.", ErrosDoCorpo(corpo));

        // Corpo que não é objeto: 400 com mensagem; chave que não existe: 404
        (status, corpo) = Resultado(await admin.SalvarModelo("monitoramento", JsonSerializer.SerializeToElement(new[] { 1 })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDadosInvalidos), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await admin.SvgDoModelo("nao_existe"));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeFluxoNaoEncontrado), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task CopiaDoOrgao_LerGravarRestaurarEDesenhar()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorFluxos(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.DoPdtic(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(10, Assert.IsType<List<PeFluxoResumoResponse>>(corpo).Count);

        (status, corpo) = Resultado(await orgao.Obter(pdtic.Id, "planejamento"));
        Assert.Equal(StatusCodes.Status200OK, status);
        var fluxo = Assert.IsType<PeFluxoResponse>(corpo);
        Assert.False(fluxo.Personalizado);

        fluxo.Definicao.Elementos.Single(e => e.Numero == "3.6").Nome = "Listar os fatores críticos de sucesso";
        (status, corpo) = Resultado(await orgao.Salvar(pdtic.Id, "planejamento", CorpoDoFluxo(fluxo.Definicao)));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeFluxoResponse>(corpo).Personalizado);
        Svg(await ControladorFluxos(UserConsultaSes).SvgDoPdtic(pdtic.Id, "planejamento"));

        (status, corpo) = Resultado(await orgao.Restaurar(pdtic.Id, "planejamento"));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(Assert.IsType<PeFluxoResponse>(corpo).Personalizado);

        // A consulta não grava; outro órgão não lê; chave que não existe: 404
        (status, corpo) = Resultado(await ControladorFluxos(UserConsultaSes).Salvar(pdtic.Id, "planejamento", CorpoDoFluxo(fluxo.Definicao)));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorFluxos(UserOrgaoSeec).Obter(pdtic.Id, "planejamento"));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.Obter(pdtic.Id, "nao_existe"));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeFluxoNaoEncontrado), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.Obter(999_999, "planejamento"));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PePdticNaoEncontrado), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task Editor_DesenhoValidacaoENomes()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorFluxos(UserOrgaoSes);

        Svg(await orgao.Desenho(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(Simples()), PdticId = pdtic.Id, Nome = "Meu fluxo" })));

        var (status, corpo) = Resultado(await orgao.Desenho(JsonSerializer.SerializeToElement(new { Definicao = new { Raias = 1 } })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeFluxoInvalido), (status, CodigoDe(corpo)));
        Assert.Contains("Raias precisa ser uma lista.", ErrosDoCorpo(corpo));
        (status, corpo) = Resultado(await orgao.Desenho(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(Simples()), PdticId = "x" })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDadosInvalidos), (status, CodigoDe(corpo)));

        (status, corpo) = Resultado(await orgao.Validar(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(Simples()) })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeFluxoValidacaoResponse>(corpo).Valida);

        (status, corpo) = Resultado(await orgao.Nomes(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains(Assert.IsType<List<PeFluxoNomeResponse>>(corpo), n => n.Marcador == "{nomes.comite}");
    }

    [Fact]
    public async Task Cronograma_201_E409NaSegunda()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorFluxos(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.SugerirCronograma(pdtic.Id));
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.Equal(32, Assert.IsType<List<PeRegistroResponse>>(corpo).Count);

        (status, corpo) = Resultado(await orgao.SugerirCronograma(pdtic.Id));
        Assert.Equal((StatusCodes.Status409Conflict, (int)ErrorCode.PeCronogramaPreenchido), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorFluxos(UserConsultaSes).SugerirCronograma(pdtic.Id));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task SemPapel_403()
    {
        var (status, corpo) = Resultado(await ControladorFluxos(UserSemPapel).Modelos());
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorFluxos(UserSemPapel).Desenho(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(Simples()) })));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
    }
}
