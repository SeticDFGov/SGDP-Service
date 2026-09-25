using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1, a rodada de correções da revisão final, no acompanhamento e nos PDFs: os passos do
/// monitoramento sem ciclo possível não aguardam para sempre (C37); o resumo da avaliação
/// intermediária diz o que ela tem (I04 e C23); quem fechou o ciclo pelo nome (C19); as colunas
/// de data e de código com a largura mínima (C06); a tabela vazia não vira a página (C26); a capa
/// do RA e do RR como a da prévia (C25 e B09); e os marcadores do ciclo só no RA (B07).
/// </summary>
public class PeF1AcompanhamentoTest : PeAcompanhamentoTestBase
{
    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    // ── C37: sem ciclo possível, o monitoramento não aguarda ────────────────

    [Fact]
    public async Task VigenciaTerminadaAntesDoAcompanhamento_OMonitoramentoNaoSeAplica_ComOMotivo()
    {
        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "PDTIC SES 2021.pdf", PdfDeUmaPagina());
        var pdtic = await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            Versao = "1.0",
            VigenciaInicio = "2021-01-01",
            VigenciaFim = "2023-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic",
            AprovacaoData = "2021-03-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "2/2021",
            PublicacaoData = "2021-03-20",
            PublicacaoEndereco = Endereco
        }, await Orgao());

        Assert.Empty(await CiclosAsync(pdtic.Id, PeDominios.TipoCiclo.Monitoramento));
        var passo = await PassoDaSituacaoAsync(pdtic.Id, "monitoramento.ciclo-monitoramento");
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, passo.Situacao);
        Assert.Equal(PePdticService.MotivoVigenciaTerminada(new DateOnly(2023, 12, 31)), passo.Motivo);
        Assert.StartsWith("A vigência do PDTIC terminou em 31/12/2023", passo.Motivo);
        Assert.Null(passo.NaoSeAplica);
        // Não é o próximo passo recomendado
        Assert.NotEqual(passo.Numero, (await SituacaoAsync(pdtic.Id)).ProximoPasso);
    }

    [Fact]
    public async Task Encerrado_SemCiclo_OMonitoramentoNaoSeAplica()
    {
        var pdtic = await PublicadoAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Encerrado);

        var passo = await PassoDaSituacaoAsync(pdtic.Id, "monitoramento.ciclo-monitoramento");
        Assert.Equal((PeDominios.SituacaoPasso.NaoSeAplica, "O PDTIC foi encerrado sem ciclo de monitoramento"), (passo.Situacao, passo.Motivo));
    }

    [Fact]
    public async Task Publicado_SemVigenciaTerminada_OMonitoramentoContinuaPeloCiclo()
    {
        var id = await AcompanhadoNoBasicoAsync();
        var passo = await PassoDaSituacaoAsync(id, "monitoramento.ciclo-monitoramento");
        Assert.NotEqual(PeDominios.SituacaoPasso.NaoSeAplica, passo.Situacao);
    }

    // ── I04, C23 e C19: o resumo da avaliação e os nomes ────────────────────

    [Fact]
    public async Task ResumoDaAvaliacao_ResultadosAnaliseEDecisao_EQuemFechouPeloNome()
    {
        var id = await AcompanhadoAsync();
        var avaliacao = await AbrirAvaliacaoAsync(id, "Avaliação da frente C");
        await IncluirNoPdticAsync(id, "resultados_intermediarios", new { valor_alcancado = "40%", data = "2026-09-01", situacao = "em_andamento" },
            new { meta = new[] { IdDe(id, "M01") } }, cicloId: avaliacao.Id);
        await IncluirNoPdticAsync(id, "analise_intermediaria", new
        {
            parecer_execucao = "A execução está dentro do esperado.", metas_nao_atingidas = "A M02 depende do contrato de backup.",
            ajustes_propostos = "Rever o prazo da M02.", parecer_riscos = "Os riscos estão sob controle."
        }, cicloId: avaliacao.Id);

        var resumo = (await CiclosAsync(id, PeDominios.TipoCiclo.Avaliacao)).Single().Resumo;
        Assert.Equal((1, true, (string?)null), (resumo.ResultadosMetas, resumo.AnaliseRegistrada, resumo.DecisaoComite));

        await IncluirNoPdticAsync(id, "avaliacao_comite", new { decisao = "seguir", data = "2026-09-10" }, cicloId: avaliacao.Id);
        resumo = (await CiclosAsync(id, PeDominios.TipoCiclo.Avaliacao)).Single().Resumo;
        Assert.Equal(("seguir", "Seguir com o PDTIC"), (resumo.DecisaoComite, resumo.DecisaoComiteRotulo));

        // No ciclo de monitoramento, os campos da avaliação ficam zerados
        var trimestre = await CicloAsync(id, Trimestre1);
        Assert.Equal((0, false, (string?)null), (trimestre.Resumo.ResultadosMetas, trimestre.Resumo.AnaliseRegistrada, trimestre.Resumo.DecisaoComite));

        // Quem fechou, pelo nome
        var fechada = await FecharAsync(avaliacao.Id);
        Assert.Equal(("otavio@saude.df.gov.br", "Otávio da Saúde"), (fechada.FechadoPor, fechada.FechadoPorNome));
    }

    // ── C06: a largura das colunas de data e de código ─────────────────────

    private static PeDocTabelaResponse TabelaDasMetas(int linhas)
    {
        var colunas = new[]
        {
            ("descricao", "Meta"), ("indicador", "Indicador"), ("valor", "Valor"), ("prazo", "Prazo"),
            ("situacao", "Situação"), ("resultado", "Resultado alcançado"), ("medido", "Data da medição"), ("observacao", "Observação")
        };
        var tabela = new PeDocTabelaResponse
        {
            SecaoChave = "metas",
            SecaoTitulo = "Metas em andamento",
            SecaoTipo = PeDominios.TipoSecao.Tabela,
            Colunas = colunas.Select(c => new PeDocColunaResponse { Chave = c.Item1, Rotulo = c.Item2 }).ToList()
        };
        for (var i = 1; i <= linhas; i++)
            tabela.Linhas.Add(new PeDocLinhaResponse
            {
                Codigo = $"M{i:00}",
                Celulas = new Dictionary<string, string>
                {
                    ["descricao"] = "Implantar o novo sistema de regulação em todas as unidades da rede, com a integração ao prontuário.",
                    ["indicador"] = "Percentual de unidades com o sistema de regulação implantado e em uso pelos servidores.",
                    ["valor"] = "100%",
                    ["prazo"] = "24/09/2027",
                    ["situacao"] = "Em andamento",
                    ["resultado"] = "A implantação avançou nas unidades da região central e está atrasada nas demais regiões.",
                    ["medido"] = "25/09/2026",
                    ["observacao"] = "O contrato de suporte precisa ser renovado antes da próxima etapa da implantação."
                }
            });
        return tabela;
    }

    [Fact]
    public void Larguras_DataECodigo_NaoQuebramNoMeio()
    {
        var tabela = TabelaDasMetas(3);
        var larguras = PeDocumentoPdf.Larguras(tabela, PeDocumentoPdf.LarguraUtilEmPe - 44);
        var indice = tabela.Colunas.FindIndex(c => c.Chave == "prazo");
        var prazo = PeDocumentoPdf.LarguraMinima(tabela, tabela.Colunas[indice]);

        // A data inteira cabe: a coluna tem pelo menos a largura do texto dela (mais a sobra da célula)
        var fonte = PeFluxoFonte.Regular;
        var texto = fonte?.Largura("24/09/2027", 8.5) ?? PeFluxoFonte.LarguraEstimada("24/09/2027", 8.5, false);
        Assert.True(prazo >= texto + 8, $"{prazo} < {texto}");
        Assert.True(larguras[indice].Constante);
        Assert.Equal(prazo, larguras[indice].Valor);
        Assert.True(larguras[tabela.Colunas.FindIndex(c => c.Chave == "medido")].Constante);
        // O texto longo segue pelo peso
        Assert.False(larguras[tabela.Colunas.FindIndex(c => c.Chave == "descricao")].Constante);
        // Desde a F2 (C06 parcial), a coluna de texto com espaço também tem largura mínima: a da
        // maior palavra dela ("andamento"; no cabeçalho, "Situação" em negrito)
        var situacao = PeDocumentoPdf.LarguraMinima(tabela, tabela.Colunas.Single(c => c.Chave == "situacao"));
        Assert.True(situacao >= PeDocumentoPdf.LarguraDoTexto("andamento", 8.5f, negrito: false) + 8, $"{situacao}");
        Assert.True(situacao >= PeDocumentoPdf.LarguraDoTexto("Situação", 8.5f, negrito: true) + 8, $"{situacao}");

        // Com espaço de sobra (página deitada, poucas colunas), vale só o peso
        var estreita = new PeDocTabelaResponse
        {
            SecaoTipo = PeDominios.TipoSecao.Tabela,
            Colunas = new List<PeDocColunaResponse> { new() { Chave = "prazo", Rotulo = "Prazo" }, new() { Chave = "nome", Rotulo = "Nome" } },
            Linhas = new List<PeDocLinhaResponse> { new() { Celulas = new Dictionary<string, string> { ["prazo"] = "24/09/2027", ["nome"] = "Ana" } } }
        };
        Assert.All(PeDocumentoPdf.Larguras(estreita, PeDocumentoPdf.LarguraUtilDeitada), l => Assert.False(l.Constante));
    }

    // ── C26: a tabela vazia não vira a página ──────────────────────────────

    [Fact]
    public void BlocoDeitado_SoComDado()
    {
        var vazio = new PeDocBlocoResponse
        {
            Tipo = PeDominios.TipoBloco.MetasPorResultado,
            PaginaDeitada = true,
            Grupos = new List<PeDocGrupoResponse>
            {
                new() { Chave = "alcancadas", Titulo = "Metas alcançadas", Tabela = new PeDocTabelaResponse { Vazia = true } },
                new() { Chave = "canceladas", Titulo = "Metas canceladas", Tabela = new PeDocTabelaResponse { Vazia = true } }
            }
        };
        Assert.False(PeDocumentoPdf.Deitado(vazio));

        vazio.Grupos[1].Tabela = TabelaDasMetas(1);
        Assert.True(PeDocumentoPdf.Deitado(vazio));

        var tabelaVazia = new PeDocBlocoResponse
        {
            Tipo = PeDominios.TipoBloco.TabelaSecao, PaginaDeitada = true, Tabela = new PeDocTabelaResponse { Vazia = true }
        };
        Assert.False(PeDocumentoPdf.Deitado(tabelaVazia));
        tabelaVazia.PaginaDeitada = false;
        Assert.False(PeDocumentoPdf.Deitado(tabelaVazia));
    }

    // ── C25, B09 e B07: a capa e os marcadores de cada documento ───────────

    [Fact]
    public async Task Capa_ASiglaDoDocumento_AMarcaDoGdf_EOPdfDoRaSai()
    {
        Assert.Equal(("PDTIC", "RA", "RR"), (PeDocumentoPdf.SiglaDoDocumento("pdtic"), PeDocumentoPdf.SiglaDoDocumento("ra"), PeDocumentoPdf.SiglaDoDocumento("rr")));
        using (var marca = typeof(PeDocumentoPdf).Assembly.GetManifestResourceStream("Planejamento.marca-gdf.png"))
            Assert.NotNull(marca);

        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var ra = await Documentos.ObterAsync(PeDocAlvo.Ra(id, t1.Id), await Orgao());
        var ciclo = Context.PeCiclos.AsNoTracking().Single(c => c.Id == t1.Id);
        Assert.Equal("Ciclo 2026 · 1º trimestre (01/01/2026 a 31/03/2026)", PeDocumentoPdf.LinhaDoCiclo(ra, ciclo));
        // Fora do RA, a capa não tem a linha do ciclo
        var rr = await Documentos.ObterAsync(PeDocAlvo.Rr(id), await Orgao());
        Assert.Null(PeDocumentoPdf.LinhaDoCiclo(rr, null));

        var versao = await Documentos.GerarPdfAsync(PeDocAlvo.Ra(id, t1.Id), await Orgao());
        Assert.True(versao.Paginas > 1);
    }

    [Fact]
    public async Task MarcadoresDoCiclo_SoNoRa()
    {
        var modeloDoPdtic = await ModeloDoc.ObterAsync(PeDominios.TipoDocumento.Pdtic);
        var modeloDoRr = await ModeloDoc.ObterAsync(PeDominios.TipoDocumento.Rr);
        var modeloDoRa = await ModeloDoc.ObterAsync(PeDominios.TipoDocumento.Ra);
        Assert.DoesNotContain(modeloDoPdtic.Marcadores, m => m.Chave.StartsWith("ciclo."));
        Assert.DoesNotContain(modeloDoRr.Marcadores, m => m.Chave.StartsWith("ciclo."));
        Assert.Equal(PeDocMarcadores.DoCiclo, modeloDoRa.Marcadores.Where(m => m.Chave.StartsWith("ciclo.")).Select(m => m.Chave));

        // No texto do PDTIC, "{ciclo.rotulo}" é texto comum: não é marcador sem valor e não some
        var pdtic = await AbrirSesAsync();
        await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, Json(Doc(Paragrafo("O ciclo {ciclo.rotulo} do {orgao.sigla}."))), await Orgao());
        var bloco = Texto(await DocumentoAsync(pdtic.Id), "introducao");
        Assert.DoesNotContain("ciclo.rotulo", bloco.MarcadoresSemValor);
        Assert.Contains("O ciclo {ciclo.rotulo} do SES.", TextoDe(bloco.TextoResolvido));
    }
}
