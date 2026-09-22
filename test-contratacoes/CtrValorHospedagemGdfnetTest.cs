using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Segundo pedido de 2026-09-22: três dados da contratação no processo (valor estimado,
/// hospedagem no CeTIC-DF e uso da rede GDFNet), as três colunas correspondentes no fim do
/// CSV (que passa a 32 colunas) e a resposta "Não foi possível avaliar com as informações
/// apresentadas" no critério IV da criticidade, que vale 0 ponto (como "Nenhum").
/// </summary>
public class CtrValorHospedagemGdfnetTest : CtrTestBase
{
    private const string Numero = "04044-00000600/2026-11";
    private const string OutroNumero = "04044-00000601/2026-12";
    private const string TerceiroNumero = "04044-00000602/2026-13";

    // As três colunas novas do CSV vêm logo depois dos sete critérios (22 a 28)
    private const int ColunaValor = 29;
    private const int ColunaHospedagem = 30;
    private const int ColunaGdfnet = 31;
    private const int ColunaCriterioII = 23;
    private const int ColunaCriterioIV = 25;
    private const int ColunaCriterioVI = 27;

    private const string NaoAvaliou = CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliar;

    private readonly CtrProcessoService _processos;

    public CtrValorHospedagemGdfnetTest()
    {
        _processos = NovoProcessoService();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>"I=Sim,II=Alto" vira o dicionário de respostas (vazio = todas no padrão).</summary>
    private static Dictionary<string, string> Respostas(string texto) =>
        texto.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(par => par.Split('='))
            .ToDictionary(par => par[0].Trim(), par => par[1].Trim());

    private static decimal Decimal(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    /// <summary>Processo com os três dados preenchidos (UsaGdfnet falso: falso não é "não informado").</summary>
    private CtrProcesso SemearComOsTres(string numero = Numero) => SemearProcesso(numero, p =>
    {
        p.ChegadaSgdi = DiasAtras(10);
        p.ValorEstimado = 1234567.89m;
        p.HospedagemCetic = CtrDominios.HospedagemCetic.NaoAplicavelSaas;
        p.UsaGdfnet = false;
    });

    private static void AssertOsTresPreservados(decimal? valor, string? hospedagem, bool? gdfnet)
    {
        Assert.Equal(1234567.89m, valor);
        Assert.Equal(CtrDominios.HospedagemCetic.NaoAplicavelSaas, hospedagem);
        Assert.False(gdfnet);
    }

    /// <summary>Entidade válida com o mínimo, para exercitar a validação sem o banco.</summary>
    private static CtrProcesso Candidato(Action<CtrProcesso> ajustar)
    {
        var candidato = new CtrProcesso();
        CtrProcessoService.AplicarDto(candidato, NovoProcessoDto(Numero));
        ajustar(candidato);
        return candidato;
    }

    /// <summary>
    /// CSV do nosso export com as primeiras <paramref name="colunas"/> colunas do cabeçalho
    /// (29 = o export de antes desta rodada; 32 = o de agora) e uma linha de dados em que só
    /// as células pedidas vêm preenchidas.
    /// </summary>
    private static byte[] Csv(int colunas, string numero, params (int Coluna, string Valor)[] celulas) =>
        BytesUtf8ComBom(string.Join(";", CtrCsv.Cabecalho[..colunas]) + "\r\n"
                        + Linha(colunas, numero, celulas) + "\r\n");

    private static string Linha(int colunas, string numero, params (int Coluna, string Valor)[] celulas)
    {
        var linha = new string[colunas];
        Array.Fill(linha, string.Empty);
        linha[0] = numero;
        linha[1] = "Secretaria de Estado de Economia";
        linha[2] = "SEEC";
        linha[4] = "Aquisição de switches de acesso";
        linha[5] = CtrDominios.CategoriaObjeto.InfraestruturaRede;
        linha[6] = DiasAtras(10).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        linha[12] = "Não";
        foreach (var (coluna, valor) in celulas) linha[coluna] = valor;
        return string.Join(";", linha);
    }

    /// <summary>A única linha lida do CSV, conferida sem erro.</summary>
    private static CtrProcessoCreateDTO LerUnica(byte[] csv)
    {
        var linha = Assert.Single(CtrCsv.Ler(csv));
        Assert.Null(linha.Erro);
        return linha.Dados!;
    }

    /// <summary>A planilha legada da equipe (12 colunas), que não conhece nada desta rodada.</summary>
    private static byte[] PlanilhaLegada(string numero) => Bytes1252(
        "Analises de contratações;;;;;;;;;;;\r\n"
        + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
        + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
        + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
        + numero + ";Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;\r\n");

    private static string[] Celulas(byte[] export, int linha) =>
        Encoding.UTF8.GetString(export, 3, export.Length - 3)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[linha]
            .Split(';');

    // ══ 1. Cadastro e edição ═════════════════════════════════════════════════

    [Fact]
    public async Task Criar_ComOsTresCampos_GravaEDevolve()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.ValorEstimado = 1234567.89m;
        dto.HospedagemCetic = CtrDominios.HospedagemCetic.Parcialmente;
        dto.UsaGdfnet = true;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(1234567.89m, resposta.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.Parcialmente, resposta.HospedagemCetic);
        Assert.True(resposta.UsaGdfnet);

        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Equal(1234567.89m, gravado.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.Parcialmente, gravado.HospedagemCetic);
        Assert.True(gravado.UsaGdfnet);
    }

    [Fact]
    public async Task Criar_SemOsTresCampos_NascemNaoInformados()
    {
        var resposta = await _processos.CriarAsync(NovoProcessoDto(Numero), await ContextoAnalistaAsync());

        Assert.Null(resposta.ValorEstimado);
        Assert.Null(resposta.HospedagemCetic);
        Assert.Null(resposta.UsaGdfnet);
    }

    [Fact]
    public async Task Atualizar_OValorEnviadoVale_ENuloLimpa()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        var dto = NovoProcessoDto(Numero);
        dto.ValorEstimado = 2500000m;
        dto.HospedagemCetic = "sim";
        dto.UsaGdfnet = true;
        var trocado = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal(2500000m, trocado.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.Sim, trocado.HospedagemCetic);
        Assert.True(trocado.UsaGdfnet);

        // O formulário manda sempre o processo inteiro: os três nulos LIMPAM, como a etapa
        // do planejamento (não é o "ausente preserva" da origem e dos critérios)
        var limpo = await _processos.AtualizarAsync(processo.Id, NovoProcessoDto(Numero), ctx);

        Assert.Null(limpo.ValorEstimado);
        Assert.Null(limpo.HospedagemCetic);
        Assert.Null(limpo.UsaGdfnet);
        var gravado = Context.CtrProcessos.Single(p => p.Id == processo.Id);
        Assert.Null(gravado.ValorEstimado);
        Assert.Null(gravado.HospedagemCetic);
        Assert.Null(gravado.UsaGdfnet);
    }

    [Fact]
    public void Contrato_OsTresCamposNosDtosENaResposta()
    {
        // JSON sai PascalCase como as propriedades (PropertyNamingPolicy = null)
        foreach (var tipo in new[] { typeof(CtrProcessoCreateDTO), typeof(CtrProcessoUpdateDTO), typeof(CtrProcessoResponse) })
        {
            Assert.Equal(typeof(decimal?), tipo.GetProperty("ValorEstimado")!.PropertyType);
            Assert.Equal(typeof(string), tipo.GetProperty("HospedagemCetic")!.PropertyType);
            Assert.Equal(typeof(bool?), tipo.GetProperty("UsaGdfnet")!.PropertyType);
        }
    }

    // ══ 2. Validação (fonte única: ValidarProcesso) ══════════════════════════

    [Theory]
    [InlineData("1234.565", "1234.57")]
    [InlineData("10.004", "10.00")]
    [InlineData("0.005", "0.01")]
    [InlineData("1500000", "1500000")]
    [InlineData("0", "0")]
    public void Validar_ValorEstimado_EhArredondadoParaCentavos(string enviado, string gravado)
    {
        var candidato = Candidato(p => p.ValorEstimado = Decimal(enviado));

        CtrProcessoService.ValidarProcesso(candidato, numeroDuplicado: false);

        Assert.Equal(Decimal(gravado), candidato.ValorEstimado);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-0.001")]   // negativo mesmo que arredondasse para zero
    [InlineData("-1500000")]
    public async Task Criar_ValorNegativo_EhRecusado(string valor)
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.ValorEstimado = Decimal(valor);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
        Assert.Contains("valor estimado da contratação não pode ser negativo", ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
    }

    [Fact]
    public void Validar_ValorEstimado_TemOLimiteDoNumeric18_2()
    {
        // O próprio limite passa, e o que arredonda para ele também
        var noLimite = Candidato(p => p.ValorEstimado = 9_999_999_999_999_999.99m);
        CtrProcessoService.ValidarProcesso(noLimite, numeroDuplicado: false);
        Assert.Equal(CtrProcessoService.ValorEstimadoMaximo, noLimite.ValorEstimado);

        var arredondaParaOLimite = Candidato(p => p.ValorEstimado = 9_999_999_999_999_999.994m);
        CtrProcessoService.ValidarProcesso(arredondaParaOLimite, numeroDuplicado: false);
        Assert.Equal(CtrProcessoService.ValorEstimadoMaximo, arredondaParaOLimite.ValorEstimado);

        // Acima dele (inclusive o que só passa depois de arredondar), recusa
        foreach (var acima in new[] { 9_999_999_999_999_999.995m, 10_000_000_000_000_000m })
        {
            var ex = Assert.Throws<ApiException>(() =>
                CtrProcessoService.ValidarProcesso(Candidato(p => p.ValorEstimado = acima), numeroDuplicado: false));

            Assert.Equal((int)ErrorCode.CtrProcessoInvalido, ex.Error.Code);
            Assert.Contains("valor estimado da contratação não pode passar de R$ 9.999.999.999.999.999,99", ex.Error.Message);
        }
    }

    [Fact]
    public async Task Atualizar_ValorNegativo_EhRecusadoSemMexerNoGravado()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();
        var dto = NovoProcessoDto(Numero);
        dto.ValorEstimado = -10m;
        dto.HospedagemCetic = CtrDominios.HospedagemCetic.Sim;
        dto.UsaGdfnet = true;

        await Assert.ThrowsAsync<ApiException>(() => _processos.AtualizarAsync(processo.Id, dto, ctx));

        // Validação no candidato solto: a entidade rastreada fica como estava
        AssertOsTresPreservados(processo.ValorEstimado, processo.HospedagemCetic, processo.UsaGdfnet);
    }

    [Theory]
    [InlineData("nao aplicavel (saas)", CtrDominios.HospedagemCetic.NaoAplicavelSaas)]
    [InlineData("NÃO APLICÁVEL (SAAS)", CtrDominios.HospedagemCetic.NaoAplicavelSaas)]
    [InlineData("  Não   aplicável (SaaS) ", CtrDominios.HospedagemCetic.NaoAplicavelSaas)]
    [InlineData("SIM", CtrDominios.HospedagemCetic.Sim)]
    [InlineData("nao", CtrDominios.HospedagemCetic.Nao)]
    [InlineData("parcialmente", CtrDominios.HospedagemCetic.Parcialmente)]
    public void Validar_Hospedagem_ViraAGrafiaDoDominio(string enviada, string gravada)
    {
        var candidato = Candidato(p => p.HospedagemCetic = enviada);

        CtrProcessoService.ValidarProcesso(candidato, numeroDuplicado: false);

        Assert.Equal(gravada, candidato.HospedagemCetic);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validar_HospedagemVazia_EhNaoInformada(string? enviada)
    {
        var candidato = Candidato(p => p.HospedagemCetic = enviada);

        CtrProcessoService.ValidarProcesso(candidato, numeroDuplicado: false);

        Assert.Null(candidato.HospedagemCetic);
    }

    [Fact]
    public async Task Criar_HospedagemNormalizada_GravaEDevolveAGrafiaDoDominio()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.HospedagemCetic = "nao aplicavel (saas)";

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal("Não aplicável (SaaS)", resposta.HospedagemCetic);
        Assert.Equal("Não aplicável (SaaS)", Context.CtrProcessos.Single().HospedagemCetic);
    }

    [Fact]
    public async Task Criar_HospedagemForaDoDominio_EhRecusadaNomeandoOCampo()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.HospedagemCetic = "Nuvem do fornecedor";

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("Hospedagem no CeTIC-DF inválida", ex.Error.Message);
        Assert.Contains("Nuvem do fornecedor", ex.Error.Message);
        Assert.Empty(Context.CtrProcessos);
    }

    // ══ 3. Checkpoint e leituras ═════════════════════════════════════════════

    [Theory]
    [InlineData(CtrDominios.Etapa.ChegadaSgdi)]
    [InlineData(CtrDominios.Etapa.ChegadaSubgd)]
    [InlineData(CtrDominios.Etapa.ChegadaUgtic)]
    [InlineData(CtrDominios.Etapa.RetornoGabSgdi)]
    [InlineData(CtrDominios.Etapa.RetornoOrgao)]
    [InlineData(CtrDominios.Etapa.AssinaturaContrato)]
    public async Task Checkpoint_EmQualquerEtapa_NaoMexeNosTresCampos(string etapa)
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = etapa, Data = DiasAtras(1) }, ctx);

        AssertOsTresPreservados(resposta.ValorEstimado, resposta.HospedagemCetic, resposta.UsaGdfnet);
        var gravado = Context.CtrProcessos.Single(p => p.Id == processo.Id);
        AssertOsTresPreservados(gravado.ValorEstimado, gravado.HospedagemCetic, gravado.UsaGdfnet);
    }

    [Fact]
    public async Task Checkpoint_NaoSeAplicaELimparAData_TambemPreservamOsTresCampos()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        var naoSeAplica = await _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.ChegadaUgtic, NaoSeAplica = true }, ctx);
        AssertOsTresPreservados(naoSeAplica.ValorEstimado, naoSeAplica.HospedagemCetic, naoSeAplica.UsaGdfnet);

        var limpo = await _processos.RegistrarCheckpointAsync(processo.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.ChegadaSgdi, Data = null }, ctx);
        AssertOsTresPreservados(limpo.ValorEstimado, limpo.HospedagemCetic, limpo.UsaGdfnet);
        AssertOsTresPreservados(processo.ValorEstimado, processo.HospedagemCetic, processo.UsaGdfnet);
    }

    [Fact]
    public async Task Leituras_TrazemOsTresCampos_NoDetalheNaListaENoPainel()
    {
        var processo = SemearComOsTres();

        var detalhe = await _processos.GetAsync(processo.Id);
        var daLista = Assert.Single((await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50 })).Items);
        var doPainel = Assert.Single((await _processos.MontarPainelAsync(0)).Gargalos);

        foreach (var resposta in new[] { detalhe, daLista, doPainel })
            AssertOsTresPreservados(resposta.ValorEstimado, resposta.HospedagemCetic, resposta.UsaGdfnet);
    }

    // ══ 4. CSV: as três colunas no fim (32 colunas) ══════════════════════════

    [Fact]
    public void Csv_Cabecalho_TerminaComOsTresDadosDaContratacao()
    {
        Assert.Equal(32, CtrCsv.Cabecalho.Length);
        Assert.Equal(new[] { "Valor estimado (R$)", "Hospedagem no CeTIC-DF", "Usa a rede GDFNet" },
            CtrCsv.Cabecalho[ColunaValor..]);

        // Os sete critérios continuam no lugar e não engolem as colunas novas
        Assert.Equal(CtrCsv.Cabecalho[22..29], CtrCsv.ColunasCriterios);

        Assert.True(CtrCsvColunasOpcionais.Todas.ValorEstimado);
        Assert.True(CtrCsvColunasOpcionais.Todas.HospedagemCetic);
        Assert.True(CtrCsvColunasOpcionais.Todas.UsaGdfnet);
    }

    [Fact]
    public void Csv_Ler_CadaColunaNovaEhDetectadaPeloCabecalho()
    {
        CtrCsvColunasOpcionais Colunas(int quantas) => Assert.Single(CtrCsv.Ler(Csv(quantas, Numero))).ColunasOpcionais;

        var anterior = Colunas(29);
        Assert.False(anterior.ValorEstimado);
        Assert.False(anterior.HospedagemCetic);
        Assert.False(anterior.UsaGdfnet);
        Assert.True(anterior.CriteriosCriticidade);

        Assert.True(Colunas(30).ValorEstimado);
        Assert.False(Colunas(30).HospedagemCetic);
        Assert.True(Colunas(31).HospedagemCetic);
        Assert.False(Colunas(31).UsaGdfnet);
        Assert.True(Colunas(32).UsaGdfnet);
    }

    private static CtrProcessoResponse Resposta(string numero, decimal? valor, string? hospedagem, bool? gdfnet) => new()
    {
        NumeroProcesso = numero,
        OrgaoNome = "Economia",
        OrgaoSigla = "SEEC",
        Objeto = "Switches",
        CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
        Origem = CtrDominios.Origem.OrgaoComunicante,
        ValorEstimado = valor,
        HospedagemCetic = hospedagem,
        UsaGdfnet = gdfnet
    };

    [Fact]
    public void Csv_Escrever_ValorEmPtBrSemMilhar_HospedagemNaGrafiaDoDominio_GdfnetSimNao()
    {
        var bytes = CtrCsv.Escrever(new List<CtrProcessoResponse>
        {
            Resposta(Numero, 1234567.89m, CtrDominios.HospedagemCetic.NaoAplicavelSaas, true),
            Resposta(OutroNumero, 1500000m, CtrDominios.HospedagemCetic.Nao, false),
            Resposta(TerceiroNumero, null, null, null)
        });

        Assert.Equal(new[] { "1234567,89", "Não aplicável (SaaS)", "Sim" }, Celulas(bytes, 1)[ColunaValor..]);
        Assert.Equal(new[] { "1500000,00", "Não", "Não" }, Celulas(bytes, 2)[ColunaValor..]);
        Assert.Equal(new[] { "", "", "" }, Celulas(bytes, 3)[ColunaValor..]);
        Assert.All(new[] { 1, 2, 3 }, linha => Assert.Equal(32, Celulas(bytes, linha).Length));
    }

    [Fact]
    public async Task Csv_RoundTrip_ExportParaImportacao_EhFiel()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearComOsTres(Numero);
        SemearProcesso(OutroNumero, p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.ValorEstimado = 1500000m;
            p.HospedagemCetic = CtrDominios.HospedagemCetic.Sim;
            p.UsaGdfnet = true;
        });
        SemearProcesso(TerceiroNumero, p => p.ChegadaSgdi = DiasAtras(30));

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());

        var previa = await NovoImportacaoService().PreviaAsync(bytes);
        Assert.Equal(3, previa.TotalLinhas);
        Assert.All(previa.Linhas, l => Assert.Equal(CtrImportacaoAcao.Atualizar, l.Acao));
        Assert.All(previa.Linhas, l => Assert.Null(l.Motivo));

        var relatorio = await NovoImportacaoService().ImportarAsync(bytes, ctx);
        Assert.Equal(3, relatorio.Atualizados);

        var primeiro = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        AssertOsTresPreservados(primeiro.ValorEstimado, primeiro.HospedagemCetic, primeiro.UsaGdfnet);

        var segundo = Context.CtrProcessos.Single(p => p.NumeroProcesso == OutroNumero);
        Assert.Equal(1500000m, segundo.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.Sim, segundo.HospedagemCetic);
        Assert.True(segundo.UsaGdfnet);

        var terceiro = Context.CtrProcessos.Single(p => p.NumeroProcesso == TerceiroNumero);
        Assert.Null(terceiro.ValorEstimado);
        Assert.Null(terceiro.HospedagemCetic);
        Assert.Null(terceiro.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_ExportDe29Colunas_PreservaOsTresCampos()
    {
        // O export de antes desta rodada (29 colunas) não sabe nada dos três: não pode apagá-los
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        var linha = Assert.Single((await NovoImportacaoService().PreviaAsync(Csv(29, Numero))).Linhas);
        Assert.Equal(CtrImportacaoAcao.Atualizar, linha.Acao);
        AssertOsTresPreservados(linha.Dados!.ValorEstimado, linha.Dados.HospedagemCetic, linha.Dados.UsaGdfnet);

        await NovoImportacaoService().ImportarAsync(Csv(29, Numero), ctx);

        AssertOsTresPreservados(processo.ValorEstimado, processo.HospedagemCetic, processo.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_PlanilhaLegadaDe12Colunas_PreservaOsTresCampos()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        var relatorio = await NovoImportacaoService().ImportarAsync(PlanilhaLegada(Numero), ctx);

        Assert.Equal(1, relatorio.Atualizados);
        AssertOsTresPreservados(processo.ValorEstimado, processo.HospedagemCetic, processo.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_CadaColunaEhIndependente()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        // 30 colunas: só a do valor existe; hospedagem e GDFNet ficam como estão
        await NovoImportacaoService().ImportarAsync(Csv(30, Numero, (ColunaValor, "2.000.000,00")), ctx);
        Assert.Equal(2000000m, processo.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.NaoAplicavelSaas, processo.HospedagemCetic);
        Assert.False(processo.UsaGdfnet);

        // 31 colunas: valor e hospedagem existem; o GDFNet segue preservado
        await NovoImportacaoService().ImportarAsync(
            Csv(31, Numero, (ColunaValor, "2.000.000,00"), (ColunaHospedagem, "Sim")), ctx);
        Assert.Equal(CtrDominios.HospedagemCetic.Sim, processo.HospedagemCetic);
        Assert.False(processo.UsaGdfnet);

        // Cabeçalho completo, mas sem o nome da coluna do valor: só ela fica ausente
        var cabecalho = CtrCsv.Cabecalho.ToArray();
        cabecalho[ColunaValor] = string.Empty;
        var csv = BytesUtf8ComBom(string.Join(";", cabecalho) + "\r\n"
            + Linha(32, Numero, (ColunaValor, "1,00"), (ColunaHospedagem, "Não"), (ColunaGdfnet, "Sim")) + "\r\n");
        await NovoImportacaoService().ImportarAsync(csv, ctx);
        Assert.Equal(2000000m, processo.ValorEstimado);
        Assert.Equal(CtrDominios.HospedagemCetic.Nao, processo.HospedagemCetic);
        Assert.True(processo.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_ColunasPresentesEVazias_LimpamOsTres()
    {
        // Coluna PRESENTE e vazia é escolha explícita de quem exportou: limpa
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComOsTres();

        await NovoImportacaoService().ImportarAsync(Csv(32, Numero), ctx);

        Assert.Null(processo.ValorEstimado);
        Assert.Null(processo.HospedagemCetic);
        Assert.Null(processo.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_ProcessoNovoSemAsColunas_NasceSemOsTres()
    {
        var ctx = await ContextoAnalistaAsync();

        var relatorio = await NovoImportacaoService().ImportarAsync(Csv(29, Numero), ctx);

        Assert.Equal(1, relatorio.Criados);
        var criado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Null(criado.ValorEstimado);
        Assert.Null(criado.HospedagemCetic);
        Assert.Null(criado.UsaGdfnet);
    }

    [Fact]
    public async Task Importar_ProcessoNovoComAsColunas_GravaOsTresNormalizados()
    {
        var ctx = await ContextoAnalistaAsync();

        var relatorio = await NovoImportacaoService().ImportarAsync(Csv(32, Numero,
            (ColunaValor, "R$ 1.234,565"), (ColunaHospedagem, "nao aplicavel (saas)"), (ColunaGdfnet, "sim")), ctx);

        Assert.Equal(1, relatorio.Criados);
        var criado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Equal(1234.57m, criado.ValorEstimado);   // arredondado pela validação, fonte única
        Assert.Equal(CtrDominios.HospedagemCetic.NaoAplicavelSaas, criado.HospedagemCetic);
        Assert.True(criado.UsaGdfnet);
    }

    [Theory]
    [InlineData("R$ 1.234.567,89", "1234567.89")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("1234567,89", "1234567.89")]
    [InlineData("1.234,5", "1234.5")]
    [InlineData("1500000", "1500000")]
    [InlineData("1500000.00", "1500000.00")]
    [InlineData("1234.5", "1234.5")]         // ponto decimal: sem vírgula e fora do desenho do milhar
    [InlineData("1.500", "1500")]            // ponto de milhar (grupo de três), não decimal
    [InlineData("R$1.500.000", "1500000")]
    [InlineData("r$ 0,5", "0.5")]
    [InlineData("1 234 567,89", "1234567.89")]
    [InlineData("R$ 1.500,00", "1500.00")] // espaço fixo que o Excel põe depois do símbolo
    [InlineData("0", "0")]
    public void Csv_Ler_Valor_AceitaOsFormatosBrasileirosEOPontoDecimal(string celula, string esperado)
    {
        var dados = LerUnica(Csv(32, Numero, (ColunaValor, celula)));

        Assert.Equal(Decimal(esperado), dados.ValorEstimado);
    }

    [Theory]
    [InlineData("abc", "valor inválido em Valor estimado (R$): abc")]
    [InlineData("R$", "valor inválido em Valor estimado (R$): R$")]
    [InlineData("1.2.3", "valor inválido em Valor estimado (R$): 1.2.3")]
    [InlineData("1,234.56", "valor inválido em Valor estimado (R$): 1,234.56")] // formato americano
    [InlineData("12,", "valor inválido em Valor estimado (R$): 12,")]
    [InlineData("1e6", "valor inválido em Valor estimado (R$): 1e6")]
    [InlineData("1,5 mil", "valor inválido em Valor estimado (R$): 1,5 mil")]
    [InlineData("-1500", "valor negativo em Valor estimado (R$): -1500")]
    [InlineData("R$ -1.500,00", "valor negativo em Valor estimado (R$): R$ -1.500,00")]
    [InlineData("-R$ 10,00", "valor negativo em Valor estimado (R$): -R$ 10,00")]
    public void Csv_Ler_ValorInvalidoOuNegativo_RejeitaNomeandoAColuna(string celula, string motivo)
    {
        var linha = Assert.Single(CtrCsv.Ler(Csv(32, Numero, (ColunaValor, celula))));

        Assert.Null(linha.Dados);
        Assert.Equal(motivo, linha.Erro);
    }

    [Fact]
    public async Task Importar_ValorAcimaDoLimite_RejeitaComAMensagemDaValidacao()
    {
        var previa = await NovoImportacaoService().PreviaAsync(
            Csv(32, Numero, (ColunaValor, "10.000.000.000.000.000,00")));

        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(CtrImportacaoAcao.Rejeitar, linha.Acao);
        Assert.Contains("valor estimado da contratação não pode passar de R$ 9.999.999.999.999.999,99", linha.Motivo);
    }

    [Fact]
    public async Task Importar_ValorComMaisDeDuasCasas_ChegaArredondadoNaPrevia()
    {
        var linha = Assert.Single((await NovoImportacaoService().PreviaAsync(
            Csv(32, Numero, (ColunaValor, "1.234,565")))).Linhas);

        Assert.Equal(CtrImportacaoAcao.Criar, linha.Acao);
        Assert.Equal(1234.57m, linha.Dados!.ValorEstimado);
    }

    [Theory]
    [InlineData("nao aplicavel (saas)", CtrDominios.HospedagemCetic.NaoAplicavelSaas)]
    [InlineData("PARCIALMENTE", CtrDominios.HospedagemCetic.Parcialmente)]
    [InlineData("não", CtrDominios.HospedagemCetic.Nao)]
    [InlineData("Sim", CtrDominios.HospedagemCetic.Sim)]
    public void Csv_Ler_Hospedagem_ViraAGrafiaDoDominio(string celula, string esperada)
    {
        Assert.Equal(esperada, LerUnica(Csv(32, Numero, (ColunaHospedagem, celula))).HospedagemCetic);
    }

    [Theory]
    [InlineData("Sim", true)]
    [InlineData("sim", true)]
    [InlineData("NÃO", false)]
    [InlineData("nao", false)]
    public void Csv_Ler_Gdfnet_LeSimNaoComoARestituicao(string celula, bool esperado)
    {
        Assert.Equal(esperado, LerUnica(Csv(32, Numero, (ColunaGdfnet, celula))).UsaGdfnet);
    }

    [Theory]
    [InlineData(ColunaHospedagem, "Talvez", "valor inválido em Hospedagem no CeTIC-DF: Talvez")]
    [InlineData(ColunaHospedagem, "SaaS", "valor inválido em Hospedagem no CeTIC-DF: SaaS")]
    [InlineData(ColunaGdfnet, "Talvez", "valor inválido em Usa a rede GDFNet: Talvez")]
    [InlineData(ColunaGdfnet, "S", "valor inválido em Usa a rede GDFNet: S")]
    public void Csv_Ler_HospedagemOuGdfnetForaDoDominio_RejeitaNomeandoAColuna(int coluna, string celula, string motivo)
    {
        var linha = Assert.Single(CtrCsv.Ler(Csv(32, Numero, (coluna, celula))));

        Assert.Null(linha.Dados);
        Assert.Equal(motivo, linha.Erro);
    }

    // ══ 5. Critério IV: "Não foi possível avaliar com as informações apresentadas" ══

    [Fact]
    public void CriterioIV_TemANovaRespostaPorUltimo_EIIeVIContinuamComQuatro()
    {
        Assert.Equal("Não foi possível avaliar com as informações apresentadas", NaoAvaliou);
        Assert.Equal(new[]
            {
                CtrDominios.CriterioCriticidade.Nenhum, CtrDominios.CriterioCriticidade.Baixo,
                CtrDominios.CriterioCriticidade.Medio, CtrDominios.CriterioCriticidade.Alto, NaoAvaliou
            },
            CtrDominios.CriterioCriticidade.RespostasDe(CtrDominios.CriterioCriticidade.ImpactoArquitetura));

        var grau = new[]
        {
            CtrDominios.CriterioCriticidade.Nenhum, CtrDominios.CriterioCriticidade.Baixo,
            CtrDominios.CriterioCriticidade.Medio, CtrDominios.CriterioCriticidade.Alto
        };
        Assert.Equal(grau, CtrDominios.CriterioCriticidade.RespostasDe(CtrDominios.CriterioCriticidade.ImpactoServicos));
        Assert.Equal(grau, CtrDominios.CriterioCriticidade.RespostasDe(CtrDominios.CriterioCriticidade.RiscosSeguranca));

        // O padrão do IV continua "Nenhum"
        Assert.Equal(CtrDominios.CriterioCriticidade.Nenhum,
            CtrDominios.CriterioCriticidade.RespostaPadrao(CtrDominios.CriterioCriticidade.ImpactoArquitetura));
    }

    [Fact]
    public void CriterioIV_NaoFoiPossivelAvaliar_ValeZeroPonto()
    {
        Assert.Equal(0, CtrCriticidade.Pontos(CtrDominios.CriterioCriticidade.ImpactoArquitetura, NaoAvaliou));

        var respostas = CtrCriticidade.Normalizar(new Dictionary<string, string> { ["IV"] = NaoAvaliou });
        Assert.Equal(0, CtrCriticidade.PontosTotais(respostas));
        Assert.Equal(CtrDominios.Criticidade.Baixa, CtrCriticidade.Calcular(respostas));
    }

    [Theory]
    [InlineData("Não foi possível avaliar com as informações apresentadas")]
    [InlineData("nao foi possivel avaliar com as informacoes apresentadas")]
    [InlineData("NÃO FOI POSSÍVEL AVALIAR COM AS INFORMAÇÕES APRESENTADAS")]
    [InlineData("Não foi possível avaliar")]
    [InlineData("nao foi possivel avaliar")]
    [InlineData("  NAO FOI   POSSIVEL AVALIAR ")]
    public void CriterioIV_Normalizar_AceitaAFraseEOAtalho_EGravaSempreAFraseCompleta(string enviada)
    {
        var respostas = CtrCriticidade.Normalizar(new Dictionary<string, string> { ["iv"] = enviada });

        Assert.Equal(NaoAvaliou, respostas[CtrDominios.CriterioCriticidade.ImpactoArquitetura]);
        // O jsonb guarda a frase legível (sem os acentos escapados)
        Assert.Contains("\"IV\":\"" + NaoAvaliou + "\"", CtrCriticidade.Serializar(respostas));
    }

    [Theory]
    [InlineData("II", NaoAvaliou)]
    [InlineData("VI", NaoAvaliou)]
    [InlineData("II", CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliarAtalho)]
    [InlineData("VI", CtrDominios.CriterioCriticidade.NaoFoiPossivelAvaliarAtalho)]
    public void CriteriosIIeVI_RecusamANovaResposta(string codigo, string resposta)
    {
        var ex = Assert.Throws<ApiException>(() =>
            CtrCriticidade.Normalizar(new Dictionary<string, string> { [codigo] = resposta }));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains($"Resposta inválida no critério {codigo}", ex.Error.Message);
    }

    [Theory]
    // 3 (II Alto) + 0 + 3 (V Sim) = 6, Alta; com o IV em "Alto" seriam 9
    [InlineData("II=Alto,IV=" + NaoAvaliou + ",V=Sim", CtrDominios.Criticidade.Alta, 6)]
    // 2 (II Médio) + 1 (III Sim) + 0 = 3, Média
    [InlineData("II=Médio,III=Sim,IV=" + NaoAvaliou, CtrDominios.Criticidade.Media, 3)]
    // 0 + 2 (VI Médio) = 2, Baixa; se contasse como "Alto" seriam 5 (Média)
    [InlineData("IV=" + NaoAvaliou + ",VI=Médio", CtrDominios.Criticidade.Baixa, 2)]
    // O alinhamento à EGD/DF continua descontando: 3 + 0 - 1 = 2, Baixa
    [InlineData("I=Sim,II=Alto,IV=" + NaoAvaliou, CtrDominios.Criticidade.Baixa, 2)]
    public void CriterioIV_NaoAvaliou_SomaExatamenteComoNenhum(string texto, string criticidade, int pontos)
    {
        var comNaoAvaliou = CtrCriticidade.Normalizar(Respostas(texto));
        var comNenhum = CtrCriticidade.Normalizar(
            Respostas(texto.Replace(NaoAvaliou, CtrDominios.CriterioCriticidade.Nenhum)));

        Assert.Equal(pontos, CtrCriticidade.PontosTotais(comNaoAvaliou));
        Assert.Equal(criticidade, CtrCriticidade.Calcular(comNaoAvaliou));
        Assert.Equal(CtrCriticidade.PontosTotais(comNenhum), CtrCriticidade.PontosTotais(comNaoAvaliou));
        Assert.Equal(CtrCriticidade.Calcular(comNenhum), CtrCriticidade.Calcular(comNaoAvaliou));
    }

    [Fact]
    public async Task CriterioIV_NoCadastro_OAtalhoGravaAFraseCompletaEOsPontosSaemNaResposta()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.CriteriosCriticidade = new Dictionary<string, string>
        {
            ["II"] = "Alto",
            ["IV"] = "nao foi possivel avaliar",
            ["V"] = "Sim"
        };

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(NaoAvaliou, resposta.CriteriosCriticidade![CtrDominios.CriterioCriticidade.ImpactoArquitetura]);
        Assert.Equal(6, resposta.PontosCriticidade);
        Assert.Equal(CtrDominios.Criticidade.Alta, resposta.Criticidade);

        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Contains("\"IV\":\"" + NaoAvaliou + "\"", gravado.CriteriosCriticidade);

        // Trocar o IV por "Alto" na edição soma os 3 pontos que a resposta nova não soma
        dto.CriteriosCriticidade["IV"] = CtrDominios.CriterioCriticidade.Alto;
        var editado = await _processos.AtualizarAsync(gravado.Id, dto, ctx);
        Assert.Equal(9, editado.PontosCriticidade);
    }

    [Fact]
    public void CriterioIV_Csv_OAtalhoNaColunaDoIV_ViraAFraseCompleta()
    {
        var dados = LerUnica(Csv(32, Numero, (ColunaCriterioIV, "Não foi possível avaliar")));

        Assert.Equal(NaoAvaliou, dados.CriteriosCriticidade![CtrDominios.CriterioCriticidade.ImpactoArquitetura]);
    }

    [Theory]
    [InlineData(ColunaCriterioII)]
    [InlineData(ColunaCriterioVI)]
    public void CriterioIV_Csv_ARespostaNovaEmOutroCriterio_RejeitaNomeandoAColuna(int coluna)
    {
        var linha = Assert.Single(CtrCsv.Ler(Csv(32, Numero, (coluna, NaoAvaliou))));

        Assert.Null(linha.Dados);
        Assert.Equal($"resposta inválida em {CtrCsv.Cabecalho[coluna]}: {NaoAvaliou}", linha.Erro);
    }

    [Fact]
    public async Task CriterioIV_Csv_RoundTripDaFraseCompleta_EhFiel()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.CriteriosCriticidade = CtrCriticidade.Serializar(
                CtrCriticidade.Normalizar(Respostas("II=Alto,IV=" + NaoAvaliou + ",V=Sim")));
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        Assert.Equal(NaoAvaliou, Celulas(bytes, 1)[ColunaCriterioIV]);

        var linha = Assert.Single((await NovoImportacaoService().PreviaAsync(bytes)).Linhas);
        Assert.Equal(CtrImportacaoAcao.Atualizar, linha.Acao);
        Assert.Null(linha.Motivo);   // a criticidade exportada confere com os critérios
        Assert.Equal(NaoAvaliou, linha.Dados!.CriteriosCriticidade![CtrDominios.CriterioCriticidade.ImpactoArquitetura]);

        await NovoImportacaoService().ImportarAsync(bytes, ctx);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Contains("\"IV\":\"" + NaoAvaliou + "\"", gravado.CriteriosCriticidade);
        Assert.Equal(CtrDominios.Criticidade.Alta, gravado.Criticidade);
    }

    // ══ 6. Migration: três colunas e um CHECK, só em ctr_processo ════════════

    /// <summary>
    /// Operações da migration lidas por reflexão, como em CtrMigrationSupervisaoTest (o
    /// provider InMemory não executa migration e o projeto de teste não referencia o EF
    /// Relational, que seria dependência nova).
    /// </summary>
    private static List<object> OperacoesDaMigration(string lado)
    {
        var tipo = typeof(CtrProcesso).Assembly.GetType("demanda_service.Migrations.CtrValorHospedagemGdfnet");
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(operacao);

    private static string TipoDe(object operacao) => operacao.GetType().Name;

    [Fact]
    public void Migration_SoAcrescentaAsTresColunasEOCheckEmCtrProcesso()
    {
        var operacoes = OperacoesDaMigration("UpOperations");

        // Só ctr_processo, sem SQL cru e sem nada removido ou alterado
        Assert.All(operacoes, o => Assert.Equal("ctr_processo", Valor(o, "Table")));
        Assert.Equal(
            new[] { "AddColumnOperation", "AddColumnOperation", "AddColumnOperation", "AddCheckConstraintOperation" },
            operacoes.Select(TipoDe));

        var colunas = operacoes.Where(o => TipoDe(o) == "AddColumnOperation")
            .ToDictionary(o => (string)Valor(o, "Name")!);
        Assert.Equal(new[] { "hospedagem_cetic", "usa_gdfnet", "valor_estimado" },
            colunas.Keys.OrderBy(n => n, StringComparer.Ordinal));
        // Nullable: os processos existentes ficam com os três "não informados", sem backfill
        Assert.All(colunas.Values, c => Assert.True((bool)Valor(c, "IsNullable")!));
        Assert.Equal("character varying(30)", Valor(colunas["hospedagem_cetic"], "ColumnType"));
        Assert.Equal("boolean", Valor(colunas["usa_gdfnet"], "ColumnType"));
        Assert.Equal("numeric(18,2)", Valor(colunas["valor_estimado"], "ColumnType"));

        // O CHECK reproduz o domínio
        var check = Assert.Single(operacoes, o => TipoDe(o) == "AddCheckConstraintOperation");
        Assert.Equal("ck_ctr_processo_hospedagem_cetic", Valor(check, "Name"));
        Assert.Equal(
            "hospedagem_cetic IS NULL OR hospedagem_cetic IN ("
            + string.Join(",", CtrDominios.HospedagemCetic.Todos.Select(h => $"'{h}'")) + ")",
            Valor(check, "Sql"));
        Assert.Equal(
            "hospedagem_cetic IS NULL OR hospedagem_cetic IN ('Sim','Não','Parcialmente','Não aplicável (SaaS)')",
            Valor(check, "Sql"));
    }

    [Fact]
    public void Migration_DownDesfazSoOQueOUpFez()
    {
        var operacoes = OperacoesDaMigration("DownOperations");

        Assert.Equal(4, operacoes.Count);
        Assert.All(operacoes, o => Assert.Equal("ctr_processo", Valor(o, "Table")));
        Assert.Equal("ck_ctr_processo_hospedagem_cetic",
            Valor(Assert.Single(operacoes, o => TipoDe(o) == "DropCheckConstraintOperation"), "Name"));
        Assert.Equal(new[] { "hospedagem_cetic", "usa_gdfnet", "valor_estimado" },
            operacoes.Where(o => TipoDe(o) == "DropColumnOperation")
                .Select(o => (string)Valor(o, "Name")!)
                .OrderBy(n => n, StringComparer.Ordinal));
    }
}
