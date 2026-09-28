using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Os ciclos do acompanhamento (E7, rodada B): os de monitoramento criados quando a lista é lida
/// (idempotente, pela periodicidade do passo 4.3 ou a padrão, com rótulo, prazo e situação), a
/// troca da periodicidade (só os ciclos sem registro e não fechados são recriados), a avaliação
/// intermediária aberta pela equipe (uma por vez), o fechamento com as pendências e o RA em
/// minuta, a reabertura, as rotas dos registros com o ciclo, o PDTIC que passa a em
/// acompanhamento no primeiro dado e a revisão, que não leva ciclos, dados dos ciclos nem os
/// relatórios.
/// </summary>
public class PeCiclosTest : PeAcompanhamentoTestBase
{
    private void FecharNoBanco(long cicloId)
    {
        var ciclo = Context.PeCiclos.Single(c => c.Id == cicloId);
        ciclo.Situacao = PeDominios.SituacaoCiclo.Fechado;
        ciclo.FechadoEm = DateTime.UtcNow;
        ciclo.FechadoPor = UserOrgaoSes.Email;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Lista_CriaOsCiclosDaVigencia_Idempotente_ComORotuloOPrazoEASituacao()
    {
        var id = await AcompanhadoAsync();

        var ciclos = await CiclosAsync(id);

        Assert.Equal(16, ciclos.Count);
        Assert.Equal(Enumerable.Range(1, 16), ciclos.Select(c => c.Numero));
        Assert.All(ciclos, c => Assert.Equal(PeDominios.TipoCiclo.Monitoramento, c.Tipo));
        Assert.Equal((Trimestre1, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 15)),
            (ciclos[0].Rotulo, ciclos[0].Inicio, ciclos[0].Fim!.Value, ciclos[0].Prazo!.Value));
        Assert.Equal(("2026 · 3º trimestre", new DateOnly(2026, 10, 15)), (ciclos[2].Rotulo, ciclos[2].Prazo!.Value));
        Assert.Equal(UltimoTrimestre, ciclos[^1].Rotulo);

        // Situação exibida: o 1º trimestre venceu (prazo em 15/04/2026); o último ainda não começou
        var hoje = PeCiclos.Hoje();
        Assert.All(ciclos, c => Assert.Equal(
            c.Inicio > hoje ? "futuro" : c.Prazo < hoje ? "atrasado" : "aberto", c.Situacao));
        Assert.Equal(PeDominios.SituacaoCicloExibida.Atrasado, ciclos[0].Situacao);
        Assert.Equal(PeDominios.SituacaoCicloExibida.Futuro, ciclos[^1].Situacao);
        Assert.True(ciclos[0].PodeEditar);
        Assert.False(ciclos[^1].PodeEditar);
        Assert.All(ciclos, c => Assert.Equal((0, 3, 0, 0), (c.Resumo.AcoesComSituacao, c.Resumo.TotalAcoes, c.Resumo.Medicoes, c.Resumo.RiscosOcorridos)));
        Assert.All(ciclos, c => Assert.Null(c.Relatorio));

        // Ler de novo não cria nada
        var deNovo = await CiclosAsync(id);
        Assert.Equal(ciclos.Select(c => c.Id), deNovo.Select(c => c.Id));
        Assert.Equal(16, Context.PeCiclos.Count(c => c.PdticId == id));

        // Quem vê o órgão lê (a consulta, a SGDI e a Secretaria do CGTIC), sem editar; outro órgão, 403
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic })
            Assert.All(await CiclosAsync(id, user: user), c => Assert.False(c.PodeEditar));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => CiclosAsync(id, user: UserOrgaoSeec)));
    }

    [Fact]
    public async Task Lista_PorTipo_AntesDaPublicacaoVazia_ESemAVersao6_409()
    {
        var id = await AcompanhadoAsync();
        var avaliacao = await AbrirAvaliacaoAsync(id);

        Assert.Equal(avaliacao.Id, Assert.Single(await CiclosAsync(id, "avaliacao")).Id);
        Assert.Equal(16, (await CiclosAsync(id, "monitoramento")).Count);
        Assert.Empty(await CiclosAsync(id, "trimestre"));
        var todos = await CiclosAsync(id);
        Assert.Equal(17, todos.Count);
        // As avaliações vêm depois do monitoramento
        Assert.Equal(avaliacao.Id, todos[^1].Id);

        // PDTIC em elaboração: nenhum ciclo
        var seec = await AbrirSeecAsync();
        Assert.Empty(await CiclosAsync(seec.Id, user: UserOrgaoSeec));

        // Antes de o carregador trazer a versão 6 (o intervalo da atualização): 409 com a mensagem
        VersaoDoModelo(5);
        var ex = await Assert.ThrowsAsync<ApiException>(() => CiclosAsync(id));
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), ex.Error.Code);
        Assert.Contains("acompanhamento do PDTIC está sendo preparado", ex.Error.Message);
    }

    [Fact]
    public async Task TrocaDaPeriodicidade_RecriaSoOsCiclosSemRegistroENaoFechados_ERenumera()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "em_andamento", 10));
        FecharNoBanco(t2.Id);

        await PlanoDeMonitoramentoAsync(id, "semestral");
        var ciclos = await CiclosAsync(id);

        // O 1º trimestre (com registro) e o 2º (fechado) ficam; os semestres cobrem o resto, sem sobrepor
        Assert.Equal(new[]
        {
            Trimestre1, Trimestre2, "2026 · 2º semestre", "2027 · 1º semestre", "2027 · 2º semestre", "2028 · 1º semestre", "2028 · 2º semestre",
            "2029 · 1º semestre", "2029 · 2º semestre"
        }, ciclos.Select(c => c.Rotulo));
        Assert.Equal(Enumerable.Range(1, 9), ciclos.Select(c => c.Numero));
        Assert.Equal((t1.Id, t2.Id), (ciclos[0].Id, ciclos[1].Id));
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 15)),
            (ciclos[2].Inicio, ciclos[2].Fim!.Value, ciclos[2].Prazo!.Value));
        Assert.Equal(9, Context.PeCiclos.Count(c => c.PdticId == id));

        // Voltar ao trimestral: os semestres sem registro saem de novo
        var periodicidade = (await Registros.ListarAsync(PeDono.DoPdtic(id), "periodicidade_monitoramento", await Orgao())).Registros.Single();
        await Registros.AtualizarAsync(PeDono.DoPdtic(id), "periodicidade_monitoramento", periodicidade.Id, Salvar(new { periodicidade = "trimestral" }), await Orgao());
        Assert.Equal(16, (await CiclosAsync(id)).Count);
    }

    [Fact]
    public async Task Avaliacao_AEquipeAbre_UmaAbertaPorVez_ComONomePadrao()
    {
        var id = await AcompanhadoAsync();

        var primeira = await AbrirAvaliacaoAsync(id);

        Assert.Equal((PeDominios.TipoCiclo.Avaliacao, 1, "Avaliação intermediária 1", PeCiclos.Hoje(), "aberto"),
            (primeira.Tipo, primeira.Numero, primeira.Rotulo, primeira.Inicio, primeira.Situacao));
        Assert.Null(primeira.Fim);
        Assert.Null(primeira.Prazo);
        Assert.True(primeira.PodeEditar);

        var ex = await Assert.ThrowsAsync<ApiException>(() => AbrirAvaliacaoAsync(id, "Outra"));
        Assert.Equal((Codigo(ErrorCode.PeAvaliacaoAberta), "A avaliação \"Avaliação intermediária 1\" ainda está aberta. Feche essa antes de abrir outra."),
            (ex.Error.Code, ex.Error.Message));

        // O monitoramento não se abre à mão; o tipo precisa vir; nome até 100 letras
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido),
            await ErroAsync(async () => await Acompanhamento.CriarCicloAsync(id, new PeCicloCriarDTO { Tipo = "monitoramento" }, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos),
            await ErroAsync(async () => await Acompanhamento.CriarCicloAsync(id, new PeCicloCriarDTO(), await Orgao())));
        FecharNoBanco(primeira.Id);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => AbrirAvaliacaoAsync(id, new string('a', 101))));

        // Só a equipe do órgão (e o admin geral) abre
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeAdmin, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => AbrirAvaliacaoAsync(id, user: user)));
        var segunda = await AbrirAvaliacaoAsync(id, "Avaliação de meio de vigência", UserAdminGeral);
        Assert.Equal((2, "Avaliação de meio de vigência"), (segunda.Numero, segunda.Rotulo));
    }

    [Fact]
    public async Task Avaliacao_SoNoPdticVigente_ENoNivelComOsPassos()
    {
        // Básico: a avaliação intermediária não está na trilha
        var basico = await AcompanhadoNoBasicoAsync();
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), await ErroAsync(() => AbrirAvaliacaoAsync(basico)));

        // Em elaboração: não abre
        await DefinirNivelDoOrgaoAsync(OrgaoSeec, "intermediario");
        var seec = await AbrirSeecAsync();
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(() => AbrirAvaliacaoAsync(seec.Id, user: UserOrgaoSeec)));
    }

    [Fact]
    public async Task Fechar_ComPendencias400_DepoisFecha_GeraORa_EReabre()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);

        var ex = await Assert.ThrowsAsync<PePendenciasException>(() => FecharAsync(t1.Id));
        Assert.Equal(Codigo(ErrorCode.PeCicloComPendencias), ex.Error.Code);
        Assert.Equal("Faltam 2 coisas para fechar o ciclo. Confira a lista.", ex.Error.Message);
        Assert.Equal("Registre a situação das ações A01, A02 e A03 neste ciclo.", ex.Pendencias[0].Motivo);
        Assert.Equal((await PassoDaSituacaoAsync(id, "monitoramento.ciclo-monitoramento")).Numero, ex.Pendencias[0].PassoNumero);
        Assert.Equal("Preencha \"Resumo do ciclo\".", ex.Pendencias[1].Motivo);
        Assert.Equal((await PassoDaSituacaoAsync(id, "monitoramento.relatorio-acompanhamento")).Numero, ex.Pendencias[1].PassoNumero);

        // Só a equipe fecha; ciclo que não começou, 409
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => FecharAsync(t1.Id, UserConsultaSes)));
        var ultimo = await CicloAsync(id, UltimoTrimestre);
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), await ErroAsync(() => FecharAsync(ultimo.Id)));

        await PreencherCicloAsync(id, t1.Id);
        var fechado = await FecharAsync(t1.Id);

        Assert.Equal((PeDominios.SituacaoCicloExibida.Fechado, UserOrgaoSes.Email), (fechado.Situacao, fechado.FechadoPor));
        Assert.NotNull(fechado.FechadoEm);
        Assert.False(fechado.PodeEditar);
        Assert.Equal((3, 3), (fechado.Resumo.AcoesComSituacao, fechado.Resumo.TotalAcoes));
        Assert.Equal((1, PeDominios.SituacaoVersaoDoc.Minuta), (fechado.Relatorio!.Numero, fechado.Relatorio.Situacao));
        // O RA é versão do relatório do ciclo, não do documento do PDTIC
        Assert.Single(await Documentos.VersoesAsync(PeDocAlvo.Ra(id, t1.Id), await Orgao()));
        Assert.Empty(await Documentos.VersoesAsync(id, await Orgao()));

        // Fechado: não recebe dados nem fecha de novo
        var dados = await Assert.ThrowsAsync<ApiException>(() => GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "concluida", 100)));
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), dados.Error.Code);
        Assert.StartsWith($"O ciclo {Trimestre1} foi fechado em ", dados.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), await ErroAsync(() => FecharAsync(t1.Id)));

        // Reabrir guarda quem e quando; o ciclo volta a receber dados (e aparece atrasado: o prazo passou)
        var reaberto = await Acompanhamento.ReabrirCicloAsync(t1.Id, await Orgao());
        Assert.Equal((PeDominios.SituacaoCicloExibida.Atrasado, UserOrgaoSes.Email), (reaberto.Situacao, reaberto.ReabertoPor));
        Assert.NotNull(reaberto.ReabertoEm);
        Assert.Null(reaberto.FechadoEm);
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), await ErroAsync(async () => await Acompanhamento.ReabrirCicloAsync(t1.Id, await Orgao())));
        await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "concluida", 100));

        // Fechar de novo gera o RA nº 2 do ciclo
        Assert.Equal(2, (await FecharAsync(t1.Id)).Relatorio!.Numero);
    }

    [Fact]
    public async Task Fechar_Avaliacao_PedeAsSecoesDaAvaliacao_OFimEHoje_EAbreAProxima()
    {
        var id = await AcompanhadoAsync();
        var avaliacao = await AbrirAvaliacaoAsync(id);

        var ex = await Assert.ThrowsAsync<PePendenciasException>(() => FecharAsync(avaliacao.Id));
        Assert.Equal(3, ex.Pendencias.Count);

        await IncluirNoPdticAsync(id, "resultados_intermediarios",
            new { valor_alcancado = "40%", data = "2026-09-01", situacao = "em_andamento" }, new { meta = new[] { IdDe(id, "M01") } }, cicloId: avaliacao.Id);
        await IncluirNoPdticAsync(id, "analise_intermediaria",
            new { parecer_execucao = "A execução está dentro do esperado.", ajustes_propostos = "Nenhum ajuste." }, cicloId: avaliacao.Id);
        await IncluirNoPdticAsync(id, "avaliacao_comite", new { decisao = "seguir", data = "2026-09-10" }, cicloId: avaliacao.Id);

        var fechada = await FecharAsync(avaliacao.Id);

        Assert.Equal((PeDominios.SituacaoCicloExibida.Fechado, PeCiclos.Hoje()), (fechada.Situacao, fechada.Fim!.Value));
        Assert.Equal(1, fechada.Relatorio!.Numero);
        Assert.Equal(2, (await AbrirAvaliacaoAsync(id)).Numero);
        // Reabrir a primeira com a segunda aberta: 409
        Assert.Equal(Codigo(ErrorCode.PeAvaliacaoAberta), await ErroAsync(async () => await Acompanhamento.ReabrirCicloAsync(avaliacao.Id, await Orgao())));
    }

    [Fact]
    public async Task Registros_NaSecaoPorCiclo_PedemOCicloDoTipo_EAsOutrasIgnoram()
    {
        var id = await AcompanhadoAsync();
        var dono = PeDono.DoPdtic(id);
        var ctx = await Orgao();
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        var avaliacao = await AbrirAvaliacaoAsync(id);

        // Sem o ciclo: 400; ciclo do outro tipo: 400; ciclo que não é do PDTIC: 404
        var ex = await Assert.ThrowsAsync<ApiException>(() => Registros.ListarAsync(dono, "relatorio_ciclo", ctx));
        Assert.Equal((Codigo(ErrorCode.PeCicloObrigatorio), "\"Resumo do ciclo\" é registrada a cada ciclo: escolha o ciclo (cicloId)."),
            (ex.Error.Code, ex.Error.Message));
        Assert.Equal(Codigo(ErrorCode.PeCicloObrigatorio), await ErroAsync(() => IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "x" })));
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido),
            await ErroAsync(() => IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "x" }, cicloId: avaliacao.Id)));
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido),
            await ErroAsync(() => IncluirNoPdticAsync(id, "avaliacao_comite", new { decisao = "seguir", data = "2026-09-10" }, cicloId: t1.Id)));
        Assert.Equal(Codigo(ErrorCode.PeCicloNaoEncontrado), await ErroAsync(() => Registros.ListarAsync(dono, "relatorio_ciclo", ctx, 999999)));

        // Cada ciclo tem o seu formulário
        var doT1 = await IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "Primeiro trimestre." }, cicloId: t1.Id);
        await IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "Segundo trimestre." }, cicloId: t2.Id);
        var lista = await Registros.ListarAsync(dono, "relatorio_ciclo", ctx, t1.Id);
        Assert.Equal(doT1.Id, Assert.Single(lista.Registros).Id);
        Assert.Equal(PeDominios.TipoCiclo.Monitoramento, lista.Secao.PorCiclo);
        Assert.True(lista.PodeEditar);
        Assert.Equal(Codigo(ErrorCode.PeFormularioJaPreenchido),
            await ErroAsync(() => IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "x" }, cicloId: t1.Id)));

        // O registro de um ciclo não é achado pelo outro; apagar tira também a parte com o ciclo
        Assert.Equal(Codigo(ErrorCode.PeRegistroNaoEncontrado),
            await ErroAsync(() => Registros.AtualizarAsync(dono, "relatorio_ciclo", doT1.Id, Salvar(new { resumo = "y" }), ctx, t2.Id)));
        await Registros.AtualizarAsync(dono, "relatorio_ciclo", doT1.Id, Salvar(new { resumo = "Primeiro trimestre, revisto." }), ctx, t1.Id);
        await Registros.ExcluirAsync(dono, "relatorio_ciclo", doT1.Id, ctx, t1.Id);
        Assert.Empty((await Registros.ListarAsync(dono, "relatorio_ciclo", ctx, t1.Id)).Registros);
        Assert.Empty(Context.PeRegistrosCiclo.AsNoTracking().Where(c => c.Id == doT1.Id));

        // Seção comum: o ciclo é ignorado
        Assert.Equal(3, (await Registros.ListarAsync(dono, "acoes", ctx, t1.Id)).Registros.Count);

        // Ciclo que ainda não começou: lê, mas não recebe dados
        var ultimo = await CicloAsync(id, UltimoTrimestre);
        var doUltimo = await Registros.ListarAsync(dono, "relatorio_ciclo", ctx, ultimo.Id);
        Assert.Empty(doUltimo.Registros);
        Assert.False(doUltimo.PodeEditar);
        var futuro = await Assert.ThrowsAsync<ApiException>(() => IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "x" }, cicloId: ultimo.Id));
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), futuro.Error.Code);
        Assert.StartsWith($"O ciclo {UltimoTrimestre} começa em 01/10/2029.", futuro.Error.Message);

        // A trilha diz as seções por ciclo
        var trilha = await TrilhaAsync(OrgaoSes);
        var secoes = trilha.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).ToDictionary(s => s.Chave, s => s.PorCiclo);
        Assert.Equal(("monitoramento", "avaliacao", (string?)null), (secoes["monitoramento_acoes"], secoes["avaliacao_comite"], secoes["acoes"]));
    }

    [Fact]
    public async Task PrimeiroDadoDoMonitoramento_PoeOPdticEmAcompanhamento()
    {
        var id = await AcompanhadoAsync();
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, PdticNoBanco(id).Situacao);

        var t1 = await CicloAsync(id, Trimestre1);
        await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "em_andamento", 10));

        Assert.Equal(PeDominios.SituacaoPdtic.EmAcompanhamento, (await Pdtics.ObterAsync(id, await Orgao())).Situacao);
        // Em acompanhamento, a lista continua criando e mostrando os ciclos
        Assert.Equal(16, (await CiclosAsync(id)).Count);
    }

    [Fact]
    public async Task Revisao_NaoLevaCiclosDadosDosCiclosNemOsRelatorios()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "em_andamento", 10));
        await IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "Resumo do ciclo." }, cicloId: t1.Id);
        await Documentos.SalvarTextoAsync(PeDocAlvo.Ra(id, t1.Id), BlocoDoModelo("introducao", documento: PeDominios.TipoDocumento.Ra).Id,
            Json(Rico("Introdução do relatório do ciclo.")), await Orgao());
        var avaliacao = await AbrirAvaliacaoAsync(id);
        await IncluirNoPdticAsync(id, "avaliacao_comite", new { decisao = "revisar", data = "2026-09-10" }, cicloId: avaliacao.Id);

        var nova = await Aprovacao.RevisarAsync(id, new PeRevisaoDTO { Justificativa = "O comitê pediu a revisão." }, await Orgao());

        Assert.Equal("1.1", nova.Versao);
        Assert.Empty(Context.PeCiclos.AsNoTracking().Where(c => c.PdticId == nova.Id));
        var porCiclo = new[] { "monitoramento_acoes", "relatorio_ciclo", "avaliacao_comite" }.Select(s => Secao(s).Id).ToList();
        Assert.Empty(Context.PeRegistros.AsNoTracking().Where(r => r.PdticId == nova.Id && porCiclo.Contains(r.SecaoId)));
        Assert.Empty(Context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == nova.Id));
        // O plano vai (as ações e as metas, com os mesmos códigos)
        Assert.Equal(new[] { "A01", "A02", "A03" },
            Context.PeRegistros.AsNoTracking().Where(r => r.PdticId == nova.Id && r.SecaoId == Secao("acoes").Id).OrderBy(r => r.Ordem).Select(r => r.Codigo));
        // A vigente fica com os ciclos, os dados e o texto do relatório
        Assert.Equal(17, Context.PeCiclos.AsNoTracking().Count(c => c.PdticId == id));
        Assert.Single(Context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == id));
    }
}
