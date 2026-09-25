using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As planilhas das seções por ciclo (E7, rodada B): os registros de todos os ciclos, na ordem
/// dos ciclos, com a coluna Ciclo antes das outras (no CSV, no XLSX e no consolidado de todos os
/// órgãos).
/// </summary>
public class PePlanilhaAcompanhamentoTest : PeAcompanhamentoTestBase
{
    private static string[] LinhasDoCsv(PePlanilhaArquivo planilha) =>
        Encoding.UTF8.GetString(planilha.Conteudo[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    [Fact]
    public async Task SecaoPorCiclo_AColunaDoCiclo_NaOrdemDosCiclos()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        // O 2º trimestre gravado antes do 1º: a planilha sai na ordem dos ciclos
        await GravarAcoesAsync(t2.Id, Linha(IdDe(id, "A01"), "concluida", 100));
        await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "em_andamento", 30), Linha(IdDe(id, "A02"), "nao_iniciada", 0));

        var linhas = LinhasDoCsv(await Planilhas.PdticSecaoAsync(id, "monitoramento_acoes", "csv", await Orgao()));

        Assert.StartsWith("Ciclo;Ação;Situação", linhas[0]);
        Assert.Equal(new[] { Trimestre1, Trimestre1, Trimestre2 }, linhas.Skip(1).Select(l => l.Split(';')[0]));
        Assert.StartsWith($"{Trimestre1};A01;Em andamento;30", linhas[1]);

        // No XLSX, a mesma coluna
        var xlsx = await Planilhas.PdticSecaoAsync(id, "monitoramento_acoes", "xlsx", await Orgao());
        using var pacote = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false);
        var livro = pacote.WorkbookPart!;
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == "Situação das ações no ciclo");
        var planilha = ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
        var cabecalho = planilha.Descendants<Row>().Single(r => r.RowIndex!.Value == 1).Elements<Cell>().Select(Texto).ToList();
        Assert.Equal("Ciclo", cabecalho[0]);
        var primeira = planilha.Descendants<Row>().Single(r => r.RowIndex!.Value == 2).Elements<Cell>().Select(Texto).ToList();
        Assert.Equal(Trimestre1, primeira[0]);

        // O consolidado de todos os órgãos: a coluna do ciclo depois das do órgão
        var consolidado = LinhasDoCsv(await Planilhas.ConsolidadoSecaoAsync("monitoramento_acoes", "csv", await ContextoDe(UserPeSgdi)));
        Assert.StartsWith("Órgão;Sigla;Nível;Versão do PDTIC;Situação do PDTIC;Ciclo;Ação", consolidado[0]);
        Assert.Equal(3, consolidado.Length - 1);
        Assert.Contains(consolidado.Skip(1), l => l.Split(';')[5] == Trimestre2);
    }
}
