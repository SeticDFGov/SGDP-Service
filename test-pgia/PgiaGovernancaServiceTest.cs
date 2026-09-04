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
/// Governança central (fase 2): deliberações do CGTIC, plataformas públicas de IA
/// generativa, normas complementares e autorizações excepcionais.
/// </summary>
public class PgiaGovernancaServiceTest : PgiaTestBase
{
    private readonly PgiaGovernancaService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaPermissionService _permissionService;

    public PgiaGovernancaServiceTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));
        _sistemaService = new PgiaSistemaService(
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

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email))!;

    /// <summary>Cria um sistema de Alto Risco, que nasce aguardando o comitê.</summary>
    private async Task<PgiaSistemaResponse> NovoSistemaAltoRiscoAsync(string denominacao = "Triagem clínica")
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        return await _sistemaService.CriarSistemaAsync(OrgaoSes.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar a triagem de atendimentos",
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
                Checklist = ChecklistRespondido(new PgiaChecklistDTO { Q16 = new List<string> { "IV" } }),
                OutrosRiscos = OutrosRiscosSeNecessario(q16: new List<string> { "IV" }),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Apoio a diagnóstico com risco relevante."
            }
        }, ctx);
    }

    // PgiaPlataformaUpdateDTO herda de Create: serve nos dois usos
    private static PgiaPlataformaUpdateDTO NovaPlataformaDto(string nome = "Assistente GDF") => new()
    {
        Nome = nome,
        Fornecedor = "Fornecedor X",
        StatusHomologacao = "Em avaliação"
    };

    // ── Deliberações do CGTIC ─────────────────────────────────────────────────

    [Fact]
    public async Task Deliberacao_FavoravelAprovaAHomologacaoDelegada()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sistema = await NovoSistemaAltoRiscoAsync();

        var deliberacao = await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel,
            NumeroAto = "12/2026",
            Ementa = "Enquadramento confirmado."
        }, cgticCtx);

        Assert.Equal("Triagem clínica", deliberacao.SistemaDenominacao);
        Assert.Equal("SES", deliberacao.OrgaoSigla);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.Aprovado, salvo.SituacaoHomologacao);
        Assert.Equal(deliberacao.Id, salvo.DeliberacaoHomologacaoId);
        Assert.Equal(cgticCtx.UserId, salvo.AvaliadoPor);
        Assert.Equal("Enquadramento confirmado.", salvo.AvaliacaoParecer);
        Assert.NotNull(salvo.AvaliadoEm);
    }

    [Fact]
    public async Task Deliberacao_DesfavoravelVetaOSistema()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sistema = await NovoSistemaAltoRiscoAsync();

        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.AprovacaoAquisicaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Desfavoravel,
            NumeroAto = "13/2026"
        }, cgticCtx);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.Vetado, salvo.SituacaoHomologacao);
        // Sem ementa, o parecer registra o ato deliberado
        Assert.Equal("Deliberação do CGTIC nº 13/2026", salvo.AvaliacaoParecer);
    }

    [Fact]
    public async Task Deliberacao_EmDiligenciaMantemAFilaDoComite()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sistema = await NovoSistemaAltoRiscoAsync();

        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.EmDiligencia
        }, cgticCtx);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, salvo.SituacaoHomologacao);
        Assert.Null(salvo.DeliberacaoHomologacaoId);
        Assert.Null(salvo.AvaliadoEm);
    }

    [Fact]
    public async Task Deliberacao_DeOutroTipoNaoDecideAHomologacao()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sistema = await NovoSistemaAltoRiscoAsync();

        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Suspensão de sistema",
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var salvo = await Context.PgiaSistemasIa.FirstAsync(s => s.Id == sistema.Id);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, salvo.SituacaoHomologacao);
    }

    [Fact]
    public async Task Deliberacao_SemSistemaVinculadoEhAceita()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var deliberacao = await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Aprovação do Guia de Contratações",
            DataDeliberacao = new DateOnly(2026, 12, 20),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        Assert.Null(deliberacao.SistemaIaId);
        Assert.Null(deliberacao.SistemaDenominacao);

        var todas = await _service.ListarDeliberacoesAsync();
        Assert.Single(todas);
    }

    [Fact]
    public async Task Deliberacao_TipoForaDoDominioEhRejeitado()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
            {
                Tipo = "Conversa de corredor",
                DataDeliberacao = new DateOnly(2026, 10, 5),
                Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
            }, cgticCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Deliberacao_SistemaInexistenteEhRejeitado()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
            {
                Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
                SistemaIaId = 9999,
                DataDeliberacao = new DateOnly(2026, 10, 5),
                Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
            }, cgticCtx));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Deliberacao_ListaPorSistemaTrazSoAsDoSistema()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var sistema = await NovoSistemaAltoRiscoAsync();

        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoDeliberacao.ClassificacaoAltoRisco,
            SistemaIaId = sistema.Id,
            DataDeliberacao = new DateOnly(2026, 10, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);
        await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Resolução normativa",
            DataDeliberacao = new DateOnly(2026, 11, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var doSistema = await _service.ListarDeliberacoesPorSistemaAsync(sistema.Id);
        Assert.Single(doSistema);
        Assert.Equal(sistema.Id, doSistema[0].SistemaIaId);

        Assert.Equal(2, (await _service.ListarDeliberacoesAsync()).Count);
    }

    [Fact]
    public async Task Deliberacao_ContratoInexistenteEhRejeitadoEValidoEhGravado()
    {
        var cgticCtx = await CtxAsync(UserCgtic.Email);
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);

        var inexistente = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
            {
                Tipo = "Critérios e requisitos de aquisição",
                ContratoId = 9999,
                DataDeliberacao = new DateOnly(2026, 11, 5),
                Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
            }, cgticCtx));
        Assert.Equal((int)ErrorCode.PgiaContratoNaoEncontrado, inexistente.Error.Code);

        var contratoService = new PgiaContratoService(new PgiaContratoRepositorio(Context));
        var contrato = await contratoService.CriarContratoAsync(OrgaoSes.Id, new PgiaContratoCreateDTO
        {
            NumeroContrato = "12/2026",
            ProcessoSei = "00060-00033333/2026-11",
            Objeto = "Aquisição de solução de IA",
            FornecedorNome = "Fornecedor X",
            ClausulaVedacaoTreinamento = true
        }, orgaoCtx);

        var deliberacao = await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Critérios e requisitos de aquisição",
            ContratoId = contrato.Id,
            DataDeliberacao = new DateOnly(2026, 11, 5),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        Assert.Equal(contrato.Id, deliberacao.ContratoId);
    }

    [Fact]
    public async Task Autorizacao_ContratoInexistenteEhRejeitadoEValidoEhGravado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);

        var inexistente = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
            {
                Tipo = "Uso de plataforma pública com dados não públicos",
                OrgaoId = OrgaoSes.Id,
                ContratoId = 9999,
                Justificativa = "Avaliação prévia concluída.",
                AutorizadaPor = "SGDI",
                DataAutorizacao = new DateOnly(2026, 11, 10)
            }, sgdiCtx));
        Assert.Equal((int)ErrorCode.PgiaContratoNaoEncontrado, inexistente.Error.Code);

        var contratoService = new PgiaContratoService(new PgiaContratoRepositorio(Context));
        var contrato = await contratoService.CriarContratoAsync(OrgaoSes.Id, new PgiaContratoCreateDTO
        {
            NumeroContrato = "13/2026",
            ProcessoSei = "00060-00033334/2026-11",
            Objeto = "Aquisição de solução de IA",
            FornecedorNome = "Fornecedor Y",
            ClausulaVedacaoTreinamento = true
        }, orgaoCtx);

        var autorizacao = await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            ContratoId = contrato.Id,
            Justificativa = "Avaliação prévia concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);

        Assert.Equal(contrato.Id, autorizacao.ContratoId);
    }

    // ── Resumo do inventário para o escopo central ────────────────────────────

    [Fact]
    public async Task ResumoDeSistemas_TrazTodosOsOrgaosOrdenados()
    {
        var orgaoSesCtx = await CtxAsync(UserOrgaoSes.Email);
        var orgaoSeecCtx = await CtxAsync(UserOrgaoSeec.Email);
        DesignarResponsavel(OrgaoSeec, UserOrgaoSeec);

        await NovoSistemaAltoRiscoAsync("Triagem clínica");
        await _sistemaService.CriarSistemaAsync(OrgaoSes.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = "Agenda inteligente",
            Finalidade = "Organizar agendamentos",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "Outra",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(),
                OutrosRiscos = OutrosRiscosSeNecessario(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Sem enquadramento nos arts. 15 a 17."
            }
        }, orgaoSesCtx);
        await _sistemaService.CriarSistemaAsync(OrgaoSeec.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = "Classificador de despesas",
            Finalidade = "Classificar empenhos",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "Outra",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(),
                OutrosRiscos = OutrosRiscosSeNecessario(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Sem enquadramento nos arts. 15 a 17."
            }
        }, orgaoSeecCtx);

        var resumo = await _service.ListarSistemasResumoAsync();

        Assert.Equal(3, resumo.Count);
        // Ordenado por sigla do órgão e depois denominação
        Assert.Equal("SEEC", resumo[0].OrgaoSigla);
        Assert.Equal("Classificador de despesas", resumo[0].Denominacao);
        Assert.Equal("SES", resumo[1].OrgaoSigla);
        Assert.Equal("Agenda inteligente", resumo[1].Denominacao);
        Assert.Equal("Triagem clínica", resumo[2].Denominacao);

        var altoRisco = resumo.First(r => r.Denominacao == "Triagem clínica");
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, altoRisco.ClassificacaoRiscoAtual);
        Assert.Equal(PgiaDominios.SituacaoHomologacao.AguardandoCgtic, altoRisco.SituacaoHomologacao);
        Assert.Equal("Desenvolvimento", altoRisco.StatusCicloVida);
    }

    // ── Plataformas públicas de IA generativa ─────────────────────────────────

    [Fact]
    public async Task Plataforma_NomeDuplicadoEhRejeitado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        await _service.CriarPlataformaAsync(NovaPlataformaDto(), sgdiCtx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarPlataformaAsync(NovaPlataformaDto(), sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaPlataformaJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task Plataforma_HomologacaoAtualizaStatusEAptidao()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var plataforma = await _service.CriarPlataformaAsync(NovaPlataformaDto(), sgdiCtx);

        var dto = NovaPlataformaDto();
        dto.StatusHomologacao = "Homologada apta a dados pessoais e sigilosos";
        dto.AptaDadosPessoaisSigilosos = true;
        dto.AtoHomologacao = "Portaria 40/2026";
        dto.DataAto = new DateOnly(2026, 11, 3);

        var atualizada = await _service.AtualizarPlataformaAsync(plataforma.Id, dto, sgdiCtx);

        Assert.Equal("Homologada apta a dados pessoais e sigilosos", atualizada.StatusHomologacao);
        Assert.True(atualizada.AptaDadosPessoaisSigilosos);

        var salva = await Context.PgiaPlataformasIaGenerativa.SingleAsync();
        Assert.Equal(UserSgdi.Email, salva.AlteradoPor);
    }

    [Fact]
    public async Task Plataforma_StatusForaDoDominioEhRejeitado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var dto = NovaPlataformaDto();
        dto.StatusHomologacao = "Talvez";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarPlataformaAsync(dto, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Plataforma_InexistenteNaAtualizacaoEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtualizarPlataformaAsync(9999, NovaPlataformaDto(), sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaPlataformaNaoEncontrada, ex.Error.Code);
    }

    // ── Normas complementares ─────────────────────────────────────────────────

    [Fact]
    public async Task Norma_CriadaEListadaComAuditoria()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var norma = await _service.CriarNormaAsync(new PgiaNormaCreateDTO
        {
            Tipo = "Guia de Contratações de IA",
            Numero = "01/2026",
            Ementa = "Estabelece o guia de contratações de soluções de IA.",
            Emissor = "SGDI",
            DataPublicacao = new DateOnly(2026, 12, 20),
            AprovadaCgticEm = new DateOnly(2026, 12, 15)
        }, sgdiCtx);

        Assert.Equal("SGDI", norma.Emissor);
        Assert.True(norma.Vigente);

        var lista = await _service.ListarNormasAsync();
        Assert.Single(lista);

        var salva = await Context.PgiaNormasComplementares.SingleAsync();
        Assert.Equal(UserSgdi.Email, salva.CriadoPor);
    }

    [Fact]
    public async Task Norma_EmissorForaDoDominioEhRejeitado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarNormaAsync(new PgiaNormaCreateDTO
            {
                Tipo = "Resolução do CGTIC",
                Ementa = "Ementa qualquer.",
                Emissor = "SUBGD",
                DataPublicacao = new DateOnly(2026, 12, 20)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Norma_EmentaVaziaEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarNormaAsync(new PgiaNormaCreateDTO
            {
                Tipo = "Resolução do CGTIC",
                Ementa = "   ",
                Emissor = "CGTIC",
                DataPublicacao = new DateOnly(2026, 12, 20)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Norma_DataDePublicacaoAusenteEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarNormaAsync(new PgiaNormaCreateDTO
            {
                Tipo = "Resolução do CGTIC",
                Ementa = "Ementa qualquer.",
                Emissor = "CGTIC"
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Norma_RevogacaoMarcaComoNaoVigente()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var norma = await _service.CriarNormaAsync(new PgiaNormaCreateDTO
        {
            Tipo = "Instrução normativa da SGDI",
            Ementa = "Disciplina o inventário.",
            Emissor = "SGDI",
            DataPublicacao = new DateOnly(2026, 9, 1)
        }, sgdiCtx);

        var atualizada = await _service.AtualizarNormaAsync(norma.Id, new PgiaNormaUpdateDTO
        {
            Tipo = "Instrução normativa da SGDI",
            Ementa = "Disciplina o inventário.",
            Emissor = "SGDI",
            DataPublicacao = new DateOnly(2026, 9, 1),
            Vigente = false
        }, sgdiCtx);

        Assert.False(atualizada.Vigente);
    }

    // ── Autorizações excepcionais ─────────────────────────────────────────────

    [Fact]
    public async Task Autorizacao_TreinamentoSemDeliberacaoEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
            {
                Tipo = PgiaDominios.TipoAutorizacao.TreinamentoFornecedor,
                OrgaoId = OrgaoSes.Id,
                Justificativa = "Necessidade de ajuste fino do modelo.",
                AutorizadaPor = "Órgão com aprovação do CGTIC",
                DataAutorizacao = new DateOnly(2026, 11, 10)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Autorizacao_TreinamentoComDeliberacaoEhAceita()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var cgticCtx = await CtxAsync(UserCgtic.Email);

        var deliberacao = await _service.CriarDeliberacaoAsync(new PgiaDeliberacaoCreateDTO
        {
            Tipo = "Autorização de treinamento com dados do GDF",
            DataDeliberacao = new DateOnly(2026, 11, 1),
            Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel
        }, cgticCtx);

        var autorizacao = await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = PgiaDominios.TipoAutorizacao.TreinamentoFornecedor,
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Necessidade de ajuste fino do modelo.",
            AutorizadaPor = "Órgão com aprovação do CGTIC",
            DeliberacaoCgticId = deliberacao.Id,
            DataAutorizacao = new DateOnly(2026, 11, 10),
            VigenciaFim = new DateOnly(2027, 11, 10)
        }, sgdiCtx);

        Assert.Equal("SES", autorizacao.OrgaoSigla);
        Assert.True(autorizacao.Ativo);
        Assert.Equal(deliberacao.Id, autorizacao.DeliberacaoCgticId);
    }

    [Fact]
    public async Task Autorizacao_DePlataformaVinculaANomeDaPlataforma()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var plataforma = await _service.CriarPlataformaAsync(NovaPlataformaDto(), sgdiCtx);

        var autorizacao = await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            PlataformaId = plataforma.Id,
            Justificativa = "Avaliação prévia de riscos concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);

        Assert.Equal("Assistente GDF", autorizacao.PlataformaNome);
    }

    [Fact]
    public async Task Autorizacao_PlataformaInexistenteEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
            {
                Tipo = "Uso de plataforma pública com dados não públicos",
                OrgaoId = OrgaoSes.Id,
                PlataformaId = 9999,
                Justificativa = "Avaliação prévia de riscos concluída.",
                AutorizadaPor = "SGDI",
                DataAutorizacao = new DateOnly(2026, 11, 10)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaPlataformaNaoEncontrada, ex.Error.Code);
    }

    [Fact]
    public async Task Autorizacao_OrgaoInexistenteEhRejeitado()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
            {
                Tipo = "Uso de plataforma pública com dados não públicos",
                OrgaoId = 9999,
                Justificativa = "Avaliação prévia de riscos concluída.",
                AutorizadaPor = "SGDI",
                DataAutorizacao = new DateOnly(2026, 11, 10)
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaOrgaoNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Autorizacao_DataAusenteEhRejeitada()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
            {
                Tipo = "Uso de plataforma pública com dados não públicos",
                OrgaoId = OrgaoSes.Id,
                Justificativa = "Avaliação prévia de riscos concluída.",
                AutorizadaPor = "SGDI"
            }, sgdiCtx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Autorizacao_EdicaoRevogaAExcecao()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        var autorizacao = await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Avaliação prévia de riscos concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);

        Assert.True(autorizacao.Ativo);

        var revogada = await _service.AtualizarAutorizacaoAsync(autorizacao.Id, new PgiaAutorizacaoUpdateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Avaliação prévia de riscos concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10),
            VigenciaFim = new DateOnly(2026, 12, 1),
            Ativo = false
        }, sgdiCtx);

        Assert.False(revogada.Ativo);

        var salva = await Context.PgiaAutorizacoesExcepcionais.SingleAsync();
        Assert.False(salva.Ativo);
        Assert.Equal(UserSgdi.Email, salva.AlteradoPor);
    }

    [Fact]
    public async Task Autorizacao_ListagemPorOrgaoSeparaOsEscopos()
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);

        await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSes.Id,
            Justificativa = "Avaliação prévia de riscos concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 10)
        }, sgdiCtx);
        await _service.CriarAutorizacaoAsync(new PgiaAutorizacaoCreateDTO
        {
            Tipo = "Uso de plataforma pública com dados não públicos",
            OrgaoId = OrgaoSeec.Id,
            Justificativa = "Avaliação prévia de riscos concluída.",
            AutorizadaPor = "SGDI",
            DataAutorizacao = new DateOnly(2026, 11, 12)
        }, sgdiCtx);

        Assert.Equal(2, (await _service.ListarAutorizacoesAsync(null)).Count);

        var doOrgao = await _service.ListarAutorizacoesAsync(OrgaoSes.Id);
        Assert.Single(doOrgao);
        Assert.Equal(OrgaoSes.Id, doOrgao[0].OrgaoId);
    }
}
