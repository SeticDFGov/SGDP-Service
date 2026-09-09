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
/// Contratações (fase 4): requisitos obrigatórios do art. 25 conforme o risco do
/// sistema vinculado, vedação de treinamento do art. 21 e triagem do art. 37.
/// </summary>
public class PgiaContratoTest : PgiaTestBase
{
    private readonly PgiaContratoService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaGovernancaService _governanca;
    private readonly PgiaPermissionService _permissionService;

    public PgiaContratoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaContratoService(new PgiaContratoRepositorio(Context));
        _governanca = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));
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

    /// <param name="incisoQ16">Marcado, o sistema nasce Alto Risco.</param>
    private async Task<PgiaSistemaResponse> NovoSistemaAsync(
        long orgaoId, string email, string denominacao,
        List<string>? q16 = null, List<string>? q17 = null)
    {
        var ctx = await CtxAsync(email);
        var dto = new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar o atendimento",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = PgiaDominios.TipoSistema.Contratado,
            Tecnologia = "IA generativa",
            StatusCicloVida = "Em aquisição",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            JustificativaNaoRedundancia = "Não há solução corporativa equivalente.",
            SupervisaoHumanaDescricao = "Servidor revisa cada decisão.",
            AvisoInteracaoIa = true,
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(new PgiaChecklistDTO
                {
                    Q16 = q16 ?? new List<string>(),
                    Q17 = q17 ?? new List<string>()
                }),
                OutrosRiscos = OutrosRiscosSeNecessario(q16: q16, q17: q17),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Enquadramento avaliado."
            }
        };
        return await _sistemaService.CriarSistemaAsync(orgaoId, dto, ctx);
    }

    // PgiaContratoUpdateDTO herda de Create: serve nos dois usos
    private static PgiaContratoUpdateDTO NovoContratoDto(long? sistemaId = null) => new()
    {
        SistemaIaId = sistemaId,
        NumeroContrato = "12/2026",
        ProcessoSei = "00060-00033333/2026-11",
        Objeto = "Aquisição de solução de IA para triagem",
        FornecedorNome = "Fornecedor X LTDA",
        Status = "Vigente",
        ClausulaVedacaoTreinamento = true,
        ReqExplicabilidade = true,
        ReqAuditabilidade = true,
        ReqPortabilidade = true,
        ReqSemAprisionamento = true,
        ReqAcessibilidade = true
    };

    private async Task<long> NovoDocumentoAsync(long orgaoId, Guid enviadoPor)
    {
        var documento = new PgiaDocumento
        {
            Tipo = "Contrato",
            OrgaoId = orgaoId,
            NomeArquivo = "homologacao.pdf",
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = enviadoPor,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documento);
        await Context.SaveChangesAsync();
        return documento.Id;
    }

    // ── Condicionais do art. 25 pelo risco ────────────────────────────────────

    [Fact]
    public async Task Contrato_SemSistemaVinculadoNaoExigeCondicionais()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var contrato = await _service.CriarContratoAsync(OrgaoSes.Id, NovoContratoDto(), ctx);

        Assert.Equal(OrgaoSes.Id, contrato.OrgaoId);
        Assert.Equal("SES", contrato.OrgaoSigla);
        Assert.Null(contrato.SistemaIaId);
        Assert.Null(contrato.PrevistoPdtic);
    }

    [Fact]
    public async Task Contrato_ModeradoExigePdticEHomologacaoTecnica()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente", q17: new List<string> { "I" });
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, sistema.ClassificacaoRiscoAtual);

        var semPdtic = NovoContratoDto(sistema.Id);
        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, semPdtic, ctx));
        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex1.Error.Code);
        Assert.Contains("PDTIC", ex1.Error.Message);

        var semHomologacao = NovoContratoDto(sistema.Id);
        semHomologacao.PrevistoPdtic = true;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, semHomologacao, ctx));
        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex2.Error.Code);
        Assert.Contains("homologação técnica", ex2.Error.Message);

        var completo = NovoContratoDto(sistema.Id);
        completo.PrevistoPdtic = true;
        completo.HomologacaoSgdiDocId = await NovoDocumentoAsync(OrgaoSes.Id, UserOrgaoSes.Id);
        var contrato = await _service.CriarContratoAsync(OrgaoSes.Id, completo, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, contrato.SistemaClassificacao);
        Assert.True(contrato.PrevistoPdtic);
    }

    [Fact]
    public async Task Contrato_AltoRiscoExigeAuditoriaIndependenteESlas()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, sistema.ClassificacaoRiscoAtual);

        var semAuditoria = NovoContratoDto(sistema.Id);
        semAuditoria.PrevistoPdtic = true;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, semAuditoria, ctx));
        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex1.Error.Code);
        Assert.Contains("auditoria independente", ex1.Error.Message);

        var semSlas = NovoContratoDto(sistema.Id);
        semSlas.PrevistoPdtic = true;
        semSlas.ClausulaAuditoriaIndependente = true;
        semSlas.SlaDesempenho = 0.95m;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, semSlas, ctx));
        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex2.Error.Code);
        Assert.Contains("níveis de serviço", ex2.Error.Message);

        var semPenalidades = ContratoAltoRiscoCompleto(sistema.Id);
        semPenalidades.SlaPenalidades = "   ";
        var ex3 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, semPenalidades, ctx));
        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex3.Error.Code);
        Assert.Contains("penalidades", ex3.Error.Message);

        var contrato = await _service.CriarContratoAsync(OrgaoSes.Id, ContratoAltoRiscoCompleto(sistema.Id), ctx);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, contrato.SistemaClassificacao);
        Assert.Equal(0.9m, contrato.SlaEquidade);
    }

    private static PgiaContratoUpdateDTO ContratoAltoRiscoCompleto(long sistemaId)
    {
        var dto = NovoContratoDto(sistemaId);
        dto.PrevistoPdtic = true;
        dto.ClausulaAuditoriaIndependente = true;
        dto.SlaDesempenho = 0.95m;
        dto.SlaAcuracia = 0.92m;
        dto.SlaEquidade = 0.9m;
        dto.SlaDisponibilidade = 0.99m;
        dto.SlaPenalidades = "Glosa de 5% por mês fora do nível acordado.";
        return dto;
    }

    // ── Vedação de treinamento (art. 21) ──────────────────────────────────────

    [Fact]
    public async Task Contrato_SemClausulaDeVedacaoExigeAutorizacaoDeTreinamento()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
        Assert.Contains("art. 21", ex.Error.Message);
    }

    [Fact]
    public async Task Contrato_AutorizacaoDeOutroTipoNaoServeParaTreinamento()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var autorizacaoDePlataforma = await _governanca.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Avaliação prévia concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;
        dto.AutorizacaoTreinamentoId = autorizacaoDePlataforma.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, orgaoCtx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Contrato_AutorizacaoDeTreinamentoValidaEhAceita()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var deliberacao = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Autorização de treinamento com dados do GDF",
            DataDeliberacao = new DateOnly(2026, 11, 1),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var autorizacao = await _governanca.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoAutorizacao.TreinamentoFornecedor,
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Ajuste fino do modelo.",
            AutorizadaPor = "Órgão com aprovação do CGTIC",
            DeliberacaoCgticId = deliberacao.Id,
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;
        dto.AutorizacaoTreinamentoId = autorizacao.Id;

        var contrato = await _service.CriarContratoAsync(OrgaoSes.Id, dto, orgaoCtx);

        Assert.False(contrato.ClausulaVedacaoTreinamento);
        Assert.Equal(autorizacao.Id, contrato.AutorizacaoTreinamentoId);
    }

    /// <summary>Autorização de treinamento pronta para uso, no órgão indicado.</summary>
    private async Task<PgiaAutorizacaoResponse> NovaAutorizacaoTreinamentoAsync(
        long orgaoId, DateOnly? vigenciaFim = null)
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var deliberacao = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Autorização de treinamento com dados do GDF",
            DataDeliberacao = new DateOnly(2026, 11, 1),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        return await _governanca.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoAutorizacao.TreinamentoFornecedor,
            OrgaoId = orgaoId,
            Justificativa = "Ajuste fino do modelo.",
            AutorizadaPor = "Órgão com aprovação do CGTIC",
            DeliberacaoCgticId = deliberacao.Id,
            DataAutorizacao = new DateOnly(2026, 11, 10),
            VigenciaFim = vigenciaFim
        }, sgdiCtx);
    }

    [Fact]
    public async Task Contrato_AutorizacaoDeTreinamentoDeOutroOrgaoEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var autorizacaoDaSeec = await NovaAutorizacaoTreinamentoAsync(OrgaoSeec.Id);

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;
        dto.AutorizacaoTreinamentoId = autorizacaoDaSeec.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
        Assert.Contains("outro órgão", ex.Error.Message);
    }

    [Fact]
    public async Task Contrato_AutorizacaoDeTreinamentoRevogadaEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var autorizacao = await NovaAutorizacaoTreinamentoAsync(OrgaoSes.Id);

        await _governanca.AtualizarAutorizacaoAsync(autorizacao.Id, new PgiaAutorizacaoUpdateDTO
        {
            Tipo = PgiaDominios.TipoAutorizacao.TreinamentoFornecedor,
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Ajuste fino do modelo.",
            AutorizadaPor = "Órgão com aprovação do CGTIC",
            DeliberacaoCgticId = autorizacao.DeliberacaoCgticId,
            DataAutorizacao = new DateOnly(2026, 11, 10),
            Ativo = false
        }, sgdiCtx);

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;
        dto.AutorizacaoTreinamentoId = autorizacao.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
        Assert.Contains("revogada", ex.Error.Message);
    }

    [Fact]
    public async Task Contrato_AutorizacaoDeTreinamentoVencidaEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        // Vigência encerrada em 2026-01-31, muito antes de hoje
        var vencida = await NovaAutorizacaoTreinamentoAsync(OrgaoSes.Id, new DateOnly(2026, 1, 31));

        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = false;
        dto.AutorizacaoTreinamentoId = vencida.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
        Assert.Contains("vencida", ex.Error.Message);
    }

    [Fact]
    public async Task Contrato_AutorizacaoInvalidaEhBarradaMesmoComClausulaDeVedacao()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var autorizacaoDaSeec = await NovaAutorizacaoTreinamentoAsync(OrgaoSeec.Id);

        // Cláusula presente, mas a autorização informada é de outro órgão
        var dto = NovoContratoDto();
        dto.ClausulaVedacaoTreinamento = true;
        dto.AutorizacaoTreinamentoId = autorizacaoDaSeec.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Contrato_DocumentoDeHomologacaoDeOutroOrgaoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente", q17: new List<string> { "I" });

        var dto = NovoContratoDto(sistema.Id);
        dto.PrevistoPdtic = true;
        dto.HomologacaoSgdiDocId = await NovoDocumentoAsync(OrgaoSeec.Id, UserOrgaoSeec.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, ex.Error.Code);
        Assert.Contains("outro órgão", ex.Error.Message);
    }

    // ── Escopo e validações gerais ────────────────────────────────────────────

    [Fact]
    public async Task Contrato_SistemaDeOutroOrgaoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistemaDaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, NovoContratoDto(sistemaDaSeec.Id), ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Contrato_CamposObrigatoriosSaoExigidos()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var dto = NovoContratoDto();
        dto.FornecedorNome = "  ";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Contrato_StatusForaDoDominioEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var dto = NovoContratoDto();
        dto.Status = "Rascunho";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarContratoAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Contrato_ListagemSeparaOsOrgaos()
    {
        await _service.CriarContratoAsync(OrgaoSes.Id, NovoContratoDto(), await CtxAsync(UserOrgaoSes.Email));

        var daSeec = NovoContratoDto();
        daSeec.NumeroContrato = "99/2026";
        await _service.CriarContratoAsync(OrgaoSeec.Id, daSeec, await CtxAsync(UserOrgaoSeec.Email));

        var contratosSes = await _service.ListarContratosPorOrgaoAsync(OrgaoSes.Id);
        Assert.Single(contratosSes);
        Assert.Equal("12/2026", contratosSes[0].NumeroContrato);
        Assert.DoesNotContain(contratosSes, c => c.NumeroContrato == "99/2026");
    }

    [Fact]
    public async Task Contrato_EdicaoRevalidaOsCondicionais()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Triagem", q16: new List<string> { "IV" });

        var contrato = await _service.CriarContratoAsync(OrgaoSes.Id, ContratoAltoRiscoCompleto(sistema.Id), ctx);

        // Tenta remover as penalidades da ANS na edição
        var update = ContratoAltoRiscoCompleto(sistema.Id);
        update.SlaPenalidades = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarContratoAsync(contrato.Id, update, ctx));

        Assert.Equal((int)ErrorCode.PgiaContratoInvalido, ex.Error.Code);
    }

    // ── Instrumentos anteriores ao decreto (art. 37) ──────────────────────────

    [Fact]
    public async Task Legado_TriagemRegistraQuemTriou()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var legado = await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot anterior ao decreto",
            Numero = "45/2024",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 11, 20)
        }, ctx);

        Assert.Equal("Maria Andrade", legado.TriadoPorNome);
        Assert.Equal(PgiaDominios.EnvolveIa.Sim, legado.EnvolveIa);

        var salvo = await Context.PgiaInstrumentosLegados.SingleAsync();
        Assert.Equal(ctx.UserId, salvo.TriadoPor);
    }

    [Fact]
    public async Task Legado_SemTriagemNaoRegistraTriador()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var legado = await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Ato normativo",
            Descricao = "Portaria anterior ao decreto"
        }, ctx);

        // Sem data de triagem, ninguém respondeu por ela ainda
        Assert.Null(legado.TriadoPorNome);
        Assert.Equal(PgiaDominios.EnvolveIa.Incerto, legado.EnvolveIa);
    }

    [Fact]
    public async Task Legado_TipoOuEnvolveIaForaDoDominioEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var tipoInvalido = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Bilhete",
                Descricao = "Qualquer"
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, tipoInvalido.Error.Code);

        var envolveInvalido = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Qualquer",
                EnvolveIa = "Talvez"
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, envolveInvalido.Error.Code);
    }

    [Fact]
    public async Task Legado_SistemaDeOutroOrgaoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistemaDaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Contrato com IA",
                EnvolveIa = PgiaDominios.EnvolveIa.Sim,
                SistemaIaId = sistemaDaSeec.Id
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Legado_ListagemSeparaOsOrgaos()
    {
        await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Da SES"
        }, await CtxAsync(UserOrgaoSes.Email));
        await _service.CriarLegadoAsync(OrgaoSeec.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Da SEEC"
        }, await CtxAsync(UserOrgaoSeec.Email));

        var daSes = await _service.ListarLegadosPorOrgaoAsync(OrgaoSes.Id);
        Assert.Single(daSes);
        Assert.Equal("Da SES", daSes[0].Descricao);
    }

    [Fact]
    public async Task Legado_RevisaoComIaExigeOAditivoDoArt21()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var semAditivo = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Contrato de chatbot",
                EnvolveIa = PgiaDominios.EnvolveIa.Sim,
                Revisado = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, semAditivo.Error.Code);
        Assert.Contains("aditivo", semAditivo.Error.Message);

        // "Incerto" é tratado como sim até confirmação: também exige a resposta
        var incerto = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Contrato suspeito",
                EnvolveIa = PgiaDominios.EnvolveIa.Incerto,
                Revisado = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, incerto.Error.Code);

        // Sem IA, a revisão não depende de aditivo
        var semIa = await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Ato normativo",
            Descricao = "Portaria sem IA",
            EnvolveIa = "Não",
            Revisado = true
        }, ctx);
        Assert.True(semIa.Revisado);
    }

    [Fact]
    public async Task Legado_AditivoConfirmadoExigeData()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Contrato de chatbot",
                EnvolveIa = PgiaDominios.EnvolveIa.Sim,
                Revisado = true,
                AditivoClausulaTreinamento = true
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Contains("data do termo aditivo", ex.Error.Message);
    }

    [Fact]
    public async Task Legado_TriadoPorEhPreservadoNaEdicaoSemMudancaDeData()
    {
        var maria = await CtxAsync(UserOrgaoSes.Email);
        var carlos = await CtxAsync(UserSemPapel.Email); // outra pessoa da mesma unidade

        var legado = await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 11, 20)
        }, maria);

        Assert.Equal("Maria Andrade", legado.TriadoPorNome);

        // Carlos edita sem mexer na data da triagem: a autoria continua da Maria
        await _service.AtualizarLegadoAsync(legado.Id, new PgiaLegadoUpdateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot corporativo",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 11, 20)
        }, carlos);

        var mantido = await Context.PgiaInstrumentosLegados.FirstAsync(l => l.Id == legado.Id);
        Assert.Equal(UserOrgaoSes.Id, mantido.TriadoPor);

        // Nova triagem, novo responsável
        await _service.AtualizarLegadoAsync(legado.Id, new PgiaLegadoUpdateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot corporativo",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 12, 1)
        }, carlos);

        var retriado = await Context.PgiaInstrumentosLegados.FirstAsync(l => l.Id == legado.Id);
        Assert.Equal(UserSemPapel.Id, retriado.TriadoPor);

        // Triagem desfeita, autoria some junto
        await _service.AtualizarLegadoAsync(legado.Id, new PgiaLegadoUpdateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot corporativo",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim
        }, carlos);

        var semTriagem = await Context.PgiaInstrumentosLegados.FirstAsync(l => l.Id == legado.Id);
        Assert.Null(semTriagem.TriadoPor);
    }

    [Fact]
    public async Task Legado_DocumentoDeComprovacaoDeOutroOrgaoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var documentoDaSeec = await NovoDocumentoAsync(OrgaoSeec.Id, UserOrgaoSeec.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
            {
                TipoInstrumento = "Contrato",
                Descricao = "Contrato de chatbot",
                ComprovacaoDocId = documentoDaSeec
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, ex.Error.Code);
        Assert.Contains("outro órgão", ex.Error.Message);
    }

    [Fact]
    public async Task Legado_RevisaoAtualizaOsCamposDoAditivo()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var legado = await _service.CriarLegadoAsync(OrgaoSes.Id, new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 11, 20)
        }, ctx);

        var revisado = await _service.AtualizarLegadoAsync(legado.Id, new PgiaLegadoUpdateDTO
        {
            TipoInstrumento = "Contrato",
            Descricao = "Contrato de chatbot",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 11, 20),
            Revisado = true,
            DataRevisao = new DateOnly(2026, 12, 15),
            AditivoClausulaTreinamento = true,
            DataAditivo = new DateOnly(2026, 12, 20)
        }, ctx);

        Assert.True(revisado.Revisado);
        Assert.True(revisado.AditivoClausulaTreinamento);
        Assert.Equal(new DateOnly(2026, 12, 20), revisado.DataAditivo);
    }
}
