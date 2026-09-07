using api.Pgia;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Questionário agrupado (decisão do dono do produto): cada grupo dos arts. 15/16/17
/// precisa de resposta explícita — incisos marcados XOR "Nenhuma das alternativas
/// acima" — e o grupo "Outros" recebe riscos declarados pelo órgão pela matriz da
/// CGDF. Nem o "nenhuma" nem os riscos declarados mexem no cálculo do risco.
/// </summary>
public class PgiaQuestionarioRiscoTest : PgiaTestBase
{
    private readonly PgiaSistemaService _service;
    private readonly PgiaPermissionService _permissionService;

    public PgiaQuestionarioRiscoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);

        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private void DesignarResponsavel(PgiaOrgao orgao, User agente)
    {
        Context.PgiaResponsaveisIa.Add(new PgiaResponsavelIa
        {
            OrgaoId = orgao.Id,
            AgenteId = agente.Id,
            AtoTipo = "Portaria",
            AtoNumero = "214/2026",
            AtoData = new DateOnly(2026, 7, 20),
            ProcessoSeiComunicacao = "00060-00012345/2026-11",
            DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
            InicioVigencia = new DateOnly(2026, 7, 20),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        Context.SaveChanges();
    }

    private async Task<PgiaUserContext> CtxAsync() =>
        (await _permissionService.GetContextAsync(UserOrgaoSes.Email))!;

    private static PgiaRiscoOutroDTO NovoRiscoDeclarado(
        string descricao = "Viés na priorização de atendimentos",
        string probabilidade = PgiaDominios.EscalaCgdf.Probabilidade.Possivel,
        string consequencia = PgiaDominios.EscalaCgdf.Consequencia.Moderada,
        string email = "responsavel@ses.df.gov.br") => new()
    {
        DescricaoRisco = descricao,
        AcaoMitigacao = "Revisão amostral mensal pela equipe de dados",
        ResponsavelNome = "Maria Andrade",
        ResponsavelEmail = email,
        Probabilidade = probabilidade,
        Consequencia = consequencia
    };

    private static PgiaClassificacaoCreateDTO NovaClassificacao(
        PgiaChecklistDTO? checklist = null, List<PgiaRiscoOutroDTO>? outros = null) => new()
    {
        Checklist = checklist ?? ChecklistRespondido(),
        OutrosRiscos = outros ?? new List<PgiaRiscoOutroDTO>(),
        Motivo = "Classificação inicial",
        DataClassificacao = new DateOnly(2026, 9, 1),
        Justificativa = "Enquadramento avaliado pelo Responsável de IA do órgão."
    };

    private static PgiaSistemaCreateDTO NovoSistema(
        PgiaClassificacaoCreateDTO classificacao, string denominacao = "Assistente virtual 156") => new()
    {
        Denominacao = denominacao,
        Finalidade = "Apoiar o atendimento ao cidadão na central 156",
        OrigemRegistro = "Nova iniciativa",
        TipoSistema = "Desenvolvido internamente",
        Tecnologia = "IA generativa",
        StatusCicloVida = "Planejamento",
        EscopoDados = "Somente dados públicos",
        AfetaCidadao = false,
        InteroperavelPadroesSgdi = true,
        // Condicionais da fase 1, para o fixture servir a qualquer nível de risco:
        // Alto exige supervisão humana descrita; Moderado exige o aviso de interação
        SupervisaoHumanaDescricao = "Revisão humana obrigatória antes de cada decisão",
        AvisoInteracaoIa = true,
        Classificacao = classificacao
    };

    private async Task<long> CriarSistemaAsync(
        PgiaClassificacaoCreateDTO classificacao, string denominacao = "Assistente virtual 156")
    {
        var sistema = await _service.CriarSistemaAsync(
            OrgaoSes.Id, NovoSistema(classificacao, denominacao), await CtxAsync());
        return sistema.Id;
    }

    // ── Completude por grupo ──────────────────────────────────────────────────

    /// <summary>
    /// Grupo respondido dos dois jeitos ao mesmo tempo: incisos marcados E
    /// "nenhuma das alternativas". Ambíguo — recusado em cada um dos três grupos.
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public async Task Completude_IncisosComNenhumaEhRejeitado(int artigo)
    {
        var checklist = ChecklistRespondido();
        if (artigo == 15) { checklist.Q15 = new List<string> { "I" }; checklist.Q15Nenhuma = true; }
        if (artigo == 16) { checklist.Q16 = new List<string> { "IV" }; checklist.Q16Nenhuma = true; }
        if (artigo == 17) { checklist.Q17 = new List<string> { "I" }; checklist.Q17Nenhuma = true; }

        var ex = await Assert.ThrowsAsync<ApiException>(() => CriarSistemaAsync(NovaClassificacao(checklist)));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
        Assert.Contains($"art. {artigo}", ex.Error.Message);
        Assert.Empty(await Context.PgiaSistemasIa.ToListAsync());
    }

    /// <summary>
    /// Grupo sem resposta nenhuma: nem incisos, nem "nenhuma das alternativas".
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public async Task Completude_GrupoSemRespostaEhRejeitado(int artigo)
    {
        var checklist = ChecklistRespondido();
        if (artigo == 15) checklist.Q15Nenhuma = false;
        if (artigo == 16) checklist.Q16Nenhuma = false;
        if (artigo == 17) checklist.Q17Nenhuma = false;

        var ex = await Assert.ThrowsAsync<ApiException>(() => CriarSistemaAsync(NovaClassificacao(checklist)));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
        Assert.Contains($"art. {artigo}", ex.Error.Message);
        Assert.Empty(await Context.PgiaSistemasIa.ToListAsync());
    }

    [Fact]
    public async Task Completude_QuestionarioTodoEmBrancoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(new PgiaChecklistDTO())));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
    }

    // ── Tudo "nenhuma" exige ao menos um risco declarado ──────────────────────

    /// <summary>
    /// Sistema sem risco algum não existe: com os três grupos em "nenhuma", o grupo
    /// "Outros" deixa de ser opcional.
    /// </summary>
    [Fact]
    public async Task TudoNenhuma_SemRiscoDeclaradoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido())));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
        Assert.Contains("descreva ao menos um risco", ex.Error.Message);
        Assert.Empty(await Context.PgiaSistemasIa.ToListAsync());
    }

    [Fact]
    public async Task TudoNenhuma_ComRiscoDeclaradoEhValidoEDaBaixoRisco()
    {
        var outros = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado("Dependência de fornecedor único") };
        var id = await CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), outros));

        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);
        Assert.Null(sistema.EnquadramentoLegal);

        var declarado = await Context.PgiaRiscosOutros.SingleAsync();
        Assert.Equal("Dependência de fornecedor único", declarado.DescricaoRisco);
    }

    [Fact]
    public async Task TudoNenhuma_ReclassificacaoSemRiscoDeclaradoEhRejeitada()
    {
        // Nasce com art. 17 marcado, então a classificação inicial dispensa "Outros"
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } })));
        var ctx = await CtxAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ReclassificarAsync(id, NovaClassificacao(ChecklistRespondido()), ctx));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
        Assert.Contains("descreva ao menos um risco", ex.Error.Message);
        // Segue valendo a classificação anterior
        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, sistema.ClassificacaoRiscoAtual);
        Assert.Single(await Context.PgiaClassificacoesRisco.Where(c => c.SistemaIaId == id).ToListAsync());
    }

    [Fact]
    public async Task TudoNenhuma_ReclassificacaoComRiscoDeclaradoEhAceita()
    {
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } })));
        var ctx = await CtxAsync();

        var nova = await _service.ReclassificarAsync(id, NovaClassificacao(
            ChecklistRespondido(),
            new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado("Risco residual monitorado") }), ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, nova.Resultado);
        Assert.Equal("Risco residual monitorado", nova.OutrosRiscos.Single().DescricaoRisco);

        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);
    }

    [Fact]
    public async Task Completude_MisturaDeIncisosENenhumaPorGrupoEhValida()
    {
        // Art. 16 marcado; arts. 15 e 17 respondidos com "nenhuma"
        var checklist = ChecklistRespondido(new PgiaChecklistDTO { Q16 = new List<string> { "IV" } });
        var id = await CriarSistemaAsync(NovaClassificacao(checklist));

        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, sistema.ClassificacaoRiscoAtual);
        Assert.Equal("art. 16, IV", sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task Completude_ValeTambemNaReclassificacao()
    {
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } })));

        var incompleto = ChecklistRespondido();
        incompleto.Q17Nenhuma = false;
        var ctx = await CtxAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ReclassificarAsync(id, NovaClassificacao(incompleto), ctx));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
        Assert.Contains("art. 17", ex.Error.Message);
        // A classificação vigente continua sendo a original
        Assert.Single(await Context.PgiaClassificacoesRisco.Where(c => c.SistemaIaId == id).ToListAsync());
    }

    // ── "Nenhuma" não pontua nem muda o resultado ─────────────────────────────

    [Fact]
    public async Task Nenhuma_NaoPontuaENaoMudaOResultado()
    {
        // Só art. 17, I (peso 2); os outros dois grupos respondidos com "nenhuma"
        var checklist = ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } });
        var id = await CriarSistemaAsync(NovaClassificacao(checklist));

        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, salva.Resultado);
        Assert.Equal(2, salva.Pontuacao); // art. 17, I = 2; os "nenhuma" somam zero
    }

    [Fact]
    public async Task Nenhuma_PersisteNoMesmoJsonbEVoltaNaResposta()
    {
        var checklist = ChecklistRespondido(new PgiaChecklistDTO { Q16 = new List<string> { "IV" } });
        var id = await CriarSistemaAsync(NovaClassificacao(checklist));

        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == id);
        Assert.Contains("\"q15_nenhuma\":true", salva.RespostasChecklist);
        Assert.Contains("\"q16_nenhuma\":false", salva.RespostasChecklist);
        Assert.Contains("\"q17_nenhuma\":true", salva.RespostasChecklist);

        var historico = await _service.ListarClassificacoesAsync(id, incluirPontuacao: false);
        var devolvida = Assert.Single(historico);
        Assert.True(devolvida.Checklist.Q15Nenhuma);
        Assert.False(devolvida.Checklist.Q16Nenhuma);
        Assert.True(devolvida.Checklist.Q17Nenhuma);
        Assert.Equal(new[] { "IV" }, devolvida.Checklist.Q16);
    }

    /// <summary>
    /// Linha antiga (jsonb sem os "nenhuma") desserializa com os bools em false,
    /// sem quebrar — compatibilidade para trás do mesmo jsonb.
    /// </summary>
    [Fact]
    public async Task Nenhuma_ChecklistAntigoSemOsCamposNaoQuebra()
    {
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } })));
        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == id);
        salva.RespostasChecklist = "{\"q15\":[],\"q16\":[],\"q17\":[\"I\"]}";
        await Context.SaveChangesAsync();

        var historico = await _service.ListarClassificacoesAsync(id, incluirPontuacao: false);
        var devolvida = Assert.Single(historico);

        Assert.Equal(new[] { "I" }, devolvida.Checklist.Q17);
        Assert.False(devolvida.Checklist.Q15Nenhuma);
        Assert.False(devolvida.Checklist.Q17Nenhuma);
    }

    // ── Grupo "Outros": riscos declarados pelo órgão ──────────────────────────

    [Fact]
    public async Task OutrosRiscos_GravamEVoltamComId()
    {
        var outros = new List<PgiaRiscoOutroDTO>
        {
            NovoRiscoDeclarado("Viés na priorização de atendimentos"),
            NovoRiscoDeclarado("Indisponibilidade do serviço em pico",
                PgiaDominios.EscalaCgdf.Probabilidade.Raro,
                PgiaDominios.EscalaCgdf.Consequencia.Menor)
        };

        var id = await CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), outros));

        var salvos = await Context.PgiaRiscosOutros.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, salvos.Count);
        Assert.All(salvos, r => Assert.Equal(UserOrgaoSes.Email, r.CriadoPor));
        Assert.Equal("Revisão amostral mensal pela equipe de dados", salvos[0].AcaoMitigacao);
        Assert.Equal(PgiaDominios.EscalaCgdf.Probabilidade.Possivel, salvos[0].Probabilidade);
        Assert.Equal(PgiaDominios.EscalaCgdf.Consequencia.Menor, salvos[1].Consequencia);

        // A classificação vigente devolve os riscos com Id (a tela pré-preenche por eles)
        var historico = await _service.ListarClassificacoesAsync(id, incluirPontuacao: false);
        var devolvida = Assert.Single(historico);
        Assert.Equal(2, devolvida.OutrosRiscos.Count);
        Assert.All(devolvida.OutrosRiscos, r => Assert.True(r.Id > 0));
        Assert.Equal("Viés na priorização de atendimentos", devolvida.OutrosRiscos[0].DescricaoRisco);
        Assert.Equal("Maria Andrade", devolvida.OutrosRiscos[0].ResponsavelNome);
        Assert.Equal("responsavel@ses.df.gov.br", devolvida.OutrosRiscos[0].ResponsavelEmail);
    }

    /// <summary>
    /// Lista vazia continua válida quando algum grupo dos arts. 15 a 17 foi marcado
    /// — o grupo "Outros" só é exigido quando os três ficam em "nenhuma".
    /// </summary>
    [Fact]
    public async Task OutrosRiscos_ListaVaziaEhValidaComGrupoMarcado()
    {
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } }),
            new List<PgiaRiscoOutroDTO>()));

        Assert.Empty(await Context.PgiaRiscosOutros.ToListAsync());
        var historico = await _service.ListarClassificacoesAsync(id, incluirPontuacao: false);
        Assert.Empty(Assert.Single(historico).OutrosRiscos);
    }

    [Fact]
    public async Task OutrosRiscos_NaoEntramNoCalculoNemNaPontuacao()
    {
        // Checklist todo "nenhuma" (Baixo Risco) com riscos declarados no grupo Outros
        var outros = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado(), NovoRiscoDeclarado("Outro risco") };
        var id = await CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), outros));

        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == id);
        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == id);

        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);
        Assert.Null(sistema.EnquadramentoLegal);
        Assert.Equal(0, salva.Pontuacao); // riscos declarados não pontuam
        Assert.Equal(2, await Context.PgiaRiscosOutros.CountAsync());
    }

    [Theory]
    [InlineData("Provável", "Moderada")]      // probabilidade fora do subconjunto
    [InlineData("Quase certo", "Moderada")]
    [InlineData("Possível", "Maior")]         // consequência fora do subconjunto
    [InlineData("Possível", "Catastrófica")]
    [InlineData("Altíssima", "Moderada")]     // fora até da escala da CGDF
    [InlineData("Possível", "")]
    public async Task OutrosRiscos_EscalaForaDoSubconjuntoEhRejeitada(string probabilidade, string consequencia)
    {
        var outros = new List<PgiaRiscoOutroDTO>
        {
            NovoRiscoDeclarado(probabilidade: probabilidade, consequencia: consequencia)
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), outros)));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Empty(await Context.PgiaRiscosOutros.ToListAsync());
        Assert.Empty(await Context.PgiaSistemasIa.ToListAsync());
    }

    /// <summary>
    /// A escala completa da CGDF fica registrada como constante (com pesos 1..5),
    /// mas neste grupo só vale o subconjunto de graus baixos a médios.
    /// </summary>
    [Fact]
    public void OutrosRiscos_EscalaCgdfCompletaERestricaoRegistradas()
    {
        Assert.Equal(5, PgiaDominios.EscalaCgdf.Probabilidade.Todos.Length);
        Assert.Equal(5, PgiaDominios.EscalaCgdf.Consequencia.Todos.Length);
        Assert.Equal(5, PgiaDominios.EscalaCgdf.Probabilidade.Pesos[PgiaDominios.EscalaCgdf.Probabilidade.QuaseCerto]);
        Assert.Equal(1, PgiaDominios.EscalaCgdf.Consequencia.Pesos[PgiaDominios.EscalaCgdf.Consequencia.Desprezivel]);

        Assert.Equal(
            new[] { "Improvável", "Raro", "Possível" },
            PgiaDominios.EscalaCgdf.Probabilidade.Permitidos);
        Assert.Equal(
            new[] { "Desprezível", "Menor", "Moderada" },
            PgiaDominios.EscalaCgdf.Consequencia.Permitidos);
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("sem-ponto@dominio")]
    [InlineData("")]
    [InlineData("Apelido <a@b.df.gov.br>")]
    public async Task OutrosRiscos_EmailDoResponsavelInvalidoEhRejeitado(string email)
    {
        var outros = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado(email: email) };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), outros)));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Empty(await Context.PgiaRiscosOutros.ToListAsync());
    }

    [Fact]
    public async Task OutrosRiscos_DescricaoOuMitigacaoVaziaEhRejeitada()
    {
        var semDescricao = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado(descricao: "   ") };
        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), semDescricao)));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex1.Error.Code);

        var semMitigacao = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado() };
        semMitigacao[0].AcaoMitigacao = "  ";
        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), semMitigacao)));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex2.Error.Code);

        var semResponsavel = new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado() };
        semResponsavel[0].ResponsavelNome = "";
        var ex3 = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSistemaAsync(NovaClassificacao(ChecklistRespondido(), semResponsavel)));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex3.Error.Code);
    }

    [Fact]
    public async Task OutrosRiscos_ReclassificacaoGravaOsRiscosDaNovaClassificacao()
    {
        var id = await CriarSistemaAsync(NovaClassificacao(
            ChecklistRespondido(), new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado("Risco da classificação inicial") }));

        await _service.ReclassificarAsync(id, NovaClassificacao(
            ChecklistRespondido(new PgiaChecklistDTO { Q17 = new List<string> { "I" } }),
            new List<PgiaRiscoOutroDTO> { NovoRiscoDeclarado("Risco da reclassificação") }), await CtxAsync());

        // Cada classificação carrega os seus: o histórico do art. 14 fica preservado
        var historico = await _service.ListarClassificacoesAsync(id, incluirPontuacao: false);
        Assert.Equal(2, historico.Count);
        Assert.Equal("Risco da reclassificação", historico[0].OutrosRiscos.Single().DescricaoRisco);
        Assert.Equal("Risco da classificação inicial", historico[1].OutrosRiscos.Single().DescricaoRisco);
        Assert.Equal(2, await Context.PgiaRiscosOutros.CountAsync());
    }
}
