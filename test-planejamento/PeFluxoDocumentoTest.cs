using System.Text.RegularExpressions;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O fluxo no documento do PDTIC (E6): o bloco de fluxo da E5 passa a trazer o SVG (a cópia do
/// órgão ou o modelo), com os nomes do dicionário e a descrição; o fluxo largo vai para a
/// página deitada; e o PDF desenha o fluxo (o QuestPDF desenha o SVG com o texto em contorno).
/// Desde a versão 5 do modelo (E7), o documento novo já traz os fluxos das etapas: na
/// metodologia, os da elaboração, da preparação, do diagnóstico e do planejamento; na revisão
/// e acompanhamento, o geral e os das etapas 4 a 7. Com a variável PE_EXEMPLO_FLUXOS, grava o
/// PDF de exemplo.
/// </summary>
public class PeFluxoDocumentoTest : PeFluxoTestBase
{
    private static int Deitadas(string pdf) => Regex.Matches(pdf, @"/MediaBox\s*\[0 0 842(\.\d+)? 595(\.\d+)?\]").Count;

    private static List<PeDocFluxoResponse> FluxosDe(PeDocumentoResponse documento, string capitulo) =>
        Cap(documento, capitulo).Blocos.Where(b => b.Tipo == PeDominios.TipoBloco.Fluxo).Select(b => b.Fluxo!).ToList();

    [Fact]
    public async Task BlocoDeFluxo_TrazOSvg_DoModeloOuDaCopia_ComOsNomesDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherNomesAsync(pdtic.Id);

        var documento = await DocumentoAsync(pdtic.Id);
        var fluxos = FluxosDe(documento, "metodologia");
        Assert.Equal(new[] { "elaboracao", "preparacao", "diagnostico", "planejamento" }, fluxos.Select(f => f.Chave));
        Assert.All(fluxos, f => Assert.StartsWith("<svg", f.Svg));
        var preparacao = fluxos[1];
        Assert.Equal("Preparação (figura 6 do guia)", preparacao.Nome);
        Assert.False(preparacao.Personalizado);
        Assert.Equal("Raias: Subcomitê Gestor de TIC da Saúde, Equipe de Elaboração do PDTIC.", preparacao.Descricao[0]);
        Assert.Contains("2. 1.1 Definir a abrangência e o período do PDTIC (Subcomitê Gestor de TIC da Saúde).", preparacao.Descricao);
        Assert.Equal(new[] { "acompanhamento", "planejamento_acompanhamento", "monitoramento", "avaliacao_intermediaria", "avaliacao_final" },
            FluxosDe(documento, "revisao_acompanhamento").Select(f => f.Chave));

        // A cópia do órgão entra no lugar do modelo
        var definicao = DefinicaoDoModelo("preparacao");
        definicao.Elementos.Single(e => e.Id == "t3").Nome = "Descrever a metodologia da Saúde";
        await Fluxos.SalvarAsync(pdtic.Id, "preparacao", Corpo(definicao, "Preparação da Saúde"), await Orgao());
        var adaptado = FluxosDe(await DocumentoAsync(pdtic.Id, UserConsultaSes), "metodologia")[1];
        Assert.True(adaptado.Personalizado);
        Assert.Equal("Preparação da Saúde (adaptado da figura 6 do guia)", adaptado.Nome);
        Assert.Contains(adaptado.Descricao, l => l.Contains("1.3 Descrever a metodologia da Saúde"));
    }

    [Fact]
    public async Task FluxoLargo_VaiParaAPaginaDeitada()
    {
        var pdtic = await AbrirSesAsync();

        var blocos = Cap(await DocumentoAsync(pdtic.Id), "metodologia").Blocos.Where(b => b.Tipo == PeDominios.TipoBloco.Fluxo).ToList();
        var elaboracao = blocos.Single(b => b.Fluxo!.Chave == "elaboracao");
        var diagnostico = blocos.Single(b => b.Fluxo!.Chave == "diagnostico");
        Assert.False(elaboracao.PaginaDeitada);
        Assert.True(diagnostico.PaginaDeitada);
        Assert.True(PeDocumentoPdf.TamanhoDoSvg(diagnostico.Fluxo!.Svg!)!.Value.Largura > PeDocumentoService.LarguraDoFluxoEmPe);

        // No roteiro do PDF, o fluxo largo abre páginas deitadas
        var roteiro = PeDocumentoPdf.Montar(await DocumentoAsync(pdtic.Id));
        Assert.Contains(roteiro.Grupos, g => g.Deitada && g.Pecas.Any(p => p.Bloco?.Fluxo?.Chave == "diagnostico"));
        Assert.Contains(roteiro.Grupos, g => !g.Deitada && g.Pecas.Any(p => p.Bloco?.Fluxo?.Chave == "elaboracao"));
    }

    [Fact]
    public async Task FluxoQueNaoExiste_NomeDoGuiaESemDesenho()
    {
        var pdtic = await AbrirSesAsync();
        // Um modelo apagado por fora (a chave do bloco continua no documento)
        Context.PeFluxosModelo.Remove(Context.PeFluxosModelo.Single(m => m.Chave == "acompanhamento"));
        Context.SaveChanges();
        var fluxo = FluxosDe(await DocumentoAsync(pdtic.Id), "revisao_acompanhamento").Single(f => f.Chave == "acompanhamento");
        Assert.Null(fluxo.Svg);
        Assert.Equal("Processo de acompanhamento do PDTIC (figura 17 do guia)", fluxo.Nome);
    }

    [Fact]
    public async Task Pdf_DesenhaOsFluxos_ComAPaginaDeitada()
    {
        var pdtic = await PdticCompletoAsync();
        var ctx = await Orgao();

        var versao = await Documentos.GerarPdfAsync(pdtic.Id, ctx);
        var pdf = (await Documentos.ArquivoDaVersaoAsync(pdtic.Id, versao.Numero, ctx)).Conteudo;
        var pasta = PastaDeExemplo();
        if (pasta != null) await File.WriteAllBytesAsync(Path.Combine(pasta, "exemplo-pdtic-com-fluxos.pdf"), pdf);

        var texto = TextoDoPdf(pdf);
        Assert.StartsWith("%PDF-", texto);
        // Os três fluxos largos da metodologia saem em páginas deitadas (além das tabelas largas)
        Assert.True(Deitadas(texto) >= 5, $"páginas deitadas: {Deitadas(texto)}");
        Assert.Equal(versao.Paginas, PeDocumentoPdf.ContarPaginas(pdf));
    }

    [Fact]
    public void Pdf_DoFluxoSozinho_OQuestPdfDesenhaOSvg()
    {
        // Cada fluxo do guia passa pelo SvgImage do QuestPDF sem erro (o mesmo SVG da prévia)
        foreach (var modelo in Context.PeFluxosModelo.AsNoTracking().ToList())
        {
            var desenho = PeFluxoDesenho.Desenhar(PeFluxoDefinicaoLeitor.DoBanco(modelo.Definicao), modelo.Nome, NomesPadrao());
            var svg = QuestPDF.Infrastructure.SvgImage.FromText(desenho.Svg);
            Assert.NotNull(svg);
        }
    }
}
