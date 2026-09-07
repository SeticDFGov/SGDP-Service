using api.Pgia;
using app.Models;
using Models.Pgia;
using Repositorio.Pgia;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Histórico de decisões do CGTIC (relatório de auditoria): casos delegados ao
/// comitê (Alto Risco e Risco Excessivo), contagens e demais deliberações.
/// </summary>
public class PgiaCgticHistoricoTest : PgiaTestBase
{
    private readonly PgiaGovernancaService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaPermissionService _permissionService;

    public PgiaCgticHistoricoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));
        _sistemaService = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);

        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        DesignarResponsavel(OrgaoSeec, UserOrgaoSeec);
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

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email, PerfilDe(email)))!;

    /// <summary>
    /// Cria um sistema com o checklist informado. Sem checklist o resultado é
    /// Baixo Risco (rota da SGDI, fora da pauta do comitê).
    /// </summary>
    private async Task<PgiaSistemaResponse> NovoSistemaAsync(
        PgiaOrgao orgao, User responsavel, string denominacao,
        DateOnly dataClassificacao, PgiaChecklistDTO? checklist = null)
    {
        var ctx = await CtxAsync(responsavel.Email);
        return await _sistemaService.CriarSistemaAsync(orgao.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar o serviço público",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA preditiva ou aprendizado de máquina",
            StatusCicloVida = "Desenvolvimento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            SupervisaoHumanaDescricao = "Servidor revisa cada decisão.",
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = checklist ?? new PgiaChecklistDTO(),
                Motivo = "Classificação inicial",
                DataClassificacao = dataClassificacao,
                Justificativa = "Enquadramento conforme os arts. 15 a 17."
            }
        }, ctx);
    }

    private Task<PgiaSistemaResponse> NovoAltoRiscoAsync(
        PgiaOrgao orgao, User responsavel, string denominacao, DateOnly dataClassificacao) =>
        NovoSistemaAsync(orgao, responsavel, denominacao, dataClassificacao,
            new PgiaChecklistDTO { Q16 = new List<string> { "IV" } });

    private Task<PgiaSistemaResponse> NovoRiscoExcessivoAsync(
        PgiaOrgao orgao, User responsavel, string denominacao, DateOnly dataClassificacao) =>
        NovoSistemaAsync(orgao, responsavel, denominacao, dataClassificacao,
            new PgiaChecklistDTO { Q15 = new List<string> { "I" } });

    private async Task<PgiaDeliberacaoResponse> DeliberarAsync(
        long? sistemaId, string resultado, string tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
        int dia = 5)
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        return await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = tipo,
            SistemaIaId = sistemaId,
            DataDeliberacao = new DateOnly(2026, 10, dia),
            Resultado = resultado,
            NumeroAto = $"{dia}/2026"
        }, cgticCtx);
    }

    /// <summary>
    /// Pauta usada nos testes de conteúdo: 4 casos (1 aprovado, 1 negado e 2
    /// pendentes, um deles já com deliberação em diligência), 1 sistema fora da
    /// rota do comitê e 2 deliberações sem caso associado.
    /// </summary>
    private async Task<PgiaCgticHistoricoResponse> MontarPautaAsync()
    {
        var aprovado = await NovoAltoRiscoAsync(OrgaoSes, UserOrgaoSes, "Triagem clínica", new DateOnly(2026, 9, 1));
        var negado = await NovoRiscoExcessivoAsync(OrgaoSes, UserOrgaoSes, "Reconhecimento facial", new DateOnly(2026, 9, 10));
        await NovoAltoRiscoAsync(OrgaoSes, UserOrgaoSes, "Priorização de leitos", new DateOnly(2026, 9, 5));
        var emDiligencia = await NovoAltoRiscoAsync(OrgaoSeec, UserOrgaoSeec, "Análise de crédito", new DateOnly(2026, 9, 20));

        // Fora da pauta do comitê: Baixo Risco vai à SGDI
        var baixoRisco = await NovoSistemaAsync(OrgaoSes, UserOrgaoSes, "Agenda inteligente", new DateOnly(2026, 9, 2));

        await DeliberarAsync(aprovado.Id, PgiaDominios.ResultadoDeliberacao.Favoravel, dia: 5);
        await DeliberarAsync(negado.Id, PgiaDominios.ResultadoDeliberacao.Desfavoravel, dia: 6);
        await DeliberarAsync(emDiligencia.Id, PgiaDominios.ResultadoDeliberacao.EmDiligencia, dia: 7);

        // Deliberações sem caso associado: sobre sistema fora da rota e sem sistema
        await DeliberarAsync(baixoRisco.Id, PgiaDominios.ResultadoDeliberacao.Favoravel,
            tipo: "Suspensão de sistema", dia: 8);
        await DeliberarAsync(null, PgiaDominios.ResultadoDeliberacao.Favoravel,
            tipo: "Aprovação do Guia de Contratações", dia: 9);

        return await _service.ObterHistoricoCgticAsync();
    }

    // ── Contagens e classificação dos casos ───────────────────────────────────

    [Fact]
    public async Task Historico_ContaEClassificaCadaCaso()
    {
        var historico = await MontarPautaAsync();

        Assert.Equal(4, historico.Contagens.Total);
        Assert.Equal(2, historico.Contagens.Pendentes);
        Assert.Equal(1, historico.Contagens.Aprovadas);
        Assert.Equal(1, historico.Contagens.Negadas);
        Assert.Equal(4, historico.Casos.Count);

        var aprovado = historico.Casos.Single(c => c.Denominacao == "Triagem clínica");
        Assert.Equal(PgiaCgticSituacaoCaso.Aprovada, aprovado.Situacao);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.Aprovado, aprovado.SituacaoHomologacao);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, aprovado.ClassificacaoRiscoAtual);
        Assert.Equal("SES", aprovado.OrgaoSigla);
        Assert.Equal(OrgaoSes.Nome, aprovado.OrgaoNome);
        // Entrada na pauta = data da classificação vigente que delegou ao comitê
        Assert.Equal(new DateOnly(2026, 9, 1), aprovado.DataEntrada);

        var negado = historico.Casos.Single(c => c.Denominacao == "Reconhecimento facial");
        Assert.Equal(PgiaCgticSituacaoCaso.Negada, negado.Situacao);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.Vetado, negado.SituacaoHomologacao);
        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, negado.ClassificacaoRiscoAtual);

        var pendente = historico.Casos.Single(c => c.Denominacao == "Priorização de leitos");
        Assert.Equal(PgiaCgticSituacaoCaso.Pendente, pendente.Situacao);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, pendente.SituacaoHomologacao);
        Assert.Empty(pendente.Deliberacoes);

        // Baixo Risco corre pela SGDI: não é caso do comitê
        Assert.DoesNotContain(historico.Casos, c => c.Denominacao == "Agenda inteligente");
    }

    [Fact]
    public async Task Historico_OrdenaPendentesPrimeiroEDepoisPorEntradaDesc()
    {
        var historico = await MontarPautaAsync();

        Assert.Collection(historico.Casos,
            c => Assert.Equal("Análise de crédito", c.Denominacao),      // pendente, 20/09
            c => Assert.Equal("Priorização de leitos", c.Denominacao),   // pendente, 05/09
            c => Assert.Equal("Reconhecimento facial", c.Denominacao),   // decidido, 10/09
            c => Assert.Equal("Triagem clínica", c.Denominacao));        // decidido, 01/09
    }

    // ── Analisadas ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Historico_AnalisadasContaSoOsCasosComDeliberacao()
    {
        var historico = await MontarPautaAsync();

        // Aprovado, negado e o que está em diligência; o caso sem deliberação fica de fora
        Assert.Equal(3, historico.Contagens.Analisadas);
        Assert.Equal(3, historico.Casos.Count(c => c.Deliberacoes.Count > 0));

        // Deliberação em diligência analisa o caso sem tirá-lo da pauta
        var emDiligencia = historico.Casos.Single(c => c.Denominacao == "Análise de crédito");
        Assert.Equal(PgiaCgticSituacaoCaso.Pendente, emDiligencia.Situacao);
        Assert.Single(emDiligencia.Deliberacoes);
        Assert.Equal("SEEC", emDiligencia.Deliberacoes[0].OrgaoSigla);
        Assert.Equal("Análise de crédito", emDiligencia.Deliberacoes[0].SistemaDenominacao);
    }

    [Fact]
    public async Task Historico_SemCasosDevolveContagensZeradas()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Resolução normativa",
            DataDeliberacao = new DateOnly(2026, 10, 1),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var historico = await _service.ObterHistoricoCgticAsync();

        Assert.Equal(0, historico.Contagens.Total);
        Assert.Equal(0, historico.Contagens.Analisadas);
        Assert.Empty(historico.Casos);
        Assert.Single(historico.OutrasDeliberacoes);
    }

    // ── Outras deliberações ───────────────────────────────────────────────────

    [Fact]
    public async Task Historico_OutrasDeliberacoesExcluemAsDosCasos()
    {
        var historico = await MontarPautaAsync();

        var idsDosCasos = historico.Casos.Select(c => c.SistemaIaId).ToHashSet();
        Assert.Equal(2, historico.OutrasDeliberacoes.Count);
        Assert.DoesNotContain(historico.OutrasDeliberacoes,
            d => d.SistemaIaId != null && idsDosCasos.Contains(d.SistemaIaId.Value));

        // Da mais recente para a mais antiga
        Assert.Equal("Aprovação do Guia de Contratações", historico.OutrasDeliberacoes[0].Tipo);
        Assert.Null(historico.OutrasDeliberacoes[0].SistemaIaId);
        Assert.Equal("Suspensão de sistema", historico.OutrasDeliberacoes[1].Tipo);
        Assert.Equal("Agenda inteligente", historico.OutrasDeliberacoes[1].SistemaDenominacao);

        // Nenhuma deliberação do comitê se perde no relatório
        var total = historico.Casos.Sum(c => c.Deliberacoes.Count) + historico.OutrasDeliberacoes.Count;
        Assert.Equal((await _service.ListarDeliberacoesAsync()).Count, total);
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Historico_PdfEhGerado()
    {
        await MontarPautaAsync();

        var pdf = await _service.GerarPdfHistoricoCgticAsync();

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    // ── Autorização ───────────────────────────────────────────────────────────

    /// <summary>
    /// Mesmo par de checagens que as actions do PgiaGovernancaController aplicam:
    /// permissão de leitura sobre deliberação mais escopo central.
    /// </summary>
    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("cgtic@sgdi.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]   // pgia_orgao: só o próprio órgão
    [InlineData("aud@auditoria.com", false)]     // pgia_auditoria: só as auditorias designadas
    [InlineData("comum@ses.df.gov.br", false)]   // sem papel PGIA
    public async Task Historico_SoInstanciasCentraisEnxergam(string email, bool esperado)
    {
        var ctx = await CtxAsync(email);

        var pode = _permissionService.CanView(ctx, PgiaResources.Deliberacao)
            && PgiaGovernancaService.EhEscopoCentral(ctx);

        Assert.Equal(esperado, pode);
    }
}
