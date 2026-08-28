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
/// Inventário de sistemas de IA e classificação de risco (fase 1): regra dos
/// arts. 15 a 18, condicionais do formulário e histórico de classificações.
/// </summary>
public class PgiaSistemaServiceTest : PgiaTestBase
{
    private readonly PgiaSistemaService _service;
    private readonly PgiaPermissionService _permissionService;

    public PgiaSistemaServiceTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private PgiaResponsavelIa DesignarResponsavel(PgiaOrgao orgao, User agente)
    {
        var designacao = new PgiaResponsavelIa
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
        };
        Context.PgiaResponsaveisIa.Add(designacao);
        Context.SaveChanges();
        return designacao;
    }

    private async Task<PgiaUserContext> ContextoSesAsync()
    {
        return (await _permissionService.GetContextAsync(UserOrgaoSes.Email))!;
    }

    private static PgiaClassificacaoCreateDTO NovaClassificacaoDto(
        List<string>? q15 = null, List<string>? q16 = null, List<string>? q17 = null) => new()
    {
        Checklist = new PgiaChecklistDTO
        {
            Q15 = q15 ?? new List<string>(),
            Q16 = q16 ?? new List<string>(),
            Q17 = q17 ?? new List<string>()
        },
        Motivo = "Classificação inicial",
        DataClassificacao = new DateOnly(2026, 9, 1),
        Justificativa = "Enquadramento avaliado pelo Responsável de IA do órgão."
    };

    private static PgiaSistemaCreateDTO NovoSistemaDto(string denominacao = "Assistente virtual 156") => new()
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
        Classificacao = NovaClassificacaoDto()
    };

    private static PgiaSistemaUpdateDTO ParaUpdate(PgiaSistemaCreateDTO dto) => new()
    {
        Denominacao = dto.Denominacao,
        Finalidade = dto.Finalidade,
        OrigemRegistro = dto.OrigemRegistro,
        OrigemRegistroDescricao = dto.OrigemRegistroDescricao,
        TipoSistema = dto.TipoSistema,
        Tecnologia = dto.Tecnologia,
        StatusCicloVida = dto.StatusCicloVida,
        DataImplantacao = dto.DataImplantacao,
        EscopoDados = dto.EscopoDados,
        AfetaCidadao = dto.AfetaCidadao,
        NaturezaDecisoes = dto.NaturezaDecisoes,
        EfeitosCidadao = dto.EfeitosCidadao,
        BaseLegalLgpd = dto.BaseLegalLgpd,
        CategoriasDadosPessoais = dto.CategoriasDadosPessoais,
        FinalidadeTratamentoDados = dto.FinalidadeTratamentoDados,
        MedidasSeguranca = dto.MedidasSeguranca,
        InteroperavelPadroesSgdi = dto.InteroperavelPadroesSgdi,
        JustificativaNaoRedundancia = dto.JustificativaNaoRedundancia,
        SupervisaoHumanaDescricao = dto.SupervisaoHumanaDescricao,
        AvisoInteracaoIa = dto.AvisoInteracaoIa,
        IdentificadorAutenticidade = dto.IdentificadorAutenticidade,
        ProcessoSei = dto.ProcessoSei,
        ComunicadoSgdiEm = dto.ComunicadoSgdiEm
    };

    // ── Regra de risco (arts. 15 a 18) ────────────────────────────────────────

    [Fact]
    public async Task Checklist_SemMarcacaoDaBaixoRisco()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);
        Assert.Null(sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task Checklist_Art17DaRiscoModerado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "I" });
        dto.AvisoInteracaoIa = true; // condicional do art. 17, § 1º

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, sistema.ClassificacaoRiscoAtual);
        Assert.Equal("art. 17, I", sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task Checklist_Art16DaAltoRiscoComPrimeiroIncisoDoArtigo()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        // Ordem de digitação invertida: o enquadramento aponta o primeiro inciso do artigo
        dto.Classificacao = NovaClassificacaoDto(q16: new List<string> { "V", "III" });
        dto.SupervisaoHumanaDescricao = "Servidor revisa cada decisão antes da publicação.";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, sistema.ClassificacaoRiscoAtual);
        Assert.Equal("art. 16, III", sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task Checklist_Art15VenceOsDemaisEDaRiscoExcessivo()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(
            q15: new List<string> { "II" },
            q16: new List<string> { "I" },
            q17: new List<string> { "I" });

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, sistema.ClassificacaoRiscoAtual);
        Assert.Equal("art. 15, II", sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task Checklist_IncisoForaDoArtigoEhRejeitado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        // Art. 17 vai só até o inciso IV
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "IX" });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Classificacao_MotivoForaDoDominioEhRejeitado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao.Motivo = "Porque sim";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Classificacao_JustificativaEhObrigatoria()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao.Justificativa = "   ";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaClassificacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Classificacao_DataAusenteEhRejeitada()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao.DataClassificacao = default;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaClassificacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Criacao_GravaClassificacaoInicialComChecklistEmJson()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "I", "III" });
        dto.AvisoInteracaoIa = false;

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == sistema.Id);
        Assert.Equal("{\"q15\":[],\"q16\":[],\"q17\":[\"I\",\"III\"]}", salva.RespostasChecklist);
        Assert.Equal(ctx.UserId, salva.ClassificadoPor);
        Assert.Equal(UserOrgaoSes.Email, salva.CriadoPor);

        var historico = await _service.ListarClassificacoesAsync(sistema.Id, incluirPontuacao: true);
        Assert.Single(historico);
        Assert.Equal(new List<string> { "I", "III" }, historico[0].Checklist.Q17);
        Assert.Equal("Maria Andrade", historico[0].ClassificadoPorNome);
    }

    // ── Pontuação dos quesitos (métrica da SGDI) ──────────────────────────────

    [Fact]
    public async Task Pontuacao_SomaOsPesosDosIncisosMarcados()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        // art. 15, I = 5; art. 16, IV = 4; art. 17, II = 1
        dto.Classificacao = NovaClassificacaoDto(
            q15: new List<string> { "I" },
            q16: new List<string> { "IV" },
            q17: new List<string> { "II" });

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var salva = await Context.PgiaClassificacoesRisco.SingleAsync(c => c.SistemaIaId == sistema.Id);
        Assert.Equal(10, salva.Pontuacao);
    }

    [Fact]
    public void Pontuacao_ChecklistVazioSomaZero()
    {
        Assert.Equal(0, PgiaQuesitos.CalcularPontuacao(null, null, null));
        Assert.Equal(0, PgiaQuesitos.CalcularPontuacao(
            new List<string>(), new List<string>(), new List<string>()));
    }

    [Fact]
    public async Task Pontuacao_SoVaiNaRespostaParaOEscopoCentral()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "I" }); // peso 2
        dto.AvisoInteracaoIa = true;
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var paraOrgao = await _service.ListarClassificacoesAsync(sistema.Id, incluirPontuacao: false);
        Assert.Null(paraOrgao[0].Pontuacao);

        var paraSgdi = await _service.ListarClassificacoesAsync(sistema.Id, incluirPontuacao: true);
        Assert.Equal(2, paraSgdi[0].Pontuacao);
    }

    [Fact]
    public async Task Pontuacao_ReclassificacaoGravaANovaSoma()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        // art. 16, I = 4; art. 16, II = 3
        var reclassificacao = NovaClassificacaoDto(q16: new List<string> { "I", "II" });
        reclassificacao.Motivo = "Revisão periódica";
        var resposta = await _service.ReclassificarAsync(sistema.Id, reclassificacao, ctx);

        // O POST é feito pelo órgão: a pontuação não volta na resposta
        Assert.Null(resposta.Pontuacao);

        var historico = await _service.ListarClassificacoesAsync(sistema.Id, incluirPontuacao: true);
        Assert.Equal(7, historico[0].Pontuacao);
        Assert.Equal(0, historico[1].Pontuacao);
    }

    // ── Pré-condições do inventário ───────────────────────────────────────────

    [Fact]
    public async Task Criacao_SemResponsavelVigenteEhRejeitada()
    {
        var ctx = await ContextoSesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx));

        Assert.Equal((int)ErrorCode.PgiaResponsavelNaoDesignado, ex.Error.Code);
    }

    [Fact]
    public async Task Criacao_OrgaoInexistenteEhRejeitada()
    {
        var ctx = await ContextoSesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(9999, NovoSistemaDto(), ctx));

        Assert.Equal((int)ErrorCode.PgiaOrgaoNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Criacao_DenominacaoDuplicadaNoOrgaoEhRejeitada()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Triagem clínica"), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Triagem clínica"), ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task Criacao_AceitaOrigemInstrumentoVigenteEmRevisao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        // 30 caracteres: não cabe no varchar(20) do schema, por isso a coluna é varchar(40)
        dto.OrigemRegistro = "Instrumento vigente em revisão";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal("Instrumento vigente em revisão", sistema.OrigemRegistro);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal("Instrumento vigente em revisão", salvo.OrigemRegistro);
    }

    [Fact]
    public async Task Criacao_DominioInvalidoEhRejeitado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Tecnologia = "Bola de cristal";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Risco Excessivo bloqueia o uso (art. 15) ──────────────────────────────

    [Fact]
    public async Task RiscoExcessivo_EmUsoEhAceitoNoCadastroParaRegistrarARealidade()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q15: new List<string> { "I" });
        dto.StatusCicloVida = PgiaDominios.StatusCicloVida.Implantado;
        dto.DataImplantacao = new DateOnly(2026, 5, 10);

        // O cadastro não é recusado: recusar na hora ensinaria o usuário a ajustar
        // as respostas até passar, e a SGDI perderia o sistema irregular de vista.
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, sistema.ClassificacaoRiscoAtual);
        Assert.Equal(PgiaDominios.StatusCicloVida.Implantado, sistema.StatusCicloVida);
        Assert.Equal("art. 15, I", sistema.EnquadramentoLegal);
    }

    [Fact]
    public async Task RiscoExcessivo_NaoPodeSerColocadoEmUsoPelaEdicao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q15: new List<string> { "I" });
        dto.StatusCicloVida = "Testagem";
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var update = ParaUpdate(dto);
        update.StatusCicloVida = PgiaDominios.StatusCicloVida.Monitoramento;
        update.DataImplantacao = new DateOnly(2026, 5, 10);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(sistema.Id, update, ctx));

        Assert.Equal((int)ErrorCode.PgiaRiscoExcessivoBloqueado, ex.Error.Code);
    }

    // ── Condicionais do formulário (seções 2.1 a 2.3) ─────────────────────────

    [Fact]
    public async Task Condicional_SistemaEmUsoExigeDataDeImplantacao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.StatusCicloVida = PgiaDominios.StatusCicloVida.Implantado;
        dto.DataImplantacao = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_AfetaCidadaoExigeNaturezaEEfeitos()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.AfetaCidadao = true;
        dto.NaturezaDecisoes = "Prioriza atendimentos";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_NaoAfetaCidadaoAnulaOsCamposDependentes()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.AfetaCidadao = false;
        dto.NaturezaDecisoes = "Texto que não se aplica";
        dto.EfeitosCidadao = "Texto que não se aplica";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Null(sistema.NaturezaDecisoes);
        Assert.Null(sistema.EfeitosCidadao);
    }

    [Fact]
    public async Task Condicional_DadosPessoaisExigemBlocoLgpd()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.EscopoDados = PgiaDominios.EscopoDados.DadosPessoais;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_BlocoLgpdCompletoEhAceito()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.EscopoDados = PgiaDominios.EscopoDados.DadosPessoaisSensiveis;
        dto.BaseLegalLgpd = "Execução de políticas públicas";
        dto.CategoriasDadosPessoais = "Nome, CPF e dados de saúde";
        dto.FinalidadeTratamentoDados = "Triagem de atendimento";
        dto.MedidasSeguranca = "Criptografia em repouso e controle de acesso";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal("Execução de políticas públicas", sistema.BaseLegalLgpd);
        Assert.Equal("Triagem de atendimento", sistema.FinalidadeTratamentoDados);
    }

    [Fact]
    public async Task Condicional_SemDadosPessoaisAnulaOBlocoLgpd()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.EscopoDados = "Somente dados públicos";
        dto.BaseLegalLgpd = "Consentimento do titular";
        dto.CategoriasDadosPessoais = "Nada disso se aplica";
        dto.FinalidadeTratamentoDados = "Nada disso se aplica";
        dto.MedidasSeguranca = "Nada disso se aplica";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Null(sistema.BaseLegalLgpd);
        Assert.Null(sistema.CategoriasDadosPessoais);
        Assert.Null(sistema.FinalidadeTratamentoDados);
        Assert.Null(sistema.MedidasSeguranca);
    }

    [Fact]
    public async Task Condicional_ContratadoExigeJustificativaDeNaoRedundancia()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.TipoSistema = PgiaDominios.TipoSistema.Contratado;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_NaoContratadoAnulaJustificativaDeNaoRedundancia()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.TipoSistema = "Desenvolvido internamente";
        dto.JustificativaNaoRedundancia = "Texto que não se aplica";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Null(sistema.JustificativaNaoRedundancia);
    }

    [Fact]
    public async Task Condicional_AltoRiscoExigeSupervisaoHumana()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q16: new List<string> { "I" });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaClassificacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_RiscoModeradoExigeAvisoDeInteracao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "II" });
        dto.AvisoInteracaoIa = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaClassificacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_BaixoRiscoPreservaSupervisaoAvisoEIdentificador()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.SupervisaoHumanaDescricao = "Servidor revisa as saídas por amostragem.";
        dto.AvisoInteracaoIa = true;
        dto.IdentificadorAutenticidade = true;

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        // Os três campos são coletados de todo sistema: o formulário os exibe de forma
        // neutra, sem revelar o grupo de risco calculado.
        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);
        Assert.Equal("Servidor revisa as saídas por amostragem.", sistema.SupervisaoHumanaDescricao);
        Assert.True(sistema.AvisoInteracaoIa);
        Assert.True(sistema.IdentificadorAutenticidade);
    }

    [Fact]
    public async Task Condicional_OrigemOutrosExigeDescricao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.OrigemRegistro = PgiaDominios.OrigemRegistro.Outros;
        dto.OrigemRegistroDescricao = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Condicional_OrigemOutrosComDescricaoEhAceita()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.OrigemRegistro = PgiaDominios.OrigemRegistro.Outros;
        dto.OrigemRegistroDescricao = "Cessão de solução de outro ente federativo";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Equal(PgiaDominios.OrigemRegistro.Outros, sistema.OrigemRegistro);
        Assert.Equal("Cessão de solução de outro ente federativo", sistema.OrigemRegistroDescricao);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal("Cessão de solução de outro ente federativo", salvo.OrigemRegistroDescricao);
    }

    [Fact]
    public async Task Condicional_OrigemConhecidaAnulaADescricao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.OrigemRegistro = "Nova iniciativa";
        dto.OrigemRegistroDescricao = "Texto que não se aplica";

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        Assert.Null(sistema.OrigemRegistroDescricao);
    }

    // ── Edição ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edicao_MantemAClassificacaoVigenteEGravaAuditoria()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var dto = NovoSistemaDto();
        dto.Classificacao = NovaClassificacaoDto(q17: new List<string> { "I" });
        dto.AvisoInteracaoIa = true;
        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, dto, ctx);

        var update = ParaUpdate(dto);
        update.Finalidade = "Nova finalidade do sistema";
        var atualizado = await _service.AtualizarSistemaAsync(sistema.Id, update, ctx);

        Assert.Equal("Nova finalidade do sistema", atualizado.Finalidade);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, atualizado.ClassificacaoRiscoAtual);
        Assert.Equal("art. 17, I", atualizado.EnquadramentoLegal);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);
        Assert.NotNull(salvo.AlteradoEm);
    }

    [Fact]
    public async Task Edicao_DenominacaoDeOutroSistemaDoOrgaoEhRejeitada()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Sistema A"), ctx);
        var b = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Sistema B"), ctx);

        var update = ParaUpdate(NovoSistemaDto("Sistema A"));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(b.Id, update, ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task Edicao_SistemaInexistenteRetornaNaoEncontrado()
    {
        var ctx = await ContextoSesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarSistemaAsync(9999, ParaUpdate(NovoSistemaDto()), ctx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    // ── Reclassificação ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reclassificacao_AtualizaDesnormalizadoEPreservaHistorico()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);
        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, sistema.ClassificacaoRiscoAtual);

        var reclassificacao = NovaClassificacaoDto(q16: new List<string> { "IV" });
        reclassificacao.Motivo = "Revisão periódica";
        reclassificacao.DataClassificacao = new DateOnly(2027, 2, 1);
        var nova = await _service.ReclassificarAsync(sistema.Id, reclassificacao, ctx);

        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, nova.Resultado);
        Assert.Equal("art. 16, IV", nova.EnquadramentoLegal);
        Assert.Equal("Maria Andrade", nova.ClassificadoPorNome);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, salvo.ClassificacaoRiscoAtual);
        Assert.Equal("art. 16, IV", salvo.EnquadramentoLegal);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);

        var historico = await _service.ListarClassificacoesAsync(sistema.Id, incluirPontuacao: true);
        Assert.Equal(2, historico.Count);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, historico[0].Resultado);   // mais recente primeiro
        Assert.Equal(PgiaDominios.ResultadoRisco.Baixo, historico[1].Resultado);
        Assert.Equal(new List<string> { "IV" }, historico[0].Checklist.Q16);
    }

    [Fact]
    public async Task Reclassificacao_ChecklistInvalidoEhRejeitado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ReclassificarAsync(sistema.Id, NovaClassificacaoDto(q15: new List<string> { "X" }), ctx));

        Assert.Equal((int)ErrorCode.PgiaChecklistInvalido, ex.Error.Code);
    }

    // ── Documentos ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Documento_HerdaOOrgaoDoSistemaERegistraOAgente()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var documento = await _service.CriarDocumentoAsync(sistema.Id, new PgiaDocumentoCreateDTO
        {
            Tipo = "AIA",
            ProcessoSei = "00060-00099999/2026-11",
            NomeArquivo = "aia-assistente-156.pdf"
        }, ctx);

        Assert.Equal(OrgaoSes.Id, documento.OrgaoId);
        Assert.Equal(sistema.Id, documento.SistemaIaId);
        Assert.Equal("Maria Andrade", documento.EnviadoPorNome);
        Assert.NotEqual(default, documento.DataEnvio);

        var salvo = await Context.PgiaDocumentos.SingleAsync();
        Assert.Equal(ctx.UserId, salvo.EnviadoPor);
        Assert.Equal(UserOrgaoSes.Email, salvo.CriadoPor);

        var lista = await _service.ListarDocumentosAsync(sistema.Id);
        Assert.Single(lista);
    }

    [Fact]
    public async Task Documento_TipoForaDoDominioEhRejeitado()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarDocumentoAsync(sistema.Id, new PgiaDocumentoCreateDTO
            {
                Tipo = "Bilhete",
                NomeArquivo = "qualquer.pdf"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Documento_NomeDoArquivoEhObrigatorio()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        var sistema = await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarDocumentoAsync(sistema.Id, new PgiaDocumentoCreateDTO
            {
                Tipo = "Outro",
                NomeArquivo = "  "
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Consulta ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listagem_PaginaEOrdenaPorDenominacao()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Zelador digital"), ctx);
        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Agenda inteligente"), ctx);
        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Monitor de leitos"), ctx);

        var pagina = await _service.ListarSistemasAsync(ctx, OrgaoSes.Id, new PagedRequest { Page = 1, PageSize = 2 });

        Assert.Equal(3, pagina.TotalItems);
        Assert.Equal(2, pagina.Items.Count);
        Assert.Equal("Agenda inteligente", pagina.Items[0].Denominacao);
        Assert.Equal("Monitor de leitos", pagina.Items[1].Denominacao);
        Assert.Equal("SES", pagina.Items[0].OrgaoSigla);
        Assert.Equal("Maria Andrade", pagina.Items[0].ResponsavelNome);
    }

    [Fact]
    public async Task Listagem_PaginaZeroCaiNaPrimeiraPagina()
    {
        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        var ctx = await ContextoSesAsync();

        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Agenda inteligente"), ctx);
        await _service.CriarSistemaAsync(OrgaoSes.Id, NovoSistemaDto("Monitor de leitos"), ctx);

        var pagina = await _service.ListarSistemasAsync(ctx, OrgaoSes.Id, new PagedRequest { Page = 0, PageSize = 1 });

        Assert.Equal(1, pagina.CurrentPage);
        Assert.Equal(2, pagina.TotalItems);
        Assert.Single(pagina.Items);
        Assert.Equal("Agenda inteligente", pagina.Items[0].Denominacao);
    }

    [Fact]
    public async Task Consulta_SistemaInexistenteRetornaNaoEncontrado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.GetSistemaAsync(9999));
        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }
}
