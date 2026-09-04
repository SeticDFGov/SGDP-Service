using System.Text;
using api.Pgia;
using app.Models;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Relatórios e auditorias (fase 4): indicadores, relatório semestral com conteúdo
/// agregado e PDF, Relatório Anual e auditorias técnicas designadas.
/// </summary>
public class PgiaRelatorioTest : PgiaTestBase
{
    private readonly PgiaRelatorioService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaOperacaoService _operacaoService;
    private readonly PgiaPermissionService _permissionService;

    public PgiaRelatorioTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context));
        _operacaoService = new PgiaOperacaoService(new PgiaOperacaoRepositorio(Context));
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
        (await _permissionService.GetContextAsync(email))!;

    private async Task<PgiaSistemaResponse> NovoSistemaAsync(
        long orgaoId, string email, string denominacao, List<string>? q16 = null)
    {
        var ctx = await CtxAsync(email);
        return await _sistemaService.CriarSistemaAsync(orgaoId, new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar o atendimento",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA generativa",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            SupervisaoHumanaDescricao = "Servidor revisa cada decisão.",
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(new PgiaChecklistDTO { Q16 = q16 ?? new List<string>() }),
                OutrosRiscos = OutrosRiscosSeNecessario(q16: q16),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Enquadramento avaliado."
            }
        }, ctx);
    }

    private static PgiaIndicadorCreateDTO NovoIndicadorDto(
        string nome, DateOnly inicio, DateOnly fim) => new()
    {
        Nome = nome,
        Categoria = "Acurácia",
        Valor = 0.93m,
        Unidade = "%",
        PeriodoInicio = inicio,
        PeriodoFim = fim,
        Meta = 0.9m
    };

    /// <summary>Ajusta o CriadoEm do sistema para simular cadastro no período desejado.</summary>
    private async Task AjustarCriacaoDoSistemaAsync(long sistemaId, DateTime criadoEm)
    {
        var sistema = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistemaId);
        sistema.CriadoEm = criadoEm;
        foreach (var c in Context.PgiaClassificacoesRisco.Where(c => c.SistemaIaId == sistemaId))
            c.CriadoEm = criadoEm;
        await Context.SaveChangesAsync();
    }

    // ── Indicadores ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Indicador_PeriodoInvertidoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarIndicadorAsync(sistema.Id,
                NovoIndicadorDto("Acurácia", new DateOnly(2026, 6, 30), new DateOnly(2026, 1, 1)), ctx));

        Assert.Equal((int)ErrorCode.PgiaPeriodoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Indicador_PeriodoAusenteEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente");

        var dto = NovoIndicadorDto("Acurácia", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        dto.PeriodoFim = default;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarIndicadorAsync(sistema.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaPeriodoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Indicador_CategoriaForaDoDominioEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente");

        var dto = NovoIndicadorDto("Acurácia", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        dto.Categoria = "Simpatia";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarIndicadorAsync(sistema.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Indicador_ListagemFicaNoSistemaCerto()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var a = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Sistema A");
        var b = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Sistema B");

        await _service.CriarIndicadorAsync(a.Id,
            NovoIndicadorDto("Acurácia do A", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)), ctx);
        await _service.CriarIndicadorAsync(b.Id,
            NovoIndicadorDto("Acurácia do B", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)), ctx);

        var doA = await _service.ListarIndicadoresPorSistemaAsync(a.Id);
        Assert.Single(doA);
        Assert.Equal("Acurácia do A", doA[0].Nome);
        Assert.Equal("Sistema A", doA[0].SistemaDenominacao);
    }

    // ── Relatório semestral ───────────────────────────────────────────────────

    [Fact]
    public async Task Relatorio_ParOrgaoAnoSemestreEhUnico()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
                new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx));

        Assert.Equal((int)ErrorCode.PgiaRelatorioJaExiste, ex.Error.Code);

        // O mesmo par vale para outro órgão
        var daSeec = await _service.CriarRelatorioSemestralAsync(OrgaoSeec.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, await CtxAsync(UserOrgaoSeec.Email));
        Assert.Equal(OrgaoSeec.Id, daSeec.OrgaoId);
    }

    [Theory]
    [InlineData((short)2026, (short)1, 2026, 7, 31)]
    [InlineData((short)2026, (short)2, 2027, 1, 31)]
    [InlineData((short)2027, (short)1, 2027, 7, 31)]
    public async Task Relatorio_PrazoCalculadoPeloSemestre(
        short ano, short semestre, int anoPrazo, int mesPrazo, int diaPrazo)
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = ano, Semestre = semestre }, ctx);

        Assert.Equal(new DateOnly(anoPrazo, mesPrazo, diaPrazo), relatorio.PrazoEnvio);
        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.Pendente, relatorio.Status);
    }

    [Fact]
    public async Task Relatorio_SemestreOuAnoInvalidoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var semestreInvalido = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
                new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 3 }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, semestreInvalido.Error.Code);

        var anoInvalido = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
                new PgiaRelatorioSemestralCreateDTO { Ano = 2020, Semestre = 1 }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, anoInvalido.Error.Code);
    }

    [Fact]
    public async Task Relatorio_EnvioNoPrazoEEmAtrasoSaoCalculados()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var noPrazo = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);
        var enviado = await _service.RegistrarEnvioAsync(noPrazo.Id, new PgiaRelatorioEnvioDTO
        {
            DataEnvio = new DateOnly(2026, 7, 30),
            ProcessoSei = "00060-00044444/2026-11"
        }, ctx);
        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.EnviadoNoPrazo, enviado.Status);

        var atrasado = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 2 }, ctx);
        var emAtraso = await _service.RegistrarEnvioAsync(atrasado.Id, new PgiaRelatorioEnvioDTO
        {
            DataEnvio = new DateOnly(2027, 2, 10),
            ProcessoSei = "00060-00055555/2027-11"
        }, ctx);
        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.EnviadoEmAtraso, emAtraso.Status);

        // O envio na data-limite ainda é no prazo
        var limite = await _service.CriarRelatorioSemestralAsync(OrgaoSeec.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, await CtxAsync(UserOrgaoSeec.Email));
        var noLimite = await _service.RegistrarEnvioAsync(limite.Id, new PgiaRelatorioEnvioDTO
        {
            DataEnvio = new DateOnly(2026, 7, 31),
            ProcessoSei = "00060-00066666/2026-11"
        }, await CtxAsync(UserOrgaoSeec.Email));
        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.EnviadoNoPrazo, noLimite.Status);
    }

    [Fact]
    public async Task Relatorio_EnvioSemSeiOuDataEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var semData = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarEnvioAsync(relatorio.Id, new PgiaRelatorioEnvioDTO
            {
                ProcessoSei = "00060-00044444/2026-11"
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, semData.Error.Code);

        var semSei = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarEnvioAsync(relatorio.Id, new PgiaRelatorioEnvioDTO
            {
                DataEnvio = new DateOnly(2026, 7, 30),
                ProcessoSei = "   "
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, semSei.Error.Code);
    }

    [Fact]
    public async Task Relatorio_SituacaoPelaSgdiRegistraPainelEControleInterno()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, orgaoCtx);

        var inadimplente = await _service.AtualizarSituacaoAsync(relatorio.Id, new PgiaRelatorioSituacaoDTO
        {
            Status = PgiaDominios.StatusRelatorioSemestral.Inadimplente,
            RegistradoPainelEm = new DateOnly(2026, 8, 5),
            ComunicadoControleInternoEm = new DateOnly(2026, 8, 10)
        }, sgdiCtx);

        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.Inadimplente, inadimplente.Status);
        Assert.Equal(new DateOnly(2026, 8, 5), inadimplente.RegistradoPainelEm);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSituacaoAsync(relatorio.Id, new PgiaRelatorioSituacaoDTO
            {
                Status = "Arquivado"
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Relatorio_ConteudoAgregaSomenteOPeriodoDoSemestre()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);

        // Sistema Alto Risco criado no 1º semestre; outro criado no 2º
        var altoRisco = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem clínica", q16: new List<string> { "IV" });
        await AjustarCriacaoDoSistemaAsync(altoRisco.Id, new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));

        var doSegundoSemestre = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Agenda inteligente");
        await AjustarCriacaoDoSistemaAsync(doSegundoSemestre.Id, new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));

        // Indicadores: um no 1º semestre, outro no 2º
        await _service.CriarIndicadorAsync(altoRisco.Id,
            NovoIndicadorDto("Acurácia S1", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)), orgaoCtx);
        await _service.CriarIndicadorAsync(altoRisco.Id,
            NovoIndicadorDto("Acurácia S2", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31)), orgaoCtx);

        // Incidentes comunicados: um em cada semestre
        await CriarIncidenteComunicadoAsync(altoRisco.Id, new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc), orgaoCtx);
        await CriarIncidenteComunicadoAsync(altoRisco.Id, new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc), orgaoCtx);

        var primeiro = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, orgaoCtx);
        var conteudoS1 = (await _service.GetRelatorioSemestralAsync(primeiro.Id)).Conteudo!;

        // I — todos os sistemas do órgão, independentemente do período
        Assert.Equal(2, conteudoS1.Sistemas.Count);

        // II — só o incidente comunicado no 1º semestre
        Assert.Single(conteudoS1.Incidentes);
        Assert.Equal(new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc), conteudoS1.Incidentes[0].DataComunicacaoSgdi);

        // III — só o indicador do 1º semestre, e só de sistema Alto Risco
        Assert.Single(conteudoS1.IndicadoresAltoRisco);
        Assert.Equal("Acurácia S1", conteudoS1.IndicadoresAltoRisco[0].Nome);

        // V — só o sistema movimentado no 1º semestre
        Assert.Single(conteudoS1.AtualizacoesInventario);
        Assert.Equal("Triagem clínica", conteudoS1.AtualizacoesInventario[0].Denominacao);

        var segundo = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 2 }, orgaoCtx);
        var conteudoS2 = (await _service.GetRelatorioSemestralAsync(segundo.Id)).Conteudo!;

        Assert.Single(conteudoS2.Incidentes);
        Assert.Equal("Acurácia S2", conteudoS2.IndicadoresAltoRisco[0].Nome);
        Assert.Single(conteudoS2.AtualizacoesInventario);
        Assert.Equal("Agenda inteligente", conteudoS2.AtualizacoesInventario[0].Denominacao);
    }

    [Fact]
    public async Task Relatorio_IncidenteNaViradaDoSemestreEmBrasiliaEntraNoPeriodo()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem clínica");

        // 30/06/2026 às 22h de Brasília = 01/07/2026 01:00 UTC. Carimbar a janela como
        // UTC perderia este incidente do 1º semestre e o jogaria no 2º.
        var comunicacaoUtc = DateTimeHelper.ToUtc(new DateTime(2026, 6, 30, 22, 0, 0));
        await CriarIncidenteComunicadoAsync(sistema.Id, comunicacaoUtc, orgaoCtx);

        var primeiro = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, orgaoCtx);
        var conteudoS1 = (await _service.GetRelatorioSemestralAsync(primeiro.Id)).Conteudo!;

        Assert.Single(conteudoS1.Incidentes);

        var segundo = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 2 }, orgaoCtx);
        var conteudoS2 = (await _service.GetRelatorioSemestralAsync(segundo.Id)).Conteudo!;

        Assert.Empty(conteudoS2.Incidentes);
    }

    [Fact]
    public async Task Relatorio_EnvioAntesDoFimDoSemestreEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarEnvioAsync(relatorio.Id, new PgiaRelatorioEnvioDTO
            {
                DataEnvio = new DateOnly(2026, 6, 20),
                ProcessoSei = "00060-00044444/2026-11"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaPeriodoInvalido, ex.Error.Code);
        Assert.Contains("após o fim do semestre", ex.Error.Message);
    }

    [Fact]
    public async Task Relatorio_DocumentoDeOutroOrgaoNoEnvioEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var documentoDaSeec = new PgiaDocumento
        {
            Tipo = "Relatório semestral",
            OrgaoId = OrgaoSeec.Id,
            NomeArquivo = "relatorio.pdf",
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = UserOrgaoSeec.Id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documentoDaSeec);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarEnvioAsync(relatorio.Id, new PgiaRelatorioEnvioDTO
            {
                DataEnvio = new DateOnly(2026, 7, 30),
                ProcessoSei = "00060-00044444/2026-11",
                DocumentoId = documentoDaSeec.Id
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, ex.Error.Code);
        Assert.Contains("outro órgão", ex.Error.Message);
    }

    [Fact]
    public async Task Relatorio_SituacaoIncoerenteComOEnvioEhRejeitada()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var semEnvio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, orgaoCtx);

        // Sem envio registrado não há como declarar enviado
        var enviadoSemEnvio = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSituacaoAsync(semEnvio.Id, new PgiaRelatorioSituacaoDTO
            {
                Status = PgiaDominios.StatusRelatorioSemestral.EnviadoNoPrazo
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, enviadoSemEnvio.Error.Code);

        // Enviado dentro do prazo não vira inadimplente nem volta a pendente
        await _service.RegistrarEnvioAsync(semEnvio.Id, new PgiaRelatorioEnvioDTO
        {
            DataEnvio = new DateOnly(2026, 7, 20),
            ProcessoSei = "00060-00044444/2026-11"
        }, orgaoCtx);

        var inadimplenteIndevido = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSituacaoAsync(semEnvio.Id, new PgiaRelatorioSituacaoDTO
            {
                Status = PgiaDominios.StatusRelatorioSemestral.Inadimplente
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, inadimplenteIndevido.Error.Code);

        var voltaPendente = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSituacaoAsync(semEnvio.Id, new PgiaRelatorioSituacaoDTO
            {
                Status = PgiaDominios.StatusRelatorioSemestral.Pendente
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, voltaPendente.Error.Code);

        // Enviado em atraso pode, sim, ser marcado inadimplente pela SGDI
        var atrasado = await _service.CriarRelatorioSemestralAsync(OrgaoSeec.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, await CtxAsync(UserOrgaoSeec.Email));
        await _service.RegistrarEnvioAsync(atrasado.Id, new PgiaRelatorioEnvioDTO
        {
            DataEnvio = new DateOnly(2026, 9, 15),
            ProcessoSei = "00060-00055555/2026-11"
        }, await CtxAsync(UserOrgaoSeec.Email));

        var marcado = await _service.AtualizarSituacaoAsync(atrasado.Id, new PgiaRelatorioSituacaoDTO
        {
            Status = PgiaDominios.StatusRelatorioSemestral.Inadimplente
        }, sgdiCtx);
        Assert.Equal(PgiaDominios.StatusRelatorioSemestral.Inadimplente, marcado.Status);
    }

    [Fact]
    public async Task Relatorio_AnoAlemDoExercicioSeguinteEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var anoDemaisAdiante = (short)(DateTimeHelper.TodayBrasilia().Year + 2);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
                new PgiaRelatorioSemestralCreateDTO { Ano = anoDemaisAdiante, Semestre = 1 }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Relatorio_ConteudoNaoVazaOutroOrgao()
    {
        var sesCtx = await CtxAsync(UserOrgaoSes.Email);
        await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Da SES");
        await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Da SEEC");

        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 2 }, sesCtx);
        var conteudo = (await _service.GetRelatorioSemestralAsync(relatorio.Id)).Conteudo!;

        Assert.Single(conteudo.Sistemas);
        Assert.Equal("Da SES", conteudo.Sistemas[0].Denominacao);
    }

    [Fact]
    public async Task Relatorio_ListagemNaoCarregaOConteudo()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var lista = await _service.ListarRelatoriosSemestraisAsync(OrgaoSes.Id);

        Assert.Single(lista);
        Assert.Null(lista[0].Conteudo);
    }

    private async Task CriarIncidenteComunicadoAsync(long sistemaId, DateTime comunicacao, PgiaUserContext ctx)
    {
        await _operacaoService.CriarIncidenteFormalAsync(new PgiaIncidenteCreateDTO
        {
            SistemaIaId = sistemaId,
            Hipotese = "IV",
            Descricao = "Vazamento de dados.",
            DataOcorrencia = comunicacao.AddDays(-2),
            DataDeteccao = comunicacao.AddDays(-1),
            NotificadoResponsavelEm = comunicacao.AddHours(-2),
            DataComunicacaoSgdi = comunicacao,
            ProcessoSei = "00060-00077777/2026-11",
            MedidasAdotadas = "Acesso revogado."
        }, ctx);
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Relatorio_PdfEhGeradoComConteudo()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem clínica", q16: new List<string> { "IV" });
        await _service.CriarIndicadorAsync(sistema.Id,
            NovoIndicadorDto("Acurácia", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31)), ctx);
        await CriarIncidenteComunicadoAsync(sistema.Id, new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc), ctx);

        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 2 }, ctx);

        var bytes = await _service.GerarPdfRelatorioSemestralAsync(relatorio.Id);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Relatorio_PdfDeRelatorioVazioTambemEhGerado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var relatorio = await _service.CriarRelatorioSemestralAsync(OrgaoSes.Id,
            new PgiaRelatorioSemestralCreateDTO { Ano = 2026, Semestre = 1 }, ctx);

        var bytes = await _service.GerarPdfRelatorioSemestralAsync(relatorio.Id);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    // ── Relatório Anual ───────────────────────────────────────────────────────

    [Fact]
    public async Task RelatorioAnual_AnoEhUnico()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        await _service.CriarRelatorioAnualAsync(new PgiaRelatorioAnualCreateDTO { Ano = 2027 }, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarRelatorioAnualAsync(new PgiaRelatorioAnualCreateDTO { Ano = 2027 }, ctx));

        Assert.Equal((int)ErrorCode.PgiaRelatorioJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task RelatorioAnual_PublicacaoEApreciacaoSaoGravadas()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var relatorio = await _service.CriarRelatorioAnualAsync(new PgiaRelatorioAnualCreateDTO { Ano = 2027 }, ctx);

        var publicado = await _service.AtualizarRelatorioAnualAsync(relatorio.Id, new PgiaRelatorioAnualUpdateDTO
        {
            Ano = 2027,
            DataPublicacao = new DateOnly(2027, 3, 30),
            UrlPublicacao = "https://transparencia.df.gov.br/relatorio-ia-2027",
            ApreciadoCgticEm = new DateOnly(2027, 3, 20),
            Recomendacoes = "Ampliar a capacitação.",
            AgendaInovacao = "Piloto de IA generativa em atendimento."
        }, ctx);

        Assert.Equal(new DateOnly(2027, 3, 30), publicado.DataPublicacao);
        Assert.Equal("Ampliar a capacitação.", publicado.Recomendacoes);

        var lista = await _service.ListarRelatoriosAnuaisAsync();
        Assert.Single(lista);
    }

    [Fact]
    public async Task RelatorioAnual_EdicaoParaAnoJaUsadoEhRejeitada()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        await _service.CriarRelatorioAnualAsync(new PgiaRelatorioAnualCreateDTO { Ano = 2027 }, ctx);
        var outro = await _service.CriarRelatorioAnualAsync(new PgiaRelatorioAnualCreateDTO { Ano = 2028 }, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarRelatorioAnualAsync(outro.Id, new PgiaRelatorioAnualUpdateDTO { Ano = 2027 }, ctx));

        Assert.Equal((int)ErrorCode.PgiaRelatorioJaExiste, ex.Error.Code);
    }

    // ── Auditorias técnicas ───────────────────────────────────────────────────

    private async Task<PgiaAuditoriaResponse> NovaAuditoriaAsync(long sistemaId, Guid? auditorId)
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        return await _service.CriarAuditoriaAsync(new PgiaAuditoriaCreateDTO
        {
            SistemaIaId = sistemaId,
            Tipo = "Periódica",
            EntidadeAuditora = "Auditoria Independente S/A",
            AuditorUserId = auditorId,
            ExternaFornecedor = true,
            DataInicio = new DateOnly(2027, 2, 1)
        }, sgdiCtx);
    }

    [Fact]
    public async Task Auditoria_DesignacaoExigeUsuarioComPapelDeAuditoria()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAuditoriaAsync(new PgiaAuditoriaCreateDTO
            {
                SistemaIaId = sistema.Id,
                Tipo = "Periódica",
                EntidadeAuditora = "Auditoria Independente S/A",
                // Maria é pgia_orgao, não auditoria externa
                AuditorUserId = UserOrgaoSes.Id,
                ExternaFornecedor = true,
                DataInicio = new DateOnly(2027, 2, 1)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Contains("auditoria externa", ex.Error.Message);
    }

    [Fact]
    public async Task Auditoria_AuditorNaoDesignadoNaoRegistraParecer()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        // Auditoria sem designação: nenhuma auditora externa pode mexer nela
        var auditoria = await NovaAuditoriaAsync(sistema.Id, null);
        var auditorCtx = await CtxAsync(UserAuditoria.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
            {
                DataInicio = new DateOnly(2027, 2, 1),
                Parecer = "Tentativa indevida."
            }, auditorCtx));

        Assert.Equal((int)ErrorCode.PgiaAuditoriaNaoDesignada, ex.Error.Code);
        Assert.Contains("designada a você", ex.Error.Message);
    }

    [Fact]
    public async Task Auditoria_DesignadaRegistraParecerEDatas()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var auditoria = await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        var auditorCtx = await CtxAsync(UserAuditoria.Email);

        var comParecer = await _service.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
        {
            DataInicio = new DateOnly(2027, 2, 5),
            DataFim = new DateOnly(2027, 3, 20),
            Parecer = "Sistema aderente aos requisitos, com ressalva no monitoramento."
        }, auditorCtx);

        Assert.Equal(new DateOnly(2027, 3, 20), comParecer.DataFim);
        Assert.Contains("ressalva", comParecer.Parecer);
        // O parecer não mexe na designação nem na publicação
        Assert.Equal("Auditoria Independente S/A", comParecer.EntidadeAuditora);
        Assert.False(comParecer.PublicadoPortal);
        Assert.Equal("Alice Auditora", comParecer.AuditorNome);
    }

    [Fact]
    public async Task Auditoria_ParecerComDataFimAnteriorEhRejeitado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var auditoria = await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        var auditorCtx = await CtxAsync(UserAuditoria.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
            {
                DataInicio = new DateOnly(2027, 3, 1),
                DataFim = new DateOnly(2027, 2, 1)
            }, auditorCtx));

        Assert.Equal((int)ErrorCode.PgiaPeriodoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Auditoria_SgdiRegistraParecerMesmoSemDesignacao()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var auditoria = await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var comParecer = await _service.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
        {
            DataInicio = new DateOnly(2027, 2, 5),
            Parecer = "Parecer recebido por ofício e registrado pela SGDI."
        }, sgdiCtx);

        Assert.Contains("ofício", comParecer.Parecer);
    }

    [Fact]
    public async Task Auditoria_ListaMinhasFiltraPeloAuditorDesignado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var outro = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador", q16: new List<string> { "IV" });

        await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        await NovaAuditoriaAsync(outro.Id, null);

        var auditorCtx = await CtxAsync(UserAuditoria.Email);
        var minhas = await _service.ListarMinhasAuditoriasAsync(auditorCtx);

        Assert.Single(minhas);
        Assert.Equal("Triagem", minhas[0].SistemaDenominacao);
        Assert.Equal("SES", minhas[0].OrgaoSigla);

        // A visão central enxerga as duas
        Assert.Equal(2, (await _service.ListarAuditoriasAsync()).Count);
    }

    [Fact]
    public async Task Auditoria_PublicacaoLimpaDataEUrlAoDespublicar()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var auditoria = await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var publicada = await _service.PublicarAuditoriaAsync(auditoria.Id, new PgiaAuditoriaPublicacaoDTO
        {
            PublicadoPortal = true,
            DataPublicacao = new DateOnly(2027, 4, 1),
            Url = "https://transparencia.df.gov.br/auditoria/1"
        }, sgdiCtx);

        Assert.True(publicada.PublicadoPortal);
        Assert.Equal("https://transparencia.df.gov.br/auditoria/1", publicada.Url);

        var despublicada = await _service.PublicarAuditoriaAsync(auditoria.Id, new PgiaAuditoriaPublicacaoDTO
        {
            PublicadoPortal = false,
            DataPublicacao = new DateOnly(2027, 4, 1),
            Url = "https://transparencia.df.gov.br/auditoria/1"
        }, sgdiCtx);

        Assert.False(despublicada.PublicadoPortal);
        Assert.Null(despublicada.DataPublicacao);
        Assert.Null(despublicada.Url);
    }

    [Fact]
    public async Task Auditoria_ListaPorSistemaEAuditoresDisponiveis()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var outro = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Agenda");

        await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);
        await NovaAuditoriaAsync(outro.Id, null);

        var doSistema = await _service.ListarAuditoriasPorSistemaAsync(sistema.Id);
        Assert.Single(doSistema);

        var auditores = await _service.ListarAuditoresAsync();
        Assert.Single(auditores);
        Assert.Equal(UserAuditoria.Id, auditores[0].UserId);
        Assert.Equal("Alice Auditora", auditores[0].Nome);
    }

    [Fact]
    public async Task Auditoria_EdicaoRevalidaODesignadoEAsDatas()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var auditorCtx = await CtxAsync(UserAuditoria.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        var auditoria = await NovaAuditoriaAsync(sistema.Id, UserAuditoria.Id);

        // Designar quem não é auditoria externa continua barrado na edição
        var designacaoInvalida = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarAuditoriaAsync(auditoria.Id, new PgiaAuditoriaCreateDTO
            {
                SistemaIaId = sistema.Id,
                Tipo = "Periódica",
                EntidadeAuditora = "Auditoria Independente S/A",
                AuditorUserId = UserOrgaoSes.Id,
                ExternaFornecedor = true,
                DataInicio = new DateOnly(2027, 2, 1)
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, designacaoInvalida.Error.Code);

        // Com a conclusão já registrada, o início não pode ultrapassá-la
        await _service.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
        {
            DataInicio = new DateOnly(2027, 2, 5),
            DataFim = new DateOnly(2027, 3, 20),
            Parecer = "Concluído."
        }, auditorCtx);

        var dataIncoerente = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarAuditoriaAsync(auditoria.Id, new PgiaAuditoriaCreateDTO
            {
                SistemaIaId = sistema.Id,
                Tipo = "Periódica",
                EntidadeAuditora = "Auditoria Independente S/A",
                AuditorUserId = UserAuditoria.Id,
                ExternaFornecedor = true,
                DataInicio = new DateOnly(2027, 4, 1)
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaPeriodoInvalido, dataIncoerente.Error.Code);

        // Edição coerente passa
        var atualizada = await _service.AtualizarAuditoriaAsync(auditoria.Id, new PgiaAuditoriaCreateDTO
        {
            SistemaIaId = sistema.Id,
            Tipo = "Independente contratual",
            EntidadeAuditora = "Outra Auditoria S/A",
            AuditorUserId = UserAuditoria.Id,
            ExternaFornecedor = true,
            DataInicio = new DateOnly(2027, 2, 1)
        }, sgdiCtx);
        Assert.Equal("Outra Auditoria S/A", atualizada.EntidadeAuditora);
        Assert.Equal(new DateOnly(2027, 3, 20), atualizada.DataFim);
    }

    [Fact]
    public async Task Auditoria_TipoForaDoDominioEhRejeitado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAuditoriaAsync(new PgiaAuditoriaCreateDTO
            {
                SistemaIaId = sistema.Id,
                Tipo = "Informal",
                EntidadeAuditora = "Auditoria X",
                ExternaFornecedor = true,
                DataInicio = new DateOnly(2027, 2, 1)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }
}
