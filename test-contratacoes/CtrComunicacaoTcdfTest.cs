using System.Collections;
using System.Reflection;
using System.Security.Claims;
using api.Contratacoes;
using Controllers.Contratacoes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-09-23 (equipe da SGDI): a comunicação do TCDF deixa de passar pelo
/// cadastro do processo da contratação (que pede critérios de criticidade que o TCDF não
/// traz) e ganha os dados do próprio Tribunal: o processo SEI de comunicação à SGDI, a data
/// de recebimento e o ato (Despacho Singular ou Decisão, com o número). A contratação que o
/// módulo ainda não tem nasce, na mesma gravação, como processo de origem TCDF; a que ele já
/// acompanha (inciso I) é vinculada pelo processo.
/// </summary>
public class CtrComunicacaoTcdfTest : CtrTestBase
{
    private const string ProcessoTcdf = "00600-00008620/2026-66";
    private const string ProcessoContratacao = "04044-00002545/2024-62";

    private readonly CtrManifestacaoService _service;

    public CtrComunicacaoTcdfTest()
    {
        _service = NovoManifestacaoService();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private static CtrContratacaoTcdfDTO Contratacao() => new()
    {
        OrgaoNome = "Departamento de Trânsito do Distrito Federal",
        OrgaoSigla = " detran ",
        Objeto = "Registro de preços para subscrições de software",
        CategoriaObjeto = CtrDominios.CategoriaObjeto.LicenciamentoSoftware,
        ValorEstimado = 1250000.5m
    };

    /// <summary>Comunicação válida do inciso II com uma contratação que o módulo ainda não tem.</summary>
    private static CtrComunicacaoTcdfCreateDTO ComunicacaoNova() => new()
    {
        ProcessoComunicacaoTcdf = ProcessoTcdf,
        OficioTcdf = "6018/2026-GP",
        DataOficio = DiasAtras(10),
        DataRecebimento = DiasAtras(8),
        AtoTcdf = CtrDominios.AtoTcdf.Decisao,
        NumeroAtoTcdf = "1234/2026",
        SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
        PrazoRegularizacaoDias = 5,
        StatusTcdf = CtrDominios.StatusTcdf.SuspensoIrregularidades,
        Contratacao = Contratacao()
    };

    /// <summary>A mesma comunicação, agora sobre um processo já cadastrado (inciso I).</summary>
    private static CtrComunicacaoTcdfCreateDTO ComunicacaoDoProcesso(long processoId) => new()
    {
        ProcessoId = processoId,
        ProcessoComunicacaoTcdf = ProcessoTcdf,
        OficioTcdf = "6019/2026-GP",
        DataOficio = DiasAtras(10),
        DataRecebimento = DiasAtras(8),
        AtoTcdf = CtrDominios.AtoTcdf.DespachoSingular,
        NumeroAtoTcdf = "45/2026-GCMA",
        SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
        ComunicadaDesde = DiasAtras(60),
        ResultadoAnalise = CtrDominios.ResultadoAnalise.Alinhada
    };

    /// <summary>Manifestação gravada direto no banco sem os dados da comunicação (as anteriores a 2026-09-23).</summary>
    private CtrManifestacaoTcdf SemearManifestacaoAntiga(CtrProcesso processo)
    {
        var antiga = new CtrManifestacaoTcdf
        {
            ProcessoId = processo.Id,
            OficioTcdf = "100/2026-GAB",
            DataOficio = DiasAtras(20),
            SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
            PrazoRegularizacaoDias = 30,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = "contratacoes@local.teste"
        };
        Context.CtrManifestacoesTcdf.Add(antiga);
        Context.SaveChanges();
        return antiga;
    }

    /// <summary>DTO de edição com os valores gravados (o que o formulário manda de volta).</summary>
    private static CtrManifestacaoUpdateDTO EdicaoDe(CtrManifestacaoTcdf m) => new()
    {
        OficioTcdf = m.OficioTcdf,
        DataOficio = m.DataOficio,
        ProcessoComunicacaoTcdf = m.ProcessoComunicacaoTcdf,
        DataRecebimento = m.DataRecebimento,
        AtoTcdf = m.AtoTcdf,
        NumeroAtoTcdf = m.NumeroAtoTcdf,
        SituacaoPortfolio = m.SituacaoPortfolio,
        PrazoRegularizacaoDias = m.PrazoRegularizacaoDias,
        StatusTcdf = m.StatusTcdf
    };

    private async Task<ApiException> RecusaAsync(CtrComunicacaoTcdfCreateDTO dto)
    {
        var ctx = await ContextoAnalistaAsync();
        return await Assert.ThrowsAsync<ApiException>(() => _service.CriarComunicacaoAsync(dto, ctx));
    }

    // ══ 1. Contratação que o módulo ainda não tem ═════════════════════════════

    [Fact]
    public async Task CriarComunicacao_ContratacaoNova_CriaOProcessoDoTcdfEAManifestacaoJuntos()
    {
        var ctx = await ContextoAnalistaAsync();

        var resposta = await _service.CriarComunicacaoAsync(ComunicacaoNova(), ctx);

        var processo = Context.CtrProcessos.Single();
        // O processo recebe o número do processo de comunicação: é o único que a SGDI tem
        Assert.Equal(ProcessoTcdf, processo.NumeroProcesso);
        Assert.Equal(CtrDominios.Origem.Tcdf, processo.Origem);
        Assert.Equal("Departamento de Trânsito do Distrito Federal", processo.OrgaoNome);
        Assert.Equal("DETRAN", processo.OrgaoSigla); // normalizado como no cadastro
        Assert.Equal("Registro de preços para subscrições de software", processo.Objeto);
        Assert.Equal(CtrDominios.CategoriaObjeto.LicenciamentoSoftware, processo.CategoriaObjeto);
        Assert.Equal(1250000.50m, processo.ValorEstimado);
        // Sem trâmite nem critérios: o TCDF não traz o processo da contratação
        Assert.Null(processo.ChegadaSgdi);
        Assert.Null(processo.Criticidade);
        Assert.Null(processo.CriteriosCriticidade);
        Assert.Equal(UserAnalista.Email, processo.CriadoPor);
        Assert.Equal(CtrDominios.Situacao.SemMovimentacao, CtrProcessoService.CalcularSituacao(processo));

        var gravada = Context.CtrManifestacoesTcdf.Single();
        Assert.Equal(processo.Id, gravada.ProcessoId);
        Assert.Equal(ProcessoTcdf, gravada.ProcessoComunicacaoTcdf);
        Assert.Equal(DiasAtras(8), gravada.DataRecebimento);
        Assert.Equal(CtrDominios.AtoTcdf.Decisao, gravada.AtoTcdf);
        Assert.Equal("1234/2026", gravada.NumeroAtoTcdf);
        Assert.Equal(5, gravada.PrazoRegularizacaoDias);

        Assert.Equal(processo.Id, resposta.ProcessoId);
        Assert.Equal(ProcessoTcdf, resposta.NumeroProcesso);
        Assert.Equal(ProcessoTcdf, resposta.ProcessoComunicacaoTcdf);
        Assert.Equal(DiasAtras(8), resposta.DataRecebimento);
        Assert.Equal(CtrDominios.AtoTcdf.Decisao, resposta.AtoTcdf);
        Assert.Equal("1234/2026", resposta.NumeroAtoTcdf);
        Assert.Equal("DETRAN", resposta.OrgaoSigla);
        Assert.Equal(CtrDominios.CategoriaObjeto.LicenciamentoSoftware, resposta.CategoriaObjeto);
        Assert.Equal(1250000.50m, resposta.ValorEstimado);
        Assert.Equal(CtrDominios.Origem.Tcdf, resposta.Origem);
        // O status no TCDF segue com precedência no estágio
        Assert.Equal(CtrDominios.Estagio.SuspensoIrregularidades, resposta.Estagio);
    }

    [Fact]
    public async Task CriarComunicacao_ContratacaoNova_ValorOpcional()
    {
        var dto = ComunicacaoNova();
        dto.Contratacao!.ValorEstimado = null;

        await _service.CriarComunicacaoAsync(dto, await ContextoAnalistaAsync());

        Assert.Null(Context.CtrProcessos.Single().ValorEstimado);
    }

    [Fact]
    public async Task CriarComunicacao_NumeroJaCadastrado_MandaVincularENaoGravaNada()
    {
        SemearProcesso(ProcessoTcdf, p => p.Origem = CtrDominios.Origem.Tcdf);

        var ex = await RecusaAsync(ComunicacaoNova());

        Assert.Equal((int)ErrorCode.CtrProcessoDuplicado, ex.Error.Code);
        Assert.Contains("registre a comunicação nele", ex.Error.Message);
        Assert.Single(Context.CtrProcessos);
        Assert.Empty(Context.CtrManifestacoesTcdf);
    }

    [Fact]
    public async Task CriarComunicacao_ContratacaoNovaNoIncisoI_MandaVincularAoProcessoDaContratacao()
    {
        var dto = ComunicacaoNova();
        dto.SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente;
        dto.PrazoRegularizacaoDias = null;
        dto.ComunicadaDesde = DiasAtras(60);
        dto.ResultadoAnalise = CtrDominios.ResultadoAnalise.Alinhada;

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("registre a comunicação no processo dela", ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
        Assert.Empty(Context.CtrManifestacoesTcdf);
    }

    [Theory]
    [InlineData("orgao", "Informe o nome do órgão.")]
    [InlineData("sigla", "Informe a sigla do órgão.")]
    [InlineData("objeto", "Informe o objeto da contratação.")]
    public async Task CriarComunicacao_ContratacaoIncompleta_EhRecusadaSemGravar(string campo, string mensagem)
    {
        var dto = ComunicacaoNova();
        if (campo == "orgao") dto.Contratacao!.OrgaoNome = " ";
        if (campo == "sigla") dto.Contratacao!.OrgaoSigla = "";
        if (campo == "objeto") dto.Contratacao!.Objeto = "";

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Equal(mensagem, ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
        Assert.Empty(Context.CtrManifestacoesTcdf);
    }

    [Fact]
    public async Task CriarComunicacao_CategoriaForaDoDominio_EhRecusada()
    {
        var dto = ComunicacaoNova();
        dto.Contratacao!.CategoriaObjeto = "Consultoria";

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Empty(Context.CtrProcessos);
    }

    [Fact]
    public async Task CriarComunicacao_ValorNegativo_EhRecusado()
    {
        var dto = ComunicacaoNova();
        dto.Contratacao!.ValorEstimado = -1m;

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Empty(Context.CtrProcessos);
    }

    // ══ 2. Contratação já acompanhada (processo vinculado) ════════════════════

    [Fact]
    public async Task CriarComunicacao_ProcessoVinculado_GravaNoProcessoSemMexerNele()
    {
        var processo = SemearProcesso(ProcessoContratacao, p => p.ChegadaSgdi = DiasAtras(90));

        var resposta = await _service.CriarComunicacaoAsync(ComunicacaoDoProcesso(processo.Id),
            await ContextoAnalistaAsync());

        Assert.Single(Context.CtrProcessos);
        Assert.Equal(processo.Id, resposta.ProcessoId);
        // O número do processo da contratação e o de comunicação do TCDF convivem
        Assert.Equal(ProcessoContratacao, resposta.NumeroProcesso);
        Assert.Equal(ProcessoTcdf, resposta.ProcessoComunicacaoTcdf);
        Assert.Equal(CtrDominios.AtoTcdf.DespachoSingular, resposta.AtoTcdf);
        Assert.Equal("45/2026-GCMA", resposta.NumeroAtoTcdf);
        // Criticidade do processo, que o inciso I reporta
        Assert.Equal(CtrDominios.Criticidade.Alta, resposta.Criticidade);
        Assert.Equal(CtrDominios.Estagio.Alinhada, resposta.Estagio);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, resposta.Origem);
    }

    [Fact]
    public async Task CriarComunicacao_ProcessoVinculadoSemCriticidade_IncisoISegueRecusado()
    {
        var processo = SemearProcesso(ProcessoContratacao, p => p.Criticidade = null);

        var ex = await RecusaAsync(ComunicacaoDoProcesso(processo.Id));

        Assert.Contains("Defina a criticidade no cadastro do processo", ex.Error.Message);
    }

    [Fact]
    public async Task CriarComunicacao_ProcessoInexistente_EhRecusado()
    {
        var ex = await RecusaAsync(ComunicacaoDoProcesso(999));

        Assert.Equal((int)ErrorCode.CtrProcessoNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task CriarComunicacao_SemProcessoNemContratacao_OuComOsDois_EhRecusada()
    {
        var processo = SemearProcesso(ProcessoContratacao);

        var nenhum = ComunicacaoNova();
        nenhum.Contratacao = null;
        var ex1 = await RecusaAsync(nenhum);
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex1.Error.Code);

        var osDois = ComunicacaoNova();
        osDois.ProcessoId = processo.Id;
        var ex2 = await RecusaAsync(osDois);
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex2.Error.Code);

        Assert.Empty(Context.CtrManifestacoesTcdf);
    }

    // ══ 3. Os quatro dados da comunicação ═════════════════════════════════════

    [Theory]
    [InlineData("processo", "Informe o número do processo de comunicação do TCDF.")]
    [InlineData("recebimento", "Informe a data de recebimento do processo na SGDI.")]
    [InlineData("ato", "Informe se o ato do TCDF é Despacho Singular ou Decisão.")]
    [InlineData("numero", "Informe o número da Decisão do TCDF.")]
    public async Task Criar_SemUmDosDadosDaComunicacao_EhRecusada(string campo, string mensagem)
    {
        var dto = ComunicacaoNova();
        if (campo == "processo") dto.ProcessoComunicacaoTcdf = "  ";
        if (campo == "recebimento") dto.DataRecebimento = null;
        if (campo == "ato") dto.AtoTcdf = null;
        if (campo == "numero") dto.NumeroAtoTcdf = "";

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Equal(mensagem, ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
    }

    [Fact]
    public async Task Criar_PelaRotaDoProcesso_TambemExigeOsDadosDaComunicacao()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(ProcessoContratacao);
        var dto = NovaManifestacaoIncisoII();
        dto.ProcessoComunicacaoTcdf = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CriarAsync(processo.Id, dto, ctx));

        Assert.Equal("Informe o número do processo de comunicação do TCDF.", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_ProcessoDeComunicacaoForaDoFormatoSei_EhRecusado()
    {
        var dto = ComunicacaoNova();
        dto.ProcessoComunicacaoTcdf = "6018/2026";

        var ex = await RecusaAsync(dto);

        Assert.Contains("fora do formato SEI", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_DataDeRecebimentoFutura_EhRecusada()
    {
        var dto = ComunicacaoNova();
        dto.DataRecebimento = Hoje.AddDays(1);

        var ex = await RecusaAsync(dto);

        Assert.Contains("não pode ser futura", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_RecebimentoAntesDoOficio_EhRecusado_ENoMesmoDiaVale()
    {
        var antes = ComunicacaoNova();
        antes.DataRecebimento = antes.DataOficio.AddDays(-1);
        var ex = await RecusaAsync(antes);
        Assert.Contains("não pode ser anterior à data do ofício", ex.Error.Message);

        var mesmoDia = ComunicacaoNova();
        mesmoDia.DataRecebimento = mesmoDia.DataOficio;
        var criada = await _service.CriarComunicacaoAsync(mesmoDia, await ContextoAnalistaAsync());
        Assert.Equal(mesmoDia.DataOficio, criada.DataRecebimento);
    }

    [Fact]
    public async Task Criar_AtoForaDoDominio_EhRecusado()
    {
        var dto = ComunicacaoNova();
        dto.AtoTcdf = "Acórdão";

        var ex = await RecusaAsync(dto);

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Criar_DespachoSingularSemNumero_MensagemNoMasculino()
    {
        var dto = ComunicacaoNova();
        dto.AtoTcdf = CtrDominios.AtoTcdf.DespachoSingular;
        dto.NumeroAtoTcdf = null;

        var ex = await RecusaAsync(dto);

        Assert.Equal("Informe o número do Despacho Singular do TCDF.", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_AparaOsTextosDaComunicacao()
    {
        var dto = ComunicacaoNova();
        dto.ProcessoComunicacaoTcdf = "  " + ProcessoTcdf + " ";
        dto.NumeroAtoTcdf = " 1234/2026 ";

        var criada = await _service.CriarComunicacaoAsync(dto, await ContextoAnalistaAsync());

        Assert.Equal(ProcessoTcdf, criada.ProcessoComunicacaoTcdf);
        Assert.Equal(ProcessoTcdf, Context.CtrProcessos.Single().NumeroProcesso);
        Assert.Equal("1234/2026", criada.NumeroAtoTcdf);
    }

    // ══ 4. Manifestações registradas antes destes campos ══════════════════════

    [Fact]
    public async Task Atualizar_ManifestacaoAntiga_SegueSemOsDadosDaComunicacao()
    {
        var processo = SemearProcesso(ProcessoContratacao);
        var antiga = SemearManifestacaoAntiga(processo);
        var edicao = EdicaoDe(antiga);
        edicao.StatusTcdf = CtrDominios.StatusTcdf.EditalRevogado;

        var atualizada = await _service.AtualizarAsync(antiga.Id, edicao, await ContextoAnalistaAsync());

        Assert.Equal(CtrDominios.Estagio.EditalRevogado, atualizada.Estagio);
        Assert.Null(atualizada.ProcessoComunicacaoTcdf);
        Assert.Null(atualizada.DataRecebimento);
    }

    [Fact]
    public async Task Atualizar_ManifestacaoAntigaComSoUmDado_PedeOsDemaisENaoGrava()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(ProcessoContratacao);
        var antiga = SemearManifestacaoAntiga(processo);
        var edicao = EdicaoDe(antiga);
        edicao.ProcessoComunicacaoTcdf = ProcessoTcdf;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.AtualizarAsync(antiga.Id, edicao, ctx));

        Assert.Equal("Informe a data de recebimento do processo na SGDI.", ex.Error.Message);
        Assert.Null(Context.CtrManifestacoesTcdf.Single().ProcessoComunicacaoTcdf);
    }

    [Fact]
    public async Task Atualizar_ManifestacaoAntigaComOsQuatro_PassaAExigiLos()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(ProcessoContratacao);
        var antiga = SemearManifestacaoAntiga(processo);
        var edicao = EdicaoDe(antiga);
        edicao.ProcessoComunicacaoTcdf = ProcessoTcdf;
        edicao.DataRecebimento = DiasAtras(18);
        edicao.AtoTcdf = CtrDominios.AtoTcdf.Decisao;
        edicao.NumeroAtoTcdf = "77/2026";

        var completada = await _service.AtualizarAsync(antiga.Id, edicao, ctx);
        Assert.Equal(ProcessoTcdf, completada.ProcessoComunicacaoTcdf);

        // Daí em diante não dá para apagar
        var apagando = EdicaoDe(Context.CtrManifestacoesTcdf.Single());
        apagando.DataRecebimento = null;
        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.AtualizarAsync(antiga.Id, apagando, ctx));
        Assert.Equal("Informe a data de recebimento do processo na SGDI.", ex.Error.Message);
    }

    [Fact]
    public async Task Atualizar_ManifestacaoNova_NaoPerdeOsDadosDaComunicacao()
    {
        var ctx = await ContextoAnalistaAsync();
        var criada = await _service.CriarComunicacaoAsync(ComunicacaoNova(), ctx);
        var edicao = EdicaoDe(Context.CtrManifestacoesTcdf.Single());
        edicao.ProcessoComunicacaoTcdf = null;
        edicao.DataRecebimento = null;
        edicao.AtoTcdf = null;
        edicao.NumeroAtoTcdf = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.AtualizarAsync(criada.Id, edicao, ctx));

        Assert.Equal("Informe o número do processo de comunicação do TCDF.", ex.Error.Message);
        Assert.Equal(ProcessoTcdf, Context.CtrManifestacoesTcdf.Single().ProcessoComunicacaoTcdf);
    }

    // ══ 5. Lista, despacho e endpoint ═════════════════════════════════════════

    [Fact]
    public async Task Listar_BuscaPeloProcessoDeComunicacao()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(ProcessoContratacao);
        SemearManifestacaoAntiga(processo); // sem processo de comunicação: a busca não quebra
        await _service.CriarComunicacaoAsync(ComunicacaoDoProcesso(processo.Id), ctx);

        var achadas = await _service.ListarAsync(new CtrManifestacaoFiltro { Filtro = "00008620" });

        Assert.Equal("6019/2026-GP", Assert.Single(achadas.Items).OficioTcdf);
    }

    [Fact]
    public void Despacho_CabecalhoTrazOProcessoDeComunicacao_EAAntigaOProcessoDaContratacao()
    {
        var processo = new CtrProcessoResponse { NumeroProcesso = ProcessoContratacao };

        Assert.Equal(ProcessoTcdf, CtrDespachoPdf.NumeroDoProcessoSei(processo,
            new CtrManifestacaoResponse { ProcessoComunicacaoTcdf = ProcessoTcdf }));
        Assert.Equal(ProcessoContratacao, CtrDespachoPdf.NumeroDoProcessoSei(processo,
            new CtrManifestacaoResponse { ProcessoComunicacaoTcdf = null }));
    }

    private CtrManifestacaoController Controller(string email) => new(_service, Permissoes)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, email) }, "teste"))
            }
        }
    };

    [Fact]
    public async Task Endpoint_CriaComQuemPodeEditar_E404ParaProcessoInexistente()
    {
        var criada = Assert.IsType<OkObjectResult>(await Controller(UserAnalista.Email).Criar(ComunicacaoNova()));
        Assert.Equal(ProcessoTcdf, Assert.IsType<CtrManifestacaoResponse>(criada.Value).ProcessoComunicacaoTcdf);

        Assert.IsType<NotFoundResult>(await Controller(UserAnalista.Email).Criar(ComunicacaoDoProcesso(999)));
    }

    [Fact]
    public async Task Endpoint_SemOPapel_EhProibido()
    {
        Assert.IsType<ForbidResult>(await Controller(UserBasico.Email).Criar(ComunicacaoNova()));
        Assert.Empty(Context.CtrProcessos);
    }

    // ══ 6. Migration: quatro colunas e dois CHECKs, só em ctr_manifestacao_tcdf ══

    /// <summary>Operações lidas por reflexão, como nas outras migrations do módulo.</summary>
    private static List<object> OperacoesDaMigration(string lado)
    {
        var tipo = typeof(CtrProcesso).Assembly.GetType("demanda_service.Migrations.CtrComunicacaoTcdf");
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(operacao);

    private static string TipoDe(object operacao) => operacao.GetType().Name;

    [Fact]
    public void Migration_SoAcrescentaColunasNullableEChecksNaManifestacao()
    {
        var operacoes = OperacoesDaMigration("UpOperations");

        Assert.All(operacoes, o => Assert.Equal("ctr_manifestacao_tcdf", Valor(o, "Table")));
        Assert.All(operacoes, o => Assert.Contains(TipoDe(o),
            new[] { "AddColumnOperation", "AddCheckConstraintOperation" }));

        var colunas = operacoes.Where(o => TipoDe(o) == "AddColumnOperation")
            .ToDictionary(o => (string)Valor(o, "Name")!);
        Assert.Equal(new[] { "ato_tcdf", "data_recebimento", "numero_ato_tcdf", "processo_comunicacao_tcdf" },
            colunas.Keys.OrderBy(n => n, StringComparer.Ordinal));
        // Nullable e sem backfill: as manifestações existentes ficam com os quatro nulos
        Assert.All(colunas.Values, c => Assert.True((bool)Valor(c, "IsNullable")!));
        Assert.Equal("character varying(25)", Valor(colunas["processo_comunicacao_tcdf"], "ColumnType"));
        Assert.Equal("date", Valor(colunas["data_recebimento"], "ColumnType"));
        Assert.Equal("character varying(20)", Valor(colunas["ato_tcdf"], "ColumnType"));
        Assert.Equal("character varying(60)", Valor(colunas["numero_ato_tcdf"], "ColumnType"));

        var checks = operacoes.Where(o => TipoDe(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => (string)Valor(o, "Name")!, o => (string)Valor(o, "Sql")!);
        Assert.Equal("ato_tcdf IS NULL OR ato_tcdf IN ('Despacho Singular','Decisão')",
            checks["ck_ctr_manifestacao_ato_tcdf"]);
        Assert.Contains("processo_comunicacao_tcdf IS NULL AND data_recebimento IS NULL",
            checks["ck_ctr_manifestacao_comunicacao"]);
        Assert.Contains("processo_comunicacao_tcdf IS NOT NULL AND data_recebimento IS NOT NULL",
            checks["ck_ctr_manifestacao_comunicacao"]);
        Assert.Equal(2, checks.Count);
    }

    [Fact]
    public void Migration_DownDesfazSoOQueOUpFez()
    {
        var operacoes = OperacoesDaMigration("DownOperations");

        Assert.Equal(6, operacoes.Count);
        Assert.All(operacoes, o => Assert.Equal("ctr_manifestacao_tcdf", Valor(o, "Table")));
        Assert.Equal(new[] { "ck_ctr_manifestacao_ato_tcdf", "ck_ctr_manifestacao_comunicacao" },
            operacoes.Where(o => TipoDe(o) == "DropCheckConstraintOperation")
                .Select(o => (string)Valor(o, "Name")!)
                .OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "ato_tcdf", "data_recebimento", "numero_ato_tcdf", "processo_comunicacao_tcdf" },
            operacoes.Where(o => TipoDe(o) == "DropColumnOperation")
                .Select(o => (string)Valor(o, "Name")!)
                .OrderBy(n => n, StringComparer.Ordinal));
    }
}
