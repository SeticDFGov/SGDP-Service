using System.Text.RegularExpressions;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Despacho ao TCDF: literalidade dos textos-base e geração do PDF nos dois
/// incisos e em cada desfecho de risco.
/// </summary>
public class CtrDespachoPdfTest : CtrTestBase
{
    private readonly CtrProcessoResponse _processo = new()
    {
        Id = 1,
        NumeroProcesso = "04044-00002545/2024-62",
        OrgaoNome = "Secretaria de Estado de Economia",
        OrgaoSigla = "SEEC",
        ComplementoArea = "Secretaria Executiva de Contratos",
        Objeto = "Aquisição de switches de acesso",
        CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede
    };

    private static CtrManifestacaoResponse Manifestacao(string situacao, string? resultado = null,
        string? desfecho = null) => new()
        {
            Id = 7,
            ProcessoId = 1,
            NumeroProcesso = "04044-00002545/2024-62",
            OficioTcdf = "123/2026-GAB",
            DataOficio = new DateOnly(2026, 8, 20),
            SituacaoPortfolio = situacao,
            ComunicadaDesde = situacao == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
                ? new DateOnly(2026, 5, 22) : null,
            Criticidade = situacao == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
                ? CtrDominios.Criticidade.Alta : null,
            ResultadoAnalise = resultado,
            DesfechoRisco = desfecho,
            PrazoRegularizacaoDias = situacao == CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente ? 15 : null
        };

    // ── Literalidade dos textos-base ──────────────────────────────────────────

    [Fact]
    public void Textos_ReproduzemODespachoOficial()
    {
        Assert.Equal("SECRETARIA DE ESTADO DE GOVERNANÇA DIGITAL E INTEGRAÇÃO – SGDI", CtrDespachoTextos.Orgao);
        Assert.Equal("DESPACHO", CtrDespachoTextos.Titulo);
        Assert.Equal("Situação no Portfólio Estratégico de Contratações de TIC", CtrDespachoTextos.TituloSituacao);
        Assert.Equal("À consideração superior.", CtrDespachoTextos.Fecho);

        Assert.Equal(
            "Em atenção à comunicação do Tribunal de Contas do Distrito Federal – TCDF acerca da contratação "
            + "de TIC acima identificada, manifesta-se esta SGDI, conforme abaixo, no âmbito do regime de "
            + "supervisão contínua instituído pelo Decreto nº 48.899/2026 e regulamentado pela Instrução "
            + "Normativa SGDI nº 1/2026.",
            CtrDespachoTextos.Abertura);

        Assert.Equal(
            "A supervisão contínua da SGDI compreende a classificação da contratação por criticidade "
            + "(art. 11 da IN), a análise técnica de alinhamento estratégico, arquitetura, interoperabilidade "
            + "e segurança da informação (art. 25) e, quando cabível, a emissão de recomendação técnica ou a "
            + "adoção de medida cautelar (arts. 30 a 34).",
            CtrDespachoTextos.AlcanceSupervisao);

        Assert.Equal(
            "A contratação já se encontrava comunicada a esta SGDI e inserida no monitoramento contínuo desde "
            + "[data]. Foi, por sua vez, classificada como de criticidade [Alta/Média/Baixa] (art. 11 da IN SGDI "
            + "nº 1/2026) e apresentou como resultado da análise e providência adotada:",
            CtrDespachoTextos.IncisoI);

        Assert.Equal(
            "a contratação está alinhada às diretrizes da IN e segue em acompanhamento contínuo, sem "
            + "necessidade de ação adicional (art. 27, I, e art. 28 da IN);",
            CtrDespachoTextos.ResultadoAlinhada);

        Assert.Equal(
            "foram identificadas oportunidades de melhoria ou pontos a esclarecer, desse modo, foi(foram) "
            + "solicitada(s) informação(ões) complementar(es) ao órgão/entidade, nos termos do art. 29 da IN;",
            CtrDespachoTextos.ResultadoInformacoesComplementares);

        Assert.Equal(
            "foram identificados riscos significativos ou desvios relevantes, conforme os critérios dos "
            + "arts. 11 e 25 da IN, tendo a SGDI:",
            CtrDespachoTextos.ResultadoRiscosSignificativos);

        Assert.Equal(
            "recomendado a suspensão temporária da contratação para reanálise (art. 33 da IN);",
            CtrDespachoTextos.RiscoRecomendouSuspensao);

        Assert.Equal(
            "comunicado o fato ao órgão de controle interno competente, mediante nota de motivação "
            + "(art. 34, VI, da IN c/c art. 6º, IV, do Decreto nº 48.899/2026);",
            CtrDespachoTextos.RiscoComunicouControleInterno);

        Assert.Equal(
            "Aguardando resposta da área demandante sobre os riscos e desvios identificados;",
            CtrDespachoTextos.RiscoAguardandoResposta);

        Assert.Equal(
            "Risco resolvido — após recomendações da SGDI e ações de mitigação adotadas pela área demandante, "
            + "os riscos foram adequadamente tratados, prosseguindo a contratação em acompanhamento contínuo;",
            CtrDespachoTextos.RiscoResolvido);

        Assert.Equal(
            "Não pode prosseguir — os riscos apontados não foram adequadamente tratados, tendo a SGDI "
            + "determinado a suspensão completa da contratação, com prazo para nova manifestação do "
            + "órgão/entidade, permanecendo o feito em acompanhamento contínuo.",
            CtrDespachoTextos.RiscoNaoPodeProsseguir);

        Assert.Equal(
            "A contratação não constava como previamente comunicada a esta SGDI. Diante da comunicação do TCDF, "
            + "o órgão/a entidade demandante será notificado para regularizar a comunicação obrigatória "
            + "(Anexo I/II da IN SGDI nº 1/2026), no prazo de [__] dias (art. 40 da IN).",
            CtrDespachoTextos.IncisoII);

        Assert.Equal(
            "Cabe ressaltar que em todas as hipóteses supra descritas, a contratação é inserida e mantida no "
            + "portfólio estratégico de contratações de TIC, sob regime de supervisão contínua da SGDI, com "
            + "acompanhamento a prosseguir conforme o art. 34 da IN SGDI nº 1/2026.",
            CtrDespachoTextos.Ressalva);

        Assert.Equal(
            "Reitera-se que a supervisão exercida por esta SGDI tem natureza técnica e orientadora, não "
            + "implicando aprovação, validação ou substituição das competências dos órgãos de controle interno "
            + "e externo (art. 9º e art. 25, §2º, da IN SGDI nº 1/2026), permanecendo a SGDI à disposição para "
            + "esclarecimentos adicionais.",
            CtrDespachoTextos.NaturezaTecnica);
    }

    [Fact]
    public void Textos_PreenchemOsColchetesDoPapel()
    {
        var incisoI = CtrDespachoTextos.IncisoIPreenchido(new DateOnly(2026, 5, 22), CtrDominios.Criticidade.Media);
        Assert.Contains("monitoramento contínuo desde 22/05/2026.", incisoI);
        Assert.Contains("criticidade Média (art. 11", incisoI);
        Assert.DoesNotContain("[data]", incisoI);
        Assert.DoesNotContain("[Alta/Média/Baixa]", incisoI);

        var incisoII = CtrDespachoTextos.IncisoIIPreenchido(15);
        Assert.Contains("no prazo de 15 dias (art. 40 da IN).", incisoII);

        // Bloco não marcado mantém os colchetes do formulário em branco
        Assert.Contains("[data]", CtrDespachoTextos.IncisoIPreenchido(null, null));
        Assert.Contains("[__]", CtrDespachoTextos.IncisoIIPreenchido(null));
    }

    [Fact]
    public void Caixa_MarcaSomenteAOpcaoEscolhida()
    {
        Assert.Equal("( X )", CtrDespachoTextos.Caixa(true));
        Assert.Equal("(   )", CtrDespachoTextos.Caixa(false));
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CtrDominios.ResultadoAnalise.Alinhada, null)]
    [InlineData(CtrDominios.ResultadoAnalise.InformacoesComplementares, null)]
    [InlineData(CtrDominios.ResultadoAnalise.RiscosSignificativos, CtrDominios.DesfechoRisco.AguardandoResposta)]
    [InlineData(CtrDominios.ResultadoAnalise.RiscosSignificativos, CtrDominios.DesfechoRisco.RiscoResolvido)]
    [InlineData(CtrDominios.ResultadoAnalise.RiscosSignificativos, CtrDominios.DesfechoRisco.NaoPodeProsseguir)]
    public void Gerar_IncisoI_ProduzPdf(string resultado, string? desfecho)
    {
        var manifestacao = Manifestacao(CtrDominios.SituacaoPortfolio.ComunicadaPreviamente, resultado, desfecho);
        manifestacao.RecomendouSuspensao = desfecho != null;
        manifestacao.ComunicouControleInterno = desfecho != null;

        var pdf = CtrDespachoPdf.Gerar(_processo, manifestacao,
            "Brasília/DF", "Clara das Contratações", "Analista da SGDI", new DateOnly(2026, 9, 1));

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Gerar_NaoImprimeAObservacaoInternaDaManifestacao()
    {
        // A observação é anotação interna da equipe: o template oficial do TCDF não
        // tem esse campo. Sem extrair texto (a fonte vai embutida e subsetada), a
        // prova é o PDF sair do MESMO tamanho com e sem observação preenchida.
        var comObservacao = Manifestacao(CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
            CtrDominios.ResultadoAnalise.Alinhada);
        comObservacao.Observacao = "Combinar com o gabinete antes de assinar — anotação interna da equipe.";

        var semObservacao = Manifestacao(CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
            CtrDominios.ResultadoAnalise.Alinhada);

        var pdfCom = CtrDespachoPdf.Gerar(_processo, comObservacao,
            "Brasília/DF", "Clara das Contratações", "Analista da SGDI", new DateOnly(2026, 9, 1));
        var pdfSem = CtrDespachoPdf.Gerar(_processo, semObservacao,
            "Brasília/DF", "Clara das Contratações", "Analista da SGDI", new DateOnly(2026, 9, 1));

        Assert.Equal(pdfSem.Length, pdfCom.Length);
    }

    [Fact]
    public void Gerar_PiorCaso_CabeEmUmaPaginaSemDeixarOFechoOrfao()
    {
        // Regressão: o fecho (À consideração superior. + local + data + nome — cargo)
        // escorregava sozinho para a página 2. Ele agora é um bloco só com ShowEntire
        // e o ritmo vertical foi ajustado para o despacho caber em uma página — mesmo
        // no pior caso: inciso I com riscos (todas as alternativas) e o objeto mais
        // longo da planilha real.
        var processoLongo = new CtrProcessoResponse
        {
            NumeroProcesso = "00080-00224827/2024-02",
            OrgaoNome = "Secretaria de Estado de Educação do Distrito Federal",
            OrgaoSigla = "SEEDF",
            ComplementoArea = "Subsecretaria de Modernização e Tecnologia",
            Objeto = "Contratação de empresa especializada na prestação de serviços técnicos especializados de "
                + "desenvolvimento, manutenção e evolução de software, sob o modelo de fábrica de software, na "
                + "modalidade de execução indireta, fornecimento contínuo e remoto, com medição baseada em pontos "
                + "de função (PF), complementada por horas técnicas trabalhadas (HST), sob demanda, sem exigência "
                + "de consumo mínimo mensal, para atender às necessidades da Secretaria de Estado de Educação do "
                + "Distrito Federal",
            CategoriaObjeto = CtrDominios.CategoriaObjeto.DesenvolvimentoSoftware
        };

        var manifestacao = Manifestacao(CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
            CtrDominios.ResultadoAnalise.RiscosSignificativos, CtrDominios.DesfechoRisco.NaoPodeProsseguir);
        manifestacao.RecomendouSuspensao = true;
        manifestacao.ComunicouControleInterno = true;

        var pdf = CtrDespachoPdf.Gerar(processoLongo, manifestacao,
            "Brasília/DF", "Clara das Contratações", "Analista da SGDI", new DateOnly(2026, 9, 1));

        Assert.Equal(1, ContarPaginas(pdf));
    }

    /// <summary>
    /// Páginas do PDF lidas do próprio arquivo: /Count do nó Pages, com a contagem
    /// dos objetos /Type /Page como reserva. Se a heurística parar de funcionar numa
    /// atualização do QuestPDF, o teste falha alto em vez de passar sem medir.
    /// </summary>
    private static int ContarPaginas(byte[] pdf)
    {
        var conteudo = System.Text.Encoding.Latin1.GetString(pdf);

        var count = Regex.Match(conteudo, @"/Count\s+(\d+)");
        if (count.Success) return int.Parse(count.Groups[1].Value);

        var paginas = Regex.Matches(conteudo, @"/Type\s*/Page[^s]").Count;
        Assert.True(paginas > 0,
            "não foi possível contar as páginas do PDF — a heurística precisa ser revista");
        return paginas;
    }

    [Fact]
    public void Gerar_IncisoII_ProduzPdf()
    {
        var pdf = CtrDespachoPdf.Gerar(_processo,
            Manifestacao(CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente),
            "Brasília/DF", "Clara das Contratações", "Analista da SGDI", new DateOnly(2026, 9, 1));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task GerarPeloService_UsaOProcessoEExigeManifestacaoDeProcessoAtivo()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62", p => p.ChegadaSgdi = DiasAtras(20));
        var manifestacoes = NovoManifestacaoService();
        var criada = await manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var pdf = await manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília/DF", "Clara", "Analista da SGDI");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        await NovoProcessoService().ExcluirAsync(processo.Id, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília/DF", "Clara", "Analista da SGDI"));
        Assert.Equal((int)ErrorCode.CtrManifestacaoNaoEncontrada, ex.Error.Code);
    }
}
