using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Carregador, versão 3 (E5): o modelo do documento do PDTIC semeado com a estrutura da seção
/// 9 do plano (capa, folha de rosto, histórico, sumário, apresentação, capítulos 1 a 21 e
/// anexos), os nove conteúdos do art. 12, § 2º, travados, os textos padrão em JSON do TipTap
/// com marcadores e os blocos de dados certos; o logotipo no dicionário de nomes; idempotente e
/// sem sobrescrever o que o administrador mudou.
/// </summary>
public class PeDocSeedTest : PeDocumentoTestBase
{
    private static readonly string[] Topo =
    {
        "capa", "folha_rosto", "historico_versoes", "sumario", "apresentacao", "introducao", "termos", "metodologia",
        "documentos_referencia", "principios", "diagnostico", "ativos", "alinhamento", "necessidades", "contratacoes",
        "seguranca", "transformacao_digital", "metas_indicadores", "sistemas_ia", "governanca_dados", "pessoas", "orcamento",
        "riscos", "revisao_acompanhamento", "fatores_criticos", "conclusao", "anexos"
    };

    // Os capítulos do modelo do PDTIC (desde a E7, rodada B, o RA e o RR têm os seus)
    private List<PeDocCapitulo> Capitulos()
    {
        var modelo = Context.PeDocModelos.AsNoTracking().Single(m => m.Tipo == PeDominios.TipoDocumento.Pdtic).Id;
        return Context.PeDocCapitulos.AsNoTracking().Where(c => c.ModeloId == modelo).ToList();
    }

    private List<PeDocBloco> BlocosDe(string capitulo)
    {
        var id = CapituloDoModelo(capitulo).Id;
        return Context.PeDocBlocos.AsNoTracking().Where(b => b.CapituloId == id).OrderBy(b => b.Ordem).ToList();
    }

    [Fact]
    public void Modelo_UmPorTipo_ComOsCapitulosDaSecao9_NaOrdem()
    {
        // Um modelo ativo por tipo: o PDTIC (E5) e, desde a versão 6 (E7, rodada B), o RA e o RR
        var modelos = Context.PeDocModelos.AsNoTracking().ToList();
        Assert.Equal(new[] { "pdtic", "ra", "rr" }, modelos.Select(m => m.Tipo).OrderBy(t => t));
        Assert.All(modelos, m => Assert.True(m.Ativo));
        var modelo = modelos.Single(m => m.Tipo == "pdtic");

        var capitulos = Capitulos();
        Assert.All(capitulos, c =>
        {
            Assert.True(c.Sistema);
            Assert.Equal(modelo.Id, c.ModeloId);
        });
        Assert.Equal(Topo, capitulos.Where(c => c.PaiId == null).OrderBy(c => c.Ordem).Select(c => c.Chave));

        string[] Filhos(string pai) => capitulos.Where(c => c.PaiId == CapituloDoModelo(pai).Id).OrderBy(c => c.Ordem).Select(c => c.Chave).ToArray();
        Assert.Equal(new[] { "diagnostico_organizacao", "diagnostico_pdtic_anterior", "diagnostico_referencial", "diagnostico_swot", "diagnostico_capacidade" },
            Filhos("diagnostico"));
        Assert.Equal(new[] { "necessidades_levantamento", "necessidades_criterios", "necessidades_priorizadas" }, Filhos("necessidades"));
        Assert.Equal(new[] { "anexo_nao_priorizadas", "anexo_plano_trabalho", "anexo_outros" }, Filhos("anexos"));

        // Sem número: as páginas especiais, a apresentação e os anexos
        Assert.Equal(new[] { "capa", "folha_rosto", "historico_versoes", "sumario", "apresentacao", "anexos", "anexo_nao_priorizadas", "anexo_plano_trabalho", "anexo_outros" },
            capitulos.Where(c => !c.Numerado).OrderBy(c => c.Id).Select(c => c.Chave));
    }

    [Fact]
    public void NoveConteudos_Travados_Obrigatorios_ComOInciso_EOPassoTravado()
    {
        var travados = Capitulos().Where(c => c.Travado).OrderBy(c => c.Ordem).ToList();

        Assert.Equal(new[] { "diagnostico", "ativos", "necessidades", "contratacoes", "seguranca", "transformacao_digital", "metas_indicadores", "sistemas_ia", "governanca_dados" },
            travados.Select(c => c.Chave));
        Assert.Equal(new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" }, travados.Select(c => c.IncisoDecreto));
        Assert.All(travados, c =>
        {
            Assert.True(c.Obrigatorio);
            Assert.Null(c.PaiId);
            // O passo de cada um é travado: o capítulo aparece em todos os níveis
            Assert.True(Passo(c.PassoChave!).Travado);
        });
    }

    [Fact]
    public void Blocos_DeDadosCertos_EmCadaCapitulo()
    {
        // Desde a versão 5 (E7), os fluxos das etapas vêm depois do fluxo geral de cada capítulo
        Assert.Equal(new[] { "texto", "tabela_secao", "fluxo", "fluxo", "fluxo", "fluxo" }, BlocosDe("metodologia").Select(b => b.Tipo));
        Assert.Equal(new[] { "elaboracao", "preparacao", "diagnostico", "planejamento" },
            BlocosDe("metodologia").Where(b => b.Tipo == "fluxo").Select(b => PeDocConfig.Fluxo(PeDocConfig.Ler(b.Config))));
        Assert.Equal(new[] { "acompanhamento", "planejamento_acompanhamento", "monitoramento", "avaliacao_intermediaria", "avaliacao_final" },
            BlocosDe("revisao_acompanhamento").Where(b => b.Tipo == "fluxo").Select(b => PeDocConfig.Fluxo(PeDocConfig.Ler(b.Config))));
        Assert.Equal("matriz_swot", Assert.Single(BlocosDe("diagnostico_swot"), b => b.Tipo != "texto").Tipo);
        Assert.Equal("ativos", PeDocConfig.Secao(PeDocConfig.Ler(BlocoDoModelo("ativos", "tabela_secao").Config)));
        Assert.True(PeDocConfig.PaginaDeitada(PeDocConfig.Ler(BlocoDoModelo("ativos", "tabela_secao").Config)));

        // Capítulos 11, 12 e 15: as ações de cada tema do decreto
        Assert.Equal("seguranca", PeDocConfig.Tema(PeDocConfig.Ler(BlocoDoModelo("seguranca", "lista_tema").Config)));
        Assert.Equal("transformacao_digital", PeDocConfig.Tema(PeDocConfig.Ler(BlocoDoModelo("transformacao_digital", "lista_tema").Config)));
        Assert.Equal("governanca_dados", PeDocConfig.Tema(PeDocConfig.Ler(BlocoDoModelo("governanca_dados", "lista_tema").Config)));

        // Capítulo 9: as priorizadas (tira as não priorizadas); no anexo, só as não priorizadas
        var priorizadas = PeDocConfig.Ler(BlocoDoModelo("necessidades_priorizadas", "tabela_secao").Config);
        Assert.Equal("necessidades", PeDocConfig.Secao(priorizadas));
        Assert.True(PeDocConfig.Filtro(priorizadas)!.Excluir);
        Assert.Equal("priorizada", PeDocConfig.Filtro(priorizadas)!.Campo);
        Assert.False(PeDocConfig.Filtro(priorizadas)!.Valor!.GetValue<bool>());
        var anexo = PeDocConfig.Ler(BlocoDoModelo("anexo_nao_priorizadas", "tabela_secao").Config);
        Assert.False(PeDocConfig.Filtro(anexo)!.Excluir);
        Assert.False(PeDocConfig.Filtro(anexo)!.Valor!.GetValue<bool>());

        // Capítulo 14: o inventário do PGIA e as aquisições planejadas
        Assert.Equal(new[] { PeDocConfig.SecaoPgia, "aquisicoes_ia" },
            BlocosDe("sistemas_ia").Where(b => b.Tipo == "tabela_secao").Select(b => PeDocConfig.Secao(PeDocConfig.Ler(b.Config))));

        // As páginas especiais sem dados: histórico e sumário não têm bloco
        Assert.Empty(BlocosDe("historico_versoes"));
        Assert.Empty(BlocosDe("sumario"));
    }

    [Fact]
    public void TextosPadrao_TipTapValido_Curtos_ComMarcadores_ESemTravessao()
    {
        var textos = Context.PeDocBlocos.AsNoTracking().Where(b => b.Tipo == "texto").ToList()
            .Select(b => PeDocConfig.TextoDoBloco(PeDocConfig.Ler(b.Config)))
            .ToList();

        Assert.True(textos.Count >= 25);
        Assert.All(textos, texto =>
        {
            Assert.NotNull(texto);
            var validado = PeTextoRico.Validar(JsonSerializer.SerializeToElement(texto));
            Assert.Null(validado.Erro);
            Assert.Equal(PeDocMarcadores.Canonico(texto), PeDocMarcadores.Canonico(validado.Documento));
            // Curtos: fora a tabela de siglas, nenhum texto padrão passa de 700 letras
            if (!PeDocMarcadores.Canonico(texto).Contains("\"table\"")) Assert.True(PeValores.TextoDoRico(texto).Length <= 700);
        });
        var marcadores = PeDocMarcadores.Fixos.ToDictionary(m => m.Chave, _ => (string?)"x");
        Assert.Contains(textos, t => PeDocMarcadores.Encontrados(t, marcadores).Contains("nomes.comite"));
        Assert.Contains(textos, t => PeDocMarcadores.Encontrados(t, marcadores).Contains("vigencia.inicio"));

        using var stream = typeof(PeDocSeed).Assembly.GetManifestResourceStream(PeDocSeed.Recurso);
        var json = new StreamReader(stream!).ReadToEnd();
        Assert.DoesNotContain((char)0x2014, json);
        Assert.DoesNotContain((char)0x2013, json);
    }

    [Fact]
    public void Logotipo_NoDicionarioDeNomes_ImagemOpcional_ForaDaTabelaEDaPlanilha()
    {
        var logotipo = Campo("nomes", "logotipo");

        Assert.Equal("arquivo", logotipo.Tipo);
        using var config = JsonDocument.Parse(logotipo.Config);
        Assert.Equal(new[] { "png", "jpg" }, config.RootElement.GetProperty("tipos").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(5, config.RootElement.GetProperty("maxMb").GetInt32());
        Assert.Equal("p p p", SituacoesDoCampo("nomes", "logotipo"));
        Assert.False(logotipo.NoDocumento);
        Assert.False(logotipo.NaPlanilha);
    }

    [Fact]
    public async Task VersaoNova_NaoDuplica_ENaoMexeNoQueOAdministradorMudou()
    {
        var admin = await Admin();
        var introducao = CapituloDoModelo("introducao");
        await ModeloDoc.AtualizarCapituloAsync(introducao.Id, new api.Planejamento.PeDocCapituloAtualizarDTO
        {
            Titulo = "Por que este plano existe",
            Informados = new HashSet<string> { "Titulo" }
        }, admin);
        var capitulos = Context.PeDocCapitulos.Count();
        var blocos = Context.PeDocBlocos.Count();

        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed);

        Assert.True(resultado.Executou);
        Assert.Equal(0, resultado.Documentos + resultado.Capitulos + resultado.Blocos);
        Assert.Equal(capitulos, Context.PeDocCapitulos.Count());
        Assert.Equal(blocos, Context.PeDocBlocos.Count());
        Assert.Equal("Por que este plano existe", CapituloDoModelo("introducao").Titulo);
    }

    [Fact]
    public async Task VersaoNova_AcrescentaOCapituloQueFalta_NoFim_ComOsBlocos()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        var documentos = PeDocSeed.Ler();
        documentos.Modelos[0].Capitulos.Add(new PeSeedDocCapitulo
        {
            Chave = "glossario_extra",
            Titulo = "Glossário da saúde digital",
            Blocos = { new PeSeedDocBloco { Tipo = "texto", Texto = new List<string> { "Termos da saúde digital." } } }
        });

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed, documentos);

        Assert.Equal((0, 1, 1), (resultado.Documentos, resultado.Capitulos, resultado.Blocos));
        var novo = CapituloDoModelo("glossario_extra");
        Assert.Equal(Context.PeDocCapitulos.Where(c => c.PaiId == null).Max(c => c.Ordem), novo.Ordem);
        Assert.True(novo.Sistema);
        Assert.Equal("Termos da saúde digital.", PeValores.TextoDoRico(PeDocConfig.TextoDoBloco(PeDocConfig.Ler(BlocoDoModelo("glossario_extra").Config))));
    }

    [Fact]
    public async Task BaseComAVersao2_RecebeODocumentoEOLogotipo()
    {
        using var vazio = new PeBancoVazio();
        var v2 = PeCarregadorModelo.LerSeed();
        v2.Versao = 2;
        await new PeCarregadorModelo(vazio.Context).CarregarAsync(v2);
        Assert.Empty(vazio.Context.PeDocModelos);

        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();

        Assert.True(resultado.Executou);
        Assert.Equal(2, resultado.VersaoAnterior);
        // O do PDTIC e, desde a versão 6, o RA e o RR
        Assert.Equal(3, resultado.Documentos);
        Assert.Equal(vazio.Context.PeDocCapitulos.Count(), resultado.Capitulos);
        Assert.True(resultado.Capitulos >= 38);
        Assert.Equal(vazio.Context.PeDocBlocos.Count(), resultado.Blocos);
        // O logotipo já veio com a versão 2 lida deste JSON; o resto do modelo não muda
        Assert.Equal(0, resultado.Passos + resultado.Secoes + resultado.Niveis + resultado.Etapas);
    }

    // ── Validação do JSON do documento ────────────────────────────────────────

    [Fact]
    public async Task Json_BlocoComSecaoQueNaoExiste_RecusaSemGravarNada()
    {
        using var vazio = new PeBancoVazio();
        var documentos = PeDocSeed.Ler();
        documentos.Modelos[0].Capitulos.Single(c => c.Chave == "ativos").Blocos.Add(new PeSeedDocBloco { Tipo = "tabela_secao", Secao = "nao_existe" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PeCarregadorModelo(vazio.Context).CarregarAsync(PeCarregadorModelo.LerSeed(), documentos));

        Assert.Contains("ativos", ex.Message);
        Assert.Contains("nao_existe", ex.Message);
        Assert.Empty(vazio.Context.PePassos);
        Assert.Empty(vazio.Context.PeDocModelos);
    }

    [Fact]
    public void Json_EstruturaErrada_Recusa()
    {
        var passos = PeCarregadorModelo.LerSeed().Etapas.SelectMany(e => e.Passos).Select(p => p.Chave).ToHashSet();

        var passo = PeDocSeed.Ler();
        passo.Modelos[0].Capitulos[5].Passo = "preparacao.nao-existe";
        Assert.Contains("preparacao.nao-existe", Assert.Throws<InvalidOperationException>(() => PeDocSeed.Validar(passo, passos)).Message);

        var repetido = PeDocSeed.Ler();
        repetido.Modelos[0].Capitulos[6].Chave = "introducao";
        Assert.Throws<InvalidOperationException>(() => PeDocSeed.Validar(repetido, passos));

        var neto = PeDocSeed.Ler();
        neto.Modelos[0].Capitulos.Single(c => c.Chave == "diagnostico").Subcapitulos[0].Subcapitulos.Add(new PeSeedDocCapitulo { Chave = "neto", Titulo = "Neto" });
        Assert.Throws<InvalidOperationException>(() => PeDocSeed.Validar(neto, passos));

        var semInciso = PeDocSeed.Ler();
        semInciso.Modelos[0].Capitulos.Single(c => c.Chave == "ativos").Inciso = null;
        Assert.Throws<InvalidOperationException>(() => PeDocSeed.Validar(semInciso, passos));

        var blocoSemTexto = PeDocSeed.Ler();
        blocoSemTexto.Modelos[0].Capitulos[5].Blocos.Add(new PeSeedDocBloco { Tipo = "texto" });
        Assert.Throws<InvalidOperationException>(() => PeDocSeed.Validar(blocoSemTexto, passos));
    }

    // ── Marcação simples dos textos padrão ────────────────────────────────────

    [Fact]
    public void MarcacaoSimples_ViraTipTap_ComCadaElemento()
    {
        var doc = PeDocTextoSimples.ParaTipTap(new[]
        {
            "### Título três",
            "#### Título quatro",
            "Parágrafo com **negrito** no meio.",
            "",
            "- item um",
            "- item dois",
            "3. terceiro",
            "4. quarto",
            "| Sigla | Significado |",
            "| TIC | Tecnologia da informação e comunicação |"
        });

        var nos = doc["content"]!.AsArray().Select(n => n!["type"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "heading", "heading", "paragraph", "paragraph", "bulletList", "orderedList", "table" }, nos);
        Assert.Equal(4, doc["content"]![1]!["attrs"]!["level"]!.GetValue<int>());
        var negrito = doc["content"]![2]!["content"]![1]!;
        Assert.Equal("negrito", negrito["text"]!.GetValue<string>());
        Assert.Equal("bold", negrito["marks"]![0]!["type"]!.GetValue<string>());
        Assert.Null(doc["content"]![3]!["content"]);
        Assert.Equal(2, doc["content"]![4]!["content"]!.AsArray().Count);
        Assert.Equal(3, doc["content"]![5]!["attrs"]!["start"]!.GetValue<int>());
        var tabela = doc["content"]![6]!["content"]!.AsArray();
        Assert.Equal("tableHeader", tabela[0]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("tableCell", tabela[1]!["content"]![1]!["type"]!.GetValue<string>());

        // O resultado passa na lista fechada do texto rico
        Assert.Null(PeTextoRico.Validar(JsonSerializer.SerializeToElement(doc)).Erro);
    }
}
