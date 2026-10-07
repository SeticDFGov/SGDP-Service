using System.Text.Json;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-10-07: o processo informa se é nova contratação ou alteração de contratação
/// vigente (opcional; nulo = não informado), e o painel INTERNO conta os dois tipos. O painel
/// público não recebe o tipo nem a contagem.
/// </summary>
public class CtrTipoContratacaoTest : CtrTestBase
{
    private const string Numero = "04044-00000900/2026-11";

    private readonly CtrProcessoService _processos;

    public CtrTipoContratacaoTest()
    {
        _processos = NovoProcessoService();
    }

    private static CtrProcessoUpdateDTO EdicaoDe(CtrProcesso p)
    {
        var dto = new CtrProcessoUpdateDTO();
        var gravado = CtrProcessoService.DtoDe(p);
        foreach (var prop in typeof(CtrProcessoCreateDTO).GetProperties())
            prop.SetValue(dto, prop.GetValue(gravado));
        return dto;
    }

    [Fact]
    public async Task Criar_SemTipo_FicaNaoInformado()
    {
        var ctx = await ContextoAnalistaAsync();

        var resposta = await _processos.CriarAsync(NovoProcessoDto(Numero), ctx);

        Assert.Null(resposta.TipoContratacao);
        Assert.Null(Context.CtrProcessos.Single(p => p.Id == resposta.Id).TipoContratacao);
    }

    [Theory]
    [InlineData("Nova contratação", CtrDominios.TipoContratacao.Nova)]
    [InlineData("  nova CONTRATACAO ", CtrDominios.TipoContratacao.Nova)]
    [InlineData("Alteração de contratação vigente", CtrDominios.TipoContratacao.Alteracao)]
    [InlineData("alteracao de contratacao vigente", CtrDominios.TipoContratacao.Alteracao)]
    public async Task Criar_ComTipo_GravaNaGrafiaDoDominio(string enviado, string gravado)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.TipoContratacao = enviado;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(gravado, resposta.TipoContratacao);
        Assert.Equal(gravado, Context.CtrProcessos.Single(p => p.Id == resposta.Id).TipoContratacao);
    }

    [Fact]
    public async Task Criar_TipoForaDoDominio_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.TipoContratacao = "Prorrogação";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Empty(Context.CtrProcessos.Where(p => p.NumeroProcesso == Numero));
    }

    [Fact]
    public async Task Editar_TrocaOTipo_EVazioLimpa()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p => p.TipoContratacao = CtrDominios.TipoContratacao.Nova);

        var dto = EdicaoDe(processo);
        dto.TipoContratacao = CtrDominios.TipoContratacao.Alteracao;
        var trocado = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        Assert.Equal(CtrDominios.TipoContratacao.Alteracao, trocado.TipoContratacao);

        dto.TipoContratacao = "  ";
        var limpo = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        Assert.Null(limpo.TipoContratacao);
    }

    [Fact]
    public async Task Checkpoint_NaoMexeNoTipo()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.TipoContratacao = CtrDominios.TipoContratacao.Alteracao;
        });

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.ChegadaSubgd,
            Data = DiasAtras(1)
        }, ctx);

        Assert.Equal(CtrDominios.TipoContratacao.Alteracao, resposta.TipoContratacao);
    }

    [Fact]
    public async Task ReimportarAPlanilha_NaoApagaOTipo()
    {
        // A planilha não tem coluna para o tipo: a importação preserva o gravado
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.TipoContratacao = CtrDominios.TipoContratacao.Nova;
        });

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);

        Assert.Equal(1, relatorio.Atualizados);
        Assert.Equal(CtrDominios.TipoContratacao.Nova,
            Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero).TipoContratacao);
    }

    [Fact]
    public async Task PainelInterno_ContaOsDoisTipos_EOsNaoInformados()
    {
        SemearProcesso("04044-00000901/2026-11", p => p.TipoContratacao = CtrDominios.TipoContratacao.Nova);
        SemearProcesso("04044-00000902/2026-11", p => p.TipoContratacao = CtrDominios.TipoContratacao.Nova);
        SemearProcesso("04044-00000903/2026-11", p => p.TipoContratacao = CtrDominios.TipoContratacao.Alteracao);
        SemearProcesso("04044-00000904/2026-11");
        // Excluído não conta
        SemearProcesso("04044-00000905/2026-11", p =>
        {
            p.TipoContratacao = CtrDominios.TipoContratacao.Alteracao;
            p.Ativo = false;
        });

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(
            new[]
            {
                (CtrDominios.TipoContratacao.Nova, 2),
                (CtrDominios.TipoContratacao.Alteracao, 1),
                (CtrDominios.TipoContratacao.NaoInformado, 1)
            },
            painel.PorTipoContratacao.Select(c => (c.Chave, c.Quantidade)).ToArray());
    }

    [Fact]
    public async Task PainelInterno_SemProcessos_TrazAsTresChavesComZero()
    {
        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(
            new[]
            {
                CtrDominios.TipoContratacao.Nova,
                CtrDominios.TipoContratacao.Alteracao,
                CtrDominios.TipoContratacao.NaoInformado
            },
            painel.PorTipoContratacao.Select(c => c.Chave).ToArray());
        Assert.All(painel.PorTipoContratacao, c => Assert.Equal(0, c.Quantidade));
    }

    private void SemearOsTres()
    {
        SemearProcesso("04044-00000911/2026-11", p => p.TipoContratacao = CtrDominios.TipoContratacao.Nova);
        SemearProcesso("04044-00000912/2026-11", p => p.TipoContratacao = CtrDominios.TipoContratacao.Alteracao);
        SemearProcesso("04044-00000913/2026-11");
    }

    [Theory]
    [InlineData(CtrDominios.TipoContratacao.Nova, "04044-00000911/2026-11")]
    [InlineData(CtrDominios.TipoContratacao.Alteracao, "04044-00000912/2026-11")]
    [InlineData(CtrDominios.TipoContratacao.NaoInformado, "04044-00000913/2026-11")]
    public async Task FiltroDaLista_PorTipo_BateComAChaveDoPainel(string tipo, string numero)
    {
        SemearOsTres();

        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { TipoContratacao = tipo, PageSize = 50 });
        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(numero, Assert.Single(lista.Items).NumeroProcesso);
        Assert.Equal(lista.TotalItems, painel.PorTipoContratacao.Single(c => c.Chave == tipo).Quantidade);
    }

    [Fact]
    public async Task FiltroDaLista_ForaDoDominio_ListaVazia_ESemFiltroTrazTodos()
    {
        SemearOsTres();

        Assert.Empty((await _processos.ListarAsync(new CtrProcessoFiltro { TipoContratacao = "Prorrogação" })).Items);
        Assert.Equal(3, (await _processos.ListarAsync(new CtrProcessoFiltro { TipoContratacao = " ", PageSize = 50 })).TotalItems);
    }

    [Fact]
    public async Task FiltroDaLista_ValeNoExport()
    {
        SemearOsTres();

        var csv = CtrCsv.Ler(await _processos.ExportarCsvAsync(new CtrProcessoFiltro
        { TipoContratacao = CtrDominios.TipoContratacao.Alteracao }));

        Assert.Equal("04044-00000912/2026-11", Assert.Single(csv).NumeroProcesso);
    }

    [Fact]
    public async Task PainelPublico_NaoRecebeOTipo()
    {
        SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.TipoContratacao = CtrDominios.TipoContratacao.Alteracao;
        });

        var painel = await _processos.MontarPainelPublicoAsync();
        var json = JsonSerializer.Serialize(painel);

        Assert.Single(painel.Contratacoes);
        Assert.DoesNotContain("TipoContratacao", json);
        Assert.DoesNotContain("vigente", json);
        Assert.DoesNotContain(typeof(CtrContratacaoPublicaResponse).GetProperties(), p => p.Name == "TipoContratacao");
        Assert.DoesNotContain(typeof(CtrPainelPublicoResponse).GetProperties(), p => p.Name.Contains("Tipo"));
    }
}
