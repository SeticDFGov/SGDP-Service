using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using api.Contratacoes;
using Controllers.Contratacoes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Contratacoes;
using Models.Pgia;
using Repositorio.Interface;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Classificação de riscos no ciclo de vida do processo (rodada da Supervisão Contínua
/// das Contratações): POST nulo = não classificado, PUT nulo PRESERVA, limpar apaga os
/// filhos, classificação enviada substitui a lista e audita, reenvio idêntico é
/// idempotente, importação e checkpoint não tocam, filtros como predicado EF, painel,
/// nível máximo em lote e o nome novo do arquivo exportado.
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

    /// <summary>Grupo nulo = "Nenhuma das alternativas acima".</summary>
    private static CtrClassificacaoRiscoDTO Classificacao(string[]? q15, string[]? q16, string[]? q17,
        params CtrRiscoDeclaradoDTO[] riscos) => new()
    {
        Q15 = q15?.ToList() ?? new List<string>(),
        Q16 = q16?.ToList() ?? new List<string>(),
        Q17 = q17?.ToList() ?? new List<string>(),
        Q15Nenhuma = q15 == null,
        Q16Nenhuma = q16 == null,
        Q17Nenhuma = q17 == null,
        RiscosDeclarados = riscos.ToList()
    };

    private async Task<CtrUserContext> ContextoAdminAsync() => (await Permissoes.GetContextAsync(UserAdmin.Email, PerfilDe(UserAdmin)))!;

    private async Task<CtrProcessoResponse> CriarAsync(string numero, CtrClassificacaoRiscoDTO? classificacao)
    {
        var dto = NovoProcessoDto(numero);
        dto.ClassificacaoRisco = classificacao;
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

    // ══ Ciclo de vida ═════════════════════════════════════════════════════════

    [Fact]
    public async Task Criar_SemClassificacao_NasceNaoClassificado()
    {
        var criado = await CriarAsync("04044-00000500/2026-11", null);

        Assert.Null(criado.RiscoClassificado);
        Assert.Null(criado.NivelMaximoRiscoDeclarado);
        Assert.Null(criado.ClassificacaoRisco);

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.Null(entidade.ChecklistRisco);
        Assert.Null(entidade.RiscoClassificadoEm);
        Assert.Empty(RiscosNoBanco(criado.Id));
    }

    [Fact]
    public async Task Criar_ComClassificacao_CalculaGravaEAudita()
    {
        var antes = DateTime.UtcNow;
        var criado = await CriarAsync("04044-00000501/2026-12", Classificacao(null, new[] { "IX", "II" }, null,
            Risco("Quase certo", "Catastrófica"), Risco("Raro", "Menor", "Indisponibilidade do fornecedor")));
        var depois = DateTime.UtcNow;

        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, criado.RiscoClassificado);
        Assert.Equal(CtrDominios.NivelRisco.Extremo, criado.NivelMaximoRiscoDeclarado);

        var classificacao = criado.ClassificacaoRisco!;
        Assert.Equal("art. 16, II", classificacao.EnquadramentoRisco);
        Assert.Equal(3 + 4, classificacao.PontuacaoRisco);
        Assert.Equal(new[] { "II", "IX" }, classificacao.Q16);
        Assert.True(classificacao.Q15Nenhuma);
        Assert.False(classificacao.Q16Nenhuma);
        Assert.Equal(UserAnalista.Email, classificacao.ClassificadoPor);
        Assert.InRange(classificacao.ClassificadoEm, antes, depois);
        Assert.Equal(new[] { "Extremo", "Baixo" }, classificacao.RiscosDeclarados.Select(r => r.Nivel));
        Assert.All(classificacao.RiscosDeclarados, r => Assert.True(r.Id > 0));

        var riscos = RiscosNoBanco(criado.Id);
        Assert.Equal(2, riscos.Count);
        Assert.All(riscos, r => Assert.Equal(UserAnalista.Email, r.CriadoPor));
    }

    [Fact]
    public async Task Criar_EmailDoResponsavel_EhGravadoEDevolvidoEmMinusculas()
    {
        var criado = await CriarAsync("04044-00000513/2026-14", Classificacao(null, new[] { "I" }, null,
            Risco("Raro", "Menor", email: "Fulano.Tal@Orgao.DF.gov.br")));

        Assert.Equal("fulano.tal@orgao.df.gov.br",
            Assert.Single(criado.ClassificacaoRisco!.RiscosDeclarados).ResponsavelEmail);
        Assert.Equal("fulano.tal@orgao.df.gov.br", Assert.Single(RiscosNoBanco(criado.Id)).ResponsavelEmail);
        Assert.Equal("fulano.tal@orgao.df.gov.br",
            Assert.Single((await _processos.GetAsync(criado.Id)).ClassificacaoRisco!.RiscosDeclarados).ResponsavelEmail);
    }

    [Fact]
    public async Task Criar_ClassificacaoIncompleta_NaoGravaNada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarAsync("04044-00000502/2026-13", Classificacao(null, null, null)));

        Assert.Equal((int)ErrorCode.CtrClassificacaoRiscoInvalida, ex.Error.Code);
        Assert.Empty(Context.CtrProcessos);
        Assert.Empty(Context.CtrRiscosDeclarados);
    }

    [Fact]
    public async Task Leituras_AninhadoSoNoDetalhe_PlanosEmTodas()
    {
        var criado = await CriarAsync("04044-00000503/2026-14", Classificacao(null, null, new[] { "I" },
            Risco("Provável", "Menor")));

        var detalhe = await _processos.GetAsync(criado.Id);
        Assert.NotNull(detalhe.ClassificacaoRisco);
        Assert.Equal(CtrDominios.NivelRisco.Alto, Assert.Single(detalhe.ClassificacaoRisco!.RiscosDeclarados).Nivel);

        var item = Assert.Single((await _processos.ListarAsync(new CtrProcessoFiltro())).Items);
        Assert.Null(item.ClassificacaoRisco);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, item.RiscoClassificado);
        Assert.Equal(CtrDominios.NivelRisco.Alto, item.NivelMaximoRiscoDeclarado);
    }

    [Fact]
    public async Task Atualizar_ClassificacaoNula_PreservaAGravadaEOsRiscos()
    {
        var criado = await CriarAsync("04044-00000504/2026-15", Classificacao(new[] { "II" }, null, null,
            Risco("Possível", "Maior")));
        var idsAntes = criado.ClassificacaoRisco!.RiscosDeclarados.Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000504/2026-15");
        dto.Objeto = "Objeto revisto na edição";
        dto.ClassificacaoRisco = null;
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal("Objeto revisto na edição", atualizado.Objeto);
        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, atualizado.RiscoClassificado);
        Assert.Equal(criado.ClassificacaoRisco.ClassificadoEm, atualizado.ClassificacaoRisco!.ClassificadoEm);
        Assert.Equal(UserAnalista.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
    }

    [Fact]
    public async Task Atualizar_ClassificacaoEnviada_SubstituiOsRiscosERegravaAuditoria()
    {
        var criado = await CriarAsync("04044-00000505/2026-16", Classificacao(null, new[] { "III" }, null,
            Risco("Raro", "Menor"), Risco("Improvável", "Maior", "Vazamento de dados")));
        var idsAntigos = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000505/2026-16");
        dto.ClassificacaoRisco = Classificacao(new[] { "IV" }, new[] { "III" }, null, Risco("Provável", "Catastrófica"));
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal(PgiaDominios.ResultadoRisco.Excessivo, atualizado.RiscoClassificado);
        Assert.Equal("art. 15, IV", atualizado.ClassificacaoRisco!.EnquadramentoRisco);
        Assert.Equal(UserAdmin.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.True(atualizado.ClassificacaoRisco.ClassificadoEm >= criado.ClassificacaoRisco!.ClassificadoEm);

        var riscos = RiscosNoBanco(criado.Id);
        var risco = Assert.Single(riscos);
        Assert.DoesNotContain(risco.Id, idsAntigos);
        Assert.Equal(UserAdmin.Email, risco.CriadoPor);
        Assert.Equal(CtrDominios.NivelRisco.Extremo, atualizado.NivelMaximoRiscoDeclarado);
    }

    [Fact]
    public async Task Atualizar_LimparClassificacao_ApagaColunasEOsRiscosDeclarados()
    {
        var criado = await CriarAsync("04044-00000506/2026-17", Classificacao(null, null, null,
            Risco("Raro", "Menor"), Risco("Possível", "Moderada", "Outro risco")));

        var dto = NovoProcessoDto("04044-00000506/2026-17");
        dto.LimparClassificacaoRisco = true;
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAnalistaAsync());

        Assert.Null(atualizado.RiscoClassificado);
        Assert.Null(atualizado.NivelMaximoRiscoDeclarado);
        Assert.Null(atualizado.ClassificacaoRisco);

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.Null(entidade.ChecklistRisco);
        Assert.Null(entidade.RiscoClassificado);
        Assert.Null(entidade.EnquadramentoRisco);
        Assert.Null(entidade.PontuacaoRisco);
        Assert.Null(entidade.RiscoClassificadoEm);
        Assert.Null(entidade.RiscoClassificadoPor);
        Assert.Empty(RiscosNoBanco(criado.Id));
    }

    [Fact]
    public async Task Atualizar_ClassificacaoELimparJuntos_EhRecusadoSemRastro()
    {
        var criado = await CriarAsync("04044-00000507/2026-18", Classificacao(null, new[] { "I" }, null,
            Risco("Raro", "Menor")));

        var dto = NovoProcessoDto("04044-00000507/2026-18");
        dto.Objeto = "Não pode entrar";
        dto.ClassificacaoRisco = Classificacao(new[] { "I" }, null, null);
        dto.LimparClassificacaoRisco = true;
        var ctx = await ContextoAnalistaAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(criado.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrClassificacaoRiscoInvalida, ex.Error.Code);
        Assert.Contains("não os dois", ex.Error.Message);

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.NotEqual("Não pode entrar", entidade.Objeto);
        Assert.Null(entidade.AlteradoEm);
        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, entidade.RiscoClassificado);
        Assert.Single(RiscosNoBanco(criado.Id));
    }

    [Fact]
    public async Task Atualizar_ClassificacaoInvalida_NaoDeixaRastroNaEntidade()
    {
        var criado = await CriarAsync("04044-00000508/2026-19", Classificacao(null, null, new[] { "II" },
            Risco("Raro", "Menor")));

        var dto = NovoProcessoDto("04044-00000508/2026-19");
        dto.Objeto = "Não pode entrar";
        var invalida = Classificacao(new[] { "I" }, null, null);
        invalida.Q16Nenhuma = false; // grupo do art. 16 sem resposta
        dto.ClassificacaoRisco = invalida;
        var ctx = await ContextoAnalistaAsync();

        await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(criado.Id, dto, ctx));

        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        Assert.NotEqual("Não pode entrar", entidade.Objeto);
        Assert.Null(entidade.AlteradoEm);
        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, entidade.RiscoClassificado);
        Assert.Single(RiscosNoBanco(criado.Id));
    }

    // ══ Idempotência do reenvio ═══════════════════════════════════════════════

    [Fact]
    public async Task Atualizar_MesmaClassificacaoComRiscosEmOutraOrdem_MantemAuditoriaEIds()
    {
        var criado = await CriarAsync("04044-00000509/2026-10", Classificacao(null, new[] { "II", "IX" }, null,
            Risco("Raro", "Menor", "Risco A", "GESTOR.Contrato@SEEC.df.gov.br"), Risco("Quase certo", "Maior", "Risco B")));

        // Instante conhecido no passado: "manter" fica inequívoco
        var instante = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        var entidade = Context.CtrProcessos.Single(p => p.Id == criado.Id);
        entidade.RiscoClassificadoEm = instante;
        Context.SaveChanges();
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        // Mesmo conteúdo normalizado: riscos em outra ordem, incisos em outra ordem,
        // textos com espaços e o e-mail com outra caixa
        var dto = NovoProcessoDto("04044-00000509/2026-10");
        dto.Objeto = "Edição de outro campo do processo";
        dto.ClassificacaoRisco = Classificacao(null, new[] { "IX", "II" }, null,
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
        Assert.All(atualizado.ClassificacaoRisco.RiscosDeclarados,
            r => Assert.Equal("gestor.contrato@seec.df.gov.br", r.ResponsavelEmail));

        // O salvamento do processo continua registrado na auditoria DO PROCESSO
        Assert.Equal("Edição de outro campo do processo", atualizado.Objeto);
        Assert.Equal(UserAdmin.Email, atualizado.AlteradoPor);
    }

    [Fact]
    public async Task Atualizar_ClassificacaoComUmIncisoAMais_RegravaERecalcula()
    {
        var criado = await CriarAsync("04044-00000510/2026-11", Classificacao(null, new[] { "II", "IX" }, null,
            Risco("Raro", "Menor", "Risco A")));

        var instante = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        Context.CtrProcessos.Single(p => p.Id == criado.Id).RiscoClassificadoEm = instante;
        Context.SaveChanges();
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var dto = NovoProcessoDto("04044-00000510/2026-11");
        dto.ClassificacaoRisco = Classificacao(null, new[] { "II", "IX", "I" }, null, Risco("Raro", "Menor", "Risco A"));
        var atualizado = await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Equal("art. 16, I", atualizado.ClassificacaoRisco!.EnquadramentoRisco);
        Assert.Equal(4 + 3 + 4, atualizado.ClassificacaoRisco.PontuacaoRisco);
        Assert.True(atualizado.ClassificacaoRisco.ClassificadoEm > instante);
        Assert.Equal(UserAdmin.Email, atualizado.ClassificacaoRisco.ClassificadoPor);
        Assert.DoesNotContain(Assert.Single(RiscosNoBanco(criado.Id)).Id, idsAntes);
    }

    [Fact]
    public async Task Atualizar_MesmosRiscosComMultiplicidadeDiferente_NaoEhReenvio()
    {
        // Multiconjunto: dois riscos iguais gravados ≠ um só enviado
        var criado = await CriarAsync("04044-00000511/2026-12", Classificacao(null, new[] { "I" }, null,
            Risco("Raro", "Menor"), Risco("Raro", "Menor")));
        Assert.Equal(2, RiscosNoBanco(criado.Id).Count);

        var dto = NovoProcessoDto("04044-00000511/2026-12");
        dto.ClassificacaoRisco = Classificacao(null, new[] { "I" }, null, Risco("Raro", "Menor"));
        await _processos.AtualizarAsync(criado.Id, dto, await ContextoAdminAsync());

        Assert.Single(RiscosNoBanco(criado.Id));
    }

    // ══ Checkpoint e importação não tocam a classificação ═════════════════════

    [Fact]
    public async Task Checkpoint_NaoTocaAClassificacao()
    {
        var criado = await CriarAsync("04044-00000512/2026-13", Classificacao(null, null, new[] { "IV" },
            Risco("Possível", "Maior")));
        var idsAntes = RiscosNoBanco(criado.Id).Select(r => r.Id).ToList();

        var resposta = await _processos.RegistrarCheckpointAsync(criado.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.ChegadaSgdi,
            Data = DiasAtras(2)
        }, await ContextoAdminAsync());

        Assert.Equal(PgiaDominios.ResultadoRisco.Moderado, resposta.RiscoClassificado);
        Assert.Equal(UserAnalista.Email, resposta.ClassificacaoRisco!.ClassificadoPor);
        Assert.Equal(criado.ClassificacaoRisco!.ClassificadoEm, resposta.ClassificacaoRisco.ClassificadoEm);
        Assert.Equal(idsAntes, RiscosNoBanco(criado.Id).Select(r => r.Id));
    }

    [Fact]
    public async Task Importar_PlanilhaRealDeNovo_NaoApagaAClassificacao()
    {
        var ctx = await ContextoAnalistaAsync();
        await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);

        var processo = Context.CtrProcessos.Single(p => p.NumeroProcesso == "00220-00008043/2026-13" && p.Ativo);
        var dto = ComoUpdate(CtrProcessoService.DtoDe(processo));
        dto.ClassificacaoRisco = Classificacao(null, new[] { "IV" }, null, Risco("Provável", "Maior"));
        var classificado = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        var idsAntes = RiscosNoBanco(processo.Id).Select(r => r.Id).ToList();
        Assert.Single(idsAntes);

        // A prévia não carrega classificação nenhuma nas linhas
        var previa = await NovoImportacaoService().PreviaAsync(PlanilhaReal());
        Assert.All(previa.Linhas.Where(l => l.Dados != null), l => Assert.Null(l.Dados!.ClassificacaoRisco));

        var relatorio = await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);
        Assert.Equal(0, relatorio.Rejeitados);

        Assert.Equal(PgiaDominios.ResultadoRisco.Alto, processo.RiscoClassificado);
        Assert.Equal("art. 16, IV", processo.EnquadramentoRisco);
        Assert.Equal(4, processo.PontuacaoRisco);
        Assert.Equal(classificado.ClassificacaoRisco!.ClassificadoEm, processo.RiscoClassificadoEm);
        Assert.Equal(UserAnalista.Email, processo.RiscoClassificadoPor);
        Assert.NotNull(processo.ChecklistRisco);
        Assert.Equal(idsAntes, RiscosNoBanco(processo.Id).Select(r => r.Id));

        var detalhe = await _processos.GetAsync(processo.Id);
        Assert.Equal(CtrDominios.NivelRisco.Extremo, detalhe.NivelMaximoRiscoDeclarado);
    }

    // ══ Filtros ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Filtro_RiscoClassificado_OsQuatroResultadosENaoClassificado()
    {
        await CriarAsync("04044-00000520/2026-11", Classificacao(new[] { "I" }, null, null));
        await CriarAsync("04044-00000521/2026-12", Classificacao(null, new[] { "I" }, null));
        await CriarAsync("04044-00000522/2026-13", Classificacao(null, null, new[] { "I" }));
        await CriarAsync("04044-00000523/2026-14", Classificacao(null, null, null, Risco("Raro", "Menor")));
        await CriarAsync("04044-00000524/2026-15", null);

        async Task<List<string>> Numeros(string? valor) =>
            (await _processos.ListarAsync(new CtrProcessoFiltro { RiscoClassificado = valor }))
            .Items.Select(p => p.NumeroProcesso).ToList();

        Assert.Equal(new[] { "04044-00000520/2026-11" }, await Numeros("Risco Excessivo"));
        Assert.Equal(new[] { "04044-00000521/2026-12" }, await Numeros("Alto Risco"));
        Assert.Equal(new[] { "04044-00000522/2026-13" }, await Numeros("Risco Moderado"));
        Assert.Equal(new[] { "04044-00000523/2026-14" }, await Numeros("Baixo Risco"));
        Assert.Equal(new[] { "04044-00000524/2026-15" }, await Numeros("Não classificado"));
        // Fora do domínio: lista vazia, nunca "todos" em silêncio
        Assert.Empty(await Numeros("Risco Crítico"));
        Assert.Equal(5, (await Numeros("  ")).Count);
    }

    [Fact]
    public async Task Filtro_NivelRiscoDeclarado_UsaONivelMaximoDoProcesso()
    {
        await CriarAsync("04044-00000530/2026-11", Classificacao(null, null, null,
            Risco("Quase certo", "Catastrófica"), Risco("Improvável", "Desprezível", "Risco baixo")));
        await CriarAsync("04044-00000531/2026-12", Classificacao(null, null, null,
            Risco("Raro", "Maior"), Risco("Possível", "Menor", "Outro médio")));
        await CriarAsync("04044-00000532/2026-13", Classificacao(null, null, null, Risco("Provável", "Menor")));
        await CriarAsync("04044-00000533/2026-14", Classificacao(null, null, null, Risco("Raro", "Desprezível")));
        await CriarAsync("04044-00000534/2026-15", Classificacao(null, new[] { "V" }, null));
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
    public async Task Exportar_ReusaOsFiltrosDeRisco()
    {
        await CriarAsync("04044-00000540/2026-11", Classificacao(null, new[] { "I" }, null));
        await CriarAsync("04044-00000541/2026-12", null);

        var csv = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { RiscoClassificado = "Alto Risco" });
        Assert.Equal("04044-00000540/2026-11", Assert.Single(CtrCsv.Ler(csv)).NumeroProcesso);

        var vazio = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { NivelRiscoDeclarado = "Crítico" });
        Assert.Empty(CtrCsv.Ler(vazio));
    }

    // ══ Painel ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Painel_ContaPorRiscoClassificadoEPorNivelMaximo_ComTodasAsChaves()
    {
        await CriarAsync("04044-00000550/2026-11", Classificacao(null, new[] { "I" }, null));
        await CriarAsync("04044-00000551/2026-12", Classificacao(null, null, new[] { "I" },
            Risco("Quase certo", "Catastrófica"), Risco("Improvável", "Desprezível", "Baixo")));
        await CriarAsync("04044-00000552/2026-13", Classificacao(null, null, null, Risco("Possível", "Moderada")));
        await CriarAsync("04044-00000553/2026-14", null);
        await CriarAsync("04044-00000554/2026-15", Classificacao(null, new[] { "II" }, null));
        // Excluído (soft delete) não entra em contagem nenhuma
        var excluido = await CriarAsync("04044-00000555/2026-16", Classificacao(new[] { "I" }, null, null,
            Risco("Provável", "Menor")));
        await _processos.ExcluirAsync(excluido.Id, await ContextoAnalistaAsync());

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(5, painel.TotalAtivos);
        Assert.Equal(
            new[]
            {
                ("Alto Risco", 2), ("Risco Moderado", 1), ("Baixo Risco", 1), ("Não classificado", 1),
                ("Risco Excessivo", 0)
            },
            painel.PorRiscoClassificado.Select(c => (c.Chave, c.Quantidade)));
        Assert.Equal(
            new[] { ("Sem riscos declarados", 3), ("Médio", 1), ("Extremo", 1), ("Baixo", 0), ("Alto", 0) },
            painel.PorNivelRiscoDeclarado.Select(c => (c.Chave, c.Quantidade)));
    }

    [Fact]
    public async Task Painel_SemProcessos_TrazAsCincoChavesDeCadaBlocoZeradas()
    {
        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(new[] { "Risco Excessivo", "Alto Risco", "Risco Moderado", "Baixo Risco", "Não classificado" },
            painel.PorRiscoClassificado.Select(c => c.Chave));
        Assert.Equal(new[] { "Baixo", "Médio", "Alto", "Extremo", "Sem riscos declarados" },
            painel.PorNivelRiscoDeclarado.Select(c => c.Chave));
        Assert.All(painel.PorRiscoClassificado.Concat(painel.PorNivelRiscoDeclarado), c => Assert.Equal(0, c.Quantidade));
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
        await CriarAsync("04044-00000560/2026-11", Classificacao(null, null, null, Risco("Quase certo", "Maior")));
        await CriarAsync("04044-00000561/2026-12", Classificacao(null, null, null, Risco("Raro", "Menor")));
        await CriarAsync("04044-00000562/2026-13", Classificacao(null, new[] { "I" }, null));

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

    // ══ Item 1: nome do módulo no arquivo exportado ═══════════════════════════

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
