using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// PETIC-DF (E3): rascunho (um por vez), atualização, envio ao CGTIC com pendências,
/// aprovação (vigente única, a anterior substituída), devolução (volta a rascunho),
/// cópia da vigente (registros, códigos, ligações e sequência), exclusão do rascunho
/// nunca enviado e a visão dos papéis de órgão (só as versões aprovadas).
/// </summary>
public class PePeticTest : PeReferenciaisTestBase
{
    [Fact]
    public async Task Rascunho_APrimeiraE1ponto0_EUmPorVez()
    {
        var rascunho = await RascunhoAsync();

        Assert.Equal("1.0", rascunho.Versao);
        Assert.Equal("rascunho", rascunho.Situacao);
        Assert.Equal(new DateOnly(2027, 1, 1), rascunho.VigenciaInicio);
        Assert.Equal(new DateOnly(2030, 12, 31), rascunho.VigenciaFim);
        Assert.Null(rascunho.AnteriorId);
        Assert.Null(rascunho.AprovadoEm);
        Assert.Null(rascunho.Deliberacao);
        Assert.Equal(Codigo(ErrorCode.PeVersaoEmAndamento), await ErroAsync(() => RascunhoAsync("Outra")));
    }

    [Fact]
    public async Task Rascunho_TituloObrigatorio_DatasValidas_FimDepoisDoInicio()
    {
        var ctx = await Admin();
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() =>
            Petics.CriarAsync(new PePeticCriarDTO { Titulo = "  " }, ctx)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() =>
            Petics.CriarAsync(new PePeticCriarDTO { Titulo = "PETIC", VigenciaInicio = "01/01/2027" }, ctx)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() =>
            Petics.CriarAsync(new PePeticCriarDTO { Titulo = "PETIC", VigenciaInicio = "2027-01-01", VigenciaFim = "2026-12-31" }, ctx)));

        // Vigência vazia vale no rascunho (é exigida só no envio)
        var semData = await Petics.CriarAsync(new PePeticCriarDTO { Titulo = "PETIC", VigenciaInicio = "" }, ctx);
        Assert.Null(semData.VigenciaInicio);
    }

    [Fact]
    public async Task Atualizar_SoORascunho_CampoAusenteNaoMuda_NuloLimpaAData()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();

        var titulo = await Petics.AtualizarAsync(rascunho.Id,
            new PePeticAtualizarDTO { Titulo = "PETIC-DF 2027-2031", Informados = new HashSet<string> { "Titulo" } }, ctx);
        Assert.Equal("PETIC-DF 2027-2031", titulo.Titulo);
        Assert.Equal(rascunho.VigenciaFim, titulo.VigenciaFim);

        var semFim = await Petics.AtualizarAsync(rascunho.Id,
            new PePeticAtualizarDTO { VigenciaFim = null, Informados = new HashSet<string> { "VigenciaFim" } }, ctx);
        Assert.Null(semFim.VigenciaFim);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Petics.AtualizarAsync(rascunho.Id,
            new PePeticAtualizarDTO { Titulo = "", Informados = new HashSet<string> { "Titulo" } }, ctx)));
    }

    [Fact]
    public async Task Enviar_ComPendencias_400_DizendoOQueFalta()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();

        // Desde a F1 (A06), o que falta vem em lista, com a seção de cada pendência
        var ex = await Assert.ThrowsAsync<PePeticPendenciasException>(() => Petics.EnviarAsync(rascunho.Id, ctx));
        Assert.Equal((int)ErrorCode.PePeticIncompleto, ex.Error.Code);
        var titulos = ex.Pendencias.Select(p => p.SecaoTitulo).ToList();
        Assert.Contains("Diretrizes", titulos);
        Assert.Contains("Objetivos estratégicos", titulos);
        Assert.Contains("Iniciativas", titulos);
        Assert.Contains(ex.Pendencias, p => p.SecaoChave == "petic_diretriz" && p.Motivo == "Inclua pelo menos um item em \"Diretrizes\".");
        // As seções opcionais não entram
        Assert.DoesNotContain(ex.Pendencias, p => p.SecaoTitulo.Contains("Missão") || p.SecaoTitulo.Contains("Eixos"));

        // Registro que ficou sem um obrigatório (o administrador tornou a fonte obrigatória)
        await PreencherMinimoAsync(rascunho.Id);
        await Modelo.DefinirSituacaoCampoAsync(Campo("petic_indicador", "fonte").Id, new PeSituacoesDTO { SituacaoGeral = "obrigatorio" }, EmailAdmin);
        ex = await Assert.ThrowsAsync<PePeticPendenciasException>(() => Petics.EnviarAsync(rascunho.Id, ctx));
        var pendencia = Assert.Single(ex.Pendencias);
        Assert.Equal(("petic_indicador", "IE01: preencha \"Fonte dos dados\"."), (pendencia.SecaoChave, pendencia.Motivo));
        Assert.Equal("Falta uma coisa para enviar o PETIC-DF ao CGTIC. Confira a lista.", ex.Error.Message);

        // Sem vigência: a pendência sem seção (o título e a vigência da versão)
        await Modelo.DefinirSituacaoCampoAsync(Campo("petic_indicador", "fonte").Id, new PeSituacoesDTO { SituacaoGeral = "opcional" }, EmailAdmin);
        await Petics.AtualizarAsync(rascunho.Id, new PePeticAtualizarDTO { VigenciaInicio = null, Informados = new HashSet<string> { "VigenciaInicio" } }, ctx);
        ex = await Assert.ThrowsAsync<PePeticPendenciasException>(() => Petics.EnviarAsync(rascunho.Id, ctx));
        pendencia = Assert.Single(ex.Pendencias);
        Assert.Equal((null, "Título e vigência", "Informe o início e o fim da vigência."), (pendencia.SecaoChave, pendencia.SecaoTitulo, pendencia.Motivo));
        Assert.Equal("rascunho", Context.PePetics.AsNoTracking().Single().Situacao);
        Assert.Empty(Context.PeDeliberacoes);
    }

    [Fact]
    public async Task Fluxo_EnviarEAprovar_AVersaoFicaFechada_EViraAVigente()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        var objetivo = await PreencherMinimoAsync(rascunho.Id);
        var dono = PeDono.DoPetic(rascunho.Id);

        var enviado = await Petics.EnviarAsync(rascunho.Id, ctx);
        Assert.Equal("em_deliberacao", enviado.Situacao);
        var deliberacao = enviado.Deliberacao!;
        Assert.Equal("aguardando", deliberacao.Situacao);
        Assert.Equal("PETIC-DF 1.0", deliberacao.Titulo);
        Assert.Equal("petic", deliberacao.ObjetoTipo);
        Assert.Equal(rascunho.Id, deliberacao.ObjetoId);
        Assert.Equal("1.0", deliberacao.VersaoObjeto);
        Assert.Equal(UserPeAdmin.Email, deliberacao.EnviadoPor);
        Assert.Null(deliberacao.OrgaoSigla);

        // Com o CGTIC, nada muda
        Assert.Equal(Codigo(ErrorCode.PeVersaoFechada), await ErroAsync(() => IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Outra" })));
        Assert.Equal(Codigo(ErrorCode.PeVersaoFechada), await ErroAsync(() => Registros.AtualizarAsync(dono, "petic_objetivo", objetivo.Id,
            Salvar(new { texto = "Mudado" }), ctx)));
        Assert.Equal(Codigo(ErrorCode.PeVersaoFechada), await ErroAsync(() => Petics.EnviarAsync(rascunho.Id, ctx)));
        Assert.Equal(Codigo(ErrorCode.PeVersaoFechada), await ErroAsync(() => Petics.ExcluirAsync(rascunho.Id, ctx)));
        Assert.Equal(Codigo(ErrorCode.PeVersaoEmAndamento), await ErroAsync(() => RascunhoAsync("Outra")));
        Assert.False((await Registros.ListarAsync(dono, "petic_objetivo", ctx)).PodeEditar);
        Assert.Null(await Petics.VigenteAsync());

        var decisao = await Deliberacoes.DecidirAsync(deliberacao.Id, new PeDecidirDTO
        {
            Decisao = "aprovado",
            AtoTipo = "Resolução",
            AtoNumero = "3/2026",
            AtoData = "2026-09-10",
            Sei = "00040-00012345/2026-11"
        }, await Cgtic());
        Assert.Equal("aprovado", decisao.Situacao);
        Assert.Equal(UserPeCgtic.Email, decisao.DecididoPor);
        Assert.NotNull(decisao.DecididoEm);
        Assert.Equal(new DateOnly(2026, 9, 10), decisao.AtoData);
        Assert.Equal("3/2026", decisao.AtoNumero);

        var vigente = await Petics.VigenteAsync();
        Assert.Equal(rascunho.Id, vigente!.Id);
        Assert.Equal("aprovado", vigente.Situacao);
        Assert.NotNull(vigente.AprovadoEm);
        Assert.Equal("aprovado", vigente.Deliberacao!.Situacao);
        Assert.Equal(Codigo(ErrorCode.PeVersaoFechada), await ErroAsync(() => IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Outra" })));
        Assert.Equal(objetivo.Id, Assert.Single(await Registros.CatalogoAsync("petic_objetivo", ctx)).Id);
    }

    [Fact]
    public async Task NovaVersao_CopiaRegistrosCodigosLigacoesESequencia_EAprovarSubstituiAAnterior()
    {
        var ctx = await Admin();
        var v1 = await RascunhoAsync("PETIC-DF 2026-2029");
        var oe1 = await PreencherMinimoAsync(v1.Id);
        var oe2 = await IncluirAsync(v1.Id, "petic_objetivo", new { texto = "Segundo objetivo" });
        await Registros.ExcluirAsync(PeDono.DoPetic(v1.Id), "petic_objetivo", oe2.Id, ctx);
        await AprovarAsync(v1.Id);

        var v2 = await RascunhoAsync("PETIC-DF 2027-2030");
        Assert.Equal("2.0", v2.Versao);
        Assert.Equal(v1.Id, v2.AnteriorId);

        var copia = Assert.Single((await Registros.ListarAsync(PeDono.DoPetic(v2.Id), "petic_objetivo", ctx)).Registros);
        Assert.Equal("OE01", copia.Codigo);
        Assert.NotEqual(oe1.Id, copia.Id);
        Assert.Equal("Ampliar os serviços digitais.", Valor(copia, "texto"));
        var indicador = Assert.Single((await Registros.ListarAsync(PeDono.DoPetic(v2.Id), "petic_indicador", ctx)).Registros);
        Assert.Equal("IE01", indicador.Codigo);
        Assert.Equal(copia.Id, indicador.Vinculos["objetivo"].Single().RegistroId);

        // A sequência veio junto: OE02 foi apagado na versão 1 e não volta
        Assert.Equal("OE03", (await IncluirAsync(v2.Id, "petic_objetivo", new { texto = "Terceiro" })).Codigo);
        Assert.Single((await Registros.ListarAsync(PeDono.DoPetic(v1.Id), "petic_objetivo", ctx)).Registros);

        await AprovarAsync(v2.Id);
        var lista = await Petics.ListarAsync(ctx);
        Assert.Equal(new[] { v2.Id, v1.Id }, lista.Select(p => p.Id));
        Assert.Equal(new[] { "aprovado", "substituido" }, lista.Select(p => p.Situacao));
        Assert.NotNull(lista[1].AprovadoEm);
        Assert.Single(Context.PePetics.Where(p => p.Situacao == "aprovado"));
        Assert.Equal(v2.Id, (await Petics.VigenteAsync())!.Id);
        Assert.Equal(2, (await Registros.CatalogoAsync("petic_objetivo", ctx)).Count);
    }

    [Fact]
    public async Task NovaVersao_SemCopiar_NasceVazia_ComCodigosDoComeco()
    {
        var ctx = await Admin();
        var (vigente, _) = await VigenteAsync();

        var vazia = await RascunhoAsync(copiar: false);

        Assert.Equal(vigente.Id, vazia.AnteriorId);
        Assert.Empty((await Registros.ListarAsync(PeDono.DoPetic(vazia.Id), "petic_objetivo", ctx)).Registros);
        Assert.Equal("OE01", (await IncluirAsync(vazia.Id, "petic_objetivo", new { texto = "Novo" })).Codigo);
    }

    [Fact]
    public async Task Devolver_ExigeObservacao_VoltaARascunho_EReenviaComNovaDeliberacao()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);
        var primeira = (await Petics.EnviarAsync(rascunho.Id, ctx)).Deliberacao!;

        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(primeira.Id, new PeDecidirDTO { Decisao = "devolvido" }, await Cgtic())));
        var devolvida = await Deliberacoes.DecidirAsync(primeira.Id,
            new PeDecidirDTO { Decisao = "devolvido", Observacao = "Ajuste os indicadores." }, await Cgtic());
        Assert.Equal("devolvido", devolvida.Situacao);

        var versao = await Petics.ObterAsync(rascunho.Id, ctx);
        Assert.Equal("rascunho", versao.Situacao);
        Assert.Equal("devolvido", versao.Deliberacao!.Situacao);
        Assert.Equal("Ajuste os indicadores.", versao.Deliberacao.Observacao);
        Assert.Null(versao.AprovadoEm);

        // Editável de novo, mas já foi ao CGTIC: não se apaga
        await IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Mais uma" });
        Assert.Equal(Codigo(ErrorCode.PeVersaoJaEnviada), await ErroAsync(() => Petics.ExcluirAsync(rascunho.Id, ctx)));

        var reenviado = await Petics.EnviarAsync(rascunho.Id, ctx);
        Assert.Equal("aguardando", reenviado.Deliberacao!.Situacao);
        Assert.NotEqual(primeira.Id, reenviado.Deliberacao.Id);
        Assert.Equal(2, Context.PeDeliberacoes.Count());
        Assert.Equal(reenviado.Deliberacao.Id, (await Petics.ListarAsync(ctx)).Single().Deliberacao!.Id);

        // Decidida não muda
        Assert.Equal(Codigo(ErrorCode.PeDeliberacaoJaDecidida), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(primeira.Id, new PeDecidirDTO { Decisao = "devolvido", Observacao = "De novo" }, await Cgtic())));
    }

    [Fact]
    public async Task Excluir_RascunhoNuncaEnviado_LevaRegistrosLigacoesESequencias()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);

        await Petics.ExcluirAsync(rascunho.Id, ctx);

        Assert.Empty(Context.PePetics);
        Assert.Empty(Context.PeRegistros.Where(r => r.PeticId != null));
        Assert.Empty(Context.PeVinculos);
        Assert.Empty(Context.PeRegistroSequencias.Where(s => s.Dono == $"petic:{rascunho.Id}"));
        Assert.Equal("1.0", (await RascunhoAsync()).Versao);
        Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado), await ErroAsync(() => Petics.ExcluirAsync(rascunho.Id, ctx)));
    }

    [Fact]
    public async Task PapelDeOrgao_SoVeAsVersoesAprovadas()
    {
        var (vigente, _) = await VigenteAsync();
        var rascunho = await RascunhoAsync();
        await IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Em rascunho" });

        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes })
        {
            var ctx = await ContextoDe(user);
            Assert.Equal(new[] { vigente.Id }, (await Petics.ListarAsync(ctx)).Select(p => p.Id));
            Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado), await ErroAsync(() => Petics.ObterAsync(rascunho.Id, ctx)));
            Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado),
                await ErroAsync(() => Registros.ListarAsync(PeDono.DoPetic(rascunho.Id), "petic_diretriz", ctx)));
            Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado),
                await ErroAsync(() => Planilhas.PeticCompletaAsync(rascunho.Id, "xlsx", ctx)));
            Assert.Single((await Registros.ListarAsync(PeDono.DoPetic(vigente.Id), "petic_diretriz", ctx)).Registros);
            Assert.Equal(vigente.Id, (await Petics.ObterAsync(vigente.Id, ctx)).Id);
        }

        // Os papéis globais veem tudo
        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserAdminGeral })
            Assert.Equal(2, (await Petics.ListarAsync(await ContextoDe(user))).Count);
    }
}
