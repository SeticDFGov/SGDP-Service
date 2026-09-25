using System.Text.Json;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2, a segunda rodada de correções, no PDF do documento: nenhuma palavra quebra no meio numa
/// coluna estreita (C06 parcial: toda coluna tem a largura mínima da maior palavra dela, do
/// cabeçalho e das células) e a legenda de uma tabela não fica sozinha no pé da página, com a
/// tabela curta inteira na mesma página (B-N1). O texto de cada página sai do próprio PDF
/// (<see cref="PeTextoDoPdf"/>).
/// </summary>
public class PeF2DocumentoPdfTest : PeDocumentoTestBase
{
    // ── Documento montado à mão ─────────────────────────────────────────────

    private static PeDocumentoResponse Documento(params PeDocBlocoResponse[] blocos) => new()
    {
        PdticId = 1,
        Versao = "1.1",
        OrgaoSigla = "SES",
        OrgaoNome = "Secretaria de Estado de Saúde",
        Titulo = "Plano Diretor de Tecnologia da Informação e Comunicação",
        Capitulos = new List<PeDocCapituloResponse>
        {
            new()
            {
                Id = 16, Chave = "orcamento", Numero = "16", Nivel = 1, Titulo = "Plano orçamentário", TituloModelo = "Plano orçamentário",
                Blocos = blocos.Select((b, i) => { b.Ordem = i + 1; return b; }).ToList()
            }
        }
    };

    private static byte[] Gerar(PeDocumentoResponse documento) =>
        PeDocumentoPdf.Gerar(new PeDocumentoPdf.Entrada
        {
            Documento = documento,
            Rodape = "SES · PDTIC versão 1.1 · minuta nº 5",
            GeradoEm = new DateTime(2026, 9, 25, 10, 0, 0)
        }).Pdf;

    /// <summary>Um texto de n parágrafos de uma linha: empurra o bloco seguinte para baixo, parágrafo a parágrafo.</summary>
    private static PeDocBlocoResponse Enchimento(int paragrafos) => new()
    {
        Id = 100,
        Tipo = PeDominios.TipoBloco.Texto,
        TextoBruto = JsonSerializer.SerializeToElement(new
        {
            type = "doc",
            content = Enumerable.Range(1, paragrafos)
                .Select(i => new { type = "paragraph", content = new[] { new { type = "text", text = $"Parágrafo {i} do texto que enche a página." } } })
                .ToArray()
        })
    };

    /// <summary>O formulário do achado B-N1: uma linha só.</summary>
    private static PeDocBlocoResponse ComparacaoComALoa() => new()
    {
        Id = 200,
        Tipo = PeDominios.TipoBloco.TabelaSecao,
        Tabela = new PeDocTabelaResponse
        {
            SecaoChave = "comparacao_loa",
            SecaoTitulo = "Comparação com a lei orçamentária",
            SecaoTipo = PeDominios.TipoSecao.Formulario,
            Colunas = new List<PeDocColunaResponse> { new() { Chave = "valor_loa", Rotulo = "Valor de TIC na LOA do órgão" } },
            Linhas = new List<PeDocLinhaResponse> { new() { Celulas = new Dictionary<string, string> { ["valor_loa"] = "R$ 10.000,00" } } }
        }
    };

    private static PeDocTabelaResponse Orcamento(int linhas) => new()
    {
        SecaoChave = "orcamento_acoes",
        SecaoTitulo = "Orçamento por ação e por ano",
        SecaoTipo = PeDominios.TipoSecao.Tabela,
        Colunas = new List<PeDocColunaResponse>
        {
            new() { Chave = "acao", Rotulo = "Ação" }, new() { Chave = "ano", Rotulo = "Ano" },
            new() { Chave = "investimento", Rotulo = "Investimento" }, new() { Chave = "custeio", Rotulo = "Custeio" }
        },
        Linhas = Enumerable.Range(1, linhas).Select(i => new PeDocLinhaResponse
        {
            Celulas = new Dictionary<string, string>
            {
                ["acao"] = $"A{i:00}", ["ano"] = "2027", ["investimento"] = $"R$ {i}.000,00", ["custeio"] = $"R$ {i}00,00"
            }
        }).ToList()
    };

    private static PeDocBlocoResponse Tabela(PeDocTabelaResponse tabela) => new() { Id = 300, Tipo = PeDominios.TipoBloco.TabelaSecao, Tabela = tabela };

    /// <summary>
    /// A tabela "Metas em andamento" do RA da reconferência (oito colunas com a do código, página
    /// em pé): a coluna "Observação" só com "-".
    /// </summary>
    private static PeDocTabelaResponse MetasEmAndamento() => new()
    {
        SecaoChave = "metas",
        SecaoTitulo = "Metas",
        SecaoTipo = PeDominios.TipoSecao.Tabela,
        Colunas = new List<PeDocColunaResponse>
        {
            new() { Chave = "descricao", Rotulo = "Meta" }, new() { Chave = "indicador", Rotulo = "Indicador" },
            new() { Chave = "valor", Rotulo = "Valor da meta" }, new() { Chave = "prazo", Rotulo = "Prazo" },
            new() { Chave = "valor_alcancado", Rotulo = "Valor alcançado" }, new() { Chave = "data_medicao", Rotulo = "Data da medição" },
            new() { Chave = "observacao", Rotulo = "Observação" }
        },
        Linhas = new List<PeDocLinhaResponse>
        {
            new()
            {
                Codigo = "M01",
                Celulas = new Dictionary<string, string>
                {
                    ["descricao"] = "Texto de teste para meta.", ["indicador"] = "Teste Indicador", ["valor"] = "Teste Valor da meta",
                    ["prazo"] = "24/09/2027", ["valor_alcancado"] = "50% dos serviços", ["data_medicao"] = "25/09/2026", ["observacao"] = "-"
                }
            }
        }
    };

    private static List<float> EmPontos(IReadOnlyList<PeDocumentoPdf.LarguraDaColuna> larguras, float disponivel)
    {
        var fixas = larguras.Where(l => l.Constante).Sum(l => l.Valor);
        var relativas = larguras.Where(l => !l.Constante).Sum(l => l.Valor);
        return larguras.Select(l => l.Constante ? l.Valor : (disponivel - fixas) * l.Valor / relativas).ToList();
    }

    private static double Largura(string palavra, bool negrito) => PeDocumentoPdf.LarguraDoTexto(palavra, 8.5f, negrito);

    // ── C06: nenhuma palavra quebra no meio ─────────────────────────────────

    [Fact]
    public void MetasEmAndamento_CadaColunaCabeAMaiorPalavra_InclusiveOCabecalhoDaColunaSoComTraco()
    {
        var tabela = MetasEmAndamento();
        var disponivel = PeDocumentoPdf.LarguraUtilEmPe - 44;
        var larguras = EmPontos(PeDocumentoPdf.Larguras(tabela, disponivel), disponivel);

        Assert.Equal(disponivel, larguras.Sum(), 1);
        for (var i = 0; i < tabela.Colunas.Count; i++)
        {
            var coluna = tabela.Colunas[i];
            var maior = coluna.Rotulo.Split(' ').Select(p => Largura(p, true))
                .Concat(tabela.Linhas.SelectMany(l => PeDocumentoPdf.SemQuebra(l.Celulas[coluna.Chave]).Split(' ')).Select(p => Largura(p, false)))
                .Max();
            // O texto da célula tem a largura da coluna menos o espaço interno (4 de cada lado)
            Assert.True(larguras[i] - 8 >= maior, $"{coluna.Rotulo}: {larguras[i] - 8:0.0} < {maior:0.0}");
        }
        // A coluna só com "-" tem a largura do cabeçalho: "Observação" inteira
        var observacao = tabela.Colunas.FindIndex(c => c.Chave == "observacao");
        Assert.True(PeDocumentoPdf.LarguraMinima(tabela, tabela.Colunas[observacao]) >= Largura("Observação", true) + 8);
    }

    [Fact]
    public void MetasEmAndamento_NoPdfEmPe_OCabecalhoEAsDatasSaemInteiros()
    {
        var grupos = new PeDocBlocoResponse
        {
            Id = 400,
            Tipo = PeDominios.TipoBloco.MetasPorResultado,
            Grupos = new List<PeDocGrupoResponse>
            {
                new() { Chave = "alcancadas", Titulo = "Metas alcançadas", Tabela = new PeDocTabelaResponse { Vazia = true } },
                new() { Chave = "em_andamento", Titulo = "Metas em andamento", Texto = "Metas ainda em execução, sem o resultado final.", Tabela = MetasEmAndamento() }
            }
        };

        var linhas = PeTextoDoPdf.Paginas(Gerar(Documento(grupos))).SelectMany(p => p).ToList();

        // Antes: "Observaç" numa linha e "ão" na seguinte
        Assert.Contains("Observação", linhas);
        Assert.DoesNotContain(linhas, l => l == "Observaç" || l == "ão");
        Assert.Contains("24/09/2027", linhas);
        Assert.Contains("25/09/2026", linhas);
        Assert.Contains("M01", linhas);
    }

    [Fact]
    public void ColunasDemais_AsMinimasNaoCabem_CadaColunaPelaParteDaSuaMinima()
    {
        var tabela = new PeDocTabelaResponse
        {
            SecaoTipo = PeDominios.TipoSecao.Tabela,
            Colunas = Enumerable.Range(1, 12).Select(i => new PeDocColunaResponse { Chave = $"c{i}", Rotulo = "Responsabilidades" }).ToList(),
            Linhas = new List<PeDocLinhaResponse> { new() { Celulas = Enumerable.Range(1, 12).ToDictionary(i => $"c{i}", i => "Sim") } }
        };
        var larguras = PeDocumentoPdf.Larguras(tabela, PeDocumentoPdf.LarguraUtilEmPe);

        // Doze vezes a palavra não cabem na página em pé: todas relativas, iguais (a mesma mínima)
        Assert.All(larguras, l => Assert.False(l.Constante));
        Assert.Single(larguras.Select(l => l.Valor).Distinct());
        Assert.True(PeDocumentoPdf.LarguraMinima(tabela, tabela.Colunas[0]) * 12 > PeDocumentoPdf.LarguraUtilEmPe);
    }

    [Fact]
    public async Task PdticCompleto_NenhumaPalavraQuebraNoMeio_NosCabecalhosENasCelulas()
    {
        var pdtic = await PdticCompletoAsync();
        var documento = await DocumentoAsync(pdtic.Id);
        var ctx = await Orgao();
        var versao = await Documentos.GerarPdfAsync(pdtic.Id, ctx);
        var pdf = (await Documentos.ArquivoDaVersaoAsync(pdtic.Id, versao.Numero, ctx)).Conteudo;

        // As palavras de todos os cabeçalhos de todas as tabelas do documento
        var tabelas = documento.Capitulos.SelectMany(c => c.Blocos)
            .SelectMany(b => b.Grupos?.Select(g => g.Tabela) ?? (b.Tabela != null ? new[] { b.Tabela } : Array.Empty<PeDocTabelaResponse>()))
            .Where(t => t.SecaoTipo == PeDominios.TipoSecao.Tabela && !t.Vazia)
            .ToList();
        Assert.True(tabelas.Count >= 10, $"tabelas: {tabelas.Count}");
        var palavras = tabelas.SelectMany(t => t.Colunas).SelectMany(c => c.Rotulo.Split(' ')).Append("Código").ToHashSet();
        // E as palavras das células que cabem na largura mínima mais larga (a maior, como um
        // endereço de internet, pode quebrar)
        palavras.UnionWith(tabelas.SelectMany(t => t.Linhas).SelectMany(l => l.Celulas.Values)
            .SelectMany(v => PeDocumentoPdf.SemQuebra(v).Split(' '))
            .Where(p => p.Length > 1 && Largura(p, false) + 9 <= 96));

        // Uma palavra quebrada sai com o começo no fim de uma linha e o resto no começo da seguinte
        foreach (var pagina in PeTextoDoPdf.Paginas(pdf))
            for (var i = 0; i + 1 < pagina.Count; i++)
            {
                var fim = pagina[i].Split(' ').Last();
                var comeco = pagina[i + 1].Split(' ').First();
                // Com o espaço no fim de uma ou no começo da outra, a quebra foi entre palavras
                if (fim.Length == 0 || comeco.Length == 0) continue;
                Assert.False(palavras.Contains(fim + comeco), $"\"{fim + comeco}\" quebrou: \"{pagina[i]}\" / \"{pagina[i + 1]}\"");
            }
    }

    // ── B-N1: a legenda com a tabela ────────────────────────────────────────

    /// <summary>
    /// Com n parágrafos antes, o bloco começa cada vez mais embaixo, até passar para a página
    /// seguinte: em toda posição, a legenda sai na página do primeiro dado (e a tabela curta,
    /// inteira nela).
    /// </summary>
    private static List<(int Paragrafos, int Legenda, int[] Dados, int UltimoParagrafo)> Varredura(
        Func<PeDocBlocoResponse> bloco, string legenda, params string[] dados)
    {
        var saida = new List<(int, int, int[], int)>();
        for (var n = 24; n <= 46; n++)
        {
            var paginas = PeTextoDoPdf.Paginas(Gerar(Documento(Enchimento(n), bloco())));
            saida.Add((n, PeTextoDoPdf.PaginaCom(paginas, legenda), dados.Select(d => PeTextoDoPdf.PaginaCom(paginas, d)).ToArray(),
                PeTextoDoPdf.PaginaCom(paginas, $"Parágrafo {n} do texto")));
        }
        return saida;
    }

    [Fact]
    public void FormularioDeUmaLinha_ALegendaSaiNaPaginaDoValor_EmQualquerPosicao()
    {
        var varredura = Varredura(ComparacaoComALoa, "Comparação com a lei orçamentária", "Valor de TIC na LOA do órgão", "R$ 10.000,00");

        foreach (var (n, legenda, dados, _) in varredura)
            Assert.True(legenda > 0 && dados.All(d => d == legenda), $"{n} parágrafos: legenda na página {legenda}, dados nas páginas {string.Join(", ", dados)}");
        // A varredura passou pela virada da página: o bloco inteiro foi para a página seguinte,
        // com o último parágrafo ainda na anterior (antes, a legenda ficava sozinha no pé dela)
        Assert.Contains(varredura, v => v.UltimoParagrafo == 1 && v.Legenda == 2);
        Assert.Contains(varredura, v => v.Legenda == 1);
    }

    [Fact]
    public void TabelaCurta_InteiraNaPaginaDaLegenda_EmQualquerPosicao()
    {
        var varredura = Varredura(() => Tabela(Orcamento(4)), "Orçamento por ação e por ano", "A01", "A02", "A03", "A04");

        foreach (var (n, legenda, dados, _) in varredura)
            Assert.True(legenda > 0 && dados.All(d => d == legenda), $"{n} parágrafos: legenda na página {legenda}, linhas nas páginas {string.Join(", ", dados)}");
        Assert.Contains(varredura, v => v.UltimoParagrafo == 1 && v.Legenda == 2);
    }

    [Fact]
    public void TabelaLonga_ALegendaSaiComOCabecalhoEAPrimeiraLinha_EOCabecalhoSeRepeteSemEla()
    {
        var tabela = Orcamento(45);
        Assert.False(PeDocumentoPdf.TabelaCurta(tabela, PeDocumentoPdf.LarguraUtilEmPe, tabela.SecaoTitulo, null));

        var varredura = Varredura(() => Tabela(Orcamento(45)), "Orçamento por ação e por ano", "A01");
        foreach (var (n, legenda, dados, _) in varredura)
            Assert.True(legenda > 0 && dados[0] == legenda, $"{n} parágrafos: legenda na página {legenda}, primeira linha na {dados[0]}");
        // A tabela longa começa na página do texto quando cabem a legenda, o cabeçalho e uma linha
        Assert.Contains(varredura, v => v.UltimoParagrafo == 1 && v.Legenda == 1);

        // Na página seguinte, o cabeçalho das colunas se repete, e a legenda não
        var paginas = PeTextoDoPdf.Paginas(Gerar(Documento(Enchimento(30), Tabela(Orcamento(45)))));
        var continuacao = paginas.Skip(PeTextoDoPdf.PaginaCom(paginas, "A01")).First(p => p.Any(l => l.StartsWith('A') && l.Length == 3));
        Assert.Contains("Investimento", continuacao);
        Assert.DoesNotContain("Orçamento por ação e por ano", continuacao);
    }

    [Fact]
    public void ListaDoTema_ALegendaSaiComAsAcoes_EmQualquerPosicao()
    {
        PeDocBlocoResponse Lista() => new()
        {
            Id = 500,
            Tipo = PeDominios.TipoBloco.ListaTema,
            Lista = new PeDocListaResponse
            {
                Tema = "Governança de dados",
                Itens = new List<PeDocListaItemResponse>
                {
                    new() { Codigo = "A01", Texto = "Publicar o catálogo de dados da secretaria.", Situacao = "Não iniciada" },
                    new() { Codigo = "A02", Texto = "Criar o comitê de dados.", Situacao = "Em andamento" }
                }
            }
        };

        var varredura = Varredura(Lista, "Ações do tema Governança de dados", "A01", "A02");
        foreach (var (n, legenda, dados, _) in varredura)
            Assert.True(legenda > 0 && dados.All(d => d == legenda), $"{n} parágrafos: legenda na página {legenda}, ações nas páginas {string.Join(", ", dados)}");
        Assert.Contains(varredura, v => v.UltimoParagrafo == 1 && v.Legenda == 2);
    }

    [Fact]
    public void Curta_PelaAlturaEstimada_UmTercoDaPagina()
    {
        var formulario = ComparacaoComALoa().Tabela!;
        Assert.True(PeDocumentoPdf.TabelaCurta(formulario, PeDocumentoPdf.LarguraUtilEmPe, formulario.SecaoTitulo, null));
        Assert.True(PeDocumentoPdf.TabelaCurta(Orcamento(4), PeDocumentoPdf.LarguraUtilEmPe, "Orçamento", null));
        Assert.False(PeDocumentoPdf.TabelaCurta(Orcamento(20), PeDocumentoPdf.LarguraUtilEmPe, "Orçamento", null));

        // A altura cresce com as linhas e com o texto que quebra
        var quatro = PeDocumentoPdf.AlturaEstimada(Orcamento(4), PeDocumentoPdf.LarguraUtilEmPe, "Orçamento", null);
        var oito = PeDocumentoPdf.AlturaEstimada(Orcamento(8), PeDocumentoPdf.LarguraUtilEmPe, "Orçamento", null);
        Assert.True(oito > quatro);
        Assert.Equal(1, PeDocumentoPdf.LinhasDoTexto("Orçamento", 200, 8.5f, negrito: false));
        Assert.True(PeDocumentoPdf.LinhasDoTexto(string.Join(' ', Enumerable.Repeat("palavra", 40)), 100, 8.5f, negrito: false) > 5);

        // O texto formatado com imagem (o organograma do diagnóstico) não é curto: a altura da
        // imagem não sai do texto, e a conta a põe no máximo
        var comImagem = new PeDocTabelaResponse
        {
            SecaoTitulo = "Diagnóstico",
            SecaoTipo = PeDominios.TipoSecao.Formulario,
            Colunas = new List<PeDocColunaResponse> { new() { Chave = "diagnostico", Rotulo = "Diagnóstico" } },
            Linhas = new List<PeDocLinhaResponse>
            {
                new()
                {
                    Celulas = new Dictionary<string, string>(),
                    Ricos = new Dictionary<string, JsonElement>
                    {
                        ["diagnostico"] = JsonSerializer.SerializeToElement(new
                        {
                            type = "doc",
                            content = new object[]
                            {
                                new { type = "paragraph", content = new[] { new { type = "text", text = "O organograma da unidade:" } } },
                                new { type = "image", attrs = new { src = "api/planejamento/arquivos/1" } }
                            }
                        })
                    }
                }
            }
        };
        Assert.False(PeDocumentoPdf.TabelaCurta(comImagem, PeDocumentoPdf.LarguraUtilEmPe, comImagem.SecaoTitulo, null));
    }
}
