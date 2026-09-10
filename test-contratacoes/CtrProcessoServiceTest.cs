using Microsoft.EntityFrameworkCore;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// CRUD, validações e situação derivada do processo.
/// </summary>
public class CtrProcessoServiceTest : CtrTestBase
{
    private readonly CtrProcessoService _service;

    public CtrProcessoServiceTest()
    {
        _service = NovoProcessoService();
    }

    // ── Criação e validações ──────────────────────────────────────────────────

    [Fact]
    public async Task Criar_GravaComAuditoriaESiglaEmCaixaAlta()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(sigla: " seec ");
        dto.OrgaoNome = "  Secretaria de Estado de Economia  ";

        var resposta = await _service.CriarAsync(dto, ctx);

        Assert.True(resposta.Id > 0);
        Assert.Equal("SEEC", resposta.OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Economia", resposta.OrgaoNome);
        Assert.Equal(UserAnalista.Email, resposta.CriadoPor);
        Assert.Equal(CtrDominios.Situacao.SemMovimentacao, resposta.Situacao);
        Assert.Equal(0, resposta.TotalManifestacoes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0404-00002545/2024-62")]
    [InlineData("04044-0002545/2024-62")]
    [InlineData("04044-00002545/2024-6")]
    [InlineData("04044.00002545/2024-62")]
    public async Task Criar_NumeroForaDoFormatoSei_EhRecusado(string numero)
    {
        var ctx = await ContextoAnalistaAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAsync(NovoProcessoDto(numero: numero), ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Criar_NumeroDuplicadoEntreAtivos_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        await _service.CriarAsync(NovoProcessoDto(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAsync(NovoProcessoDto(), ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoDuplicado, ex.Error.Code);
        Assert.Contains("04044-00002545/2024-62", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_NumeroDeProcessoExcluido_EhReaproveitado()
    {
        var ctx = await ContextoAnalistaAsync();
        var primeiro = await _service.CriarAsync(NovoProcessoDto(), ctx);
        await _service.ExcluirAsync(primeiro.Id, ctx);

        // A unicidade só vale entre ATIVOS (índice parcial WHERE ativo)
        var segundo = await _service.CriarAsync(NovoProcessoDto(), ctx);

        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.Null(await _service.GetEntidadeAsync(primeiro.Id));
    }

    [Fact]
    public async Task Criar_CategoriaForaDoDominio_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.CategoriaObjeto = "Computadores";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Criar_SemOrgaoOuObjeto_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var semObjeto = NovoProcessoDto();
        semObjeto.Objeto = "   ";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(semObjeto, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex1.Error.Code);

        var semOrgao = NovoProcessoDto(numero: "00080-00224827/2024-02");
        semOrgao.OrgaoNome = "";
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(semOrgao, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex2.Error.Code);
    }

    [Fact]
    public async Task Criar_CronologiaForaDeOrdem_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(10);
        dto.ChegadaSubgd = DiasAtras(20); // anterior à chegada na SGDI

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        // A mensagem nomeia as DUAS datas
        Assert.Contains("SUBGD", ex.Error.Message);
        Assert.Contains("SGDI", ex.Error.Message);
        Assert.Contains(DiasAtras(20).ToString("dd/MM/yyyy"), ex.Error.Message);
        Assert.Contains(DiasAtras(10).ToString("dd/MM/yyyy"), ex.Error.Message);
    }

    [Fact]
    public async Task Criar_ComLacunaNaCronologia_EhAceito()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(30);
        dto.UgticNaoSeAplica = true;      // etapa pulada
        dto.RetornoGabSgdi = DiasAtras(5);

        var resposta = await _service.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Situacao.RetornadoGabSgdi, resposta.Situacao);
    }

    [Fact]
    public async Task Criar_DataFutura_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = Hoje.AddDays(1);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        Assert.Contains("futura", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_UgticNaoSeAplicaComData_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(10);
        dto.ChegadaUgtic = DiasAtras(8);
        dto.UgticNaoSeAplica = true;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("não se aplica", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_RestituidoSemDataOuMotivo_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();

        var semData = NovoProcessoDto();
        semData.Restituido = true;
        semData.RestituidoMotivo = "Faltou o formulário da IN 01/2026";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(semData, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex1.Error.Code);

        var semMotivo = NovoProcessoDto();
        semMotivo.Restituido = true;
        semMotivo.RestituidoEm = DiasAtras(2);
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(semMotivo, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex2.Error.Code);
    }

    [Fact]
    public async Task Criar_NaoRestituidoComDataEMotivo_AnulaOsDois()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.Restituido = false;
        dto.RestituidoEm = DiasAtras(3);
        dto.RestituidoMotivo = "sobra de preenchimento";

        var resposta = await _service.CriarAsync(dto, ctx);

        // Normalização, não erro
        Assert.False(resposta.Restituido);
        Assert.Null(resposta.RestituidoEm);
        Assert.Null(resposta.RestituidoMotivo);
    }

    [Fact]
    public async Task Criar_RestituidoAntesDaChegada_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(5);
        dto.Restituido = true;
        dto.RestituidoEm = DiasAtras(9);
        dto.RestituidoMotivo = "devolvido";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
    }

    // ── Situação derivada, última movimentação e dias parados ─────────────────

    [Fact]
    public void CalcularSituacao_CobreAsSeteSituacoes()
    {
        var d = DiasAtras(10);

        Assert.Equal(CtrDominios.Situacao.SemMovimentacao,
            CtrProcessoService.CalcularSituacao(new CtrProcesso()));

        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi,
            CtrProcessoService.CalcularSituacao(new CtrProcesso { ChegadaSgdi = d }));

        Assert.Equal(CtrDominios.Situacao.EmAnaliseSubgd,
            CtrProcessoService.CalcularSituacao(new CtrProcesso { ChegadaSgdi = d, ChegadaSubgd = d }));

        // Com a UGTIC "não se aplica" a situação permanece na SUBGD
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSubgd,
            CtrProcessoService.CalcularSituacao(new CtrProcesso
            { ChegadaSgdi = d, ChegadaSubgd = d, UgticNaoSeAplica = true }));

        Assert.Equal(CtrDominios.Situacao.EmAnaliseUgtic,
            CtrProcessoService.CalcularSituacao(new CtrProcesso
            { ChegadaSgdi = d, ChegadaSubgd = d, ChegadaUgtic = d }));

        Assert.Equal(CtrDominios.Situacao.RetornadoGabSgdi,
            CtrProcessoService.CalcularSituacao(new CtrProcesso
            { ChegadaSgdi = d, ChegadaSubgd = d, ChegadaUgtic = d, RetornoGabSgdi = d }));

        Assert.Equal(CtrDominios.Situacao.Concluido,
            CtrProcessoService.CalcularSituacao(new CtrProcesso
            { ChegadaSgdi = d, ChegadaSubgd = d, ChegadaUgtic = d, RetornoGabSgdi = d, RetornoOrgao = d }));

        // Restituído tem precedência sobre tudo
        Assert.Equal(CtrDominios.Situacao.Restituido,
            CtrProcessoService.CalcularSituacao(new CtrProcesso
            { ChegadaSgdi = d, RetornoOrgao = d, Restituido = true }));
    }

    [Fact]
    public void UltimaMovimentacao_EhAMaiorDataInclusiveARestituicao()
    {
        var processo = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            ChegadaSubgd = DiasAtras(20),
            Restituido = true,
            RestituidoEm = DiasAtras(4)
        };

        Assert.Equal(DiasAtras(4), CtrProcessoService.CalcularUltimaMovimentacao(processo));
        Assert.Null(CtrProcessoService.CalcularUltimaMovimentacao(new CtrProcesso()));
    }

    [Fact]
    public async Task DiasSemMovimento_ContaDaUltimaData_OuDaCriacao()
    {
        var ctx = await ContextoAnalistaAsync();

        var comData = NovoProcessoDto();
        comData.ChegadaSgdi = DiasAtras(12);
        var resposta = await _service.CriarAsync(comData, ctx);
        Assert.Equal(12, resposta.DiasSemMovimento);
        Assert.Equal(DiasAtras(12), resposta.UltimaMovimentacao);

        // Sem nenhuma data, conta da criação (hoje) — nunca negativo
        var semData = await _service.CriarAsync(NovoProcessoDto(numero: "00080-00224827/2024-02"), ctx);
        Assert.Equal(0, semData.DiasSemMovimento);
        Assert.Null(semData.UltimaMovimentacao);
    }

    // ── Edição, exclusão e checkpoint ─────────────────────────────────────────

    [Fact]
    public async Task Atualizar_GravaAlteradoPor()
    {
        var ctx = await ContextoAnalistaAsync();
        var criado = await _service.CriarAsync(NovoProcessoDto(), ctx);

        var dto = NovoProcessoDto();
        dto.Objeto = "Aquisição de switches topo de rack";
        dto.ChegadaSgdi = DiasAtras(4);
        var atualizado = await _service.AtualizarAsync(criado.Id, dto, ctx);

        Assert.Equal("Aquisição de switches topo de rack", atualizado.Objeto);
        Assert.Equal(UserAnalista.Email, atualizado.AlteradoPor);
        Assert.NotNull(atualizado.AlteradoEm);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, atualizado.Situacao);
    }

    [Fact]
    public async Task Excluir_EhSoftDelete()
    {
        var ctx = await ContextoAnalistaAsync();
        var criado = await _service.CriarAsync(NovoProcessoDto(), ctx);

        await _service.ExcluirAsync(criado.Id, ctx);

        Assert.Null(await _service.GetEntidadeAsync(criado.Id));
        var linha = await Context.CtrProcessos.AsNoTracking().FirstAsync(p => p.Id == criado.Id);
        Assert.False(linha.Ativo);
        Assert.Equal(UserAnalista.Email, linha.AlteradoPor);
    }

    [Fact]
    public async Task Checkpoint_RegistraLimpaEMarcaNaoSeAplica()
    {
        var ctx = await ContextoAnalistaAsync();
        var criado = await _service.CriarAsync(NovoProcessoDto(), ctx);

        var comSgdi = await _service.RegistrarCheckpointAsync(criado.Id,
            new api.Contratacoes.CtrCheckpointDTO
            { Etapa = CtrDominios.Etapa.ChegadaSgdi, Data = DiasAtras(6) }, ctx);
        Assert.Equal(DiasAtras(6), comSgdi.ChegadaSgdi);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, comSgdi.Situacao);

        var naoSeAplica = await _service.RegistrarCheckpointAsync(criado.Id,
            new api.Contratacoes.CtrCheckpointDTO
            { Etapa = CtrDominios.Etapa.ChegadaUgtic, Data = DiasAtras(2), NaoSeAplica = true }, ctx);
        Assert.True(naoSeAplica.UgticNaoSeAplica);
        Assert.Null(naoSeAplica.ChegadaUgtic);

        var desmarcado = await _service.RegistrarCheckpointAsync(criado.Id,
            new api.Contratacoes.CtrCheckpointDTO
            { Etapa = CtrDominios.Etapa.ChegadaUgtic, Data = DiasAtras(2), NaoSeAplica = false }, ctx);
        Assert.False(desmarcado.UgticNaoSeAplica);
        Assert.Equal(DiasAtras(2), desmarcado.ChegadaUgtic);

        var limpo = await _service.RegistrarCheckpointAsync(criado.Id,
            new api.Contratacoes.CtrCheckpointDTO
            { Etapa = CtrDominios.Etapa.ChegadaUgtic, Data = null }, ctx);
        Assert.Null(limpo.ChegadaUgtic);
    }

    [Fact]
    public async Task Checkpoint_EtapaInvalidaOuNaoSeAplicaForaDaUgtic_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var criado = await _service.CriarAsync(NovoProcessoDto(), ctx);

        var ex1 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarCheckpointAsync(criado.Id,
                new api.Contratacoes.CtrCheckpointDTO { Etapa = "ChegadaCGDF", Data = Hoje }, ctx));
        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex1.Error.Code);

        var ex2 = await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarCheckpointAsync(criado.Id,
                new api.Contratacoes.CtrCheckpointDTO
                { Etapa = CtrDominios.Etapa.ChegadaSubgd, NaoSeAplica = true }, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex2.Error.Code);
    }

    [Fact]
    public async Task Checkpoint_QuebrandoACronologia_EhRecusadoESemGravar()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(10);
        var criado = await _service.CriarAsync(dto, ctx);

        await Assert.ThrowsAsync<ApiException>(() =>
            _service.RegistrarCheckpointAsync(criado.Id,
                new api.Contratacoes.CtrCheckpointDTO
                { Etapa = CtrDominios.Etapa.ChegadaSubgd, Data = DiasAtras(30) }, ctx));

        var atual = await _service.GetAsync(criado.Id);
        Assert.Null(atual.ChegadaSubgd);
    }

    [Fact]
    public async Task Get_DeProcessoExcluido_NaoEncontra()
    {
        var ctx = await ContextoAnalistaAsync();
        var criado = await _service.CriarAsync(NovoProcessoDto(), ctx);
        await _service.ExcluirAsync(criado.Id, ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.GetAsync(criado.Id));
        Assert.Equal((int)ErrorCode.CtrProcessoNaoEncontrado, ex.Error.Code);
    }
}
