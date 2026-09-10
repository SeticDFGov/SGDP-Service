using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Manifestações ao TCDF: incisos I e II, bloco de riscos, estágio derivado,
/// listagem com filtro por estágio e o efeito do soft delete do processo.
/// </summary>
public class CtrManifestacaoTest : CtrTestBase
{
    private readonly CtrManifestacaoService _service;
    private readonly CtrProcesso _processo;

    public CtrManifestacaoTest()
    {
        _service = NovoManifestacaoService();
        _processo = SemearProcesso("04044-00002545/2024-62", p => p.ChegadaSgdi = DiasAtras(20));
    }

    // ── Inciso I ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_IncisoI_GravaEDerivaOEstagio()
    {
        var ctx = await ContextoAnalistaAsync();

        var resposta = await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoI(), ctx);

        Assert.True(resposta.Id > 0);
        Assert.Equal(_processo.Id, resposta.ProcessoId);
        Assert.Equal("04044-00002545/2024-62", resposta.NumeroProcesso);
        Assert.Equal("SEEC", resposta.OrgaoSigla);
        Assert.Equal(CtrDominios.Estagio.Alinhada, resposta.Estagio);
        Assert.Equal(UserAnalista.Email, resposta.CriadoPor);
    }

    [Fact]
    public async Task Criar_IncisoI_SemDataCriticidadeOuResultado_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var semData = NovaManifestacaoIncisoI();
        semData.ComunicadaDesde = null;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, semData, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex1.Error.Code);

        var semCriticidade = NovaManifestacaoIncisoI();
        semCriticidade.Criticidade = null;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, semCriticidade, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex2.Error.Code);

        var semResultado = NovaManifestacaoIncisoI();
        semResultado.ResultadoAnalise = null;
        var ex3 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, semResultado, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex3.Error.Code);
    }

    [Fact]
    public async Task Criar_IncisoI_ComPrazoDeRegularizacao_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovaManifestacaoIncisoI();
        dto.PrazoRegularizacaoDias = 30; // campo do bloco inativo

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("Prazo de regularização", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_CriticidadeOuResultadoForaDoDominio_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var criticidade = NovaManifestacaoIncisoI();
        criticidade.Criticidade = "Altíssima";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, criticidade, ctx));
        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex1.Error.Code);

        var resultado = NovaManifestacaoIncisoI();
        resultado.ResultadoAnalise = "Tudo certo";
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, resultado, ctx));
        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex2.Error.Code);
    }

    // ── Bloco de riscos ───────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_RiscosSignificativos_AceitaAsDuasAcoesEODesfecho()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos);
        dto.RecomendouSuspensao = true;
        dto.ComunicouControleInterno = true;
        dto.DesfechoRisco = CtrDominios.DesfechoRisco.NaoPodeProsseguir;

        var resposta = await _service.CriarAsync(_processo.Id, dto, ctx);

        Assert.True(resposta.RecomendouSuspensao);
        Assert.True(resposta.ComunicouControleInterno);
        Assert.Equal(CtrDominios.Estagio.NaoPodeProsseguir, resposta.Estagio);
    }

    [Fact]
    public async Task Criar_RiscosSemDesfecho_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos);
        dto.DesfechoRisco = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Criar_AcoesDeRiscoSemRiscos_SaoRecusadas()
    {
        var ctx = await ContextoAnalistaAsync();

        var comSuspensao = NovaManifestacaoIncisoI();
        comSuspensao.RecomendouSuspensao = true;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, comSuspensao, ctx));
        Assert.Contains("suspensão", ex1.Error.Message);

        var comControle = NovaManifestacaoIncisoI();
        comControle.ComunicouControleInterno = true;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, comControle, ctx));
        Assert.Contains("controle interno", ex2.Error.Message);

        var comDesfecho = NovaManifestacaoIncisoI();
        comDesfecho.DesfechoRisco = CtrDominios.DesfechoRisco.RiscoResolvido;
        var ex3 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, comDesfecho, ctx));
        Assert.Contains("Desfecho do risco", ex3.Error.Message);
    }

    // ── Inciso II ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_IncisoII_GravaOPrazoEDerivaANotificacao()
    {
        var ctx = await ContextoAnalistaAsync();

        var resposta = await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoII(), ctx);

        Assert.Equal(30, resposta.PrazoRegularizacaoDias);
        Assert.Equal(CtrDominios.Estagio.NotificacaoRegularizar, resposta.Estagio);
        Assert.Null(resposta.Criticidade);
    }

    [Fact]
    public async Task Criar_IncisoII_SemPrazoOuComPrazoForaDaFaixa_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var semPrazo = NovaManifestacaoIncisoII();
        semPrazo.PrazoRegularizacaoDias = null;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, semPrazo, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex1.Error.Code);

        var zerado = NovaManifestacaoIncisoII();
        zerado.PrazoRegularizacaoDias = 0;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, zerado, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex2.Error.Code);

        var enorme = NovaManifestacaoIncisoII();
        enorme.PrazoRegularizacaoDias = 400;
        var ex3 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, enorme, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex3.Error.Code);
    }

    [Fact]
    public async Task Criar_IncisoII_ComCamposDoIncisoI_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var comCriticidade = NovaManifestacaoIncisoII();
        comCriticidade.Criticidade = CtrDominios.Criticidade.Baixa;
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, comCriticidade, ctx));
        Assert.Contains("Criticidade", ex.Error.Message);

        var comData = NovaManifestacaoIncisoII();
        comData.ComunicadaDesde = DiasAtras(10);
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, comData, ctx));
        Assert.Contains("Comunicada desde", ex2.Error.Message);
    }

    // ── Comuns ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_SemOficioOuComDataFutura_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var semOficio = NovaManifestacaoIncisoI();
        semOficio.OficioTcdf = "  ";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, semOficio, ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex1.Error.Code);

        var futura = NovaManifestacaoIncisoI();
        futura.DataOficio = Hoje.AddDays(1);
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(_processo.Id, futura, ctx));
        Assert.Contains("futura", ex2.Error.Message);
    }

    [Fact]
    public async Task Criar_EmProcessoInexistenteOuExcluido_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var excluido = SemearProcesso("00080-00224827/2024-02", p => p.Ativo = false);

        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAsync(9999, NovaManifestacaoIncisoI(), ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoNaoEncontrado, ex1.Error.Code);

        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAsync(excluido.Id, NovaManifestacaoIncisoI(), ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoNaoEncontrado, ex2.Error.Code);
    }

    [Fact]
    public async Task Atualizar_EvoluiODesfechoNaMesmaManifestacao()
    {
        var ctx = await ContextoAnalistaAsync();
        var criada = await _service.CriarAsync(_processo.Id,
            NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos), ctx);
        Assert.Equal(CtrDominios.Estagio.AguardandoResposta, criada.Estagio);

        var dto = NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos);
        dto.DesfechoRisco = CtrDominios.DesfechoRisco.RiscoResolvido;
        var atualizada = await _service.AtualizarAsync(criada.Id, dto, ctx);

        Assert.Equal(criada.Id, atualizada.Id);
        Assert.Equal(CtrDominios.Estagio.RiscoResolvido, atualizada.Estagio);
        Assert.Equal(UserAnalista.Email, atualizada.AlteradoPor);
        Assert.Single(await _service.ListarDoProcessoAsync(_processo.Id));
    }

    [Fact]
    public async Task Listar_FiltraPorEstagioEBusca()
    {
        var ctx = await ContextoAnalistaAsync();
        var outro = SemearProcesso("00080-00224827/2024-02");

        await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoI(), ctx);
        await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoII(), ctx);
        var riscos = NovaManifestacaoIncisoI(CtrDominios.ResultadoAnalise.RiscosSignificativos);
        riscos.OficioTcdf = "789/2026-GAB";
        await _service.CriarAsync(outro.Id, riscos, ctx);

        var todas = await _service.ListarAsync(new CtrManifestacaoFiltro { PageSize = 50 });
        Assert.Equal(3, todas.TotalItems);

        var alinhadas = await _service.ListarAsync(new CtrManifestacaoFiltro
        { Estagio = CtrDominios.Estagio.Alinhada, PageSize = 50 });
        Assert.Single(alinhadas.Items);

        var notificacoes = await _service.ListarAsync(new CtrManifestacaoFiltro
        { Estagio = CtrDominios.Estagio.NotificacaoRegularizar, PageSize = 50 });
        Assert.Single(notificacoes.Items);

        var aguardando = await _service.ListarAsync(new CtrManifestacaoFiltro
        { Estagio = CtrDominios.Estagio.AguardandoResposta, PageSize = 50 });
        Assert.Single(aguardando.Items);
        Assert.Equal("789/2026-GAB", aguardando.Items[0].OficioTcdf);

        var porOficio = await _service.ListarAsync(new CtrManifestacaoFiltro { Filtro = "789", PageSize = 50 });
        Assert.Single(porOficio.Items);

        var porNumero = await _service.ListarAsync(new CtrManifestacaoFiltro { Filtro = "00224827", PageSize = 50 });
        Assert.Single(porNumero.Items);
    }

    [Fact]
    public async Task Listar_OrdenaDaMaisRecenteParaAMaisAntiga()
    {
        var ctx = await ContextoAnalistaAsync();
        var antiga = NovaManifestacaoIncisoI();
        antiga.OficioTcdf = "001/2026";
        antiga.DataOficio = DiasAtras(30);
        await _service.CriarAsync(_processo.Id, antiga, ctx);

        var recente = NovaManifestacaoIncisoI();
        recente.OficioTcdf = "002/2026";
        recente.DataOficio = DiasAtras(1);
        await _service.CriarAsync(_processo.Id, recente, ctx);

        var lista = await _service.ListarAsync(new CtrManifestacaoFiltro { PageSize = 50 });
        Assert.Equal("002/2026", lista.Items[0].OficioTcdf);

        var doProcesso = await _service.ListarDoProcessoAsync(_processo.Id);
        Assert.Equal("002/2026", doProcesso[0].OficioTcdf);
    }

    [Fact]
    public async Task ProcessoExcluido_SomeDasListasEDo404()
    {
        var ctx = await ContextoAnalistaAsync();
        var criada = await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoI(), ctx);

        await NovoProcessoService().ExcluirAsync(_processo.Id, ctx);

        Assert.Empty((await _service.ListarAsync(new CtrManifestacaoFiltro { PageSize = 50 })).Items);
        Assert.Null(await _service.GetEntidadeAsync(criada.Id));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.GetAsync(criada.Id));
        Assert.Equal((int)ErrorCode.CtrManifestacaoNaoEncontrada, ex.Error.Code);
    }

    [Fact]
    public async Task Listar_SaneiaPaginacao()
    {
        var ctx = await ContextoAnalistaAsync();
        await _service.CriarAsync(_processo.Id, NovaManifestacaoIncisoI(), ctx);

        var pagina = await _service.ListarAsync(new CtrManifestacaoFiltro { Page = 0, PageSize = -3 });

        Assert.Equal(1, pagina.CurrentPage);
        Assert.Equal(1, pagina.PageSize);
        Assert.Single(pagina.Items);
    }

    [Fact]
    public void CalcularEstagio_CobreOsSeisValores()
    {
        Assert.Equal(CtrDominios.Estagio.NotificacaoRegularizar,
            CtrManifestacaoService.CalcularEstagio(new CtrManifestacaoTcdf
            { SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente }));

        Assert.Equal(CtrDominios.Estagio.Alinhada,
            CtrManifestacaoService.CalcularEstagio(new CtrManifestacaoTcdf
            {
                SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
                ResultadoAnalise = CtrDominios.ResultadoAnalise.Alinhada
            }));

        Assert.Equal(CtrDominios.Estagio.InformacoesSolicitadas,
            CtrManifestacaoService.CalcularEstagio(new CtrManifestacaoTcdf
            {
                SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
                ResultadoAnalise = CtrDominios.ResultadoAnalise.InformacoesComplementares
            }));

        foreach (var desfecho in CtrDominios.DesfechoRisco.Todos)
        {
            Assert.Equal(desfecho, CtrManifestacaoService.CalcularEstagio(new CtrManifestacaoTcdf
            {
                SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
                ResultadoAnalise = CtrDominios.ResultadoAnalise.RiscosSignificativos,
                DesfechoRisco = desfecho
            }));
        }

        Assert.Equal(6, CtrDominios.Estagio.Todos.Length);
    }
}
