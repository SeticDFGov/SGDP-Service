using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Rodada de esclarecimentos e pendências: pedido de esclarecimento ao órgão
/// (parte A1), pendências apontadas pelo TCDF (A2) e as correções da revisão
/// adversarial que mudam comportamento do servidor (A1/A2/M1/B3 da parte B,
/// mais a trava do `NaoSeAplica` nas cinco etapas do checkpoint).
/// </summary>
[Collection(CtrDespachoPdfCollection.Nome)]
public class CtrEsclarecimentosPendenciasTest : CtrTestBase
{
    private readonly CtrProcessoService _processos;
    private readonly CtrManifestacaoService _manifestacoes;

    public CtrEsclarecimentosPendenciasTest()
    {
        _processos = NovoProcessoService();
        _manifestacoes = NovoManifestacaoService();
    }

    // ══ A1. Pedido de esclarecimentos ao órgão ════════════════════════════════

    [Fact]
    public void CalcularEsclarecimentoPendente_SaiDasDuasDatas()
    {
        Assert.False(CtrProcessoService.CalcularEsclarecimentoPendente(new CtrProcesso()));

        Assert.True(CtrProcessoService.CalcularEsclarecimentoPendente(new CtrProcesso
        {
            EsclarecimentoSolicitadoEm = DiasAtras(7),
            EsclarecimentoDescricao = "Detalhar a memória de cálculo"
        }));

        // Respondido = não há mais pendência
        Assert.False(CtrProcessoService.CalcularEsclarecimentoPendente(new CtrProcesso
        {
            EsclarecimentoSolicitadoEm = DiasAtras(7),
            EsclarecimentoDescricao = "Detalhar a memória de cálculo",
            EsclarecimentoRespondidoEm = DiasAtras(2)
        }));
    }

    [Fact]
    public void CalcularDiasEsclarecimentoPendente_ContaDoPedidoAteHoje()
    {
        Assert.Equal(7, CtrProcessoService.CalcularDiasEsclarecimentoPendente(DiasAtras(7), null, Hoje));
        Assert.Null(CtrProcessoService.CalcularDiasEsclarecimentoPendente(DiasAtras(7), DiasAtras(1), Hoje));
        Assert.Null(CtrProcessoService.CalcularDiasEsclarecimentoPendente(null, null, Hoje));
    }

    [Fact]
    public async Task Criar_ComPedidoDeEsclarecimento_DerivaPendenciaEDias()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000300/2026-11");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.EsclarecimentoSolicitadoEm = DiasAtras(9);
        dto.EsclarecimentoDescricao = "  Detalhar a memória de cálculo do quantitativo  ";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.True(resposta.EsclarecimentoPendente);
        Assert.Equal(9, resposta.DiasEsclarecimentoPendente);
        Assert.Equal("Detalhar a memória de cálculo do quantitativo", resposta.EsclarecimentoDescricao);
        // A situação do trâmite NÃO muda: esclarecimento é sinal paralelo
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, resposta.Situacao);
    }

    [Fact]
    public async Task Criar_ComPedidoSemDescricao_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000301/2026-12");
        dto.EsclarecimentoSolicitadoEm = DiasAtras(5);
        dto.EsclarecimentoDescricao = "   ";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("o que foi pedido", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_RespostaSemPedido_EhRecusada()
    {
        // A resposta é fato datado: anulá-la em silêncio (como era) escondia perda
        // de dado, sobretudo na importação
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000302/2026-13");
        dto.EsclarecimentoSolicitadoEm = null;
        dto.EsclarecimentoDescricao = "Texto órfão";
        dto.EsclarecimentoRespondidoEm = DiasAtras(3);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Equal("Informe a data do pedido de esclarecimento antes de registrar a resposta.",
            ex.Error.Message);
    }

    [Fact]
    public async Task Atualizar_RespostaSemPedido_EhRecusadaESemRastro()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000307/2026-18", p => p.ChegadaSgdi = DiasAtras(20));

        var dto = NovoProcessoDto("04044-00000307/2026-18");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.EsclarecimentoRespondidoEm = DiasAtras(3);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(processo.Id, dto, ctx));

        Assert.Contains("antes de registrar a resposta", ex.Error.Message);
        // A disciplina do candidato vale aqui também: nada ficou na entidade rastreada
        Assert.Null(processo.EsclarecimentoRespondidoEm);
        Assert.Null(processo.AlteradoEm);
    }

    [Fact]
    public async Task Criar_LimparADataDoPedido_ContinuaAnulandoADescricao()
    {
        // A normalização que É gesto explícito do usuário fica: sem a data do pedido,
        // a descrição que sobrou no formulário some (não vira erro)
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000308/2026-19");
        dto.EsclarecimentoSolicitadoEm = null;
        dto.EsclarecimentoDescricao = "Texto que sobrou no formulário";
        dto.EsclarecimentoRespondidoEm = null;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Null(resposta.EsclarecimentoSolicitadoEm);
        Assert.Null(resposta.EsclarecimentoDescricao);
        Assert.Null(resposta.EsclarecimentoRespondidoEm);
        Assert.False(resposta.EsclarecimentoPendente);
    }

    [Fact]
    public async Task Atualizar_LimparADataDoPedido_AnulaDescricaoEResposta()
    {
        // Encerrar o bloco inteiro (as três células em branco) continua valendo
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000309/2026-20", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.EsclarecimentoSolicitadoEm = DiasAtras(15);
            p.EsclarecimentoDescricao = "Pedido antigo";
            p.EsclarecimentoRespondidoEm = DiasAtras(5);
        });

        var dto = NovoProcessoDto("04044-00000309/2026-20");
        dto.ChegadaSgdi = DiasAtras(30);
        // O formulário volta com os três campos limpos
        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Null(resposta.EsclarecimentoSolicitadoEm);
        Assert.Null(resposta.EsclarecimentoDescricao);
        Assert.Null(resposta.EsclarecimentoRespondidoEm);
    }

    [Fact]
    public async Task Importar_RespostaSemPedido_RejeitaALinhaComOMotivo()
    {
        // Sem a recusa, a planilha entrava como "Atualizar" e a resposta sumia sem
        // nada aparecer na prévia
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00000313/2026-14", p => p.ChegadaSgdi = DiasAtras(40));

        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000313/2026-14;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;;;;;;25/03/2026\r\n";

        var previa = await NovoImportacaoService().PreviaAsync(BytesUtf8ComBom(csv));
        var linha = Assert.Single(previa.Linhas);

        Assert.Equal(CtrImportacaoAcao.Rejeitar, linha.Acao);
        Assert.Equal("Informe a data do pedido de esclarecimento antes de registrar a resposta.", linha.Motivo);

        var relatorio = await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);
        Assert.Equal(1, relatorio.Rejeitados);
        Assert.Equal(0, relatorio.Atualizados);
    }

    [Fact]
    public async Task Criar_DatasDeEsclarecimentoIncoerentes_SaoRecusadas()
    {
        var ctx = await ContextoAnalistaAsync();

        var futura = NovoProcessoDto("04044-00000303/2026-14");
        futura.EsclarecimentoSolicitadoEm = Hoje.AddDays(1);
        futura.EsclarecimentoDescricao = "Pedido";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(futura, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex1.Error.Code);
        Assert.Contains("pedido de esclarecimentos", ex1.Error.Message);

        var respostaFutura = NovoProcessoDto("04044-00000304/2026-15");
        respostaFutura.EsclarecimentoSolicitadoEm = DiasAtras(5);
        respostaFutura.EsclarecimentoDescricao = "Pedido";
        respostaFutura.EsclarecimentoRespondidoEm = Hoje.AddDays(1);
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(respostaFutura, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex2.Error.Code);

        var invertida = NovoProcessoDto("04044-00000305/2026-16");
        invertida.EsclarecimentoSolicitadoEm = DiasAtras(5);
        invertida.EsclarecimentoDescricao = "Pedido";
        invertida.EsclarecimentoRespondidoEm = DiasAtras(10);
        var ex3 = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(invertida, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex3.Error.Code);
        Assert.Contains("anterior à data do pedido", ex3.Error.Message);
    }

    [Fact]
    public async Task UltimaMovimentacao_ConsideraAsDatasDeEsclarecimento()
    {
        // Pedir e responder esclarecimento É movimentação do processo
        var processo = SemearProcesso("04044-00000306/2026-17", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.EsclarecimentoSolicitadoEm = DiasAtras(10);
            p.EsclarecimentoDescricao = "Pedido";
            p.EsclarecimentoRespondidoEm = DiasAtras(3);
        });

        var resposta = await _processos.GetAsync(processo.Id);

        Assert.Equal(DiasAtras(3), resposta.UltimaMovimentacao);
        Assert.Equal(3, resposta.DiasSemMovimento);
    }

    [Fact]
    public async Task Filtro_EsclarecimentoPendente_SeparaOsDoisLados()
    {
        SemearProcesso("04044-00000310/2026-11", p =>
        {
            p.EsclarecimentoSolicitadoEm = DiasAtras(12);
            p.EsclarecimentoDescricao = "Pedido em aberto";
        });
        SemearProcesso("04044-00000311/2026-12", p =>
        {
            p.EsclarecimentoSolicitadoEm = DiasAtras(20);
            p.EsclarecimentoDescricao = "Pedido respondido";
            p.EsclarecimentoRespondidoEm = DiasAtras(5);
        });
        SemearProcesso("04044-00000312/2026-13");

        var pendentes = await _processos.ListarAsync(new CtrProcessoFiltro { EsclarecimentoPendente = true });
        Assert.Equal("04044-00000310/2026-11", Assert.Single(pendentes.Items).NumeroProcesso);

        var semPendencia = await _processos.ListarAsync(new CtrProcessoFiltro { EsclarecimentoPendente = false });
        Assert.Equal(2, semPendencia.TotalItems);

        var todos = await _processos.ListarAsync(new CtrProcessoFiltro());
        Assert.Equal(3, todos.TotalItems);
    }

    [Fact]
    public async Task Painel_ContaOsEsclarecimentosPendentes()
    {
        SemearProcesso("04044-00000320/2026-11", p =>
        {
            p.EsclarecimentoSolicitadoEm = DiasAtras(12);
            p.EsclarecimentoDescricao = "Pedido em aberto";
        });
        SemearProcesso("04044-00000321/2026-12", p =>
        {
            p.EsclarecimentoSolicitadoEm = DiasAtras(20);
            p.EsclarecimentoDescricao = "Pedido respondido";
            p.EsclarecimentoRespondidoEm = DiasAtras(5);
        });
        SemearProcesso("04044-00000322/2026-13");

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(3, painel.TotalAtivos);
        Assert.Equal(1, painel.TotalEsclarecimentoPendente);
    }

    // ── CSV do esclarecimento ─────────────────────────────────────────────────

    [Fact]
    public void Csv_Cabecalho_TerminaComAsTresColunasDeEsclarecimento()
    {
        Assert.Equal(22, CtrCsv.Cabecalho.Length);
        Assert.Equal(
            new[] { "Pedido de esclarecimento em", "Esclarecimento solicitado", "Esclarecimento respondido em" },
            CtrCsv.Cabecalho[^3..]);
    }

    [Fact]
    public void Csv_RoundTrip_DoEsclarecimento_EhFiel()
    {
        var bytes = CtrCsv.Escrever(new List<CtrProcessoResponse>
        {
            new()
            {
                NumeroProcesso = "04044-00000330/2026-11",
                OrgaoNome = "Economia",
                OrgaoSigla = "SEEC",
                Objeto = "Switches",
                CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
                Origem = CtrDominios.Origem.OrgaoComunicante,
                EsclarecimentoSolicitadoEm = new DateOnly(2026, 3, 10),
                EsclarecimentoDescricao = "Detalhar a memória de cálculo; com anexos",
                EsclarecimentoRespondidoEm = new DateOnly(2026, 3, 25)
            }
        });

        var linha = Assert.Single(CtrCsv.Ler(bytes));

        Assert.Null(linha.Erro);
        Assert.Equal(new DateOnly(2026, 3, 10), linha.Dados!.EsclarecimentoSolicitadoEm);
        Assert.Equal("Detalhar a memória de cálculo; com anexos", linha.Dados.EsclarecimentoDescricao);
        Assert.Equal(new DateOnly(2026, 3, 25), linha.Dados.EsclarecimentoRespondidoEm);
    }

    [Fact]
    public async Task Importar_SemAsColunasDeEsclarecimento_PreservaOBloco()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000331/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.EsclarecimentoSolicitadoEm = DiasAtras(10);
            p.EsclarecimentoDescricao = "Pedido gravado pela tela";
        });

        // Planilha legada de 12 colunas: nem sabe que esclarecimento existe
        var csv = "Analises de contratações;;;;;;;;;;;\r\n"
            + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
            + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
            + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
            + "04044-00000331/2026-12;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;\r\n";

        await NovoImportacaoService().ImportarAsync(Bytes1252(csv), ctx);

        Assert.Equal(DiasAtras(10), processo.EsclarecimentoSolicitadoEm);
        Assert.Equal("Pedido gravado pela tela", processo.EsclarecimentoDescricao);
        Assert.Null(processo.EsclarecimentoRespondidoEm);
    }

    [Fact]
    public async Task Importar_ComAsColunasDeEsclarecimentoVazias_LimpaOBloco()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000332/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.EsclarecimentoSolicitadoEm = DiasAtras(10);
            p.EsclarecimentoDescricao = "Pedido gravado pela tela";
        });

        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000332/2026-13;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;;;;;;\r\n";

        await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);

        Assert.Null(processo.EsclarecimentoSolicitadoEm);
        Assert.Null(processo.EsclarecimentoDescricao);
        Assert.Null(processo.EsclarecimentoRespondidoEm);
    }

    // ══ A2. Esclarecimentos Adicionais (antes "Pendências identificadas pelo TCDF") ══
    // Renomeado de ponta a ponta na rodada da Supervisão Contínua; semântica intacta.

    [Fact]
    public async Task Manifestacao_GravaEDevolveOsEsclarecimentosAdicionais()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000340/2026-11", p => p.ChegadaSgdi = DiasAtras(20));

        var dto = NovaManifestacaoIncisoI();
        dto.EsclarecimentosAdicionais = "  Ausência de pesquisa de preços e de estudo técnico preliminar.  ";

        var criada = await _manifestacoes.CriarAsync(processo.Id, dto, ctx);

        Assert.Equal("Ausência de pesquisa de preços e de estudo técnico preliminar.", criada.EsclarecimentosAdicionais);

        // Vale também no inciso II (sem CHECK condicional)
        var dtoII = NovaManifestacaoIncisoII();
        dtoII.EsclarecimentosAdicionais = "Contratação não constava do portfólio.";
        var incisoII = await _manifestacoes.CriarAsync(processo.Id, dtoII, ctx);
        Assert.Equal("Contratação não constava do portfólio.", incisoII.EsclarecimentosAdicionais);

        // Texto em branco normaliza para nulo
        var vazio = NovaManifestacaoIncisoI();
        vazio.OficioTcdf = "999/2026-GAB";
        vazio.EsclarecimentosAdicionais = "   ";
        var semEsclarecimentos = await _manifestacoes.CriarAsync(processo.Id, vazio, ctx);
        Assert.Null(semEsclarecimentos.EsclarecimentosAdicionais);
    }

    [Fact]
    public async Task DespachoPdf_NaoImprimeOsEsclarecimentosAdicionais()
    {
        // Mesma decisão da Observação e do status: o template do TCDF não tem o campo
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000341/2026-12", p => p.ChegadaSgdi = DiasAtras(20));

        var criada = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);
        var pdfSem = await _manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília", "Fulano", "Diretor");

        var comEsclarecimentos = NovaManifestacaoIncisoI();
        comEsclarecimentos.EsclarecimentosAdicionais = "Ausência de pesquisa de preços e de estudo técnico preliminar.";
        await _manifestacoes.AtualizarAsync(criada.Id, comEsclarecimentos, ctx);
        var pdfCom = await _manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília", "Fulano", "Diretor");

        Assert.Equal(pdfSem.Length, pdfCom.Length);
    }

    // ══ Parte B — correções da revisão adversarial ════════════════════════════

    // ── B/A1: célula vazia no retorno ao órgão preserva o "não se aplica" ─────

    [Fact]
    public async Task Importar_PlanilhaReal_NaoApagaORetornoDispensado()
    {
        // A coluna "Data de Retorno ao Órgão Comunicante" EXISTE na planilha de 12
        // colunas, então a proteção por coluna ausente não a cobre: sem a correção,
        // a célula vazia zerava o flag e o processo saía de "Concluído".
        var ctx = await ContextoAnalistaAsync();
        await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);

        // Este processo tem "Retorno ao Gab SGDI" na planilha (16/07/2026) e a coluna
        // do retorno ao órgão VAZIA — é exatamente o caso em que a equipe marca, na
        // tela, que a devolução ao órgão não se aplica.
        var processo = Context.CtrProcessos.First(p => p.NumeroProcesso == "00220-00008043/2026-13");
        Assert.NotNull(processo.RetornoGabSgdi);

        processo.RetornoOrgaoNaoSeAplica = true;
        Context.SaveChanges();
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(processo));

        await NovoImportacaoService().ImportarAsync(PlanilhaReal(), ctx);

        Assert.True(processo.RetornoOrgaoNaoSeAplica);
        Assert.Equal(CtrDominios.Situacao.Concluido, CtrProcessoService.CalcularSituacao(processo));
    }

    [Fact]
    public async Task Importar_DataExplicitaNoRetorno_DesmarcaOFlag()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000350/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.RetornoGabSgdi = DiasAtras(20);
            p.RetornoOrgaoNaoSeAplica = true;
        });

        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000350/2026-11;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + $"01/02/2026;;;10/02/2026;{DiasAtras(2):dd/MM/yyyy};;Não;;;;;;;;;\r\n";

        await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);

        Assert.False(processo.RetornoOrgaoNaoSeAplica);
        Assert.Equal(DiasAtras(2), processo.RetornoOrgao);
    }

    [Fact]
    public async Task Importar_TracoNoRetorno_MarcaOFlag()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000351/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(40);
            p.RetornoGabSgdi = DiasAtras(20);
        });

        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000351/2026-12;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;10/02/2026;-;;Não;;;;;;;;;\r\n";

        await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);

        Assert.True(processo.RetornoOrgaoNaoSeAplica);
        Assert.Null(processo.RetornoOrgao);
    }

    // ── B/A2: criticidade não pode sumir com manifestação do inciso I ─────────

    [Fact]
    public async Task Atualizar_RemoverACriticidadeComIncisoI_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000360/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });
        await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var dto = NovoProcessoDto("04044-00000360/2026-11");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.Criticidade = null;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("manifestação do inciso I", ex.Error.Message);
        // A entidade rastreada não ficou suja
        Assert.Equal(CtrDominios.Criticidade.Alta, processo.Criticidade);
    }

    [Fact]
    public async Task Atualizar_RemoverACriticidadeSemIncisoI_EhAceito()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000361/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });
        // Só inciso II: não reporta criticidade
        await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoII(), ctx);

        var dto = NovoProcessoDto("04044-00000361/2026-12");
        dto.ChegadaSgdi = DiasAtras(20);
        dto.Criticidade = null;

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Null(resposta.Criticidade);
    }

    [Fact]
    public async Task Importar_RemoverACriticidadeComIncisoI_RejeitaALinha()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000362/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });
        await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        // Coluna de criticidade PRESENTE e vazia = pedido explícito de limpar
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000362/2026-13;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;;;;;;\r\n";

        var relatorio = await NovoImportacaoService().ImportarAsync(BytesUtf8ComBom(csv), ctx);

        Assert.Equal(1, relatorio.Rejeitados);
        Assert.Contains("manifestação do inciso I", Assert.Single(relatorio.Linhas).Motivo);
        Assert.Equal(CtrDominios.Criticidade.Alta, processo.Criticidade);
    }

    [Fact]
    public async Task DespachoPdf_IncisoISemCriticidade_EhRecusado()
    {
        // Cinto de segurança: o despacho sairia com o "[Alta/Média/Baixa]" do papel
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000363/2026-14", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });
        var criada = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        // Some pelo banco (linha legada, ou o caminho que a correção do PUT fechou)
        processo.Criticidade = null;
        Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília", "Fulano", "Diretor"));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("criticidade", ex.Error.Message);
    }

    [Fact]
    public async Task DespachoPdf_IncisoIISemCriticidade_ContinuaSaindo()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000364/2026-15", p => p.Criticidade = null);
        var criada = await _manifestacoes.CriarAsync(processo.Id, NovaManifestacaoIncisoII(), ctx);

        var pdf = await _manifestacoes.GerarDespachoPdfAsync(criada.Id, "Brasília", "Fulano", "Diretor");

        Assert.NotEmpty(pdf);
    }

    // ── B/M1: PUT sem Origem preserva a gravada ──────────────────────────────

    [Fact]
    public async Task Atualizar_SemOrigemNoCorpo_PreservaAOrigemGravada()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000370/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.Origem = CtrDominios.Origem.Tcdf;
        });

        var semOrigem = NovoProcessoDto("04044-00000370/2026-11");
        semOrigem.ChegadaSgdi = DiasAtras(20);
        semOrigem.Origem = null;
        Assert.Equal(CtrDominios.Origem.Tcdf, (await _processos.AtualizarAsync(processo.Id, semOrigem, ctx)).Origem);

        var vazia = NovoProcessoDto("04044-00000370/2026-11");
        vazia.ChegadaSgdi = DiasAtras(20);
        vazia.Origem = "   ";
        Assert.Equal(CtrDominios.Origem.Tcdf, (await _processos.AtualizarAsync(processo.Id, vazia, ctx)).Origem);

        // Valor explícito continua trocando
        var explicita = NovoProcessoDto("04044-00000370/2026-11");
        explicita.ChegadaSgdi = DiasAtras(20);
        explicita.Origem = CtrDominios.Origem.OrgaoComunicante;
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante,
            (await _processos.AtualizarAsync(processo.Id, explicita, ctx)).Origem);
    }

    [Fact]
    public async Task Criar_SemOrigem_ContinuaNascendoComoOrgaoComunicante()
    {
        // A regra "vazio = Órgão comunicante" fica SÓ na criação
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000371/2026-12");
        dto.Origem = null;

        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, (await _processos.CriarAsync(dto, ctx)).Origem);
    }

    // ── B/B3: "-" na assinatura rejeita a linha ──────────────────────────────

    [Fact]
    public void Csv_TracoNaAssinaturaDoContrato_RejeitaALinha()
    {
        // Não existe "não se aplica" para assinatura de contrato
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04044-00000380/2026-11;Economia;SEEC;;Switches;Infraestrutura de Rede;"
            + "01/02/2026;;;;;;Não;;;;-;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Dados);
        Assert.Contains("data inválida em Assinatura do contrato", linha.Erro);
    }

    // ── B/C1 + B4: NaoSeAplica nas cinco etapas do checkpoint ────────────────

    [Theory]
    [InlineData(CtrDominios.Etapa.ChegadaSgdi)]
    [InlineData(CtrDominios.Etapa.ChegadaSubgd)]
    [InlineData(CtrDominios.Etapa.RetornoGabSgdi)]
    public async Task Checkpoint_NaoSeAplicaFalse_EhRecusadoNasEtapasQueNaoAceitam(string etapa)
    {
        // Contrato travado dos dois lados: o front tem de OMITIR a chave nessas
        // etapas (mandar false devolvia 500 no primeiro uso do modal)
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso($"04044-0000039{CtrDominios.Etapa.Todos.ToList().IndexOf(etapa)}/2026-11");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
            {
                Etapa = etapa,
                Data = DiasAtras(1),
                NaoSeAplica = false
            }, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("Não se aplica", ex.Error.Message);
    }

    [Theory]
    [InlineData(CtrDominios.Etapa.ChegadaUgtic)]
    [InlineData(CtrDominios.Etapa.RetornoOrgao)]
    public async Task Checkpoint_NaoSeAplicaFalse_EhAceitoNasDuasEtapasQueAceitam(string etapa)
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso($"04044-0000039{CtrDominios.Etapa.Todos.ToList().IndexOf(etapa)}/2026-12",
            p =>
            {
                p.ChegadaSgdi = DiasAtras(30);
                p.ChegadaSubgd = DiasAtras(25);
                p.RetornoGabSgdi = DiasAtras(10);
            });

        // A data respeita a cronologia da etapa (UGTIC vem antes do retorno ao Gab)
        var data = etapa == CtrDominios.Etapa.ChegadaUgtic ? DiasAtras(20) : DiasAtras(2);

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = etapa,
            Data = data,
            NaoSeAplica = false
        }, ctx);

        Assert.Equal(data,
            etapa == CtrDominios.Etapa.ChegadaUgtic ? resposta.ChegadaUgtic : resposta.RetornoOrgao);
    }

    [Theory]
    [InlineData(CtrDominios.Etapa.ChegadaSgdi)]
    [InlineData(CtrDominios.Etapa.ChegadaSubgd)]
    [InlineData(CtrDominios.Etapa.ChegadaUgtic)]
    [InlineData(CtrDominios.Etapa.RetornoGabSgdi)]
    [InlineData(CtrDominios.Etapa.RetornoOrgao)]
    public async Task Checkpoint_SemAChaveNaoSeAplica_FuncionaNasCincoEtapas(string etapa)
    {
        // É este o contrato que o front passa a cumprir: omitir a chave
        var ctx = await ContextoAnalistaAsync();
        var indice = CtrDominios.Etapa.Todos.ToList().IndexOf(etapa);
        var processo = SemearProcesso($"04044-0000040{indice}/2026-11");

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = etapa,
            Data = DiasAtras(1)
        }, ctx);

        var gravada = etapa switch
        {
            CtrDominios.Etapa.ChegadaSgdi => resposta.ChegadaSgdi,
            CtrDominios.Etapa.ChegadaSubgd => resposta.ChegadaSubgd,
            CtrDominios.Etapa.ChegadaUgtic => resposta.ChegadaUgtic,
            CtrDominios.Etapa.RetornoGabSgdi => resposta.RetornoGabSgdi,
            _ => resposta.RetornoOrgao
        };

        Assert.Equal(DiasAtras(1), gravada);
        Assert.NotNull(resposta.AlteradoEm);
    }

    [Fact]
    public async Task Checkpoint_LimparADataDoRetornoSemAChave_NaoDesmarcaONaoSeAplica()
    {
        // B4: "Limpar a data desta etapa" com a chave omitida deixa o flag intacto
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000410/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.RetornoGabSgdi = DiasAtras(10);
            p.RetornoOrgaoNaoSeAplica = true;
        });

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.RetornoOrgao,
            Data = null
        }, ctx);

        Assert.True(resposta.RetornoOrgaoNaoSeAplica);
        Assert.Null(resposta.RetornoOrgao);
        Assert.Equal(CtrDominios.Situacao.Concluido, resposta.Situacao);
    }

    // ── Assimetria CalcularEstagio × PredicadoEstagio ────────────────────────

    [Fact]
    public async Task Estagio_CalculoEFiltroConcordam_ComEStatusTcdf()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000420/2026-11", p => p.ChegadaSgdi = DiasAtras(20));

        var comStatus = NovaManifestacaoIncisoI();
        comStatus.OficioTcdf = "800/2026-GAB";
        comStatus.StatusTcdf = CtrDominios.StatusTcdf.EditalRevogado;
        var criada = await _manifestacoes.CriarAsync(processo.Id, comStatus, ctx);

        // Cálculo e filtro têm de dar a MESMA resposta (antes um usava
        // IsNullOrWhiteSpace e o outro == null)
        Assert.Equal(CtrDominios.Estagio.EditalRevogado, criada.Estagio);

        var porFiltro = await _manifestacoes.ListarAsync(
            new CtrManifestacaoFiltro { Estagio = CtrDominios.Estagio.EditalRevogado });
        Assert.Equal("800/2026-GAB", Assert.Single(porFiltro.Items).OficioTcdf);

        // E some do estágio de análise
        var alinhadas = await _manifestacoes.ListarAsync(
            new CtrManifestacaoFiltro { Estagio = CtrDominios.Estagio.Alinhada });
        Assert.Equal(0, alinhadas.TotalItems);
    }
}
