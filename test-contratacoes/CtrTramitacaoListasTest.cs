using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-09-24 da equipe da SGDI, fora da criticidade: Nº SEI do Formulário na
/// identificação, documento SEI do pedido de esclarecimentos, encaminhamento para análise
/// técnica (SUBSIS ou SUBINFRA) com o retorno, restituição que as telas deixam de pedir sem
/// perder o gravado, as duas listas separadas pela origem e a coluna STATUS.
/// </summary>
public class CtrTramitacaoListasTest : CtrTestBase
{
    private const string Numero = "04044-00000800/2026-11";
    private const string Formulario = "04044-00009999/2026-55";

    private readonly CtrProcessoService _processos;

    public CtrTramitacaoListasTest()
    {
        _processos = NovoProcessoService();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private static CtrProcessoUpdateDTO EdicaoDe(CtrProcesso p)
    {
        var dto = new CtrProcessoUpdateDTO();
        var gravado = CtrProcessoService.DtoDe(p);
        foreach (var prop in typeof(CtrProcessoCreateDTO).GetProperties())
            prop.SetValue(dto, prop.GetValue(gravado));
        return dto;
    }

    /// <summary>Processo com análise técnica encaminhada à SUBSIS e respondida.</summary>
    private CtrProcesso SemearComAnaliseTecnica(string numero = Numero) => SemearProcesso(numero, p =>
    {
        p.ChegadaSgdi = DiasAtras(30);
        p.NumeroSeiFormulario = Formulario;
        p.AnaliseTecnicaEncaminhadaEm = DiasAtras(20);
        p.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        p.AnaliseTecnicaRetornoEm = DiasAtras(12);
        p.AnaliseTecnicaRetornoResumo = "Sem óbices técnicos; recomenda revisar o SLA.";
        p.EsclarecimentoSolicitadoEm = DiasAtras(25);
        p.EsclarecimentoDescricao = "Pedido de informações complementares";
        p.EsclarecimentoDocumentoSei = "187654321";
    });

    private async Task<ApiException> RecusaAoCriarAsync(CtrProcessoUpdateDTO dto)
    {
        var ctx = await ContextoAnalistaAsync();
        return await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));
    }

    // ══ 5. Nº SEI do Formulário ══════════════════════════════════════════════

    [Fact]
    public async Task NumeroSeiFormulario_Opcional_GravadoSemEspacosNasPontas()
    {
        var ctx = await ContextoAnalistaAsync();

        var sem = await _processos.CriarAsync(NovoProcessoDto(Numero), ctx);
        Assert.Null(sem.NumeroSeiFormulario);

        var dto = NovoProcessoDto("04044-00000801/2026-12");
        dto.NumeroSeiFormulario = "  " + Formulario + " ";
        var com = await _processos.CriarAsync(dto, ctx);
        Assert.Equal(Formulario, com.NumeroSeiFormulario);
        Assert.Equal(Formulario, Context.CtrProcessos.Single(p => p.Id == com.Id).NumeroSeiFormulario);
    }

    // Desde 2026-09-25 o campo é o número de um documento dentro do processo SEI (como 213807905),
    // em qualquer formato: só o tamanho é conferido
    [Theory]
    [InlineData("213807905")]
    [InlineData("21380-7905")]
    [InlineData("12345")]
    [InlineData("04044-00009999/26-55")]
    [InlineData("SEI 213807905")]
    [InlineData("1234567890123456789012345")]
    public async Task NumeroSeiFormulario_TextoLivre_EhAceito(string valor)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.NumeroSeiFormulario = valor;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(valor, resposta.NumeroSeiFormulario);
        Assert.Equal(valor, Context.CtrProcessos.Single(p => p.Id == resposta.Id).NumeroSeiFormulario);
    }

    [Fact]
    public async Task NumeroSeiFormulario_AcimaDe25Caracteres_EhRecusado()
    {
        var dto = NovoProcessoDto(Numero);
        dto.NumeroSeiFormulario = "12345678901234567890123456";

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Equal("O Nº SEI do Formulário tem até 25 caracteres.", ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
    }

    [Fact]
    public async Task NumeroSeiFormulario_EmBrancoViraNulo_ENaEdicaoNuloLimpa()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComAnaliseTecnica();

        var dto = EdicaoDe(processo);
        dto.NumeroSeiFormulario = "   ";
        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Null(resposta.NumeroSeiFormulario);
    }

    // ══ 6. Documento SEI do pedido de esclarecimentos ════════════════════════

    [Fact]
    public async Task EsclarecimentoDocumentoSei_GravadoComOPedido_EAnuladoSemAData()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.EsclarecimentoSolicitadoEm = DiasAtras(3);
        dto.EsclarecimentoDescricao = "Informações complementares sobre o ETP";
        dto.EsclarecimentoDocumentoSei = " 187654321 ";

        var resposta = await _processos.CriarAsync(dto, ctx);
        Assert.Equal("187654321", resposta.EsclarecimentoDocumentoSei);

        // Limpar a data do pedido limpa o bloco (é gesto do usuário, não erro)
        var edicao = EdicaoDe(Context.CtrProcessos.Single(p => p.Id == resposta.Id));
        edicao.EsclarecimentoSolicitadoEm = null;
        var limpo = await _processos.AtualizarAsync(resposta.Id, edicao, ctx);

        Assert.Null(limpo.EsclarecimentoDocumentoSei);
        Assert.Null(limpo.EsclarecimentoDescricao);
    }

    [Fact]
    public async Task EsclarecimentoDocumentoSei_Opcional()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.EsclarecimentoSolicitadoEm = DiasAtras(3);
        dto.EsclarecimentoDescricao = "Informações complementares sobre o ETP";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Null(resposta.EsclarecimentoDocumentoSei);
        Assert.True(resposta.EsclarecimentoPendente);
    }

    // ══ 7. Análise técnica ═══════════════════════════════════════════════════

    [Fact]
    public async Task AnaliseTecnica_Completa_EhGravadaComAAreaNaGrafiaDoDominio()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(10);
        dto.AnaliseTecnicaArea = " subinfra ";
        dto.AnaliseTecnicaRetornoEm = DiasAtras(4);
        dto.AnaliseTecnicaRetornoResumo = "  Parecer favorável com ressalvas de capacidade.  ";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(DiasAtras(10), resposta.AnaliseTecnicaEncaminhadaEm);
        Assert.Equal(CtrDominios.AreaTecnica.Subinfra, resposta.AnaliseTecnicaArea);
        Assert.Equal(DiasAtras(4), resposta.AnaliseTecnicaRetornoEm);
        Assert.Equal("Parecer favorável com ressalvas de capacidade.", resposta.AnaliseTecnicaRetornoResumo);
        // Não muda a situação do trâmite
        Assert.Equal(CtrDominios.Situacao.SemMovimentacao, resposta.Situacao);
    }

    [Fact]
    public async Task AnaliseTecnica_SoOEncaminhamentoComAArea_EhAceito()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = Hoje;
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.AreaTecnica.Subsis, resposta.AnaliseTecnicaArea);
        Assert.Null(resposta.AnaliseTecnicaRetornoEm);
    }

    [Fact]
    public async Task AnaliseTecnica_DataSemArea_EhRecusada()
    {
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(2);

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("Informe a área técnica (SUBSIS ou SUBINFRA)", ex.Error.Message);
    }

    [Fact]
    public async Task AnaliseTecnica_AreaForaDoDominio_EhRecusada()
    {
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(2);
        dto.AnaliseTecnicaArea = "SUBGD";

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("Área técnica inválida: SUBGD", ex.Error.Message);
    }

    [Fact]
    public async Task AnaliseTecnica_RetornoSemEncaminhamento_EhRecusado()
    {
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaRetornoEm = DiasAtras(2);
        dto.AnaliseTecnicaRetornoResumo = "Parecer favorável";

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("Informe a data do encaminhamento para análise técnica antes de registrar o retorno",
            ex.Error.Message);
    }

    [Fact]
    public async Task AnaliseTecnica_RetornoAntesDoEncaminhamento_EhRecusado()
    {
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(5);
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        dto.AnaliseTecnicaRetornoEm = DiasAtras(6);

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        Assert.Contains("não pode ser anterior à data do encaminhamento", ex.Error.Message);
    }

    [Fact]
    public async Task AnaliseTecnica_RetornoNoMesmoDiaDoEncaminhamento_EhAceito()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(5);
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        dto.AnaliseTecnicaRetornoEm = DiasAtras(5);

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(DiasAtras(5), resposta.AnaliseTecnicaRetornoEm);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnaliseTecnica_DataFutura_EhRecusada(bool noEncaminhamento)
    {
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subinfra;
        dto.AnaliseTecnicaEncaminhadaEm = noEncaminhamento ? Hoje.AddDays(1) : DiasAtras(3);
        if (!noEncaminhamento) dto.AnaliseTecnicaRetornoEm = Hoje.AddDays(1);

        var ex = await RecusaAoCriarAsync(dto);

        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, ex.Error.Code);
        Assert.Contains("não pode ser futura", ex.Error.Message);
    }

    [Fact]
    public async Task AnaliseTecnica_SemAsDatas_AAreaEOResumoSaoAnulados()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;          // sem a data do encaminhamento
        dto.AnaliseTecnicaRetornoResumo = "Resumo sem a data do retorno";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Null(resposta.AnaliseTecnicaArea);
        Assert.Null(resposta.AnaliseTecnicaRetornoResumo);
    }

    [Fact]
    public async Task AnaliseTecnica_SemDataDoRetorno_OResumoEhAnulado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComAnaliseTecnica();

        var dto = EdicaoDe(processo);
        dto.AnaliseTecnicaRetornoEm = null;
        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Null(resposta.AnaliseTecnicaRetornoResumo);
        Assert.Equal(CtrDominios.AreaTecnica.Subsis, resposta.AnaliseTecnicaArea);
    }

    [Fact]
    public async Task AnaliseTecnica_EhMovimentacaoDoProcesso()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.ChegadaSgdi = DiasAtras(40);
        dto.AnaliseTecnicaEncaminhadaEm = DiasAtras(9);
        dto.AnaliseTecnicaArea = CtrDominios.AreaTecnica.Subsis;
        dto.AnaliseTecnicaRetornoEm = DiasAtras(2);

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(DiasAtras(2), resposta.UltimaMovimentacao);
        Assert.Equal(2, resposta.DiasSemMovimento);

        // O painel usa a mesma conta: parado há 2 dias não é gargalo de 15
        var painel = await _processos.MontarPainelAsync(15);
        Assert.DoesNotContain(painel.Gargalos, p => p.NumeroProcesso == Numero);
    }

    [Fact]
    public async Task AnaliseTecnica_NaEdicaoNuloLimpa()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComAnaliseTecnica();

        var dto = EdicaoDe(processo);
        dto.AnaliseTecnicaEncaminhadaEm = null;
        dto.AnaliseTecnicaArea = null;
        dto.AnaliseTecnicaRetornoEm = null;
        dto.AnaliseTecnicaRetornoResumo = null;
        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Null(resposta.AnaliseTecnicaEncaminhadaEm);
        Assert.Null(resposta.AnaliseTecnicaArea);
        Assert.Null(resposta.AnaliseTecnicaRetornoEm);
        Assert.Null(resposta.AnaliseTecnicaRetornoResumo);
    }

    [Fact]
    public async Task CamposNovos_CheckpointNaoMexeNeles()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComAnaliseTecnica();

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.ChegadaSubgd,
            Data = DiasAtras(1)
        }, ctx);

        Assert.Equal(Formulario, resposta.NumeroSeiFormulario);
        Assert.Equal("187654321", resposta.EsclarecimentoDocumentoSei);
        Assert.Equal(CtrDominios.AreaTecnica.Subsis, resposta.AnaliseTecnicaArea);
        Assert.Equal(DiasAtras(20), resposta.AnaliseTecnicaEncaminhadaEm);
        Assert.Equal(DiasAtras(12), resposta.AnaliseTecnicaRetornoEm);
        Assert.Equal("Sem óbices técnicos; recomenda revisar o SLA.", resposta.AnaliseTecnicaRetornoResumo);
    }

    [Fact]
    public async Task CamposNovos_ReimportarAPlanilha_NaoOsApaga()
    {
        // A planilha não tem coluna para eles: nem a exportada (32 colunas) nem a legada
        var ctx = await ContextoAnalistaAsync();
        SemearComAnaliseTecnica();

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);

        Assert.Equal(1, relatorio.Atualizados);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Equal(Formulario, gravado.NumeroSeiFormulario);
        Assert.Equal("187654321", gravado.EsclarecimentoDocumentoSei);
        Assert.Equal(CtrDominios.AreaTecnica.Subsis, gravado.AnaliseTecnicaArea);
        Assert.Equal(DiasAtras(20), gravado.AnaliseTecnicaEncaminhadaEm);
        Assert.Equal(DiasAtras(12), gravado.AnaliseTecnicaRetornoEm);
        Assert.Equal("Sem óbices técnicos; recomenda revisar o SLA.", gravado.AnaliseTecnicaRetornoResumo);

        var legada = "Analises de contratações;;;;;;;;;;;\r\n"
            + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
            + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
            + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
            + $"{Numero};Economia;SEEC;;Switches;Infraestrutura de Rede;{DiasAtras(30):dd/MM/yyyy};;;;;\r\n";
        Assert.Equal(1, (await NovoImportacaoService().ImportarAsync(Bytes1252(legada), ctx)).Atualizados);
        gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Equal(Formulario, gravado.NumeroSeiFormulario);
        Assert.Equal(CtrDominios.AreaTecnica.Subsis, gravado.AnaliseTecnicaArea);
        Assert.Equal("187654321", gravado.EsclarecimentoDocumentoSei);
    }

    // ══ 8. Restituição: as telas não pedem mais, o gravado fica ══════════════

    [Fact]
    public async Task Restituicao_EdicaoQueDevolveOGravado_NaoApagaNada()
    {
        // O formulário sem a seção da restituição devolve o que veio no GET
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(10);
            p.RestituidoMotivo = "Faltou o ETP";
        });

        var dto = EdicaoDe(processo);
        dto.Observacao = "Só a observação mudou";
        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.True(resposta.Restituido);
        Assert.Equal(DiasAtras(10), resposta.RestituidoEm);
        Assert.Equal("Faltou o ETP", resposta.RestituidoMotivo);
        Assert.Equal(CtrDominios.Situacao.Restituido, resposta.Situacao);
    }

    // ══ 9. Duas listas pela origem ═══════════════════════════════════════════

    [Fact]
    public async Task Listas_ProcessosDeSupervisaoEComunicacoesDoTcdf_SeparamPelaOrigem()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00000810/2026-11", p => p.ChegadaSgdi = DiasAtras(10));
        SemearProcesso("04044-00000811/2026-12", p => p.ChegadaSgdi = DiasAtras(20));
        // A comunicação do TCDF de uma contratação que o módulo não tinha cria o processo dela
        await NovoManifestacaoService().CriarComunicacaoAsync(new CtrComunicacaoTcdfCreateDTO
        {
            ProcessoComunicacaoTcdf = "00600-00008620/2026-66",
            OficioTcdf = "6018/2026-GP",
            DataOficio = DiasAtras(10),
            DataRecebimento = DiasAtras(8),
            AtoTcdf = CtrDominios.AtoTcdf.Decisao,
            NumeroAtoTcdf = "1234/2026",
            SituacaoPortfolio = CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
            PrazoRegularizacaoDias = 5,
            Contratacao = new CtrContratacaoTcdfDTO
            {
                OrgaoNome = "Departamento de Trânsito do Distrito Federal",
                OrgaoSigla = "DETRAN",
                Objeto = "Subscrições de software",
                CategoriaObjeto = CtrDominios.CategoriaObjeto.LicenciamentoSoftware
            }
        }, ctx);

        var supervisao = await _processos.ListarAsync(new CtrProcessoFiltro
        { Origem = CtrDominios.Origem.OrgaoComunicante, PageSize = 50 });
        var tcdf = await _processos.ListarAsync(new CtrProcessoFiltro
        { Origem = CtrDominios.Origem.Tcdf, PageSize = 50 });
        var todos = await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });

        Assert.Equal(new[] { "04044-00000810/2026-11", "04044-00000811/2026-12" },
            supervisao.Items.Select(p => p.NumeroProcesso).OrderBy(n => n));
        Assert.Equal("00600-00008620/2026-66", Assert.Single(tcdf.Items).NumeroProcesso);
        // Uma e outra, sem sobra nem repetição
        Assert.Equal(todos.TotalItems, supervisao.TotalItems + tcdf.TotalItems);
        Assert.Empty(supervisao.Items.Select(p => p.Id).Intersect(tcdf.Items.Select(p => p.Id)));

        // Cada lista com os filtros de hoje: a situação combina com a origem
        var tcdfSemMovimento = await _processos.ListarAsync(new CtrProcessoFiltro
        { Origem = CtrDominios.Origem.Tcdf, Situacao = CtrDominios.Situacao.SemMovimentacao, PageSize = 50 });
        Assert.Single(tcdfSemMovimento.Items);
        var supervisaoSemMovimento = await _processos.ListarAsync(new CtrProcessoFiltro
        { Origem = CtrDominios.Origem.OrgaoComunicante, Situacao = CtrDominios.Situacao.SemMovimentacao, PageSize = 50 });
        Assert.Empty(supervisaoSemMovimento.Items);

        // O CSV de cada lista traz só a lista
        var csvTcdf = CtrCsv.Ler(await _processos.ExportarCsvAsync(new CtrProcessoFiltro { Origem = CtrDominios.Origem.Tcdf }));
        Assert.Equal("00600-00008620/2026-66", Assert.Single(csvTcdf).NumeroProcesso);
        var csvSupervisao = CtrCsv.Ler(await _processos.ExportarCsvAsync(new CtrProcessoFiltro
        { Origem = CtrDominios.Origem.OrgaoComunicante }));
        Assert.Equal(2, csvSupervisao.Count);
    }

    [Fact]
    public async Task Listas_OrigemForaDoDominio_DevolveListaVazia()
    {
        SemearProcesso("04044-00000812/2026-13");

        var resposta = await _processos.ListarAsync(new CtrProcessoFiltro { Origem = "Outra", PageSize = 50 });

        Assert.Empty(resposta.Items);
    }

    // ══ 10. Coluna STATUS ════════════════════════════════════════════════════

    [Fact]
    public void Status_SegueARegraDeConclusaoDoModulo()
    {
        Assert.Equal(new[] { "Em regime de supervisão", "Concluída" }, CtrDominios.StatusSupervisao.Todos);
        Assert.Equal(CtrDominios.StatusSupervisao.EmRegimeSupervisao, CtrProcessoService.CalcularStatusSupervisao((DateOnly?)null));
        Assert.Equal(CtrDominios.StatusSupervisao.Concluida, CtrProcessoService.CalcularStatusSupervisao(DiasAtras(1)));
    }

    [Fact]
    public async Task Status_NaLista_ConcluidaSoComOContratoAssinado()
    {
        // Uma amostra de cada situação: só o contrato assinado conclui a supervisão
        SemearProcesso("04044-00000820/2026-11");
        SemearProcesso("04044-00000821/2026-12", p => p.ChegadaSgdi = DiasAtras(10));
        SemearProcesso("04044-00000822/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.RetornoGabSgdi = DiasAtras(5);
            p.RetornoOrgao = DiasAtras(2);   // Análise concluída: segue em supervisão
        });
        SemearProcesso("04044-00000823/2026-14", p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(3);
            p.RestituidoMotivo = "Faltou documento";
        });
        SemearProcesso("04044-00000824/2026-15", p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.DataAssinaturaContrato = DiasAtras(1);
        });

        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { IncluirConcluidos = true, PageSize = 50 });

        Assert.Equal(5, lista.TotalItems);
        Assert.All(lista.Items, p => Assert.Equal(
            p.Situacao == CtrDominios.Situacao.Concluido
                ? CtrDominios.StatusSupervisao.Concluida
                : CtrDominios.StatusSupervisao.EmRegimeSupervisao,
            p.StatusSupervisao));
        Assert.Equal(CtrDominios.StatusSupervisao.Concluida,
            lista.Items.Single(p => p.NumeroProcesso == "04044-00000824/2026-15").StatusSupervisao);
        Assert.Equal(CtrDominios.StatusSupervisao.EmRegimeSupervisao,
            lista.Items.Single(p => p.NumeroProcesso == "04044-00000822/2026-13").StatusSupervisao);
    }

    [Fact]
    public async Task Status_MudaComACheckpointDaAssinatura()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso(Numero, p => p.ChegadaSgdi = DiasAtras(10));

        var assinado = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        { Etapa = CtrDominios.Etapa.AssinaturaContrato, Data = DiasAtras(1) }, ctx);
        Assert.Equal(CtrDominios.StatusSupervisao.Concluida, assinado.StatusSupervisao);

        var reaberto = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        { Etapa = CtrDominios.Etapa.AssinaturaContrato, Data = null }, ctx);
        Assert.Equal(CtrDominios.StatusSupervisao.EmRegimeSupervisao, reaberto.StatusSupervisao);
    }

    // ══ Contrato: os DTOs só ganharam campos ═════════════════════════════════

    [Fact]
    public void Contrato_DtosGanharamOsCamposNovos_SemPerderOsAntigos()
    {
        var entrada = typeof(CtrProcessoCreateDTO).GetProperties().Select(p => p.Name).ToHashSet();
        var saida = typeof(CtrProcessoResponse).GetProperties().Select(p => p.Name).ToHashSet();

        foreach (var novo in new[]
                 {
                     "NumeroSeiFormulario", "EsclarecimentoDocumentoSei", "AnaliseTecnicaEncaminhadaEm",
                     "AnaliseTecnicaArea", "AnaliseTecnicaRetornoEm", "AnaliseTecnicaRetornoResumo"
                 })
        {
            Assert.Contains(novo, entrada);
            Assert.Contains(novo, saida);
        }

        Assert.Contains("PontosPorCriterio", saida);
        Assert.Contains("RespostaPerguntaAnteriorII", saida);
        Assert.Contains("StatusSupervisao", saida);

        // Os de antes continuam (inclusive a restituição, que as telas deixaram de pedir)
        foreach (var antigo in new[]
                 {
                     "Restituido", "RestituidoEm", "RestituidoMotivo", "CriteriosCriticidade", "Criticidade",
                     "Origem", "EsclarecimentoSolicitadoEm", "EsclarecimentoDescricao", "EsclarecimentoRespondidoEm"
                 })
        {
            Assert.Contains(antigo, entrada);
            Assert.Contains(antigo, saida);
        }
        Assert.Contains("PontosCriticidade", saida);
        Assert.Contains("Situacao", saida);
    }
}
