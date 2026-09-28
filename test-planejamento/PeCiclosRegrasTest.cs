using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As regras puras dos ciclos do acompanhamento (E7, rodada B), sem banco: os períodos pelo
/// calendário e cortados pela vigência, os rótulos de cada periodicidade, o começo do
/// acompanhamento (a publicação, nunca antes da vigência), o prazo de fechamento, a situação
/// exibida, o plano idempotente dos ciclos de monitoramento (a troca da periodicidade recria só
/// os ciclos sem registro e não fechados, sem cobrir o que ficou), o rótulo curto do nome do
/// arquivo do RA e as regras do painel (situação física, percentuais e orçamento).
/// </summary>
public class PeCiclosRegrasTest
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static PePdtic Pdtic(DateOnly? inicio, DateOnly? fim, DateTime? publicado, bool externo = false, DateTime? criado = null) => new()
    {
        Id = 7,
        OrgaoId = 1,
        Versao = "1.0",
        Situacao = PeDominios.SituacaoPdtic.Publicado,
        VigenciaInicio = inicio,
        VigenciaFim = fim,
        PublicadoEm = publicado,
        RegistradoExternamente = externo,
        CriadoEm = criado ?? new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc),
        CriadoPor = "x"
    };

    [Fact]
    public void Periodos_Trimestrais_DoPeriodoDaPublicacaoAteOFimDaVigencia()
    {
        var periodos = PeCiclos.Periodos(D(2026, 9, 24), D(2026, 1, 1), D(2027, 12, 31), 3);

        Assert.Equal(new[]
        {
            "2026 · 3º trimestre", "2026 · 4º trimestre", "2027 · 1º trimestre", "2027 · 2º trimestre", "2027 · 3º trimestre", "2027 · 4º trimestre"
        }, periodos.Select(p => p.Rotulo));
        Assert.Equal((D(2026, 7, 1), D(2026, 9, 30)), (periodos[0].Inicio, periodos[0].Fim));
        Assert.Equal((D(2027, 10, 1), D(2027, 12, 31)), (periodos[^1].Inicio, periodos[^1].Fim));
    }

    [Theory]
    [InlineData(1, "2027 · março", "2027-03-01", "2027-03-31")]
    [InlineData(2, "2027 · 2º bimestre", "2027-03-01", "2027-04-30")]
    [InlineData(3, "2027 · 1º trimestre", "2027-01-01", "2027-03-31")]
    [InlineData(6, "2027 · 1º semestre", "2027-01-01", "2027-06-30")]
    [InlineData(12, "2027", "2027-01-01", "2027-12-31")]
    public void Rotulos_DeCadaPeriodicidade(int meses, string rotulo, string inicio, string fim)
    {
        var primeiro = PeCiclos.Periodos(D(2027, 3, 15), D(2026, 1, 1), D(2029, 12, 31), meses)[0];

        Assert.Equal(rotulo, primeiro.Rotulo);
        Assert.Equal(DateOnly.Parse(inicio), primeiro.Inicio);
        Assert.Equal(DateOnly.Parse(fim), primeiro.Fim);
    }

    [Fact]
    public void Periodos_CortadosPelaVigencia_NasPontas()
    {
        var periodos = PeCiclos.Periodos(D(2025, 6, 1), D(2026, 2, 15), D(2026, 11, 10), 3);

        // O acompanhamento não começa antes da vigência; o primeiro e o último ciclos são cortados
        Assert.Equal(4, periodos.Count);
        Assert.Equal(("2026 · 1º trimestre", D(2026, 2, 15), D(2026, 3, 31)), (periodos[0].Rotulo, periodos[0].Inicio, periodos[0].Fim));
        Assert.Equal(("2026 · 4º trimestre", D(2026, 10, 1), D(2026, 11, 10)), (periodos[3].Rotulo, periodos[3].Inicio, periodos[3].Fim));
        // Periodicidade fora da lista vale como trimestral; começo depois do fim da vigência, nenhum
        Assert.Equal(4, PeCiclos.Periodos(D(2025, 6, 1), D(2026, 2, 15), D(2026, 11, 10), 5).Count);
        Assert.Empty(PeCiclos.Periodos(D(2027, 1, 1), D(2026, 2, 15), D(2026, 11, 10), 3));
    }

    [Fact]
    public void InicioDoAcompanhamento_APublicacaoEmBrasilia_NuncaAntesDaVigencia_ENoExternoORegistro()
    {
        // 01h de 10/03 em UTC ainda é 09/03 em Brasília
        Assert.Equal(D(2026, 3, 9), PeCiclos.InicioDoAcompanhamento(Pdtic(D(2026, 1, 1), D(2029, 12, 31), new DateTime(2026, 3, 10, 1, 0, 0, DateTimeKind.Utc))));
        Assert.Equal(D(2027, 1, 1), PeCiclos.InicioDoAcompanhamento(Pdtic(D(2027, 1, 1), D(2029, 12, 31), new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc))));
        Assert.Equal(D(2026, 8, 5), PeCiclos.InicioDoAcompanhamento(Pdtic(D(2026, 1, 1), D(2029, 12, 31), new DateTime(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc),
            externo: true, criado: new DateTime(2026, 8, 5, 15, 0, 0, DateTimeKind.Utc))));
    }

    [Fact]
    public void Plano_CriaOsCiclosComOPrazo_EDeNovoNaoMudaNada()
    {
        var pdtic = Pdtic(D(2026, 1, 1), D(2026, 12, 31), new DateTime(2026, 5, 20, 15, 0, 0, DateTimeKind.Utc));

        var plano = PeCiclos.PlanejarMonitoramento(pdtic, new List<PeCiclo>(), new HashSet<long>(), 3, 15, "equipe@ses", DateTime.UtcNow);

        Assert.Equal(new[] { "2026 · 2º trimestre", "2026 · 3º trimestre", "2026 · 4º trimestre" }, plano.Criar.Select(c => c.Rotulo));
        var primeiro = plano.Criar[0];
        Assert.Equal((D(2026, 4, 1), D(2026, 6, 30), D(2026, 7, 15)), (primeiro.Inicio, primeiro.Fim!.Value, primeiro.Prazo!.Value));
        Assert.All(plano.Criar, c => Assert.Equal((PeDominios.TipoCiclo.Monitoramento, PeDominios.SituacaoCiclo.Aberto, 7L), (c.Tipo, c.Situacao, c.PdticId)));

        // Os mesmos períodos já gravados: nada entra nem sai
        long id = 0;
        var gravados = plano.Resultado().Select(c =>
        {
            c.Id = ++id;
            return c;
        }).ToList();
        var deNovo = PeCiclos.PlanejarMonitoramento(pdtic, gravados, new HashSet<long>(), 3, 15, "equipe@ses", DateTime.UtcNow);
        Assert.False(deNovo.MudaAlgo);
        Assert.Equal(3, deNovo.Manter.Count);

        // Sem vigência, nada muda
        Assert.False(PeCiclos.PlanejarMonitoramento(Pdtic(null, null, DateTime.UtcNow), gravados, new HashSet<long>(), 3, 15, "x", DateTime.UtcNow).MudaAlgo);
    }

    [Fact]
    public void Plano_TrocaDePeriodicidade_MantemOComRegistroEOFechado_ESemCobrirOQueFicou()
    {
        var pdtic = Pdtic(D(2026, 1, 1), D(2026, 12, 31), new DateTime(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc));
        long id = 0;
        var trimestrais = PeCiclos.PlanejarMonitoramento(pdtic, new List<PeCiclo>(), new HashSet<long>(), 3, 15, "x", DateTime.UtcNow)
            .Resultado()
            .Select(c =>
            {
                c.Id = ++id;
                return c;
            })
            .ToList();
        // O 1º trimestre tem registro; o 2º foi fechado; o 3º e o 4º estão vazios
        trimestrais[1].Situacao = PeDominios.SituacaoCiclo.Fechado;

        var plano = PeCiclos.PlanejarMonitoramento(pdtic, trimestrais, new HashSet<long> { trimestrais[0].Id }, 1, 15, "x", DateTime.UtcNow);

        Assert.Equal(new[] { 1L, 2L }, plano.Manter.Select(c => c.Id));
        Assert.Equal(new[] { 3L, 4L }, plano.Apagar.Select(c => c.Id));
        // Os meses que faltam: julho a dezembro (janeiro a junho ficam com os trimestres que ficaram)
        Assert.Equal(new[] { "2026 · julho", "2026 · agosto", "2026 · setembro", "2026 · outubro", "2026 · novembro", "2026 · dezembro" },
            plano.Criar.Select(c => c.Rotulo));
        Assert.Equal(8, plano.Resultado().Count);
    }

    [Fact]
    public void Subtrair_OsPedacosQueSobram()
    {
        var restos = PeCiclos.Subtrair((D(2026, 1, 1), D(2026, 6, 30)), new[] { (D(2026, 2, 1), D(2026, 2, 28)), (D(2026, 5, 1), D(2026, 7, 31)) });

        Assert.Equal(new[] { (D(2026, 1, 1), D(2026, 1, 31)), (D(2026, 3, 1), D(2026, 4, 30)) }, restos);
        Assert.Empty(PeCiclos.Subtrair((D(2026, 1, 1), D(2026, 1, 31)), new[] { (D(2025, 12, 1), D(2026, 2, 28)) }));
    }

    [Fact]
    public void SituacaoExibida_FuturoAbertoAtrasadoFechado_EQuandoRecebeDados()
    {
        var ciclo = new PeCiclo
        {
            Tipo = PeDominios.TipoCiclo.Monitoramento,
            Rotulo = "2026 · 2º trimestre",
            Inicio = D(2026, 4, 1),
            Fim = D(2026, 6, 30),
            Prazo = D(2026, 7, 15),
            Situacao = PeDominios.SituacaoCiclo.Aberto
        };

        Assert.Equal(PeDominios.SituacaoCicloExibida.Futuro, PeCiclos.Exibida(ciclo, D(2026, 3, 31)));
        Assert.Equal(PeDominios.SituacaoCicloExibida.Aberto, PeCiclos.Exibida(ciclo, D(2026, 4, 1)));
        Assert.Equal(PeDominios.SituacaoCicloExibida.Aberto, PeCiclos.Exibida(ciclo, D(2026, 7, 15)));
        Assert.Equal(PeDominios.SituacaoCicloExibida.Atrasado, PeCiclos.Exibida(ciclo, D(2026, 7, 16)));
        Assert.Equal("O ciclo 2026 · 2º trimestre começa em 01/04/2026. Os dados entram a partir do início do ciclo.",
            PeCiclos.RecusaDeDados(ciclo, D(2026, 3, 31)));
        Assert.Null(PeCiclos.RecusaDeDados(ciclo, D(2026, 8, 1)));

        ciclo.Situacao = PeDominios.SituacaoCiclo.Fechado;
        Assert.Equal(PeDominios.SituacaoCicloExibida.Fechado, PeCiclos.Exibida(ciclo, D(2026, 8, 1)));
        Assert.Equal("O ciclo 2026 · 2º trimestre está fechado. Para mudar os dados dele, reabra o ciclo.", PeCiclos.RecusaDeDados(ciclo, D(2026, 8, 1)));
    }

    [Theory]
    [InlineData("monitoramento", "2027 · 1º trimestre", 3, "2027-T1")]
    [InlineData("monitoramento", "2027 · 2º bimestre", 3, "2027-B2")]
    [InlineData("monitoramento", "2027 · 1º semestre", 3, "2027-S1")]
    [InlineData("monitoramento", "2027 · março", 3, "2027-03")]
    [InlineData("monitoramento", "2027", 3, "2027")]
    [InlineData("monitoramento", "Outro nome", 3, "ciclo-3")]
    [InlineData("avaliacao", "Avaliação de meio de vigência", 2, "avaliacao-2")]
    public void RotuloCurto_DoNomeDoArquivoDoRa(string tipo, string rotulo, int numero, string curto) =>
        Assert.Equal(curto, PeCiclos.RotuloCurto(new PeCiclo { Tipo = tipo, Rotulo = rotulo, Numero = numero }));

    // ── Regras do painel ──────────────────────────────────────────────────────

    [Fact]
    public void SituacaoFisica_PelaSituacaoEPelasDatasPrevistas()
    {
        var referencia = D(2026, 6, 30);
        Assert.Equal("sem_registro", PeRegrasDoPainel.SituacaoFisica(false, null, null, null, referencia));
        Assert.Equal("cancelada", PeRegrasDoPainel.SituacaoFisica(true, "cancelada", D(2026, 1, 1), D(2026, 2, 1), referencia));
        Assert.Equal("concluida", PeRegrasDoPainel.SituacaoFisica(true, "concluida", D(2026, 1, 1), D(2026, 2, 1), referencia));
        Assert.Equal("atrasada", PeRegrasDoPainel.SituacaoFisica(true, "em_andamento", D(2026, 1, 1), D(2026, 6, 29), referencia));
        Assert.Equal("em_dia", PeRegrasDoPainel.SituacaoFisica(true, "em_andamento", D(2026, 1, 1), D(2026, 6, 30), referencia));
        // Não iniciada: atrasa pelo início previsto vencido
        Assert.Equal("atrasada", PeRegrasDoPainel.SituacaoFisica(true, "nao_iniciada", D(2026, 6, 1), D(2027, 1, 1), referencia));
        Assert.Equal("em_dia", PeRegrasDoPainel.SituacaoFisica(true, "nao_iniciada", D(2026, 7, 1), D(2027, 1, 1), referencia));
        // A referência é o fim do ciclo que já passou; senão, hoje
        Assert.Equal(D(2026, 3, 31), PeRegrasDoPainel.Referencia(new PeCiclo { Fim = D(2026, 3, 31) }, D(2026, 9, 1)));
        Assert.Equal(D(2026, 9, 1), PeRegrasDoPainel.Referencia(new PeCiclo { Fim = D(2026, 9, 30) }, D(2026, 9, 1)));
        Assert.Equal(D(2026, 9, 1), PeRegrasDoPainel.Referencia(null, D(2026, 9, 1)));
    }

    [Fact]
    public void Percentuais_DaAcaoEDaMeta_EOrcamentoQueNaoSeAplica()
    {
        Assert.Equal(100m, PeRegrasDoPainel.PercentualDaAcao(null, "concluida"));
        Assert.Null(PeRegrasDoPainel.PercentualDaAcao(null, "em_andamento"));
        Assert.Equal(35m, PeRegrasDoPainel.PercentualDaAcao(35m, "em_andamento"));

        // Média simples quando falta peso; ponderada quando todas têm; a cancelada fica de fora
        Assert.Equal(50m, PeRegrasDoPainel.PercentualDaMeta(new List<(decimal?, decimal?, bool)> { (20m, 70m, false), (80m, null, false), (0m, 10m, true) }));
        Assert.Equal(38m, PeRegrasDoPainel.PercentualDaMeta(new List<(decimal?, decimal?, bool)> { (20m, 70m, false), (80m, 30m, false) }));
        Assert.Null(PeRegrasDoPainel.PercentualDaMeta(new List<(decimal?, decimal?, bool)> { (null, 50m, false) }));

        Assert.True(PeRegrasDoPainel.OrcamentoNaoSeAplica(null, 0m));
        Assert.False(PeRegrasDoPainel.OrcamentoNaoSeAplica(0m, 10m));
    }
}
