using System.Text.Json;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas do acompanhamento (E7, rodada B), com os status e os corpos do contrato: os ciclos
/// (a lista, abrir a avaliação com 201, fechar com 400 e Pendencias, reabrir), as grades (400
/// com Campos "id.campo"), o painel, os relatórios RA e RR no PeDocumentoController (a prévia,
/// o PDF com 201 e o download com o nome e nosniff), os erros com corpo (404, 403, 409) e o 409
/// com corpo antes da versão 6 do modelo.
/// </summary>
public class PeAcompanhamentoControllerTest : PeAcompanhamentoTestBase
{
    private static object? Propriedade(object? corpo, string nome) => corpo!.GetType().GetProperty(nome)!.GetValue(corpo);

    [Fact]
    public async Task Ciclos_Lista_Avaliacao201_Fechar400ComPendencias_EReabrir()
    {
        var id = await AcompanhadoAsync();
        var orgao = ControladorAcompanhamento(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.Ciclos(id, null));
        Assert.Equal(StatusCodes.Status200OK, status);
        var ciclos = Assert.IsType<List<PeCicloResponse>>(corpo);
        Assert.Equal(16, ciclos.Count);

        (status, corpo) = Resultado(await orgao.CriarCiclo(id, Corpo(new { Tipo = "avaliacao", Rotulo = "Avaliação do 1º ano" })));
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.Equal("Avaliação do 1º ano", Assert.IsType<PeCicloResponse>(corpo).Rotulo);
        (status, corpo) = Resultado(await orgao.CriarCiclo(id, Corpo(new { Tipo = "avaliacao" })));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PeAvaliacaoAberta), CodigoDe(corpo));
        (status, corpo) = Resultado(await orgao.CriarCiclo(id, JsonSerializer.SerializeToElement("avaliacao")));
        Assert.Equal((StatusCodes.Status400BadRequest, Codigo(ErrorCode.PeDadosInvalidos)), (status, CodigoDe(corpo)));

        // Fechar com pendências: 400 com a lista, como no envio
        (status, corpo) = Resultado(await orgao.Fechar(ciclos[0].Id));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PeCicloComPendencias), CodigoDe(corpo));
        var pendencias = Assert.IsAssignableFrom<IReadOnlyList<PePendenciaResponse>>(Propriedade(corpo, "Pendencias"));
        Assert.Equal(2, pendencias.Count);
        Assert.NotNull(Propriedade(corpo, "Message"));

        // Reabrir o que está aberto: 409; ciclo que não existe: 404; a consulta: 403
        (status, corpo) = Resultado(await orgao.Reabrir(ciclos[0].Id));
        Assert.Equal((StatusCodes.Status409Conflict, Codigo(ErrorCode.PeCicloFechado)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.Fechar(999999));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeCicloNaoEncontrado)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorAcompanhamento(UserConsultaSes).Fechar(ciclos[0].Id));
        Assert.Equal((StatusCodes.Status403Forbidden, Codigo(ErrorCode.PeSemPermissao)), (status, CodigoDe(corpo)));
        // A consulta lê a lista
        (status, _) = Resultado(await ControladorAcompanhamento(UserConsultaSes).Ciclos(id, "monitoramento"));
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task Grades_EPainel_ComOsErrosPorIdECampo()
    {
        var id = await AcompanhadoAsync();
        var orgao = ControladorAcompanhamento(UserOrgaoSes);
        var t1 = await CicloAsync(id, Trimestre1);
        var a1 = IdDe(id, "A01");

        var (status, corpo) = Resultado(await orgao.SalvarAcoes(t1.Id, Corpo(new { Itens = new[] { new { AcaoId = a1, Situacao = "em_andamento", ExecucaoFisica = 140 } } })));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PeRegistroInvalido), CodigoDe(corpo));
        var campos = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(Propriedade(corpo, "Campos"));
        Assert.Equal($"{a1}.execucao_fisica", Assert.Single(campos.Keys));

        (status, corpo) = Resultado(await orgao.SalvarAcoes(t1.Id, Corpo(new { Itens = new[] { new { AcaoId = a1, Situacao = "em_andamento", ExecucaoFisica = 40 } } })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(3, Assert.IsType<List<PeCicloAcaoResponse>>(corpo).Count);
        (status, corpo) = Resultado(await orgao.Acoes(t1.Id));
        Assert.Equal("em_andamento", Assert.IsType<List<PeCicloAcaoResponse>>(corpo).Single(a => a.AcaoId == a1).Situacao);

        (status, corpo) = Resultado(await orgao.Medicoes(t1.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(Assert.IsType<List<PeCicloMedicaoResponse>>(corpo));
        (status, corpo) = Resultado(await orgao.SalvarMedicoes(t1.Id, Corpo(new { Itens = Array.Empty<object>() })));
        Assert.Equal(StatusCodes.Status200OK, status);

        (status, corpo) = Resultado(await ControladorAcompanhamento(UserPeSgdi).Painel(id, null));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(Trimestre1, Assert.IsType<PePainelResponse>(corpo).Ciclo!.Rotulo);
        (status, corpo) = Resultado(await orgao.Painel(id, 999999));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeCicloNaoEncontrado)), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task SemAVersao6_409ComCorpo()
    {
        var id = await AcompanhadoAsync();
        VersaoDoModelo(5);
        var orgao = ControladorAcompanhamento(UserOrgaoSes);

        foreach (var resultado in new[]
                 {
                     await orgao.Ciclos(id, null), await orgao.Painel(id, null), await orgao.CriarCiclo(id, Corpo(new { Tipo = "avaliacao" })),
                     await ControladorDocumento(UserOrgaoSes).ObterRr(id)
                 })
        {
            var (status, corpo) = Resultado(resultado);
            Assert.Equal((StatusCodes.Status409Conflict, Codigo(ErrorCode.PeModeloIndisponivel)), (status, CodigoDe(corpo)));
            Assert.NotNull(Propriedade(corpo, "Message"));
        }
    }

    [Fact]
    public async Task Relatorios_APreviaOPdf201EODownload()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var documento = ControladorDocumento(UserOrgaoSes);

        var (status, corpo) = Resultado(await documento.ObterRa(id, t1.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(PeDominios.TipoDocumento.Ra, Assert.IsType<PeDocumentoResponse>(corpo).DocTipo);
        (status, corpo) = Resultado(await documento.ObterRr(id));
        Assert.Equal(PeDominios.TipoDocumento.Rr, Assert.IsType<PeDocumentoResponse>(corpo).DocTipo);

        // Texto e capítulo do RA pelas rotas dele
        var introducao = BlocoDoModelo("introducao", documento: PeDominios.TipoDocumento.Ra).Id;
        (status, corpo) = Resultado(await documento.SalvarTextoDoRa(id, t1.Id, introducao, Corpo(new { Texto = Rico("Introdução do ciclo.") })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeDocBlocoResponse>(corpo).EditadoPeloOrgao);
        (status, corpo) = Resultado(await documento.RestaurarTextoDoRa(id, t1.Id, introducao));
        Assert.False(Assert.IsType<PeDocBlocoResponse>(corpo).EditadoPeloOrgao);
        var anexos = CapituloDoModelo("anexos", PeDominios.TipoDocumento.Rr).Id;
        (status, corpo) = Resultado(await documento.AtualizarCapituloDoRr(id, anexos, Corpo(new { Oculto = true })));
        Assert.True(Assert.IsType<PeDocCapituloResponse>(corpo).Oculto);

        // O PDF: 201 com a versão; o download com o nome do RA e nosniff
        (status, corpo) = Resultado(await documento.GerarPdfDoRa(id, t1.Id));
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.Equal(1, Assert.IsType<PeDocVersaoResponse>(corpo).Numero);
        (status, corpo) = Resultado(await documento.VersoesDoRa(id, t1.Id));
        Assert.Single(Assert.IsType<List<PeDocVersaoResponse>>(corpo));
        var arquivo = Assert.IsType<FileContentResult>(await documento.ArquivoDoRa(id, t1.Id, 1));
        Assert.Equal(("application/pdf", "RA_SES_v1.0_2026-T1_1.pdf"), (arquivo.ContentType, arquivo.FileDownloadName));
        Assert.Equal("nosniff", documento.Response.Headers["X-Content-Type-Options"].ToString());

        // Versão que não existe e ciclo de outro PDTIC: 404 com corpo
        (status, corpo) = Resultado(await documento.ArquivoDoRr(id, 1));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeDocVersaoNaoEncontrada)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await documento.ObterRa(id, 999999));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeCicloNaoEncontrado)), (status, CodigoDe(corpo)));
    }
}
