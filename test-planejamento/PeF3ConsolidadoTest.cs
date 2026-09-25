using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Models.Pgia;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, pedido do orquestrador depois da entrega: nas planilhas consolidadas (por seção e completa,
/// CSV e XLSX), no modo livre, a coluna "Nível" (que mostraria o nível base para todo órgão) vira
/// "Nível alcançado", com o nível que o PDTIC de cada linha alcançou, pela régua e pelo lote da
/// conformidade; vazia quando ele não alcançou nenhum nível ou foi registrado fora do sistema. A
/// aba Leia-me explica a coluna. No modo definido, a coluna fica como estava.
/// </summary>
public class PeF3ConsolidadoTest : PePaineisTestBase
{
    private const string AjudaDoNivelAlcancado =
        "O nível que o PDTIC da linha alcançou, pela régua dos níveis (a mesma da conformidade). Fica vazio quando o PDTIC "
        + "ainda não completa o primeiro nível ou foi aprovado fora do sistema.";

    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    private static List<string> LinhasDoCsv(PePlanilhaArquivo planilha)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, planilha.Conteudo[..3]);
        return Encoding.UTF8.GetString(planilha.Conteudo[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    private static Worksheet Aba(WorkbookPart livro, string nome)
    {
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == nome);
        return ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
    }

    /// <summary>O texto da célula pela referência ("C2"); a célula vazia não é gravada no arquivo.</summary>
    private static string Celula(Worksheet aba, string referencia) =>
        aba.Descendants<Cell>().FirstOrDefault(c => c.CellReference?.Value == referencia) is { } celula ? Texto(celula) : string.Empty;

    /// <summary>A linha da planilha (a partir da 2) cuja coluna B (a sigla) é a do órgão.</summary>
    private static uint LinhaDoOrgao(Worksheet aba, string sigla) =>
        aba.Descendants<Row>().Single(r => r.RowIndex!.Value > 1 && Celula(aba, $"B{r.RowIndex.Value}") == sigla).RowIndex!.Value;

    private static void SemErros(SpreadsheetDocument documento)
    {
        var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016)
            .Validate(documento).ToList();
        Assert.True(erros.Count == 0, string.Join(" | ", erros.Take(5).Select(e => $"{e.Path?.XPath} {e.Description}")));
    }

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

    [Fact]
    public async Task Livre_PorSecaoECompleta_ONivelAlcancadoDoPdticDeCadaLinha_EALeiaMe()
    {
        Livre();
        await ProntoParaEnviarAsync();
        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(seec.Id, "necessidades", new { descricao = "Painel fiscal", tipo = "servico", prioridade_simples = "media" },
            user: UserOrgaoSeec);
        var sgdi = await Sgdi();

        // CSV: "Nível alcançado" no lugar de "Nível"; a SES alcançou o Básico, a SEEC nenhum nível (vazio)
        var linhas = LinhasDoCsv(await Planilhas.ConsolidadoSecaoAsync("necessidades", "csv", sgdi));
        Assert.StartsWith("Órgão;Sigla;Nível alcançado;Versão do PDTIC;Situação do PDTIC;Código;Necessidade;", linhas[0]);
        Assert.StartsWith("Secretaria de Estado de Economia;SEEC;;1.0;Em elaboração;N01;Painel fiscal;", linhas.Single(l => l.Contains(";SEEC;")));
        Assert.StartsWith("Secretaria de Estado de Saúde;SES;Básico;1.0;Em elaboração;N01;", linhas.Single(l => l.Contains(";SES;")));
        // O mesmo nível da conformidade (a régua e o lote dela)
        Assert.Equal(("Básico", null), ((await LinhaAsync(OrgaoSes)).NivelAlcancadoNome, (await LinhaAsync(OrgaoSeec)).NivelAlcancadoNome));

        // XLSX por seção: a coluna, as células (a vazia não é gravada) e a ajuda na Leia-me
        var xlsx = await Planilhas.ConsolidadoSecaoAsync("necessidades", "xlsx", sgdi);
        using (var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false))
        {
            var livro = documento.WorkbookPart!;
            var dados = Aba(livro, "Necessidades de TIC");
            Assert.Equal(("Nível alcançado", "Versão do PDTIC"), (Celula(dados, "C1"), Celula(dados, "D1")));
            Assert.Equal("Básico", Celula(dados, $"C{LinhaDoOrgao(dados, "SES")}"));
            Assert.Equal(string.Empty, Celula(dados, $"C{LinhaDoOrgao(dados, "SEEC")}"));
            var leiaMe = Aba(livro, "Leia-me").Descendants<Cell>().Select(Texto).ToList();
            Assert.Contains(AjudaDoNivelAlcancado, leiaMe);
            Assert.Contains(leiaMe, t => t.StartsWith("Cada linha é um registro do PDTIC de um órgão. As primeiras colunas dizem o órgão, "
                                                      + "o nível que o PDTIC alcançou (vazio quando ele ainda não completa o primeiro nível"));
            Assert.DoesNotContain(leiaMe, t => t.Contains("Nível de maturidade do órgão hoje"));
            SemErros(documento);
        }

        // A completa: a mesma coluna em cada aba
        var completa = await Planilhas.ConsolidadoCompletoAsync("xlsx", sgdi);
        using (var documento = SpreadsheetDocument.Open(new MemoryStream(completa.Conteudo), false))
        {
            var livro = documento.WorkbookPart!;
            foreach (var nome in new[] { "Abrangência e vigência", "Necessidades de TIC" })
            {
                var aba = Aba(livro, nome);
                Assert.Equal("Nível alcançado", Celula(aba, "C1"));
                Assert.Equal("Básico", Celula(aba, $"C{LinhaDoOrgao(aba, "SES")}"));
            }
            Assert.Equal(string.Empty, Celula(Aba(livro, "Necessidades de TIC"), $"C{LinhaDoOrgao(Aba(livro, "Necessidades de TIC"), "SEEC")}"));
            SemErros(documento);
        }
    }

    [Fact]
    public async Task Livre_ORegistradoForaDoSistemaFicaVazio_ENoDefinidoAColunaDeSempre()
    {
        Livre();
        await ProntoParaEnviarAsync();
        var sedf = NovoOrgao("SEDF", "Secretaria de Estado de Educação");
        var externo = await RegistrarForaAsync(sedf);
        await IncluirNoPdticAsync(externo.Id, "metas",
            new { descricao = "Conectar as escolas.", indicador = "Escolas conectadas", valor = "100%", prazo = "2027-12-31" }, user: UserAdminGeral);
        var sgdi = await Sgdi();

        var metas = LinhasDoCsv(await Planilhas.ConsolidadoSecaoAsync("metas", "csv", sgdi));
        Assert.StartsWith("Órgão;Sigla;Nível alcançado;", metas[0]);
        Assert.StartsWith("Secretaria de Estado de Educação;SEDF;;1.0;Publicado;M01;Conectar as escolas.", metas.Single(l => l.Contains(";SEDF;")));
        Assert.StartsWith("Secretaria de Estado de Saúde;SES;Básico;", metas.Single(l => l.Contains(";SES;")));

        // No modo definido, a coluna é a de sempre: o nível de hoje de cada órgão (o padrão aqui)
        DefinirModoNiveis(PeDominios.ModoNiveis.Definido);
        var definido = LinhasDoCsv(await Planilhas.ConsolidadoSecaoAsync("metas", "csv", sgdi));
        Assert.StartsWith("Órgão;Sigla;Nível;Versão do PDTIC;", definido[0]);
        Assert.StartsWith("Secretaria de Estado de Educação;SEDF;Básico;1.0;Publicado;", definido.Single(l => l.Contains(";SEDF;")));
        Assert.StartsWith("Secretaria de Estado de Saúde;SES;Básico;", definido.Single(l => l.Contains(";SES;")));
        var xlsx = await Planilhas.ConsolidadoSecaoAsync("metas", "xlsx", sgdi);
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx.Conteudo), false);
        var leiaMe = Aba(documento.WorkbookPart!, "Leia-me").Descendants<Cell>().Select(Texto).ToList();
        Assert.Contains("Nível de maturidade do órgão hoje: diz que campos aparecem para ele.", leiaMe);
        Assert.Contains(leiaMe, t => t.Contains("As primeiras colunas dizem o órgão, o nível de maturidade dele,"));
        Assert.DoesNotContain(AjudaDoNivelAlcancado, leiaMe);
    }
}
