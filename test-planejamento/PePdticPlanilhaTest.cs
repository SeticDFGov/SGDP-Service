using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Planilhas do PDTIC (E4): por seção (CSV ou XLSX) e completa, com as colunas do nível do
/// órgão e o nome PDTIC_SIGLA_secao_data; e o consolidado de todos os PDTICs atuais (papéis
/// globais e admin geral), com as colunas do órgão antes dos campos e a união dos campos
/// visíveis em qualquer nível (célula vazia quando o campo não aparece para o órgão).
/// </summary>
public class PePdticPlanilhaTest : PePdticTestBase
{
    private static string TextoCsv(PePlanilhaArquivo planilha)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, planilha.Conteudo[..3]);
        return Encoding.UTF8.GetString(planilha.Conteudo[3..]);
    }

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    private static Worksheet Aba(WorkbookPart livro, string nome)
    {
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == nome);
        return ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
    }

    private static List<string> Linha(Worksheet aba, int numero) =>
        aba.Descendants<Row>().Single(r => r.RowIndex!.Value == numero).Elements<Cell>().Select(Texto).ToList();

    [Fact]
    public async Task PorSecao_ComAsColunasDoNivelDoOrgao_EONomeComASigla()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", new { descricao = "=Trocar a rede", tipo = "infraestrutura", prioridade_simples = "alta" });

        var basico = await Planilhas.PdticSecaoAsync(pdtic.Id, "necessidades", "csv", await ContextoDe(UserConsultaSes));
        Assert.Equal(PePlanilhaService.MimeCsv, basico.TipoMime);
        Assert.Matches(@"^PDTIC_SES_necessidades_\d{4}-\d{2}-\d{2}\.csv$", basico.NomeArquivo);
        var linhas = TextoCsv(basico).Split("\r\n");
        Assert.Equal("Código;Necessidade;Tipo;Objetivo do PETIC-DF;Prioridade", linhas[0]);
        Assert.Equal("N01;'=Trocar a rede;Infraestrutura de TIC;;Alta", linhas[1]);

        // No Intermediário a seção ganha as colunas do nível
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var intermediario = TextoCsv(await Planilhas.PdticSecaoAsync(pdtic.Id, "necessidades", "csv", await Orgao())).Split("\r\n")[0];
        Assert.StartsWith("Código;Necessidade;Tipo;Origem;Áreas que pedem;", intermediario);
        Assert.Contains(";Gravidade;Urgência;Tendência;Prioridade calculada;Priorizada?", intermediario);
        Assert.DoesNotContain(";Prioridade;", intermediario + ";");

        // Seção fora do nível, formato errado e outro órgão
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(async () =>
            await Planilhas.PdticSecaoAsync(pdtic.Id, "swot_fraquezas", "csv", await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(async () =>
            await Planilhas.PdticSecaoAsync(pdtic.Id, "necessidades", "pdf", await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
            await Planilhas.PdticSecaoAsync(pdtic.Id, "necessidades", "csv", await ContextoDe(UserOrgaoSeec))));
    }

    [Fact]
    public async Task Completa_UmaAbaPorSecaoDoNivel_ComLeiaMeDoOrgao_SoXlsx()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "abrangencia", new
        {
            tipo_abrangencia = "todo_com_vinculadas", vigencia_inicio = "2027-01-01", vigencia_fim = "2030-12-31", periodicidade_revisao = "anual"
        });

        var planilha = await Planilhas.PdticCompletaAsync(pdtic.Id, null, await ContextoDe(UserPeSgdi));
        Assert.Matches(@"^PDTIC_SES_completa_\d{4}-\d{2}-\d{2}\.xlsx$", planilha.NomeArquivo);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var abas = livro.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value).ToList();
        Assert.Equal("Abrangência e vigência", abas[0]);
        Assert.Contains("Necessidades de TIC", abas);
        Assert.Contains("Leia-me", abas);
        Assert.DoesNotContain("Fraquezas", abas);

        var leiaMe = Aba(livro, "Leia-me").Descendants<Cell>().Select(Texto).ToList();
        Assert.Contains("SES · Secretaria de Estado de Saúde", leiaMe);
        Assert.Contains("Básico", leiaMe);
        Assert.Contains("01/01/2027 a 31/12/2030", leiaMe);
        Assert.Contains(leiaMe, t => t.Contains("versão 1.0 (em elaboração)"));

        var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016)
            .Validate(documento).ToList();
        Assert.True(erros.Count == 0, string.Join(" | ", erros.Select(e => $"{e.Path?.XPath} {e.Description}")));

        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(async () => await Planilhas.PdticCompletaAsync(pdtic.Id, "csv", await Orgao())));
    }

    [Fact]
    public async Task Consolidado_ColunasDoOrgaoAntes_EUniaoDosCamposDosNiveis()
    {
        var ses = await AbrirSesAsync();
        await DefinirNivelDoOrgaoAsync(OrgaoSeec, "avancado");
        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(ses.Id, "necessidades", new { descricao = "Trocar a rede", tipo = "infraestrutura", prioridade_simples = "alta" });
        await IncluirNoPdticAsync(seec.Id, "necessidades", new
        {
            descricao = "Painel fiscal", tipo = "servico", origem = "informacao", areas = "Receita", gravidade = 4, urgencia = 3, tendencia = 2, priorizada = false
        }, user: UserOrgaoSeec);

        var csv = await Planilhas.ConsolidadoSecaoAsync("necessidades", "csv", await ContextoDe(UserPeSgdi));
        Assert.Matches(@"^PDTIC_consolidado_necessidades_\d{4}-\d{2}-\d{2}\.csv$", csv.NomeArquivo);
        var linhas = TextoCsv(csv).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var cabecalho = linhas[0].Split(';').ToList();
        Assert.Equal(new[] { "Órgão", "Sigla", "Nível", "Versão do PDTIC", "Situação do PDTIC", "Código", "Necessidade", "Tipo", "Origem" },
            cabecalho.Take(9));
        // A união: a prioridade do Básico e os critérios do Intermediário e do Avançado
        Assert.Contains("Prioridade", cabecalho);
        Assert.Contains("Gravidade", cabecalho);
        Assert.Equal(3, linhas.Length);

        // Por sigla: SEEC antes de SES; célula vazia no campo que não aparece para o órgão
        var daSeec = linhas[1].Split(';');
        var daSes = linhas[2].Split(';');
        Assert.Equal(new[] { "Secretaria de Estado de Economia", "SEEC", "Avançado", "1.0", "Em elaboração", "N01", "Painel fiscal" }, daSeec.Take(7));
        Assert.Equal(new[] { "Secretaria de Estado de Saúde", "SES", "Básico", "1.0", "Em elaboração", "N01", "Trocar a rede" }, daSes.Take(7));
        var gravidade = cabecalho.IndexOf("Gravidade");
        var prioridade = cabecalho.IndexOf("Prioridade");
        Assert.Equal("4", daSeec[gravidade]);
        Assert.Equal(string.Empty, daSeec[prioridade]);
        Assert.Equal(string.Empty, daSes[gravidade]);
        Assert.Equal("Alta", daSes[prioridade]);

        // XLSX: as mesmas colunas, com números como número
        var xlsx = await Planilhas.ConsolidadoSecaoAsync("necessidades", null, await ContextoDe(UserAdminGeral));
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var dados = Aba(livro, "Necessidades de TIC");
        Assert.Equal(cabecalho, Linha(dados, 1));
        Assert.Equal("SEEC", Linha(dados, 2)[1]);
        var leiaMe = Aba(livro, "Leia-me").Descendants<Cell>().Select(Texto).ToList();
        Assert.Contains("2 órgãos com PDTIC atual", leiaMe);
    }

    [Fact]
    public async Task Consolidado_SoOsAtuais_EOrgaoQueNaoVeASecaoFicaDeFora()
    {
        var ses = await AbrirSesAsync();
        await DefinirNivelDoOrgaoAsync(OrgaoSeec, "intermediario");
        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(seec.Id, "swot_fraquezas", new { descricao = "Rede antiga" }, user: UserOrgaoSeec);

        // A SWOT não aparece no Básico da SES: só a SEEC entra
        var fraquezas = TextoCsv(await Planilhas.ConsolidadoSecaoAsync("swot_fraquezas", "csv", await ContextoDe(UserPeCgtic)))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, fraquezas.Length);
        Assert.StartsWith("Secretaria de Estado de Economia;SEEC;Intermediário;1.0;Em elaboração;D01;Rede antiga", fraquezas[1]);

        // PDTIC encerrado sai do consolidado
        var p = Context.PePdtics.Single(x => x.Id == seec.Id);
        p.Situacao = PeDominios.SituacaoPdtic.Encerrado;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        Assert.Single(TextoCsv(await Planilhas.ConsolidadoSecaoAsync("swot_fraquezas", "csv", await ContextoDe(UserPeCgtic)))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
        _ = ses;
    }

    [Fact]
    public async Task Consolidado_SoPapeisGlobais_SecaoInexistente_ECompletoSoXlsx()
    {
        await AbrirSesAsync();

        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Planilhas.ConsolidadoSecaoAsync("necessidades", "csv", await ContextoDe(user))));
        var sgdi = await ContextoDe(UserPeSgdi);
        foreach (var chave in new[] { "nao_existe", "petic_objetivo", "principio" })
            Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Planilhas.ConsolidadoSecaoAsync(chave, "csv", sgdi)));
        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(() => Planilhas.ConsolidadoCompletoAsync("csv", sgdi)));

        var completo = await Planilhas.ConsolidadoCompletoAsync("xlsx", sgdi);
        Assert.Matches(@"^PDTIC_consolidado_completa_\d{4}-\d{2}-\d{2}\.xlsx$", completo.NomeArquivo);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(completo.Conteudo), false);
        var abas = documento.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value).ToList();
        Assert.Equal("Abrangência e vigência", abas[0]);
        // Seções de qualquer nível (a SWOT existe no Intermediário e no Avançado)
        Assert.Contains("Fraquezas", abas);
        Assert.Contains("Leia-me", abas);
        var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016)
            .Validate(documento).ToList();
        Assert.True(erros.Count == 0, string.Join(" | ", erros.Take(5).Select(e => $"{e.Path?.XPath} {e.Description}")));
    }
}
