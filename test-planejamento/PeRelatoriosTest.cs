using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Os relatórios do acompanhamento (E7, rodada B), no mesmo motor do documento da E5: o RA de
/// cada ciclo (Anexo XIV) e o RR (Anexo XV), cada um com o seu modelo, a sua cópia do órgão e as
/// suas versões. O que se prova: os marcadores do ciclo; no RA de um monitoramento, os capítulos
/// da avaliação (4, 5 e 8) em branco com o aviso e as seções da avaliação fora; as tabelas das
/// seções por ciclo filtradas pelo ciclo do relatório; os blocos novos em grupos (ações por
/// situação, metas por resultado, riscos que ocorreram e medições); o RA de uma avaliação com o
/// ciclo de monitoramento de referência; o RR com todos os ciclos e a coluna do ciclo; a edição
/// isolada por documento; quem lê e quem edita; o PDF com o nome, o rodapé e a numeração de cada
/// documento; e o intervalo da atualização (409).
/// </summary>
public class PeRelatoriosTest : PeAcompanhamentoTestBase
{
    /// <summary>
    /// O PDTIC completo com dois trimestres: no 1º, A01 em andamento (30%), A02 não iniciada e
    /// A03 cancelada, a medição do IM01, o resumo e a ocorrência do R01; no 2º, A01 concluída, o
    /// resumo e o R01 fechado.
    /// </summary>
    private async Task<(long Id, PeCicloResponse T1, PeCicloResponse T2)> AcompanhamentoAsync()
    {
        var id = await AcompanhadoAsync();
        var (a1, a2, a3, r1) = (IdDe(id, "A01"), IdDe(id, "A02"), IdDe(id, "A03"), IdDe(id, "R01"));
        var indicador = await PlanoDeMonitoramentoAsync(id);
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);

        await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 30, 25, "Termo de referência aprovado."), Linha(a2, "nao_iniciada", 0),
            Linha(a3, "cancelada", 0));
        await GravarMedicoesAsync(t1.Id, new PeCicloMedicaoItemDTO { IndicadorId = indicador.Id, ValorApurado = 22.5m, Data = "2026-03-31" });
        await IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "Resumo do primeiro trimestre." }, cicloId: t1.Id);
        await IncluirNoPdticAsync(id, "riscos_ocorridos",
            new { data = "2026-02-10", situacao = "aberto", acoes_realizadas = "Estudo técnico antecipado.", responsavel = "Coordenação de Sistemas" },
            new { risco = new[] { r1 } }, cicloId: t1.Id);

        await GravarAcoesAsync(t2.Id, Linha(a1, "concluida", 100, 90));
        await IncluirNoPdticAsync(id, "relatorio_ciclo", new { resumo = "Resumo do segundo trimestre." }, cicloId: t2.Id);
        await IncluirNoPdticAsync(id, "riscos_ocorridos",
            new { data = "2026-05-10", situacao = "fechado", acoes_realizadas = "Contrato assinado.", responsavel = "Coordenação de Sistemas" },
            new { risco = new[] { r1 } }, cicloId: t2.Id);
        return (id, t1, t2);
    }

    private static PeDocBlocoResponse Bloco(PeDocumentoResponse documento, string capitulo, string tipo) =>
        Cap(documento, capitulo).Blocos.Single(b => b.Tipo == tipo);

    private static List<string?> Codigos(PeDocGrupoResponse grupo) => grupo.Tabela.Linhas.Select(l => l.Codigo).ToList();

    [Fact]
    public async Task RaDoMonitoramento_OCicloNaCapa_OsCapitulosDaAvaliacaoEmBranco_EOsDadosDoCiclo()
    {
        var (id, t1, _) = await AcompanhamentoAsync();

        var ra = await Documentos.ObterAsync(PeDocAlvo.Ra(id, t1.Id), await Orgao());

        Assert.Equal((PeDominios.TipoDocumento.Ra, (long?)t1.Id, Trimestre1, "Relatório de Acompanhamento do PDTIC"),
            (ra.DocTipo, ra.CicloId, ra.CicloRotulo, ra.Titulo));
        Assert.True(ra.PodeEditar);
        Assert.Empty(ra.Versoes);

        // O ciclo na capa: a capa do PDF (e a da prévia) traz a linha do ciclo com o período, e o
        // texto da capa do modelo não repete (F1, C25)
        var ciclo = Context.PeCiclos.AsNoTracking().Single(c => c.Id == t1.Id);
        Assert.Equal("Ciclo 2026 · 1º trimestre (01/01/2026 a 31/03/2026)", PeDocumentoPdf.LinhaDoCiclo(ra, ciclo));
        Assert.DoesNotContain("Ciclo", TextoDe(Texto(ra, "capa").TextoResolvido));
        Assert.DoesNotContain(Texto(ra, "capa").MarcadoresSemValor, m => m.StartsWith("ciclo."));

        // Os capítulos 4, 5 e 8 (da avaliação intermediária) saem em branco, com o aviso
        var emBranco = ra.Capitulos.Where(c => c.Aviso != null).ToList();
        Assert.Equal(new[] { ("avaliacao_metas", "4"), ("execucao_orcamentaria", "5"), ("pessoas", "8") }, emBranco.Select(c => (c.Chave, c.Numero!)));
        Assert.All(emBranco, c =>
        {
            Assert.Empty(c.Blocos);
            Assert.Equal(PeDocumentoService.AvisoSoNaAvaliacao, c.Aviso);
        });

        // Ações por situação no ciclo: a referência é 31/03/2026 (o fim do ciclo)
        var acoes = Bloco(ra, "monitoramento_acoes", PeDominios.TipoBloco.AcoesPorSituacao);
        Assert.Null(acoes.Tabela);
        Assert.True(acoes.PaginaDeitada);
        Assert.Equal(new[] { "em_dia", "atrasadas", "nao_iniciadas", "canceladas" }, acoes.Grupos!.Select(g => g.Chave));
        Assert.Equal(new[] { "Ações em dia e concluídas", "Ações atrasadas", "Ações não iniciadas", "Ações canceladas" }, acoes.Grupos!.Select(g => g.Titulo));
        Assert.Equal(new[] { "A01" }, Codigos(acoes.Grupos![0]));
        Assert.Equal(new[] { "A02" }, Codigos(acoes.Grupos![1]));
        Assert.True(acoes.Grupos![2].Tabela.Vazia);
        Assert.Equal(new[] { "A03" }, Codigos(acoes.Grupos![3]));
        Assert.Equal("Ações com a conclusão prevista (ou, nas não iniciadas, o início previsto) antes de 31/03/2026.", acoes.Grupos![1].Texto);
        Assert.Null(acoes.Grupos![3].Texto);
        Assert.Equal(new[] { "acao", "situacao", "conclusao", "execucao_fisica", "execucao_orcamentaria", "observacao" },
            acoes.Grupos![0].Tabela.Colunas.Select(c => c.Chave));
        var a1 = acoes.Grupos![0].Tabela.Linhas[0].Celulas;
        Assert.Equal(("Contratar e implantar a solução de regulação.", "Em andamento", "30/06/2027", "30%", "25%", "Termo de referência aprovado."),
            (a1["acao"], a1["situacao"], a1["conclusao"], a1["execucao_fisica"], a1["execucao_orcamentaria"], a1["observacao"]));

        // Medições do ciclo: cada indicador do plano, com a medição
        var medicoes = Assert.Single(Bloco(ra, "monitoramento_acoes", PeDominios.TipoBloco.Medicoes).Grupos!);
        Assert.Equal(("medicoes", "Medições dos indicadores"), (medicoes.Chave, medicoes.Titulo));
        var medicao = Assert.Single(medicoes.Tabela.Linhas);
        Assert.Equal(("IM01", "22,5", "31/03/2026"), (medicao.Codigo, medicao.Celulas["valor_apurado"], medicao.Celulas["data"]));

        // A tabela de uma seção por ciclo mostra só o registro do ciclo do relatório
        var resumo = TabelaDe(ra, "monitoramento_acoes", "relatorio_ciclo");
        Assert.Equal("Resumo do primeiro trimestre.", Assert.Single(resumo.Tabela!.Linhas).Celulas["resumo"]);
        Assert.DoesNotContain(resumo.Tabela.Colunas, c => c.Chave == PeDocumentoService.ColunaDoCiclo);

        // Riscos que ocorreram no ciclo; as seções da avaliação não aparecem no RA de um monitoramento
        var riscos = Assert.Single(Bloco(ra, "riscos", PeDominios.TipoBloco.RiscosOcorridos).Grupos!);
        Assert.Equal("Riscos que ocorreram no ciclo", riscos.Titulo);
        var risco = Assert.Single(riscos.Tabela.Linhas);
        Assert.Equal(("R01", "Atraso na contratação da solução de regulação.", "Alto", "Aberto"),
            (risco.Codigo, risco.Celulas["risco"], risco.Celulas["nivel_do_risco"], risco.Celulas["situacao"]));
        Assert.DoesNotContain(ra.Capitulos.SelectMany(c => c.Blocos), b => b.Tabela?.SecaoChave is "analise_intermediaria" or "avaliacao_comite");
        Assert.DoesNotContain(ra.Capitulos.SelectMany(c => c.Blocos), b => b.Tipo == PeDominios.TipoBloco.MetasPorResultado);

        // O roteiro do PDF leva o aviso dos três capítulos
        var roteiro = PeDocumentoPdf.Montar(ra);
        Assert.Equal(3, roteiro.Grupos.SelectMany(g => g.Pecas).Count(p => p.Tipo == PeDocumentoPdf.TipoPeca.Aviso));
    }

    [Fact]
    public async Task RaDaAvaliacao_AsMetasPeloResultado_EOMonitoramentoDeReferencia()
    {
        var (id, _, _) = await AcompanhamentoAsync();
        var avaliacao = await AbrirAvaliacaoAsync(id);
        await IncluirNoPdticAsync(id, "resultados_intermediarios", new { valor_alcancado = "40%", data = "2026-09-01", situacao = "em_andamento" },
            new { meta = new[] { IdDe(id, "M01") } }, cicloId: avaliacao.Id);
        await IncluirNoPdticAsync(id, "resultados_intermediarios", new { valor_alcancado = "0%", data = "2026-09-01", situacao = "nao_alcancada" },
            new { meta = new[] { IdDe(id, "M02") } }, cicloId: avaliacao.Id);
        await IncluirNoPdticAsync(id, "analise_intermediaria", new
        {
            parecer_execucao = "A execução está dentro do esperado.", metas_nao_atingidas = "A M02 depende do contrato de backup.",
            ajustes_propostos = "Rever o prazo da M02.", parecer_riscos = "Os riscos estão sob controle."
        }, cicloId: avaliacao.Id);

        var ra = await Documentos.ObterAsync(PeDocAlvo.Ra(id, avaliacao.Id), await Orgao());

        Assert.Equal("Avaliação intermediária 1", ra.CicloRotulo);
        Assert.Empty(ra.Capitulos.Where(c => c.Aviso != null));
        // A avaliação aberta ainda não tem fim: a capa diz desde quando, sem a palavra "Ciclo" (F1, C25)
        var doCiclo = Context.PeCiclos.AsNoTracking().Single(c => c.Id == avaliacao.Id);
        Assert.Equal($"Avaliação intermediária 1 (desde {PeFormato.Data(doCiclo.Inicio)})", PeDocumentoPdf.LinhaDoCiclo(ra, doCiclo));

        var metas = Bloco(ra, "avaliacao_metas", PeDominios.TipoBloco.MetasPorResultado);
        Assert.Equal(new[] { "alcancadas", "em_andamento", "nao_alcancadas", "canceladas" }, metas.Grupos!.Select(g => g.Chave));
        Assert.Equal(new[] { "M01" }, Codigos(metas.Grupos![1]));
        Assert.Equal(new[] { "M02" }, Codigos(metas.Grupos![2]));
        Assert.Equal(("40%", "01/09/2026"), (metas.Grupos![1].Tabela.Linhas[0].Celulas["valor_alcancado"], metas.Grupos![1].Tabela.Linhas[0].Celulas["data"]));
        Assert.Equal("A M02 depende do contrato de backup.",
            TabelaDe(ra, "avaliacao_metas", "analise_intermediaria").Tabela!.Linhas[0].Celulas["metas_nao_atingidas"]);
        Assert.Equal("Os riscos estão sob controle.", TabelaDe(ra, "riscos", "analise_intermediaria").Tabela!.Linhas[0].Celulas["parecer_riscos"]);

        // As ações e o resumo vêm do último ciclo de monitoramento com dado (o 2º trimestre)
        var acoes = Bloco(ra, "monitoramento_acoes", PeDominios.TipoBloco.AcoesPorSituacao);
        Assert.StartsWith("Pela situação registrada no ciclo 2026 · 2º trimestre: ações", acoes.Grupos![0].Texto);
        Assert.Equal(new[] { "A01" }, Codigos(acoes.Grupos![0]));
        Assert.Equal(new[] { "A02", "A03" }, Codigos(acoes.Grupos!.Single(g => g.Chave == "sem_registro")));
        Assert.Equal("Resumo do segundo trimestre.", TabelaDe(ra, "monitoramento_acoes", "relatorio_ciclo").Tabela!.Linhas.Single().Celulas["resumo"]);

        // Os riscos: as ocorrências dos ciclos até hoje, com o ciclo de cada uma
        var riscos = Assert.Single(Bloco(ra, "riscos", PeDominios.TipoBloco.RiscosOcorridos).Grupos!);
        Assert.Equal(PeDocumentoService.ColunaDoCiclo, riscos.Tabela.Colunas[0].Chave);
        Assert.Equal(new[] { Trimestre1, Trimestre2 }, riscos.Tabela.Linhas.Select(l => l.Celulas[PeDocumentoService.ColunaDoCiclo]));
    }

    [Fact]
    public async Task Rr_TodosOsCiclos_ComOCiclo_EAsMetasPeloResultadoFinal()
    {
        var (id, _, _) = await AcompanhamentoAsync();
        await IncluirNoPdticAsync(id, "resultados_metas", new { resultado = "alcancada", motivo = "O sistema chegou a todas as unidades." },
            new { meta = new[] { IdDe(id, "M01") } });

        var rr = await Documentos.ObterAsync(PeDocAlvo.Rr(id), await Orgao());

        Assert.Equal((PeDominios.TipoDocumento.Rr, (long?)null, (string?)null, "Relatório de Resultados do PDTIC"), (rr.DocTipo, rr.CicloId, rr.CicloRotulo, rr.Titulo));
        Assert.Empty(rr.Capitulos.Where(c => c.Aviso != null));

        // Ações pela última situação registrada, com o ciclo do registro
        var acoes = Bloco(rr, "avaliacao_metas", PeDominios.TipoBloco.AcoesPorSituacao);
        Assert.Equal(new[] { "concluidas", "em_andamento", "nao_iniciadas", "canceladas" }, acoes.Grupos!.Select(g => g.Chave));
        Assert.Equal(new[] { "A01" }, Codigos(acoes.Grupos![0]));
        Assert.Equal(new[] { "A02" }, Codigos(acoes.Grupos![2]));
        Assert.Equal(new[] { "A03" }, Codigos(acoes.Grupos![3]));
        Assert.Equal(PeDocumentoService.ColunaDoCiclo, acoes.Grupos![0].Tabela.Colunas[0].Chave);
        Assert.Equal((Trimestre2, Trimestre1), (acoes.Grupos![0].Tabela.Linhas[0].Celulas[PeDocumentoService.ColunaDoCiclo],
            acoes.Grupos![2].Tabela.Linhas[0].Celulas[PeDocumentoService.ColunaDoCiclo]));

        // Metas pelo resultado final: a sem resultado vem à parte
        var metas = Bloco(rr, "avaliacao_metas", PeDominios.TipoBloco.MetasPorResultado);
        Assert.Equal(new[] { "alcancadas", "nao_alcancadas", "canceladas", "sem_registro" }, metas.Grupos!.Select(g => g.Chave));
        Assert.Equal(new[] { "M01" }, Codigos(metas.Grupos![0]));
        Assert.Equal("O sistema chegou a todas as unidades.", metas.Grupos![0].Tabela.Linhas[0].Celulas["motivo"]);
        Assert.Equal(new[] { "M02" }, Codigos(metas.Grupos![3]));

        // Os riscos de todos os ciclos
        var riscos = Assert.Single(Bloco(rr, "avaliacao_metas", PeDominios.TipoBloco.RiscosOcorridos).Grupos!);
        Assert.Equal(("Riscos que ocorreram", "As ocorrências registradas em todos os ciclos de monitoramento."), (riscos.Titulo, riscos.Texto));
        Assert.Equal(2, riscos.Tabela.Linhas.Count);
    }

    [Fact]
    public async Task Edicao_CadaDocumentoComASuaCopia()
    {
        var (id, t1, t2) = await AcompanhamentoAsync();
        var ctx = await Orgao();
        var ra = PeDocAlvo.Ra(id, t1.Id);
        var introducao = BlocoDoModelo("introducao", documento: PeDominios.TipoDocumento.Ra);

        var bloco = await Documentos.SalvarTextoAsync(ra, introducao.Id, Json(Rico("Introdução do primeiro trimestre.")), ctx);

        Assert.True(bloco.EditadoPeloOrgao);
        Assert.Equal("Introdução do primeiro trimestre.", TextoDe(Texto(await Documentos.ObterAsync(ra, ctx), "introducao").TextoBruto));
        // O RA do outro ciclo continua com o texto do modelo
        Assert.False(Texto(await Documentos.ObterAsync(PeDocAlvo.Ra(id, t2.Id), ctx), "introducao").EditadoPeloOrgao);
        // A linha da cópia é do RA do ciclo
        var copia = Context.PeDocOrgaoBlocosDocumento.AsNoTracking().Single();
        Assert.Equal((PeDominios.TipoDocumento.Ra, (long?)t1.Id), (copia.DocTipo, copia.CicloId));

        // Esconder um capítulo opcional num relatório não mexe nos outros
        await Documentos.AtualizarCapituloAsync(ra, CapituloDoModelo("alinhamento", PeDominios.TipoDocumento.Ra).Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx);
        Assert.True(Cap(await Documentos.ObterAsync(ra, ctx), "alinhamento").Oculto);
        Assert.False(Cap(await Documentos.ObterAsync(PeDocAlvo.Ra(id, t2.Id), ctx), "alinhamento").Oculto);
        Assert.False(Cap(await Documentos.ObterAsync(PeDocAlvo.Rr(id), ctx), "alinhamento").Oculto);

        // O bloco de outro documento não é deste
        Assert.Equal(Codigo(ErrorCode.PeDocBlocoNaoEncontrado),
            await ErroAsync(() => Documentos.SalvarTextoAsync(ra, BlocoDoModelo("introducao").Id, Json(Rico("x")), ctx)));

        // Voltar ao texto do modelo tira a cópia (e a parte com o documento)
        await Documentos.RestaurarTextoAsync(ra, introducao.Id, ctx);
        Assert.Empty(Context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == id));
        Assert.Empty(Context.PeDocOrgaoBlocosDocumento.AsNoTracking());
    }

    [Fact]
    public async Task QuemLeEQuemEdita_EQuando()
    {
        var (id, t1, _) = await AcompanhamentoAsync();
        var ra = PeDocAlvo.Ra(id, t1.Id);
        var introducao = BlocoDoModelo("introducao", documento: PeDominios.TipoDocumento.Ra).Id;

        // A consulta e os papéis globais leem, sem editar
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic })
        {
            var ctx = await ContextoDe(user);
            Assert.False((await Documentos.ObterAsync(ra, ctx)).PodeEditar);
            Assert.False((await Documentos.ObterAsync(PeDocAlvo.Rr(id), ctx)).PodeEditar);
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => Documentos.SalvarTextoAsync(ra, introducao, Json(Rico("x")), ctx)));
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => Documentos.GerarPdfAsync(ra, ctx)));
        }
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Documentos.ObterAsync(ra, await ContextoDe(UserOrgaoSeec))));

        // Ciclo que não é do PDTIC: 404; o RA do ciclo que não começou: lê, mas não edita
        Assert.Equal(Codigo(ErrorCode.PeCicloNaoEncontrado), await ErroAsync(async () => await Documentos.ObterAsync(PeDocAlvo.Ra(id, 999999), await Orgao())));
        var ultimo = PeDocAlvo.Ra(id, (await CicloAsync(id, UltimoTrimestre)).Id);
        Assert.False((await Documentos.ObterAsync(ultimo, await Orgao())).PodeEditar);
        var futuro = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.SalvarTextoAsync(ultimo, introducao, Json(Rico("x")), await Orgao()));
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), futuro.Error.Code);
        Assert.StartsWith($"O ciclo {UltimoTrimestre} começa em 01/10/2029.", futuro.Error.Message);

        // PDTIC encerrado: lê, mas não edita
        Situacao(id, PeDominios.SituacaoPdtic.Encerrado);
        Assert.False((await Documentos.ObterAsync(ra, await Orgao())).PodeEditar);
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(async () => await Documentos.SalvarTextoAsync(ra, introducao, Json(Rico("x")), await Orgao())));
    }

    [Fact]
    public async Task AntesDaPublicacao409_ESemAVersao6409()
    {
        var seec = await AbrirSeecAsync();
        var ctx = await ContextoDe(UserOrgaoSeec);
        var ex = await Assert.ThrowsAsync<ApiException>(() => Documentos.ObterAsync(PeDocAlvo.Rr(seec.Id), ctx));
        Assert.Equal((Codigo(ErrorCode.PePdticSituacaoInvalida), "O relatório de resultados fica disponível depois da publicação do PDTIC."),
            (ex.Error.Code, ex.Error.Message));

        var id = await AcompanhadoAsync();
        VersaoDoModelo(5);
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), await ErroAsync(async () => await Documentos.ObterAsync(PeDocAlvo.Rr(id), await Orgao())));
        // O documento do PDTIC continua de pé
        Assert.Equal(PeDominios.TipoDocumento.Pdtic, (await Documentos.ObterAsync(id, await Orgao())).DocTipo);
    }

    [Fact]
    public async Task Pdf_CadaDocumentoComASuaNumeracao_ONomeEORodape()
    {
        var (id, t1, _) = await AcompanhamentoAsync();
        var ctx = await Orgao();
        var ra = PeDocAlvo.Ra(id, t1.Id);

        var primeira = await Documentos.GerarPdfAsync(ra, ctx);
        var segunda = await Documentos.GerarPdfAsync(ra, ctx);
        var rr = await Documentos.GerarPdfAsync(PeDocAlvo.Rr(id), ctx);

        Assert.Equal((1, 2, 1), (primeira.Numero, segunda.Numero, rr.Numero));
        Assert.All(new[] { primeira, segunda, rr }, v => Assert.Equal(PeDominios.SituacaoVersaoDoc.Minuta, v.Situacao));
        Assert.True(primeira.Paginas >= 5, $"páginas: {primeira.Paginas}");
        Assert.Equal(new[] { 2, 1 }, (await Documentos.VersoesAsync(ra, ctx)).Select(v => v.Numero));
        Assert.Empty(await Documentos.VersoesAsync(PeDocAlvo.Ra(id, (await CicloAsync(id, Trimestre2)).Id), ctx));
        Assert.Single(await Documentos.VersoesAsync(PeDocAlvo.Rr(id), ctx));
        // O documento do PDTIC não vê as versões dos relatórios (nem o passo do documento)
        Assert.Empty(await Documentos.VersoesAsync(id, ctx));
        Assert.Empty((await Documentos.ObterAsync(id, ctx)).Versoes);

        var arquivo = await Documentos.ArquivoDaVersaoAsync(ra, 2, ctx);
        Assert.Equal("RA_SES_v1.0_2026-T1_2.pdf", arquivo.NomeArquivo);
        Assert.StartsWith("%PDF-", TextoDoPdf(arquivo.Conteudo));
        Assert.Equal("RR_SES_v1.0_1.pdf", (await Documentos.ArquivoDaVersaoAsync(PeDocAlvo.Rr(id), 1, ctx)).NomeArquivo);
        Assert.Equal(Codigo(ErrorCode.PeDocVersaoNaoEncontrada), await ErroAsync(() => Documentos.ArquivoDaVersaoAsync(PeDocAlvo.Rr(id), 2, ctx)));

        // Os rodapés
        Assert.Equal("SES · Relatório de acompanhamento, 2026 · 1º trimestre · PDTIC versão 1.0 · minuta nº 1",
            PeDocumentoService.Rodape("SES", "1.0", 1, PeDominios.SituacaoVersaoDoc.Minuta, PeDominios.TipoDocumento.Ra, Trimestre1));
        Assert.Equal("SES · Relatório de resultados · PDTIC versão 1.0 · minuta nº 2",
            PeDocumentoService.Rodape("SES", "1.0", 2, PeDominios.SituacaoVersaoDoc.Minuta, PeDominios.TipoDocumento.Rr, null));
        Assert.Equal("SES · PDTIC versão 1.0 · minuta nº 3",
            PeDocumentoService.Rodape("SES", "1.0", 3, PeDominios.SituacaoVersaoDoc.Minuta, PeDominios.TipoDocumento.Pdtic, null));
    }
}
