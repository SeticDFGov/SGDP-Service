using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Edição do documento pela equipe do órgão (PUT e DELETE dos blocos de texto): quem edita,
/// em que situação do PDTIC, que bloco aceita texto, o texto rico pela lista fechada e as
/// imagens (a enviada por quem grava passa a ser do PDTIC e abre para quem vê o órgão).
/// </summary>
public class PeDocumentoEdicaoTest : PeDocumentoTestBase
{
    private static int Codigo(ApiException ex) => ex.Error.Code;

    [Fact]
    public async Task SoAEquipeDoOrgaoEOAdminGeral_EditamOTexto()
    {
        var pdtic = await AbrirSesAsync();
        var bloco = BlocoDoModelo("introducao").Id;
        var texto = Json(Doc(Paragrafo("Texto do órgão.")));

        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
        {
            var ex = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.SalvarTextoAsync(pdtic.Id, bloco, texto, await ContextoDe(user)));
            Assert.Equal((int)ErrorCode.PeSemPermissao, Codigo(ex));
            ex = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.RestaurarTextoAsync(pdtic.Id, bloco, await ContextoDe(user)));
            Assert.Equal((int)ErrorCode.PeSemPermissao, Codigo(ex));
        }
        // Equipe de outro órgão nem vê o PDTIC
        var outro = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.SalvarTextoAsync(pdtic.Id, bloco, texto, await ContextoDe(UserOrgaoSeec)));
        Assert.Equal((int)ErrorCode.PeSemPermissao, Codigo(outro));

        Assert.True((await Documentos.SalvarTextoAsync(pdtic.Id, bloco, texto, await ContextoDe(UserAdminGeral))).EditadoPeloOrgao);
        Assert.True((await Documentos.SalvarTextoAsync(pdtic.Id, bloco, Json(Doc(Paragrafo("Outro texto."))), await Orgao())).EditadoPeloOrgao);

        // Gravar toca o PDTIC (alterado em e por)
        Assert.Equal(UserOrgaoSes.Email, Context.PePdtics.AsNoTracking().Single(p => p.Id == pdtic.Id).AlteradoPor);
    }

    [Fact]
    public async Task PdticForaDaElaboracao_409_MasContinuaLegivel()
    {
        var pdtic = await AbrirSesAsync();
        var entidade = Context.PePdtics.Single(p => p.Id == pdtic.Id);
        entidade.Situacao = PeDominios.SituacaoPdtic.EmAprovacao;
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
            await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, Json(Doc(Paragrafo("x"))), await Orgao()));
        Assert.Equal((int)ErrorCode.PePdticFechado, Codigo(ex));
        ex = await Assert.ThrowsAsync<ApiException>(async () =>
            await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("termos").Id,
                new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, await Orgao()));
        Assert.Equal((int)ErrorCode.PePdticFechado, Codigo(ex));

        var documento = await DocumentoAsync(pdtic.Id);
        Assert.False(documento.PodeEditar);
    }

    [Fact]
    public async Task SoBlocoDeTexto_DoDocumentoDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var ctx = await Orgao();
        var texto = Json(Doc(Paragrafo("x")));

        var tabela = await Assert.ThrowsAsync<ApiException>(() =>
            Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("ativos", "tabela_secao").Id, texto, ctx));
        Assert.Equal((int)ErrorCode.PeDocBlocoNaoEditavel, Codigo(tabela));

        // Capítulo que não está no documento do Básico (riscos) e bloco que não existe: 404
        var fora = await Assert.ThrowsAsync<ApiException>(() => Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("riscos").Id, texto, ctx));
        Assert.Equal((int)ErrorCode.PeDocBlocoNaoEncontrado, Codigo(fora));
        var inexistente = await Assert.ThrowsAsync<ApiException>(() => Documentos.SalvarTextoAsync(pdtic.Id, 999_999, texto, ctx));
        Assert.Equal((int)ErrorCode.PeDocBlocoNaoEncontrado, Codigo(inexistente));

        // Bloco de capítulo oculto também não é editado
        await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("termos").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx);
        var oculto = await Assert.ThrowsAsync<ApiException>(() => Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("termos").Id, texto, ctx));
        Assert.Equal((int)ErrorCode.PeDocBlocoNaoEncontrado, Codigo(oculto));
    }

    [Theory]
    [InlineData("blockquote", "citação")]
    [InlineData("codeBlock", "bloco de código")]
    public async Task TextoForaDaLista_400ComORecurso(string no, string nome)
    {
        var pdtic = await AbrirSesAsync();
        var texto = Json(Doc(new { type = no, content = new object[] { Paragrafo("x") } }));

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
            await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, texto, await Orgao()));

        Assert.Equal((int)ErrorCode.PeDocTextoInvalido, Codigo(ex));
        Assert.Contains(nome, ex.Error.Message);
    }

    [Fact]
    public async Task LinkQueNaoEhHttp_400()
    {
        var pdtic = await AbrirSesAsync();
        var texto = Json(Doc(new
        {
            type = "paragraph",
            content = new object[] { new { type = "text", text = "clique", marks = new object[] { new { type = "link", attrs = new { href = "javascript:alert(1)" } } } } }
        }));

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
            await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, texto, await Orgao()));
        Assert.Equal((int)ErrorCode.PeDocTextoInvalido, Codigo(ex));
    }

    [Fact]
    public async Task Imagem_DeQuemGrava_PassaASerDoPdtic_EAbreParaQuemVeOOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var imagem = await EnviarArquivoAsync(UserOrgaoSes, "grafico.png", PngDeVerdade());
        var texto = Json(Doc(Paragrafo("Veja o gráfico."), new { type = "image", attrs = new { src = $"/api/planejamento/arquivos/{imagem.Id}", alt = "Gráfico" } }));

        var bloco = await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, texto, await Orgao());

        Assert.Contains($"\"src\":\"api/planejamento/arquivos/{imagem.Id}\"", bloco.TextoBruto!.Value.GetRawText());
        var arquivo = Context.PeArquivos.AsNoTracking().Single(a => a.Id == imagem.Id);
        Assert.Equal((PeDominios.DonoArquivo.Pdtic, (long?)pdtic.Id), (arquivo.DonoTipo, arquivo.DonoId));
        // A consulta do órgão e a SGDI baixam; outro órgão não
        Assert.NotEmpty((await Arquivos.BaixarAsync(imagem.Id, await ContextoDe(UserConsultaSes))).Conteudo);
        Assert.NotEmpty((await Arquivos.BaixarAsync(imagem.Id, await ContextoDe(UserPeSgdi))).Conteudo);
        await Assert.ThrowsAsync<ApiException>(async () => await Arquivos.BaixarAsync(imagem.Id, await ContextoDe(UserOrgaoSeec)));

        // Gravar de novo com a mesma imagem (já do PDTIC) vale
        Assert.True((await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("conclusao").Id, texto, await Orgao())).EditadoPeloOrgao);
    }

    [Fact]
    public async Task Imagem_DeOutraPessoa_OuQueNaoEhImagem_400()
    {
        var pdtic = await AbrirSesAsync();
        var daAdmin = await EnviarArquivoAsync(UserAdminGeral, "outra.png", PngDeVerdade());
        var pdf = await EnviarArquivoAsync(UserOrgaoSes, "ato.pdf", Pdf());

        foreach (var id in new[] { daAdmin.Id, pdf.Id, 999_999L })
        {
            var texto = Json(Doc(new { type = "image", attrs = new { src = $"api/planejamento/arquivos/{id}" } }));
            var ex = await Assert.ThrowsAsync<ApiException>(async () =>
                await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, texto, await Orgao()));
            Assert.Equal((int)ErrorCode.PeDocTextoInvalido, Codigo(ex));
        }
        Assert.Null(Context.PeArquivos.AsNoTracking().Single(a => a.Id == daAdmin.Id).DonoTipo);
    }
}
