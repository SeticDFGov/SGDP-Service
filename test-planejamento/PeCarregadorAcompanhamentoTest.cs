using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Carregador, versão 6 (E7, rodada B): as sete seções por ciclo (quatro do monitoramento e três
/// da avaliação intermediária), marcadas também numa base que já tinha as seções; as
/// configurações do prazo de fechamento (15 dias) e dos dias da avaliação final (90); os modelos
/// do RA (Anexo XIV) e do RR (Anexo XV), com os blocos novos; a marca que liga o acompanhamento
/// (a versão 6 carregada) e as recusas do JSON.
/// </summary>
public class PeCarregadorAcompanhamentoTest : PeDocumentoTestBase
{
    private static readonly Dictionary<string, string> Esperadas = new()
    {
        ["monitoramento_acoes"] = "monitoramento",
        ["medicoes_indicadores"] = "monitoramento",
        ["riscos_ocorridos"] = "monitoramento",
        ["relatorio_ciclo"] = "monitoramento",
        ["resultados_intermediarios"] = "avaliacao",
        ["analise_intermediaria"] = "avaliacao",
        ["avaliacao_comite"] = "avaliacao"
    };

    private static List<(string, string)> Marcadas(AppDbContext context)
    {
        var secoes = context.PeSecoes.AsNoTracking().ToDictionary(s => s.Id, s => s.Chave);
        return context.PeSecoesCiclo.AsNoTracking().ToList().Select(m => (secoes[m.Id], m.PorCiclo)).OrderBy(m => m.Item1).ToList();
    }

    private static List<(string, string)> Ordenadas(Dictionary<string, string> marcas) => marcas.Select(m => (m.Key, m.Value)).OrderBy(m => m.Item1).ToList();

    private List<string> Capitulos(string tipo)
    {
        var modelo = Context.PeDocModelos.AsNoTracking().Single(m => m.Tipo == tipo).Id;
        return Context.PeDocCapitulos.AsNoTracking().Where(c => c.ModeloId == modelo && c.PaiId == null).OrderBy(c => c.Ordem).Select(c => c.Chave).ToList();
    }

    private List<string> TiposDosBlocos(string capitulo, string tipo)
    {
        var id = CapituloDoModelo(capitulo, tipo).Id;
        return Context.PeDocBlocos.AsNoTracking().Where(b => b.CapituloId == id).OrderBy(b => b.Ordem).Select(b => b.Tipo).ToList();
    }

    [Fact]
    public async Task Versao6_AsSecoesPorCiclo_AsConfiguracoes_EOAcompanhamentoLigado()
    {
        Assert.Equal(Ordenadas(Esperadas), Marcadas(Context));
        Assert.Equal("15", Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChavePrazoFechamentoCiclo).Valor);
        Assert.Equal("90", Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveDiasAvaliacaoFinal).Valor);

        var acompanhamento = await PeAcompanhamentoAtivo.LerAsync(Context);
        Assert.Equal((true, 15, 90, "trimestral"),
            (acompanhamento.Ativo, acompanhamento.PrazoFechamentoDias, acompanhamento.DiasAvaliacaoFinal, acompanhamento.PeriodicidadePadrao));

        // O modelo diz a seção por ciclo (e o GET modelo também)
        var modelo = await Modelo.ObterModeloAsync(false);
        var secoes = modelo.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).ToDictionary(s => s.Chave, s => s.PorCiclo);
        Assert.Equal(("monitoramento", "avaliacao", (string?)null), (secoes["relatorio_ciclo"], secoes["avaliacao_comite"], secoes["metas"]));
    }

    [Fact]
    public void Modelos_DoRaEDoRr_ComOsCapitulosDosAnexos_EOsBlocosNovos()
    {
        Assert.Equal(new[]
        {
            "capa", "folha_rosto", "historico_versoes", "sumario", "apresentacao", "introducao", "metodologia", "monitoramento_acoes", "avaliacao_metas",
            "execucao_orcamentaria", "riscos", "alinhamento", "pessoas", "conclusao", "anexos"
        }, Capitulos(PeDominios.TipoDocumento.Ra));
        Assert.Equal(new[]
        {
            "capa", "folha_rosto", "historico_versoes", "sumario", "apresentacao", "introducao", "metodologia", "avaliacao_metas", "alinhamento",
            "nao_priorizadas", "execucao_orcamentaria", "pessoas", "licoes_aprendidas", "conclusao", "anexos"
        }, Capitulos(PeDominios.TipoDocumento.Rr));

        Assert.Equal(new[] { "texto", "acoes_por_situacao", "medicoes", "tabela_secao" }, TiposDosBlocos("monitoramento_acoes", PeDominios.TipoDocumento.Ra));
        Assert.Equal(new[] { "texto", "metas_por_resultado", "acoes_por_situacao", "tabela_secao", "riscos_ocorridos", "tabela_secao" },
            TiposDosBlocos("avaliacao_metas", PeDominios.TipoDocumento.Rr));
        // Os capítulos da avaliação intermediária no RA (os que ficam em branco no RA de um monitoramento)
        Assert.Equal("avaliacao-intermediaria.resultados-intermediarios", CapituloDoModelo("avaliacao_metas", PeDominios.TipoDocumento.Ra).PassoChave);
        Assert.Equal("avaliacao-intermediaria.comparacao-metas", CapituloDoModelo("pessoas", PeDominios.TipoDocumento.Ra).PassoChave);
        Assert.Equal("monitoramento.ciclo-monitoramento", CapituloDoModelo("monitoramento_acoes", PeDominios.TipoDocumento.Ra).PassoChave);

        // Os blocos novos só têm a página deitada no config
        var acoes = BlocoDoModelo("monitoramento_acoes", PeDominios.TipoBloco.AcoesPorSituacao, PeDominios.TipoDocumento.Ra);
        Assert.True(PeDocConfig.PaginaDeitada(PeDocConfig.Ler(acoes.Config)));
        Assert.Empty(PeDocConfig.Ler(BlocoDoModelo("monitoramento_acoes", PeDominios.TipoBloco.Medicoes, PeDominios.TipoDocumento.Ra).Config));
    }

    [Fact]
    public async Task ModeloDoDocumento_DoRaEDoRr_PeloTipo_ComOsMarcadoresDoCiclo()
    {
        var ra = await ModeloDoc.ObterAsync(PeDominios.TipoDocumento.Ra);
        var rr = await ModeloDoc.ObterAsync(PeDominios.TipoDocumento.Rr);

        Assert.Equal((PeDominios.TipoDocumento.Ra, 15), (ra.Tipo, ra.Capitulos.Count(c => c.PaiId == null)));
        Assert.Equal((PeDominios.TipoDocumento.Rr, 15), (rr.Tipo, rr.Capitulos.Count(c => c.PaiId == null)));
        Assert.Contains(ra.Marcadores, m => m.Chave == "ciclo.rotulo");
        Assert.Contains(ra.Marcadores, m => m.Chave == "ciclo.fim");
    }

    [Fact]
    public async Task BaseNaVersao5_RecebeAsMarcasOsModelosEAsConfiguracoes_UmaVezSo()
    {
        using var vazio = new PeBancoVazio();
        var v5 = PeCarregadorModelo.LerSeed();
        v5.Versao = 5;
        foreach (var secao in v5.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes)) secao.PorCiclo = null;
        v5.Configuracoes.Remove(PeConfiguracao.ChavePrazoFechamentoCiclo);
        v5.Configuracoes.Remove(PeConfiguracao.ChaveDiasAvaliacaoFinal);
        var documentos5 = PeDocSeed.Ler();
        documentos5.Modelos.RemoveAll(m => m.Tipo != PeDominios.TipoDocumento.Pdtic);
        await new PeCarregadorModelo(vazio.Context).CarregarAsync(v5, documentos5);
        Assert.Empty(vazio.Context.PeSecoesCiclo);
        Assert.False(await PeAcompanhamentoAtivo.AtivoAsync(vazio.Context));
        Assert.Equal(6, PeCarregadorModelo.VersaoDoAcompanhamento);

        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();

        Assert.Equal((5, 6, 7, 2, 2), (resultado.VersaoAnterior, resultado.Versao, resultado.SecoesPorCiclo, resultado.Documentos, resultado.Configuracoes));
        Assert.Equal(0, resultado.Secoes + resultado.Passos + resultado.Campos);
        Assert.Equal(Ordenadas(Esperadas), Marcadas(vazio.Context));
        Assert.True(await PeAcompanhamentoAtivo.AtivoAsync(vazio.Context));

        // Uma versão nova depois: as marcas e os modelos que existem não se repetem
        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        var depois = await new PeCarregadorModelo(vazio.Context).CarregarAsync(seed);
        Assert.Equal((0, 0), (depois.SecoesPorCiclo, depois.Documentos));
        Assert.Equal(7, vazio.Context.PeSecoesCiclo.Count());
    }

    [Fact]
    public void Json_PorCicloForaDoDominio_OuNumaSecaoForaDoPdtic_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == "relatorio_ciclo").PorCiclo = "semestre";
        var ex = Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
        Assert.Contains("porCiclo da seção \"relatorio_ciclo\"", ex.Message);

        var foraDoPdtic = PeCarregadorModelo.LerSeed();
        foraDoPdtic.SecoesForaDoPdtic[0].PorCiclo = "monitoramento";
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(foraDoPdtic));
    }
}
