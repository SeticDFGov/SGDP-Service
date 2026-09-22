using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using api.Contratacoes;
using Controllers.Contratacoes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Contratacoes;
using Repositorio.Interface;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Riscos da contratação no ciclo de vida do processo: POST nulo ou vazio = sem riscos,
/// PUT nulo PRESERVA, a lista enviada substitui (vazia tira todos), limpar tira todos,
/// reenvio idêntico não troca Ids nem quem/quando, importação e checkpoint não tocam,
/// filtro por nível como predicado EF, painel, nível máximo em lote e o nome do arquivo
/// exportado. Desde 2026-09-21 não há mais o questionário dos arts. 15 a 17 do PGIA: as
/// colunas legadas dele continuam no banco e são zeradas quando os riscos são mexidos.
/// </summary>
public class CtrSupervisaoClassificacaoRiscoTest : CtrTestBase
{
    private readonly CtrProcessoService _processos;

    public CtrSupervisaoClassificacaoRiscoTest()
    {
        _processos = NovoProcessoService();
    }

    private static CtrRiscoDeclaradoDTO Risco(string probabilidade, string consequencia,
        string descricao = "Atraso na entrega dos equipamentos",
        string email = "gestor.contrato@seec.df.gov.br") => new()
    {
        DescricaoRisco = descricao,
        AcaoMitigacao = "Cronograma com marcos e cláusula de multa",
        ResponsavelNome = "Gestor do contrato",
        ResponsavelEmail = email,
        Probabilidade = probabilidade,
        Consequencia = consequencia
    };

    private static CtrClassificacaoRiscoDTO Riscos(params CtrRiscoDeclaradoDTO[] riscos) => new()
    {
        RiscosDeclarados = riscos.ToList()
    };

    private async Task<CtrUserContext> ContextoAdminAsync() => (await Permissoes.GetContextAsync(UserAdmin.Email, PerfilDe(UserAdmin)))!;

    private async Task<CtrProcessoResponse> CriarAsync(string numero, CtrClassificacaoRiscoDTO? riscos)
    {
        var dto = NovoProcessoDto(numero);
        dto.ClassificacaoRisco = riscos;
        return await _processos.CriarAsync(dto, await ContextoAnalistaAsync());
    }

    private List<CtrRiscoDeclarado> RiscosNoBanco(long processoId) =>
        Context.CtrRiscosDeclarados.Where(r => r.ProcessoId == processoId).OrderBy(r => r.Id).ToList();

    private static CtrProcessoUpdateDTO ComoUpdate(CtrProcessoCreateDTO origem)
    {
        var update = new CtrProcessoUpdateDTO();
        foreach (var propriedade in typeof(CtrProcessoCreateDTO).GetProperties())
            propriedade.SetValue(update, propriedade.GetValue(origem));
        return update;
    }

    /// <summary>Instante conhecido no passado nos riscos gravados: "manter" fica inequívoco.</summary>
    private void DatarRiscos(long processoId, DateTime instante)
    {
        foreach (var risco in Context.CtrRiscosDeclarados.Where(r => r.ProcessoId == processoId))
            risco.CriadoEm = instante;
        Context.SaveChanges();
    }

    /// <summary>Processo classificado ANTES de 2026-09-21: o questionário do PGIA nas colunas legadas.</summary>
    private void GravarQuestionarioLegado(long processoId)
    {
        var entidade = Context.CtrProcessos.Single(p => p.Id == processoId);
        entidade.ChecklistRisco =
            "{\"q15\":[],\"q16\":[\"IV\"],\"q17\":[],\"q15_nenhuma\":true,\"q16_nenhuma\":false,\"q17_nenhuma\":true}";
        entidade.RiscoClassificado = "Alto Risco";
        entidade.EnquadramentoRisco = "art. 16, IV";
        entidade.PontuacaoRisco = 4;
        entidade.RiscoClassificadoEm = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        entidade.RiscoClassificadoPor = UserAnalista.Email;
        Context.SaveChanges();
    }

    private void AssertQuestionarioLegadoZerado(long processoId)
    {
        var entidade = Context.CtrProcessos.Single(p => p.Id == processoId);
        Assert.Null(entidade.ChecklistRisco);
        Assert.Null(entidade.RiscoClassificado);
        Assert.Null(entidade.EnquadramentoRisco);
        Assert.Null(entidade.PontuacaoRisco);
        Assert.Null(entidade.RiscoClassificadoEm);
        Assert.Null(entidade.RiscoClassificadoPor);
    }

    // ══ Ciclo de vida ═════════════════════════════════════════════════════════

    [Fact]
    public async Task Criar_SemRiscos_NasceSemRiscos()
    {
        var nulo = await CriarAsync("04044-00000500/2026-11", null);
        var vazio = await CriarAsync("04044-00000514/2026-15", Riscos());

        foreach (var criado in new[] { nulo, vazio })
        {
            Assert.Null(criado.NivelMaximoRiscoDeclarado);
            Assert.Null(criado.ClassificacaoRisco);
            Assert.Empty(RiscosNoBanco(criado.Id));
            AssertQuestionarioLegadoZerado(criado.Id);
        }
    }

    [Fact]
    public async Task Criar_ComRiscos_GravaERegistraQuemEQuando()
    {
        var antes = DateTime.UtcNow;
        var criado = await CriarAsync("04044-00000501/2026-12", Riscos(
            Risco("Quase certo", "Catastrófica"), Risco("Raro", "Menor", "Indisponibilidade do fornecedor")));
        var depois = DateTime.UtcNow;

        Assert.Equal(CtrDominios.NivelRisco.Extremo, criado.NivelMaximoRiscoDeclarado);

        var riscos = criado.ClassificacaoRisco!;
        Assert.Equal(UserAnalista.Email, riscos.ClassificadoPor);
        Assert.InRange(riscos.ClassificadoEm, antes, depois);
        Assert.Equal(new[] { "Extremo", "Baixo" }, riscos.RiscosDeclarados.Select(r => r.Nivel));
        Assert.All(riscos.RiscosDeclarados, r => Assert.True(r.Id > 0));

        var gravados = RiscosNoBanco(criado.Id);
        Assert.Equal(2, gravados.Count);
        Assert.All(gravados, r => Assert.Equal(UserAnalista.Email, r.CriadoPor));
        // O questionário do PGIA não é mais escrito
        AssertQuestionarioLegadoZerado(criado.Id);
    }

    [Fact]
    public async Task Criar_EmailDoResponsavel_EhGravadoEDevolvidoEmMinusculas()
    {
        var criado = await CriarAsync("04044-00000513/2026-14", Riscos(
            Risco("Raro", "Menor", email: "Fulano.Tal@Orgao.DF.gov.br")));

        Assert.Equal("fulano.tal@orgao.df.gov.br",
            Assert.Single(criado.ClassificacaoRisco!.RiscosDeclarados).ResponsavelEmail);
        Assert.Equal("fulano.tal@orgao.df.gov.br", Assert.Single(RiscosNoBanco(criado.Id)).ResponsavelEmail);
        Assert.Equal("fulano.tal@orgao.df.gov.br",
            Assert.Single((await _processos.GetAsync(criado.Id)).ClassificacaoRisco!.RiscosDeclarados).ResponsavelEmail);
    }

    [Fact]
    public async Task Criar_RiscoIncompleto_NaoGravaNada()
    {
        var incompleto = Risco("Raro", "Menor");
        incompleto.AcaoMitigacao = "  ";

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarAsync("04044-00000502/2026-13", Riscos(Risco("Possível", "Maior"), incompleto)));

        Assert.Equal((int)ErrorCode.CtrClassificacaoRiscoInvalida, ex.Error.Code);
        Assert.Empty(Context.CtrProcessos);
        Assert.Empty(Context.CtrRiscosDeclarados);
    }

    [Fact]
    public async Task Leituras_AninhadoSoNoDetalhe_NivelMaximoEmTodas()
    {
        var criado = await CriarAsync("04044-00000503/2026-14", Riscos(Risco("Provável", "Menor")));

        var detalhe = await _processos.GetAsync(criado.Id);
        Assert.NotNull(detalhe.ClassificacaoRisco);
        Assert.Equal(CtrDominios.NivelRisco.Alto, Assert.Single(detalhe.ClassificacaoRisco!.RiscosDeclarados).Nivel);

        var item = Assert.Single((await _processos.ListarAsync(new CtrProcessoFiltro())).Items);
        Assert.Null(item.ClassificacaoRisco);
        Assert.Equal(CtrDominios.NivelRisco.Alto, item.NivelMaximoRiscoDeclarado);
    }

    [Fact]
    public async Task Atualizar_ClassificacaoNula_PreservaOsRiscos()
    {
        var criado = await CriarAsync("04044-00000504/2026-15", Riscos(Risco("Possível", "Maior")));
        var idsAntes = criado.ClassificacaoRisco!.RiscosDeclarados.Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000504/2026-15");
        dto.Objeto = "Objeto revisto na edição";
        dto.ClassificacaoRisco = null;
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal("Objeto revisto na edição", atualizado.Objeto);
        Assert.Equal(CtrDominios.NivelRisco.Alto, atualizado.NivelMaximoRiscoDeclarado);
        Assert.Equal(criado.ClassificacaoRisco.ClassificadoEm, atualizado.ClassificacaoRisco!.ClassificadoEm);
        Assert.Equal(UserAnalista.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
    }

    [Fact]
    public async Task Atualizar_ListaEnviada_SubstituiOsRiscosERegistraQuemEQuando()
    {
        var criado = await CriarAsync("04044-00000505/2026-16", Riscos(
            Risco("Raro", "Menor"), Risco("Improvável", "Maior", "Vazamento de dados")));
        var idsAntigos = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000505/2026-16");
        dto.ClassificacaoRisco = Riscos(Risco("Provável", "Catastrófica"));
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal(UserAdmin.Email, atualizado.ClassificacaoRisco!.ClassificadoPor);
        Assert.True(atualizado.ClassificacaoRisco.ClassificadoEm >= criado.ClassificacaoRisco!.ClassificadoEm);

        var risco = Assert.Single(RiscosNoBanco(criado.Id));
        Assert.DoesNotContain(risco.Id, idsAntigos);
        Assert.Equal(UserAdmin.Email, risco.CriadoPor);
        Assert.Equal(CtrDominios.NivelRisco.Extremo, atualizado.NivelMaximoRiscoDeclarado);
    }

    [Fact]
    public async Task Atualizar_ListaVazia_TiraTodosOsRiscos()
    {
        var criado = await CriarAsync("04044-00000515/2026-16", Riscos(
            Risco("Raro", "Menor"), Risco("Possível", "Moderada", "Outro risco")));

        var dto = NovoProcessoDto("04044-00000515/2026-16");
        dto.ClassificacaoRisco = Riscos();
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAnalistaAsync());

        Assert.Null(atualizado.NivelMaximoRiscoDeclarado);
        Assert.Null(atualizado.ClassificacaoRisco);
        Assert.Empty(RiscosNoBanco(criado.Id));
    }

    [Fact]
    public async Task Atualizar_LimparClassificacao_TiraTodosOsRiscos()
    {
        var criado = await CriarAsync("04044-00000506/2026-17", Riscos(
            Risco("Raro", "Menor"), Risco("Possível", "Moderada", "Outro risco")));

        var dto = NovoProcessoDto("04044-00000506/2026-17");
        dto.LimparClassificacaoRisco = true;
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAnalistaAsync());

        Assert.Null(atualizado.NivelMaximoRiscoDeclarado);
        Assert.Null(atualizado.ClassificacaoRisco);
        Assert.Empty(RiscosNoBanco(criado.Id));
        AssertQuestionarioLegadoZerado(criado.Id);
    }

    [Fact]
    public async Task Atualizar_ClassificacaoELimparJuntos_EhRecusadoSemRastro()
    {
        var criado = await CriarAsync("04044-00000507/2026-18", Riscos(Risco("Raro", "Menor")));

        var dto = NovoProcessoDto("04044-00000507/2026-18");
        dto.Objeto = "Não pode entrar";
        dto.ClassificacaoRisco = Riscos(Risco("Provável", "Maior"));
        dto.LimparClassificacaoRisco = true;
        var ctx = await ContextoAnalistaAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(criado.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrClassificacaoRiscoInvalida, ex.Error.Code);
        Assert.Contains("não os dois", ex.Error.Message);

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.NotEqual("Não pode entrar", entidade.Objeto);
        Assert.Null(entidade.AlteradoEm);
        Assert.Equal("Raro", Assert.Single(RiscosNoBanco(criado.Id)).Probabilidade);
    }

    [Fact]
    public async Task Atualizar_RiscoInvalido_NaoDeixaRastroNaEntidade()
    {
        var criado = await CriarAsync("04044-00000508/2026-19", Riscos(Risco("Raro", "Menor")));
        GravarQuestionarioLegado(criado.Id);

        var dto = NovoProcessoDto("04044-00000508/2026-19");
        dto.Objeto = "Não pode entrar";
        dto.ClassificacaoRisco = Riscos(Risco("Raro", "Menor", email: "gestor@seec"));
        var ctx = await ContextoAnalistaAsync();

        await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(criado.Id, dto, ctx));

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.NotEqual("Não pode entrar", entidade.Objeto);
        Assert.Null(entidade.AlteradoEm);
        // Nem o questionário legado é zerado quando o envio é recusado
        Assert.Equal("Alto Risco", entidade.RiscoClassificado);
        Assert.Single(RiscosNoBanco(criado.Id));
    }

    // ══ Reenvio idêntico ══════════════════════════════════════════════════════

    [Fact]
    public async Task Atualizar_MesmosRiscosEmOutraOrdem_MantemIdsEQuemQuando()
    {
        var criado = await CriarAsync("04044-00000509/2026-10", Riscos(
            Risco("Raro", "Menor", "Risco A", "GESTOR.Contrato@SEEC.df.gov.br"), Risco("Quase certo", "Maior", "Risco B")));

        var instante = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        DatarRiscos(criado.Id, instante);
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        // Mesmo conteúdo normalizado: riscos em outra ordem, textos com espaços e o
        // e-mail com outra caixa
        var dto = NovoProcessoDto("04044-00000509/2026-10");
        dto.Objeto = "Edição de outro campo do processo";
        dto.ClassificacaoRisco = Riscos(
            Risco("Quase certo", "Maior", "  Risco B  "),
            Risco("Raro", "Menor", "Risco A", " Gestor.Contrato@seec.df.gov.br "));
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal(instante, atualizado.ClassificacaoRisco!.ClassificadoEm);
        Assert.Equal(UserAnalista.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
        Assert.Equal(idsAntes, atualizado.ClassificacaoRisco.RiscosDeclarados.Select(r => r.Id));

        // A caixa do e-mail não é informação: gravado SEMPRE em minúsculas (desde o POST,
        // que veio com outra caixa), então o reenvio com outra caixa é idêntico de fato e
        // nada é descartado em silêncio
        Assert.All(RiscosNoBanco(criado.Id), r => Assert.Equal("gestor.contrato@seec.df.gov.br", r.ResponsavelEmail));

        // O salvamento do processo continua registrado na auditoria DO PROCESSO
        Assert.Equal("Edição de outro campo do processo", atualizado.Objeto);
        Assert.Equal(UserAdmin.Email, atualizado.AlteradoPor);
    }

    [Fact]
    public async Task Atualizar_RiscoComOutraConsequencia_SubstituiERegistra()
    {
        var criado = await CriarAsync("04044-00000510/2026-11", Riscos(Risco("Raro", "Menor", "Risco A")));

        var instante = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        DatarRiscos(criado.Id, instante);
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000510/2026-11");
        dto.ClassificacaoRisco = Riscos(Risco("Raro", "Maior", "Risco A"));
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.True(atualizado.ClassificacaoRisco!.ClassificadoEm > instante);
        Assert.Equal(UserAdmin.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.Equal(CtrDominios.NivelRisco.Medio, atualizado.NivelMaximoRiscoDeclarado);
        Assert.DoesNotContain(Assert.Single(RiscosNoBanco(criado.Id)).Id, idsAntes);
    }

    [Fact]
    public async Task Atualizar_MesmosRiscosComMultiplicidadeDiferente_NaoEhReenvio()
    {
        // Multiconjunto: dois riscos iguais gravados ≠ um só enviado
        var criado = await CriarAsync("04044-00000511/2026-12", Riscos(Risco("Raro", "Menor"), Risco("Raro", "Menor")));
        Assert.Equal(2, RiscosNoBanco(criado.Id).Count);

        var dto = NovoProcessoDto("04044-00000511/2026-12");
        dto.ClassificacaoRisco = Riscos(Risco("Raro", "Menor"));
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Single(RiscosNoBanco(criado.Id));
    }

    // ══ Questionário legado do PGIA (colunas sem uso desde 2026-09-21) ════════

    [Fact]
    public async Task Legado_QuestionarioSemRiscos_ApareceComoSemRiscos()
    {
        var criado = await CriarAsync("04044-00000570/2026-11", null);
        GravarQuestionarioLegado(criado.Id);

        var detalhe = await _processos.GetAsync(criado.Id);
        Assert.Null(detalhe.ClassificacaoRisco);
        Assert.Null(detalhe.NivelMaximoRiscoDeclarado);

        var semRiscos = await _processos.ListarAsync(new CtrProcessoFiltro
        {
            NivelRiscoDeclarado = CtrDominios.NivelRisco.SemRiscosDeclarados
        });
        Assert.Equal(criado.Id, Assert.Single(semRiscos.Items).Id);

        var painel = await _processos.MontarPainelAsync(15);
        Assert.Equal(1, painel.PorNivelRiscoDeclarado.Single(c => c.Chave == "Sem riscos declarados").Quantidade);
    }

    [Fact]
    public async Task Legado_EditarOutroCampo_NaoMexeNoQuestionario()
    {
        var criado = await CriarAsync("04044-00000571/2026-12", Riscos(Risco("Raro", "Menor")));
        GravarQuestionarioLegado(criado.Id);

        var dto = NovoProcessoDto("04044-00000571/2026-12");
        dto.Observacao = "Só a observação mudou";
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.Equal("Alto Risco", entidade.RiscoClassificado);
        Assert.NotNull(entidade.ChecklistRisco);
        Assert.Single(RiscosNoBanco(criado.Id));
    }

    [Fact]
    public async Task Legado_MexerNosRiscos_ZeraOQuestionario()
    {
        var criado = await CriarAsync("04044-00000572/2026-13", Riscos(Risco("Raro", "Menor")));
        GravarQuestionarioLegado(criado.Id);

        var dto = NovoProcessoDto("04044-00000572/2026-13");
        dto.ClassificacaoRisco = Riscos(Risco("Possível", "Maior"));
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        AssertQuestionarioLegadoZerado(criado.Id);
        Assert.Equal("Possível", Assert.Single(RiscosNoBanco(criado.Id)).Probabilidade);
    }

    [Fact]
    public async Task Legado_ReenvioIdentico_ZeraOQuestionarioEMantemOsRiscos()
    {
        var criado = await CriarAsync("04044-00000573/2026-14", Riscos(Risco("Raro", "Menor")));
        GravarQuestionarioLegado(criado.Id);
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000573/2026-14");
        dto.ClassificacaoRisco = Riscos(Risco("Raro", "Menor"));
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        AssertQuestionarioLegadoZerado(criado.Id);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
    }

    [Fact]
    public async Task Legado_LimparSemRiscos_ZeraOQuestionario()
    {
        var criado = await CriarAsync("04044-00000574/2026-15", null);
        GravarQuestionarioLegado(criado.Id);

        var dto = NovoProcessoDto("04044-00000574/2026-15");
        dto.LimparClassificacaoRisco = true;
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        AssertQuestionarioLegadoZerado(criado.Id);
    }

    // ══ Checkpoint e importação não tocam os riscos ═══════════════════════════

    [Fact]
    public async Task Checkpoint_NaoTocaOsRiscos()
    {
        var criado = await CriarAsync("04044-00000512/2026-13", Riscos(Risco("Possível", "Maior")));
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var resposta = await _processos.RegistrarCheckpointAsync(criado.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.ChegadaSgdi,
            Data = DiasAtras(2)
        }, await ContextoAdminAsync());

        Assert.Equal(CtrDominios.NivelRisco.Alto, resposta.NivelMaximoRiscoDeclarado);
        Assert.Equal(UserAnalista.Email, resposta.ClassificacaoRisco!.ClassificadoPor);
        Assert.Equal(criado.ClassificacaoRisco!.ClassificadoEm, resposta.ClassificacaoRisco.ClassificadoEm);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
    }

    [Fact]
    public async Task Importar_PlanilhaRealDeNovo_NaoApagaOsRiscos()
    {
        var ctx = await ContextoAnalistaAsync();
        await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);

        var processo = Context.CtrProcessos.Single(p => p.NumeroProcesso == "00220-00008043/2026-13" && p.Ativo);
        var dto = ComoUpdate(CtrProcessoService.DtoDe(processo));
        dto.ClassificacaoRisco = Riscos(Risco("Provável", "Maior"));
        var classificado = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        var idsAntes = RiscosNoBanco(processo.Id).Select(r => r.Id).ToList();
        Assert.Single(idsAntes);

        // A prévia não carrega riscos nas linhas
        var previa = await NovoImportacaoService().PreviaAsync(PlanilhaReal());
        Assert.All(previa.Linhas.Where(l => l.Dados != null), l => Assert.Null(l.Dados!.ClassificacaoRisco));

        var relatorio = await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);
        Assert.Equal(0, relatorio.Rejeitados);

        Assert.Equal(idsAntes, RiscosNoBanco(processo.Id).Select(r => r.Id));
        var detalhe = await _processos.GetAsync(processo.Id);
        Assert.Equal(CtrDominios.NivelRisco.Extremo, detalhe.NivelMaximoRiscoDeclarado);
        Assert.Equal(classificado.ClassificacaoRisco!.ClassificadoEm, detalhe.ClassificacaoRisco!.ClassificadoEm);
    }

    // ══ Filtro ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Filtro_NivelRiscoDeclarado_UsaONivelMaximoDoProcesso()
    {
        await CriarAsync("04044-00000530/2026-11", Riscos(
            Risco("Quase certo", "Catastrófica"), Risco("Improvável", "Desprezível", "Risco baixo")));
        await CriarAsync("04044-00000531/2026-12", Riscos(
            Risco("Raro", "Maior"), Risco("Possível", "Menor", "Outro médio")));
        await CriarAsync("04044-00000532/2026-13", Riscos(Risco("Provável", "Menor")));
        await CriarAsync("04044-00000533/2026-14", Riscos(Risco("Raro", "Desprezível")));
        await CriarAsync("04044-00000534/2026-15", Riscos());
        await CriarAsync("04044-00000535/2026-16", null);

        async Task<List<string>> Numeros(string valor) =>
            (await _processos.ListarAsync(new CtrProcessoFiltro { NivelRiscoDeclarado = valor, OrderBy = "NumeroProcesso" }))
            .Items.Select(p => p.NumeroProcesso).ToList();

        Assert.Equal(new[] { "04044-00000530/2026-11" }, await Numeros("Extremo"));
        Assert.Equal(new[] { "04044-00000531/2026-12" }, await Numeros("Médio"));
        Assert.Equal(new[] { "04044-00000532/2026-13" }, await Numeros("Alto"));
        // O processo 530 tem um risco Baixo, mas o seu MÁXIMO é Extremo
        Assert.Equal(new[] { "04044-00000533/2026-14" }, await Numeros("Baixo"));
        Assert.Equal(new[] { "04044-00000534/2026-15", "04044-00000535/2026-16" },
            (await Numeros("Sem riscos declarados")).OrderBy(n => n));
        Assert.Empty(await Numeros("Vermelho"));
    }

    [Fact]
    public async Task Exportar_ReusaOFiltroDeNivel()
    {
        await CriarAsync("04044-00000540/2026-11", Riscos(Risco("Quase certo", "Maior")));
        await CriarAsync("04044-00000541/2026-12", null);

        var csv = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { NivelRiscoDeclarado = "Extremo" });
        Assert.Equal("04044-00000540/2026-11", Assert.Single(CtrCsv.Ler(csv)).NumeroProcesso);

        var vazio = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { NivelRiscoDeclarado = "Crítico" });
        Assert.Empty(CtrCsv.Ler(vazio));
    }

    // ══ Painel ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Painel_ContaPorNivelMaximo_ComTodasAsChaves()
    {
        await CriarAsync("04044-00000550/2026-11", Riscos());
        await CriarAsync("04044-00000551/2026-12", Riscos(
            Risco("Quase certo", "Catastrófica"), Risco("Improvável", "Desprezível", "Baixo")));
        await CriarAsync("04044-00000552/2026-13", Riscos(Risco("Possível", "Moderada")));
        await CriarAsync("04044-00000553/2026-14", null);
        await CriarAsync("04044-00000554/2026-15", null);
        // Excluído (soft delete) não entra em contagem nenhuma
        var excluido = await CriarAsync("04044-00000555/2026-16", Riscos(Risco("Provável", "Menor")));
        await _processos.ExcluirAsync(excluido.Id, await ContextoAnalistaAsync());

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(5, painel.TotalAtivos);
        Assert.Equal(
            new[] { ("Sem riscos declarados", 3), ("Médio", 1), ("Extremo", 1), ("Baixo", 0), ("Alto", 0) },
            painel.PorNivelRiscoDeclarado.Select(c => (c.Chave, c.Quantidade)));
    }

    [Fact]
    public async Task Painel_SemProcessos_TrazAsCincoChavesZeradas()
    {
        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(new[] { "Baixo", "Médio", "Alto", "Extremo", "Sem riscos declarados" },
            painel.PorNivelRiscoDeclarado.Select(c => c.Chave));
        Assert.All(painel.PorNivelRiscoDeclarado, c => Assert.Equal(0, c.Quantidade));
        // O bloco do "risco classificado" pelo questionário do PGIA saiu
        Assert.DoesNotContain(typeof(CtrPainelResponse).GetProperties(), p => p.Name == "PorRiscoClassificado");
    }

    // ══ Lote (sem N+1) ════════════════════════════════════════════════════════

    /// <summary>Repositório que conta as chamadas e repassa ao real (DispatchProxy, sem pacote novo).</summary>
    public class RepositorioContador : DispatchProxy
    {
        public ICtrProcessoRepositorio Alvo { get; set; } = null!;

        public Dictionary<string, int> Chamadas { get; } = new();

        protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
        {
            Chamadas[metodo!.Name] = Chamadas.GetValueOrDefault(metodo.Name) + 1;
            try
            {
                return metodo.Invoke(Alvo, argumentos);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }

    [Fact]
    public async Task Listagem_ResolveONivelMaximoEmLote()
    {
        await CriarAsync("04044-00000560/2026-11", Riscos(Risco("Quase certo", "Maior")));
        await CriarAsync("04044-00000561/2026-12", Riscos(Risco("Raro", "Menor")));
        await CriarAsync("04044-00000562/2026-13", null);

        var proxy = DispatchProxy.Create<ICtrProcessoRepositorio, RepositorioContador>();
        var contador = (RepositorioContador)(object)proxy;
        contador.Alvo = Repositorio;

        var pagina = await new CtrProcessoService(proxy).ListarAsync(new CtrProcessoFiltro());
        var niveis = pagina.Items.ToDictionary(p => p.NumeroProcesso, p => p.NivelMaximoRiscoDeclarado);

        Assert.Equal(3, niveis.Count);
        Assert.Equal("Extremo", niveis["04044-00000560/2026-11"]);
        Assert.Equal("Baixo", niveis["04044-00000561/2026-12"]);
        Assert.Null(niveis["04044-00000562/2026-13"]);
        // Uma consulta de escalas para a página inteira; nenhuma leitura dos riscos completos
        Assert.Equal(1, contador.Chamadas.GetValueOrDefault(nameof(ICtrProcessoRepositorio.ListarEscalasDeRiscoPorProcessosAsync)));
        Assert.False(contador.Chamadas.ContainsKey(nameof(ICtrProcessoRepositorio.ListarRiscosDeclaradosAsync)));
    }

    // ══ Nome do módulo no arquivo exportado ═══════════════════════════════════

    [Fact]
    public async Task Exportar_ArquivoSaiComONomeNovoDoModulo()
    {
        var controller = new CtrProcessoController(NovoProcessoService(), NovoManifestacaoService(), Permissoes)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Email, UserAnalista.Email) }, "teste"))
                }
            }
        };

        var arquivo = Assert.IsType<FileContentResult>(await controller.Exportar(new CtrProcessoFiltro()));

        Assert.Equal("supervisao-continua-contratacoes.csv", arquivo.FileDownloadName);
        Assert.Equal(CtrProcessoController.NomeArquivoExportacao, arquivo.FileDownloadName);
    }
}
