using api.Contratacoes;
using Models.Contratacoes;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-10-07: duas categorias do objeto a mais, "Nuvem" e "Tokens de IA", depois de
/// "Serviços de TIC". Valem no cadastro, na importação (sem caixa nem acento) e no export.
/// </summary>
public class CtrCategoriaNuvemTokensIaTest : CtrTestBase
{
    private readonly CtrProcessoService _processos;

    public CtrCategoriaNuvemTokensIaTest()
    {
        _processos = NovoProcessoService();
    }

    [Fact]
    public void Dominio_TemAsDuasDepoisDeServicosDeTic()
    {
        var todos = CtrDominios.CategoriaObjeto.Todos;
        var servicos = Array.IndexOf(todos, CtrDominios.CategoriaObjeto.ServicosTic);

        Assert.Equal(15, todos.Length);
        Assert.Equal(CtrDominios.CategoriaObjeto.Nuvem, todos[servicos + 1]);
        Assert.Equal(CtrDominios.CategoriaObjeto.TokensIa, todos[servicos + 2]);
        Assert.Equal("Nuvem", CtrDominios.CategoriaObjeto.Nuvem);
        Assert.Equal("Tokens de IA", CtrDominios.CategoriaObjeto.TokensIa);
    }

    [Theory]
    [InlineData(CtrDominios.CategoriaObjeto.Nuvem)]
    [InlineData(CtrDominios.CategoriaObjeto.TokensIa)]
    public async Task Cadastro_AceitaACategoria(string categoria)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000950/2026-11");
        dto.CategoriaObjeto = categoria;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(categoria, resposta.CategoriaObjeto);
    }

    [Theory]
    [InlineData("nuvem", CtrDominios.CategoriaObjeto.Nuvem)]
    [InlineData("Núvem", CtrDominios.CategoriaObjeto.Nuvem)]
    [InlineData("TOKENS DE IA", CtrDominios.CategoriaObjeto.TokensIa)]
    [InlineData(" tokens  de ia ", CtrDominios.CategoriaObjeto.TokensIa)]
    public void Planilha_ResolveSemCaixaNemAcento(string celula, string categoria)
    {
        Assert.Equal(categoria, CtrCsv.ResolverCategoria(celula));
    }

    [Fact]
    public async Task ExportEReimportacao_PreservamAsCategorias()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00000951/2026-11", p => p.CategoriaObjeto = CtrDominios.CategoriaObjeto.Nuvem);
        SemearProcesso("04044-00000952/2026-11", p => p.CategoriaObjeto = CtrDominios.CategoriaObjeto.TokensIa);

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);

        Assert.Equal(2, relatorio.Atualizados);
        Assert.Equal(CtrDominios.CategoriaObjeto.Nuvem,
            Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000951/2026-11").CategoriaObjeto);
        Assert.Equal(CtrDominios.CategoriaObjeto.TokensIa,
            Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000952/2026-11").CategoriaObjeto);
    }

    [Fact]
    public async Task FiltroDaLista_PelaCategoriaNova()
    {
        SemearProcesso("04044-00000953/2026-11", p => p.CategoriaObjeto = CtrDominios.CategoriaObjeto.TokensIa);
        SemearProcesso("04044-00000954/2026-11");

        var lista = await _processos.ListarAsync(new CtrProcessoFiltro
        { Categoria = CtrDominios.CategoriaObjeto.TokensIa, PageSize = 50 });

        Assert.Equal("04044-00000953/2026-11", Assert.Single(lista.Items).NumeroProcesso);
    }
}
