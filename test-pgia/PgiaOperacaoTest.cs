using api.Common;
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
/// Operação contínua (fase 3): aviso e comunicação de incidentes, apuração da SGDI,
/// não conformidades, trilhas ProCapIA/DF e registro de uso de IA (art. 13).
/// </summary>
public class PgiaOperacaoTest : PgiaTestBase
{
    private readonly PgiaOperacaoService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaPermissionService _permissionService;

    public PgiaOperacaoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaOperacaoService(new PgiaOperacaoRepositorio(Context));
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

    private async Task<PgiaSistemaResponse> NovoSistemaAsync(long orgaoId, string email, string denominacao)
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
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Sem enquadramento nos arts. 15 a 17."
            }
        }, ctx);
    }

    private async Task<PgiaPlataformaIaGenerativa> NovaPlataformaAsync(string nome, string status)
    {
        var plataforma = new PgiaPlataformaIaGenerativa
        {
            Nome = nome,
            StatusHomologacao = status,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaPlataformasIaGenerativa.Add(plataforma);
        await Context.SaveChangesAsync();
        return plataforma;
    }

    private static PgiaIncidenteComunicarDTO ComunicarDto() => new()
    {
        Hipotese = "IV",
        DataComunicacaoSgdi = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
        ProcessoSei = "00060-00077777/2026-11",
        MedidasAdotadas = "Acesso revogado e senha rotacionada."
    };

    // ── Aviso do agente (art. 13, II) ─────────────────────────────────────────

    [Fact]
    public async Task Aviso_UsuarioSemPapelComOrgaoPodeAvisar()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        // Carlos Comum: perfil gestor do SGDP, sem PapelPgia, lotado na unidade da SES
        var ctx = await CtxAsync(UserSemPapel.Email);

        Assert.True(_permissionService.PodeRegistrarUso(ctx));
        Assert.Equal(string.Empty, ctx.PapelEfetivo);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "O sistema devolveu dado de outro cidadão."
        }, ctx);

        Assert.Equal(OrgaoSes.Id, aviso.OrgaoId);
        Assert.Equal("Carlos Comum", aviso.ComunicadoPorNome);
        Assert.Null(aviso.Hipotese);
        Assert.Null(aviso.DataComunicacaoSgdi);
        Assert.Null(aviso.ProcessoSei);
        Assert.Equal(PgiaDominios.StatusApuracao.Recebida, aviso.StatusApuracao);
        Assert.NotEqual(default, aviso.NotificadoResponsavelEm);
    }

    [Fact]
    public async Task Aviso_UsuarioSemOrgaoResolvidoEhRejeitado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        // Alice Auditora não tem unidade: o órgão não se resolve
        var ctx = await CtxAsync(UserAuditoria.Email);

        Assert.False(_permissionService.PodeRegistrarUso(ctx));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
            {
                SistemaIaId = sistema.Id,
                Descricao = "Qualquer coisa."
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSemOrgaoResolvido, ex.Error.Code);
    }

    [Fact]
    public async Task Aviso_SistemaDeOutroOrgaoEhRejeitado()
    {
        var sistemaDaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");
        var ctx = await CtxAsync(UserSemPapel.Email); // lotado na SES

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
            {
                SistemaIaId = sistemaDaSeec.Id,
                Descricao = "Sistema de outro órgão."
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Aviso_DescricaoVaziaEhRejeitada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
            {
                SistemaIaId = sistema.Id,
                Descricao = "   "
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Aviso_HipoteseForaDoDominioEhRejeitada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
            {
                SistemaIaId = sistema.Id,
                Descricao = "Vazamento.",
                Hipotese = "VII"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Comunicação formal (art. 30) ──────────────────────────────────────────

    [Fact]
    public async Task Comunicar_CompletaOAvisoComHipoteseSeiEData()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var agenteCtx = await CtxAsync(UserSemPapel.Email);
        var responsavelCtx = await CtxAsync(UserOrgaoSes.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento de dados de atendimento."
        }, agenteCtx);

        var comunicado = await _service.ComunicarAsync(aviso.Id, ComunicarDto(), responsavelCtx);

        Assert.Equal("IV", comunicado.Hipotese);
        Assert.Equal("00060-00077777/2026-11", comunicado.ProcessoSei);
        Assert.NotNull(comunicado.DataComunicacaoSgdi);
        Assert.Equal("Acesso revogado e senha rotacionada.", comunicado.MedidasAdotadas);

        var salvo = await Context.PgiaIncidentes.FirstAsync(i => i.Id == aviso.Id);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);
    }

    [Fact]
    public async Task Comunicar_CorrigeAsDatasAproximadasDoAviso()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var agenteCtx = await CtxAsync(UserSemPapel.Email);
        var responsavelCtx = await CtxAsync(UserOrgaoSes.Email);

        var aproximada = new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc);
        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento.",
            DataOcorrencia = aproximada,
            DataDeteccao = aproximada
        }, agenteCtx);

        var dto = ComunicarDto();
        dto.DataOcorrencia = new DateTime(2026, 11, 17, 22, 30, 0, DateTimeKind.Utc);
        dto.DataDeteccao = new DateTime(2026, 11, 18, 7, 15, 0, DateTimeKind.Utc);

        var comunicado = await _service.ComunicarAsync(aviso.Id, dto, responsavelCtx);

        Assert.Equal(new DateTime(2026, 11, 17, 22, 30, 0, DateTimeKind.Utc), comunicado.DataOcorrencia);
        Assert.Equal(new DateTime(2026, 11, 18, 7, 15, 0, DateTimeKind.Utc), comunicado.DataDeteccao);
    }

    [Fact]
    public async Task Comunicar_SemDatasNovasPreservaAsDoAviso()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var informada = new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc);
        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento.",
            DataOcorrencia = informada,
            DataDeteccao = informada
        }, ctx);

        var comunicado = await _service.ComunicarAsync(aviso.Id, ComunicarDto(), ctx);

        Assert.Equal(informada, comunicado.DataOcorrencia);
        Assert.Equal(informada, comunicado.DataDeteccao);
    }

    [Fact]
    public async Task Comunicar_SegundaComunicacaoEhBloqueada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento."
        }, ctx);
        await _service.ComunicarAsync(aviso.Id, ComunicarDto(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ComunicarAsync(aviso.Id, ComunicarDto(), ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Contains("já foi comunicado", ex.Error.Message);
    }

    [Fact]
    public async Task Comunicar_SemHipoteseOuSeiEhRejeitada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento."
        }, ctx);

        var semHipotese = ComunicarDto();
        semHipotese.Hipotese = string.Empty;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ComunicarAsync(aviso.Id, semHipotese, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex1.Error.Code);

        var semSei = ComunicarDto();
        semSei.ProcessoSei = "  ";
        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ComunicarAsync(aviso.Id, semSei, ctx));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex2.Error.Code);
    }

    [Fact]
    public async Task IncidenteFormal_NasceComunicadoEEntraNaFilaDaSgdi()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var incidente = await _service.CriarIncidenteFormalAsync(new PgiaIncidenteCreateDTO
        {
            SistemaIaId = sistema.Id,
            Hipotese = "I",
            Descricao = "Recomendação clínica equivocada.",
            DataOcorrencia = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc),
            DataDeteccao = new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc),
            NotificadoResponsavelEm = new DateTime(2026, 11, 18, 16, 0, 0, DateTimeKind.Utc),
            DataComunicacaoSgdi = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc),
            ProcessoSei = "00060-00088888/2026-11"
        }, ctx);

        Assert.NotNull(incidente.DataComunicacaoSgdi);
        Assert.Equal("Assistente 156", incidente.SistemaDenominacao);
        Assert.Equal("SES", incidente.OrgaoSigla);

        var fila = await _service.ListarComunicadosAsync(null);
        Assert.Single(fila);
    }

    [Fact]
    public async Task IncidenteFormal_DatasAusentesSaoRejeitadas()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarIncidenteFormalAsync(new PgiaIncidenteCreateDTO
            {
                SistemaIaId = sistema.Id,
                Hipotese = "I",
                Descricao = "Sem datas.",
                DataComunicacaoSgdi = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc),
                ProcessoSei = "00060-00088888/2026-11"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Apuração pela SGDI (art. 30, § único) ─────────────────────────────────

    [Fact]
    public async Task Apuracao_ExigeComunicacaoPrevia()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Ainda é só um aviso interno."
        }, orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ApurarAsync(aviso.Id, new PgiaIncidenteApuracaoDTO
            {
                StatusApuracao = "Em apuração"
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaIncidenteNaoComunicado, ex.Error.Code);
    }

    [Fact]
    public async Task Apuracao_GravaRecomendacoesEStatus()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento."
        }, orgaoCtx);
        await _service.ComunicarAsync(aviso.Id, ComunicarDto(), orgaoCtx);

        var apurado = await _service.ApurarAsync(aviso.Id, new PgiaIncidenteApuracaoDTO
        {
            StatusApuracao = "Concluída com recomendações",
            RecomendacoesSgdi = "Revisar o controle de acesso.",
            PropostaCgtic = false
        }, sgdiCtx);

        Assert.Equal("Concluída com recomendações", apurado.StatusApuracao);
        Assert.Equal("Revisar o controle de acesso.", apurado.RecomendacoesSgdi);
        Assert.False(apurado.SistemaSuspenso);

        var salvo = await Context.PgiaIncidentes.FirstAsync(i => i.Id == aviso.Id);
        Assert.Equal(UserSgdi.Email, salvo.AlteradoPor);
    }

    [Fact]
    public async Task Apuracao_SuspensaoExigeData()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento."
        }, orgaoCtx);
        await _service.ComunicarAsync(aviso.Id, ComunicarDto(), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ApurarAsync(aviso.Id, new PgiaIncidenteApuracaoDTO
            {
                StatusApuracao = "Encaminhada ao CGTIC",
                SistemaSuspenso = true
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Apuracao_StatusForaDoDominioEhRejeitado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var aviso = await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistema.Id,
            Descricao = "Vazamento."
        }, orgaoCtx);
        await _service.ComunicarAsync(aviso.Id, ComunicarDto(), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ApurarAsync(aviso.Id, new PgiaIncidenteApuracaoDTO
            {
                StatusApuracao = "Arquivada sem análise"
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Listagem_IncidentesSaoSeparadosPorOrgao()
    {
        var sistemaSes = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var sistemaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");

        await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistemaSes.Id,
            Descricao = "Incidente da SES."
        }, await CtxAsync(UserOrgaoSes.Email));
        await _service.CriarAvisoAsync(new PgiaIncidenteAvisoDTO
        {
            SistemaIaId = sistemaSeec.Id,
            Descricao = "Incidente da SEEC."
        }, await CtxAsync(UserOrgaoSeec.Email));

        var daSes = await _service.ListarIncidentesPorOrgaoAsync(OrgaoSes.Id);
        Assert.Single(daSes);
        Assert.Equal("Incidente da SES.", daSes[0].Descricao);

        // Nenhum foi comunicado ainda: a fila central está vazia
        Assert.Empty(await _service.ListarComunicadosAsync(null));
    }

    // ── Não conformidades ─────────────────────────────────────────────────────

    [Fact]
    public async Task NaoConformidade_OrgaoTemOrigemEOrgaoForcados()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var nc = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            // Tenta apontar outro órgão e outra origem: ambos são ignorados
            OrgaoId = OrgaoSeec.Id,
            Origem = PgiaDominios.OrigemNaoConformidade.Sgdi,
            Descricao = "Inventário desatualizado."
        }, ctx);

        Assert.Equal(OrgaoSes.Id, nc.OrgaoId);
        Assert.Equal(PgiaDominios.OrigemNaoConformidade.Sgtic, nc.Origem);
        Assert.Equal(PgiaDominios.SituacaoNaoConformidade.Registrada, nc.Situacao);
        Assert.NotEqual(default, nc.DataRegistro);
    }

    [Fact]
    public async Task NaoConformidade_SgdiRegistraParaOutroOrgao()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var nc = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            OrgaoId = OrgaoSes.Id,
            Origem = PgiaDominios.OrigemNaoConformidade.Sgdi,
            Descricao = "Sistema em uso sem homologação."
        }, ctx);

        Assert.Equal(OrgaoSes.Id, nc.OrgaoId);
        Assert.Equal("SES", nc.OrgaoSigla);
        Assert.Equal(PgiaDominios.OrigemNaoConformidade.Sgdi, nc.Origem);
    }

    [Fact]
    public async Task NaoConformidade_SgdiNaoConsegueGravarOrigemDoSgtic()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var nc = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            OrgaoId = OrgaoSes.Id,
            // Tenta se passar por achado do próprio órgão: a origem é forçada
            Origem = PgiaDominios.OrigemNaoConformidade.Sgtic,
            Descricao = "Sistema em uso sem homologação."
        }, ctx);

        Assert.Equal(PgiaDominios.OrigemNaoConformidade.Sgdi, nc.Origem);
    }

    [Fact]
    public async Task NaoConformidade_OrgaoNaoEditaAchadoDaSupervisao()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);

        var daSupervisao = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            OrgaoId = OrgaoSes.Id,
            Descricao = "Sistema em uso sem homologação."
        }, sgdiCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarNaoConformidadeAsync(daSupervisao.Id, new PgiaNaoConformidadeUpdateDTO
            {
                Descricao = "Sistema em uso sem homologação.",
                Situacao = "Sanada"
            }, orgaoCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Contains("supervisão da SGDI", ex.Error.Message);

        // A SGDI encerra a sua própria
        var sanada = await _service.AtualizarNaoConformidadeAsync(daSupervisao.Id, new PgiaNaoConformidadeUpdateDTO
        {
            Descricao = "Sistema em uso sem homologação.",
            Situacao = "Sanada"
        }, sgdiCtx);
        Assert.Equal("Sanada", sanada.Situacao);
    }

    [Fact]
    public async Task NaoConformidade_OrgaoSegueEditandoOsPropriosAchados()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);

        var doOrgao = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            Descricao = "Inventário desatualizado."
        }, orgaoCtx);

        var tratada = await _service.AtualizarNaoConformidadeAsync(doOrgao.Id, new PgiaNaoConformidadeUpdateDTO
        {
            Descricao = "Inventário desatualizado.",
            Situacao = "Em tratamento"
        }, orgaoCtx);

        Assert.Equal("Em tratamento", tratada.Situacao);
    }

    [Fact]
    public async Task NaoConformidade_SgdiSemOrgaoNoCorpoEhRejeitada()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
            {
                Origem = PgiaDominios.OrigemNaoConformidade.Sgdi,
                Descricao = "Sem órgão."
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task NaoConformidade_AtualizacaoValidaASituacao()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var nc = await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            Descricao = "Inventário desatualizado."
        }, orgaoCtx);

        var atualizada = await _service.AtualizarNaoConformidadeAsync(nc.Id, new PgiaNaoConformidadeUpdateDTO
        {
            Descricao = "Inventário desatualizado.",
            Situacao = "Sanada",
            ReportadaSgdiEm = new DateOnly(2026, 11, 20)
        }, sgdiCtx);

        Assert.Equal("Sanada", atualizada.Situacao);
        Assert.Equal(new DateOnly(2026, 11, 20), atualizada.ReportadaSgdiEm);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarNaoConformidadeAsync(nc.Id, new PgiaNaoConformidadeUpdateDTO
            {
                Descricao = "Inventário desatualizado.",
                Situacao = "Resolvida na conversa"
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task NaoConformidade_ListagensSeparamOrgaoETotal()
    {
        await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            Descricao = "Da SES."
        }, await CtxAsync(UserOrgaoSes.Email));
        await _service.CriarNaoConformidadeAsync(new PgiaNaoConformidadeCreateDTO
        {
            Descricao = "Da SEEC."
        }, await CtxAsync(UserOrgaoSeec.Email));

        Assert.Single(await _service.ListarNaoConformidadesPorOrgaoAsync(OrgaoSes.Id));
        Assert.Equal(2, (await _service.ListarNaoConformidadesAsync()).Count);
    }

    // ── Capacitação ProCapIA/DF ───────────────────────────────────────────────

    [Fact]
    public async Task Capacitacao_ParAgenteTrilhaEhUnico()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        await _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
        {
            AgenteId = UserOrgaoSes.Id,
            Trilha = "Governança de IA",
            PrevistaPlanoCapacitacao = true
        }, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
            {
                AgenteId = UserOrgaoSes.Id,
                Trilha = "Governança de IA"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaCapacitacaoJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task Capacitacao_ConcluidaExigeDataDeConclusao()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
            {
                AgenteId = UserOrgaoSes.Id,
                Trilha = "Letramento em IA",
                Status = PgiaDominios.StatusCapacitacao.Concluida
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Capacitacao_PrevistaNaoGuardaDataDeConclusao()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var capacitacao = await _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
        {
            AgenteId = UserOrgaoSes.Id,
            Trilha = "Letramento em IA",
            Status = "Prevista",
            // Data enviada por engano: só vale na trilha concluída
            DataConclusao = new DateOnly(2026, 12, 10)
        }, ctx);

        Assert.Equal("Prevista", capacitacao.Status);
        Assert.Null(capacitacao.DataConclusao);

        var salva = await Context.PgiaCapacitacoes.SingleAsync();
        Assert.Null(salva.DataConclusao);
    }

    [Fact]
    public async Task Capacitacao_AgenteDeOutraUnidadeEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
            {
                AgenteId = UserOrgaoSeec.Id,
                Trilha = "Letramento em IA"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaAgenteDeOutroOrgao, ex.Error.Code);
    }

    [Fact]
    public async Task Capacitacao_TrilhaForaDoDominioEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
            {
                AgenteId = UserOrgaoSes.Id,
                Trilha = "Curso de Excel"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Capacitacao_ConclusaoAtualizaStatusEData()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var capacitacao = await _service.CriarCapacitacaoAsync(OrgaoSes.Id, new PgiaCapacitacaoCreateDTO
        {
            AgenteId = UserOrgaoSes.Id,
            Trilha = "Uso responsável de IA",
            PrevistaPlanoCapacitacao = true
        }, ctx);

        Assert.Equal("Prevista", capacitacao.Status);
        Assert.Equal("Maria Andrade", capacitacao.AgenteNome);

        var concluida = await _service.AtualizarCapacitacaoAsync(capacitacao.Id, new PgiaCapacitacaoUpdateDTO
        {
            AgenteId = UserOrgaoSes.Id,
            Trilha = "Uso responsável de IA",
            Status = PgiaDominios.StatusCapacitacao.Concluida,
            DataConclusao = new DateOnly(2026, 12, 10),
            PrevistaPlanoCapacitacao = true
        }, ctx);

        Assert.Equal(PgiaDominios.StatusCapacitacao.Concluida, concluida.Status);
        Assert.Equal(new DateOnly(2026, 12, 10), concluida.DataConclusao);

        var lista = await _service.ListarCapacitacoesPorOrgaoAsync(OrgaoSes.Id);
        Assert.Single(lista);
    }

    // ── Registro de uso de IA (art. 13, IV e V) ───────────────────────────────

    [Fact]
    public async Task Uso_ExigeExatamenteUmaFonte()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var plataforma = await NovaPlataformaAsync("Assistente GDF", "Homologada");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var semFonte = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                ProdutoRef = "Ofício 12/2026",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaRegistroUsoInvalido, semFonte.Error.Code);

        var duasFontes = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                PlataformaId = plataforma.Id,
                ProdutoRef = "Ofício 12/2026",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaRegistroUsoInvalido, duasFontes.Error.Code);
    }

    [Fact]
    public async Task Uso_RevisaoHumanaNaoConfirmadaEhRejeitada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                ProdutoRef = "Ofício 12/2026",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = false
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaRevisaoHumanaObrigatoria, ex.Error.Code);
        Assert.Contains("art. 13, V", ex.Error.Message);
    }

    [Fact]
    public async Task Uso_PlataformaNaoHomologadaEhRejeitada()
    {
        var plataforma = await NovaPlataformaAsync("Plataforma X", "Em avaliação");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                PlataformaId = plataforma.Id,
                ProdutoRef = "Minuta de parecer",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaPlataformaNaoHomologada, ex.Error.Code);
        Assert.Contains("art. 20", ex.Error.Message);
    }

    [Theory]
    [InlineData("Homologada")]
    [InlineData("Homologada apta a dados pessoais e sigilosos")]
    public async Task Uso_PlataformaHomologadaEhAceita(string status)
    {
        var plataforma = await NovaPlataformaAsync("Plataforma " + status, status);
        var ctx = await CtxAsync(UserSemPapel.Email);

        var uso = await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
        {
            PlataformaId = plataforma.Id,
            ProdutoRef = "Minuta de parecer",
            DataUso = new DateOnly(2026, 11, 20),
            RevisaoHumanaConfirmada = true
        }, ctx);

        Assert.Equal(plataforma.Id, uso.PlataformaId);
        Assert.Null(uso.SistemaIaId);
        Assert.Equal(OrgaoSes.Id, uso.OrgaoId);
        Assert.Equal("Carlos Comum", uso.AgenteNome);
    }

    [Fact]
    public async Task Uso_SistemaDeOutroOrgaoEhRejeitado()
    {
        var sistemaDaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");
        var ctx = await CtxAsync(UserSemPapel.Email); // lotado na SES

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistemaDaSeec.Id,
                ProdutoRef = "Ofício 12/2026",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Uso_SemOrgaoResolvidoEhRejeitado()
    {
        var plataforma = await NovaPlataformaAsync("Assistente GDF", "Homologada");
        var ctx = await CtxAsync(UserAuditoria.Email); // sem unidade

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                PlataformaId = plataforma.Id,
                ProdutoRef = "Minuta",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSemOrgaoResolvido, ex.Error.Code);
    }

    [Fact]
    public async Task Uso_ProdutoEDataSaoObrigatorios()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        var semProduto = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                ProdutoRef = "   ",
                DataUso = new DateOnly(2026, 11, 20),
                RevisaoHumanaConfirmada = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaRegistroUsoInvalido, semProduto.Error.Code);

        var semData = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                ProdutoRef = "Ofício 12/2026",
                RevisaoHumanaConfirmada = true
            }, ctx));
        Assert.Equal((int)ErrorCode.PgiaRegistroUsoInvalido, semData.Error.Code);
    }

    [Fact]
    public async Task Uso_ListagemSeparaOAgenteEOOrgao()
    {
        var sistemaSes = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var sistemaSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");

        var carlosCtx = await CtxAsync(UserSemPapel.Email);   // SES
        var mariaCtx = await CtxAsync(UserOrgaoSes.Email);    // SES
        var joaoCtx = await CtxAsync(UserOrgaoSeec.Email);    // SEEC

        await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
        {
            SistemaIaId = sistemaSes.Id,
            ProdutoRef = "Ofício do Carlos",
            DataUso = new DateOnly(2026, 11, 20),
            RevisaoHumanaConfirmada = true
        }, carlosCtx);
        await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
        {
            SistemaIaId = sistemaSes.Id,
            ProdutoRef = "Ofício da Maria",
            DataUso = new DateOnly(2026, 11, 21),
            RevisaoHumanaConfirmada = true
        }, mariaCtx);
        await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
        {
            SistemaIaId = sistemaSeec.Id,
            ProdutoRef = "Nota da SEEC",
            DataUso = new DateOnly(2026, 11, 22),
            RevisaoHumanaConfirmada = true
        }, joaoCtx);

        var meusDoCarlos = await _service.ListarMeusUsosAsync(carlosCtx, new PagedRequest());
        Assert.Equal(1, meusDoCarlos.TotalItems);
        Assert.Equal("Ofício do Carlos", meusDoCarlos.Items[0].ProdutoRef);

        var daSes = await _service.ListarUsosPorOrgaoAsync(OrgaoSes.Id, new PagedRequest());
        Assert.Equal(2, daSes.TotalItems);
        Assert.DoesNotContain(daSes.Items, u => u.ProdutoRef == "Nota da SEEC");

        var daSeec = await _service.ListarUsosPorOrgaoAsync(OrgaoSeec.Id, new PagedRequest());
        Assert.Equal(1, daSeec.TotalItems);
    }

    [Fact]
    public async Task Uso_ListagemPaginaEOrdenaPorDataDesc()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        foreach (var dia in new[] { 10, 11, 12, 13, 14 })
        {
            await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                ProdutoRef = $"Ofício do dia {dia}",
                DataUso = new DateOnly(2026, 11, dia),
                RevisaoHumanaConfirmada = true
            }, ctx);
        }

        var primeira = await _service.ListarMeusUsosAsync(ctx, new PagedRequest { Page = 1, PageSize = 2 });
        Assert.Equal(5, primeira.TotalItems);
        Assert.Equal(3, primeira.TotalPages);
        Assert.Equal(2, primeira.Items.Count);
        // Mais recente primeiro
        Assert.Equal("Ofício do dia 14", primeira.Items[0].ProdutoRef);
        Assert.Equal("Ofício do dia 13", primeira.Items[1].ProdutoRef);

        var segunda = await _service.ListarMeusUsosAsync(ctx, new PagedRequest { Page = 2, PageSize = 2 });
        Assert.Equal("Ofício do dia 12", segunda.Items[0].ProdutoRef);

        // Página zerada cai na primeira, como no inventário
        var saneada = await _service.ListarUsosPorOrgaoAsync(OrgaoSes.Id, new PagedRequest { Page = 0, PageSize = 2 });
        Assert.Equal(1, saneada.CurrentPage);
        Assert.Equal("Ofício do dia 14", saneada.Items[0].ProdutoRef);
    }

    [Fact]
    public async Task Uso_RespostaDoPostVemCompletaSemRelerOHistorico()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var ctx = await CtxAsync(UserSemPapel.Email);

        // Histórico já povoado: a resposta do POST não deve depender dele
        for (var dia = 1; dia <= 5; dia++)
        {
            await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
            {
                SistemaIaId = sistema.Id,
                ProdutoRef = $"Anterior {dia}",
                DataUso = new DateOnly(2026, 11, dia),
                RevisaoHumanaConfirmada = true
            }, ctx);
        }

        var uso = await _service.CriarUsoAsync(new PgiaRegistroUsoCreateDTO
        {
            SistemaIaId = sistema.Id,
            ProdutoRef = "Ofício mais novo",
            ProcessoSei = "00060-00012121/2026-11",
            DataUso = new DateOnly(2026, 11, 25),
            RevisaoHumanaConfirmada = true
        }, ctx);

        Assert.NotEqual(0, uso.Id);
        Assert.Equal("Ofício mais novo", uso.ProdutoRef);
        Assert.Equal("00060-00012121/2026-11", uso.ProcessoSei);
        Assert.Equal("Carlos Comum", uso.AgenteNome);
        Assert.Equal("Assistente 156", uso.SistemaDenominacao);
        Assert.Equal(OrgaoSes.Id, uso.OrgaoId);
        Assert.True(uso.RevisaoHumanaConfirmada);
        Assert.NotEqual(default, uso.CriadoEm);
    }

    // ── Fontes do registro de uso (art. 13) ───────────────────────────────────

    [Fact]
    public async Task Fontes_UsuarioSemPapelRecebeSistemasDoOrgaoEPlataformasHomologadas()
    {
        await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");
        await NovaPlataformaAsync("Homologada simples", "Homologada");
        await NovaPlataformaAsync("Homologada plena", "Homologada apta a dados pessoais e sigilosos");
        await NovaPlataformaAsync("Em avaliação", "Em avaliação");
        await NovaPlataformaAsync("Revogada", "Homologação revogada");
        await NovaPlataformaAsync("Recusada", "Não homologada");

        var ctx = await CtxAsync(UserSemPapel.Email); // sem papel PGIA, lotado na SES

        var fontes = await _service.ListarFontesDeUsoAsync(ctx);

        Assert.Equal(OrgaoSes.Id, fontes.OrgaoId);
        Assert.Equal("SES", fontes.OrgaoSigla);

        Assert.Single(fontes.Sistemas);
        Assert.Equal("Assistente 156", fontes.Sistemas[0].Denominacao);
        Assert.DoesNotContain(fontes.Sistemas, s => s.Denominacao == "Classificador");

        Assert.Equal(2, fontes.Plataformas.Count);
        Assert.Contains(fontes.Plataformas, p => p.Nome == "Homologada simples");
        Assert.Contains(fontes.Plataformas, p => p.Nome == "Homologada plena");
        Assert.DoesNotContain(fontes.Plataformas, p => p.Nome == "Revogada");
        Assert.DoesNotContain(fontes.Plataformas, p => p.Nome == "Recusada");
        Assert.DoesNotContain(fontes.Plataformas, p => p.Nome == "Em avaliação");
    }

    [Fact]
    public async Task Fontes_UsuarioSemOrgaoRecebeListaDeSistemasVazia()
    {
        await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await NovaPlataformaAsync("Homologada simples", "Homologada");

        var ctx = await CtxAsync(UserAuditoria.Email); // sem unidade, sem órgão resolvido

        var fontes = await _service.ListarFontesDeUsoAsync(ctx);

        Assert.Null(fontes.OrgaoId);
        Assert.Null(fontes.OrgaoSigla);
        Assert.Empty(fontes.Sistemas);
    }
}
