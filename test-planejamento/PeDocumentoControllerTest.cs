using System.Text.Json;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas da E5 como o front chama (PeDocumentoController): 200 nas leituras e edições, 201
/// ao gerar o PDF e ao criar no modelo, 204 ao apagar no modelo, o PDF com o nome e o nosniff,
/// e os erros sempre como { Code, Message } com 400, 403, 404 ou 409.
/// </summary>
public class PeDocumentoControllerTest : PeDocumentoTestBase
{
    [Fact]
    public async Task Documento_LerEditarEsconder_EOsErrosComCorpo()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorDocumento(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.Obter(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.IsType<PeDocumentoResponse>(corpo);
        Assert.Equal(StatusCodes.Status200OK, Resultado(await ControladorDocumento(UserConsultaSes).Obter(pdtic.Id)).Status);

        var bloco = BlocoDoModelo("introducao").Id;
        (status, corpo) = Resultado(await orgao.SalvarTexto(pdtic.Id, bloco, Json(new { Texto = Doc(Paragrafo("Novo.")) })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeDocBlocoResponse>(corpo).EditadoPeloOrgao);
        (status, corpo) = Resultado(await orgao.RestaurarTexto(pdtic.Id, bloco));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(Assert.IsType<PeDocBlocoResponse>(corpo).EditadoPeloOrgao);

        // Corpo sem Texto: 400; texto fora da lista: 400; bloco de tabela: 400; consulta: 403
        (status, corpo) = Resultado(await orgao.SalvarTexto(pdtic.Id, bloco, Json(new { Outro = 1 })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDadosInvalidos), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.SalvarTexto(pdtic.Id, bloco, Json(new { Texto = "texto solto" })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDocTextoInvalido), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.SalvarTexto(pdtic.Id, BlocoDoModelo("ativos", "tabela_secao").Id, Json(new { Texto = Doc(Paragrafo("x")) })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDocBlocoNaoEditavel), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorDocumento(UserConsultaSes).SalvarTexto(pdtic.Id, bloco, Json(new { Texto = Doc(Paragrafo("x")) })));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.RestaurarTexto(pdtic.Id, 999_999));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeDocBlocoNaoEncontrado), (status, CodigoDe(corpo)));

        // Capítulo: esconder o opcional (200) e o travado (409)
        (status, corpo) = Resultado(await orgao.AtualizarCapitulo(pdtic.Id, CapituloDoModelo("termos").Id, Json(new { Oculto = true })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeDocCapituloResponse>(corpo).Oculto);
        (status, corpo) = Resultado(await orgao.AtualizarCapitulo(pdtic.Id, CapituloDoModelo("ativos").Id, Json(new { Oculto = true, TituloProprio = (string?)null })));
        Assert.Equal((StatusCodes.Status409Conflict, (int)ErrorCode.PeDocCapituloObrigatorio), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.AtualizarCapitulo(pdtic.Id, 999_999, Json(new { Oculto = true })));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeDocCapituloNaoEncontrado), (status, CodigoDe(corpo)));

        // Outro órgão: 403 com corpo
        (status, corpo) = Resultado(await ControladorDocumento(UserOrgaoSeec).Obter(pdtic.Id));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task Pdf_Gera201_ListaEBaixaComONome()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorDocumento(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.GerarPdf(pdtic.Id));
        Assert.Equal(StatusCodes.Status201Created, status);
        var versao = Assert.IsType<PeDocVersaoResponse>(corpo);
        Assert.Equal(1, versao.Numero);

        (status, corpo) = Resultado(await ControladorDocumento(UserPeSgdi).Versoes(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(Assert.IsType<List<PeDocVersaoResponse>>(corpo));

        var consulta = ControladorDocumento(UserConsultaSes);
        var arquivo = Assert.IsType<FileContentResult>(await consulta.Arquivo(pdtic.Id, 1));
        Assert.Equal("application/pdf", arquivo.ContentType);
        Assert.Equal("PDTIC_SES_v1.0_1.pdf", arquivo.FileDownloadName);
        Assert.Equal("nosniff", consulta.Response.Headers["X-Content-Type-Options"].ToString());

        (status, corpo) = Resultado(await consulta.Arquivo(pdtic.Id, 5));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeDocVersaoNaoEncontrada), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await consulta.GerarPdf(pdtic.Id));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task Modelo_LeQualquerPapel_AlteraSoOAdministrador()
    {
        var (status, corpo) = Resultado(await ControladorDocumento(UserOrgaoSes).ObterModelo("pdtic"));
        Assert.Equal(StatusCodes.Status200OK, status);
        var modelo = Assert.IsType<PeDocModeloResponse>(corpo);
        Assert.NotEmpty(modelo.Marcadores);

        (status, corpo) = Resultado(await ControladorDocumento(UserPeSgdi).CriarCapitulo(Json(new { Titulo = "Extra" })));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));

        var admin = ControladorDocumento(UserPeAdmin);
        (status, corpo) = Resultado(await admin.CriarCapitulo(Json(new { Titulo = "Extra", Numerado = false })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var capitulo = Assert.IsType<PeDocModeloCapituloResponse>(corpo);
        (status, corpo) = Resultado(await admin.AtualizarCapituloDoModelo(capitulo.Id, Json(new { Titulo = "Extra renomeado" })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Extra renomeado", Assert.IsType<PeDocModeloCapituloResponse>(corpo).Titulo);

        (status, corpo) = Resultado(await admin.CriarBloco(Json(new { CapituloId = capitulo.Id, Tipo = "texto", Config = new { Texto = Doc(Paragrafo("Texto do capítulo extra.")) } })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var bloco = Assert.IsType<PeDocModeloBlocoResponse>(corpo);
        (status, corpo) = Resultado(await admin.AtualizarBloco(bloco.Id, Json(new { Config = new { Texto = Doc(Paragrafo("Outro texto.")), PaginaDeitada = true } })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(Assert.IsType<PeDocModeloBlocoResponse>(corpo).Config.GetProperty("PaginaDeitada").GetBoolean());
        (status, corpo) = Resultado(await admin.AtualizarBloco(bloco.Id, Json(new { Config = new { Secao = "ativos" } })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDocConfigInvalida), (status, CodigoDe(corpo)));

        (status, _) = Resultado(await admin.OrdenarBlocos(Json(new { Ids = new[] { bloco.Id } })));
        Assert.Equal(StatusCodes.Status200OK, status);
        var ids = modelo.Capitulos.Where(c => c.PaiId == null).Select(c => c.Id).Append(capitulo.Id).Reverse().ToArray();
        (status, corpo) = Resultado(await admin.OrdenarCapitulos(Json(new { Ids = ids })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(capitulo.Id, Assert.IsType<PeDocModeloResponse>(corpo).Capitulos[0].Id);
        (status, corpo) = Resultado(await admin.OrdenarCapitulos(Json(new { Ids = new[] { capitulo.Id } })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeOrdemInvalida), (status, CodigoDe(corpo)));

        Assert.Equal(StatusCodes.Status204NoContent, Resultado(await admin.ExcluirBloco(bloco.Id)).Status);
        Assert.Equal(StatusCodes.Status204NoContent, Resultado(await admin.ExcluirCapitulo(capitulo.Id)).Status);
        (status, corpo) = Resultado(await admin.ExcluirCapitulo(CapituloDoModelo("ativos").Id));
        Assert.Equal((StatusCodes.Status409Conflict, (int)ErrorCode.PeItemTravado), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await admin.ExcluirCapitulo(CapituloDoModelo("termos").Id));
        Assert.Equal((StatusCodes.Status409Conflict, (int)ErrorCode.PeItemDoSistema), (status, CodigoDe(corpo)));

        // Corpo que não é objeto
        (status, corpo) = Resultado(await admin.CriarCapitulo(Json(new[] { 1 })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDadosInvalidos), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task SemCadastro_404_ComCorpo()
    {
        var principal = PrincipalDe("kc-ninguem", "ninguem@df.gov.br", app.Auth.Perfis.Basico);
        var controlador = new Controllers.Planejamento.PeDocumentoController(Documentos, ModeloDoc, Permissoes)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };

        var (status, corpo) = Resultado(await controlador.ObterModelo(null));

        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeUsuarioNaoEncontrado), (status, CodigoDe(corpo)));
    }
}
