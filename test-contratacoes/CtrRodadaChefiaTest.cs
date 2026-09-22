using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Rodada da chefia (2026-09-11): devolução ao órgão dispensada, fase da contratação
/// (etapa do planejamento + assinatura), criticidade movida da manifestação para o
/// processo, status do processo no TCDF e origem (órgão comunicante × TCDF).
/// Cada mudança de comportamento tem teste próprio aqui.
/// </summary>
[Collection(CtrDespachoPdfCollection.Nome)]
public class CtrRodadaChefiaTest : CtrTestBase
{
    private readonly CtrProcessoService _processos;
    private readonly CtrManifestacaoService _manifestacoes;

    public CtrRodadaChefiaTest()
    {
        _processos = NovoProcessoService();
        _manifestacoes = NovoManifestacaoService();
    }

    // ══ 1. Pular a devolução ao órgão ═════════════════════════════════════════

    [Fact]
    public void CalcularSituacao_RetornoDispensadoComRetornoAoGab_Conclui()
    {
        var processo = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            ChegadaSubgd = DiasAtras(25),
            ChegadaUgtic = DiasAtras(20),
            RetornoGabSgdi = DiasAtras(10),
            RetornoOrgaoNaoSeAplica = true
        };

        Assert.Equal(CtrDominios.Situacao.AnaliseConcluida, CtrProcessoService.CalcularSituacao(processo));
    }

    [Fact]
    public void CalcularSituacao_RetornoDispensadoSemRetornoAoGab_NaoConclui()
    {
        // Marcar "não se aplica" sozinho não conclui nada: o processo segue onde está
        var naUgtic = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            ChegadaSubgd = DiasAtras(25),
            ChegadaUgtic = DiasAtras(20),
            RetornoOrgaoNaoSeAplica = true
        };
        Assert.Equal(CtrDominios.Situacao.EmAnaliseUgtic, CtrProcessoService.CalcularSituacao(naUgtic));

        var semNada = new CtrProcesso { RetornoOrgaoNaoSeAplica = true };
        Assert.Equal(CtrDominios.Situacao.SemMovimentacao, CtrProcessoService.CalcularSituacao(semNada));
    }

    [Fact]
    public void CalcularSituacao_RestituidoVenceORetornoDispensado()
    {
        var processo = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            RetornoGabSgdi = DiasAtras(10),
            RetornoOrgaoNaoSeAplica = true,
            Restituido = true,
            RestituidoEm = DiasAtras(5),
            RestituidoMotivo = "Devolvido por falta de documentos"
        };

        Assert.Equal(CtrDominios.Situacao.Restituido, CtrProcessoService.CalcularSituacao(processo));
    }

    [Fact]
    public async Task Filtro_Concluido_IncluiORetornoDispensado()
    {
        // Concluído "clássico" (com data de retorno ao órgão)
        SemearProcesso("04044-00000101/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(12);
            p.RetornoOrgao = DiasAtras(6);
        });

        // Concluído pela devolução dispensada
        SemearProcesso("04044-00000102/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(11);
            p.RetornoOrgaoNaoSeAplica = true;
        });

        // Dispensado, mas ainda sem retorno ao Gab: NÃO é concluído
        SemearProcesso("04044-00000103/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.ChegadaSubgd = DiasAtras(25);
            p.RetornoOrgaoNaoSeAplica = true;
        });

        var concluidos = await _processos.ListarAsync(
            new CtrProcessoFiltro { Situacao = CtrDominios.Situacao.AnaliseConcluida });
        Assert.Equal(2, concluidos.TotalItems);

        var naGab = await _processos.ListarAsync(
            new CtrProcessoFiltro { Situacao = CtrDominios.Situacao.RetornadoGabSgdi });
        Assert.Equal(0, naGab.TotalItems); // os dois com retorno ao Gab já concluíram

        var naSubgd = await _processos.ListarAsync(
            new CtrProcessoFiltro { Situacao = CtrDominios.Situacao.EmAnaliseSubgd });
        Assert.Equal("04044-00000103/2026-13", Assert.Single(naSubgd.Items).NumeroProcesso);
    }

    [Fact]
    public async Task Checkpoint_RetornoOrgao_NaoSeAplicaLimpaADataEConclui()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000110/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(10);
            p.RetornoOrgao = DiasAtras(4);
        });

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.RetornoOrgao,
            NaoSeAplica = true
        }, ctx);

        Assert.True(resposta.RetornoOrgaoNaoSeAplica);
        Assert.Null(resposta.RetornoOrgao);
        Assert.Equal(CtrDominios.Situacao.AnaliseConcluida, resposta.Situacao);
    }

    [Fact]
    public async Task Checkpoint_RetornoOrgao_InformarADataDesmarcaONaoSeAplica()
    {
        // Mesma simetria da correção B7 (UGTIC): informar a data reativa a etapa
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000111/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(10);
            p.RetornoOrgaoNaoSeAplica = true;
        });

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.RetornoOrgao,
            Data = DiasAtras(2)
        }, ctx);

        Assert.False(resposta.RetornoOrgaoNaoSeAplica);
        Assert.Equal(DiasAtras(2), resposta.RetornoOrgao);
        Assert.Equal(CtrDominios.Situacao.AnaliseConcluida, resposta.Situacao);
    }

    [Fact]
    public async Task Checkpoint_NaoSeAplica_ContinuaRecusadoNasDemaisEtapas()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000112/2026-13");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
            {
                Etapa = CtrDominios.Etapa.ChegadaSubgd,
                NaoSeAplica = true
            }, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("Não se aplica", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_RetornoDispensadoComData_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000113/2026-14");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.RetornoOrgao = DiasAtras(2);
        dto.RetornoOrgaoNaoSeAplica = true;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("não se aplica", ex.Error.Message);
    }

    [Fact]
    public async Task Atualizar_DesmarcarNaoSeAplica_NaoApagaADataJaGravada()
    {
        // Desmarcar é decisão do usuário: a data que ele informar fica
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000114/2026-15", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(10);
        });

        var dto = NovoProcessoDto("04044-00000114/2026-15");
        dto.ChegadaSgdi = DiasAtras(30);
        dto.RetornoGabSgdi = DiasAtras(10);
        dto.RetornoOrgao = DiasAtras(3);
        dto.RetornoOrgaoNaoSeAplica = false;

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.False(resposta.RetornoOrgaoNaoSeAplica);
        Assert.Equal(DiasAtras(3), resposta.RetornoOrgao);
    }

    // ══ 2.1 Assinatura do contrato: a data FINAL do trâmite ═══════════════════
    // (Desde 2026-09-22 substitui a "fase" derivada: assinado = Concluído.)

    [Fact]
    public void CalcularSituacao_AssinaturaDoContrato_ConcluiOProcesso()
    {
        Assert.Equal(CtrDominios.Situacao.SemMovimentacao,
            CtrProcessoService.CalcularSituacao(new CtrProcesso()));

        Assert.Equal(CtrDominios.Situacao.Concluido,
            CtrProcessoService.CalcularSituacao(new CtrProcesso { DataAssinaturaContrato = DiasAtras(1) }));

        // Vence tudo: trâmite no meio, restituição e devolução ao órgão
        var noMeio = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            ChegadaSubgd = DiasAtras(20),
            DataAssinaturaContrato = DiasAtras(1)
        };
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(noMeio));

        var restituido = new CtrProcesso
        {
            ChegadaSgdi = DiasAtras(30),
            Restituido = true,
            RestituidoEm = DiasAtras(10),
            RestituidoMotivo = "Devolvido por falta de documentos",
            DataAssinaturaContrato = DiasAtras(1)
        };
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(restituido));

        // A etapa do planejamento NÃO é anulada pela assinatura: é onde ele parou
        var assinado = new CtrProcesso
        {
            EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr,
            DataAssinaturaContrato = DiasAtras(1)
        };
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(assinado));
        Assert.Equal(CtrDominios.EtapaPlanejamento.Tr, assinado.EtapaPlanejamento);
    }

    [Fact]
    public async Task Criar_ComEtapaEAssinatura_DevolveConcluido()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000120/2026-11");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Etp;

        var emAnalise = await _processos.CriarAsync(dto, ctx);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, emAnalise.Situacao);
        Assert.Equal(CtrDominios.EtapaPlanejamento.Etp, emAnalise.EtapaPlanejamento);

        dto.NumeroProcesso = "04044-00000121/2026-12";
        dto.DataAssinaturaContrato = DiasAtras(2);
        var concluido = await _processos.CriarAsync(dto, ctx);
        Assert.Equal(CtrDominios.Situacao.Concluido, concluido.Situacao);
        Assert.Equal(DiasAtras(2), concluido.DataAssinaturaContrato);
        Assert.Equal(CtrDominios.EtapaPlanejamento.Etp, concluido.EtapaPlanejamento);
    }

    [Fact]
    public async Task Criar_AssinaturaAnteriorAoTramite_EhAceita()
    {
        // O TCDF também analisa contrato JÁ assinado: a assinatura não entra na
        // cronologia dos checkpoints
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000122/2026-13");
        dto.ChegadaSgdi = DiasAtras(10);
        dto.DataAssinaturaContrato = DiasAtras(200);

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Situacao.Concluido, resposta.Situacao);
        Assert.Equal(DiasAtras(200), resposta.DataAssinaturaContrato);
    }

    [Fact]
    public async Task Criar_AssinaturaFuturaOuEtapaForaDoDominio_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();

        var futura = NovoProcessoDto("04044-00000123/2026-14");
        futura.DataAssinaturaContrato = Hoje.AddDays(1);
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(futura, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex1.Error.Code);
        Assert.Contains("assinatura", ex1.Error.Message);

        var etapa = NovoProcessoDto("04044-00000124/2026-15");
        etapa.EtapaPlanejamento = "EDITAL";
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(etapa, ctx));
        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex2.Error.Code);
        Assert.Contains("Etapa do planejamento", ex2.Error.Message);
    }

    [Fact]
    public async Task Filtro_Etapa_ForaDoDominioDevolveVazio_EConcluidoSoEntraQuandoPedido()
    {
        SemearProcesso("04044-00000130/2026-11", p => p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Dfd);
        SemearProcesso("04044-00000131/2026-12", p =>
        {
            p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr;
            p.DataAssinaturaContrato = DiasAtras(5);
        });

        var porEtapa = await _processos.ListarAsync(
            new CtrProcessoFiltro { EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Dfd });
        Assert.Equal("04044-00000130/2026-11", Assert.Single(porEtapa.Items).NumeroProcesso);

        // O assinado está Concluído: fora da lista por padrão, dentro quando pedido
        var porEtapaTr = await _processos.ListarAsync(
            new CtrProcessoFiltro { EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr });
        Assert.Equal(0, porEtapaTr.TotalItems);

        var comConcluidos = await _processos.ListarAsync(new CtrProcessoFiltro
        { EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr, IncluirConcluidos = true });
        Assert.Equal("04044-00000131/2026-12", Assert.Single(comConcluidos.Items).NumeroProcesso);

        // Regra B5: o que o servidor não entende vira lista vazia, nunca "todos"
        var etapaInvalida = await _processos.ListarAsync(new CtrProcessoFiltro { EtapaPlanejamento = "EDITAL" });
        Assert.Equal(0, etapaInvalida.TotalItems);
    }

    [Fact]
    public async Task Painel_Concluidos_TrazTotalERelacaoDoMaisRecentePrimeiro()
    {
        SemearProcesso("04044-00000140/2026-11");
        SemearProcesso("04044-00000141/2026-12", p => p.DataAssinaturaContrato = DiasAtras(3));
        SemearProcesso("04044-00000142/2026-13", p => p.DataAssinaturaContrato = DiasAtras(9));

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(3, painel.TotalAtivos);
        Assert.Equal(2, painel.TotalConcluidos);
        Assert.Equal(new[] { "04044-00000141/2026-12", "04044-00000142/2026-13" },
            painel.Concluidos.Select(c => c.NumeroProcesso).ToArray());
        Assert.All(painel.Concluidos, c => Assert.Equal(CtrDominios.Situacao.Concluido, c.Situacao));
        Assert.Equal(2, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.Concluido).Quantidade);
        // Concluído não é gargalo, por mais parado que esteja
        Assert.DoesNotContain(painel.Gargalos, g => g.DataAssinaturaContrato != null);
    }

    [Fact]
    public async Task Painel_SemConcluidos_TrazTotalZeroERelacaoVazia()
    {
        SemearProcesso("04044-00000143/2026-14");

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(0, painel.TotalConcluidos);
        Assert.Empty(painel.Concluidos);
        Assert.Null(painel.TemposMedios.ChegadaParaAssinatura);
        Assert.Equal(0, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.Concluido).Quantidade);
    }

    // ══ 2.2 Criticidade no processo ═══════════════════════════════════════════

    [Fact]
    public async Task Manifestacao_IncisoI_SemCriticidadeNoProcesso_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000150/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = null; // o dado agora nasce no cadastro do processo
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Equal("Defina a criticidade no cadastro do processo antes de registrar a manifestação do inciso I.",
            ex.Error.Message);
    }

    [Fact]
    public async Task Manifestacao_IncisoII_SemCriticidadeNoProcesso_EhAceita()
    {
        // A exigência é só do inciso I (é ele que reporta a criticidade)
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000151/2026-12", p => p.Criticidade = null);

        var resposta = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoII(), ctx);

        Assert.Equal(CtrDominios.Estagio.NotificacaoRegularizar, resposta.Estagio);
        Assert.Null(resposta.Criticidade);
    }

    [Fact]
    public async Task Manifestacao_Response_EspelhaACriticidadeDoProcesso()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000152/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Media;
        });

        var criada = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);
        Assert.Equal(CtrDominios.Criticidade.Media, criada.Criticidade);

        // Mudar a criticidade NO PROCESSO muda o espelho de todas as manifestações
        var dto = CtrProcessoService.DtoDe(processo);
        dto.Criticidade = CtrDominios.Criticidade.Baixa;
        await _processos.AtualizarAsync(processo.Id, new CtrProcessoUpdateDTO
        {
            NumeroProcesso = dto.NumeroProcesso,
            OrgaoNome = dto.OrgaoNome,
            OrgaoSigla = dto.OrgaoSigla,
            Objeto = dto.Objeto,
            CategoriaObjeto = dto.CategoriaObjeto,
            ChegadaSgdi = dto.ChegadaSgdi,
            Criticidade = CtrDominios.Criticidade.Baixa
        }, ctx);

        var relida = await _manifestacoes.GetAsync(criada.Id);
        Assert.Equal(CtrDominios.Criticidade.Baixa, relida.Criticidade);
    }

    [Fact]
    public async Task Processo_CriticidadeForaDoDominio_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000153/2026-14");
        dto.Criticidade = "Altíssima";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("Criticidade", ex.Error.Message);
    }

    [Fact]
    public async Task Filtro_Criticidade_FiltraEForaDoDominioDevolveVazio()
    {
        SemearProcesso("04044-00000154/2026-15", p => p.Criticidade = CtrDominios.Criticidade.Alta);
        SemearProcesso("04044-00000155/2026-16", p => p.Criticidade = CtrDominios.Criticidade.Baixa);

        var altas = await _processos.ListarAsync(
            new CtrProcessoFiltro { Criticidade = CtrDominios.Criticidade.Alta });
        Assert.Equal("04044-00000154/2026-15", Assert.Single(altas.Items).NumeroProcesso);

        var invalida = await _processos.ListarAsync(new CtrProcessoFiltro { Criticidade = "Altíssima" });
        Assert.Equal(0, invalida.TotalItems);
    }

    [Fact]
    public async Task DespachoPdf_ImprimeACriticidadeDoProcesso()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000156/2026-17", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Baixa;
        });
        var manifestacao = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var pdfBaixa = await _manifestacoes.GerarDespachoPdfAsync(manifestacao.Id, "Brasília", "Fulano", "Diretor");

        // Trocando a criticidade NO PROCESSO o despacho muda (o texto vem de lá)
        processo.Criticidade = CtrDominios.Criticidade.Media;
        Context.SaveChanges();
        var pdfMedia = await _manifestacoes.GerarDespachoPdfAsync(manifestacao.Id, "Brasília", "Fulano", "Diretor");

        Assert.NotEmpty(pdfBaixa);
        Assert.NotEqual(pdfBaixa.Length, pdfMedia.Length);
    }

    // ══ 3. Status do processo no TCDF ═════════════════════════════════════════

    [Fact]
    public void CalcularEstagio_StatusTcdf_TemPrecedencia()
    {
        // Vale em qualquer inciso e com qualquer resultado
        var incisoI = new CtrManifestacaoTcdf
        {
            SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
            ResultadoAnalise = CtrDominios.ResultadoAnalise.Alinhada,
            StatusTcdf = CtrDominios.StatusTcdf.EditalRevogado
        };
        Assert.Equal(CtrDominios.Estagio.EditalRevogado, CtrManifestacaoService.CalcularEstagio(incisoI));

        var incisoII = new CtrManifestacaoTcdf
        {
            SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
            PrazoRegularizacaoDias = 30,
            StatusTcdf = CtrDominios.StatusTcdf.SuspensoIrregularidades
        };
        Assert.Equal(CtrDominios.Estagio.SuspensoIrregularidades,
            CtrManifestacaoService.CalcularEstagio(incisoII));

        var riscos = new CtrManifestacaoTcdf
        {
            SituacaoPortfolio = CtrDominios.SituacaoPortfolio.ComunicadaPreviamente,
            ResultadoAnalise = CtrDominios.ResultadoAnalise.RiscosSignificativos,
            DesfechoRisco = CtrDominios.DesfechoRisco.AguardandoResposta,
            StatusTcdf = CtrDominios.StatusTcdf.SuspensoIrregularidades
        };
        Assert.Equal(CtrDominios.Estagio.SuspensoIrregularidades,
            CtrManifestacaoService.CalcularEstagio(riscos));
    }

    [Fact]
    public async Task Manifestacao_ComStatusTcdf_ApareceNoFiltroPorEstagio()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000160/2026-11", p => p.ChegadaSgdi = DiasAtras(20));

        var comStatus = NovaManifestacaoIncisoI();
        comStatus.OficioTcdf = "700/2026-GAB";
        comStatus.StatusTcdf = CtrDominios.StatusTcdf.SuspensoIrregularidades;
        var suspensa = await _manifestacoes.CriarAsync(processo.Id, comStatus, ctx);
        Assert.Equal(CtrDominios.Estagio.SuspensoIrregularidades, suspensa.Estagio);

        var semStatus = NovaManifestacaoIncisoI();
        semStatus.OficioTcdf = "701/2026-GAB";
        await _manifestacoes.CriarAsync(processo.Id, semStatus, ctx);

        var suspensas = await _manifestacoes.ListarAsync(
            new CtrManifestacaoFiltro { Estagio = CtrDominios.Estagio.SuspensoIrregularidades });
        Assert.Equal("700/2026-GAB", Assert.Single(suspensas.Items).OficioTcdf);

        // O estágio de análise SÓ traz quem não tem status (mesma precedência)
        var alinhadas = await _manifestacoes.ListarAsync(
            new CtrManifestacaoFiltro { Estagio = CtrDominios.Estagio.Alinhada });
        Assert.Equal("701/2026-GAB", Assert.Single(alinhadas.Items).OficioTcdf);

        var revogados = await _manifestacoes.ListarAsync(
            new CtrManifestacaoFiltro { Estagio = CtrDominios.Estagio.EditalRevogado });
        Assert.Equal(0, revogados.TotalItems);
    }

    [Fact]
    public async Task Manifestacao_StatusTcdfForaDoDominio_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000161/2026-12", p => p.ChegadaSgdi = DiasAtras(20));

        var dto = NovaManifestacaoIncisoI();
        dto.StatusTcdf = "Cancelado";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _manifestacoes.CriarAsync(processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("Status no TCDF", ex.Error.Message);
    }

    [Fact]
    public async Task DespachoPdf_NaoImprimeOStatusTcdf()
    {
        // Mesma decisão já tomada para a Observação: o template do TCDF não tem o
        // campo. O PDF sai IDÊNTICO em tamanho com e sem status.
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000162/2026-13", p => p.ChegadaSgdi = DiasAtras(20));

        var semStatus = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);
        var pdfSem = await _manifestacoes.GerarDespachoPdfAsync(semStatus.Id, "Brasília", "Fulano", "Diretor");

        var comStatus = NovaManifestacaoIncisoI();
        comStatus.StatusTcdf = CtrDominios.StatusTcdf.SuspensoIrregularidades;
        await _manifestacoes.AtualizarAsync(semStatus.Id, comStatus, ctx);
        var pdfCom = await _manifestacoes.GerarDespachoPdfAsync(semStatus.Id, "Brasília", "Fulano", "Diretor");

        Assert.Equal(pdfSem.Length, pdfCom.Length);
    }

    // ══ 4. Origem (órgão comunicante × TCDF) ══════════════════════════════════

    [Fact]
    public async Task Criar_SemOrigem_NasceComoOrgaoComunicante()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000170/2026-11");
        dto.Origem = null;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, resposta.Origem);
    }

    [Fact]
    public async Task Criar_ProcessoDoTcdf_SemTramiteEJaAssinado_EhAceito()
    {
        // O caso típico: análise interna do Tribunal sobre contrato já assinado
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000171/2026-12");
        dto.Origem = CtrDominios.Origem.Tcdf;
        dto.DataAssinaturaContrato = DiasAtras(90);

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Origem.Tcdf, resposta.Origem);
        // Já assinado = já Concluído (só aparece na lista quando pedido); nasce sem trâmite
        Assert.Equal(CtrDominios.Situacao.Concluido, resposta.Situacao);
        Assert.Null(resposta.ChegadaSgdi);
    }

    [Fact]
    public async Task Criar_OrigemForaDoDominio_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000172/2026-13");
        dto.Origem = "Ministério Público";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("Origem", ex.Error.Message);
    }

    [Fact]
    public async Task Filtro_Origem_FiltraEForaDoDominioDevolveVazio()
    {
        SemearProcesso("04044-00000173/2026-14");
        SemearProcesso("04044-00000174/2026-15", p => p.Origem = CtrDominios.Origem.Tcdf);

        var doTcdf = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.Tcdf });
        Assert.Equal("04044-00000174/2026-15", Assert.Single(doTcdf.Items).NumeroProcesso);

        var doOrgao = await _processos.ListarAsync(
            new CtrProcessoFiltro { Origem = CtrDominios.Origem.OrgaoComunicante });
        Assert.Equal("04044-00000173/2026-14", Assert.Single(doOrgao.Items).NumeroProcesso);

        var invalida = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = "TCU" });
        Assert.Equal(0, invalida.TotalItems);
    }

    // ══ 5. CSV — as 4 colunas novas e o "-" no retorno ao órgão ═══════════════

    [Fact]
    public void Csv_Cabecalho_TrazAsQuatroColunasDaRodadaNaPosicaoContratada()
    {
        // As 4 desta rodada vêm logo depois das 15 originais (as 3 do esclarecimento,
        // da rodada seguinte, entram DEPOIS delas — a ordem já existente é preservada)
        Assert.Equal(
            new[] { "Etapa do planejamento", "Assinatura do contrato", "Criticidade", "Origem" },
            CtrCsv.Cabecalho[15..19]);
    }

    [Fact]
    public void Csv_Escrever_UsaOTracoNoRetornoDispensado()
    {
        var bytes = CtrCsv.Escrever(new List<CtrProcessoResponse>
        {
            new()
            {
                NumeroProcesso = "04044-00000180/2026-11",
                OrgaoNome = "Economia",
                OrgaoSigla = "SEEC",
                Objeto = "Switches",
                CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
                ChegadaSgdi = new DateOnly(2026, 5, 22),
                RetornoGabSgdi = new DateOnly(2026, 6, 10),
                RetornoOrgaoNaoSeAplica = true,
                EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr,
                DataAssinaturaContrato = new DateOnly(2026, 7, 1),
                Criticidade = CtrDominios.Criticidade.Alta,
                Origem = CtrDominios.Origem.Tcdf
            }
        });

        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var linha = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];
        var celulas = linha.Split(';');

        Assert.Equal("-", celulas[10]);  // Data de Retorno ao Órgão Comunicante
        Assert.Equal("TR", celulas[15]);
        Assert.Equal("01/07/2026", celulas[16]);
        Assert.Equal("Alta", celulas[17]);
        Assert.Equal("TCDF", celulas[18]);
    }

    [Fact]
    public void Csv_Ler_TracoNoRetornoAoOrgaoMarcaNaoSeAplica()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000181/2026-12;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "22/05/2026;01/06/2026;;10/06/2026;-;;Não;;;TR;01/07/2026;Alta;TCDF\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Erro);
        Assert.True(linha.Dados!.RetornoOrgaoNaoSeAplica);
        Assert.Null(linha.Dados.RetornoOrgao);
        Assert.Equal(CtrDominios.EtapaPlanejamento.Tr, linha.Dados.EtapaPlanejamento);
        Assert.Equal(new DateOnly(2026, 7, 1), linha.Dados.DataAssinaturaContrato);
        Assert.Equal(CtrDominios.Criticidade.Alta, linha.Dados.Criticidade);
        Assert.Equal(CtrDominios.Origem.Tcdf, linha.Dados.Origem);
    }

    [Fact]
    public void Csv_Ler_ColunasNovasAusentesOuVazias_MantemOComportamentoAntigo()
    {
        // Planilha legada (12 colunas): nada de novo é inventado
        var legada = "Analises de contratações;;;;;;;;;;;\r\n"
            + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
            + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
            + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
            + "04044-00000182/2026-13;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(legada)));

        Assert.Null(linha.Erro);
        Assert.Null(linha.Dados!.EtapaPlanejamento);
        Assert.Null(linha.Dados.DataAssinaturaContrato);
        Assert.Null(linha.Dados.Criticidade);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, linha.Dados.Origem);
        Assert.False(linha.Dados.RetornoOrgaoNaoSeAplica);

        // Colunas presentes, porém vazias: idem
        var vazias = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000183/2026-14;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "22/05/2026;;;;;;Não;;;;;;\r\n";

        var outra = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(vazias)));
        Assert.Null(outra.Erro);
        Assert.Null(outra.Dados!.EtapaPlanejamento);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, outra.Dados.Origem);
    }

    [Theory]
    [InlineData("EDITAL;;;", "etapa do planejamento desconhecida")]
    [InlineData(";30/02/2026;;", "data inválida em Assinatura do contrato")]
    [InlineData(";;Altíssima;", "criticidade desconhecida")]
    [InlineData(";;;TCU", "origem desconhecida")]
    public void Csv_Ler_ValorForaDoDominioNasColunasNovas_RejeitaNomeandoAColuna(
        string cauda, string motivoEsperado)
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000184/2026-15;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "22/05/2026;;;;;;Não;;;" + cauda + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Dados);
        Assert.Contains(motivoEsperado, linha.Erro);
    }

    [Fact]
    public async Task Csv_RoundTrip_ComOsCamposNovos_ClassificaTudoComoAtualizarSemDiferenca()
    {
        var ctx = await ContextoAnalistaAsync();

        SemearProcesso("04044-00000190/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.RetornoGabSgdi = DiasAtras(20);
            p.RetornoOrgaoNaoSeAplica = true;
            p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Dfd;
            p.DataAssinaturaContrato = DiasAtras(5);
            p.Criticidade = CtrDominios.Criticidade.Media;
            p.Origem = CtrDominios.Origem.Tcdf;
        });

        SemearProcesso("04044-00000191/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.ChegadaSubgd = DiasAtras(28);
            p.UgticNaoSeAplica = true;
            p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Etp;
            p.Criticidade = CtrDominios.Criticidade.Baixa;
        });

        // O assinado está Concluído: o export só o traz quando pedido, como a lista
        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { PageSize = 50, IncluirConcluidos = true });
        var previa = await NovoImportacaoService().PreviaAsync(bytes);

        Assert.Equal(2, previa.TotalLinhas);
        Assert.All(previa.Linhas, l => Assert.Equal(CtrImportacaoAcao.Atualizar, l.Acao));
        Assert.All(previa.Linhas, l => Assert.Null(l.Motivo));

        // Campo a campo: o arquivo exportado descreve exatamente o que está gravado
        foreach (var linha in previa.Linhas)
        {
            var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == linha.NumeroProcesso);
            var lido = linha.Dados!;

            Assert.Equal(gravado.RetornoOrgao, lido.RetornoOrgao);
            Assert.Equal(gravado.RetornoOrgaoNaoSeAplica, lido.RetornoOrgaoNaoSeAplica);
            Assert.Equal(gravado.UgticNaoSeAplica, lido.UgticNaoSeAplica);
            Assert.Equal(gravado.EtapaPlanejamento, lido.EtapaPlanejamento);
            Assert.Equal(gravado.DataAssinaturaContrato, lido.DataAssinaturaContrato);
            Assert.Equal(gravado.Criticidade, lido.Criticidade);
            Assert.Equal(gravado.Origem, lido.Origem);
        }

        // E a reimportação de fato não muda nada (idempotência)
        await NovoImportacaoService().ImportarAsync(bytes, ctx);
        var tcdf = Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000190/2026-11");
        Assert.Equal(CtrDominios.Origem.Tcdf, tcdf.Origem);
        Assert.True(tcdf.RetornoOrgaoNaoSeAplica);
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(tcdf));
    }

    // ══ 6. Coluna AUSENTE × coluna VAZIA na importação ════════════════════════
    // Defeito de PERDA DE DADOS confirmado ao vivo: reimportar a planilha real (12
    // colunas) zerava criticidade, etapa, assinatura e origem digitadas na tela —
    // inclusive a criticidade que o backfill da migration tinha gravado.

    /// <summary>Planilha legada de 12 colunas, como a planilha real da equipe.</summary>
    private static byte[] PlanilhaLegada(params string[] linhas)
    {
        var csv = "Analises de contratações;;;;;;;;;;;\r\n"
            + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
            + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
            + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
            + string.Join("\r\n", linhas) + "\r\n";

        return Bytes1252(csv);
    }

    [Fact]
    public async Task Importar_PlanilhaDe12Colunas_PreservaOsCamposDaRodadaEAtualizaORestante()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000200/2026-11", p =>
        {
            p.OrgaoNome = "Nome antigo";
            p.ChegadaSgdi = DiasAtras(50);
            p.Observacao = "Observação antiga";
            p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Etp;
            p.DataAssinaturaContrato = DiasAtras(15);
            p.Criticidade = CtrDominios.Criticidade.Alta;
            p.Origem = CtrDominios.Origem.Tcdf;
        });

        var bytes = PlanilhaLegada(
            "04044-00000200/2026-11;Secretaria de Estado de Economia;SEEC;;Switches novos;"
            + "Infraestrutura de Rede;01/02/2026;;;;;Observação nova");

        // A prévia já mostra o resultado real (nada de "vai apagar")
        var previa = await NovoImportacaoService().PreviaAsync(bytes);
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(CtrImportacaoAcao.Atualizar, linha.Acao);
        Assert.Equal(CtrDominios.EtapaPlanejamento.Etp, linha.Dados!.EtapaPlanejamento);
        Assert.Equal(CtrDominios.Criticidade.Alta, linha.Dados.Criticidade);
        Assert.Equal(CtrDominios.Origem.Tcdf, linha.Dados.Origem);

        await NovoImportacaoService().ImportarAsync(bytes, ctx);

        // Os 4 campos da rodada da chefia: PRESERVADOS (o arquivo nem tem as colunas)
        Assert.Equal(CtrDominios.EtapaPlanejamento.Etp, processo.EtapaPlanejamento);
        Assert.Equal(DiasAtras(15), processo.DataAssinaturaContrato);
        Assert.Equal(CtrDominios.Criticidade.Alta, processo.Criticidade);
        Assert.Equal(CtrDominios.Origem.Tcdf, processo.Origem);

        // As 12 colunas originais: atualizadas normalmente (a planilha é a fonte)
        Assert.Equal("Secretaria de Estado de Economia", processo.OrgaoNome);
        Assert.Equal("Switches novos", processo.Objeto);
        Assert.Equal(new DateOnly(2026, 2, 1), processo.ChegadaSgdi);
        Assert.Equal("Observação nova", processo.Observacao);
    }

    [Fact]
    public async Task Importar_PlanilhaReal_NaoApagaACriticidadeDoBackfill()
    {
        // A repro exata do defeito: planilha real da equipe (12 colunas, Windows-1252)
        // sobre um processo que já tem criticidade — o caso do backfill da migration.
        var ctx = await ContextoAnalistaAsync();
        var importacao = NovoImportacaoService();

        await importacao.ImportarAsync(PlanilhaReal(), ctx);

        var processo = Context.CtrProcessos.First(p => p.NumeroProcesso == "04044-00002545/2024-62");
        processo.Criticidade = CtrDominios.Criticidade.Alta;   // como o backfill (ou a tela) gravaria
        processo.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Tr;
        Context.SaveChanges();

        // Reimportar o MESMO arquivo não pode zerar nada disso
        var relatorio = await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);

        Assert.Equal(0, relatorio.Criados);
        Assert.Equal(37, relatorio.Atualizados);
        Assert.Equal(0, relatorio.Rejeitados);
        Assert.Equal(CtrDominios.Criticidade.Alta, processo.Criticidade);
        Assert.Equal(CtrDominios.EtapaPlanejamento.Tr, processo.EtapaPlanejamento);
    }

    [Fact]
    public async Task Importar_ColunasPresentesEVazias_LimpamOsCampos()
    {
        // Coluna PRESENTE e vazia é escolha explícita de quem exportou: limpa
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000201/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(50);
            p.EtapaPlanejamento = CtrDominios.EtapaPlanejamento.Etp;
            p.DataAssinaturaContrato = DiasAtras(15);
            p.Criticidade = CtrDominios.Criticidade.Alta;
            p.Origem = CtrDominios.Origem.Tcdf;
        });

        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000201/2026-12;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;;;\r\n";

        await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);

        Assert.Null(processo.EtapaPlanejamento);
        Assert.Null(processo.DataAssinaturaContrato);
        Assert.Null(processo.Criticidade);
        // Origem é NOT NULL: célula vazia volta ao default do domínio
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, processo.Origem);
    }

    [Fact]
    public async Task Importar_ProcessoNovoDePlanilhaDe12Colunas_NasceComOsDefaults()
    {
        var ctx = await ContextoAnalistaAsync();

        var bytes = PlanilhaLegada(
            "04044-00000202/2026-13;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;");

        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);

        Assert.Equal(1, relatorio.Criados);
        var criado = Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000202/2026-13");
        Assert.Null(criado.EtapaPlanejamento);
        Assert.Null(criado.DataAssinaturaContrato);
        Assert.Null(criado.Criticidade);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, criado.Origem);
    }

    [Fact]
    public void Csv_Ler_ColunasOpcionais_RefletemOCabecalhoDoArquivo()
    {
        var legada = CtrCsv.Ler(PlanilhaLegada(
            "04044-00000203/2026-14;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;"));
        var opcionaisLegada = Assert.Single(legada).ColunasOpcionais;
        Assert.False(opcionaisLegada.EtapaPlanejamento);
        Assert.False(opcionaisLegada.DataAssinaturaContrato);
        Assert.False(opcionaisLegada.Criticidade);
        Assert.False(opcionaisLegada.Origem);

        var completa = CtrCsv.Ler(BytesUtf8ComBom(string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000204/2026-15;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;;;\r\n"));
        var opcionaisCompleta = Assert.Single(completa).ColunasOpcionais;
        Assert.True(opcionaisCompleta.EtapaPlanejamento);
        Assert.True(opcionaisCompleta.DataAssinaturaContrato);
        Assert.True(opcionaisCompleta.Criticidade);
        Assert.True(opcionaisCompleta.Origem);
        Assert.True(opcionaisCompleta.Esclarecimento);
        Assert.False(opcionaisLegada.Esclarecimento);
    }

    [Fact]
    public void Csv_Ler_ColunaAusenteComLixoNaLinhaDeDados_NaoInventaColuna()
    {
        // O cabeçalho é a autoridade: célula a mais numa linha de dados é ignorada,
        // e não vira "coluna presente com valor fora do domínio"
        var bytes = PlanilhaLegada(
            "04044-00000205/2026-16;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;;"
            + "Não;;;EDITAL;;Altíssima;TCU");

        var linha = Assert.Single(CtrCsv.Ler(bytes));

        Assert.Null(linha.Erro);
        Assert.Null(linha.Dados!.EtapaPlanejamento);
        Assert.Null(linha.Dados.Criticidade);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, linha.Dados.Origem);
    }
}
