using System.Globalization;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A situação dos passos do acompanhamento (E7, rodada B): o monitoramento pelos ciclos
/// (aguardando antes do primeiro começar, atrasado com o ciclo vencido e aberto, pendente com o
/// ciclo no prazo, feito com todos os começados fechados), os passos da avaliação intermediária
/// aguardando até a equipe abrir uma avaliação (e depois pela mais recente), a etapa 7 esperando
/// os dias antes do fim da vigência (ou até ter dado), o próximo passo e, sem a versão 6, o
/// comportamento da rodada A.
/// </summary>
public class PeSituacaoAcompanhamentoTest : PeAcompanhamentoTestBase
{
    private const string Ciclo = "monitoramento.ciclo-monitoramento";
    private const string Resumo = "monitoramento.relatorio-acompanhamento";

    private static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private void VigenciaNoBanco(long pdticId, DateOnly inicio, DateOnly fim)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        pdtic.VigenciaInicio = inicio;
        pdtic.VigenciaFim = fim;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Monitoramento_AtrasadoComOCicloVencido_EOProximoPassoEEle()
    {
        var id = await AcompanhadoAsync();

        var situacao = await SituacaoAsync(id);
        var ciclo = situacao.Passos.Single(p => p.Chave == Ciclo);
        var resumo = situacao.Passos.Single(p => p.Chave == Resumo);

        // Os trimestres de 2026 até o que venceu estão abertos e atrasados
        Assert.Equal(PeDominios.SituacaoPasso.Atrasado, ciclo.Situacao);
        Assert.StartsWith("Os ciclos 2026 · 1º trimestre", ciclo.Motivo);
        Assert.EndsWith("passaram do prazo de fechamento e ainda estão abertos", ciclo.Motivo);
        Assert.Equal(PeDominios.SituacaoPasso.Atrasado, resumo.Situacao);
        Assert.True(ciclo.PodeEditar);
        // O atrasado passa na frente de tudo
        Assert.Equal(ciclo.Numero, situacao.ProximoPasso);
    }

    [Fact]
    public async Task Monitoramento_PendenteNoPrazo_EFeitoComOsComecadosFechados()
    {
        // Publicado hoje: o ciclo do trimestre de hoje está aberto e no prazo
        var id = await AcompanhadoAsync(DateTime.UtcNow);
        var aberto = (await CiclosAsync(id)).Single(c => c.Situacao == PeDominios.SituacaoCicloExibida.Aberto);

        var passo = await PassoDaSituacaoAsync(id, Ciclo);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, passo.Situacao);
        Assert.Null(passo.Motivo);

        await PreencherCicloAsync(id, aberto.Id);
        await FecharAsync(aberto.Id);

        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(id, Ciclo)).Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(id, Resumo)).Situacao);
    }

    [Fact]
    public async Task Monitoramento_AguardandoOPrimeiroCiclo_QuandoAVigenciaAindaNaoComecou()
    {
        var id = await AcompanhadoAsync(DateTime.UtcNow);
        var inicio = new DateOnly(PeCiclos.Hoje().Year + 1, 1, 1);
        VigenciaNoBanco(id, inicio, inicio.AddYears(3).AddDays(-1));

        var passo = await PassoDaSituacaoAsync(id, Ciclo);

        Assert.Equal(PeDominios.SituacaoPasso.Aguardando, passo.Situacao);
        Assert.Equal($"O primeiro ciclo de monitoramento começa em {Data(inicio)}", passo.Motivo);
    }

    [Fact]
    public async Task Avaliacao_AguardandoAteAbrirUma_DepoisPelaMaisRecente()
    {
        var id = await AcompanhadoAsync();
        const string resultados = "avaliacao-intermediaria.resultados-intermediarios";
        const string comite = "avaliacao-intermediaria.avaliacao-comite";

        var antes = await PassoDaSituacaoAsync(id, resultados);
        Assert.Equal((PeDominios.SituacaoPasso.Aguardando, PePdticService.MotivoSemAvaliacao), (antes.Situacao, antes.Motivo));
        Assert.Equal(PeDominios.SituacaoPasso.Aguardando, (await PassoDaSituacaoAsync(id, comite)).Situacao);

        var avaliacao = await AbrirAvaliacaoAsync(id);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoDaSituacaoAsync(id, resultados)).Situacao);
        await IncluirNoPdticAsync(id, "resultados_intermediarios",
            new { valor_alcancado = "40%", data = "2026-09-01", situacao = "em_andamento" }, new { meta = new[] { IdDe(id, "M01") } }, cicloId: avaliacao.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(id, resultados)).Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoDaSituacaoAsync(id, comite)).Situacao);
    }

    [Fact]
    public async Task Etapa7_EsperaOsDiasAntesDoFimDaVigencia_OuODado()
    {
        var id = await AcompanhadoAsync();
        const string licoes = "fechamento.licoes-aprendidas";

        var espera = await PassoDaSituacaoAsync(id, licoes);
        Assert.Equal((PeDominios.SituacaoPasso.Aguardando, "Disponível a partir de 02/10/2029, 90 dias antes do fim da vigência (31/12/2029)"),
            (espera.Situacao, espera.Motivo));
        // A espera não impede a edição (quem já tem o dado grava antes): o passo continua editável
        Assert.True(espera.PodeEditar);

        // Com um dado na etapa, ela deixa de esperar
        await IncluirNoPdticAsync(id, "resultados_metas", new { resultado = "alcancada", motivo = "O sistema chegou a todas as unidades." }, new { meta = new[] { IdDe(id, "M01") } });
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoDaSituacaoAsync(id, licoes)).Situacao);
    }

    [Fact]
    public async Task Etapa7_DisponivelDentroDosDias()
    {
        var id = await AcompanhadoAsync();
        var fim = PeCiclos.Hoje().AddDays(60);
        VigenciaNoBanco(id, new DateOnly(2026, 1, 1), fim);

        var passo = await PassoDaSituacaoAsync(id, "fechamento.licoes-aprendidas");

        Assert.Equal(PeDominios.SituacaoPasso.Pendente, passo.Situacao);
        Assert.Null(passo.Motivo);
    }

    [Fact]
    public async Task SemAVersao6_OMonitoramentoContinuaContinuo()
    {
        var id = await AcompanhadoAsync();
        VersaoDoModelo(5);

        var situacao = await SituacaoAsync(id);

        Assert.Equal(PeDominios.SituacaoPasso.Continuo, situacao.Passos.Single(p => p.Chave == Ciclo).Situacao);
        Assert.NotEqual(PeDominios.SituacaoPasso.Aguardando, situacao.Passos.Single(p => p.Chave == "fechamento.licoes-aprendidas").Situacao);
    }
}
