using System.IO.Compression;
using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A planilha de um passo do PDTIC (pedido extra da E8): em XLSX, uma aba por seção visível do
/// passo (as desligadas no nível e as fora da planilha ficam de fora), na ordem, com as colunas de
/// sempre e a Leia-me com o passo (e o ciclo); em CSV, a planilha da seção quando o passo tem uma
/// só, ou um ZIP com um CSV por seção; o ciclo obrigatório no passo com seção por ciclo (e o filtro
/// por ele); o nome do arquivo; o passo sem seção e o fora da trilha; e quem lê.
/// </summary>
public class PePlanilhaDoPassoTest : PePaineisTestBase
{
    private static string TextoCsv(byte[] conteudo)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, conteudo[..3]);
        return Encoding.UTF8.GetString(conteudo[3..]);
    }

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    private static List<string> Abas(byte[] xlsx)
    {
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx), false);
        var erros = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016).Validate(documento);
        Assert.Empty(erros);
        return documento.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value!).ToList();
    }

    /// <summary>As linhas de uma aba (a primeira é o cabeçalho), com o texto de cada célula.</summary>
    private static List<List<string>> Linhas(byte[] xlsx, string aba)
    {
        using var documento = SpreadsheetDocument.Open(new MemoryStream(xlsx), false);
        var livro = documento.WorkbookPart!;
        var folha = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == aba);
        var planilha = ((WorksheetPart)livro.GetPartById(folha.Id!)).Worksheet!;
        return planilha.Descendants<Row>().Select(r => r.Elements<Cell>().Select(Texto).ToList()).ToList();
    }

    private static Dictionary<string, byte[]> Zip(byte[] conteudo)
    {
        using var zip = new ZipArchive(new MemoryStream(conteudo), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var entrada = e.Open();
            using var memoria = new MemoryStream();
            entrada.CopyTo(memoria);
            return memoria.ToArray();
        });
    }

    private async Task<PePlanilhaArquivo> PassoAsync(long pdticId, string passo, string? formato, long? cicloId = null, app.Models.User? user = null) =>
        await Planilhas.PdticPassoAsync(pdticId, passo, formato, cicloId, await ContextoDe(user ?? UserOrgaoSes));

    [Fact]
    public async Task UmaSecao_OCsvDaSecao_EOXlsxComALeiaMe()
    {
        var id = (await AbrirSesAsync()).Id;
        await PreencherAbrangenciaAsync(id);

        var csv = await PassoAsync(id, "preparacao.abrangencia", "csv");
        Assert.Equal((PePlanilhaService.MimeCsv, $"PDTIC_SES_passo-1.1_{Iso(HojeData())}.csv"), (csv.TipoMime, csv.NomeArquivo));
        var daSecao = await Planilhas.PdticSecaoAsync(id, "abrangencia", "csv", await Orgao());
        Assert.Equal(TextoCsv(daSecao.Conteudo), TextoCsv(csv.Conteudo));

        var xlsx = await PassoAsync(id, "preparacao.abrangencia", null, user: UserConsultaSes);
        Assert.Equal((PePlanilhaService.MimeXlsx, $"PDTIC_SES_passo-1.1_{Iso(HojeData())}.xlsx"), (xlsx.TipoMime, xlsx.NomeArquivo));
        // A aba oculta Listas guarda os valores das listas (validação de dados), como na planilha da seção
        Assert.Equal(new[] { "Abrangência e vigência", "Leia-me", "Listas" }, Abas(xlsx.Conteudo));
        var leiaMe = Linhas(xlsx.Conteudo, "Leia-me");
        Assert.Contains(leiaMe, l => l.Count >= 2 && l[0] == "Passo" && l[1] == "1.1 · Diga o que o PDTIC abrange e por quanto tempo vale");
        Assert.Contains(leiaMe, l => l.Count >= 2 && l[0] == "Órgão" && l[1] == "SES · Secretaria de Estado de Saúde");
        Assert.DoesNotContain(leiaMe, l => l.Count >= 1 && l[0] == "Ciclo");
        // A ajuda de cada coluna também vai para a Leia-me
        Assert.Contains(leiaMe, l => l.Any(c => c == "Início da vigência"));
    }

    [Fact]
    public async Task VariasSecoes_UmaAbaPorSecao_EOZipComUmCsvPorSecao()
    {
        var id = (await AbrirSesAsync()).Id;
        await IncluirNoPdticAsync(id, "sgtic", new { forma = "subcomite", ato_tipo = "portaria", ato_numero = "12/2026", ato_data = "2026-01-10" });
        await IncluirNoPdticAsync(id, "sgtic_membros", new { nome = "=Maria da Silva", papel = "presidente" });
        await IncluirNoPdticAsync(id, "sgtic_membros", new { nome = "João Souza", papel = "membro" });

        var xlsx = await PassoAsync(id, "preparacao.sgtic", "xlsx");
        var esperadas = PeXlsx.NomesUnicos(new[] { Secao("sgtic").Titulo, Secao("sgtic_membros").Titulo, PeXlsx.NomeLeiaMe, PeXlsx.NomeListas }).Take(3);
        Assert.Equal(esperadas, Abas(xlsx.Conteudo).Take(3));
        Assert.Equal(3, Linhas(xlsx.Conteudo, Abas(xlsx.Conteudo)[1]).Count);

        var zip = await PassoAsync(id, "preparacao.sgtic", "csv");
        Assert.Equal((PePlanilhaService.MimeZip, $"PDTIC_SES_passo-1.3_{Iso(HojeData())}.zip"), (zip.TipoMime, zip.NomeArquivo));
        var arquivos = Zip(zip.Conteudo);
        Assert.Equal(new[] { "1.3-sgtic.csv", "1.3-sgtic_membros.csv" }, arquivos.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var membros = TextoCsv(arquivos["1.3-sgtic_membros.csv"]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, membros.Length);
        // A proteção contra fórmula vale no CSV de dentro do ZIP
        Assert.StartsWith("'=Maria da Silva", membros[1]);
        Assert.Equal(TextoCsv((await Planilhas.PdticSecaoAsync(id, "sgtic", "csv", await Orgao())).Conteudo), TextoCsv(arquivos["1.3-sgtic.csv"]));
    }

    [Fact]
    public async Task SecaoDesligadaNoNivelOuForaDaPlanilha_FicaDeFora()
    {
        var id = (await AbrirSesAsync()).Id;
        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Ana Souza" });

        // No Básico, o ato de designação da equipe está desligado: sobra uma seção (o CSV dela)
        var basico = await PassoAsync(id, "preparacao.equipe", "csv");
        Assert.Equal(PePlanilhaService.MimeCsv, basico.TipoMime);
        Assert.StartsWith("Nome", TextoCsv(basico.Conteudo));
        Assert.Contains("\r\nAna Souza", TextoCsv(basico.Conteudo));

        // No Avançado, as duas
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var avancado = await PassoAsync(id, "preparacao.equipe", "csv");
        Assert.Equal(PePlanilhaService.MimeZip, avancado.TipoMime);
        Assert.Equal(new[] { "1.4-equipe_designacao.csv", "1.4-equipe_elaboracao.csv" }, Zip(avancado.Conteudo).Keys.OrderBy(k => k, StringComparer.Ordinal));

        // Seção que o administrador deixou fora da planilha também fica de fora
        var designacao = Context.PeSecoes.Single(s => s.Chave == "equipe_designacao");
        designacao.NaPlanilha = false;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        Assert.Equal(PePlanilhaService.MimeCsv, (await PassoAsync(id, "preparacao.equipe", "csv")).TipoMime);
        Assert.Equal(new[] { Secao("equipe_elaboracao").Titulo, PeXlsx.NomeLeiaMe }, Abas((await PassoAsync(id, "preparacao.equipe", "xlsx")).Conteudo).Take(2));
    }

    [Fact]
    public async Task PassoPorCiclo_OCicloObrigatorio_EASecaoSoComOCiclo()
    {
        var id = await AcompanhadoAsync();
        var (a1, a2) = (IdDe(id, "A01"), IdDe(id, "A02"));
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 30), Linha(a2, "nao_iniciada", 0));
        await GravarAcoesAsync(t2.Id, Linha(a1, "concluida", 100));
        const string passo = "monitoramento.ciclo-monitoramento";

        Assert.Equal(Codigo(ErrorCode.PeCicloObrigatorio), await ErroAsync(() => PassoAsync(id, passo, "xlsx")));
        var avaliacao = await AbrirAvaliacaoAsync(id);
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido), await ErroAsync(() => PassoAsync(id, passo, "xlsx", avaliacao.Id)));
        Assert.Equal(Codigo(ErrorCode.PeCicloNaoEncontrado), await ErroAsync(() => PassoAsync(id, passo, "xlsx", 999999)));

        var xlsx = await PassoAsync(id, passo, "xlsx", t1.Id);
        Assert.Equal($"PDTIC_SES_passo-5.1_2026-T1_{Iso(HojeData())}.xlsx", xlsx.NomeArquivo);
        var abas = Abas(xlsx.Conteudo);
        Assert.Equal(new[] { Secao("monitoramento_acoes").Titulo, Secao("medicoes_indicadores").Titulo, Secao("riscos_ocorridos").Titulo, PeXlsx.NomeLeiaMe },
            abas.Take(4));
        // Só o 1º trimestre: as duas linhas dele, com a coluna do ciclo na frente
        var acoes = Linhas(xlsx.Conteudo, abas[0]);
        Assert.Equal(3, acoes.Count);
        Assert.Equal("Ciclo", acoes[0][0]);
        Assert.All(acoes.Skip(1), l => Assert.Equal(Trimestre1, l[0]));
        Assert.Contains(Linhas(xlsx.Conteudo, PeXlsx.NomeLeiaMe), l => l.Count >= 2 && l[0] == "Ciclo" && l[1] == "2026 · 1º trimestre (01/01/2026 a 31/03/2026)");

        var zip = await PassoAsync(id, passo, "csv", t2.Id);
        Assert.Equal(($"PDTIC_SES_passo-5.1_2026-T2_{Iso(HojeData())}.zip", PePlanilhaService.MimeZip), (zip.NomeArquivo, zip.TipoMime));
        var arquivos = Zip(zip.Conteudo);
        Assert.Equal(new[] { "5.1-medicoes_indicadores.csv", "5.1-monitoramento_acoes.csv", "5.1-riscos_ocorridos.csv" }, arquivos.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var linhas = TextoCsv(arquivos["5.1-monitoramento_acoes.csv"]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, linhas.Length);
        Assert.StartsWith($"{Trimestre2};", linhas[1]);

        // Passo sem seção por ciclo: o cicloId é ignorado (e o nome fica sem o ciclo)
        var semCiclo = await PassoAsync(id, "preparacao.abrangencia", "csv", t1.Id);
        Assert.Equal($"PDTIC_SES_passo-1.1_{Iso(HojeData())}.csv", semCiclo.NomeArquivo);
    }

    [Fact]
    public async Task PassoSemSecao404_ForaDaTrilha404_EFormatoErrado400()
    {
        var id = (await AbrirSesAsync()).Id;

        var ex = await Assert.ThrowsAsync<ApiException>(() => PassoAsync(id, "planejamento.documento", "xlsx"));
        Assert.Equal(Codigo(ErrorCode.PePassoSemPlanilha), ex.Error.Code);
        Assert.StartsWith("O passo 3.5 não tem dados para a planilha", ex.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PePassoSemPlanilha), await ErroAsync(() => PassoAsync(id, "planejamento.deliberacao-cgtic", "csv")));
        // A aprovação do SGTIC fica no passo do envio: ele tem planilha
        Assert.Equal(PePlanilhaService.MimeXlsx, (await PassoAsync(id, "planejamento.aprovacao-sgtic", "xlsx")).TipoMime);
        // A SWOT não está no Básico; passo que não existe
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), await ErroAsync(() => PassoAsync(id, "diagnostico.swot", "xlsx")));
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), await ErroAsync(() => PassoAsync(id, "nao.existe", "xlsx")));
        Assert.Equal(Codigo(ErrorCode.PePlanilhaInvalida), await ErroAsync(() => PassoAsync(id, "preparacao.abrangencia", "pdf")));
    }

    [Fact]
    public async Task QuemLeOPdtic_BaixaAPlanilhaDoPasso()
    {
        var id = (await AbrirSesAsync()).Id;
        await PreencherAbrangenciaAsync(id);

        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin, UserAdminGeral })
            Assert.Equal(PePlanilhaService.MimeCsv, (await PassoAsync(id, "preparacao.abrangencia", "csv", user: user)).TipoMime);
        foreach (var user in new[] { UserOrgaoSeec, UserSemPapel })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => PassoAsync(id, "preparacao.abrangencia", "csv", user: user)));
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado), await ErroAsync(() => PassoAsync(999999, "preparacao.abrangencia", "csv")));
    }

    [Fact]
    public async Task Rota_ArquivoComONome_EErroComCorpo()
    {
        var id = (await AbrirSesAsync()).Id;
        await IncluirNoPdticAsync(id, "sgtic", new { forma = "subcomite", ato_tipo = "portaria", ato_numero = "12/2026", ato_data = "2026-01-10" });
        var controlador = ControladorPdtic(UserConsultaSes);

        var arquivo = Assert.IsType<FileContentResult>(await controlador.PlanilhaDoPasso(id, "preparacao.sgtic", "csv", null));
        Assert.Equal((PePlanilhaService.MimeZip, $"PDTIC_SES_passo-1.3_{Iso(HojeData())}.zip"), (arquivo.ContentType, arquivo.FileDownloadName));

        var (status, corpo) = Resultado(await controlador.PlanilhaDoPasso(id, "planejamento.documento", null, null));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PePassoSemPlanilha)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorPdtic(UserOrgaoSeec).PlanilhaDoPasso(id, "preparacao.sgtic", "csv", null));
        Assert.Equal((StatusCodes.Status403Forbidden, Codigo(ErrorCode.PeSemPermissao)), (status, CodigoDe(corpo)));
    }
}
