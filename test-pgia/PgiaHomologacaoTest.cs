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
/// Fluxo de homologação do inventário (fase 2): rota SGDI/CGTIC pela classificação,
/// avaliação central, gate de implantação e AIA dos sistemas de Alto Risco.
/// </summary>
public class PgiaHomologacaoTest : PgiaTestBase
{
    private readonly PgiaSistemaService _service;
    private readonly PgiaGovernancaService _governanca;
    private readonly PgiaPermissionService _permissionService;

    public PgiaHomologacaoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);
        _governanca = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));

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

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email))!;

    private static PgiaClassificacaoCreateDTO Checklist(
        List<string>? q15 = null, List<string>? q16 = null, List<string>? q17 = null) => new()
    {
        Checklist = ChecklistRespondido(new PgiaChecklistDTO
        {
            Q15 = q15 ?? new List<string>(),
            Q16 = q16 ?? new List<string>(),
            Q17 = q17 ?? new List<string>()
        }),
        OutrosRiscos = OutrosRiscosSeNecessario(q15, q16, q17),
        Motivo = "Classificação inicial",
        DataClassificacao = new DateOnly(2026, 9, 1),
        Justificativa = "Enquadramento avaliado pelo Responsável de IA."
    };

    private static PgiaSistemaCreateDTO NovoSistemaDto(string denominacao = "Assistente virtual 156") => new()
    {
        Denominacao = denominacao,
        Finalidade = "Apoiar o atendimento ao cidadão",
        OrigemRegistro = "Nova iniciativa",
        TipoSistema = "Desenvolvido internamente",
        Tecnologia = "IA generativa",
        StatusCicloVida = "Planejamento",
        EscopoDados = "Somente dados públicos",
        AfetaCidadao = false,
        InteroperavelPadroesSgdi = true,
        Classificacao = Checklist()
    };

    private static PgiaSistemaUpdateDTO ParaUpdate(PgiaSistemaCreateDTO dto, string status, DateOnly? implantacao) => new()
    {
        Denominacao = dto.Denominacao,
        Finalidade = dto.Finalidade,
        OrigemRegistro = dto.OrigemRegistro,
        TipoSistema = dto.TipoSistema,
        Tecnologia = dto.Tecnologia,
        StatusCicloVida = status,
        DataImplantacao = implantacao,
        EscopoDados = dto.EscopoDados,
        AfetaCidadao = dto.AfetaCidadao,
        InteroperavelPadroesSgdi = dto.InteroperavelPadroesSgdi,
        SupervisaoHumanaDescricao = dto.SupervisaoHumanaDescricao,
        AvisoInteracaoIa = dto.AvisoInteracaoIa
    };

    private static PgiaAiaCreateDTO NovaAiaDto(string status) => new()
    {
        Status = status,
        DataInicio = new DateOnly(2026, 9, 10),
        DataConclusao = status == PgiaDominios.StatusAia.Concluida ? new DateOnly(2026, 10, 1) : null,
        ProximaRevisao = status == PgiaDominios.StatusAia.Concluida ? new DateOnly(2027, 10, 1) : null,
        ImpactosDireitosFundamentais = "Risco de viés na priorização de atendimentos.",
        MedidasPreventivas = "Curadoria da base e revisão de features.",
        MedidasMitigadoras = "Revisão humana das decisões negativas.",
        MedidasReversao = "Desligamento do modelo e reprocessamento manual."
    };

    // ── Rota inicial pela classificação ───────────────────────────────────────

    [Fact]
    public async Task RotaInicial_BaixoRiscoVaiParaASgdi()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoSgdi, sistema.SituacaoHomologacao);
    }

    [Fact]
    public async Task RotaInicial_RiscoModeradoVaiParaASgdi()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q17: new List<string> { "I" });
        dto.AvisoInteracaoIa = true;

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, sistema.ClassificacaoRiscoAtual);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoSgdi, sistema.SituacaoHomologacao);
    }

    [Fact]
    public async Task RotaInicial_AltoRiscoVaiParaOCgtic()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q16: new List<string> { "I" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão.";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, sistema.SituacaoHomologacao);
    }

    [Fact]
    public async Task RotaInicial_RiscoExcessivoVaiParaOCgtic()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q15: new List<string> { "I" });

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, sistema.SituacaoHomologacao);
    }

    [Fact]
    public async Task Reclassificacao_ReabreAHomologacaoNaNovaRota()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);
        await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = true,
            Parecer = "Sem óbice."
        }, sgdiCtx);

        // Passa a Alto Risco: a aprovação anterior perde validade e o caso vai ao comitê
        var nova = Checklist(q16: new List<string> { "II" });
        nova.Motivo = "Revisão periódica";
        await _service.ReclassificarAsync(sistema.Id, nova, orgaoCtx);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, salvo.SituacaoHomologacao);
        Assert.Null(salvo.AvaliacaoParecer);
        Assert.Null(salvo.AvaliadoPor);
        Assert.Null(salvo.AvaliadoEm);
        Assert.Null(salvo.DeliberacaoHomologacaoId);
    }

    // ── Avaliação da SGDI ─────────────────────────────────────────────────────

    [Fact]
    public async Task Avaliacao_SgdiAprovaSistemaDaSuaRota()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);
        var avaliado = await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = true,
            Parecer = "Baixo risco, sem óbice à adoção."
        }, sgdiCtx);

        Assert.Equal(PgiaDominios.SituacaoHomologacao.Aprovado, avaliado.SituacaoHomologacao);
        Assert.Equal("Baixo risco, sem óbice à adoção.", avaliado.AvaliacaoParecer);
        Assert.Equal("Ana SGDI", avaliado.AvaliadoPorNome);
        Assert.NotNull(avaliado.AvaliadoEm);
    }

    [Fact]
    public async Task Avaliacao_SgdiVetaComParecer()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);
        var avaliado = await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = false,
            Parecer = "Finalidade não demonstrada."
        }, sgdiCtx);

        Assert.Equal(PgiaDominios.SituacaoHomologacao.Vetado, avaliado.SituacaoHomologacao);
    }

    [Fact]
    public async Task Avaliacao_SgdiNaoDecideCasoDelegadoAoCgtic()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q16: new List<string> { "I" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão.";
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
            {
                Aprovado = true,
                Parecer = "Tentativa indevida."
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaHomologacaoIndevida, ex.Error.Code);
    }

    [Fact]
    public async Task Avaliacao_ParecerVazioEhRejeitado()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
            {
                Aprovado = true,
                Parecer = "  "
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Fila de homologação ───────────────────────────────────────────────────

    [Fact]
    public async Task Fila_TrazPontuacaoEClassificacaoVigente()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q17: new List<string> { "I" }); // peso 2
        dto.AvisoInteracaoIa = true;
        await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);

        var fila = await _service.ListarHomologacoesPendentesAsync(sgdiCtx, null);

        Assert.Single(fila);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoSgdi, fila[0].Sistema.SituacaoHomologacao);
        Assert.Equal("SES", fila[0].Sistema.OrgaoSigla);
        Assert.NotNull(fila[0].ClassificacaoVigente);
        Assert.Equal(2, fila[0].ClassificacaoVigente!.Pontuacao);
        Assert.Equal(new List<string> { "I" }, fila[0].ClassificacaoVigente!.Checklist.Q17);
    }

    [Fact]
    public async Task Fila_CasaCadaSistemaComASuaClassificacaoVigenteEOrdenaPorAntiguidade()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        // Três sistemas com pontuações distintas; o primeiro ainda ganha uma reclassificação
        var baixo = NovoSistemaDto("A primeiro");
        var primeiro = await _service.CriarSistemaAsync(OrgaoSes.Id, baixo, orgaoCtx);

        var segundoDto = NovoSistemaDto("B segundo");
        segundoDto.Classificacao = Checklist(q17: new List<string> { "I" }); // peso 2
        segundoDto.AvisoInteracaoIa = true;
        await _service.CriarSistemaAsync(OrgaoSes.Id, segundoDto, orgaoCtx);

        var terceiroDto = NovoSistemaDto("C terceiro");
        terceiroDto.Classificacao = Checklist(q16: new List<string> { "I" }); // peso 4
        terceiroDto.SupervisaoHumanaDescricao = "Revisão humana.";
        await _service.CriarSistemaAsync(OrgaoSes.Id, terceiroDto, orgaoCtx);

        // Reclassifica o primeiro: a fila tem de mostrar a vigente, não a inicial
        var nova = Checklist(q17: new List<string> { "I", "IV" }); // 2 + 2 = 4
        nova.Motivo = "Revisão periódica";
        await _service.ReclassificarAsync(primeiro.Id, nova, orgaoCtx);

        var fila = await _service.ListarHomologacoesPendentesAsync(sgdiCtx, null);

        Assert.Equal(3, fila.Count);
        Assert.Equal("A primeiro", fila[0].Sistema.Denominacao); // mais antigo primeiro
        Assert.Equal("B segundo", fila[1].Sistema.Denominacao);
        Assert.Equal("C terceiro", fila[2].Sistema.Denominacao);

        Assert.Equal(4, fila[0].ClassificacaoVigente!.Pontuacao);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, fila[0].ClassificacaoVigente!.Resultado);
        Assert.Equal(2, fila[1].ClassificacaoVigente!.Pontuacao);
        Assert.Equal(4, fila[2].ClassificacaoVigente!.Pontuacao);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, fila[2].ClassificacaoVigente!.Resultado);

        // Cada item carrega a classificação do seu próprio sistema
        Assert.All(fila, item =>
            Assert.Equal(item.Sistema.Id, item.ClassificacaoVigente!.SistemaIaId));
    }

    [Fact]
    public async Task Fila_FiltraPelaSituacaoInformada()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Baixo"), orgaoCtx);

        var alto = NovoSistemaDto("Alto");
        alto.Classificacao = Checklist(q16: new List<string> { "I" });
        alto.SupervisaoHumanaDescricao = "Revisão humana.";
        await _service.CriarSistemaAsync(OrgaoSes.Id, alto, orgaoCtx);

        var doComite = await _service.ListarHomologacoesPendentesAsync(
            sgdiCtx, PgiaDominios.SituacaoHomologacao.AguardandoCgtic);

        Assert.Single(doComite);
        Assert.Equal("Alto", doComite[0].Sistema.Denominacao);
    }

    [Fact]
    public async Task Fila_SituacaoInvalidaEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ListarHomologacoesPendentesAsync(sgdiCtx, PgiaDominios.SituacaoHomologacao.Aprovado));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Gate de implantação ───────────────────────────────────────────────────

    [Fact]
    public async Task Gate_SemHomologacaoBloqueiaAImplantacao()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var dto = NovoSistemaDto();
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(sistema.Id,
                ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), ctx));

        Assert.Equal((int)ErrorCode.PgiaImplantacaoBloqueada, ex.Error.Code);
    }

    [Fact]
    public async Task Gate_VetoBloqueiaAImplantacao()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);
        await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = false,
            Parecer = "Sem finalidade demonstrada."
        }, sgdiCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(sistema.Id,
                ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx));

        Assert.Equal((int)ErrorCode.PgiaImplantacaoBloqueada, ex.Error.Code);
        Assert.Contains("vetado", ex.Error.Message);
    }

    [Fact]
    public async Task Gate_BaixoRiscoAprovadoLiberaAImplantacao()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);
        await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = true,
            Parecer = "Sem óbice."
        }, sgdiCtx);

        var atualizado = await _service.AtualizarSistemaAsync(sistema.Id,
            ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx);

        Assert.Equal(PgiaDominios.StatusCicloVida.Implantado, atualizado.StatusCicloVida);
    }

    [Fact]
    public async Task Gate_AltoRiscoAprovadoSemAiaCompletaBloqueia()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q16: new List<string> { "I" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão.";
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);

        await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel,
            NumeroAto = "12/2026"
        }, cgticCtx);

        // AIA existe, mas ainda em elaboração e sem publicação nem deliberação vinculada
        await _service.CriarAiaAsync(sistema.Id, NovaAiaDto("Em elaboração"), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(sistema.Id,
                ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx));

        Assert.Equal((int)ErrorCode.PgiaImplantacaoBloqueada, ex.Error.Code);
        Assert.Contains("art. 16", ex.Error.Message);
    }

    [Fact]
    public async Task Gate_AltoRiscoComAiaConcluidaPublicadaEDeliberadaLibera()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q16: new List<string> { "I" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão.";
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);

        var deliberacao = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel,
            Ementa = "Enquadramento confirmado; implantação autorizada."
        }, cgticCtx);

        var aia = await _service.CriarAiaAsync(sistema.Id, NovaAiaDto(PgiaDominios.StatusAia.Concluida), orgaoCtx);
        await _service.PublicarAiaAsync(aia.Id, new PgiaAiaPublicacaoDTO
        {
            PublicadaPortal = true,
            DataPublicacaoPortal = new DateOnly(2026, 10, 20),
            UrlPublicacao = "https://transparencia.df.gov.br/aia/1"
        }, sgdiCtx);
        var vinculada = await _service.VincularDeliberacaoAiaAsync(aia.Id, deliberacao.Id, sgdiCtx);

        Assert.Equal(PgiaDominios.ResultadoDeliberacao.Favoravel, vinculada.DeliberacaoResultado);

        var atualizado = await _service.AtualizarSistemaAsync(sistema.Id,
            ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx);

        Assert.Equal(PgiaDominios.StatusCicloVida.Implantado, atualizado.StatusCicloVida);
    }

    [Fact]
    public async Task Gate_SistemaJaEmUsoContinuaEditavel()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);
        await _service.AvaliarHomologacaoAsync(sistema.Id, new PgiaAvaliacaoHomologacaoDTO
        {
            Aprovado = true,
            Parecer = "Sem óbice."
        }, sgdiCtx);
        await _service.AtualizarSistemaAsync(sistema.Id,
            ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx);

        // Implantado -> Monitoramento não é nova entrada em uso: o gate não reabre
        var update = ParaUpdate(dto, PgiaDominios.StatusCicloVida.Monitoramento, new DateOnly(2026, 11, 1));
        update.Finalidade = "Finalidade revisada";
        var atualizado = await _service.AtualizarSistemaAsync(sistema.Id, update, orgaoCtx);

        Assert.Equal(PgiaDominios.StatusCicloVida.Monitoramento, atualizado.StatusCicloVida);
    }

    // ── AIA ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aia_ConcluidaSemDatasEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var dto = NovaAiaDto(PgiaDominios.StatusAia.Concluida);
        dto.ProximaRevisao = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAiaAsync(sistema.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Aia_TextosObrigatoriosSaoExigidos()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var dto = NovaAiaDto("Em elaboração");
        dto.MedidasReversao = "   ";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAiaAsync(sistema.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Aia_RegistraElaboradorEApareceNaListaDoSistema()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var aia = await _service.CriarAiaAsync(sistema.Id, NovaAiaDto("Em elaboração"), ctx);

        Assert.Equal("Maria Andrade", aia.ElaboradaPorNome);
        Assert.False(aia.PublicadaPortal);

        var lista = await _service.ListarAiasAsync(sistema.Id);
        Assert.Single(lista);

        var salva = await Context.PgiaAias.SingleAsync();
        Assert.Equal(ctx.UserId, salva.ElaboradaPor);
        Assert.Equal(UserOrgaoSes.Email, salva.CriadoPor);
    }

    [Fact]
    public async Task Aia_DocumentoDeOutroOrgaoEhRejeitado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var documentoDeOutroOrgao = new PgiaDocumento
        {
            Tipo = "AIA",
            OrgaoId = OrgaoSeec.Id,
            NomeArquivo = "aia-alheia.pdf",
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = UserOrgaoSeec.Id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documentoDeOutroOrgao);
        await Context.SaveChangesAsync();

        var dto = NovaAiaDto("Em elaboração");
        dto.DocumentoId = documentoDeOutroOrgao.Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAiaAsync(sistema.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Aia_DeliberacaoDeTipoQueNaoDecideHomologacaoNaoPodeSerVinculada()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);

        // Deliberação do próprio sistema, favorável, mas de assunto que não aprova adoção
        var suspensao = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Suspensão de sistema",
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var aia = await _service.CriarAiaAsync(sistema.Id, NovaAiaDto("Em elaboração"), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.VincularDeliberacaoAiaAsync(aia.Id, suspensao.Id, cgticCtx));

        Assert.Equal((int)ErrorCode.PgiaDeliberacaoNaoEncontrada, ex.Error.Code);
        Assert.Contains("art. 16", ex.Error.Message);
    }

    [Fact]
    public async Task Aia_DeliberacaoGeralSemSistemaNaoPodeSerVinculada()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);

        var geral = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.AprovacaoAquisicaoAltoRisco,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var aia = await _service.CriarAiaAsync(sistema.Id, NovaAiaDto("Em elaboração"), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.VincularDeliberacaoAiaAsync(aia.Id, geral.Id, cgticCtx));

        Assert.Equal((int)ErrorCode.PgiaDeliberacaoNaoEncontrada, ex.Error.Code);
    }

    [Fact]
    public async Task Gate_AltoRiscoComDeliberacaoDeTipoErradoNaAiaBloqueia()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var dto = NovoSistemaDto();
        dto.Classificacao = Checklist(q16: new List<string> { "I" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão.";
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, orgaoCtx);

        // Homologação aprovada pelo comitê
        await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        // AIA concluída e publicada, mas com deliberação de assunto diverso gravada direto
        var aia = await _service.CriarAiaAsync(sistema.Id, NovaAiaDto(PgiaDominios.StatusAia.Concluida), orgaoCtx);
        await _service.PublicarAiaAsync(aia.Id, new PgiaAiaPublicacaoDTO
        {
            PublicadaPortal = true,
            DataPublicacaoPortal = new DateOnly(2026, 10, 20)
        }, sgdiCtx);

        var suspensao = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Suspensão de sistema",
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 22),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);
        var aiaSalva = await Context.PgiaAias.FirstAsync(a => a.Id == aia.Id);
        aiaSalva.DeliberacaoCgticId = suspensao.Id;
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(sistema.Id,
                ParaUpdate(dto, PgiaDominios.StatusCicloVida.Implantado, new DateOnly(2026, 11, 1)), orgaoCtx));

        Assert.Equal((int)ErrorCode.PgiaImplantacaoBloqueada, ex.Error.Code);
    }

    [Fact]
    public async Task Aia_DeliberacaoDeOutroSistemaNaoPodeSerVinculada()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var sistemaA = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Sistema A"), orgaoCtx);
        var sistemaB = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Sistema B"), orgaoCtx);

        // Tipo correto, mas de outro sistema: o que reprova aqui é o vínculo cruzado
        var deliberacaoDeB = await _governanca.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistemaB.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var aia = await _service.CriarAiaAsync(sistemaA.Id, NovaAiaDto("Em elaboração"), orgaoCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.VincularDeliberacaoAiaAsync(aia.Id, deliberacaoDeB.Id, cgticCtx));

        Assert.Equal((int)ErrorCode.PgiaDeliberacaoNaoEncontrada, ex.Error.Code);
    }

    // ── Registro Público ──────────────────────────────────────────────────────

    [Fact]
    public async Task RegistroPublico_MarcaEDesmarcaAPublicacao()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), orgaoCtx);

        var publicado = await _service.AtualizarRegistroPublicoAsync(sistema.Id, new PgiaRegistroPublicoDTO
        {
            Publicado = true,
            DataPublicacao = new DateOnly(2026, 12, 1)
        }, sgdiCtx);

        Assert.True(publicado.PublicadoRegistroPublico);
        Assert.Equal(new DateOnly(2026, 12, 1), publicado.DataPublicacaoRegistro);

        var despublicado = await _service.AtualizarRegistroPublicoAsync(sistema.Id, new PgiaRegistroPublicoDTO
        {
            Publicado = false,
            DataPublicacao = new DateOnly(2026, 12, 1)
        }, sgdiCtx);

        Assert.False(despublicado.PublicadoRegistroPublico);
        Assert.Null(despublicado.DataPublicacaoRegistro);
    }
}
