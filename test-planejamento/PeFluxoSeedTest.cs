using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Os fluxos do guia semeados (E6, versão 4 do carregador): as dez figuras (4, 5, 6, 7, 14, 17,
/// 19, 20, 21 e 22), cada uma válida pela validação da API, com a numeração do guia, as raias
/// com os nomes do dicionário, as decisões e os retornos que as figuras desenham e os que só o
/// texto do guia descreve, os artefatos; e o carregador: acrescenta o que falta, não duplica e
/// não sobrescreve o que o administrador mudou.
/// </summary>
public class PeFluxoSeedTest : PeFluxoTestBase
{
    private static readonly string[] Chaves =
    {
        "macroprocesso", "elaboracao", "preparacao", "diagnostico", "planejamento", "acompanhamento",
        "planejamento_acompanhamento", "monitoramento", "avaliacao_intermediaria", "avaliacao_final"
    };

    private static List<string> Numeros(PeFluxoDefinicao d) =>
        PeFluxoAnalise.Analisar(d).OrdemDeLeitura.Where(e => e.Numero != null).Select(e => e.Numero!).ToList();

    private static PeFluxoElemento Por(PeFluxoDefinicao d, string numeroOuId) =>
        d.Elementos.Single(e => e.Numero == numeroOuId || e.Id == numeroOuId);

    /// <summary>As ligações que voltam (retornos) como "de -> para" pelos números ou ids.</summary>
    private static List<string> Retornos(PeFluxoDefinicao d)
    {
        var analise = PeFluxoAnalise.Analisar(d);
        string Nome(string id) => analise.PorId[id].Numero ?? id;
        return d.Ligacoes.Where(analise.EhRetorno).Select(l => $"{Nome(l.De)} -> {Nome(l.Para)} ({l.Rotulo})").ToList();
    }

    [Fact]
    public void DezFluxos_NaOrdem_ComAsFiguras()
    {
        var modelos = Context.PeFluxosModelo.AsNoTracking().OrderBy(m => m.Ordem).ToList();
        Assert.Equal(Chaves, modelos.Select(m => m.Chave));
        Assert.Equal(new[] { "Figura 4", "Figura 5", "Figura 6", "Figura 7", "Figura 14", "Figura 17", "Figura 19", "Figura 20", "Figura 21", "Figura 22" },
            modelos.Select(m => m.FiguraGuia));
        Assert.Equal(Enumerable.Range(1, 10), modelos.Select(m => m.Ordem));
        Assert.All(modelos, m => Assert.Equal(PeCarregadorModelo.Autor, m.CriadoPor));
        // O bloco de fluxo do documento (E5) aponta para chaves que existem
        Assert.Contains("elaboracao", Chaves);
        Assert.Contains("acompanhamento", Chaves);
    }

    [Fact]
    public void CadaFluxo_ValidoPelaValidacaoDaApi()
    {
        var validos = PeFluxoSeed.Validar(PeFluxoSeed.Ler());
        Assert.Equal(10, validos.Count);
        foreach (var chave in Chaves)
        {
            var resultado = PeFluxoDefinicaoLeitor.Ler(ComoJson(DefinicaoDoModelo(chave)));
            Assert.True(resultado.Valida, $"{chave}: {string.Join(" | ", resultado.Erros)}");
        }
    }

    [Fact]
    public void Numeracao_ADoGuia()
    {
        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"1.{i}"), Numeros(DefinicaoDoModelo("preparacao")));
        Assert.Equal(Enumerable.Range(1, 14).Select(i => $"2.{i}"), Numeros(DefinicaoDoModelo("diagnostico")));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"3.{i}"), Numeros(DefinicaoDoModelo("planejamento")));
        Assert.Equal(Enumerable.Range(1, 6).Select(i => $"4.{i}"), Numeros(DefinicaoDoModelo("planejamento_acompanhamento")));
        Assert.Equal(new[] { "5.1", "5.2" }, Numeros(DefinicaoDoModelo("monitoramento")));
        Assert.Equal(new[] { "6.1", "6.2", "6.3" }, Numeros(DefinicaoDoModelo("avaliacao_intermediaria")));
        Assert.Equal(new[] { "7.1", "7.2", "7.3", "7.4" }, Numeros(DefinicaoDoModelo("avaliacao_final")));
        // Os macrofluxos não têm número (como nas figuras 4, 5 e 17)
        foreach (var chave in new[] { "macroprocesso", "elaboracao", "acompanhamento" })
            Assert.Empty(Numeros(DefinicaoDoModelo(chave)));

        var diagnostico = DefinicaoDoModelo("diagnostico");
        Assert.Equal("Identificar as necessidades de infraestrutura de TIC", Por(diagnostico, "2.9").Nome);
        Assert.Equal("Identificar as necessidades de contratação de TIC", Por(diagnostico, "2.10").Nome);
        Assert.Equal("Identificar as necessidades de pessoal de TIC", Por(diagnostico, "2.11").Nome);
        Assert.Equal("Aprovar o inventário de necessidades", Por(diagnostico, "2.14").Nome);
        Assert.Equal("Definir a abrangência e o período do PDTIC", Por(DefinicaoDoModelo("preparacao"), "1.1").Nome);
        Assert.Equal("Publicar o PDTIC", Por(DefinicaoDoModelo("planejamento"), "3.10").Nome);
    }

    [Fact]
    public void Raias_ComOsNomesDoDicionario()
    {
        string Raias(string chave) => string.Join(" | ", DefinicaoDoModelo(chave).Raias.Select(r => r.Nome));
        Assert.Equal("{nomes.comite} | {nomes.equipe}", Raias("preparacao"));
        Assert.Equal("{nomes.comite} | {nomes.equipe}", Raias("diagnostico"));
        Assert.Equal("{nomes.autoridade_cargo} | {nomes.comite} | {nomes.equipe}", Raias("planejamento"));
        Assert.Equal("{nomes.comite} | {nomes.equipe_acompanhamento}", Raias("planejamento_acompanhamento"));
        Assert.Equal("{nomes.comite} | {nomes.equipe_acompanhamento}", Raias("monitoramento"));
        Assert.Equal("{nomes.comite} | {nomes.equipe_acompanhamento}", Raias("avaliacao_intermediaria"));
        Assert.Equal("{nomes.autoridade_cargo} | {nomes.comite} | {nomes.equipe_acompanhamento}", Raias("avaliacao_final"));
        // Quem faz o quê, como no guia: o comitê aprova, a equipe elabora, a autoridade publica
        var planejamento = DefinicaoDoModelo("planejamento");
        string RaiaDe(string numero) => planejamento.Raias.Single(r => r.Id == Por(planejamento, numero).RaiaId).Nome;
        Assert.Equal("{nomes.comite}", RaiaDe("3.1"));
        Assert.Equal("{nomes.equipe}", RaiaDe("3.8"));
        Assert.Equal("{nomes.comite}", RaiaDe("3.9"));
        Assert.Equal("{nomes.autoridade_cargo}", RaiaDe("3.10"));
    }

    [Fact]
    public void DecisoesERetornos_DasFigurasEDoTexto()
    {
        // Desenhados nas figuras 14 e 17
        Assert.Equal(new[] { "d1 -> 3.8 (Não)" }, Retornos(DefinicaoDoModelo("planejamento")));
        Assert.Equal(new[] { "s5 -> s1 ()" }, Retornos(DefinicaoDoModelo("acompanhamento")));
        // Só no texto do guia: reprovação volta para quem elaborou
        Assert.Equal(new[] { "d1 -> 1.7 (Não)" }, Retornos(DefinicaoDoModelo("preparacao")));
        Assert.Equal(new[] { "d1 -> 2.12 (Não)" }, Retornos(DefinicaoDoModelo("diagnostico")));
        Assert.Equal(new[] { "d1 -> 4.5 (Não)" }, Retornos(DefinicaoDoModelo("planejamento_acompanhamento")));
        Assert.Equal(new[] { "d2 -> 5.2 (Não)" }, Retornos(DefinicaoDoModelo("monitoramento")));
        Assert.Equal(new[] { "d1 -> 6.2 (Não)" }, Retornos(DefinicaoDoModelo("avaliacao_intermediaria")));
        Assert.Equal(new[] { "d1 -> 7.2 (Não)", "d2 -> 7.3 (Não)" }, Retornos(DefinicaoDoModelo("avaliacao_final")));
        Assert.Equal(new[] { "d1 -> s1 (Sim)" }, Retornos(DefinicaoDoModelo("macroprocesso")));
        Assert.Empty(Retornos(DefinicaoDoModelo("elaboracao")));

        // Figura 7: a ligação vinda da avaliação intermediária e os três caminhos em paralelo
        var diagnostico = DefinicaoDoModelo("diagnostico");
        var entrada = diagnostico.Elementos.Single(e => e.Tipo == PeDominios.TipoElementoFluxo.Ligacao);
        Assert.Equal("Revisão do PDTIC [Avaliação intermediária]", entrada.Nome);
        Assert.Equal(2, diagnostico.Elementos.Count(e => e.Tipo == PeDominios.TipoElementoFluxo.Paralelo));
        var analise = PeFluxoAnalise.Analisar(diagnostico);
        var paralelos = new[] { "2.9", "2.10", "2.11" }.Select(n => Por(diagnostico, n)).ToList();
        Assert.Single(paralelos.Select(p => analise.Camada[p.Id]).Distinct());
        Assert.Equal(new[] { -1, 0, 1 }, paralelos.Select(p => analise.Faixa[p.Id]));

        // Figura 21: prosseguir ou revisar o PDTIC (segue no diagnóstico da elaboração)
        var intermediaria = DefinicaoDoModelo("avaliacao_intermediaria");
        var saidas = intermediaria.Ligacoes.Where(l => l.De == "d2").Select(l => l.Rotulo).ToList();
        Assert.Equal(new[] { "Prosseguir", "Revisão do PDTIC" }, saidas);
        Assert.Equal("Diagnóstico [Elaboração do PDTIC]", intermediaria.Elementos.Single(e => e.Tipo == PeDominios.TipoElementoFluxo.Ligacao).Nome);

        // Figura 17: encerramento, avaliação final e intermediária
        var acompanhamento = DefinicaoDoModelo("acompanhamento");
        Assert.Equal("Encerramento do PDTIC?", acompanhamento.Elementos.Single(e => e.Tipo == PeDominios.TipoElementoFluxo.Decisao).Nome);
    }

    [Fact]
    public void Artefatos_DasFiguras()
    {
        Assert.Equal(new[] { "Lista dos princípios e diretrizes", "Inventário de necessidades (parcial)" },
            Por(DefinicaoDoModelo("preparacao"), "1.6").Artefatos);
        Assert.Equal(new[] { "Resumo do PDTIC (DODF)", "PDTIC no site do órgão" }, Por(DefinicaoDoModelo("planejamento"), "3.10").Artefatos);
        Assert.Equal(new[] { "Plano de acompanhamento (PA-PDTIC)" }, Por(DefinicaoDoModelo("planejamento_acompanhamento"), "4.5").Artefatos);
        Assert.Equal(new[] { "PDTIC" }, DefinicaoDoModelo("macroprocesso").Elementos.Single(e => e.Nome == "Elaboração").Artefatos);
        Assert.All(Chaves, chave => Assert.All(DefinicaoDoModelo(chave).Elementos.Where(e => e.Artefatos.Count > 0),
            e => Assert.True(PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo))));
    }

    [Fact]
    public void Json_EmbutidoNaAplicacao_SemTravessao()
    {
        using var stream = typeof(PeFluxoSeed).Assembly.GetManifestResourceStream(PeFluxoSeed.Recurso);
        Assert.NotNull(stream);
        var texto = new StreamReader(stream!).ReadToEnd();
        Assert.DoesNotContain((char)0x2014, texto);
        Assert.DoesNotContain((char)0x2013, texto);
    }

    // ── Carregador ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SegundaCarga_NaoDuplica()
    {
        var resultado = await new PeCarregadorModelo(Context).CarregarAsync();
        Assert.False(resultado.Executou);
        Assert.Equal(10, Context.PeFluxosModelo.Count());
    }

    [Fact]
    public async Task BaseComAVersao3_RecebeOsFluxos()
    {
        using var vazio = new PeBancoVazio();
        var v3 = PeCarregadorModelo.LerSeed();
        v3.Versao = 3;
        await new PeCarregadorModelo(vazio.Context).CarregarAsync(v3);
        Assert.Empty(vazio.Context.PeFluxosModelo);

        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();

        Assert.True(resultado.Executou);
        Assert.Equal(3, resultado.VersaoAnterior);
        Assert.Equal(10, resultado.Fluxos);
        Assert.Equal(0, resultado.Passos + resultado.Capitulos + resultado.Documentos);
        Assert.Equal(10, vazio.Context.PeFluxosModelo.Count());
    }

    [Fact]
    public async Task VersaoNova_NaoMexeNoQueOAdministradorMudou_EAcrescentaOQueFalta()
    {
        var admin = await Admin();
        var definicao = DefinicaoDoModelo("preparacao");
        definicao.Elementos.Single(e => e.Numero == "1.3").Nome = "Descrever a metodologia do órgão";
        await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao, "Preparação do PDTIC"), admin);

        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        var fluxos = PeFluxoSeed.Ler();
        var novo = PeFluxoSeed.Ler().Fluxos.Single(f => f.Chave == "elaboracao");
        novo.Chave = "revisao_extra";
        novo.Nome = "Revisão extra";
        novo.FiguraGuia = null;
        fluxos.Fluxos.Add(novo);

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed, null, fluxos);

        Assert.Equal(1, resultado.Fluxos);
        Assert.Equal("Preparação do PDTIC", ModeloNoBanco("preparacao").Nome);
        Assert.Equal("Descrever a metodologia do órgão", DefinicaoDoModelo("preparacao").Elementos.Single(e => e.Numero == "1.3").Nome);
        var extra = ModeloNoBanco("revisao_extra");
        Assert.Equal(11, extra.Ordem);
        Assert.Null(extra.FiguraGuia);
    }

    [Fact]
    public async Task Json_FluxoInvalido_NadaEGravado()
    {
        using var vazio = new PeBancoVazio();
        var fluxos = PeFluxoSeed.Ler();
        var ruim = fluxos.Fluxos.Single(f => f.Chave == "monitoramento");
        ruim.Definicao = ComoJson(new { Raias = new[] { new { Id = "r1", Nome = "Equipe", Ordem = 1 } }, Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PeCarregadorModelo(vazio.Context).CarregarAsync(PeCarregadorModelo.LerSeed(), null, fluxos));

        Assert.Contains("monitoramento", ex.Message);
        Assert.Contains("O fluxo precisa de um início.", ex.Message);
        Assert.Empty(vazio.Context.PePassos);
        Assert.Empty(vazio.Context.PeFluxosModelo);
    }

    [Fact]
    public void Json_ChaveRepetida_Recusa()
    {
        var fluxos = PeFluxoSeed.Ler();
        fluxos.Fluxos.Add(fluxos.Fluxos[0]);
        var ex = Assert.Throws<InvalidOperationException>(() => PeFluxoSeed.Validar(fluxos));
        Assert.Contains("macroprocesso", ex.Message);
    }

    [Fact]
    public void ContagemDosElementos()
    {
        // O que cada fluxo tem (raias, elementos, ligações), para o relatório da entrega
        var contagem = Chaves.ToDictionary(c => c, c =>
        {
            var d = DefinicaoDoModelo(c);
            return (d.Raias.Count, d.Elementos.Count, d.Ligacoes.Count);
        });
        Assert.Equal((1, 5, 5), contagem["macroprocesso"]);
        Assert.Equal((1, 5, 4), contagem["elaboracao"]);
        Assert.Equal((2, 11, 11), contagem["preparacao"]);
        Assert.Equal((2, 20, 22), contagem["diagnostico"]);
        Assert.Equal((3, 13, 13), contagem["planejamento"]);
        Assert.Equal((1, 10, 11), contagem["acompanhamento"]);
        Assert.Equal((2, 9, 9), contagem["planejamento_acompanhamento"]);
        Assert.Equal((2, 6, 7), contagem["monitoramento"]);
        Assert.Equal((2, 8, 8), contagem["avaliacao_intermediaria"]);
        Assert.Equal((3, 8, 9), contagem["avaliacao_final"]);
    }
}
