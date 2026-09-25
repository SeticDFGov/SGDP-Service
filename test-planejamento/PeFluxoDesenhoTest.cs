using System.Xml.Linq;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O desenho automático (E6): para cada fluxo do guia e para definições feitas à mão, as caixas
/// não se sobrepõem, as colunas crescem ao longo das ligações, os paralelos ficam empilhados na
/// mesma coluna, os retornos passam por baixo, o texto cabe nas caixas, as ligações não
/// atravessam caixas e o SVG é um XML válido com os glifos da Lato (texto em contorno, que o
/// QuestPDF desenha). Com a variável PE_EXEMPLO_FLUXOS, os SVGs são gravados nessa pasta.
/// </summary>
public class PeFluxoDesenhoTest : PeFluxoTestBase
{
    public static IEnumerable<object[]> ChavesDoGuia => PeFluxoSeed.Ler().Fluxos.Select(f => new object[] { f.Chave });

    private PeFluxoDesenho DoGuia(string chave) => PeFluxoDesenho.Desenhar(DefinicaoDoModelo(chave), ModeloNoBanco(chave).Nome, NomesPadrao());

    [Theory]
    [MemberData(nameof(ChavesDoGuia))]
    public void FluxoDoGuia_CaixasSemSobreposicao_TextoDentro_LigacoesSemAtravessar(string chave)
    {
        var desenho = DoGuia(chave);

        // Caixas (e documentos dos artefatos) não se sobrepõem
        var areas = desenho.Caixas.Select(c => (Nome: c.Elemento.Id, Area: c.Area))
            .Concat(desenho.Artefatos.Select((a, i) => (Nome: $"doc{i}", Area: a.Documento)))
            .ToList();
        for (var i = 0; i < areas.Count; i++)
            for (var j = i + 1; j < areas.Count; j++)
                Assert.False(areas[i].Area.Sobrepoe(areas[j].Area), $"{chave}: {areas[i].Nome} sobre {areas[j].Nome}");

        // O texto de cada tarefa fica dentro da caixa dela
        foreach (var caixa in desenho.Caixas.Where(c => PeDominios.TipoElementoFluxo.EhAtividade(c.Elemento.Tipo)))
            foreach (var t in desenho.Textos.Where(t => t.Dono == caixa.Elemento.Id))
                Assert.True(caixa.Area.Contem(t.Area, 0.5), $"{chave}: \"{t.Conteudo}\" sai da caixa de {caixa.Elemento.Id}");

        // Nenhuma ligação atravessa uma caixa
        Assert.All(desenho.Linhas, l => Assert.True(l.Colisoes == 0, $"{chave}: a ligação {l.Ligacao.Id} bate em {string.Join("; ", l.Atingidos)}"));

        // Todo elemento dentro da raia dele
        foreach (var caixa in desenho.Caixas)
            Assert.True(desenho.Raias[caixa.Raia].Area.Contem(caixa.Area), $"{chave}: {caixa.Elemento.Id} fora da raia");
    }

    [Theory]
    [MemberData(nameof(ChavesDoGuia))]
    public void FluxoDoGuia_SvgValido_ComTextoEmContorno(string chave)
    {
        var desenho = DoGuia(chave);
        var xml = XDocument.Parse(desenho.Svg);
        XNamespace svg = "http://www.w3.org/2000/svg";
        XNamespace xlink = "http://www.w3.org/1999/xlink";

        Assert.Equal(svg + "svg", xml.Root!.Name);
        Assert.Equal("img", xml.Root.Attribute("role")!.Value);
        Assert.Equal(ModeloNoBanco(chave).Nome, xml.Root.Element(svg + "title")!.Value);
        Assert.Contains("1.", xml.Root.Element(svg + "desc")!.Value);
        // Texto em contorno: nenhum <text>, glifos definidos uma vez e usados com xlink:href
        Assert.Empty(xml.Descendants(svg + "text"));
        var glifos = xml.Descendants(svg + "path").Where(p => p.Attribute("id") != null).Select(p => p.Attribute("id")!.Value).ToList();
        Assert.NotEmpty(glifos);
        Assert.Equal(glifos.Count, glifos.Distinct().Count());
        var usos = xml.Descendants(svg + "use").Select(u => u.Attribute(xlink + "href")!.Value.TrimStart('#')).ToList();
        Assert.NotEmpty(usos);
        Assert.All(usos, u => Assert.Contains(u, glifos));

        // Sem travessão no desenho
        Assert.DoesNotContain((char)0x2014, desenho.Svg);
        Assert.DoesNotContain((char)0x2013, desenho.Svg);
    }

    [Fact]
    public void Colunas_CrescemAoLongoDasLigacoes_ERetornosPassamPorBaixo()
    {
        foreach (var chave in ChavesDoGuia.Select(c => (string)c[0]))
        {
            var desenho = DoGuia(chave);
            var caixa = desenho.Caixas.ToDictionary(c => c.Elemento.Id);
            foreach (var linha in desenho.Linhas)
            {
                var de = caixa[linha.Ligacao.De];
                var para = caixa[linha.Ligacao.Para];
                if (!linha.Retorno)
                {
                    // Avanço: o destino fica numa coluna à direita
                    Assert.True(para.Camada > de.Camada, $"{chave}: {linha.Ligacao.Id} não avança");
                    Assert.True(para.Area.X > de.Area.Direita, $"{chave}: {linha.Ligacao.Id} volta para a esquerda");
                    continue;
                }
                // Retorno: desce abaixo de tudo o que há na raia do canal e volta por baixo
                var indice = Math.Max(de.Raia, para.Raia);
                var raia = desenho.Raias[indice];
                var fundo = desenho.Caixas.Where(c => c.Raia == indice).Max(c => c.Area.Base);
                var maisBaixo = linha.Pontos.Max(p => p.Y);
                Assert.True(maisBaixo > fundo, $"{chave}: o retorno {linha.Ligacao.Id} não passa por baixo");
                Assert.True(maisBaixo < raia.Area.Base, $"{chave}: o retorno {linha.Ligacao.Id} sai da raia");
                Assert.True(para.Camada < de.Camada || para.Camada == de.Camada, $"{chave}: {linha.Ligacao.Id} não volta");
            }
        }
    }

    [Fact]
    public void Paralelos_NaMesmaColuna_EmpilhadosSemSobrepor()
    {
        var desenho = DoGuia("diagnostico");
        var paralelos = desenho.Caixas.Where(c => c.Elemento.Numero is "2.9" or "2.10" or "2.11").OrderBy(c => c.Area.Y).ToList();
        Assert.Equal(3, paralelos.Count);
        Assert.Single(paralelos.Select(p => Math.Round(p.Cx, 3)).Distinct());
        Assert.Equal(new[] { "2.9", "2.10", "2.11" }, paralelos.Select(p => p.Elemento.Numero));
        // O do meio segue a linha de quem abriu (o paralelo) e de quem junta
        var abre = desenho.Caixas.Where(c => c.Elemento.Tipo == PeDominios.TipoElementoFluxo.Paralelo).OrderBy(c => c.Cx).ToList();
        Assert.Equal(abre[0].Cy, paralelos[1].Cy, 3);
        Assert.Equal(abre[1].Cy, paralelos[1].Cy, 3);
        // A ligação vinda da avaliação intermediária fica embaixo do início, na mesma coluna
        var inicio = desenho.Caixas.Single(c => c.Elemento.Tipo == PeDominios.TipoElementoFluxo.Inicio);
        var entrada = desenho.Caixas.Single(c => c.Elemento.Tipo == PeDominios.TipoElementoFluxo.Ligacao);
        Assert.Equal(inicio.Cx, entrada.Cx, 3);
        Assert.True(entrada.Cy > inicio.Cy);
    }

    [Fact]
    public void Raias_EmFaixas_NaOrdem_ComONomeNaVertical()
    {
        var desenho = DoGuia("planejamento");
        Assert.Equal(3, desenho.Raias.Count);
        for (var i = 1; i < desenho.Raias.Count; i++)
            Assert.Equal(desenho.Raias[i - 1].Area.Base, desenho.Raias[i].Area.Y, 3);
        var nomes = desenho.Textos.Where(t => t.Dono.StartsWith("raia:")).ToList();
        Assert.All(nomes, t => Assert.True(t.Vertical));
        Assert.Equal("Autoridade Máxima", string.Join(" ", nomes.Where(t => t.Dono == "raia:r1").Select(t => t.Conteudo)));
        // A faixa do fluxo (o "pool" do guia), com duas raias ou mais
        Assert.Equal("Planejamento", string.Join(" ", desenho.Textos.Where(t => t.Dono == "pool").Select(t => t.Conteudo)));
        Assert.DoesNotContain(DoGuia("elaboracao").Textos, t => t.Dono == "pool");
        // Cada nome cabe na altura da própria raia
        foreach (var raia in desenho.Raias)
            Assert.All(nomes.Where(t => t.Dono == "raia:" + raia.Raia.Id), t => Assert.True(raia.Area.Contem(t.Area, 0.5)));
    }

    [Fact]
    public void Tarefa_NumeroAcimaDoNome_EmNegrito()
    {
        var desenho = DoGuia("preparacao");
        var caixa = desenho.Caixas.Single(c => c.Elemento.Numero == "1.1");
        var textos = desenho.Textos.Where(t => t.Dono == caixa.Elemento.Id).OrderBy(t => t.Y).ToList();
        Assert.Equal("1.1", textos[0].Conteudo);
        Assert.True(textos[0].Negrito);
        Assert.All(textos.Skip(1), t => Assert.False(t.Negrito));
        Assert.Equal("Definir a abrangência e o período do PDTIC", string.Join(" ", textos.Skip(1).Select(t => t.Conteudo)));
    }

    [Fact]
    public void NomeLongo_QuebraSemSairDaCaixa_ECortaComReticencias()
    {
        var definicao = Simples();
        definicao.Elementos.Single(e => e.Id == "a").Nome = string.Join(" ", Enumerable.Repeat("Levantar e consolidar as necessidades", 8));
        definicao.Elementos.Single(e => e.Id == "b").Nome = "Palavraenormesemespacoquenaocabenalinhadacaixadatarefa";
        var desenho = Desenhar(definicao);

        foreach (var id in new[] { "a", "b" })
        {
            var caixa = desenho.Caixas.Single(c => c.Elemento.Id == id);
            var textos = desenho.Textos.Where(t => t.Dono == id).ToList();
            Assert.All(textos, t => Assert.True(caixa.Area.Contem(t.Area, 0.5), $"\"{t.Conteudo}\" sai da caixa"));
        }
        var linhasA = desenho.Textos.Where(t => t.Dono == "a" && !t.Negrito).ToList();
        Assert.Equal(7, linhasA.Count);
        Assert.EndsWith("…", linhasA[^1].Conteudo);
        Assert.True(desenho.Textos.Count(t => t.Dono == "b" && !t.Negrito) > 1);
    }

    [Fact]
    public void Nomes_DoDicionario_OuComoEstao()
    {
        var nomes = new Dictionary<string, string?>(NomesPadrao()) { ["nomes.comite"] = "Subcomitê Gestor de TIC" };
        var desenho = PeFluxoDesenho.Desenhar(Simples(), "Teste", nomes);
        Assert.Equal("Subcomitê Gestor de TIC", string.Join(" ", desenho.Textos.Where(t => t.Dono == "raia:r1").Select(t => t.Conteudo)));
        Assert.Contains("Raias: Subcomitê Gestor de TIC, Equipe.", desenho.Descricao);
        // Marcador desconhecido fica escrito
        var definicao = Simples();
        definicao.Raias[1].Nome = "{nomes.outro}";
        Assert.Contains("Raias: Comitê de Governança Digital, {nomes.outro}.", Desenhar(definicao).Descricao);
    }

    [Fact]
    public void SemAFonte_UsaTexto_EODesenhoContinua()
    {
        var desenho = PeFluxoDesenho.Desenhar(Simples(), "Teste", NomesPadrao(), usarFonte: false);
        XNamespace svg = "http://www.w3.org/2000/svg";
        var xml = XDocument.Parse(desenho.Svg);
        Assert.Contains(xml.Descendants(svg + "text"), t => t.Value == "Primeira tarefa");
        Assert.Empty(xml.Descendants(svg + "use"));
    }

    [Fact]
    public void MesmaDefinicao_MesmoSvg()
    {
        Assert.Equal(DoGuia("planejamento").Svg, DoGuia("planejamento").Svg);
        // A definição dada não é alterada pelo desenho (os nomes são trocados numa cópia)
        var definicao = Simples();
        Desenhar(definicao);
        Assert.Equal("{nomes.comite}", definicao.Raias[0].Nome);
    }

    [Fact]
    public void Descricao_ListaNumeradaNaOrdemDoDesenho()
    {
        var desenho = DoGuia("planejamento");
        Assert.Equal("Raias: Autoridade Máxima, Comitê de Governança Digital, Equipe de Elaboração do PDTIC.", desenho.Descricao[0]);
        Assert.Equal("1. Início (Comitê de Governança Digital).", desenho.Descricao[1]);
        Assert.Contains("11. Decisão \"Minuta aprovada?\" (Comitê de Governança Digital): Sim, segue para 3.10 Publicar o PDTIC; Não, volta para 3.8 Consolidar a minuta do PDTIC.",
            desenho.Descricao);
        Assert.Equal("13. Fim (Autoridade Máxima).", desenho.Descricao[^1]);
    }

    [Fact]
    public void Exemplo_GravaOsSvgsDosFluxosDoGuia()
    {
        var pasta = PastaDeExemplo();
        foreach (var (fluxo, i) in PeFluxoSeed.Ler().Fluxos.Select((f, i) => (f, i)))
        {
            var desenho = DoGuia(fluxo.Chave);
            Assert.StartsWith("<svg", desenho.Svg);
            if (pasta != null)
                File.WriteAllText(Path.Combine(pasta, $"{(i + 1):00}-{fluxo.Chave}.svg"), desenho.Svg);
        }
    }
}
