using System.Text;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-10-13 (desenho_lista_supervisao_e_historicos.md): as 11 situações com a data
/// que as define (SituacaoDesde), o STATUS que sai, a nota informativa (campos, validação,
/// checkpoint e movimentação), os registros históricos (origem "Anterior ao regime": criação,
/// obrigatórios, recusas, edição, exclusão, marcar e desfazer, manifestação ao TCDF), o painel
/// interno sem os históricos e com o bloco próprio, o painel público e as quatro colunas novas
/// da planilha.
/// </summary>
public class CtrSituacoesHistoricosTest : CtrTestBase
{
    private const string Numero = "04044-00001000/2026-11";
    private const string NumeroHistorico = "00060-00000555/2019-21";
    private const string Contrato = "Contrato nº 12/2019";

    private readonly CtrProcessoService _processos;

    public CtrSituacoesHistoricosTest()
    {
        _processos = NovoProcessoService();
    }

    /// <summary>Registro histórico mínimo: os obrigatórios da origem nova e nada mais.</summary>
    private static CtrProcessoUpdateDTO HistoricoDto(string numero = NumeroHistorico)
    {
        var dto = NovoProcessoDto(numero);
        dto.Origem = CtrDominios.Origem.AnteriorAoRegime;
        dto.DataAssinaturaContrato = DiasAtras(800);
        dto.NumeroContrato = Contrato;
        return dto;
    }

    /// <summary>Insere um registro histórico direto no banco.</summary>
    private CtrProcesso SemearHistorico(string numero, Action<CtrProcesso>? ajustar = null) =>
        SemearProcesso(numero, p =>
        {
            p.Origem = CtrDominios.Origem.AnteriorAoRegime;
            p.Criticidade = null;
            p.DataAssinaturaContrato = DiasAtras(800);
            p.NumeroContrato = Contrato;
            ajustar?.Invoke(p);
        });

    private static CtrProcessoUpdateDTO EdicaoDe(CtrProcesso p)
    {
        var dto = new CtrProcessoUpdateDTO();
        var gravado = CtrProcessoService.DtoDe(p);
        foreach (var prop in typeof(CtrProcessoCreateDTO).GetProperties())
            prop.SetValue(dto, prop.GetValue(gravado));
        return dto;
    }

    private static CtrRiscoDeclaradoDTO Risco() => new()
    {
        DescricaoRisco = "Atraso na entrega",
        AcaoMitigacao = "Cronograma com marcos",
        ResponsavelNome = "Fulano",
        ResponsavelEmail = "fulano@df.gov.br",
        Probabilidade = "Possível",
        Consequencia = "Moderada"
    };

    private static string[] Celulas(byte[] export, int linha) =>
        Encoding.UTF8.GetString(export, 3, export.Length - 3)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[linha]
            .Split(';');

    // ══ 1. As 11 situações ═══════════════════════════════════════════════════

    [Fact]
    public void Situacoes_SaoAsOnzeDoDesenho_NaOrdemDePrioridade()
    {
        Assert.Equal(new[]
        {
            "Contrato assinado", "Restituído ao órgão", "Nota informativa enviada", "Análise concluída",
            "Esclarecimentos solicitados", "Aguardando análise técnica", "Retornado ao Gab SGDI",
            "Em análise na UGTIC", "Em análise na SUBGD", "Em análise na SGDI", "Sem movimentação"
        }, CtrDominios.Situacao.Todos);

        Assert.Equal(new[] { "Contrato assinado", "Restituído ao órgão", "Nota informativa enviada", "Análise concluída" },
            CtrDominios.Situacao.Terminais);
    }

    public static IEnumerable<object?[]> CasosDePrecedencia()
    {
        // Cada caso liga a situação esperada e mais as de prioridade menor, para provar que a
        // primeira que se aplica vence; a data esperada é a do fato que a define
        yield return Caso(CtrDominios.Situacao.ContratoAssinado, 1, p =>
        {
            Tudo(p);
            p.DataAssinaturaContrato = DiasAtras(1);
        });
        yield return Caso(CtrDominios.Situacao.RestituidoOrgao, 2, p =>
        {
            Tudo(p);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(2);
            p.RestituidoMotivo = "Faltou o ETP";
        });
        yield return Caso(CtrDominios.Situacao.NotaInformativaEnviada, 3, p =>
        {
            Tudo(p);
            p.NotaInformativaEnviadaEm = DiasAtras(3);
        });
        yield return Caso(CtrDominios.Situacao.AnaliseConcluida, 4, p =>
        {
            Tudo(p);
            p.RetornoOrgao = DiasAtras(4);
        });
        // Devolução dispensada com o retorno ao Gab: a data é a do retorno ao Gab
        yield return Caso(CtrDominios.Situacao.AnaliseConcluida, 9, p =>
        {
            Tudo(p);
            p.RetornoOrgaoNaoSeAplica = true;
        });
        yield return Caso(CtrDominios.Situacao.EsclarecimentosSolicitados, 5, p =>
        {
            Tudo(p);
            p.EsclarecimentoSolicitadoEm = DiasAtras(5);
            p.EsclarecimentoDescricao = "Memória de cálculo";
        });
        yield return Caso(CtrDominios.Situacao.AguardandoAnaliseTecnica, 6, p =>
        {
            Tudo(p);
            p.AnaliseTecnicaEncaminhadaEm = DiasAtras(6);
            p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        });
        // Esclarecimento respondido e análise técnica com retorno não seguram mais a situação
        yield return Caso(CtrDominios.Situacao.RetornadoGabSgdi, 9, p =>
        {
            Tudo(p);
            p.EsclarecimentoSolicitadoEm = DiasAtras(8);
            p.EsclarecimentoDescricao = "Memória de cálculo";
            p.EsclarecimentoRespondidoEm = DiasAtras(7);
            p.AnaliseTecnicaEncaminhadaEm = DiasAtras(8);
            p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subinfra;
            p.AnaliseTecnicaRetornoEm = DiasAtras(7);
        });
        yield return Caso(CtrDominios.Situacao.EmAnaliseUgtic, 10, p =>
        {
            Tudo(p);
            p.RetornoGabSgdi = null;
        });
        yield return Caso(CtrDominios.Situacao.EmAnaliseSubgd, 11, p =>
        {
            Tudo(p);
            p.RetornoGabSgdi = null;
            p.ChegadaUgtic = null;
            p.UgticNaoSeAplica = true;
        });
        yield return Caso(CtrDominios.Situacao.EmAnaliseSgdi, 12, p => p.ChegadaSgdi = DiasAtras(12));
        yield return Caso(CtrDominios.Situacao.SemMovimentacao, null, _ => { });
    }

    private static void Tudo(CtrProcesso p)
    {
        p.ChegadaSgdi = DiasAtras(12);
        p.ChegadaSubgd = DiasAtras(11);
        p.ChegadaUgtic = DiasAtras(10);
        p.RetornoGabSgdi = DiasAtras(9);
    }

    private static object?[] Caso(string situacao, int? diasDesde, Action<CtrProcesso> montar) =>
        new object?[] { situacao, diasDesde, montar };

    [Theory]
    [MemberData(nameof(CasosDePrecedencia))]
    public void Situacao_PrimeiraQueSeAplicaVence_ComADataDoFato(string esperada, int? diasDesde,
        Action<CtrProcesso> montar)
    {
        var processo = new CtrProcesso();
        montar(processo);

        Assert.Equal(esperada, CtrProcessoService.CalcularSituacao(processo));
        Assert.Equal(diasDesde == null ? null : DiasAtras(diasDesde.Value), CtrProcessoService.CalcularSituacaoDesde(processo));

        // O mapeamento da resposta usa as mesmas fontes
        var resposta = CtrProcessoService.MapProcesso(processo, Hoje);
        Assert.Equal(esperada, resposta.Situacao);
        Assert.Equal(CtrProcessoService.CalcularSituacaoDesde(processo), resposta.SituacaoDesde);
    }

    [Fact]
    public async Task Predicado_E_Calculo_DaoOMesmoResultado_NaMatrizDeCasos()
    {
        // 13 fatos ligados ou não (8.192 processos): cada processo cai em exatamente uma
        // situação, e o filtro que roda no banco acha os mesmos que o cálculo em memória
        var d = DiasAtras(30);
        var casos = new List<CtrProcesso>();
        for (var bits = 0; bits < 1 << 13; bits++)
        {
            var b = bits;
            bool Liga(int i) => (b & (1 << i)) != 0;
            casos.Add(new CtrProcesso
            {
                NumeroProcesso = $"04044-{bits:00000000}/2026-11",
                OrgaoNome = "Órgão",
                OrgaoSigla = "ORG",
                Objeto = "Objeto",
                CategoriaObjeto = CtrDominios.CategoriaObjeto.Nuvem,
                Origem = CtrDominios.Origem.OrgaoComunicante,
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = "teste",
                DataAssinaturaContrato = Liga(0) ? d : null,
                Restituido = Liga(1),
                RestituidoEm = Liga(1) ? d : null,
                RestituidoMotivo = Liga(1) ? "motivo" : null,
                NotaInformativaEnviadaEm = Liga(2) ? d : null,
                RetornoOrgao = Liga(3) ? d : null,
                RetornoOrgaoNaoSeAplica = Liga(4) && !Liga(3),
                EsclarecimentoSolicitadoEm = Liga(5) ? d : null,
                EsclarecimentoDescricao = Liga(5) ? "pedido" : null,
                EsclarecimentoRespondidoEm = Liga(5) && Liga(6) ? d : null,
                AnaliseTecnicaEncaminhadaEm = Liga(7) ? d : null,
                AnaliseTecnicaArea = Liga(7) ? CtrDominios.AreaTecnica.Subsis : null,
                AnaliseTecnicaRetornoEm = Liga(7) && Liga(8) ? d : null,
                RetornoGabSgdi = Liga(9) ? d : null,
                ChegadaUgtic = Liga(10) ? d : null,
                ChegadaSubgd = Liga(11) ? d : null,
                ChegadaSgdi = Liga(12) ? d : null
            });
        }
        Context.CtrProcessos.AddRange(casos);
        await Context.SaveChangesAsync();

        var vistos = new HashSet<long>();
        foreach (var situacao in CtrDominios.Situacao.Todos)
        {
            var predicado = CtrProcessoService.PredicadoSituacao(situacao)!;
            // Um lambda só, sem Invoke: é o que o EF traduz em SQL
            Assert.DoesNotContain("Invoke", predicado.ToString());

            var noBanco = Context.CtrProcessos.Where(predicado).Select(p => p.Id).OrderBy(id => id).ToList();
            var emMemoria = casos.Where(p => CtrProcessoService.CalcularSituacao(p) == situacao)
                .Select(p => p.Id).OrderBy(id => id).ToList();

            Assert.Equal(emMemoria, noBanco);
            Assert.NotEmpty(noBanco);
            foreach (var id in noBanco) Assert.True(vistos.Add(id), $"processo {id} em duas situações");
        }

        Assert.Equal(casos.Count, vistos.Count);
    }

    [Theory]
    [InlineData("Concluído")]
    [InlineData("Restituído")]
    [InlineData("Qualquer coisa")]
    public async Task Filtro_ComNomeAntigoOuForaDoDominio_DevolveListaVazia(string situacao)
    {
        SemearProcesso(Numero, p => p.DataAssinaturaContrato = DiasAtras(1));
        SemearProcesso("04044-00001001/2026-12", p =>
        {
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(1);
            p.RestituidoMotivo = "motivo";
        });

        Assert.Null(CtrProcessoService.PredicadoSituacao(situacao));
        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { Situacao = situacao, IncluirConcluidos = true });
        Assert.Empty(lista.Items);
    }

    [Theory]
    [InlineData(CtrDominios.Situacao.NotaInformativaEnviada)]
    [InlineData(CtrDominios.Situacao.EsclarecimentosSolicitados)]
    [InlineData(CtrDominios.Situacao.AguardandoAnaliseTecnica)]
    [InlineData(CtrDominios.Situacao.ContratoAssinado)]
    public async Task Filtro_PelasSituacoesNovas_AchaSoOsDela(string situacao)
    {
        SemearProcesso("04044-00001010/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.NotaInformativaEnviadaEm = DiasAtras(2);
        });
        SemearProcesso("04044-00001011/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.EsclarecimentoSolicitadoEm = DiasAtras(2);
            p.EsclarecimentoDescricao = "pedido";
        });
        SemearProcesso("04044-00001012/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.AnaliseTecnicaEncaminhadaEm = DiasAtras(2);
            p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subinfra;
        });
        SemearProcesso("04044-00001013/2026-14", p => p.DataAssinaturaContrato = DiasAtras(2));

        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { Situacao = situacao, PageSize = 50 });

        var item = Assert.Single(lista.Items);
        Assert.Equal(situacao, item.Situacao);
        Assert.Equal(DiasAtras(2), item.SituacaoDesde);
    }

    [Fact]
    public async Task Lista_EscondeOsDeContratoAssinadoPorPadrao_EIncluirConcluidosOsTraz()
    {
        SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(5));
        SemearProcesso("04044-00001001/2026-12", p => p.DataAssinaturaContrato = DiasAtras(1));

        var padrao = await _processos.ListarAsync(new CtrProcessoFiltro());
        Assert.Equal(new[] { Numero }, padrao.Items.Select(p => p.NumeroProcesso));

        var todos = await _processos.ListarAsync(new CtrProcessoFiltro { IncluirConcluidos = true });
        Assert.Equal(2, todos.TotalItems);
    }

    [Fact]
    public void Status_SaiuDeTodoLugar()
    {
        Assert.Null(typeof(CtrDominios).GetNestedType("StatusSupervisao"));
        Assert.Null(typeof(CtrProcessoResponse).GetProperty("StatusSupervisao"));
        Assert.DoesNotContain(typeof(CtrProcessoService).GetMethods(), m => m.Name == "CalcularStatusSupervisao");
        Assert.NotNull(typeof(CtrProcessoResponse).GetProperty("SituacaoDesde"));
    }

    // ══ 2. Nota informativa ══════════════════════════════════════════════════

    [Fact]
    public async Task Nota_NoCadastro_DefineASituacao_EContaComoMovimentacao()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.ChegadaSgdi = DiasAtras(30);
        dto.ChegadaSubgd = DiasAtras(20);
        dto.NotaInformativaEnviadaEm = DiasAtras(4);
        dto.NotaInformativaDocumentoSei = "  213807905  ";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Situacao.NotaInformativaEnviada, resposta.Situacao);
        Assert.Equal(DiasAtras(4), resposta.SituacaoDesde);
        Assert.Equal(DiasAtras(4), resposta.NotaInformativaEnviadaEm);
        Assert.Equal("213807905", resposta.NotaInformativaDocumentoSei);
        Assert.Equal(DiasAtras(4), resposta.UltimaMovimentacao);
        Assert.Equal(4, resposta.DiasSemMovimento);
    }

    [Fact]
    public async Task Nota_Futura_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.NotaInformativaEnviadaEm = Hoje.AddDays(1);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        Assert.Contains("não pode ser futura", ex.Error.Message);
    }

    [Fact]
    public async Task Nota_AntesDaChegadaASgdi_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.ChegadaSgdi = DiasAtras(10);
        dto.NotaInformativaEnviadaEm = DiasAtras(11);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        Assert.Contains("anterior à data de chegada à SGDI", ex.Error.Message);
    }

    [Fact]
    public async Task Nota_SemChegada_EhAceita_FicaForaDaCronologiaDoTramite()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.NotaInformativaEnviadaEm = DiasAtras(3);

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Situacao.NotaInformativaEnviada, resposta.Situacao);
    }

    [Fact]
    public async Task Nota_DocumentoAcimaDe25Caracteres_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.NotaInformativaEnviadaEm = DiasAtras(3);
        dto.NotaInformativaDocumentoSei = new string('9', 26);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("até 25 caracteres", ex.Error.Message);
    }

    [Fact]
    public async Task Nota_DocumentoVazio_EhNulo_ESemDataEhAnulado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.NotaInformativaEnviadaEm = DiasAtras(3);
        dto.NotaInformativaDocumentoSei = "   ";
        var vazio = await _processos.CriarAsync(dto, ctx);
        Assert.Null(vazio.NotaInformativaDocumentoSei);

        var semData = NovoProcessoDto("04044-00001001/2026-12");
        semData.NotaInformativaDocumentoSei = "213807905";
        var anulado = await _processos.CriarAsync(semData, ctx);
        Assert.Null(anulado.NotaInformativaDocumentoSei);
        Assert.Null(anulado.NotaInformativaEnviadaEm);
    }

    [Fact]
    public async Task Nota_NoPut_OEnviadoVale_ENuloLimpa()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(20));

        var edicao = EdicaoDe(processo);
        edicao.NotaInformativaEnviadaEm = DiasAtras(2);
        edicao.NotaInformativaDocumentoSei = "999";
        var comNota = await _processos.AtualizarAsync(processo.Id, edicao, ctx);
        Assert.Equal(DiasAtras(2), comNota.NotaInformativaEnviadaEm);
        Assert.Equal("999", comNota.NotaInformativaDocumentoSei);

        edicao.NotaInformativaEnviadaEm = null;
        var semNota = await _processos.AtualizarAsync(processo.Id, edicao, ctx);
        Assert.Null(semNota.NotaInformativaEnviadaEm);
        Assert.Null(semNota.NotaInformativaDocumentoSei);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, semNota.Situacao);
    }

    [Fact]
    public async Task Checkpoint_DaNota_GravaDataEDocumento_ELimparADataLimpaODocumento()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(20));

        var enviada = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.NotaInformativa, Data = DiasAtras(1), DocumentoSei = " 213807905 "
        }, ctx);
        Assert.Equal(CtrDominios.Situacao.NotaInformativaEnviada, enviada.Situacao);
        Assert.Equal(DiasAtras(1), enviada.SituacaoDesde);
        Assert.Equal("213807905", enviada.NotaInformativaDocumentoSei);

        var limpa = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.NotaInformativa, Data = null, DocumentoSei = "213807905"
        }, ctx);
        Assert.Null(limpa.NotaInformativaEnviadaEm);
        Assert.Null(limpa.NotaInformativaDocumentoSei);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, limpa.Situacao);
    }

    [Fact]
    public async Task Checkpoint_DeOutraEtapa_NaoTocaANota_ERecusaODocumento()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.NotaInformativaEnviadaEm = DiasAtras(2);
            p.NotaInformativaDocumentoSei = "777";
        });

        var subgd = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        { Etapa = CtrDominios.Etapa.ChegadaSubgd, Data = DiasAtras(10), DocumentoSei = "  " }, ctx);
        Assert.Equal(DiasAtras(2), subgd.NotaInformativaEnviadaEm);
        Assert.Equal("777", subgd.NotaInformativaDocumentoSei);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.ChegadaSubgd, Data = DiasAtras(10), DocumentoSei = "123" }, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("só vale para a nota informativa", ex.Error.Message);
    }

    [Fact]
    public async Task Checkpoint_DaNota_ValidaComoOCadastro_ERecusaONaoSeAplica()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(5));

        var antes = await Assert.ThrowsAsync<ApiException>(() => _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.NotaInformativa, Data = DiasAtras(6) }, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, antes.Error.Code);

        var naoSeAplica = await Assert.ThrowsAsync<ApiException>(() => _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.NotaInformativa, Data = DiasAtras(1), NaoSeAplica = true }, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, naoSeAplica.Error.Code);

        Assert.Null(Context.CtrProcessos.Single(p => p.Id == processo.Id).NotaInformativaEnviadaEm);
    }

    // ══ 3. Registros históricos ═══════════════════════════════════════════════

    [Fact]
    public async Task Historico_CriacaoMinima_PeloPostDeSempre()
    {
        var ctx = await ContextoAnalistaAsync();

        var resposta = await _processos.CriarAsync(HistoricoDto(), ctx);

        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, resposta.Origem);
        Assert.Equal(CtrDominios.Situacao.ContratoAssinado, resposta.Situacao);
        Assert.Equal(DiasAtras(800), resposta.SituacaoDesde);
        Assert.Equal(Contrato, resposta.NumeroContrato);
        Assert.Null(resposta.Criticidade);
        Assert.Null(resposta.CriteriosCriticidade);
    }

    [Fact]
    public async Task Historico_ComOsOpcionais_DatasDoTramiteValorAutorizacaoObservacaoETipo()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = HistoricoDto();
        dto.ChegadaSgdi = DiasAtras(900);
        dto.ChegadaSubgd = DiasAtras(890);
        dto.ChegadaUgtic = DiasAtras(880);
        dto.RetornoGabSgdi = DiasAtras(870);
        dto.RetornoOrgao = DiasAtras(860);
        dto.ValorEstimado = 1500000m;
        dto.AutorizacaoDocumentoSei = "  45678901  ";
        dto.Observacao = "Autorizada no regime anterior";
        dto.TipoContratacao = CtrDominios.TipoContratacao.Nova;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Situacao.ContratoAssinado, resposta.Situacao);
        Assert.Equal("45678901", resposta.AutorizacaoDocumentoSei);
        Assert.Equal(DiasAtras(860), resposta.RetornoOrgao);
        Assert.Equal(1500000m, resposta.ValorEstimado);
    }

    [Fact]
    public async Task Historico_CronologiaDoTramite_ValeIgual()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = HistoricoDto();
        dto.ChegadaSgdi = DiasAtras(900);
        dto.ChegadaSubgd = DiasAtras(950);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
    }

    public static IEnumerable<object[]> ObrigatoriosDoHistorico()
    {
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.DataAssinaturaContrato = null), "assinatura do contrato" };
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.NumeroContrato = "  "), "número do contrato" };
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.NumeroProcesso = "123"), "formato SEI" };
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.OrgaoSigla = ""), "sigla do órgão" };
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.OrgaoNome = ""), "nome do órgão" };
        yield return new object[] { (Action<CtrProcessoUpdateDTO>)(d => d.Objeto = ""), "objeto" };
    }

    [Theory]
    [MemberData(nameof(ObrigatoriosDoHistorico))]
    public async Task Historico_SemUmObrigatorio_EhRecusado(Action<CtrProcessoUpdateDTO> tirar, string trecho)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = HistoricoDto();
        tirar(dto);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains(trecho, ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
    }

    public static IEnumerable<object[]> BlocosQueOHistoricoNaoLeva()
    {
        yield return new object[]
        {
            (Action<CtrProcessoUpdateDTO>)(d => d.CriteriosCriticidade = new Dictionary<string, string> { ["I"] = "Sim" }),
            "critérios de criticidade"
        };
        yield return new object[]
            { (Action<CtrProcessoUpdateDTO>)(d => d.Criticidade = CtrDominios.Criticidade.Alta), "critérios de criticidade" };
        yield return new object[]
        {
            (Action<CtrProcessoUpdateDTO>)(d =>
            {
                d.EsclarecimentoSolicitadoEm = DiasAtras(5);
                d.EsclarecimentoDescricao = "pedido";
            }),
            "pedido de esclarecimentos"
        };
        yield return new object[]
        {
            (Action<CtrProcessoUpdateDTO>)(d =>
            {
                d.AnaliseTecnicaEncaminhadaEm = DiasAtras(5);
                d.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
            }),
            "análise técnica"
        };
        yield return new object[]
            { (Action<CtrProcessoUpdateDTO>)(d => d.NotaInformativaEnviadaEm = DiasAtras(5)), "nota informativa" };
        yield return new object[]
            { (Action<CtrProcessoUpdateDTO>)(d => d.NumeroSeiFormulario = "213807905"), "Nº SEI do Formulário" };
        yield return new object[]
        {
            (Action<CtrProcessoUpdateDTO>)(d => d.ClassificacaoRisco = new CtrClassificacaoRiscoDTO
                { RiscosDeclarados = { Risco() } }),
            "riscos da contratação"
        };
    }

    [Theory]
    [MemberData(nameof(BlocosQueOHistoricoNaoLeva))]
    public async Task Historico_ComBlocoQueNaoLeva_EhRecusado_NaCriacaoENaEdicao(
        Action<CtrProcessoUpdateDTO> preencher, string trecho)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = HistoricoDto();
        preencher(dto);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("anterior ao regime não leva", ex.Error.Message);
        Assert.Contains(trecho, ex.Error.Message);

        var gravado = SemearHistorico(NumeroHistorico);
        var edicao = EdicaoDe(gravado);
        preencher(edicao);
        var naEdicao = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(gravado.Id, edicao, ctx));
        Assert.Contains(trecho, naEdicao.Error.Message);
    }

    [Fact]
    public async Task Historico_ListaDeRiscosVazia_EhAceita()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = HistoricoDto();
        dto.ClassificacaoRisco = new CtrClassificacaoRiscoDTO();

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Null(resposta.ClassificacaoRisco);
    }

    [Fact]
    public async Task Historico_Edicao_PeloPutDeSempre_EOrigemAusentePreserva()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);

        var edicao = EdicaoDe(historico);
        edicao.Origem = null;
        edicao.Objeto = "Licenças de banco de dados";
        edicao.NumeroContrato = "Contrato nº 99/2019";
        edicao.AutorizacaoDocumentoSei = "123";

        var resposta = await _processos.AtualizarAsync(historico.Id, edicao, ctx);

        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, resposta.Origem);
        Assert.Equal("Licenças de banco de dados", resposta.Objeto);
        Assert.Equal("Contrato nº 99/2019", resposta.NumeroContrato);
        Assert.Equal("123", resposta.AutorizacaoDocumentoSei);
    }

    [Fact]
    public async Task Historico_PutNaoMarcaNemDesfaz_UsaAAcaoPropria()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);
        var supervisao = SemearProcesso(Numero, p =>
        {
            p.DataAssinaturaContrato = DiasAtras(1);
            p.NumeroContrato = Contrato;
            p.Criticidade = null;
        });

        var desfazer = EdicaoDe(historico);
        desfazer.Origem = CtrDominios.Origem.OrgaoComunicante;
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(historico.Id, desfazer, ctx));
        Assert.Contains("use a ação própria", ex1.Error.Message);

        var marcar = EdicaoDe(supervisao);
        marcar.Origem = CtrDominios.Origem.AnteriorAoRegime;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(supervisao.Id, marcar, ctx));
        Assert.Contains("use a ação própria", ex2.Error.Message);
    }

    [Fact]
    public async Task Historico_Exclusao_LogicaComAuditoria()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);

        await _processos.ExcluirAsync(historico.Id, ctx);

        var gravado = Context.CtrProcessos.Single(p => p.Id == historico.Id);
        Assert.False(gravado.Ativo);
        Assert.Equal(ctx.Email, gravado.AlteradoPor);
        Assert.Empty((await _processos.ListarAsync(new CtrProcessoFiltro
            { Origem = CtrDominios.Origem.AnteriorAoRegime })).Items);
    }

    [Fact]
    public async Task Listas_PelaOrigem_AListaNovaTrazOsHistoricosSemIncluirConcluidos()
    {
        SemearHistorico(NumeroHistorico);
        SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(3));
        SemearProcesso("04044-00001001/2026-12", p => p.Origem = CtrDominios.Origem.Tcdf);

        var historicos = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.AnteriorAoRegime });
        var supervisao = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.OrgaoComunicante });
        var tcdf = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.Tcdf });

        Assert.Equal(new[] { NumeroHistorico }, historicos.Items.Select(p => p.NumeroProcesso));
        Assert.Equal(new[] { Numero }, supervisao.Items.Select(p => p.NumeroProcesso));
        Assert.Equal(new[] { "04044-00001001/2026-12" }, tcdf.Items.Select(p => p.NumeroProcesso));

        // A busca acha também pelo número do contrato
        var busca = await _processos.ListarAsync(new CtrProcessoFiltro
            { Origem = CtrDominios.Origem.AnteriorAoRegime, Filtro = "12/2019" });
        Assert.Single(busca.Items);
    }

    // ── Marcar e desfazer ────────────────────────────────────────────────────

    [Fact]
    public async Task Marcar_ProcessoAssinado_ViraHistorico_EApagaOQueOHistoricoNaoLeva()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(400);
            p.DataAssinaturaContrato = DiasAtras(300);
            p.CriteriosCriticidade = CtrCriticidade.Serializar(CtrCriticidade.Normalizar(new Dictionary<string, string>()));
            p.NumeroSeiFormulario = "213807905";
            p.EsclarecimentoSolicitadoEm = DiasAtras(390);
            p.EsclarecimentoDescricao = "pedido";
            p.EsclarecimentoDocumentoSei = "111";
            p.AnaliseTecnicaEncaminhadaEm = DiasAtras(390);
            p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
            p.AnaliseTecnicaRetornoEm = DiasAtras(380);
            p.AnaliseTecnicaRetornoResumo = "ok";
            p.NotaInformativaEnviadaEm = DiasAtras(350);
            p.NotaInformativaDocumentoSei = "222";
            p.Observacao = "Fica";
        });
        var editado = EdicaoDe(processo);
        editado.ClassificacaoRisco = new CtrClassificacaoRiscoDTO { RiscosDeclarados = { Risco() } };
        await _processos.AtualizarAsync(processo.Id, editado, ctx);
        Assert.Single(Context.CtrRiscosDeclarados.Where(r => r.ProcessoId == processo.Id));

        var marcado = await _processos.MarcarAnteriorAoRegimeAsync(processo.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true, NumeroContrato = "  " + Contrato + "  " }, ctx);

        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, marcado.Origem);
        Assert.Equal(Contrato, marcado.NumeroContrato);
        Assert.Equal(CtrDominios.Situacao.ContratoAssinado, marcado.Situacao);
        Assert.Null(marcado.Criticidade);
        Assert.Null(marcado.CriteriosCriticidade);
        Assert.Null(marcado.NumeroSeiFormulario);
        Assert.Null(marcado.EsclarecimentoSolicitadoEm);
        Assert.Null(marcado.EsclarecimentoDescricao);
        Assert.Null(marcado.EsclarecimentoDocumentoSei);
        Assert.Null(marcado.AnaliseTecnicaEncaminhadaEm);
        Assert.Null(marcado.AnaliseTecnicaArea);
        Assert.Null(marcado.AnaliseTecnicaRetornoResumo);
        Assert.Null(marcado.NotaInformativaEnviadaEm);
        Assert.Null(marcado.NotaInformativaDocumentoSei);
        Assert.Null(marcado.ClassificacaoRisco);
        Assert.Equal("Fica", marcado.Observacao);
        Assert.Equal(DiasAtras(400), marcado.ChegadaSgdi);
        Assert.Empty(Context.CtrRiscosDeclarados.Where(r => r.ProcessoId == processo.Id));
        Assert.Equal(ctx.Email, Context.CtrProcessos.Single(p => p.Id == processo.Id).AlteradoPor);
    }

    [Fact]
    public async Task Marcar_SemNumeroNoCorpo_UsaOGravado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.Criticidade = null;
            p.DataAssinaturaContrato = DiasAtras(10);
            p.NumeroContrato = Contrato;
        });

        var marcado = await _processos.MarcarAnteriorAoRegimeAsync(processo.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true }, ctx);

        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, marcado.Origem);
        Assert.Equal(Contrato, marcado.NumeroContrato);
    }

    [Fact]
    public async Task Marcar_SemContratoAssinado_OuSemNumero_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var semAssinatura = SemearProcesso(Numero, p => p.NumeroContrato = Contrato);
        var semNumero = SemearProcesso("04044-00001001/2026-12", p => p.DataAssinaturaContrato = DiasAtras(10));

        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _processos.MarcarAnteriorAoRegimeAsync(semAssinatura.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true }, ctx));
        Assert.Contains("contrato assinado", ex1.Error.Message);

        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _processos.MarcarAnteriorAoRegimeAsync(semNumero.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true, NumeroContrato = " " }, ctx));
        Assert.Contains("número do contrato", ex2.Error.Message);

        Assert.All(Context.CtrProcessos, p => Assert.Equal(CtrDominios.Origem.OrgaoComunicante, p.Origem));
    }

    [Fact]
    public async Task Marcar_ProcessoDoTcdf_EhRecusado_MarcarEDesfazer()
    {
        var ctx = await ContextoAnalistaAsync();
        var tcdf = SemearProcesso(Numero, p =>
        {
            p.Origem = CtrDominios.Origem.Tcdf;
            p.DataAssinaturaContrato = DiasAtras(10);
            p.NumeroContrato = Contrato;
        });

        foreach (var anterior in new[] { true, false })
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.MarcarAnteriorAoRegimeAsync(tcdf.Id,
                new CtrAnteriorAoRegimeDTO { Anterior = anterior }, ctx));
            Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        }

        Assert.Equal(CtrDominios.Origem.Tcdf, Context.CtrProcessos.Single().Origem);
    }

    [Fact]
    public async Task Marcar_ComManifestacaoDoIncisoI_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.DataAssinaturaContrato = DiasAtras(10);
            p.NumeroContrato = Contrato;
        });
        await NovoManifestacaoService().CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.MarcarAnteriorAoRegimeAsync(processo.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true }, ctx));

        Assert.Contains("inciso I", ex.Error.Message);
        Assert.Equal(CtrDominios.Criticidade.Alta, Context.CtrProcessos.Single().Criticidade);
    }

    [Fact]
    public async Task Desfazer_VoltaParaOrgaoComunicante_EGuardaONumeroDoContrato()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);

        var desfeito = await _processos.MarcarAnteriorAoRegimeAsync(historico.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = false }, ctx);

        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, desfeito.Origem);
        Assert.Equal(Contrato, desfeito.NumeroContrato);
        Assert.Equal(CtrDominios.Situacao.ContratoAssinado, desfeito.Situacao);

        // Desfazer de novo não muda nada; marcar de novo volta ao histórico
        var deNovo = await _processos.MarcarAnteriorAoRegimeAsync(historico.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = false }, ctx);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, deNovo.Origem);

        var remarcado = await _processos.MarcarAnteriorAoRegimeAsync(historico.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true }, ctx);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, remarcado.Origem);
    }

    [Fact]
    public async Task Marcar_JaHistorico_PodeTrocarONumeroDoContrato()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);

        var resposta = await _processos.MarcarAnteriorAoRegimeAsync(historico.Id,
            new CtrAnteriorAoRegimeDTO { Anterior = true, NumeroContrato = "Contrato nº 1/2020" }, ctx);

        Assert.Equal("Contrato nº 1/2020", resposta.NumeroContrato);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, resposta.Origem);
    }

    // ── Manifestação ao TCDF no histórico ────────────────────────────────────

    [Fact]
    public async Task Historico_RecebeManifestacaoAoTcdf_PeloIncisoII()
    {
        var ctx = await ContextoAnalistaAsync();
        var historico = SemearHistorico(NumeroHistorico);
        var manifestacoes = NovoManifestacaoService();

        var resposta = await manifestacoes.CriarAsync(historico.Id, NovaManifestacaoIncisoII(), ctx);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, resposta.Origem);

        // O inciso I reporta a criticidade, que o registro histórico não tem
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            manifestacoes.CriarAsync(historico.Id, NovaManifestacaoIncisoI(), ctx));
        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("inciso II", ex.Error.Message);

        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.AnteriorAoRegime });
        Assert.Equal(1, lista.Items.Single().TotalManifestacoes);
    }

    // ══ 4. Painel interno e painel público ═════════════════════════════════════

    [Fact]
    public async Task Painel_EstatisticasSemOsHistoricos_EOBlocoProprio()
    {
        // Supervisão: dois em análise (um do TCDF) e um assinado
        SemearProcesso(Numero, p =>
        {
            p.OrgaoSigla = "SES";
            p.ChegadaSgdi = DiasAtras(30);
        });
        SemearProcesso("04044-00001001/2026-12", p =>
        {
            p.OrgaoSigla = "PCDF";
            p.Origem = CtrDominios.Origem.Tcdf;
        });
        SemearProcesso("04044-00001002/2026-13", p =>
        {
            p.OrgaoSigla = "SES";
            p.ChegadaSgdi = DiasAtras(40);
            p.DataAssinaturaContrato = DiasAtras(20);
        });
        // Históricos, com categorias e órgãos que a supervisão não tem
        SemearHistorico("00060-00000001/2019-11", p =>
        {
            p.OrgaoSigla = "CAESB";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.Nuvem;
            p.ChegadaSgdi = DiasAtras(1000);
        });
        SemearHistorico("00060-00000002/2019-12", p =>
        {
            p.OrgaoSigla = "CAESB";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.TokensIa;
        });
        SemearHistorico("00060-00000003/2019-13", p =>
        {
            p.OrgaoSigla = "DETRAN-DF";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.Nuvem;
        });
        SemearHistorico("00060-00000004/2019-14", p => p.Ativo = false);

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(3, painel.TotalAtivos);
        Assert.Equal(11, painel.PorSituacao.Count);
        Assert.Equal(3, painel.PorSituacao.Sum(c => c.Quantidade));
        Assert.Equal(1, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.ContratoAssinado).Quantidade);
        Assert.Equal(1, painel.TotalConcluidos);
        Assert.Equal(new[] { "04044-00001002/2026-13" }, painel.Concluidos.Select(c => c.NumeroProcesso));
        Assert.Equal(new[] { CtrDominios.CategoriaObjeto.InfraestruturaRede }, painel.PorCategoria.Select(c => c.Chave));
        Assert.Equal(new[] { "SES", "PCDF" }, painel.PorOrgao.Select(c => c.Chave));
        Assert.Equal(3, painel.PorTipoContratacao.Sum(c => c.Quantidade));
        Assert.Equal(3, painel.PorNivelRiscoDeclarado.Sum(c => c.Quantidade));
        // O histórico com chegada e assinatura não entra na média (seria de 1000 dias)
        Assert.Equal(20d, painel.TemposMedios.ChegadaParaAssinatura);

        Assert.Equal(3, painel.Historicos.Total);
        Assert.Equal(new[] { (CtrDominios.CategoriaObjeto.Nuvem, 2), (CtrDominios.CategoriaObjeto.TokensIa, 1) },
            painel.Historicos.PorCategoria.Select(c => (c.Chave, c.Quantidade)));
        Assert.Equal(new[] { ("CAESB", 2), ("DETRAN-DF", 1) },
            painel.Historicos.PorOrgao.Select(c => (c.Chave, c.Quantidade)));
    }

    [Fact]
    public async Task Painel_SemHistoricos_OBlocoVemZerado()
    {
        SemearProcesso(Numero);

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(0, painel.Historicos.Total);
        Assert.Empty(painel.Historicos.PorCategoria);
        Assert.Empty(painel.Historicos.PorOrgao);
    }

    [Fact]
    public async Task Painel_GargalosIgnoramAsQuatroTerminais()
    {
        var antigo = DiasAtras(60);
        SemearProcesso("04044-00001001/2026-11", p => p.DataAssinaturaContrato = antigo);
        SemearProcesso("04044-00001002/2026-12", p =>
        {
            p.Restituido = true;
            p.RestituidoEm = antigo;
            p.RestituidoMotivo = "motivo";
        });
        SemearProcesso("04044-00001003/2026-13", p =>
        {
            p.ChegadaSgdi = antigo;
            p.NotaInformativaEnviadaEm = antigo;
        });
        SemearProcesso("04044-00001004/2026-14", p =>
        {
            p.ChegadaSgdi = antigo;
            p.RetornoOrgao = antigo;
        });
        // Os dois que a SGDI ainda espera entram nos gargalos
        SemearProcesso("04044-00001005/2026-15", p =>
        {
            p.ChegadaSgdi = antigo;
            p.EsclarecimentoSolicitadoEm = antigo;
            p.EsclarecimentoDescricao = "pedido";
        });
        SemearProcesso("04044-00001006/2026-16", p =>
        {
            p.ChegadaSgdi = antigo;
            p.AnaliseTecnicaEncaminhadaEm = antigo;
            p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        });
        // Histórico parado há muito tempo também não é gargalo (nem entra na supervisão)
        SemearHistorico(NumeroHistorico, p => p.ChegadaSgdi = DiasAtras(900));

        var painel = await _processos.MontarPainelAsync(15);

        Assert.Equal(
            new[] { CtrDominios.Situacao.EsclarecimentosSolicitados, CtrDominios.Situacao.AguardandoAnaliseTecnica },
            painel.Gargalos.Select(g => g.Situacao).OrderByDescending(s => s));
    }

    [Fact]
    public async Task Painel_NotaContaComoMovimentacao()
    {
        SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(60);
            p.EsclarecimentoSolicitadoEm = DiasAtras(60);
            p.EsclarecimentoDescricao = "pedido";
        });
        var semNota = await _processos.MontarPainelAsync(15);
        Assert.Equal(60, Assert.Single(semNota.Gargalos).DiasSemMovimento);

        // Com a nota, a situação vira terminal e a data dela é a última movimentação
        var processo = Context.CtrProcessos.Single();
        processo.NotaInformativaEnviadaEm = DiasAtras(2);
        Context.SaveChanges();

        var comNota = await _processos.MontarPainelAsync(0);
        Assert.Empty(comNota.Gargalos);
        var lista = await _processos.ListarAsync(new CtrProcessoFiltro());
        Assert.Equal(2, lista.Items.Single().DiasSemMovimento);
    }

    [Fact]
    public async Task PainelPublico_NaoRecebeOsHistoricos()
    {
        SemearProcesso(Numero, p => p.OrgaoSigla = "SES");
        SemearHistorico(NumeroHistorico, p => p.OrgaoSigla = "CAESB");

        var publico = await _processos.MontarPainelPublicoAsync();

        Assert.Equal(new[] { "SES" }, publico.Contratacoes.Select(c => c.OrgaoSigla));
    }

    // ══ 5. Planilha ═══════════════════════════════════════════════════════════

    [Fact]
    public void Csv_Cabecalho_TemAsQuatroColunasNovasNoFim()
    {
        Assert.Equal(36, CtrCsv.Cabecalho.Length);
        Assert.Equal(new[]
        {
            "Nota informativa enviada em", "Documento SEI da nota informativa", "Número do contrato",
            "Documento SEI da autorização"
        }, CtrCsv.Cabecalho[32..]);

        Assert.True(CtrCsvColunasOpcionais.Todas.NotaInformativaEnviadaEm);
        Assert.True(CtrCsvColunasOpcionais.Todas.NotaInformativaDocumentoSei);
        Assert.True(CtrCsvColunasOpcionais.Todas.NumeroContrato);
        Assert.True(CtrCsvColunasOpcionais.Todas.AutorizacaoDocumentoSei);
    }

    [Fact]
    public async Task Csv_RoundTrip_ExportImport_SemPerda()
    {
        var ctx = await ContextoAnalistaAsync();
        var supervisao = await _processos.CriarAsync(Montar(NovoProcessoDto(Numero), d =>
        {
            d.ChegadaSgdi = DiasAtras(30);
            d.NotaInformativaEnviadaEm = DiasAtras(3);
            d.NotaInformativaDocumentoSei = "=213807905";   // proteção contra fórmula no export
            d.NumeroContrato = "Contrato nº 5/2026";
        }), ctx);
        var historico = await _processos.CriarAsync(Montar(HistoricoDto(), d =>
        {
            d.ChegadaSgdi = DiasAtras(900);
            d.AutorizacaoDocumentoSei = "45678901";
        }), ctx);

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro { IncluirConcluidos = true });
        var celulas = Enumerable.Range(1, 2).Select(l => Celulas(bytes, l)).ToList();
        Assert.All(celulas, c => Assert.Equal(36, c.Length));
        var linhaHistorico = celulas.Single(c => c[0] == NumeroHistorico);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, linhaHistorico[18]);
        Assert.Equal(new[] { "", "", Contrato, "45678901" }, linhaHistorico[32..]);
        var linhaSupervisao = celulas.Single(c => c[0] == Numero);
        Assert.Equal(new[] { DiasAtras(3).ToString("dd/MM/yyyy"), "'=213807905", "Contrato nº 5/2026", "" },
            linhaSupervisao[32..]);

        // Prévia: as duas linhas atualizam o que já existe, sem recusa
        var previa = await NovoImportacaoService().PreviaAsync(bytes);
        Assert.All(previa.Linhas, l =>
        {
            Assert.Equal(CtrImportacaoAcao.Atualizar, l.Acao);
            Assert.Null(l.Motivo);
        });

        // Excluídos os dois, a importação os recria iguais
        await _processos.ExcluirAsync(supervisao.Id, ctx);
        await _processos.ExcluirAsync(historico.Id, ctx);
        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);
        Assert.Equal(2, relatorio.Criados);

        var novaSupervisao = Context.CtrProcessos.Single(p => p.Ativo && p.NumeroProcesso == Numero);
        Assert.Equal(DiasAtras(3), novaSupervisao.NotaInformativaEnviadaEm);
        Assert.Equal("=213807905", novaSupervisao.NotaInformativaDocumentoSei);
        Assert.Equal("Contrato nº 5/2026", novaSupervisao.NumeroContrato);
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, novaSupervisao.Origem);

        var novoHistorico = Context.CtrProcessos.Single(p => p.Ativo && p.NumeroProcesso == NumeroHistorico);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, novoHistorico.Origem);
        Assert.Equal(Contrato, novoHistorico.NumeroContrato);
        Assert.Equal("45678901", novoHistorico.AutorizacaoDocumentoSei);
        Assert.Equal(DiasAtras(800), novoHistorico.DataAssinaturaContrato);
        Assert.Equal(DiasAtras(900), novoHistorico.ChegadaSgdi);
        Assert.Null(novoHistorico.Criticidade);
    }

    private static CtrProcessoUpdateDTO Montar(CtrProcessoUpdateDTO dto, Action<CtrProcessoUpdateDTO> ajustar)
    {
        ajustar(dto);
        return dto;
    }

    /// <summary>Arquivo com as primeiras <paramref name="colunas"/> colunas do export e uma linha.</summary>
    private static byte[] Arquivo(int colunas, string numero, string origem, Dictionary<int, string>? celulas = null)
    {
        var linha = new string[colunas];
        Array.Fill(linha, string.Empty);
        linha[0] = numero;
        linha[1] = "Economia";
        linha[2] = "SEEC";
        linha[4] = "Switches";
        linha[5] = CtrDominios.CategoriaObjeto.InfraestruturaRede;
        linha[12] = "Não";
        if (colunas > 18) linha[18] = origem;
        foreach (var (coluna, valor) in celulas ?? new Dictionary<int, string>()) linha[coluna] = valor;
        return BytesUtf8ComBom(string.Join(";", CtrCsv.Cabecalho[..colunas]) + "\r\n" + string.Join(";", linha) + "\r\n");
    }

    [Fact]
    public async Task Csv_ColunasAusentes_PreservamOGravado()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso(Numero, p =>
        {
            p.Criticidade = null;
            p.NotaInformativaEnviadaEm = DiasAtras(3);
            p.NotaInformativaDocumentoSei = "777";
            p.NumeroContrato = Contrato;
            p.AutorizacaoDocumentoSei = "888";
        });

        // Arquivo de 32 colunas (o export de antes): as quatro novas nem existem nele
        await NovoImportacaoService().ImportarAsync(Arquivo(32, Numero, CtrDominios.Origem.OrgaoComunicante), ctx);

        var gravado = Context.CtrProcessos.Single();
        Assert.Equal(DiasAtras(3), gravado.NotaInformativaEnviadaEm);
        Assert.Equal("777", gravado.NotaInformativaDocumentoSei);
        Assert.Equal(Contrato, gravado.NumeroContrato);
        Assert.Equal("888", gravado.AutorizacaoDocumentoSei);
    }

    [Fact]
    public async Task Csv_ColunasPresentesEVazias_Limpam()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso(Numero, p =>
        {
            p.NotaInformativaEnviadaEm = DiasAtras(3);
            p.NotaInformativaDocumentoSei = "777";
            p.NumeroContrato = Contrato;
            p.AutorizacaoDocumentoSei = "888";
        });

        await NovoImportacaoService().ImportarAsync(Arquivo(36, Numero, CtrDominios.Origem.OrgaoComunicante), ctx);

        var gravado = Context.CtrProcessos.Single();
        Assert.Null(gravado.NotaInformativaEnviadaEm);
        Assert.Null(gravado.NotaInformativaDocumentoSei);
        Assert.Null(gravado.NumeroContrato);
        Assert.Null(gravado.AutorizacaoDocumentoSei);
    }

    [Theory]
    [InlineData("31/02/2026", "data inválida em Nota informativa enviada em")]
    [InlineData("-", "data inválida em Nota informativa enviada em")]
    public void Csv_NotaComDataInvalida_RejeitaNomeandoAColuna(string valor, string motivo)
    {
        var lidas = CtrCsv.Ler(Arquivo(36, Numero, CtrDominios.Origem.OrgaoComunicante,
            new Dictionary<int, string> { [32] = valor }));

        Assert.StartsWith(motivo, Assert.Single(lidas).Erro);
    }

    [Fact]
    public async Task Csv_DocumentoDaNotaAcimaDe25_RejeitaALinha()
    {
        var previa = await NovoImportacaoService().PreviaAsync(Arquivo(36, Numero, CtrDominios.Origem.OrgaoComunicante,
            new Dictionary<int, string> { [32] = DiasAtras(1).ToString("dd/MM/yyyy"), [33] = new string('1', 26) }));

        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(CtrImportacaoAcao.Rejeitar, linha.Acao);
        Assert.Contains("até 25 caracteres", linha.Motivo);
    }

    [Fact]
    public async Task Csv_HistoricoNovo_ExigeOsObrigatorios_ENaoMarcaNemDesfazOExistente()
    {
        var ctx = await ContextoAnalistaAsync();
        var assinatura = new Dictionary<int, string> { [16] = DiasAtras(800).ToString("dd/MM/yyyy") };

        var semNumero = await NovoImportacaoService().PreviaAsync(
            Arquivo(36, NumeroHistorico, CtrDominios.Origem.AnteriorAoRegime, assinatura));
        Assert.Equal(CtrImportacaoAcao.Rejeitar, semNumero.Linhas.Single().Acao);
        Assert.Contains("número do contrato", semNumero.Linhas.Single().Motivo);

        assinatura[34] = Contrato;
        var completo = await NovoImportacaoService().ImportarAsync(
            Arquivo(36, NumeroHistorico, CtrDominios.Origem.AnteriorAoRegime, assinatura), ctx);
        Assert.Equal(1, completo.Criados);
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, Context.CtrProcessos.Single().Origem);

        // A planilha não desfaz o registro histórico (é a ação própria do processo)
        var desfazer = await NovoImportacaoService().PreviaAsync(
            Arquivo(36, NumeroHistorico, CtrDominios.Origem.OrgaoComunicante, assinatura));
        Assert.Equal(CtrImportacaoAcao.Rejeitar, desfazer.Linhas.Single().Acao);
        Assert.Contains("use a ação própria", desfazer.Linhas.Single().Motivo);

        // A planilha legada (sem a coluna Origem) preserva o histórico
        var legada = await NovoImportacaoService().ImportarAsync(Arquivo(15, NumeroHistorico, string.Empty), ctx);
        Assert.Equal(1, legada.Atualizados);
        var gravado = Context.CtrProcessos.Single();
        Assert.Equal(CtrDominios.Origem.AnteriorAoRegime, gravado.Origem);
        Assert.Equal(Contrato, gravado.NumeroContrato);
    }
}
