using System.Text.Json;
using System.Text.Json.Nodes;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Validação do texto rico (E4): a lista fechada de nós e marcas do contrato, cada nó no
/// lugar em que o editor o põe, atributos só os conhecidos (os outros saem), título 3 ou 4,
/// link http ou https, imagem só do próprio módulo, e o documento sem letra nem imagem vazio.
/// </summary>
public class PeTextoRicoTest
{
    private static PeTextoRico.Resultado Validar(string json) => PeTextoRico.Validar(JsonDocument.Parse(json).RootElement);

    private static string Doc(string conteudo) => "{\"type\":\"doc\",\"content\":[" + conteudo + "]}";

    /// <summary>JSON igual pelo conteúdo (o ToJsonString escapa acentos).</summary>
    private static void Igual(string esperado, JsonNode? atual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(esperado), atual), $"esperado {esperado}, veio {atual?.ToJsonString()}");

    private const string Paragrafo = "{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Olá\"}]}";

    [Fact]
    public void TodosOsNosEMarcasDaLista_SaoAceitos_ELimpos()
    {
        var json = Doc(
            "{\"type\":\"heading\",\"attrs\":{\"level\":4,\"textAlign\":\"left\",\"id\":\"x\"},\"content\":[{\"type\":\"text\",\"text\":\"Título\"}]},"
            + "{\"type\":\"paragraph\",\"attrs\":{\"textAlign\":\"center\"},\"content\":["
            + "{\"type\":\"text\",\"text\":\"negrito\",\"marks\":[{\"type\":\"bold\"},{\"type\":\"italic\"},{\"type\":\"underline\"},{\"type\":\"bold\"}]},"
            + "{\"type\":\"hardBreak\"},"
            + "{\"type\":\"text\",\"text\":\"link\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"http://www.df.gov.br/a?b=1\",\"target\":\"_blank\",\"rel\":\"noopener noreferrer nofollow\",\"class\":null}}]}]},"
            + "{\"type\":\"bulletList\",\"content\":[{\"type\":\"listItem\",\"content\":[" + Paragrafo + ",{\"type\":\"orderedList\",\"attrs\":{\"start\":3,\"type\":\"a\"},\"content\":[{\"type\":\"listItem\",\"content\":[" + Paragrafo + "]}]}]}]},"
            + "{\"type\":\"table\",\"content\":[{\"type\":\"tableRow\",\"content\":[{\"type\":\"tableHeader\",\"attrs\":{\"colspan\":2,\"rowspan\":1,\"colwidth\":[100,200]},\"content\":[" + Paragrafo + "]}]},"
            + "{\"type\":\"tableRow\",\"content\":[{\"type\":\"tableCell\",\"attrs\":{\"colspan\":1,\"rowspan\":1,\"colwidth\":null,\"style\":\"color:red\"},\"content\":[" + Paragrafo + "]},{\"type\":\"tableCell\",\"content\":[" + Paragrafo + "]}]}]},"
            + "{\"type\":\"image\",\"attrs\":{\"src\":\"/api/planejamento/arquivos/12\",\"alt\":\"Organograma\",\"title\":null,\"width\":640}}");

        var resultado = Validar(json);

        Assert.Null(resultado.Erro);
        Assert.Equal(new long[] { 12 }, resultado.Imagens);
        var doc = resultado.Documento!;
        Igual("{\"level\":4}", doc["content"]![0]!["attrs"]!);
        Assert.Null(doc["content"]![1]!["attrs"]);
        var marcas = doc["content"]![1]!["content"]![0]!["marks"]!.AsArray();
        Assert.Equal(new[] { "bold", "italic", "underline" }, marcas.Select(m => m!["type"]!.GetValue<string>()));
        var link = doc["content"]![1]!["content"]![2]!["marks"]![0]!["attrs"]!;
        Igual("{\"href\":\"http://www.df.gov.br/a?b=1\",\"target\":\"_blank\",\"rel\":\"noopener noreferrer nofollow\"}", link);
        Igual("{\"start\":3,\"type\":\"a\"}", doc["content"]![2]!["content"]![0]!["content"]![1]!["attrs"]!);
        Igual("{\"colspan\":2,\"rowspan\":1,\"colwidth\":[100,200]}", doc["content"]![3]!["content"]![0]!["content"]![0]!["attrs"]!);
        Igual("{\"colspan\":1,\"rowspan\":1}", doc["content"]![3]!["content"]![1]!["content"]![0]!["attrs"]!);
        Igual("{\"src\":\"api/planejamento/arquivos/12\",\"alt\":\"Organograma\",\"width\":640}", doc["content"]![4]!["attrs"]!);
        Assert.Equal(new long[] { 12 }, PeTextoRico.ImagensDe(doc));
        Assert.True(PeTextoRico.TemConteudo(doc));
    }

    [Theory]
    [InlineData("{\"type\":\"blockquote\",\"content\":[{\"type\":\"paragraph\"}]}", "citação")]
    [InlineData("{\"type\":\"codeBlock\",\"content\":[{\"type\":\"text\",\"text\":\"x\"}]}", "bloco de código")]
    [InlineData("{\"type\":\"horizontalRule\"}", "linha horizontal")]
    [InlineData("{\"type\":\"taskList\",\"content\":[]}", "lista de tarefas")]
    [InlineData("{\"type\":\"iframe\"}", "iframe")]
    [InlineData("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"strike\"}]}]}", "tachado")]
    [InlineData("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"code\"}]}]}", "código")]
    [InlineData("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"textStyle\",\"attrs\":{\"color\":\"red\"}}]}]}", "estilo de texto")]
    public void ForaDaLista_Recusado_ComONomeDoRecurso(string no, string nome)
    {
        var resultado = Validar(Doc(no));
        Assert.Null(resultado.Documento);
        Assert.Contains("(" + nome + ")", resultado.Erro);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,oi")]
    [InlineData("/relativo")]
    [InlineData("ftp://servidor/arquivo")]
    [InlineData("mailto:a@b.c")]
    [InlineData("")]
    public void Link_SoHttpOuHttps_EnderecoCompleto(string href)
    {
        var resultado = Validar(Doc("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"" + href + "\"}}]}]}"));
        Assert.NotNull(resultado.Erro);
        Assert.Contains("http", resultado.Erro);
    }

    [Theory]
    [InlineData("https://exemplo.com/api/planejamento/arquivos/1")]
    [InlineData("api/planejamento/arquivos/1?x=1")]
    [InlineData("api/planejamento/arquivos/abc")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("api/planejamento/arquivos/0")]
    public void Imagem_SoArquivoDoModulo(string src)
    {
        var resultado = Validar(Doc("{\"type\":\"image\",\"attrs\":{\"src\":\"" + src + "\"}}"));
        Assert.Contains("botão de imagem", resultado.Erro);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Titulo_So3Ou4(int nivel)
    {
        var resultado = Validar(Doc("{\"type\":\"heading\",\"attrs\":{\"level\":" + nivel + "},\"content\":[{\"type\":\"text\",\"text\":\"T\"}]}"));
        Assert.Equal("Use só títulos de nível 3 ou 4.", resultado.Erro);
    }

    [Theory]
    // Texto solto no documento, item de lista num parágrafo, célula fora da linha
    [InlineData("{\"type\":\"text\",\"text\":\"solto\"}")]
    [InlineData("{\"type\":\"paragraph\",\"content\":[{\"type\":\"listItem\",\"content\":[]}]}")]
    [InlineData("{\"type\":\"table\",\"content\":[{\"type\":\"tableCell\",\"content\":[{\"type\":\"paragraph\"}]}]}")]
    // Lista, tabela e linha vazias; texto sem o texto; conteúdo que não é lista
    [InlineData("{\"type\":\"bulletList\",\"content\":[]}")]
    [InlineData("{\"type\":\"table\"}")]
    [InlineData("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\"}]}")]
    [InlineData("{\"type\":\"paragraph\",\"content\":{\"type\":\"text\",\"text\":\"x\"}}")]
    [InlineData("{\"semTipo\":true}")]
    public void EstruturaFora_DoLugar_Recusada(string no)
    {
        Assert.Equal("O texto formatado veio num formato que não serve. Atualize a tela e tente de novo.", Validar(Doc(no)).Erro);
    }

    [Fact]
    public void NaoEDocumento_Recusado()
    {
        Assert.NotNull(Validar("{\"type\":\"paragraph\"}").Erro);
        Assert.NotNull(Validar("[1,2]").Erro);
        Assert.NotNull(Validar("\"texto\"").Erro);
    }

    [Fact]
    public void SemLetraNemImagem_Vazio()
    {
        foreach (var json in new[]
                 {
                     "{\"type\":\"doc\"}",
                     Doc(""),
                     Doc("{\"type\":\"paragraph\"}"),
                     Doc("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"   \"},{\"type\":\"hardBreak\"}]}"),
                     Doc("{\"type\":\"table\",\"content\":[{\"type\":\"tableRow\",\"content\":[{\"type\":\"tableCell\",\"content\":[{\"type\":\"paragraph\"}]}]}]}")
                 })
        {
            var resultado = Validar(json);
            Assert.Null(resultado.Erro);
            Assert.Null(resultado.Documento);
        }
        // Texto vazio (que o editor não produz) sai sem erro
        Igual(Doc(Paragrafo), Validar(Doc("{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"\"},{\"type\":\"text\",\"text\":\"Olá\"}]}")).Documento);
    }

    [Fact]
    public void ProfundidadeDemais_Recusada()
    {
        var json = Paragrafo;
        for (var i = 0; i < 13; i++)
            json = "{\"type\":\"bulletList\",\"content\":[{\"type\":\"listItem\",\"content\":[" + json + "]}]}";
        Assert.Contains("níveis demais", Validar(Doc(json)).Erro);
    }

    [Fact]
    public void Rotulo_DoTextoRico_ETextoCorrido()
    {
        var doc = JsonNode.Parse(Doc("{\"type\":\"heading\",\"attrs\":{\"level\":3},\"content\":[{\"type\":\"text\",\"text\":\"Um\"}]}," + Paragrafo));
        Assert.Equal("Um Olá", PeValores.TextoDoRico(doc));
    }
}
