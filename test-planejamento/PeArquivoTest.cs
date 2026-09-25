using System.Security.Cryptography;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Anexos (E3): envio (tamanho, lista fechada de extensões, conteúdo que bate com a
/// extensão, imagem até 5 MB, nome saneado, MIME da extensão, hash), quem envia e quem
/// baixa (sem dono: só quem enviou; com dono: quem vê o dono do registro) e o nosniff.
/// </summary>
public class PeArquivoTest : PeReferenciaisTestBase
{
    [Fact]
    public async Task Enviar_PdfPngEJpeg_ComOMimeDaExtensao_EOHash()
    {
        var pdf = await EnviarArquivoAsync(UserPeAdmin, "ato.pdf", Pdf());
        var png = await EnviarArquivoAsync(UserPeAdmin, "logo.PNG", Png());
        var jpeg = await EnviarArquivoAsync(UserPeAdmin, "foto.jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 });

        Assert.Equal(("ato.pdf", "application/pdf", 1000L), (pdf.Nome, pdf.TipoMime, pdf.Tamanho));
        Assert.Equal("image/png", png.TipoMime);
        Assert.Equal("image/jpeg", jpeg.TipoMime);
        var guardado = Context.PeArquivos.AsNoTracking().Single(a => a.Id == pdf.Id);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf())).ToLowerInvariant(), guardado.Hash);
        Assert.Null(guardado.DonoTipo);
        Assert.Equal(UserPeAdmin.Email, guardado.CriadoPor);
        Assert.Equal(Pdf(), Context.PeArquivosConteudo.AsNoTracking().Single(c => c.Id == pdf.Id).Conteudo);
    }

    [Fact]
    public async Task Enviar_Recusa_ExtensaoConteudoTamanhoEVazio_ESaneiaONome()
    {
        async Task<string> Erro(string nome, byte[] conteudo)
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => EnviarArquivoAsync(UserPeAdmin, nome, conteudo));
            Assert.Equal((int)ErrorCode.PeArquivoInvalido, ex.Error.Code);
            return ex.Error.Message;
        }

        Assert.Contains("não aceito", await Erro("ata.docx", Pdf()));
        Assert.Contains("sem extensão", await Erro("ata", Pdf()));
        Assert.Contains("não é PDF", await Erro("ata.pdf", Png()));
        Assert.Contains("não é PNG", await Erro("logo.png", Pdf()));
        Assert.Contains("5 MB", await Erro("logo.png", Png(6 * 1024 * 1024)));
        Assert.Contains("25 MB", await Erro("grande.pdf", Pdf(26 * 1024 * 1024)));
        Assert.Contains("Envie um arquivo", await Erro("vazio.pdf", Array.Empty<byte>()));
        Assert.Empty(Context.PeArquivos);

        var saneado = await EnviarArquivoAsync(UserPeAdmin, "C:\\pasta\\..\\ata final.pdf", Pdf());
        Assert.Equal("ata final.pdf", saneado.Nome);
    }

    [Fact]
    public async Task Enviar_SoQuemEditaAlgumaCoisaNoModulo()
    {
        foreach (var user in new[] { UserPeSgdi, UserConsultaSes })
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => EnviarArquivoAsync(user, "ato.pdf", Pdf()));
            Assert.Equal((int)ErrorCode.PeSemPermissao, ex.Error.Code);
        }
        foreach (var user in new[] { UserPeAdmin, UserPeCgtic, UserOrgaoSes, UserAdminGeral })
            Assert.True((await EnviarArquivoAsync(user, "ato.pdf", Pdf())).Id > 0);
    }

    [Fact]
    public async Task Baixar_SemDonoSoQuemEnviou_ComDonoQuemVeODono()
    {
        var secao = await SecaoDfAsync("Atos");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        await CampoAsync(secao.Id, "ato", "arquivo");
        var arquivo = await EnviarArquivoAsync(UserPeAdmin, "ato.pdf", Pdf());

        // Sem dono: só quem enviou
        Assert.Equal(Pdf(), (await Arquivos.BaixarAsync(arquivo.Id, await Admin())).Conteudo);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Arquivos.BaixarAsync(arquivo.Id, await ContextoDe(UserPeSgdi))));
        Assert.Equal(Codigo(ErrorCode.PeArquivoNaoEncontrado), await ErroAsync(async () => await Arquivos.BaixarAsync(999, await Admin())));

        // No catálogo do DF: qualquer papel do módulo
        await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Ato", ato = new { ArquivoId = arquivo.Id } }), await Admin());
        var baixado = await Arquivos.BaixarAsync(arquivo.Id, await ContextoDe(UserConsultaSes));
        Assert.Equal(("ato.pdf", "application/pdf"), (baixado.Nome, baixado.TipoMime));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Arquivos.BaixarAsync(arquivo.Id, await ContextoDe(UserSemPapel))));
    }

    [Fact]
    public async Task Baixar_AnexoDeRascunhoDoPetic_NaoAbreParaOPapelDeOrgao()
    {
        var anexo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("petic_diretriz").Id, Chave = "anexo", Rotulo = "Anexo", Tipo = "arquivo" }, EmailAdmin);
        await Modelo.DefinirSituacaoCampoAsync(anexo.Id, new PeSituacoesDTO { SituacaoGeral = "opcional" }, EmailAdmin);
        var rascunho = await RascunhoAsync();
        var arquivo = await EnviarArquivoAsync(UserPeAdmin, "estudo.pdf", Pdf());
        await IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Diretriz", anexo = new { ArquivoId = arquivo.Id } });

        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Arquivos.BaixarAsync(arquivo.Id, await ContextoDe(UserOrgaoSes))));
        Assert.Equal("estudo.pdf", (await Arquivos.BaixarAsync(arquivo.Id, await ContextoDe(UserPeSgdi))).Nome);
    }

    [Fact]
    public async Task Controller_Envio201_EDownloadComNosniff()
    {
        var controller = ControladorArquivos(UserPeAdmin);
        using var conteudo = new MemoryStream(Pdf());
        var formulario = new FormFile(conteudo, 0, conteudo.Length, "arquivo", "ato.pdf");

        var (status, corpo) = Resultado(await controller.Enviar(formulario));
        Assert.Equal(StatusCodes.Status201Created, status);
        var enviado = Assert.IsType<PeArquivoResponse>(corpo);
        Assert.Equal("ato.pdf", enviado.Nome);

        var semArquivo = Resultado(await controller.Enviar(null));
        Assert.Equal(400, semArquivo.Status);
        Assert.Equal((int)ErrorCode.PeArquivoInvalido, CodigoDe(semArquivo.Valor));

        var baixar = ControladorArquivos(UserPeAdmin);
        var arquivo = Assert.IsType<FileContentResult>(await baixar.Baixar(enviado.Id));
        Assert.Equal("application/pdf", arquivo.ContentType);
        Assert.Equal("ato.pdf", arquivo.FileDownloadName);
        Assert.Equal("nosniff", baixar.Response.Headers["X-Content-Type-Options"].ToString());

        var outro = Resultado(await ControladorArquivos(UserPeSgdi).Baixar(enviado.Id));
        Assert.Equal(403, outro.Status);
        Assert.Equal((int)ErrorCode.PeSemPermissao, CodigoDe(outro.Valor));
    }
}
