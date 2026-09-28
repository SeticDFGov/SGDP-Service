using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O conversor único do texto rico (JSON do TipTap) para o QuestPDF, nó por nó: parágrafo,
/// quebra de linha, títulos 3 e 4, negrito, itálico, sublinhado, link, listas com marcadores e
/// numeradas, tabela com cabeçalho e mesclagem, imagem (e a que falta). O PDF é conferido pelo
/// que dá para ler sem abrir o conteúdo: as fontes embutidas (Lato normal, negrito, itálico),
/// os links (/URI), as imagens (/Subtype /Image) e o número de páginas.
/// </summary>
public class PeTextoRicoPdfTest
{
    public PeTextoRicoPdfTest()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static JsonNode No(object valor) => JsonNode.Parse(JsonSerializer.Serialize(valor))!;

    private static object Doc(params object[] nos) => new { type = "doc", content = nos };

    private static object P(params object[] inline) => new { type = "paragraph", content = inline };

    private static object T(string texto, params string[] marcas) =>
        marcas.Length == 0 ? new { type = "text", text = texto } : (object)new { type = "text", text = texto, marks = marcas.Select(m => new { type = m }).ToArray() };

    private static object Item(string texto) => new { type = "listItem", content = new object[] { P(T(texto)) } };

    private static byte[] Pdf(object documento, Func<long, PeImagemPdf?>? imagem = null) =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(40);
            p.Content().Element(x => PeTextoRicoPdf.Desenhar(x, No(documento), new PeTextoRicoPdf.Opcoes { Imagem = imagem ?? (_ => null) }));
        })).GeneratePdf();

    private static string Bruto(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static bool TemFonte(byte[] pdf, string fonte) => Regex.IsMatch(Bruto(pdf), $@"/BaseFont\s*/[A-Z]{{6}}\+{fonte}\b");

    private static int Imagens(byte[] pdf) => Regex.Matches(Bruto(pdf), @"/Subtype\s*/Image").Count;

    private static void PdfValido(byte[] pdf, int paginas = 1)
    {
        Assert.StartsWith("%PDF-", Bruto(pdf));
        Assert.Equal(paginas, PeDocumentoPdf.ContarPaginas(pdf));
    }

    [Fact]
    public void Paragrafo_QuebraDeLinha_EParagrafoVazio()
    {
        var pdf = Pdf(Doc(P(T("Primeira linha"), new { type = "hardBreak" }, T("segunda linha")), new { type = "paragraph" }, P(T("Fim."))));

        PdfValido(pdf);
        Assert.True(TemFonte(pdf, "Lato-Regular"));
        Assert.False(TemFonte(pdf, "Lato-Bold"));
    }

    [Fact]
    public void Titulos_DeNivel3E4_SaemEmNegrito()
    {
        var pdf = Pdf(Doc(
            new { type = "heading", attrs = new { level = 3 }, content = new object[] { T("Título três") } },
            new { type = "heading", attrs = new { level = 4 }, content = new object[] { T("Título quatro") } }));

        PdfValido(pdf);
        Assert.True(TemFonte(pdf, "Lato-Bold"));
        Assert.False(TemFonte(pdf, "Lato-Regular"));
    }

    [Fact]
    public void Negrito_Italico_ESublinhado()
    {
        var pdf = Pdf(Doc(P(T("negrito", "bold"), T(" itálico", "italic"), T(" os dois", "bold", "italic"), T(" sublinhado", "underline"))));

        PdfValido(pdf);
        Assert.True(TemFonte(pdf, "Lato-Bold"));
        Assert.True(TemFonte(pdf, "Lato-Italic"));
        Assert.True(TemFonte(pdf, "Lato-BoldItalic"));
        Assert.True(TemFonte(pdf, "Lato-Regular"));
    }

    [Fact]
    public void Link_ClicavelNoPdf_EOEnderecoQueNaoEhHttpNao()
    {
        var link = new { type = "text", text = "portal", marks = new object[] { new { type = "link", attrs = new { href = "https://www.df.gov.br/pdtic" } } } };
        var ruim = new { type = "text", text = "ruim", marks = new object[] { new { type = "link", attrs = new { href = "javascript:alert(1)" } } } };

        var pdf = Pdf(Doc(P(T("Veja o "), link, T(" e o "), ruim)));

        PdfValido(pdf);
        var uris = Regex.Matches(Bruto(pdf), @"/URI\s*\(([^)]*)\)").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "https://www.df.gov.br/pdtic" }, uris);
    }

    [Fact]
    public void Listas_ComMarcadores_ENumeradas_UmaDentroDaOutra()
    {
        var pdf = Pdf(Doc(
            new
            {
                type = "bulletList",
                content = new object[]
                {
                    Item("primeiro"),
                    new
                    {
                        type = "listItem",
                        content = new object[] { P(T("segundo")), new { type = "orderedList", attrs = new { start = 3, type = "I" }, content = new object[] { Item("três"), Item("quatro") } } }
                    }
                }
            },
            new { type = "orderedList", attrs = new { type = "a" }, content = new object[] { Item("a"), Item("b") } }));

        PdfValido(pdf);
        Assert.Equal("1", PeTextoRicoPdf.Numero(1, "1"));
        Assert.Equal("d", PeTextoRicoPdf.Numero(4, "a"));
        Assert.Equal("ab", PeTextoRicoPdf.Numero(28, "a"));
        Assert.Equal("D", PeTextoRicoPdf.Numero(4, "A"));
        Assert.Equal("ix", PeTextoRicoPdf.Numero(9, "i"));
        Assert.Equal("XIV", PeTextoRicoPdf.Numero(14, "I"));
    }

    [Fact]
    public void Tabela_CabecalhoRepetido_MesclagemELarguras()
    {
        var tabela = No(new
        {
            type = "table",
            content = new object[]
            {
                new { type = "tableRow", content = new object[]
                {
                    new { type = "tableHeader", attrs = new { colwidth = new[] { 200 } }, content = new object[] { P(T("A")) } },
                    new { type = "tableHeader", attrs = new { colspan = 2, colwidth = new[] { 100, 100 } }, content = new object[] { P(T("B e C")) } }
                } },
                new { type = "tableRow", content = new object[]
                {
                    new { type = "tableCell", attrs = new { rowspan = 2 }, content = new object[] { P(T("a1 e a2")) } },
                    new { type = "tableCell", content = new object[] { P(T("b1")) } },
                    new { type = "tableCell", content = new object[] { P(T("c1")) } }
                } },
                new { type = "tableRow", content = new object[]
                {
                    new { type = "tableCell", content = new object[] { P(T("b2")) } },
                    new { type = "tableCell", attrs = new { rowspan = 9 }, content = new object[] { P(T("c2")) } }
                } }
            }
        }).AsObject();

        var grade = PeTextoRicoPdf.Posicionar(tabela);

        Assert.Equal(3, grade.Colunas);
        Assert.Equal(1, grade.LinhasCabecalho);
        Assert.Equal(new[] { 200f, 100f, 100f }, grade.Larguras);
        // A célula da terceira linha pula a coluna que a mesclagem de cima ocupa
        Assert.Equal((2, 1), grade.Celulas.Where(c => c.Linha == 2).Select(c => (c.Linha, c.Coluna)).First());
        // A mesclagem que passa do fim da tabela é cortada
        Assert.Equal(1, grade.Celulas.Single(c => c.Linha == 2 && c.Coluna == 2).Linhas);
        Assert.Equal(2, grade.Celulas.Single(c => c.Linha == 1 && c.Coluna == 0).Linhas);

        PdfValido(Pdf(Doc(tabela)));

        // Tabela longa: o cabeçalho repete e a tabela passa de página
        var linhas = new List<object>
        {
            new { type = "tableRow", content = new object[] { new { type = "tableHeader", content = new object[] { P(T("Item")) } }, new { type = "tableHeader", content = new object[] { P(T("Descrição")) } } } }
        };
        for (var i = 1; i <= 120; i++)
            linhas.Add(new { type = "tableRow", content = new object[] { new { type = "tableCell", content = new object[] { P(T($"{i}")) } }, new { type = "tableCell", content = new object[] { P(T($"Linha {i} da tabela longa")) } } } });
        var longa = Pdf(Doc(new { type = "table", content = linhas }));
        Assert.True(PeDocumentoPdf.ContarPaginas(longa) >= 3);
    }

    [Fact]
    public void Imagem_DoArquivo_ForaDoParagrafoEDentroDele_EAQueFaltaViraAviso()
    {
        var png = PeImagemPdf.Abrir(PeImagensDeTeste.Png(200, 100));
        Assert.NotNull(png);
        Assert.Equal((200, 100), (png!.Largura, png.Altura));
        PeImagemPdf? Imagem(long id) => id == 7 ? png : null;

        var sozinha = Pdf(Doc(new { type = "image", attrs = new { src = "api/planejamento/arquivos/7", alt = "Gráfico", width = 150 } }), Imagem);
        PdfValido(sozinha);
        Assert.Equal(1, Imagens(sozinha));

        var noParagrafo = Pdf(Doc(P(T("antes "), new { type = "image", attrs = new { src = "/api/planejamento/arquivos/7" } }, T(" depois"))), Imagem);
        Assert.Equal(1, Imagens(noParagrafo));

        var falta = Pdf(Doc(new { type = "image", attrs = new { src = "api/planejamento/arquivos/8", alt = "Organograma" } }), Imagem);
        PdfValido(falta);
        Assert.Equal(0, Imagens(falta));
        Assert.True(TemFonte(falta, "Lato-Italic"));

        // Conteúdo que não abre (só o começo de um PNG) não vira imagem
        var cabecalho = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(cabecalho, 0);
        Assert.Null(PeImagemPdf.Abrir(cabecalho));
        Assert.Null(PeImagemPdf.Abrir(Encoding.ASCII.GetBytes("%PDF-1.7 nada")));
        // O JPEG do QuestPDF: o tamanho sai do marcador SOF (a proporção é a pedida)
        var jpeg = PeImagemPdf.Tamanho(Placeholders.Image(300, 120));
        Assert.NotNull(jpeg);
        Assert.InRange(jpeg!.Value.Largura / (double)jpeg.Value.Altura, 2.4, 2.7);
    }

    [Fact]
    public void DocumentoLongo_PassaDePagina_SemErro()
    {
        var paragrafos = Enumerable.Range(1, 160)
            .Select(i => P(T($"Parágrafo {i}: texto corrido do diagnóstico, com frases de tamanho normal para ocupar a linha inteira da página.")))
            .ToArray();

        var pdf = Pdf(Doc(paragrafos));

        Assert.True(PeDocumentoPdf.ContarPaginas(pdf) >= 4);
    }

    [Fact]
    public void NoForaDaLista_EhIgnorado()
    {
        var pdf = Pdf(Doc(new { type = "blockquote", content = new object[] { P(T("citação")) } }, P(T("texto"))));

        PdfValido(pdf);
    }
}
