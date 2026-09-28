using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1, achado A06 da revisão final: o envio do PETIC-DF ao CGTIC com pendências responde a lista
/// do que falta, seção por seção (400 com Pendencias), e a prévia do envio (GET petic/{id}/envio)
/// diz se a versão pode ir ao CGTIC, com a mesma lista.
/// </summary>
public class PeF1PeticTest : PeReferenciaisTestBase
{
    [Fact]
    public async Task Previa_DoRascunho_ListaOQueFalta_EDepoisLibera()
    {
        var rascunho = await RascunhoAsync(copiar: false);

        var previa = await Petics.EnvioAsync(rascunho.Id, await Admin());
        Assert.False(previa.PodeEnviar);
        Assert.Equal("Resolva o que falta na lista antes de enviar o PETIC-DF ao CGTIC.", previa.Motivo);
        Assert.Contains(previa.Pendencias, p => p.SecaoChave == "petic_objetivo" && p.SecaoTitulo == "Objetivos estratégicos");
        Assert.All(previa.Pendencias, p => Assert.False(string.IsNullOrWhiteSpace(p.Motivo)));

        await PreencherMinimoAsync(rascunho.Id);
        previa = await Petics.EnvioAsync(rascunho.Id, await Admin());
        Assert.True(previa.PodeEnviar);
        Assert.Empty(previa.Pendencias);
        Assert.Null(previa.Motivo);

        // Quem não envia lê a prévia com o porquê
        var daSgdi = await Petics.EnvioAsync(rascunho.Id, await ContextoDe(UserPeSgdi));
        Assert.False(daSgdi.PodeEnviar);
        Assert.Equal("Quem envia o PETIC-DF ao CGTIC é o administrador do módulo.", daSgdi.Motivo);

        // Enviado: a prévia diz que a versão não muda mais
        await Petics.EnviarAsync(rascunho.Id, await Admin());
        var enviada = await Petics.EnvioAsync(rascunho.Id, await Admin());
        Assert.False(enviada.PodeEnviar);
        Assert.NotNull(enviada.Motivo);
    }

    [Fact]
    public async Task Previa_PapelDeOrgaoNaoVeORascunho_404()
    {
        var rascunho = await RascunhoAsync(copiar: false);
        var ex = await Assert.ThrowsAsync<ApiException>(async () => await Petics.EnvioAsync(rascunho.Id, await ContextoDe(UserOrgaoSes)));
        Assert.Equal((int)ErrorCode.PePeticNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Controller_Enviar_400ComAsPendencias_EAPrevia()
    {
        var rascunho = await RascunhoAsync(copiar: false);
        var controlador = ControladorPetic(UserPeAdmin);

        var (status, corpo) = Resultado(await controlador.Enviar(rascunho.Id));
        Assert.Equal(400, status);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(corpo));
        Assert.Equal((int)ErrorCode.PePeticIncompleto, json.RootElement.GetProperty("Code").GetInt32());
        var pendencias = json.RootElement.GetProperty("Pendencias").EnumerateArray().ToList();
        Assert.NotEmpty(pendencias);
        Assert.All(pendencias, p =>
        {
            Assert.True(p.TryGetProperty("SecaoChave", out _));
            Assert.True(p.TryGetProperty("SecaoTitulo", out _));
            Assert.True(p.TryGetProperty("Motivo", out _));
        });

        var (statusDaPrevia, previa) = Resultado(await controlador.Envio(rascunho.Id));
        Assert.Equal(200, statusDaPrevia);
        Assert.False(((PePeticEnvioResponse)previa!).PodeEnviar);
    }
}
