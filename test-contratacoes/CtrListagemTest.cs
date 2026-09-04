using api.Contratacoes;
using Models.Contratacoes;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Listagem: busca, filtros (inclusive por situação derivada), saneamento da
/// paginação, ordenação, siglas e export CSV.
/// </summary>
public class CtrListagemTest : CtrTestBase
{
    private readonly CtrProcessoService _service;

    public CtrListagemTest()
    {
        _service = NovoProcessoService();
        Semear();
    }

    private void Semear()
    {
        // Sem movimentação
        SemearProcesso("04044-00000001/2026-11", p =>
        {
            p.OrgaoSigla = "SEEC";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede;
        });

        // Em análise na SGDI
        SemearProcesso("04044-00000002/2026-12", p =>
        {
            p.OrgaoSigla = "SEEC";
            p.ChegadaSgdi = DiasAtras(40);
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.LicenciamentoSoftware;
            p.Objeto = "Licenças de software de design gráfico";
        });

        // Em análise na SUBGD (UGTIC não se aplica)
        SemearProcesso("00080-00000003/2026-13", p =>
        {
            p.OrgaoSigla = "SEEDF";
            p.ChegadaSgdi = DiasAtras(30);
            p.ChegadaSubgd = DiasAtras(28);
            p.UgticNaoSeAplica = true;
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.SistemasGestao;
        });

        // Em análise na UGTIC
        SemearProcesso("00080-00000004/2026-14", p =>
        {
            p.OrgaoSigla = "SEEDF";
            p.ChegadaSgdi = DiasAtras(20);
            p.ChegadaSubgd = DiasAtras(19);
            p.ChegadaUgtic = DiasAtras(18);
        });

        // Retornado ao Gab SGDI
        SemearProcesso("00113-00000005/2026-15", p =>
        {
            p.OrgaoSigla = "DER";
            p.ChegadaSgdi = DiasAtras(15);
            p.ChegadaSubgd = DiasAtras(14);
            p.ChegadaUgtic = DiasAtras(13);
            p.RetornoGabSgdi = DiasAtras(10);
        });

        // Concluído
        SemearProcesso("00113-00000006/2026-16", p =>
        {
            p.OrgaoSigla = "DER";
            p.ChegadaSgdi = DiasAtras(12);
            p.ChegadaSubgd = DiasAtras(11);
            p.ChegadaUgtic = DiasAtras(10);
            p.RetornoGabSgdi = DiasAtras(9);
            p.RetornoOrgao = DiasAtras(8);
        });

        // Restituído
        SemearProcesso("00480-00000007/2026-17", p =>
        {
            p.OrgaoSigla = "CGDF";
            p.ChegadaSgdi = DiasAtras(6);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(3);
            p.RestituidoMotivo = "Processo restituído ao órgão para observância da IN 01/2026";
        });

        // Excluído (não pode aparecer em nada)
        SemearProcesso("00480-00000008/2026-18", p =>
        {
            p.OrgaoSigla = "SLU";
            p.Ativo = false;
        });
    }

    [Fact]
    public async Task Listar_SoTrazAtivos()
    {
        var pagina = await _service.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });

        Assert.Equal(7, pagina.TotalItems);
        Assert.DoesNotContain(pagina.Items, p => p.NumeroProcesso == "00480-00000008/2026-18");
    }

    [Theory]
    [InlineData(CtrDominios.Situacao.SemMovimentacao, 1)]
    [InlineData(CtrDominios.Situacao.EmAnaliseSgdi, 1)]
    [InlineData(CtrDominios.Situacao.EmAnaliseSubgd, 1)]
    [InlineData(CtrDominios.Situacao.EmAnaliseUgtic, 1)]
    [InlineData(CtrDominios.Situacao.RetornadoGabSgdi, 1)]
    [InlineData(CtrDominios.Situacao.Concluido, 1)]
    [InlineData(CtrDominios.Situacao.Restituido, 1)]
    public async Task Listar_FiltraPorSituacaoDerivada(string situacao, int esperado)
    {
        var pagina = await _service.ListarAsync(new CtrProcessoFiltro { Situacao = situacao, PageSize = 50 });

        Assert.Equal(esperado, pagina.TotalItems);
        Assert.All(pagina.Items, p => Assert.Equal(situacao, p.Situacao));
    }

    [Fact]
    public async Task Listar_SituacaoForaDoDominio_NaoDevolveNada()
    {
        // Filtro que o servidor não entende NÃO pode virar "todos" em silêncio
        var pagina = await _service.ListarAsync(new CtrProcessoFiltro { Situacao = "Em análise no TCDF", PageSize = 50 });

        Assert.Empty(pagina.Items);
        Assert.Equal(0, pagina.TotalItems);
    }

    [Fact]
    public async Task Listar_SemSituacao_NaoFiltraPorSituacao()
    {
        var vazia = await _service.ListarAsync(new CtrProcessoFiltro { Situacao = "   ", PageSize = 50 });

        Assert.Equal(7, vazia.TotalItems);
    }

    [Fact]
    public async Task Listar_BuscaTextualEhCaseInsensitive()
    {
        var porObjeto = await _service.ListarAsync(new CtrProcessoFiltro { Filtro = "DESIGN gráfico" });
        Assert.Single(porObjeto.Items);
        Assert.Equal("04044-00000002/2026-12", porObjeto.Items[0].NumeroProcesso);

        var porSigla = await _service.ListarAsync(new CtrProcessoFiltro { Filtro = "der", PageSize = 50 });
        Assert.Equal(2, porSigla.TotalItems);

        var porNumero = await _service.ListarAsync(new CtrProcessoFiltro { Filtro = "00000007" });
        Assert.Single(porNumero.Items);
    }

    [Fact]
    public async Task Listar_FiltraPorCategoriaSiglaRestituidosEPeriodo()
    {
        var porCategoria = await _service.ListarAsync(new CtrProcessoFiltro
        { Categoria = CtrDominios.CategoriaObjeto.LicenciamentoSoftware, PageSize = 50 });
        Assert.Single(porCategoria.Items);

        // A sigla é comparada em caixa alta
        var porSigla = await _service.ListarAsync(new CtrProcessoFiltro { Sigla = "seedf", PageSize = 50 });
        Assert.Equal(2, porSigla.TotalItems);

        var restituidos = await _service.ListarAsync(new CtrProcessoFiltro { Restituidos = true, PageSize = 50 });
        Assert.Single(restituidos.Items);

        var naoRestituidos = await _service.ListarAsync(new CtrProcessoFiltro { Restituidos = false, PageSize = 50 });
        Assert.Equal(6, naoRestituidos.TotalItems);

        // Janela sobre ChegadaSgdi: exclui quem não tem a data
        var janela = await _service.ListarAsync(new CtrProcessoFiltro
        { De = DiasAtras(16), Ate = DiasAtras(5), PageSize = 50 });
        Assert.Equal(3, janela.TotalItems);
    }

    [Fact]
    public async Task Listar_SaneiaPaginacao()
    {
        var negativa = await _service.ListarAsync(new CtrProcessoFiltro { Page = -5, PageSize = 0 });
        Assert.Equal(1, negativa.CurrentPage);
        Assert.Equal(1, negativa.PageSize);
        Assert.Single(negativa.Items);

        // PageSize acima do teto do PagedRequest é limitado a 100 pela própria classe
        var teto = await _service.ListarAsync(new CtrProcessoFiltro { PageSize = 5000 });
        Assert.Equal(100, teto.PageSize);

        // Page gigante não estoura o Skip (int.MaxValue / pageSize)
        var enorme = await _service.ListarAsync(new CtrProcessoFiltro { Page = int.MaxValue, PageSize = 20 });
        Assert.Empty(enorme.Items);
        Assert.Equal(7, enorme.TotalItems);
    }

    [Fact]
    public async Task Listar_OrdenacaoPadraoEhChegadaSgdiDescComNulosPorUltimo()
    {
        var pagina = await _service.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });

        Assert.Equal("00480-00000007/2026-17", pagina.Items[0].NumeroProcesso); // mais recente
        Assert.Null(pagina.Items[^1].ChegadaSgdi);                              // nulo por último
    }

    [Fact]
    public async Task Listar_OrdenacaoPorCampoValido()
    {
        var porNumero = await _service.ListarAsync(new CtrProcessoFiltro
        { OrderBy = "NumeroProcesso", OrderDirection = "asc", PageSize = 50 });
        Assert.Equal("00080-00000003/2026-13", porNumero.Items[0].NumeroProcesso);

        var porSigla = await _service.ListarAsync(new CtrProcessoFiltro
        { OrderBy = "OrgaoSigla", OrderDirection = "desc", PageSize = 50 });
        Assert.Equal("SEEDF", porSigla.Items[0].OrgaoSigla);

        // Campo desconhecido cai no padrão
        var invalido = await _service.ListarAsync(new CtrProcessoFiltro { OrderBy = "Objeto", PageSize = 50 });
        Assert.Equal("00480-00000007/2026-17", invalido.Items[0].NumeroProcesso);
    }

    [Fact]
    public async Task ListarSiglas_SoAtivasSemRepetirEOrdenadas()
    {
        var siglas = await _service.ListarSiglasAsync();

        Assert.Equal(new[] { "CGDF", "DER", "SEEC", "SEEDF" }, siglas);
    }

    [Fact]
    public async Task Listar_TrazTotalDeManifestacoesEUltimoEstagio()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = Context.CtrProcessos.First(p => p.NumeroProcesso == "04044-00000001/2026-11");
        var manifestacoes = NovoManifestacaoService();

        await manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoII(), ctx);
        var segunda = NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos);
        segunda.DataOficio = Hoje;
        await manifestacoes.CriarAsync(processo.Id, segunda, ctx);

        var pagina = await _service.ListarAsync(new CtrProcessoFiltro { Filtro = "00000001", PageSize = 50 });

        Assert.Equal(2, pagina.Items[0].TotalManifestacoes);
        Assert.Equal(CtrDominios.Estagio.AguardandoResposta, pagina.Items[0].UltimoEstagioTcdf);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Exportar_TemBomCabecalhoEDatasBrasileiras()
    {
        var bytes = await _service.ExportarCsvAsync(new CtrProcessoFiltro { PageSize = 50 });

        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var linhas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(string.Join(";", CtrCsv.Cabecalho), linhas[0]);
        Assert.Equal(8, linhas.Length); // cabeçalho + 7 ativos
        Assert.Contains(DiasAtras(3).ToString("dd/MM/yyyy"), texto);
        // UGTIC "não se aplica" volta como "-"
        Assert.Contains(";-;", texto);
    }

    [Fact]
    public async Task Exportar_RespeitaOsFiltros()
    {
        var bytes = await _service.ExportarCsvAsync(new CtrProcessoFiltro { Sigla = "DER" });
        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var linhas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, linhas.Length); // cabeçalho + 2
    }

    [Fact]
    public async Task Exportar_ERelerAPrevia_ClassificaTudoComoAtualizar()
    {
        // Round-trip: o arquivo exportado tem de voltar sem perda nem rejeição
        var bytes = await _service.ExportarCsvAsync(new CtrProcessoFiltro { PageSize = 50 });

        var previa = await NovoImportacaoService().PreviaAsync(bytes);

        Assert.Equal(7, previa.TotalLinhas);
        Assert.All(previa.Linhas, l => Assert.Equal(CtrImportacaoAcao.Atualizar, l.Acao));
        Assert.All(previa.Linhas, l => Assert.Null(l.Motivo));

        var restituido = previa.Linhas.Single(l => l.NumeroProcesso == "00480-00000007/2026-17");
        Assert.True(restituido.Dados!.Restituido);
        Assert.Equal(DiasAtras(3), restituido.Dados.RestituidoEm);
        Assert.Equal(CtrDominios.Situacao.Restituido, restituido.Situacao);

        var semUgtic = previa.Linhas.Single(l => l.NumeroProcesso == "00080-00000003/2026-13");
        Assert.True(semUgtic.Dados!.UgticNaoSeAplica);
        Assert.Null(semUgtic.Dados.ChegadaUgtic);
    }
}
