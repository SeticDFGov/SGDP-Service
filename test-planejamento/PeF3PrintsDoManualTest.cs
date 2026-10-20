using System.Text.Json;
using System.Text.Json.Nodes;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, rodada dos prints do manual: os defeitos que ela achou no back-end.
/// <list type="number">
/// <item>A lista dos PDTICs (GET pdtic) traz, em cada item, o nível que aquela versão alcançou,
/// pela mesma régua, nos dois modos (nulo sem nível e no PDTIC registrado fora do sistema).</item>
/// <item>O andamento por etapa da página do órgão conta nos Feitos e no Total só os passos
/// obrigatórios, como a visão geral, e traz os opcionais feitos à parte, nos dois modos.</item>
/// <item>O que falta nas linhas sem código (o cronograma da elaboração) vira uma frase só, com
/// quantas linhas, no envio ao CGTIC e no nível alcançado (o mesmo construtor).</item>
/// <item>As ajudas e o "o que fazer" do modelo inicial falam das formas ("Na forma do nível
/// Básico, o mínimo do decreto, bastam..."), que valem nos dois modos.</item>
/// <item>O comentário aberto da SGDI não tira o passo da régua: o nível mede o conteúdo, e a
/// situação do passo continua "atencao".</item>
/// </list>
/// </summary>
public class PeF3PrintsDoManualTest : PePaineisTestBase
{
    private const string FaltaDoCronograma = "Cronograma: preencha \"Início\" e \"Término\"";

    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    private async Task<List<PePdticListaItemResponse>> ListaAsync() =>
        (await Pdtics.ListarAsync(new PePdticConsulta { PageSize = 100 }, await Sgdi())).Items;

    private async Task<PeOrgaoResumoResponse> ResumoAsync(PgiaOrgao orgao) => await Paineis.ResumoAsync(orgao.Id, await Sgdi());

    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(d => d.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    /// <summary>O PDTIC do órgão registrado fora do sistema pelo admin geral (aprovado pelo CGTIC).</summary>
    private async Task<PePdticResponse> RegistrarForaAsync(PgiaOrgao orgao)
    {
        var arquivo = await EnviarArquivoAsync(UserAdminGeral, "pdtic.pdf", PdfDeUmaPagina());
        return await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            OrgaoId = orgao.Id,
            Versao = "1.0",
            VigenciaInicio = "2026-01-01",
            VigenciaFim = "2029-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic",
            AprovacaoData = "2026-02-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "3/2026",
            PublicacaoData = "2026-02-20",
            PublicacaoEndereco = Endereco
        }, await AdminGeral());
    }

    // ── 1. A lista dos PDTICs com o nível alcançado ─────────────────────────────

    [Theory]
    [InlineData(PeDominios.ModoNiveis.Definido)]
    [InlineData(PeDominios.ModoNiveis.Livre)]
    public async Task Lista_CadaItemComONivelQueAVersaoAlcancou_NulosSemNivelEForaDoSistema(string modo)
    {
        DefinirModoNiveis(modo);
        var ses = await ProntoParaEnviarAsync();
        var seec = await AbrirSeecAsync();
        var externo = await RegistrarForaAsync(NovoOrgao("SEDES"));

        var lista = await ListaAsync();
        Assert.Equal(3, lista.Count);

        var itemSes = lista.Single(i => i.Id == ses.Id);
        Assert.Equal((NivelId("basico"), "Básico"), (itemSes.NivelAlcancadoId!.Value, itemSes.NivelAlcancadoNome));
        var itemSeec = lista.Single(i => i.Id == seec.Id);
        Assert.Equal(((long?)null, (string?)null), (itemSeec.NivelAlcancadoId, itemSeec.NivelAlcancadoNome));
        var itemExterno = lista.Single(i => i.Id == externo.Id);
        Assert.True(itemExterno.RegistradoExternamente);
        Assert.Equal(((long?)null, (string?)null), (itemExterno.NivelAlcancadoId, itemExterno.NivelAlcancadoNome));

        // O mesmo nível que a situação dos passos calcula para cada versão
        foreach (var item in lista.Where(i => !i.RegistradoExternamente))
        {
            var nivel = (await SituacaoAsync(item.Id, UserAdminGeral)).Nivel;
            Assert.Equal((nivel.AlcancadoId, nivel.AlcancadoNome), (item.NivelAlcancadoId, item.NivelAlcancadoNome));
        }

        // No modo livre, o nível de hoje do órgão é o nível base para todos (era o que a coluna mostrava):
        // o alcançado é o que distingue os PDTICs
        if (modo == PeDominios.ModoNiveis.Livre)
            Assert.All(lista, i => Assert.Equal("Básico", i.NivelNome));
    }

    [Fact]
    public async Task Lista_OsCamposNovosSoNaLista_ComFiltroDeSituacao()
    {
        Livre();
        var ses = await ProntoParaEnviarAsync();

        // Pela rota: os dois campos em cada item, em PascalCase
        var (status, corpo) = Resultado(await ControladorPdtic(UserPeCgtic).Listar(new PePdticConsulta()));
        Assert.Equal(StatusCodes.Status200OK, status);
        var item = JsonSerializer.SerializeToElement(corpo).GetProperty("Items")[0];
        Assert.Equal(NivelId("basico"), item.GetProperty("NivelAlcancadoId").GetInt64());
        Assert.Equal("Básico", item.GetProperty("NivelAlcancadoNome").GetString());

        // As respostas de um PDTIC continuam sem eles (só a lista calcula)
        var um = JsonSerializer.SerializeToElement(Resultado(await ControladorPdtic(UserPeSgdi).Obter(ses.Id)).Valor);
        Assert.False(um.TryGetProperty("NivelAlcancadoId", out _));

        // Com o filtro de situação, o nível da versão daquela situação
        var emElaboracao = (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "em_elaboracao" }, await Sgdi())).Items;
        Assert.Equal("Básico", Assert.Single(emElaboracao).NivelAlcancadoNome);
        Assert.Empty((await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "encerrado" }, await Sgdi())).Items);
    }

    // ── 2. O andamento por etapa da página do órgão ──────────────────────────────

    [Fact]
    public async Task Andamento_Definido_SoOsObrigatoriosNoTotal_EOsOpcionaisFeitosAParte()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        var pdtic = await ProntoParaEnviarAsync();
        // As estratégias opcionais para a SES (ajuste), com os objetivos do PETIC-DF preenchidos: um opcional
        // feito (desde a versão 9, a etapa 1 tem 5 passos no Básico)
        await AjustarPassosAsync(OrgaoSes, ("preparacao.estrategias", "opcional"));

        var etapa1 = (await ResumoAsync(OrgaoSes)).Andamento[0];
        Assert.Equal(("Prepare o PDTIC", 4, 4, 1), (etapa1.Titulo, etapa1.Feitos, etapa1.Total, etapa1.OpcionaisFeitos));

        // Sem o registro, o passo opcional fica sem conteúdo: os obrigatórios continuam todos resolvidos
        // (antes a etapa dizia "4 de 5")
        var alinhamento = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "alinhamento_petic", await Orgao())).Registros.Single();
        await Registros.ExcluirAsync(PeDono.DoPdtic(pdtic.Id), "alinhamento_petic", alinhamento.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.Opcional, (await PassoAsync(pdtic.Id, "preparacao.estrategias")).Situacao);
        etapa1 = (await ResumoAsync(OrgaoSes)).Andamento[0];
        Assert.Equal((4, 4, 0), (etapa1.Feitos, etapa1.Total, etapa1.OpcionaisFeitos));
    }

    [Fact]
    public async Task Andamento_Livre_AEtapa1ComOsObrigatoriosResolvidos_EOsOpcionaisAParte()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();

        // No modo livre, desde a versão 9, a etapa 1 tem 5 passos, todos obrigatórios (os do Básico): os
        // documentos de referência, o plano de trabalho e a aprovação dele viraram seções de passos dela
        var trilha = await TrilhaAsync(OrgaoSes);
        var situacao = await SituacaoAsync(pdtic.Id);
        var daEtapa1 = trilha.Etapas[0].Passos.Select(p => situacao.Passos.Single(s => s.PassoId == p.Id)).ToList();
        Assert.Equal((5, 5), (daEtapa1.Count, daEtapa1.Count(p => p.Obrigatorio)));
        var etapa1 = (await ResumoAsync(OrgaoSes)).Andamento[0];
        Assert.Equal((5, 5, 0), (etapa1.Feitos, etapa1.Total, etapa1.OpcionaisFeitos));

        // Um passo opcional (o PDTIC anterior, da etapa 2, que o Básico não pede) preenchido conta nos opcionais feitos
        var antesDoOpcional = (await ResumoAsync(OrgaoSes)).Andamento[1];
        await IncluirNoPdticAsync(pdtic.Id, "resultados_pdtic_anterior", new { resultados = "As metas de 2025 foram cumpridas.", teve_anterior = true });
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "diagnostico.pdtic-anterior")).Situacao);
        var etapa2 = (await ResumoAsync(OrgaoSes)).Andamento[1];
        Assert.Equal((antesDoOpcional.Feitos, antesDoOpcional.Total, antesDoOpcional.OpcionaisFeitos + 1),
            (etapa2.Feitos, etapa2.Total, etapa2.OpcionaisFeitos));

        // Um obrigatório pendente sai dos Feitos (o Total continua o dos obrigatórios)
        var principios = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "principios_diretrizes", await Orgao())).Registros;
        foreach (var principio in principios)
            await Registros.ExcluirAsync(PeDono.DoPdtic(pdtic.Id), "principios_diretrizes", principio.Id, await Orgao());
        etapa1 = (await ResumoAsync(OrgaoSes)).Andamento[0];
        Assert.Equal((4, 5, 0), (etapa1.Feitos, etapa1.Total, etapa1.OpcionaisFeitos));
    }

    // ── 3. O que falta nas linhas sem código ─────────────────────────────────────

    /// <summary>O PDTIC da SES no Avançado com o cronograma sugerido pelos fluxos (32 linhas, sem as datas) e o plano de trabalho preenchido.</summary>
    private async Task<(long Id, List<PeRegistroResponse> Linhas)> CronogramaSemDatasAsync()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var id = (await AbrirSesAsync()).Id;
        // Desde a versão 9, o plano de trabalho e o cronograma são seções do passo da metodologia
        await IncluirNoPdticAsync(id, "metodologia", new { metodologia_adotada = "guia_sisp", descricao = "Oficinas com as áreas." });
        await IncluirNoPdticAsync(id, "plano_trabalho", new
        {
            objetivo = "Elaborar o PDTIC 2027 a 2030.",
            justificativa = "O PDTIC anterior terminou.",
            premissas_restricoes = "A equipe tem dedicação parcial."
        });
        var linhas = await new PeFluxoService(Context, Registros, Permissoes).SugerirCronogramaAsync(id, await Orgao());
        Assert.Equal(32, linhas.Count);
        return (id, linhas);
    }

    /// <summary>Grava as datas de uma linha direto no banco (as linhas sugeridas nascem sem elas).</summary>
    private void DatasNoBanco(long registroId, string? inicio, string? termino)
    {
        var registro = Context.PeRegistros.Single(r => r.Id == registroId);
        var dados = JsonNode.Parse(registro.Dados)!.AsObject();
        if (inicio != null) dados["inicio"] = inicio;
        if (termino != null) dados["termino"] = termino;
        registro.Dados = dados.ToJsonString();
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private async Task<string> MotivoNoEnvioAsync(long pdticId, string passo) =>
        (await Aprovacao.EnvioAsync(pdticId, await Orgao())).Pendencias.Single(p => p.PassoId == Passo(passo).Id).Motivo;

    [Fact]
    public async Task LinhasSemCodigo_UmaFraseComQuantasLinhas_NoEnvio()
    {
        var (id, linhas) = await CronogramaSemDatasAsync();

        // As 32 linhas sem as datas: uma frase só (antes, a mesma frase a cada linha e "E mais 30 pendências")
        Assert.Equal($"{FaltaDoCronograma} em 32 linhas.", await MotivoNoEnvioAsync(id, "preparacao.metodologia"));

        // Campos diferentes não se juntam; a linha sozinha continua sem a contagem
        foreach (var linha in linhas.Skip(3)) DatasNoBanco(linha.Id, "2026-02-01", "2026-02-28");
        DatasNoBanco(linhas[1].Id, "2026-02-01", null);
        DatasNoBanco(linhas[2].Id, "2026-02-01", null);
        Assert.Equal($"{FaltaDoCronograma}. Cronograma: preencha \"Término\" em 2 linhas.",
            await MotivoNoEnvioAsync(id, "preparacao.metodologia"));

        DatasNoBanco(linhas[1].Id, null, "2026-02-28");
        DatasNoBanco(linhas[2].Id, null, "2026-02-28");
        Assert.Equal($"{FaltaDoCronograma}.", await MotivoNoEnvioAsync(id, "preparacao.metodologia"));

        // Com o cronograma completo, falta a aprovação do plano de trabalho (a última seção do passo, desde a versão 9)
        DatasNoBanco(linhas[0].Id, "2026-02-01", "2026-02-28");
        Assert.Equal("Registre a decisão e a data.", await MotivoNoEnvioAsync(id, "preparacao.metodologia"));
        Assert.Contains(await Pendencias(id), p => p.PassoId == Passo("preparacao.metodologia").Id);
    }

    private async Task<List<PePendenciaResponse>> Pendencias(long pdticId) => (await Aprovacao.EnvioAsync(pdticId, await Orgao())).Pendencias;

    [Fact]
    public async Task LinhasSemCodigo_AMesmaFraseNoNivelAlcancado()
    {
        // O plano de trabalho e o cronograma obrigatórios também no Básico: desde a versão 9 eles são seções
        // do passo da metodologia, e a forma do Básico desse passo passa a pedi-los
        // (nos três níveis: a forma de um nível acima também atende ao Básico)
        await Modelo.DefinirSituacaoSecaoAsync(Secao("plano_trabalho").Id, Todos("obrigatorio"), EmailAdmin);
        await Modelo.DefinirSituacaoSecaoAsync(Secao("cronograma_elaboracao").Id, Todos("obrigatorio"), EmailAdmin);
        var (id, _) = await CronogramaSemDatasAsync();

        var nivel = (await SituacaoAsync(id)).Nivel;
        Assert.Equal("Básico", nivel.ProximoNome);
        var falta = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("preparacao.metodologia").Id);
        Assert.Equal($"{FaltaDoCronograma} em 32 linhas.", falta.Motivo);
        Assert.Equal(falta.Motivo, await MotivoNoEnvioAsync(id, "preparacao.metodologia"));
    }

    // ── 4. Os textos do modelo inicial na linguagem das formas ───────────────────

    [Fact]
    public void Seed_AsAjudasEOOQueFazer_NaLinguagemDasFormas_QueValeNosDoisModos()
    {
        var textos = Context.PePassos.AsNoTracking().Select(p => p.OQueFazer).ToList()
            .Concat(Context.PeSecoes.AsNoTracking().Select(s => s.Ajuda).ToList())
            .Concat(Context.PeCampos.AsNoTracking().Select(c => c.Ajuda).ToList())
            .Where(t => t != null)
            .ToList();
        Assert.DoesNotContain(textos, t => t!.Contains("No nível") || t.Contains("nos níveis") || t.Contains("nível do órgão"));

        // A revisão de 2026-10-20 tirou das ajudas e do "o que fazer" a frase sobre a forma de cada nível (o que cada
        // nível pede continua nas situações dos campos): nenhum texto cita o nível pelo nome, e a ajuda do objetivo do
        // PETIC-DF fala da vigência do PETIC-DF, não do nível
        Assert.DoesNotContain(textos, t => t!.Contains("Básico") || t.Contains("Intermediário") || t.Contains("Avançado"));
        Assert.Null(Secao("ativos").Ajuda);
        Assert.Null(Campo("necessidades", "prioridade_simples").Ajuda);
        Assert.Equal("Preencha quando há PETIC-DF vigente.", Campo("necessidades", "objetivo_petic").Ajuda);
        Assert.Equal("", Passo("monitoramento.ciclo-monitoramento").OQueFazer);
    }

    // ── 5. O comentário aberto não tira o passo da régua ─────────────────────────

    [Fact]
    public async Task ComentarioAberto_ONivelContinua_EmTodaParte_EOPassoFicaEmAtencao()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        var passo = Passo("preparacao.abrangencia");
        var comentario = await Comentarios.CriarAsync(pdtic.Id,
            new PeComentarioCriarDTO { PassoId = passo.Id, Texto = "Confira a vigência." }, await Sgdi());

        // A situação do passo e o próximo passo não mudam: atenção
        var situacao = await SituacaoAsync(pdtic.Id);
        var abrangencia = situacao.Passos.Single(p => p.PassoId == passo.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Atencao, abrangencia.Situacao);
        Assert.Equal(abrangencia.Numero, situacao.ProximoPasso);

        // O nível mede o conteúdo: Básico na situação, na conformidade, no painel, na lista e na página do órgão
        Assert.Equal("Básico", situacao.Nivel.AlcancadoNome);
        Assert.DoesNotContain(situacao.Nivel.Faltam, f => f.Motivo == PePdticService.FaltaComentarioAberto);
        Assert.Equal("Básico", (await LinhaAsync(OrgaoSes)).NivelAlcancadoNome);
        Assert.Equal(1, (await PainelAsync()).PorNivel.Single(n => n.NivelId == NivelId("basico")).Quantidade);
        Assert.Equal("Básico", (await ListaAsync()).Single(i => i.Id == pdtic.Id).NivelAlcancadoNome);
        var resumo = await ResumoAsync(OrgaoSes);
        Assert.Equal("Básico", resumo.Nivel.AlcancadoNome);
        Assert.Contains(resumo.ComentariosAbertos, c => c.PassoId == passo.Id);

        // O passo com o comentário e sem o conteúdo: falta pelo conteúdo, não pelo comentário
        var registro = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "abrangencia", await Orgao())).Registros.Single();
        await Registros.ExcluirAsync(PeDono.DoPdtic(pdtic.Id), "abrangencia", registro.Id, await Orgao());
        var nivel = (await SituacaoAsync(pdtic.Id)).Nivel;
        Assert.Null(nivel.AlcancadoId);
        Assert.Equal("Preencha \"Abrangência e vigência\".", Assert.Single(nivel.Faltam, f => f.PassoId == passo.Id).Motivo);

        await Comentarios.ResolverAsync(comentario.Id, await Orgao());
        Assert.Null((await SituacaoAsync(pdtic.Id)).Nivel.AlcancadoId);
    }
}
