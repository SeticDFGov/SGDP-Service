using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A conformidade da SGDI (E8): cada item sobre o PDTIC vigente (sem ele, o da elaboração), com
/// o detalhe em texto; o "não se aplica" do acompanhamento antes da publicação (fora da conta); o
/// PDTIC registrado fora do sistema; o órgão sem PDTIC (0%, baixa); os grupos nos limites; os
/// filtros e o resumo; a planilha em CSV e XLSX; e quem vê.
/// </summary>
public class PeConformidadeTest : PePaineisTestBase
{
    private static PeConformidadeAtendeResponse Item(PeConformidadeOrgaoResponse linha, string chave) => linha.Itens[chave];

    [Theory]
    [InlineData(0, "baixa")]
    [InlineData(24, "baixa")]
    [InlineData(25, "media")]
    [InlineData(69, "media")]
    [InlineData(70, "alta")]
    [InlineData(100, "alta")]
    public void Grupo_NosLimites(int percentual, string grupo) => Assert.Equal(grupo, PeDominios.GrupoConformidade.De(percentual));

    [Fact]
    public void Percentual_AtendidosSobreAplicaveis_Arredondado()
    {
        Assert.Equal(25, PeConformidadeRegras.Percentual(1, 4));
        Assert.Equal(67, PeConformidadeRegras.Percentual(2, 3));
        Assert.Equal(83, PeConformidadeRegras.Percentual(5, 6));
        Assert.Equal(20, PeConformidadeRegras.Percentual(1, 5));
        Assert.Equal(0, PeConformidadeRegras.Percentual(0, 5));
        Assert.Equal(0, PeConformidadeRegras.Percentual(0, 0));
        Assert.Equal(("Sim", "Não", "Não se aplica"), (PeConformidadeRegras.Texto(true), PeConformidadeRegras.Texto(false), PeConformidadeRegras.Texto(null)));
        Assert.Equal((12, "revisão anual, o padrão"), PeConformidadeRegras.PeriodicidadeDaRevisao(null));
        Assert.Equal((6, "revisão semestral"), PeConformidadeRegras.PeriodicidadeDaRevisao("semestral"));
        Assert.Equal((12, "revisão anual, o padrão"), PeConformidadeRegras.PeriodicidadeDaRevisao("outra"));
    }

    [Fact]
    public async Task Itens_NaOrdem_ComABase()
    {
        var conformidade = await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), await Sgdi());

        Assert.Equal(new[] { "aprovado_cgtic", "comunicado_sgdi", "vigente", "nove_conteudos", "revisao_em_dia", "acompanhamento_em_dia" },
            conformidade.Itens.Select(i => i.Chave));
        Assert.Equal(("PDTIC aprovado pelo CGTIC", "art. 5º do Decreto nº 48.900/2026"), (conformidade.Itens[0].Rotulo, conformidade.Itens[0].Base));
        Assert.Equal("art. 7º, V, do Decreto nº 48.899/2026", conformidade.Itens[1].Base);
        Assert.Equal("art. 12, § 2º, do Decreto nº 48.900/2026", conformidade.Itens[3].Base);
        Assert.Equal("Guia de PDTIC do SISP, capítulo 7", conformidade.Itens[5].Base);
        Assert.All(conformidade.Itens, i => Assert.False(string.IsNullOrWhiteSpace(i.AtendeQuando)));
    }

    [Fact]
    public async Task SemPdtic_ZeroPorCento_Baixa_EOAcompanhamentoNaoSeAplica()
    {
        var linha = await LinhaAsync(OrgaoSeec);

        Assert.Equal(("SEEC", "Secretaria de Estado de Economia", "Básico"), (linha.Sigla, linha.Nome, linha.NivelNome));
        Assert.Null(linha.PdticId);
        Assert.Null(linha.PdticSituacao);
        Assert.Equal((0, 5, 0, "baixa"), (linha.Atendidos, linha.Aplicaveis, linha.Percentual, linha.Grupo));
        Assert.All(linha.Itens.Where(i => i.Key != "acompanhamento_em_dia"),
            i => Assert.Equal((false, "O órgão ainda não abriu o PDTIC"), (i.Value.Atende, i.Value.Detalhe)));
        Assert.Null(Item(linha, "acompanhamento_em_dia").Atende);
        Assert.Null(linha.Inadimplencia);
    }

    [Fact]
    public async Task EmElaboracao_NoveConteudosPelosPassosTravados()
    {
        var id = (await AbrirSesAsync()).Id;

        var vazio = await LinhaAsync(OrgaoSes);
        Assert.Equal((id, "1.0", "em_elaboracao"), (vazio.PdticId!.Value, vazio.PdticVersao, vazio.PdticSituacao));
        Assert.Equal((false, "Ainda não enviado ao CGTIC"), (Item(vazio, "aprovado_cgtic").Atende, Item(vazio, "aprovado_cgtic").Detalhe));
        Assert.Equal((false, "Ainda não enviado ao CGTIC pelo sistema"), (Item(vazio, "comunicado_sgdi").Atende, Item(vazio, "comunicado_sgdi").Detalhe));
        Assert.Equal((false, "Ainda não publicado (em elaboração)"), (Item(vazio, "vigente").Atende, Item(vazio, "vigente").Detalhe));
        // No Básico: 2.1 (I), 2.2 (II), 2.3 (III), 3.2 (VII), 3.3 (V, VI e IX) e 3.4 (IV); a 2.4 (VIII) já está feita (as aquisições são opcionais)
        Assert.Equal((false, "Falta concluir os passos 2.1 (inciso I), 2.2 (inciso II), 2.3 (inciso III), 3.2 (inciso VII), 3.3 (incisos V, VI e IX) e mais 1"),
            (Item(vazio, "nove_conteudos").Atende, Item(vazio, "nove_conteudos").Detalhe));
        Assert.Equal((false, "Ainda sem aprovação do CGTIC"), (Item(vazio, "revisao_em_dia").Atende, Item(vazio, "revisao_em_dia").Detalhe));
        Assert.Equal((null, "O acompanhamento começa depois da publicação do PDTIC"),
            (Item(vazio, "acompanhamento_em_dia").Atende, Item(vazio, "acompanhamento_em_dia").Detalhe));
        Assert.Equal((0, 5, 0, "baixa"), (vazio.Atendidos, vazio.Aplicaveis, vazio.Percentual, vazio.Grupo));

        // Com a elaboração preenchida, os nove conteúdos ficam feitos: 1 de 5, 20%
        await PreencherElaboracaoAsync(id);
        var preenchido = await LinhaAsync(OrgaoSes);
        Assert.Equal((true, "Os passos dos nove conteúdos estão feitos"), (Item(preenchido, "nove_conteudos").Atende, Item(preenchido, "nove_conteudos").Detalhe));
        Assert.Equal((1, 5, 20, "baixa"), (preenchido.Atendidos, preenchido.Aplicaveis, preenchido.Percentual, preenchido.Grupo));

        // Comentário aberto num passo travado: o passo fica em atenção e o item deixa de atender
        var ativos = Passo("diagnostico.ativos").Id;
        await Comentarios.CriarAsync(id, new PeComentarioCriarDTO { PassoId = ativos, Texto = "Faltou o contrato do datacenter." }, await Sgdi());
        var comComentario = await LinhaAsync(OrgaoSes);
        Assert.Equal((false, "Falta concluir o passo 2.2 (inciso II, com comentário aberto)"),
            (Item(comComentario, "nove_conteudos").Atende, Item(comComentario, "nove_conteudos").Detalhe));
    }

    [Fact]
    public async Task EmAprovacaoEDevolvido_ComunicadoPeloEnvio_EAAprovacaoPendente()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var hoje = Br(HojeData());

        var enviado = await LinhaAsync(OrgaoSes);
        Assert.Equal("em_aprovacao", enviado.PdticSituacao);
        Assert.Equal((false, $"Aguardando a deliberação do CGTIC desde {hoje}"), (Item(enviado, "aprovado_cgtic").Atende, Item(enviado, "aprovado_cgtic").Detalhe));
        Assert.Equal((true, $"Enviado ao CGTIC pelo sistema em {hoje} (vale como a comunicação à SGDI)"),
            (Item(enviado, "comunicado_sgdi").Atende, Item(enviado, "comunicado_sgdi").Detalhe));
        Assert.Equal((2, 5, 40, "media"), (enviado.Atendidos, enviado.Aplicaveis, enviado.Percentual, enviado.Grupo));

        await DevolverNoCgticAsync(pdtic.Id);
        var devolvido = await LinhaAsync(OrgaoSes);
        Assert.Equal((false, $"Devolvido pelo CGTIC em {hoje} para ajuste"), (Item(devolvido, "aprovado_cgtic").Atende, Item(devolvido, "aprovado_cgtic").Detalhe));
        // A versão devolvida já foi comunicada
        Assert.True(Item(devolvido, "comunicado_sgdi").Atende);
    }

    [Fact]
    public async Task Publicado_EmDia_TodosOsItens()
    {
        var id = await PublicadoHojeAsync();
        var hoje = HojeData();

        var linha = await LinhaAsync(OrgaoSes);

        Assert.Equal((id, "publicado"), (linha.PdticId!.Value, linha.PdticSituacao));
        Assert.Equal((true, $"Aprovado em {Br(hoje)} (Resolução nº 7/2026)"), (Item(linha, "aprovado_cgtic").Atende, Item(linha, "aprovado_cgtic").Detalhe));
        Assert.True(Item(linha, "comunicado_sgdi").Atende);
        Assert.Equal((true, "Vigente de 01/01/2026 a 31/12/2029"), (Item(linha, "vigente").Atende, Item(linha, "vigente").Detalhe));
        Assert.True(Item(linha, "nove_conteudos").Atende);
        Assert.Equal((true, $"Aprovação em {Br(hoje)}; a próxima revisão vence em {Br(hoje.AddMonths(12))} (revisão anual)"),
            (Item(linha, "revisao_em_dia").Atende, Item(linha, "revisao_em_dia").Detalhe));
        // Publicado hoje: o ciclo de hoje está aberto e no prazo
        Assert.Equal((true, "Nenhum ciclo de monitoramento atrasado"), (Item(linha, "acompanhamento_em_dia").Atende, Item(linha, "acompanhamento_em_dia").Detalhe));
        Assert.Equal((6, 6, 100, "alta"), (linha.Atendidos, linha.Aplicaveis, linha.Percentual, linha.Grupo));
        // A conformidade não grava: os ciclos continuam sem linha no banco
        Assert.Empty(CiclosNoBanco(id));
    }

    [Fact]
    public async Task Revisao_PelaDataDoAtoEPelaPeriodicidade_EVigenciaVencida()
    {
        var id = await PublicadoHojeAsync();
        var hoje = HojeData();

        // A última aprovação há 13 meses, com a revisão anual: vencida
        var ato = hoje.AddMonths(-13);
        DataDoAto(id, ato);
        var vencida = await LinhaAsync(OrgaoSes);
        Assert.Equal((false, $"A revisão venceu em {Br(ato.AddMonths(12))}: a última aprovação foi em {Br(ato)} (revisão anual)"),
            (Item(vencida, "revisao_em_dia").Atende, Item(vencida, "revisao_em_dia").Detalhe));
        Assert.Equal(1, (await PainelAsync()).Alertas.RevisaoVencida);

        // Semestral: há 7 meses já venceu; há 5 meses, não
        var abrangencia = Context.PeRegistros.Single(r => r.PdticId == id && r.SecaoId == Secao("abrangencia").Id);
        abrangencia.Dados = abrangencia.Dados.Replace("\"anual\"", "\"semestral\"");
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        DataDoAto(id, hoje.AddMonths(-7));
        Assert.False(Item(await LinhaAsync(OrgaoSes), "revisao_em_dia").Atende);
        var recente = hoje.AddMonths(-5);
        DataDoAto(id, recente);
        var semestral = Item(await LinhaAsync(OrgaoSes), "revisao_em_dia");
        Assert.Equal((true, $"Aprovação em {Br(recente)}; a próxima revisão vence em {Br(recente.AddMonths(6))} (revisão semestral)"),
            (semestral.Atende, semestral.Detalhe));

        // A vigência terminou ontem: o PDTIC continua publicado, mas não está vigente
        Situacao(id, "publicado", hoje.AddDays(-1));
        var terminada = await LinhaAsync(OrgaoSes);
        Assert.Equal((false, $"A vigência terminou em {Br(hoje.AddDays(-1))}"), (Item(terminada, "vigente").Atende, Item(terminada, "vigente").Detalhe));
        Assert.Equal(1, (await PainelAsync()).Alertas.VigenciaVencida);
    }

    [Fact]
    public async Task Acompanhamento_CicloAtrasado_SemGravar()
    {
        // Publicado em 10/01/2026: o 1º trimestre venceu em 15/04/2026 sem fechar
        var id = await AcompanhadoAsync();

        var linha = await LinhaAsync(OrgaoSes);

        var acompanhamento = Item(linha, "acompanhamento_em_dia");
        Assert.False(acompanhamento.Atende);
        Assert.StartsWith("Os ciclos 2026 · 1º trimestre", acompanhamento.Detalhe);
        Assert.EndsWith("passaram do prazo de fechamento", acompanhamento.Detalhe);
        Assert.Equal(1, (await PainelAsync()).Alertas.CicloAtrasado);
        // O cálculo usou o plano da periodicidade: nenhum ciclo foi criado
        Assert.Empty(CiclosNoBanco(id));

        // Com os trimestres vencidos fechados, em dia
        var ciclos = await CiclosAsync(id);
        foreach (var ciclo in ciclos.Where(c => c.Situacao == PeDominios.SituacaoCicloExibida.Atrasado))
        {
            await PreencherCicloAsync(id, ciclo.Id);
            await FecharAsync(ciclo.Id);
        }
        var emDia = Item(await LinhaAsync(OrgaoSes), "acompanhamento_em_dia");
        Assert.True(emDia.Atende);
        Assert.StartsWith("Nenhum ciclo de monitoramento atrasado; o último fechado foi 2026 · ", emDia.Detalhe);
    }

    /// <summary>Um PDF de verdade (o registro fora do sistema conta as páginas).</summary>
    private static byte[] PdfDeVerdade()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    private async Task<PePdticResponse> RegistrarForaAsync(string instancia, DateOnly aprovado, DateOnly publicado)
    {
        var arquivo = await EnviarArquivoAsync(UserAdminGeral, "PDTIC SEEC.pdf", PdfDeVerdade());
        return await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            OrgaoId = OrgaoSeec.Id,
            Versao = "1.0",
            VigenciaInicio = "2025-01-01",
            VigenciaFim = "2029-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = instancia,
            AprovacaoData = Iso(aprovado),
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "5/2026",
            PublicacaoData = Iso(publicado),
            PublicacaoEndereco = Endereco
        }, await AdminGeral());
    }

    [Fact]
    public async Task RegistradoForaDoSistema_ComunicadoPeloRegistro_RevisaoPelaPublicacao_EOsPassosExternos()
    {
        var hoje = HojeData();
        var pdtic = await RegistrarForaAsync("cgtic", hoje.AddDays(-30), hoje.AddDays(-20));

        var linha = await LinhaAsync(OrgaoSeec);

        Assert.Equal((true, $"Aprovado em {Br(hoje.AddDays(-30))} (Resolução nº 5/2026), registrado fora do sistema"),
            (Item(linha, "aprovado_cgtic").Atende, Item(linha, "aprovado_cgtic").Detalhe));
        Assert.Equal((true, $"Registrado fora do sistema em {Br(hoje)}, com a aprovação de {Br(hoje.AddDays(-30))}"),
            (Item(linha, "comunicado_sgdi").Atende, Item(linha, "comunicado_sgdi").Detalhe));
        Assert.True(Item(linha, "vigente").Atende);
        // A revisão conta da publicação
        Assert.Equal((true, $"Publicação em {Br(hoje.AddDays(-20))}; a próxima revisão vence em {Br(hoje.AddDays(-20).AddMonths(12))} (revisão anual, o padrão)"),
            (Item(linha, "revisao_em_dia").Atende, Item(linha, "revisao_em_dia").Detalhe));
        // As etapas 1 a 3 foram feitas fora, menos metas e ações (3.2 no Básico), que a equipe preenche para acompanhar
        Assert.Equal((false, "Falta concluir o passo 3.2 (inciso VII)"), (Item(linha, "nove_conteudos").Atende, Item(linha, "nove_conteudos").Detalhe));

        var meta = await IncluirNoPdticAsync(pdtic.Id, "metas", new { descricao = "Implantar o portal.", indicador = "Portal no ar", valor = "Sim", prazo = "2027-12-31" },
            user: UserAdminGeral);
        await IncluirNoPdticAsync(pdtic.Id, "acoes", new { descricao = "Contratar o portal.", tema = new[] { "transformacao_digital" }, responsavel = "TIC", conclusao = "2027-06-30", situacao = "nao_iniciada" },
            user: UserAdminGeral);
        Assert.NotNull(meta);
        var completo = await LinhaAsync(OrgaoSeec);
        Assert.Equal((true, "Os passos dos nove conteúdos estão feitos (6 fora do sistema, no PDTIC registrado)"),
            (Item(completo, "nove_conteudos").Atende, Item(completo, "nove_conteudos").Detalhe));
    }

    [Fact]
    public async Task RegistradoForaDoSistema_AprovadoPorOutraInstancia_NaoAtendeOCgtic()
    {
        var hoje = HojeData();
        await RegistrarForaAsync("outra", hoje.AddDays(-400), hoje.AddDays(-390));

        var linha = await LinhaAsync(OrgaoSeec);

        Assert.Equal((false, "Aprovado fora do sistema por outra instância, sem deliberação do CGTIC"),
            (Item(linha, "aprovado_cgtic").Atende, Item(linha, "aprovado_cgtic").Detalhe));
        Assert.True(Item(linha, "comunicado_sgdi").Atende);
        // A publicação foi há mais de um ano: a revisão venceu
        Assert.False(Item(linha, "revisao_em_dia").Atende);
    }

    [Fact]
    public async Task Encerrado_SemPdticEmVigor_EARevisaoAvaliaAVigente()
    {
        var id = await PublicadoHojeAsync();

        // Com a revisão aberta, a conformidade continua avaliando a vigente
        var revisao = await Aprovacao.RevisarAsync(id, new PeRevisaoDTO { Justificativa = "Mudou a estrutura do órgão." }, await Orgao());
        var comRevisao = await LinhaAsync(OrgaoSes);
        Assert.Equal((id, "1.0", "publicado"), (comRevisao.PdticId!.Value, comRevisao.PdticVersao, comRevisao.PdticSituacao));
        DataDoAto(id, HojeData().AddMonths(-14));
        Assert.EndsWith("; a revisão 1.1 está em elaboração", Item(await LinhaAsync(OrgaoSes), "revisao_em_dia").Detalhe);

        // Sem vigente nem elaboração (as duas versões encerradas), o órgão fica sem PDTIC: 0% e baixa
        Situacao(revisao.Id, PeDominios.SituacaoPdtic.Encerrado);
        Situacao(id, PeDominios.SituacaoPdtic.Encerrado);
        var encerrado = await LinhaAsync(OrgaoSes);
        Assert.Null(encerrado.PdticId);
        Assert.Equal((0, "baixa"), (encerrado.Percentual, encerrado.Grupo));
        Assert.StartsWith("O PDTIC 1.1 foi encerrado em ", Item(encerrado, "aprovado_cgtic").Detalhe);
        Assert.EndsWith(" e o próximo ainda não foi aberto", Item(encerrado, "aprovado_cgtic").Detalhe);
    }

    [Fact]
    public async Task Filtros_PorGrupoNivelEBusca_EOResumoSemOGrupo()
    {
        await PublicadoHojeAsync();
        var outro = NovoOrgao("DETRAN", "Departamento de Trânsito");
        await DefinirNivelDoOrgaoAsync(outro, "avancado");
        var sgdi = await Sgdi();

        var todos = await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), sgdi);
        Assert.Equal(new[] { "DETRAN", "SEEC", "SES" }, todos.Orgaos.Select(o => o.Sigla));
        Assert.Equal((1, 0, 2), (todos.Resumo.Alta, todos.Resumo.Media, todos.Resumo.Baixa));

        var baixa = await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Grupo = "baixa" }, sgdi);
        Assert.Equal(new[] { "DETRAN", "SEEC" }, baixa.Orgaos.Select(o => o.Sigla));
        // O resumo não depende do filtro do grupo
        Assert.Equal((1, 0, 2), (baixa.Resumo.Alta, baixa.Resumo.Media, baixa.Resumo.Baixa));
        Assert.Empty((await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Grupo = "otima" }, sgdi)).Orgaos);

        var avancado = await Paineis.ConformidadeAsync(new PeConformidadeConsulta { NivelId = NivelId("avancado") }, sgdi);
        Assert.Equal(("DETRAN", "Avançado"), (Assert.Single(avancado.Orgaos).Sigla, avancado.Orgaos[0].NivelNome));
        Assert.Equal((0, 0, 1), (avancado.Resumo.Alta, avancado.Resumo.Media, avancado.Resumo.Baixa));
        Assert.Empty((await Paineis.ConformidadeAsync(new PeConformidadeConsulta { NivelId = 999999 }, sgdi)).Orgaos);

        var busca = await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Filtro = "trânsito" }, sgdi);
        Assert.Equal("DETRAN", Assert.Single(busca.Orgaos).Sigla);
        Assert.Equal("SES", Assert.Single((await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Filtro = "ses" }, sgdi)).Orgaos).Sigla);

        // Órgão desativado sai da conformidade
        var desativado = NovoOrgao("EXTINTO");
        var linha = Context.PgiaOrgaos.Single(o => o.Id == desativado.Id);
        linha.Ativo = false;
        Context.SaveChanges();
        Assert.DoesNotContain((await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), sgdi)).Orgaos, o => o.Sigla == "EXTINTO");
    }

    [Fact]
    public async Task Permissoes_SoOsPapeisGlobaisEOAdminGeral()
    {
        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserPeCgtic, UserAdminGeral })
            Assert.Equal(2, (await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), await ContextoDe(user))).Orgaos.Count);
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserSemPapel })
        {
            var ctx = await ContextoDe(user);
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => Paineis.ConformidadeAsync(new PeConformidadeConsulta(), ctx)));
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "csv", ctx)));
        }
    }

    // ── Planilha ────────────────────────────────────────────────────────────

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    [Fact]
    public async Task Planilha_CsvEXlsx_UmaLinhaPorOrgao_UmaColunaPorItem()
    {
        await PublicadoHojeAsync();
        InadimplenciaNoBanco(OrgaoSeec, "inadimplente");
        var sgdi = await Sgdi();

        var csv = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "csv", sgdi);
        Assert.Equal(PePlanilhaService.MimeCsv, csv.TipoMime);
        Assert.Equal($"PDTIC_conformidade_{Iso(HojeData())}.csv", csv.NomeArquivo);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csv.Conteudo[..3]);
        var linhas = Encoding.UTF8.GetString(csv.Conteudo[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Órgão;Sigla;Nível;Versão do PDTIC;Situação do PDTIC;PDTIC aprovado pelo CGTIC;PDTIC comunicado à SGDI;PDTIC vigente;"
                     + "Os nove conteúdos preenchidos;Revisão em dia;Acompanhamento em dia;Atendidos;Aplicáveis;Percentual;Grupo;Inadimplência;Prazo da notificação",
            linhas[0]);
        Assert.Equal(3, linhas.Length);
        Assert.StartsWith("Secretaria de Estado de Economia;SEEC;Básico;;Sem PDTIC;Não;Não;Não;Não;Não;Não se aplica;0;5;0;Baixa;Inadimplente;", linhas[1]);
        Assert.Equal("Secretaria de Estado de Saúde;SES;Básico;1.0;Publicado;Sim;Sim;Sim;Sim;Sim;Sim;6;6;100;Alta;;", linhas[2]);

        var xlsx = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta { Grupo = "alta" }, null, sgdi);
        Assert.Equal(PePlanilhaService.MimeXlsx, xlsx.TipoMime);
        Assert.Equal($"PDTIC_conformidade_{Iso(HojeData())}.xlsx", xlsx.NomeArquivo);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false);
        var livro = documento.WorkbookPart!;
        Assert.Equal(new[] { "Conformidade", "Leia-me" }, livro.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value));
        var aba = ((WorksheetPart)livro.GetPartById(livro.Workbook.Sheets.Elements<Sheet>().First().Id!)).Worksheet!;
        var linhasXlsx = aba.Descendants<Row>().ToList();
        Assert.Equal(2, linhasXlsx.Count);
        Assert.Equal(new[] { "Secretaria de Estado de Saúde", "SES", "Básico", "1.0", "Publicado", "Sim", "Sim", "Sim", "Sim", "Sim", "Sim", "6", "6", "100", "Alta" },
            linhasXlsx[1].Elements<Cell>().Take(15).Select(Texto));
        var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016).Validate(documento);
        Assert.Empty(erros);

        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(() => Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "pdf", sgdi)));
    }
}
