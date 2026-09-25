using System.Reflection;
using System.Text;
using api.Planejamento;
using Controllers.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2 (segunda leva), painel da E8 e conformidade: cada número do painel bate com a lista para
/// onde ele leva, com a mesma regra nas duas pontas (a situação de referência do órgão, as
/// marcas dos alertas na linha da conformidade e os filtros de situação e de alerta no
/// servidor); a fila do CGTIC inteira quando o painel não tem filtro; os PDTICs encerrados na
/// lista quando o filtro pede; e a planilha da conformidade com os filtros da tela.
/// </summary>
public class PeF2PainelTest : PePaineisTestBase
{
    /// <summary>Muda a vigência do PDTIC à mão.</summary>
    private void Vigencia(long pdticId, DateOnly inicio, DateOnly fim)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        pdtic.VigenciaInicio = inicio;
        pdtic.VigenciaFim = fim;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Um órgão em cada caso difícil: SES publicada com o ato de 14 meses atrás (revisão vencida);
    /// SEEC em elaboração, sem aprovação; DETRAN sem PDTIC; CAESB só com o PDTIC encerrado; TCB
    /// publicado com a vigência terminada; METRO publicado com a vigência que ainda não começou.
    /// </summary>
    private async Task<Dictionary<string, long>> CenarioAsync()
    {
        var ids = new Dictionary<string, long> { ["SES"] = await PublicadoHojeAsync() };
        DataDoAto(ids["SES"], HojeData().AddMonths(-14));
        ids["SEEC"] = (await AbrirSeecAsync()).Id;
        NovoOrgao("DETRAN", "Departamento de Trânsito");
        ids["CAESB"] = await AbrirPdticAsync(NovoOrgao("CAESB", "Companhia de Saneamento Ambiental"));
        Situacao(ids["CAESB"], PeDominios.SituacaoPdtic.Encerrado);
        ids["TCB"] = await AbrirPdticAsync(NovoOrgao("TCB", "Transporte Urbano"));
        Situacao(ids["TCB"], PeDominios.SituacaoPdtic.Publicado);
        Vigencia(ids["TCB"], HojeData().AddYears(-4), HojeData().AddDays(-10));
        ids["METRO"] = await AbrirPdticAsync(NovoOrgao("METRO", "Companhia do Metropolitano"));
        Situacao(ids["METRO"], PeDominios.SituacaoPdtic.Publicado);
        Vigencia(ids["METRO"], HojeData().AddDays(20), HojeData().AddYears(4));
        return ids;
    }

    private async Task<List<PeConformidadeOrgaoResponse>> ListaAsync(PeConformidadeConsulta consulta) =>
        (await Paineis.ConformidadeAsync(consulta, await Sgdi())).Orgaos;

    // ── Painel × conformidade ───────────────────────────────────────────────

    [Fact]
    public async Task CadaNumeroDoPainel_BateComAListaDaConformidade_ComOsMesmosFiltros()
    {
        await CenarioAsync();
        foreach (var nivel in new long?[] { null, NivelId("basico") })
        {
            var painel = await PainelAsync(nivelId: nivel);
            var todos = await ListaAsync(new PeConformidadeConsulta { NivelId = nivel });
            Assert.Equal(painel.TotalOrgaos, todos.Count);

            foreach (var s in painel.PorSituacao)
            {
                // O filtro do servidor e a regra da tela (a situação da linha, com "sem_pdtic" no nulo) dão a mesma lista
                var lista = await ListaAsync(new PeConformidadeConsulta { NivelId = nivel, Situacao = s.Chave });
                Assert.Equal(s.Quantidade, lista.Count);
                Assert.All(lista, l => Assert.Equal(s.Chave, l.PdticSituacao ?? "sem_pdtic"));
                Assert.Equal(s.Quantidade, todos.Count(l => (l.PdticSituacao ?? "sem_pdtic") == s.Chave));
            }

            foreach (var (alerta, numero, marca) in new (string, int, Func<PeConformidadeOrgaoResponse, bool>)[]
                     {
                         ("vigencia", painel.Alertas.VigenciaVencida, l => l.Alertas.VigenciaVencida),
                         ("revisao", painel.Alertas.RevisaoVencida, l => l.Alertas.RevisaoVencida),
                         ("ciclo", painel.Alertas.CicloAtrasado, l => l.Alertas.CicloAtrasado)
                     })
            {
                Assert.Equal(numero, (await ListaAsync(new PeConformidadeConsulta { NivelId = nivel, Alerta = alerta })).Count);
                Assert.Equal(numero, todos.Count(marca));
            }
        }

        // O cenário tem de fato cada caso
        var geral = await PainelAsync();
        Assert.Equal((1, 1, 1, 1), (Quantidade(geral, "sem_pdtic"), Quantidade(geral, "encerrado"), geral.Alertas.RevisaoVencida, geral.Alertas.VigenciaVencida));
    }

    [Fact]
    public async Task SemPdtic_EEncerrado_ComoNoPainel()
    {
        var ids = await CenarioAsync();

        Assert.Equal(new[] { "DETRAN" }, (await ListaAsync(new PeConformidadeConsulta { Situacao = "sem_pdtic" })).Select(o => o.Sigla));
        var caesb = Assert.Single(await ListaAsync(new PeConformidadeConsulta { Situacao = "encerrado" }));
        Assert.Equal(("CAESB", ids["CAESB"], "1.0", "encerrado"), (caesb.Sigla, caesb.PdticId!.Value, caesb.PdticVersao, caesb.PdticSituacao));
        // Os itens avaliam só a versão vigente ou a da elaboração: o encerrado não atende nada
        Assert.Equal((0, "baixa"), (caesb.Percentual, caesb.Grupo));
        Assert.EndsWith(" e o próximo ainda não foi aberto", caesb.Itens["aprovado_cgtic"].Detalhe);

        // Várias situações de uma vez (os números "PDTIC a caminho" e "PDTIC publicado" do painel)
        Assert.Equal(new[] { "SEEC" },
            (await ListaAsync(new PeConformidadeConsulta { Situacao = "em_elaboracao,devolvido,em_aprovacao,aprovado" })).Select(o => o.Sigla));
        Assert.Equal(new[] { "METRO", "SES", "TCB" },
            (await ListaAsync(new PeConformidadeConsulta { Situacao = "publicado, em_acompanhamento" })).Select(o => o.Sigla));
        // Situação desconhecida não acha nada (nunca "todos" em silêncio); a conhecida junto vale
        Assert.Empty(await ListaAsync(new PeConformidadeConsulta { Situacao = "arquivado" }));
        Assert.Equal(new[] { "DETRAN" }, (await ListaAsync(new PeConformidadeConsulta { Situacao = "arquivado,sem_pdtic" })).Select(o => o.Sigla));
    }

    [Fact]
    public async Task RevisaoEVigenciaVencidas_SoAsQueVenceram_PelasMarcasDaLinha()
    {
        await CenarioAsync();
        var linhas = await ListaAsync(new PeConformidadeConsulta());
        PeConformidadeOrgaoResponse Linha(string sigla) => linhas.Single(l => l.Sigla == sigla);

        // "Revisão em dia" também não atende sem aprovação (SEEC) e sem PDTIC (DETRAN), mas a revisão não venceu
        Assert.Equal((false, false), (Linha("SEEC").Itens["revisao_em_dia"].Atende, Linha("SEEC").Alertas.RevisaoVencida));
        Assert.Equal((false, false), (Linha("DETRAN").Itens["revisao_em_dia"].Atende, Linha("DETRAN").Alertas.RevisaoVencida));
        Assert.Equal((false, true), (Linha("SES").Itens["revisao_em_dia"].Atende, Linha("SES").Alertas.RevisaoVencida));
        Assert.Equal(new[] { "SES" }, (await ListaAsync(new PeConformidadeConsulta { Alerta = "revisao" })).Select(o => o.Sigla));

        // "PDTIC vigente" também não atende com a vigência que ainda não começou (METRO), mas ela não venceu
        Assert.Equal((false, false), (Linha("METRO").Itens["vigente"].Atende, Linha("METRO").Alertas.VigenciaVencida));
        Assert.Equal((false, true), (Linha("TCB").Itens["vigente"].Atende, Linha("TCB").Alertas.VigenciaVencida));
        Assert.Equal(new[] { "TCB" }, (await ListaAsync(new PeConformidadeConsulta { Alerta = "vigencia" })).Select(o => o.Sigla));

        // A notificação em aberto (notificada ou inadimplente) e o alerta desconhecido
        InadimplenciaNoBanco(OrgaoSeec, "notificado");
        Assert.Equal(new[] { "SEEC" }, (await ListaAsync(new PeConformidadeConsulta { Alerta = "inadimplencia" })).Select(o => o.Sigla));
        Assert.Empty(await ListaAsync(new PeConformidadeConsulta { Alerta = "outro" }));
    }

    [Fact]
    public async Task ResumoDosGrupos_ComOsOutrosFiltros_SemODoGrupo()
    {
        await CenarioAsync();
        var sgdi = await Sgdi();

        var publicados = await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Situacao = "publicado", Grupo = "media" }, sgdi);
        var semGrupo = await ListaAsync(new PeConformidadeConsulta { Situacao = "publicado" });
        Assert.Equal((semGrupo.Count(l => l.Grupo == "alta"), semGrupo.Count(l => l.Grupo == "media"), semGrupo.Count(l => l.Grupo == "baixa")),
            (publicados.Resumo.Alta, publicados.Resumo.Media, publicados.Resumo.Baixa));
        Assert.Equal(semGrupo.Where(l => l.Grupo == "media").Select(l => l.Sigla), publicados.Orgaos.Select(l => l.Sigla));
    }

    // ── As deliberações aguardando e a fila do CGTIC ────────────────────────

    [Fact]
    public async Task DeliberacoesAguardando_SemFiltro_AFilaInteira_ComFiltro_SoOsOrgaosFiltrados()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var petic = await RascunhoAsync(copiar: false);
        await PreencherMinimoAsync(petic.Id);
        await Petics.EnviarAsync(petic.Id, await Admin());

        var fila = await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Situacao = "aguardando" });
        Assert.Equal(2, fila.TotalItems);
        Assert.Equal(fila.TotalItems, (await PainelAsync()).Alertas.DeliberacoesAguardando);
        // Com filtro, só os PDTICs dos órgãos filtrados (o PETIC-DF não é de nenhum órgão)
        Assert.Equal(1, (await PainelAsync(situacao: "em_aprovacao")).Alertas.DeliberacoesAguardando);
        Assert.Equal(1, (await PainelAsync(nivelId: NivelId("basico"))).Alertas.DeliberacoesAguardando);
        Assert.Equal(0, (await PainelAsync(situacao: "sem_pdtic")).Alertas.DeliberacoesAguardando);
    }

    // ── Lista dos PDTICs ────────────────────────────────────────────────────

    [Fact]
    public async Task ListaDosPdtics_OFiltroTrazOsEncerradosEOsSubstituidos_SemFiltroSoOsAtuais()
    {
        var ids = await CenarioAsync();
        var trocado = await AbrirPdticAsync(NovoOrgao("CEB"));
        Situacao(trocado, PeDominios.SituacaoPdtic.Substituido);
        var sgdi = await Sgdi();

        var atuais = await Pdtics.ListarAsync(new PePdticConsulta { PageSize = 100 }, sgdi);
        Assert.DoesNotContain(atuais.Items, p => p.Situacao is "encerrado" or "substituido");
        Assert.Equal(new[] { "METRO", "SEEC", "SES", "TCB" }, atuais.Items.Select(p => p.OrgaoSigla));

        var encerrados = await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "encerrado" }, sgdi);
        Assert.Equal(1, encerrados.TotalItems);
        Assert.Equal((ids["CAESB"], "CAESB"), (encerrados.Items[0].Id, encerrados.Items[0].OrgaoSigla));
        var substituidos = await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "substituido" }, sgdi);
        Assert.Equal(trocado, Assert.Single(substituidos.Items).Id);
        Assert.Equal(0, (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "arquivado" }, sgdi)).TotalItems);
        // O filtro pelo órgão junto
        Assert.Equal(0, (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "encerrado", Filtro = "SES" }, sgdi)).TotalItems);
    }

    // ── A planilha da conformidade com os filtros da tela ───────────────────

    [Fact]
    public async Task Planilha_ComOsFiltrosDaTela_AMesmaListaDaTela()
    {
        await CenarioAsync();
        var sgdi = await Sgdi();
        var consultas = new[]
        {
            new PeConformidadeConsulta(),
            new PeConformidadeConsulta { Grupo = "baixa" },
            new PeConformidadeConsulta { NivelId = NivelId("basico") },
            new PeConformidadeConsulta { NivelId = NivelId("avancado") },
            // Sem caixa e sem acento, como a tela: "saude" acha "Secretaria de Estado de Saúde"
            new PeConformidadeConsulta { Filtro = "saude" },
            new PeConformidadeConsulta { Filtro = "  TRÂNSITO " },
            new PeConformidadeConsulta { Situacao = "encerrado,sem_pdtic" },
            new PeConformidadeConsulta { Alerta = "revisao" },
            new PeConformidadeConsulta { Grupo = "baixa", Situacao = "publicado", Filtro = "c" },
            new PeConformidadeConsulta { Grupo = "outro" }
        };
        foreach (var consulta in consultas)
        {
            var tela = (await Paineis.ConformidadeAsync(consulta, sgdi)).Orgaos.Select(o => o.Sigla).ToList();
            var csv = await Paineis.ConformidadePlanilhaAsync(consulta, "csv", sgdi);
            var linhas = Encoding.UTF8.GetString(csv.Conteudo[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            Assert.Equal(tela, linhas.Select(l => l.Split(';')[1]));
        }
        Assert.Equal(new[] { "SES" }, (await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Filtro = "saude" }, sgdi)).Orgaos.Select(o => o.Sigla));
        Assert.Equal(new[] { "DETRAN" }, (await Paineis.ConformidadeAsync(new PeConformidadeConsulta { Filtro = "  TRÂNSITO " }, sgdi)).Orgaos.Select(o => o.Sigla));

        // O XLSX sai com os mesmos órgãos e diz na aba Leia-me os filtros aplicados
        var xlsx = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta
        {
            NivelId = NivelId("basico"), Filtro = "saude", Situacao = "publicado,encerrado", Alerta = "revisao"
        }, "xlsx", sgdi);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false);
        var livro = documento.WorkbookPart!;
        Worksheet Aba(string nome) =>
            ((WorksheetPart)livro.GetPartById(livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == nome).Id!)).Worksheet!;
        static string Texto(Cell c) => c.DataType?.Value == CellValues.InlineString ? c.InlineString!.InnerText : c.CellValue?.Text ?? string.Empty;
        Assert.Equal(2, Aba("Conformidade").Descendants<Row>().Count());
        var leiaMe = Aba("Leia-me").Descendants<Cell>().Select(Texto).ToList();
        Assert.Contains("Nível: Básico · Busca: saude · Situação: Publicado, Encerrado · Alerta: Revisão do PDTIC vencida", leiaMe);
        var semFiltro = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "xlsx", sgdi);
        using var outro = SpreadsheetDocument.Open(new MemoryStream(semFiltro.Conteudo), false);
        var livroSemFiltro = outro.WorkbookPart!;
        var abaLeiaMe = (WorksheetPart)livroSemFiltro.GetPartById(livroSemFiltro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == "Leia-me").Id!);
        Assert.Contains("Nenhum: todos os órgãos ativos", abaLeiaMe.Worksheet!.Descendants<Cell>().Select(Texto));
    }

    /// <summary>
    /// A armadilha do model binding (a mesma da Supervisão Contínua): o parâmetro do modelo de
    /// query não pode ter o nome de uma propriedade dele, senão o ASP.NET passa a exigir
    /// "consulta.Filtro" e ignora os filtros sem erro. Vale para as rotas da conformidade e da planilha.
    /// </summary>
    [Fact]
    public void ModelosDeQueryDosPaineis_ParametroSemONomeDeUmaPropriedade()
    {
        var acoes = typeof(PePaineisController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var complexos = acoes
            .SelectMany(a => a.GetParameters())
            .Where(p => p.GetCustomAttribute<FromQueryAttribute>() != null && p.ParameterType.IsClass && p.ParameterType != typeof(string))
            .ToList();
        Assert.Contains(complexos, p => p.ParameterType == typeof(PeConformidadeConsulta) && p.Member.Name == nameof(PePaineisController.ConformidadePlanilha));
        foreach (var parametro in complexos)
        {
            var propriedades = parametro.ParameterType.GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain(propriedades, nome => string.Equals(nome, parametro.Name, StringComparison.OrdinalIgnoreCase));
            // Os parâmetros simples da mesma rota também não podem ter o nome de uma propriedade
            foreach (var simples in ((MethodInfo)parametro.Member).GetParameters().Where(p => p != parametro))
                Assert.DoesNotContain(propriedades, nome => string.Equals(nome, simples.Name, StringComparison.OrdinalIgnoreCase));
        }
        Assert.Equal(new[] { "Alerta", "Filtro", "Grupo", "NivelId", "Situacao" },
            typeof(PeConformidadeConsulta).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }
}
