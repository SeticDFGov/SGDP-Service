using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, segundo pedido do orquestrador depois da entrega: a aba Leia-me das planilhas do PDTIC de um
/// órgão (por seção, completa e do passo). No modo livre, a linha do nível passa a "Nível
/// alcançado", com o nível que aquele PDTIC alcançou (a régua da conformidade, lendo a situação só
/// dele), "-" sem nível alcançado e o motivo no PDTIC registrado fora do sistema. No modo definido,
/// fica "Nível de maturidade" com o nível de hoje do órgão, como sempre.
/// </summary>
public class PeF3PlanilhaDoOrgaoTest : PePaineisTestBase
{
    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    /// <summary>Os itens da Leia-me (rótulo na coluna A, valor na B), na ordem.</summary>
    private static List<(string Rotulo, string Valor)> ItensDaLeiaMe(PePlanilhaArquivo planilha)
    {
        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == "Leia-me");
        var folha = ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
        return folha.Descendants<Row>()
            .Select(r => r.Elements<Cell>().Select(Texto).ToList())
            .Where(c => c.Count == 2)
            .Select(c => (c[0], c[1]))
            .ToList();
    }

    /// <summary>As três planilhas do PDTIC que têm a Leia-me: a da seção, a completa e a do passo.</summary>
    private async Task<List<PePlanilhaArquivo>> PlanilhasAsync(long pdticId, string secao, string passo, PeUserContext ctx) => new()
    {
        await Planilhas.PdticSecaoAsync(pdticId, secao, "xlsx", ctx),
        await Planilhas.PdticCompletaAsync(pdticId, null, ctx),
        await Planilhas.PdticPassoAsync(pdticId, passo, "xlsx", null, ctx)
    };

    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(d => d.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    [Fact]
    public async Task Livre_ONivelAlcancadoDestePdtic_NaSecaoNaCompletaENoPasso()
    {
        Livre();
        var ses = await ProntoParaEnviarAsync();
        var seec = await AbrirSeecAsync();

        // A SES alcançou o Básico; a SEEC, com o PDTIC novo, nenhum nível ("-")
        foreach (var planilha in await PlanilhasAsync(ses.Id, "necessidades", "diagnostico.necessidades-tic", await Sgdi()))
        {
            var itens = ItensDaLeiaMe(planilha);
            Assert.Contains(("Nível alcançado", "Básico"), itens);
            Assert.DoesNotContain(itens, i => i.Rotulo == "Nível de maturidade");
            // A linha fica no lugar da antiga, depois do PDTIC
            Assert.Equal("PDTIC", itens[itens.FindIndex(i => i.Rotulo == "Nível alcançado") - 1].Rotulo);
        }
        foreach (var planilha in await PlanilhasAsync(seec.Id, "abrangencia", "preparacao.abrangencia", await ContextoDe(UserOrgaoSeec)))
            Assert.Contains(("Nível alcançado", "-"), ItensDaLeiaMe(planilha));

        // O CSV não tem a Leia-me: sai como sempre, também no modo livre
        var csv = await Planilhas.PdticSecaoAsync(ses.Id, "necessidades", "csv", await Sgdi());
        Assert.Equal("text/csv; charset=utf-8", csv.TipoMime);
        Assert.EndsWith(".csv", csv.NomeArquivo);
        var passoCsv = await Planilhas.PdticPassoAsync(ses.Id, "diagnostico.necessidades-tic", "csv", null, await Sgdi());
        Assert.NotEmpty(passoCsv.Conteudo);

        // O mesmo nível da situação do PDTIC (a régua da conformidade)
        Assert.Equal("Básico", (await SituacaoAsync(ses.Id)).Nivel.AlcancadoNome);
        Assert.Null((await SituacaoAsync(seec.Id, UserPeSgdi)).Nivel.AlcancadoNome);
    }

    [Fact]
    public async Task Livre_ORegistradoForaDoSistema_ComOMotivo()
    {
        Livre();
        var sedf = NovoOrgao("SEDF", "Secretaria de Estado de Educação");
        var arquivo = await EnviarArquivoAsync(UserAdminGeral, "pdtic.pdf", PdfDeUmaPagina());
        var externo = await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            OrgaoId = sedf.Id,
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

        foreach (var planilha in await PlanilhasAsync(externo.Id, "abrangencia", "preparacao.abrangencia", await AdminGeral()))
            Assert.Contains(("Nível alcançado", PeNivelAlcancado.MotivoExterno), ItensDaLeiaMe(planilha));
    }

    [Fact]
    public async Task Definido_ALinhaDeSempre_ONivelDeHojeDoOrgao()
    {
        var ses = await ProntoParaEnviarAsync();

        foreach (var planilha in await PlanilhasAsync(ses.Id, "necessidades", "diagnostico.necessidades-tic", await Orgao()))
        {
            var itens = ItensDaLeiaMe(planilha);
            Assert.Contains(("Nível de maturidade", "Básico"), itens);
            Assert.DoesNotContain(itens, i => i.Rotulo == "Nível alcançado");
        }

        // O nível escolhido para o órgão (não o alcançado): no Intermediário, "Intermediário"
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        Assert.Contains(("Nível de maturidade", "Intermediário"),
            ItensDaLeiaMe(await Planilhas.PdticSecaoAsync(ses.Id, "abrangencia", "xlsx", await Orgao())));
    }
}
