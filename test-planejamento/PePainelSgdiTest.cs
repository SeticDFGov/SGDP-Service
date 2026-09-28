using System.Diagnostics;
using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;
using Xunit.Abstractions;

namespace test.planejamento;

/// <summary>
/// O painel da SGDI (E8): os órgãos pela situação do PDTIC de referência (cada situação, e sem
/// PDTIC), os em elaboração pela etapa do próximo passo, os órgãos por nível, os filtros (nível e
/// situação; fora do domínio, nenhum órgão), as necessidades e metas por objetivo do PETIC-DF
/// (também a ligação de uma versão anterior, pelo código), as ações por tema, os riscos por nível,
/// a execução no último ciclo com dado, os alertas, quem vê, que nada é gravado e que a situação de
/// todos os órgãos calculada de uma vez é igual à de cada um.
/// </summary>
public class PePainelSgdiTest : PePaineisTestBase
{
    private readonly ITestOutputHelper _saida;

    public PePainelSgdiTest(ITestOutputHelper saida)
    {
        _saida = saida;
    }

    private void Datas(long pdticId, DateOnly? vigenciaInicio = null, DateOnly? vigenciaFim = null, DateTime? aprovadoEm = null, DateTime? publicadoEm = null)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        if (vigenciaInicio != null) pdtic.VigenciaInicio = vigenciaInicio;
        if (vigenciaFim != null) pdtic.VigenciaFim = vigenciaFim;
        if (aprovadoEm != null) pdtic.AprovadoEm = aprovadoEm;
        if (publicadoEm != null) pdtic.PublicadoEm = publicadoEm;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Um órgão em cada situação: SEAGRI (sem PDTIC), SEDUH (em elaboração, vazio), SEJUS (em
    /// elaboração, com a etapa 1 feita), SEMA (em aprovação, com a deliberação aguardando), SEL
    /// (devolvido), SEFAZ (aprovado), PCDF (publicado, com a vigência vencida e a aprovação de 2023),
    /// CBMDF (em acompanhamento desde 10/01/2026, no Avançado, com os trimestres vencidos abertos) e
    /// DETRAN (encerrado); mais a SES e a SEEC, sem PDTIC.
    /// </summary>
    private async Task<Dictionary<string, PgiaOrgao>> CenarioAsync()
    {
        var orgaos = new[] { "SEAGRI", "SEDUH", "SEJUS", "SEMA", "SEL", "SEFAZ", "PCDF", "CBMDF", "DETRAN" }.ToDictionary(s => s, s => NovoOrgao(s));
        var hoje = HojeData();

        await AbrirPdticAsync(orgaos["SEDUH"]);
        var sejus = await AbrirPdticAsync(orgaos["SEJUS"]);
        await PreencherEtapa1Async(sejus);

        var sema = await AbrirPdticAsync(orgaos["SEMA"]);
        Situacao(sema, PeDominios.SituacaoPdtic.EmAprovacao);
        DeliberacaoAguardandoNoBanco(sema);
        Situacao(await AbrirPdticAsync(orgaos["SEL"]), PeDominios.SituacaoPdtic.Devolvido);
        Situacao(await AbrirPdticAsync(orgaos["SEFAZ"]), PeDominios.SituacaoPdtic.Aprovado);

        var pcdf = await AbrirPdticAsync(orgaos["PCDF"]);
        Datas(pcdf, new DateOnly(2023, 1, 1), hoje.AddDays(-10), new DateTime(2023, 2, 1, 15, 0, 0, DateTimeKind.Utc));
        Situacao(pcdf, PeDominios.SituacaoPdtic.Publicado);

        await DefinirNivelDoOrgaoAsync(orgaos["CBMDF"], "avancado");
        var cbmdf = await AbrirPdticAsync(orgaos["CBMDF"]);
        Datas(cbmdf, new DateOnly(2026, 1, 1), new DateOnly(2029, 12, 31), DateTime.UtcNow);
        PublicarEm(cbmdf, Publicacao, PeDominios.SituacaoPdtic.EmAcompanhamento);

        Situacao(await AbrirPdticAsync(orgaos["DETRAN"]), PeDominios.SituacaoPdtic.Encerrado);

        InadimplenciaNoBanco(orgaos["SEAGRI"], PeDominios.SituacaoInadimplencia.Inadimplente);
        InadimplenciaNoBanco(orgaos["SEDUH"], PeDominios.SituacaoInadimplencia.Notificado);
        InadimplenciaNoBanco(orgaos["SEL"], PeDominios.SituacaoInadimplencia.Saneado);
        return orgaos;
    }

    [Fact]
    public async Task PorSituacao_EmElaboracaoPorEtapa_PorNivel_EAlertas()
    {
        await CenarioAsync();

        var painel = await PainelAsync();

        Assert.Equal(11, painel.TotalOrgaos);
        Assert.Equal(new[] { "sem_pdtic", "em_elaboracao", "em_aprovacao", "devolvido", "aprovado", "publicado", "em_acompanhamento", "encerrado" },
            painel.PorSituacao.Select(s => s.Chave));
        Assert.Equal(new[] { 3, 2, 1, 1, 1, 1, 1, 1 }, painel.PorSituacao.Select(s => s.Quantidade));
        Assert.Equal(new[] { "Sem PDTIC", "Em elaboração", "Em aprovação", "Devolvido pelo CGTIC", "Aprovado", "Publicado", "Em acompanhamento", "Encerrado" },
            painel.PorSituacao.Select(s => s.Rotulo));

        // SEDUH no 1.1 (etapa 1) e SEJUS no 2.1 (etapa 2); as três etapas da elaboração sempre aparecem
        Assert.Equal(new[] { (1, "Prepare o PDTIC", 1), (2, "Faça o diagnóstico", 1), (3, "Planeje", 0) },
            painel.EmElaboracaoPorEtapa.Select(e => (e.Etapa, e.Titulo, e.Quantidade)));

        Assert.Equal(new[] { ("Básico", 10), ("Intermediário", 0), ("Avançado", 1) }, painel.PorNivel.Select(n => (n.Nome, n.Quantidade)));

        // PCDF com a vigência vencida e a revisão de 2023 vencida; CBMDF com os trimestres de 2026 vencidos e abertos
        Assert.Equal((1, 1, 1, 1, 1), (painel.Alertas.VigenciaVencida, painel.Alertas.RevisaoVencida, painel.Alertas.CicloAtrasado,
            painel.Alertas.Inadimplentes, painel.Alertas.DeliberacoesAguardando));

        Assert.Equal(new[] { "Básico", "Intermediário", "Avançado" }, painel.Filtros.Niveis.Select(n => n.Nome));
        Assert.Equal(PeDominios.SituacaoPainel.Todas, painel.Filtros.Situacoes.Select(s => s.Chave));
        Assert.True(painel.GeradoEm <= DateTime.UtcNow);
        // Sem PETIC-DF vigente, nenhum objetivo
        Assert.Empty(painel.PorObjetivoPetic);
    }

    [Fact]
    public async Task Filtros_PorNivelEPorSituacao_ForaDoDominioNenhum()
    {
        await CenarioAsync();

        var avancado = await PainelAsync(nivelId: NivelId("avancado"));
        Assert.Equal(1, avancado.TotalOrgaos);
        Assert.Equal(1, Quantidade(avancado, "em_acompanhamento"));
        Assert.Equal(1, avancado.Alertas.CicloAtrasado);
        Assert.Equal(0, avancado.Alertas.Inadimplentes);

        var elaboracao = await PainelAsync(situacao: "em_elaboracao");
        Assert.Equal(2, elaboracao.TotalOrgaos);
        Assert.Equal(new[] { 0, 2, 0, 0, 0, 0, 0, 0 }, elaboracao.PorSituacao.Select(s => s.Quantidade));
        Assert.Equal(new[] { 1, 1, 0 }, elaboracao.EmElaboracaoPorEtapa.Select(e => e.Quantidade));

        var semPdtic = await PainelAsync(situacao: "sem_pdtic");
        Assert.Equal((3, 1), (semPdtic.TotalOrgaos, semPdtic.Alertas.Inadimplentes));
        Assert.Equal(1, (await PainelAsync(situacao: "em_aprovacao")).Alertas.DeliberacoesAguardando);

        foreach (var vazio in new[] { await PainelAsync(situacao: "vencido"), await PainelAsync(nivelId: 999999) })
        {
            Assert.Equal(0, vazio.TotalOrgaos);
            Assert.All(vazio.PorSituacao, s => Assert.Equal(0, s.Quantidade));
            // As opções dos filtros continuam
            Assert.Equal(8, vazio.Filtros.Situacoes.Count);
        }
    }

    [Fact]
    public async Task Painel_SoLeNadaGrava_EQuemVe()
    {
        await CenarioAsync();
        var antes = (Context.PeCiclos.Count(), Context.PeRegistros.Count(), Context.PeInadimplencias.Count(), Context.PePdtics.Count());

        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserPeCgtic, UserAdminGeral })
            Assert.Equal(11, (await PainelAsync(user: user)).TotalOrgaos);
        await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), await Sgdi());

        // O CBMDF tem ciclos atrasados pelo plano, mas nenhuma linha foi gravada
        Assert.Equal(antes, (Context.PeCiclos.Count(), Context.PeRegistros.Count(), Context.PeInadimplencias.Count(), Context.PePdtics.Count()));
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserSemPapel })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => PainelAsync(user: user)));
    }

    [Fact]
    public async Task PorObjetivoPetic_NecessidadesEMetas_TambemDaVersaoAnteriorPeloCodigo()
    {
        var (_, objetivo) = await VigenteAsync();
        var pdtic = await AbrirSesAsync();
        var necessidade = await IncluirNoPdticAsync(pdtic.Id, "necessidades", new { descricao = "Portal de serviços.", tipo = "servico", prioridade_simples = "alta" },
            new { objetivo_petic = new[] { objetivo.Id } });
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", new { descricao = "Trocar os monitores.", tipo = "infraestrutura", prioridade_simples = "baixa" });
        await IncluirNoPdticAsync(pdtic.Id, "metas", new { descricao = "Portal no ar.", indicador = "Serviços no portal", valor = "50", prazo = "2027-12-31" },
            new { necessidades = new[] { necessidade.Id }, objetivo_petic = new[] { objetivo.Id } });

        var painel = await PainelAsync();
        var oe01 = Assert.Single(painel.PorObjetivoPetic);
        Assert.Equal((objetivo.Id, "OE01", "Ampliar os serviços digitais.", 1, 1), (oe01.RegistroId, oe01.Codigo, oe01.Texto, oe01.Necessidades, oe01.Metas));

        // A versão 2.0 aprovada copia os objetivos com os mesmos códigos; as ligações do PDTIC
        // continuam no OE01 da 1.0 e contam no OE01 da 2.0, pelo código
        var rascunho = await RascunhoAsync("PETIC-DF 2027-2030");
        await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Integrar os dados do DF." });
        await AprovarAsync(rascunho.Id);
        var novo = await PainelAsync();
        Assert.Equal(new[] { ("OE01", 1, 1), ("OE02", 0, 0) }, novo.PorObjetivoPetic.Select(o => (o.Codigo!, o.Necessidades, o.Metas)));
        Assert.NotEqual(objetivo.Id, novo.PorObjetivoPetic[0].RegistroId);
    }

    /// <summary>
    /// O PDTIC completo da SES (Avançado) em acompanhamento: no 1º trimestre, A01 em andamento, A02
    /// não iniciada e A03 cancelada; no 2º, A01 concluída. A SEEC (Básico), publicada, com uma ação sem registro.
    /// </summary>
    private async Task AcompanhamentoDeDoisOrgaosAsync()
    {
        var id = await AcompanhadoAsync();
        var (a1, a2, a3) = (IdDe(id, "A01"), IdDe(id, "A02"), IdDe(id, "A03"));
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 30), Linha(a2, "nao_iniciada", 0), Linha(a3, "cancelada", 0));
        await GravarAcoesAsync(t2.Id, Linha(a1, "concluida", 100));

        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(seec.Id, "acoes", new { descricao = "Implantar o protocolo digital.", tema = new[] { "transformacao_digital", "governanca_dados" },
            responsavel = "TIC", conclusao = "2027-06-30", situacao = "nao_iniciada" }, user: UserOrgaoSeec);
        PublicarEm(seec.Id, DateTime.UtcNow);
    }

    [Fact]
    public async Task AcoesPorTema_RiscosPorNivel_EExecucaoNoUltimoCiclo()
    {
        await AcompanhamentoDeDoisOrgaosAsync();

        var painel = await PainelAsync();

        Assert.Equal(new[] { ("seguranca", 1), ("transformacao_digital", 3), ("governanca_dados", 1), ("infraestrutura", 0), ("sistemas", 1), ("pessoas", 0), ("outro", 0) },
            painel.AcoesPorTema.Select(t => (t.Valor, t.Quantidade)));
        Assert.All(painel.AcoesPorTema, t => Assert.False(string.IsNullOrWhiteSpace(t.Rotulo)));

        // Os dois riscos da SES são de nível alto (probabilidade média e impacto alto); a SEEC, no Básico, não tem a seção dos riscos
        Assert.Equal(new[] { ("alto", 2), ("medio", 0), ("baixo", 0), ("sem_nivel", 0) }, painel.RiscosPorNivel.Select(r => (r.Nivel, r.Quantidade)));
        Assert.Equal("Sem nível", painel.RiscosPorNivel[3].Rotulo);

        // SES pelo 2º trimestre (A01 concluída; A02 e A03 pelo 1º); a ação da SEEC sem registro
        Assert.Equal(new[] { ("nao_iniciada", 1), ("em_andamento", 0), ("concluida", 1), ("cancelada", 1), ("sem_registro", 1) },
            painel.ExecucaoUltimoCiclo.Select(e => (e.Situacao, e.Quantidade)));
        Assert.Equal("Sem registro", painel.ExecucaoUltimoCiclo[4].Rotulo);
        // A SES entrou em acompanhamento com o primeiro dado do monitoramento; a SEEC está publicada
        Assert.Equal((1, 1, 0), (Quantidade(painel, "em_acompanhamento"), Quantidade(painel, "publicado"), Quantidade(painel, "sem_pdtic")));

        // O filtro vale também para os gráficos
        var basico = await PainelAsync(nivelId: NivelId("basico"));
        Assert.Equal(new[] { ("nao_iniciada", 0), ("em_andamento", 0), ("concluida", 0), ("cancelada", 0), ("sem_registro", 1) },
            basico.ExecucaoUltimoCiclo.Select(e => (e.Situacao, e.Quantidade)));
        Assert.Equal(1, basico.AcoesPorTema.Single(t => t.Valor == "governanca_dados").Quantidade);
    }

    [Fact]
    public async Task SituacaoDeTodos_DeUmaVez_IgualADeCadaUm()
    {
        var ses = await AcompanhadoAsync();
        var t1 = await CicloAsync(ses, Trimestre1);
        await PreencherCicloAsync(ses, t1.Id);
        await FecharAsync(t1.Id);
        await AbrirAvaliacaoAsync(ses);
        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(seec.Id, "abrangencia", new { tipo_abrangencia = "todo_com_vinculadas", vigencia_inicio = "2026-01-01", vigencia_fim = "2027-12-31", periodicidade_revisao = "anual" },
            user: UserOrgaoSeec);
        await Comentarios.CriarAsync(seec.Id, new PeComentarioCriarDTO { PassoId = Passo("preparacao.nomes").Id, Texto = "Preencha o dicionário." }, await Sgdi());
        var extra = NovoOrgao("SEDES");
        await AbrirPdticAsync(extra);

        var base_ = await PeBaseDosPaineis.CarregarAsync(Context, await PeBaseDosPaineis.OrgaosAtivosAsync(Context), comSituacao: true);
        var sgdi = await Sgdi();
        foreach (var retrato in base_.Orgaos.Where(r => r.EmVigor != null))
        {
            var sozinho = await Pdtics.SituacaoAsync(retrato.EmVigor!.Id, sgdi);
            Assert.Equal(JsonSerializer.Serialize(sozinho), JsonSerializer.Serialize(retrato.Situacao!.Resposta));
        }
        Assert.Equal(3, base_.Orgaos.Count(r => r.Situacao != null));
    }

    [Fact]
    public async Task Carga_VinteEcincoOrgaos_PoucasConsultasNoTotal()
    {
        for (var i = 1; i <= 25; i++)
        {
            var orgao = NovoOrgao($"ORG{i:00}");
            if (i % 5 == 0) continue;
            var id = await AbrirPdticAsync(orgao);
            await IncluirNoPdticAsync(id, "abrangencia", new { tipo_abrangencia = "todo_com_vinculadas", vigencia_inicio = "2026-01-01", vigencia_fim = "2029-12-31", periodicidade_revisao = "anual" },
                user: UserAdminGeral);
            await IncluirNoPdticAsync(id, "ativos", new { nome = $"Sistema {i}", tipo = "sistema", situacao = "em_operacao" }, user: UserAdminGeral);
            if (i % 3 == 0) Situacao(id, PeDominios.SituacaoPdtic.Publicado);
        }
        var sgdi = await Sgdi();

        var relogio = Stopwatch.StartNew();
        var painel = await Paineis.PainelAsync(new PePainelConsulta(), sgdi);
        var tempoPainel = relogio.ElapsedMilliseconds;
        relogio.Restart();
        var conformidade = await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), sgdi);
        var tempoConformidade = relogio.ElapsedMilliseconds;
        _saida.WriteLine($"Painel com {painel.TotalOrgaos} órgãos: {tempoPainel} ms; conformidade: {tempoConformidade} ms (InMemory).");

        Assert.Equal(27, painel.TotalOrgaos);
        Assert.Equal(27, conformidade.Orgaos.Count);
        Assert.Equal(2 + 5, Quantidade(painel, "sem_pdtic"));
        Assert.Equal(7, Quantidade(painel, "publicado"));
        Assert.Equal(13, Quantidade(painel, "em_elaboracao"));
    }
}
