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
/// Planilhas (E3): CSV (BOM, ponto e vírgula, proteção contra fórmula, números com vírgula,
/// datas dd/mm/aaaa, ligações pelos códigos) e XLSX conferido lendo o pacote com o próprio
/// Open XML SDK (abas, cabeçalho destacado e fixo, filtro, números e datas com formato,
/// validação das listas e a aba Leia-me).
/// </summary>
public class PePlanilhaTest : PeReferenciaisTestBase
{
    private static string TextoCsv(PePlanilhaArquivo planilha)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, planilha.Conteudo[..3]);
        return Encoding.UTF8.GetString(planilha.Conteudo[3..]);
    }

    private static string Texto(WorkbookPart livro, Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    private static Worksheet Aba(WorkbookPart livro, string nome)
    {
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == nome);
        return ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
    }

    private static Cell Celula(Worksheet aba, string referencia) =>
        aba.Descendants<Cell>().Single(c => c.CellReference == referencia);

    [Fact]
    public async Task Csv_ComBom_PontoEVirgula_CodigosDasLigacoes_EProtecaoContraFormula()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        var oe = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "=HYPERLINK(\"x\")", descricao = "Linha 1; linha 2" });
        await IncluirAsync(rascunho.Id, "petic_indicador",
            new { nome = "Serviços digitais", linha_base = 10.5, meta = 1234.5, prazo = "2027-12-31", unidade = "%", fonte = "-1" },
            new { objetivo = new[] { oe.Id } });

        var indicadores = await Planilhas.PeticSecaoAsync(rascunho.Id, "petic_indicador", "csv", ctx);

        Assert.Equal("text/csv; charset=utf-8", indicadores.TipoMime);
        Assert.Matches(@"^PETIC-DF_1\.0_petic_indicador_\d{4}-\d{2}-\d{2}\.csv$", indicadores.NomeArquivo);
        var linhas = TextoCsv(indicadores).Split("\r\n");
        Assert.Equal("Código;Indicador;Objetivo estratégico;Fórmula de cálculo;Unidade de medida;Linha de base;Meta;Prazo da meta;Fonte dos dados",
            linhas[0]);
        Assert.Equal("IE01;Serviços digitais;OE01;;%;10,5;1234,5;31/12/2027;'-1", linhas[1]);
        Assert.Equal(string.Empty, linhas[2]);

        var objetivos = TextoCsv(await Planilhas.PeticSecaoAsync(rascunho.Id, "petic_objetivo", "csv", ctx));
        Assert.Contains("OE01;\"'=HYPERLINK(\"\"x\"\")\";;\"Linha 1; linha 2\"\r\n", objetivos);
    }

    [Fact]
    public async Task Csv_DosPrincipios_OsOnze_EOAtalho()
    {
        var planilha = await Planilhas.DfSecaoAsync("principio", "csv", await ContextoDe(UserConsultaSes));

        Assert.Matches(@"^DF_principio_\d{4}-\d{2}-\d{2}\.csv$", planilha.NomeArquivo);
        var linhas = TextoCsv(planilha).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Código;Princípio ou diretriz;Fundamento;Pode ser critério de priorização?", linhas[0]);
        Assert.Equal(12, linhas.Length);
        // O texto literal do inciso termina em ";": a célula vai entre aspas
        Assert.StartsWith("PR01;\"Eficiência e economicidade", linhas[1]);
        Assert.Contains("compartilhadas;\";art. 4º", linhas[1]);
        Assert.EndsWith(";art. 4º, I, do Decreto nº 48.900/2026;Sim", linhas[1]);
    }

    [Fact]
    public async Task Xlsx_AbreNoOpenXml_ComCabecalhoFixoFiltroNumeroValidacaoELeiaMe()
    {
        var ctx = await Admin();
        await Registros.CriarAsync(PeDono.Df, "diretriz_ciclo",
            Salvar(new { ano = 2027, texto = "Priorizar a nuvem do CeTIC-DF.", situacao_deliberacao = "proposta" }), ctx);
        await Registros.CriarAsync(PeDono.Df, "diretriz_ciclo",
            Salvar(new { ano = 2027, texto = "Integrar os dados.", situacao_deliberacao = "aprovada", observacao = "Aprovada em setembro." }), ctx);

        var planilha = await Planilhas.DfSecaoAsync("diretriz_ciclo", null, ctx);

        Assert.Equal(PePlanilhaService.MimeXlsx, planilha.TipoMime);
        Assert.Matches(@"^DF_diretriz_ciclo_\d{4}-\d{2}-\d{2}\.xlsx$", planilha.NomeArquivo);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var abas = livro.Workbook!.Sheets!.Elements<Sheet>().ToList();
        Assert.Equal(new[] { "Diretrizes do ciclo", "Leia-me", "Listas" }, abas.Select(a => a.Name!.Value));
        Assert.Equal(SheetStateValues.Hidden, abas[2].State!.Value);

        var dados = Aba(livro, "Diretrizes do ciclo");
        // Cabeçalho destacado (negrito com fundo) e fixo (painel congelado na linha 1)
        Assert.Equal(new[] { "Código", "Ano do ciclo", "Diretriz ou prioridade", "Situação da deliberação", "Observação" },
            dados.Descendants<Row>().First().Elements<Cell>().Select(c => Texto(livro, c)));
        var estilos = livro.WorkbookStylesPart!.Stylesheet!;
        var formatoCabecalho = estilos.CellFormats!.Elements<CellFormat>().ElementAt((int)Celula(dados, "A1").StyleIndex!.Value);
        Assert.NotNull(estilos.Fonts!.Elements<Font>().ElementAt((int)formatoCabecalho.FontId!.Value).Bold);
        Assert.Equal(PatternValues.Solid, estilos.Fills!.Elements<Fill>().ElementAt((int)formatoCabecalho.FillId!.Value).PatternFill!.PatternType!.Value);
        var painel = dados.Descendants<Pane>().Single();
        Assert.Equal(1D, painel.VerticalSplit!.Value);
        Assert.Equal(PaneStateValues.Frozen, painel.State!.Value);
        Assert.Equal("A2", painel.TopLeftCell!.Value);

        // Filtro no cabeçalho, sobre todas as linhas
        Assert.Equal("A1:E3", dados.Descendants<AutoFilter>().Single().Reference!.Value);
        // Número como número, com formato
        var ano = Celula(dados, "B2");
        Assert.Null(ano.DataType);
        Assert.Equal("2027", ano.CellValue!.Text);
        var formatoAno = estilos.CellFormats!.Elements<CellFormat>().ElementAt((int)ano.StyleIndex!.Value);
        Assert.Equal("#,##0", estilos.NumberingFormats!.Elements<NumberingFormat>().Single(f => f.NumberFormatId!.Value == formatoAno.NumberFormatId!.Value).FormatCode!.Value);
        Assert.Equal("Proposta pela SGDI", Texto(livro, Celula(dados, "D2")));
        Assert.Equal("DC02", Texto(livro, Celula(dados, "A3")));
        // Larguras
        Assert.Equal(5, dados.Descendants<Column>().Count());

        // Lista com validação, apontando para a aba oculta
        var validacao = dados.Descendants<DataValidation>().Single();
        Assert.Equal(DataValidationValues.List, validacao.Type!.Value);
        Assert.Equal("D2:D10000", validacao.SequenceOfReferences!.InnerText);
        Assert.Equal("'Listas'!$A$2:$A$5", validacao.Formula1!.Text);
        var listas = Aba(livro, "Listas");
        Assert.Equal(new[] { "Diretrizes do ciclo · Situação da deliberação", "Proposta pela SGDI", "Em deliberação", "Aprovada", "Devolvida para ajuste" },
            listas.Descendants<Cell>().Where(c => c.CellReference!.Value!.StartsWith("A")).Select(c => Texto(livro, c)));

        // Leia-me: o dono, a data da extração e a ajuda de cada coluna
        var leiaMe = Aba(livro, "Leia-me").Descendants<Cell>().Select(c => Texto(livro, c)).ToList();
        Assert.Contains("Catálogo do DF (princípios e diretrizes do ciclo)", leiaMe);
        Assert.Contains("Extraído em", leiaMe);
        Assert.Contains("O ano de planejamento a que a diretriz vale.", leiaMe);
        Assert.Contains("Código do registro, dado pelo sistema. Não se repete, nem depois de apagado.", leiaMe);
    }

    [Fact]
    public async Task Xlsx_DataComoNumeroComFormato_ELigacaoPeloCodigo()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        var oe = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Objetivo" });
        await IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "Indicador", meta = 80.5, prazo = "2027-12-31" },
            new { objetivo = new[] { oe.Id } });

        var planilha = await Planilhas.PeticSecaoAsync(rascunho.Id, "petic_indicador", "XLSX", ctx);

        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var dados = Aba(livro, "Indicadores estratégicos");
        var estilos = livro.WorkbookStylesPart!.Stylesheet!;
        string Formato(Cell c)
        {
            var id = estilos.CellFormats!.Elements<CellFormat>().ElementAt((int)c.StyleIndex!.Value).NumberFormatId!.Value;
            return estilos.NumberingFormats!.Elements<NumberingFormat>().Single(f => f.NumberFormatId!.Value == id).FormatCode!.Value!;
        }

        var prazo = Celula(dados, "H2");
        Assert.Equal(new DateTime(2027, 12, 31).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture), prazo.CellValue!.Text);
        Assert.Equal("dd/mm/yyyy", Formato(prazo));
        Assert.Equal("80.5", Celula(dados, "G2").CellValue!.Text);
        Assert.Equal("OE01", Texto(livro, Celula(dados, "C2")));
        // Sem lista, sem aba Listas
        Assert.DoesNotContain(livro.Workbook!.Sheets!.Elements<Sheet>(), s => s.Name == "Listas");
    }

    [Fact]
    public async Task Completa_UmaAbaPorSecaoVisivelENaPlanilha_SoXlsx()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);
        await Modelo.DefinirSituacaoSecaoAsync(Secao("petic_eixo").Id, new PeSituacoesDTO { SituacaoGeral = "desligado" }, EmailAdmin);
        await Modelo.AtualizarSecaoAsync(Secao("petic_prioridade").Id,
            new PeSecaoAtualizarDTO { NaPlanilha = false, Informados = new HashSet<string> { "NaPlanilha" } }, EmailAdmin);

        var planilha = await Planilhas.PeticCompletaAsync(rascunho.Id, null, ctx);

        Assert.Matches(@"^PETIC-DF_1\.0_completa_\d{4}-\d{2}-\d{2}\.xlsx$", planilha.NomeArquivo);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        Assert.Equal(new[] { "Missão, visão e valores", "Diretrizes", "Objetivos do programa", "Objetivos estratégicos",
                "Indicadores estratégicos", "Iniciativas", "Leia-me" },
            livro.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value));
        // O formulário não tem a coluna de código
        Assert.Equal("Missão", Texto(livro, Celula(Aba(livro, "Missão, visão e valores"), "A1")));
        var leiaMe = Aba(livro, "Leia-me").Descendants<Cell>().Select(c => Texto(livro, c)).ToList();
        Assert.Contains("1.0 (rascunho)", leiaMe);
        Assert.Contains("PETIC-DF 2027-2030", leiaMe);
        Assert.Contains("01/01/2027 a 31/12/2030", leiaMe);

        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(() => Planilhas.PeticCompletaAsync(rascunho.Id, "csv", ctx)));
        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(() => Planilhas.PeticSecaoAsync(rascunho.Id, "petic_objetivo", "pdf", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Planilhas.PeticSecaoAsync(rascunho.Id, "petic_prioridade", "csv", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Planilhas.PeticSecaoAsync(rascunho.Id, "petic_eixo", "csv", ctx)));
        Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado), await ErroAsync(() => Planilhas.PeticCompletaAsync(999, "xlsx", ctx)));
    }

    [Fact]
    public async Task Xlsx_SemErroNoValidadorDoOpenXml()
    {
        var ctx = await Admin();
        await Registros.CriarAsync(PeDono.Df, "diretriz_ciclo",
            Salvar(new { ano = 2027, texto = "Linha com\u0001controle e \"aspas\"", situacao_deliberacao = "proposta" }), ctx);
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);

        foreach (var planilha in new[]
                 {
                     await Planilhas.DfSecaoAsync("diretriz_ciclo", "xlsx", ctx),
                     await Planilhas.DfSecaoAsync("principio", "xlsx", ctx),
                     await Planilhas.PeticCompletaAsync(rascunho.Id, "xlsx", ctx)
                 })
        {
            using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
            var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016)
                .Validate(documento).ToList();
            Assert.True(erros.Count == 0, planilha.NomeArquivo + ": " + string.Join(" | ", erros.Select(e => $"{e.Path?.XPath} {e.Description}")));
        }
    }

    [Fact]
    public void NomesDeAba_ValidosNoExcel_SemRepetir()
    {
        var nomes = PeXlsx.NomesUnicos(new[] { "Objetivos: estratégicos [DF]", "objetivos estratégicos df", "Objetivos estratégicos DF",
            new string('a', 40), "'Leia-me'", "" });

        Assert.Equal("Objetivos estratégicos DF", nomes[0]);
        Assert.Equal("objetivos estratégicos df (2)", nomes[1]);
        Assert.Equal("Objetivos estratégicos DF (3)", nomes[2]);
        Assert.Equal(31, nomes[3].Length);
        Assert.Equal("Leia-me", nomes[4]);
        Assert.Equal("Aba", nomes[5]);
        Assert.Equal("AA", PeXlsx.Letra(26));
        Assert.Equal("Z", PeXlsx.Letra(25));
    }
}
