using api.Contratacoes;
using Microsoft.EntityFrameworkCore;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Importação da planilha: prévia sem gravar, upsert idempotente por número de
/// processo e o teste de aceite com o arquivo real da equipe.
/// </summary>
public class CtrImportacaoTest : CtrTestBase
{
    private const string Cabecalho =
        "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;Chegada da analise - SGDI;"
        + "Chegada da analise- SUBGD;Chegada da analise- UGTIC;Data de Retorno ao Gab SGDI;"
        + "Data de Retorno ao Órgão Comunicante;Observação";

    private readonly CtrImportacaoService _service;

    public CtrImportacaoTest()
    {
        _service = NovoImportacaoService();
    }

    private static byte[] Planilha(params string[] linhas) =>
        Bytes1252("Analises de contratações;;;;;;;;;;;\r\n" + Cabecalho + "\r\n"
            + string.Join("\r\n", linhas) + "\r\n");

    private static string Linha(string numero, string sigla = "SEEC", string data = "22/05/2026") =>
        $"{numero};Secretaria de Estado de Economia;{sigla};;Aquisição de switches;"
        + $"Infraestrutura de Rede;{data};;;;;";

    [Fact]
    public async Task Previa_NaoGravaNada()
    {
        var previa = await _service.PreviaAsync(Planilha(Linha("04044-00002545/2024-62")));

        Assert.Equal(1, previa.TotalLinhas);
        Assert.Equal(CtrImportacaoAcao.Criar, previa.Linhas[0].Acao);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, previa.Linhas[0].Situacao);
        Assert.Empty(await Context.CtrProcessos.ToListAsync());
    }

    [Fact]
    public async Task Importar_CriaEDepoisAtualizaSemDuplicar()
    {
        var ctx = await ContextoAnalistaAsync();

        var primeiro = await _service.ImportarAsync(Planilha(Linha("04044-00002545/2024-62")), ctx);
        Assert.Equal(1, primeiro.Criados);
        Assert.Equal(0, primeiro.Atualizados);
        Assert.Equal(0, primeiro.Rejeitados);

        // Idempotência: o mesmo arquivo de novo só atualiza
        var segundo = await _service.ImportarAsync(Planilha(Linha("04044-00002545/2024-62", data: "23/05/2026")), ctx);
        Assert.Equal(0, segundo.Criados);
        Assert.Equal(1, segundo.Atualizados);

        var processos = await Context.CtrProcessos.ToListAsync();
        var processo = Assert.Single(processos);
        Assert.Equal(new DateOnly(2026, 5, 23), processo.ChegadaSgdi);
        Assert.Equal(UserAnalista.Email, processo.CriadoPor);
        Assert.Equal(UserAnalista.Email, processo.AlteradoPor);
    }

    [Fact]
    public async Task Importar_GravaAsValidasERelataAsRejeitadas()
    {
        var ctx = await ContextoAnalistaAsync();

        var relatorio = await _service.ImportarAsync(Planilha(
            Linha("04044-00002545/2024-62"),
            Linha("numero-invalido"),
            Linha("00080-00224827/2024-02", data: "01/01/2099")), ctx); // data futura

        Assert.Equal(1, relatorio.Criados);
        Assert.Equal(2, relatorio.Rejeitados);
        Assert.Single(await Context.CtrProcessos.ToListAsync());

        var futura = relatorio.Linhas.Single(l => l.NumeroProcesso == "00080-00224827/2024-02");
        Assert.Contains("futura", futura.Motivo);
    }

    [Fact]
    public async Task Importar_LinhaInvalidaNaoSujaOProcessoQueJaExistia()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00002545/2024-62", p => p.ChegadaSgdi = new DateOnly(2026, 5, 22));

        // Cronologia impossível: a linha é rejeitada e o processo fica como estava
        var relatorio = await _service.ImportarAsync(Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;20/05/2026;;;;"), ctx);

        Assert.Equal(1, relatorio.Rejeitados);
        var processo = await Context.CtrProcessos.AsNoTracking().FirstAsync();
        Assert.Null(processo.ChegadaSubgd);
        Assert.Equal(new DateOnly(2026, 5, 22), processo.ChegadaSgdi);
    }

    [Fact]
    public async Task Importar_NaoRessuscitaProcessoExcluido()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00002545/2024-62", p => p.Ativo = false);

        var relatorio = await _service.ImportarAsync(Planilha(Linha("04044-00002545/2024-62")), ctx);

        // O upsert é entre ATIVOS: o número volta a ser livre e nasce um processo novo
        Assert.Equal(1, relatorio.Criados);
        Assert.Equal(2, await Context.CtrProcessos.CountAsync());
        Assert.Equal(1, await Context.CtrProcessos.CountAsync(p => p.Ativo));
    }

    [Fact]
    public async Task Importar_ArquivoVazioOuSemCabecalho_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var vazio = await Assert.ThrowsAsync<ApiException>(() => _service.ImportarAsync(Array.Empty<byte>(), ctx));
        Assert.Equal((int)ErrorCode.CtrImportacaoInvalida, vazio.Error.Code);

        var semCabecalho = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ImportarAsync(Bytes1252("uma coisa;outra coisa\r\n"), ctx));
        Assert.Equal((int)ErrorCode.CtrImportacaoInvalida, semCabecalho.Error.Code);
    }

    // ── Aceite: a planilha real da equipe ─────────────────────────────────────

    [Fact]
    public async Task Importar_PlanilhaReal_Cria37ProcessosSemRejeicao()
    {
        var ctx = await ContextoAnalistaAsync();

        var relatorio = await _service.ImportarAsync(PlanilhaReal(), ctx);

        Assert.Equal(37, relatorio.Criados);
        Assert.Equal(0, relatorio.Rejeitados);
        Assert.Equal(37, await Context.CtrProcessos.CountAsync(p => p.Ativo));

        // As duas restituições saem da observação para campos próprios
        var restituidos = await Context.CtrProcessos.Where(p => p.Restituido).ToListAsync();
        Assert.Equal(2, restituidos.Count);
        Assert.All(restituidos, p => Assert.Equal(new DateOnly(2026, 9, 3), p.RestituidoEm));
        Assert.All(restituidos, p => Assert.False(string.IsNullOrWhiteSpace(p.RestituidoMotivo)));
        Assert.All(restituidos, p => Assert.False(string.IsNullOrWhiteSpace(p.Observacao)));

        // Acentuação íntegra e a célula "-" da UGTIC virando "não se aplica"
        Assert.True(await Context.CtrProcessos.AnyAsync(p =>
            p.OrgaoNome == "Fundação de Apoio à Pesquisa do Distrito Federal"));
        Assert.True(await Context.CtrProcessos.AnyAsync(p => p.UgticNaoSeAplica));
        Assert.True(await Context.CtrProcessos.AnyAsync(p =>
            p.CategoriaObjeto == CtrDominios.CategoriaObjeto.SemObjeto));

        // Todas as categorias gravadas pertencem ao domínio
        var categorias = await Context.CtrProcessos.Select(p => p.CategoriaObjeto).Distinct().ToListAsync();
        Assert.All(categorias, c => Assert.Contains(c, CtrDominios.CategoriaObjeto.Todos));
    }

    [Fact]
    public async Task Importar_PlanilhaReal_DuasVezes_NaoDuplica()
    {
        var ctx = await ContextoAnalistaAsync();

        await _service.ImportarAsync(PlanilhaReal(), ctx);
        var segunda = await _service.ImportarAsync(PlanilhaReal(), ctx);

        Assert.Equal(0, segunda.Criados);
        Assert.Equal(37, segunda.Atualizados);
        Assert.Equal(0, segunda.Rejeitados);
        Assert.Equal(37, await Context.CtrProcessos.CountAsync());
    }

    [Fact]
    public async Task Importar_PlanilhaReal_EExportar_VoltaSemPerda()
    {
        var ctx = await ContextoAnalistaAsync();
        await _service.ImportarAsync(PlanilhaReal(), ctx);

        var exportado = await NovoProcessoService()
            .ExportarCsvAsync(new CtrProcessoFiltro { PageSize = 100 });

        var previa = await _service.PreviaAsync(exportado);

        Assert.Equal(37, previa.TotalLinhas);
        Assert.All(previa.Linhas, l => Assert.Equal(CtrImportacaoAcao.Atualizar, l.Acao));
    }
}
