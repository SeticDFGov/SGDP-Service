using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, parte C4: o passo opcional não cobra, nos dois modos dos níveis. O passo opcional para o
/// órgão e ainda sem conteúdo (nenhum registro em nenhuma seção dele; no documento, nenhum PDF
/// gerado) fica "opcional" onde antes ficaria pendente ou atrasado; com conteúdo, segue a regra de
/// sempre. O próximo passo só leva ao opcional em atenção. Nada obrigatório depende do opcional:
/// o fechamento do ciclo só pede o resumo com o 5.2 obrigatório, a revisão aceita a justificativa
/// (ou a decisão "revisar") com o 6.3 opcional, e o documento, os relatórios e a planilha completa
/// deixam de fora o passo opcional sem conteúdo.
/// </summary>
public class PeF3OpcionalTest : PePaineisTestBase
{
    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    [Fact]
    public async Task Opcional_SemConteudo_NaoCobra_ComConteudoSegueARegraDeSempre()
    {
        Livre();
        var pdtic = await AbrirSesAsync();

        var swot = await PassoAsync(pdtic.Id, "diagnostico.swot");
        Assert.Equal((PeDominios.SituacaoPasso.Opcional, false, (string?)null), (swot.Situacao, swot.Obrigatorio, swot.Motivo));
        Assert.True((await PassoAsync(pdtic.Id, "diagnostico.ativos")).Obrigatorio);

        // Com conteúdo (uma força, faltam as outras três seções): pendente, mas fora do próximo passo
        await IncluirNoPdticAsync(pdtic.Id, "swot_forcas", new { descricao = "Equipe técnica experiente." });
        var situacao = await SituacaoAsync(pdtic.Id);
        swot = situacao.Passos.Single(p => p.Chave == "diagnostico.swot");
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, swot.Situacao);
        Assert.NotEqual(swot.Numero, situacao.ProximoPasso);
        Assert.Equal(situacao.Passos.First(p => p.Situacao == PeDominios.SituacaoPasso.Pendente && p.Obrigatorio).Numero, situacao.ProximoPasso);

        // Completo: feito
        await IncluirNoPdticAsync(pdtic.Id, "swot_fraquezas", new { descricao = "Sistemas legados." });
        await IncluirNoPdticAsync(pdtic.Id, "swot_oportunidades", new { descricao = "Soluções corporativas." });
        await IncluirNoPdticAsync(pdtic.Id, "swot_ameacas", new { descricao = "Ransomware." });
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "diagnostico.swot")).Situacao);
    }

    [Fact]
    public async Task Opcional_EmAtencao_EOProximoPasso()
    {
        Livre();
        var pdtic = await AbrirSesAsync();
        var swot = Passo("diagnostico.swot").Id;
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = swot, Texto = "Vale fazer a SWOT." }, await Sgdi());

        var situacao = await SituacaoAsync(pdtic.Id);
        var passo = situacao.Passos.Single(p => p.PassoId == swot);
        Assert.Equal(PeDominios.SituacaoPasso.Atencao, passo.Situacao);
        Assert.Equal(passo.Numero, situacao.ProximoPasso);
    }

    [Fact]
    public async Task Opcional_NoDocumento_SemPdfGerado_ENoModoDefinidoTambem()
    {
        // Modo definido: o passo do documento opcional para o órgão (ajuste)
        await AjustarPassosAsync(OrgaoSes, ("planejamento.documento", "opcional"));
        var pdtic = await AbrirSesAsync();
        Assert.Equal(PeDominios.SituacaoPasso.Opcional, (await PassoAsync(pdtic.Id, "planejamento.documento")).Situacao);

        await Documentos.GerarPdfAsync(pdtic.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "planejamento.documento")).Situacao);
    }

    [Fact]
    public async Task Envio_SoOsObrigatorios_OsOpcionaisDoGuiaNaoEntram()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        var envio = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        Assert.True(envio.PodeEnviar);
        Assert.Empty(envio.Pendencias);
        var situacao = await SituacaoAsync(pdtic.Id);
        var opcionais = situacao.Passos.Where(p => !p.Obrigatorio && p.Chave.StartsWith("diagnostico.")).ToList();
        Assert.NotEmpty(opcionais);
        Assert.All(opcionais, p => Assert.Contains(p.Situacao, new[] { PeDominios.SituacaoPasso.Opcional, PeDominios.SituacaoPasso.Feito }));
        Assert.Equal(PeDominios.SituacaoPasso.Opcional, opcionais.Single(p => p.Chave == "diagnostico.swot").Situacao);
        // O passo sem seção obrigatória (a consolidação do inventário) já ficava feito sem conteúdo: "feito" não muda
        Assert.Equal(PeDominios.SituacaoPasso.Feito, opcionais.Single(p => p.Chave == "diagnostico.inventario").Situacao);
    }

    [Fact]
    public async Task Monitoramento_O52Opcional_NaoCobraOResumo_NemAtrasa()
    {
        Livre();
        var id = await AcompanhadoNoBasicoAsync();
        var trimestre = await CicloAsync(id, Trimestre1);

        // O 5.1 atrasado (o 1º trimestre passou do prazo); o 5.2, opcional e sem conteúdo, não
        var situacao = await SituacaoAsync(id);
        Assert.Equal(PeDominios.SituacaoPasso.Atrasado, situacao.Passos.Single(p => p.Chave == "monitoramento.ciclo-monitoramento").Situacao);
        var resumo = situacao.Passos.Single(p => p.Chave == PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento);
        Assert.Equal((PeDominios.SituacaoPasso.Opcional, false), (resumo.Situacao, resumo.Obrigatorio));

        // Fechar sem o resumo do ciclo: só a situação das ações
        var grade = await Acompanhamento.AcoesAsync(trimestre.Id, await Orgao());
        await GravarAcoesAsync(trimestre.Id, grade.Select(a => Linha(a.AcaoId, "em_andamento")).ToArray());
        var fechado = await FecharAsync(trimestre.Id);
        Assert.Equal(PeDominios.SituacaoCiclo.Fechado, fechado.Situacao);
    }

    [Fact]
    public async Task Monitoramento_O52Obrigatorio_CobraOResumo()
    {
        // O 5.2 obrigatório para o órgão (ajuste): o fechamento pede o resumo, como sempre
        Livre();
        await AjustarPassosAsync(OrgaoSes, (PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento, "obrigatorio"));
        var id = await AcompanhadoNoBasicoAsync();
        var trimestre = await CicloAsync(id, Trimestre1);
        var grade = await Acompanhamento.AcoesAsync(trimestre.Id, await Orgao());
        await GravarAcoesAsync(trimestre.Id, grade.Select(a => Linha(a.AcaoId, "em_andamento")).ToArray());

        var ex = await Assert.ThrowsAsync<PePendenciasException>(() => FecharAsync(trimestre.Id));
        Assert.Contains(ex.Pendencias, p => p.PassoTitulo == Passo(PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento).Titulo);
    }

    [Fact]
    public async Task Revisao_Com63Opcional_AJustificativaOuADecisaoRevisarBastam()
    {
        Livre();
        var id = await AcompanhadoNoBasicoAsync();
        Assert.False((await PassoAsync(id, PeDominios.ChavePdtic.PassoAvaliacaoComite)).Obrigatorio);

        // Sem justificativa e sem decisão: 400
        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria),
            await ErroAsync(async () => await Aprovacao.RevisarAsync(id, new PeRevisaoDTO(), await Orgao())));

        // A decisão "revisar" da avaliação mais recente com decisão também basta
        var avaliacao = await AbrirAvaliacaoAsync(id, "Avaliação de 2026");
        await IncluirNoPdticAsync(id, PeDominios.ChavePdtic.SecaoAvaliacaoComite, new { decisao = "revisar", data = HojeIso() }, cicloId: avaliacao.Id);
        var revisao = await Aprovacao.RevisarAsync(id, new PeRevisaoDTO(), await Orgao());
        Assert.Equal("1.1", revisao.Versao);
    }

    [Fact]
    public async Task Revisao_Com63Opcional_AJustificativaBasta()
    {
        Livre();
        var id = await AcompanhadoNoBasicoAsync();
        var revisao = await Aprovacao.RevisarAsync(id, new PeRevisaoDTO { Justificativa = "Mudou a estrutura da secretaria." }, await Orgao());
        Assert.Equal(("1.1", "Mudou a estrutura da secretaria."), (revisao.Versao, revisao.Revisao!.Justificativa));
    }

    [Fact]
    public async Task Documento_OCapituloDoPassoOpcionalSemConteudoNaoAparece_EANumeracaoSegue()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var definido = await DocumentoAsync(pdtic.Id);

        // No modo livre, só com o conteúdo do Básico, o documento sai igual ao do Básico no definido
        Livre();
        var livre = await DocumentoAsync(pdtic.Id);
        Assert.Equal(Resumo(definido), Resumo(livre));
        Assert.Null(CapOuNulo(livre, "diagnostico_swot"));
        Assert.Null(CapOuNulo(livre, "riscos"));

        // Com conteúdo, o capítulo do passo opcional aparece, com o número pela posição
        await IncluirNoPdticAsync(pdtic.Id, "swot_forcas", new { descricao = "Equipe técnica experiente." });
        var comSwot = await DocumentoAsync(pdtic.Id);
        var capitulo = Cap(comSwot, "diagnostico_swot");
        Assert.NotNull(capitulo.Numero);
        Assert.Single(capitulo.Blocos, b => b.Tipo == PeDominios.TipoBloco.MatrizSwot);
        Assert.Equal(Resumo(livre).Count + 1, Resumo(comSwot).Count);
    }

    [Fact]
    public async Task Documento_OBlocoDeTabelaDoPassoOpcionalSemConteudoNaoAparece()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        // O capítulo de revisão e acompanhamento não tem passo: as tabelas das seções do plano de
        // acompanhamento que são de passos opcionais e sem conteúdo não aparecem
        var documento = await DocumentoAsync(pdtic.Id);
        var capitulo = Cap(documento, "revisao_acompanhamento");
        var secoes = capitulo.Blocos.Where(b => b.Tabela != null).Select(b => b.Tabela!.SecaoChave).ToList();
        Assert.DoesNotContain("projetos", secoes);
        Assert.DoesNotContain("indicadores_avaliacao", secoes);
    }

    [Fact]
    public async Task PlanilhaCompleta_ASecaoDoPassoOpcionalSemConteudoFicaDeFora()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var definido = Abas((await Planilhas.PdticCompletaAsync(pdtic.Id, "xlsx", await Orgao())).Conteudo);

        Livre();
        var livre = Abas((await Planilhas.PdticCompletaAsync(pdtic.Id, "xlsx", await Orgao())).Conteudo);
        Assert.Equal(definido, livre);

        await IncluirNoPdticAsync(pdtic.Id, "swot_forcas", new { descricao = "Equipe técnica experiente." });
        var comSwot = Abas((await Planilhas.PdticCompletaAsync(pdtic.Id, "xlsx", await Orgao())).Conteudo);
        Assert.Equal(livre.Count + 4, comSwot.Count);

        // A planilha da seção de um passo opcional sem conteúdo continua saindo (não muda)
        var secao = await Planilhas.PdticSecaoAsync(pdtic.Id, "riscos", "csv", await Orgao());
        Assert.NotEmpty(secao.Conteudo);
    }

    private static List<string> Resumo(PeDocumentoResponse documento) =>
        documento.Capitulos.Select(c => $"{c.Chave}|{c.Numero}|{c.Blocos.Count}").ToList();

    private static List<string> Abas(byte[] xlsx)
    {
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx), false);
        return documento.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value!).ToList();
    }
}
