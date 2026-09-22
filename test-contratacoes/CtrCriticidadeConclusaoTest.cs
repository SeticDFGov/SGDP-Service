using System.Collections;
using System.Reflection;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Rodada de 2026-09-22: a assinatura do contrato vira a data FINAL do trâmite (processo
/// Concluído, fora da lista e na relação do painel), a criticidade passa a ser DERIVADA
/// das respostas aos critérios do art. 11, § 3º, da IN (regra em <see cref="CtrCriticidade"/>),
/// a lista sai ordenada por criticidade e o CSV ganha as sete colunas dos critérios.
/// </summary>
public class CtrCriticidadeConclusaoTest : CtrTestBase
{
    private readonly CtrProcessoService _processos;

    public CtrCriticidadeConclusaoTest()
    {
        _processos = NovoProcessoService();
    }

    /// <summary>"I=Sim,II=Alto" vira o dicionário de respostas (vazio = todas no padrão).</summary>
    private static Dictionary<string, string> Respostas(string texto) =>
        texto.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(par => par.Split('='))
            .ToDictionary(par => par[0].Trim(), par => par[1].Trim());

    // ══ 1. A regra (fonte única: CtrCriticidade) ═════════════════════════════

    [Theory]
    [InlineData("", CtrDominios.Criticidade.Baixa, 0)]
    [InlineData("III=Sim", CtrDominios.Criticidade.Baixa, 1)]
    [InlineData("V=Sim", CtrDominios.Criticidade.Media, 3)]
    [InlineData("II=Alto", CtrDominios.Criticidade.Media, 3)]
    [InlineData("II=Baixo,IV=Médio", CtrDominios.Criticidade.Media, 3)]
    [InlineData("V=Sim,VI=Médio", CtrDominios.Criticidade.Media, 5)]
    [InlineData("II=Alto,IV=Alto", CtrDominios.Criticidade.Alta, 6)]
    [InlineData("V=Sim,VI=Médio,III=Sim", CtrDominios.Criticidade.Alta, 6)]
    [InlineData("VII=Sim", CtrDominios.Criticidade.Alta, 0)]
    [InlineData("II=Alto,III=Sim,IV=Alto,V=Sim,VI=Alto", CtrDominios.Criticidade.Alta, 13)]
    // Alinhado à EGD/DF é bom: tira 1 ponto e pode baixar a categoria; sozinho não fica negativo
    [InlineData("I=Sim", CtrDominios.Criticidade.Baixa, 0)]
    [InlineData("I=Sim,II=Alto", CtrDominios.Criticidade.Baixa, 2)]
    [InlineData("I=Sim,II=Alto,IV=Alto", CtrDominios.Criticidade.Media, 5)]
    [InlineData("I=Sim,VII=Sim", CtrDominios.Criticidade.Alta, 0)]
    public void Calcular_PontuaCadaCriterioEOValorEstimadoDecideSozinho(string texto, string esperada, int pontos)
    {
        var respostas = CtrCriticidade.Normalizar(Respostas(texto));

        Assert.Equal(esperada, CtrCriticidade.Calcular(respostas));
        Assert.Equal(pontos, CtrCriticidade.PontosTotais(respostas));
    }

    [Fact]
    public void Normalizar_AceitaCaixaEAcentoDiferentesEPreencheOPadrao()
    {
        var respostas = CtrCriticidade.Normalizar(new Dictionary<string, string>
        {
            ["vii"] = "sim",
            ["ii"] = "medio",
            ["I"] = "NAO",
            ["VI"] = ""    // vazia = não respondida
        });

        Assert.Equal(CtrDominios.CriterioCriticidade.Todos.OrderBy(c => c), respostas.Keys.OrderBy(c => c));
        Assert.Equal(CtrDominios.CriterioCriticidade.Sim, respostas[CtrDominios.CriterioCriticidade.ValorEstimado]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Medio, respostas[CtrDominios.CriterioCriticidade.ImpactoServicos]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao, respostas[CtrDominios.CriterioCriticidade.AlinhamentoEgd]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nenhum, respostas[CtrDominios.CriterioCriticidade.RiscosSeguranca]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao, respostas[CtrDominios.CriterioCriticidade.Compartilhamento]);
    }

    [Theory]
    [InlineData("VIII=Sim", "Critério de criticidade desconhecido")]
    [InlineData("II=Talvez", "Resposta inválida no critério II")]
    [InlineData("I=Alto", "Resposta inválida no critério I")]   // graduação num critério Sim/Não
    public void Normalizar_RecusaCodigoOuRespostaForaDoDominio(string texto, string mensagem)
    {
        var ex = Assert.Throws<ApiException>(() => CtrCriticidade.Normalizar(Respostas(texto)));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains(mensagem, ex.Error.Message);
    }

    [Fact]
    public void Serializar_EhCanonicoEReversivel()
    {
        var respostas = CtrCriticidade.Normalizar(Respostas("VI=Alto,I=Sim"));

        var json = CtrCriticidade.Serializar(respostas);

        // Na ordem dos incisos e com os acentos legíveis (não escapados)
        Assert.StartsWith("{\"I\":\"Sim\",\"II\":\"Nenhum\"", json);
        Assert.Contains("\"VI\":\"Alto\"", json);
        Assert.Contains("Não", json);
        Assert.Equal(respostas, CtrCriticidade.Desserializar(json));
        Assert.Null(CtrCriticidade.Desserializar(null));
        Assert.Null(CtrCriticidade.Desserializar(""));
    }

    // ══ 2. Criticidade derivada no cadastro e na edição ══════════════════════

    [Fact]
    public async Task Criar_ComRespostas_DerivaACriticidadeEIgnoraOValorDoCorpo()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000501/2026-11");
        dto.Criticidade = CtrDominios.Criticidade.Baixa; // ignorado: há respostas
        dto.CriteriosCriticidade = Respostas("V=Sim,VI=Alto");

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Criticidade.Alta, resposta.Criticidade);
        Assert.Equal(6, resposta.PontosCriticidade);
        Assert.NotNull(resposta.CriteriosCriticidade);
        Assert.Equal(7, resposta.CriteriosCriticidade!.Count);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao, resposta.CriteriosCriticidade["I"]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Sim, resposta.CriteriosCriticidade["V"]);

        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000501/2026-11");
        Assert.Equal(CtrDominios.Criticidade.Alta, gravado.Criticidade);
        Assert.Contains("\"V\":\"Sim\"", gravado.CriteriosCriticidade);
    }

    [Fact]
    public async Task Criar_SemRespostas_MantemACriticidadeDoCorpo()
    {
        // Compatibilidade: planilha antiga e clientes que ainda mandam só o valor
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000502/2026-12");
        dto.Criticidade = CtrDominios.Criticidade.Media;

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.Criticidade.Media, resposta.Criticidade);
        Assert.Null(resposta.CriteriosCriticidade);
        Assert.Null(resposta.PontosCriticidade);
    }

    [Fact]
    public async Task Criar_ComRespostaForaDoDominio_EhRecusado()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto("04044-00000503/2026-13");
        dto.CriteriosCriticidade = Respostas("IV=Enorme");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _processos.CriarAsync(dto, ctx));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains("critério IV", ex.Error.Message);
        Assert.DoesNotContain(Context.CtrProcessos, p => p.NumeroProcesso == "04044-00000503/2026-13");
    }

    [Fact]
    public async Task Atualizar_SemRespostasNoCorpo_PreservaAsGravadasERecalculaDelas()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000504/2026-14", p =>
        {
            p.CriteriosCriticidade = CtrCriticidade.Serializar(CtrCriticidade.Normalizar(Respostas("II=Alto,IV=Alto")));
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });

        var dto = NovoProcessoDto("04044-00000504/2026-14");
        dto.Criticidade = CtrDominios.Criticidade.Baixa; // sem respostas no corpo: as gravadas mandam
        dto.CriteriosCriticidade = null;

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal(CtrDominios.Criticidade.Alta, resposta.Criticidade);
        Assert.Equal(6, resposta.PontosCriticidade);
        Assert.Equal(CtrDominios.CriterioCriticidade.Alto, resposta.CriteriosCriticidade!["II"]);
    }

    [Fact]
    public async Task Atualizar_ProcessoAnteriorARegra_GuardaACriticidadeAntigaAteAvaliarOsCriterios()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000505/2026-15", p =>
        {
            p.Criticidade = CtrDominios.Criticidade.Alta;
            p.CriteriosCriticidade = null;
        });

        // Editar outra coisa sem mexer nos critérios: a criticidade antiga fica
        var dto = NovoProcessoDto("04044-00000505/2026-15");
        dto.Criticidade = CtrDominios.Criticidade.Alta;
        dto.Observacao = "Só a observação mudou";
        var intacto = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        Assert.Equal(CtrDominios.Criticidade.Alta, intacto.Criticidade);
        Assert.Null(intacto.CriteriosCriticidade);

        // Sem respostas, o corpo ainda pode trocar (ou tirar) a criticidade, como antes
        dto.Criticidade = CtrDominios.Criticidade.Media;
        Assert.Equal(CtrDominios.Criticidade.Media, (await _processos.AtualizarAsync(processo.Id, dto, ctx)).Criticidade);

        // Avaliados os critérios, a regra automática assume
        dto.CriteriosCriticidade = Respostas("");
        var avaliado = await _processos.AtualizarAsync(processo.Id, dto, ctx);
        Assert.Equal(CtrDominios.Criticidade.Baixa, avaliado.Criticidade);
        Assert.Equal(0, avaliado.PontosCriticidade);
    }

    // ══ 3. Ordem da lista ════════════════════════════════════════════════════

    [Fact]
    public async Task Listar_OrdemPadraoEhCriticidadeDaMaisAltaEDepoisChegadaMaisRecente()
    {
        SemearProcesso("04044-00000510/2026-11", p => { p.Criticidade = CtrDominios.Criticidade.Baixa; p.ChegadaSgdi = DiasAtras(10); });
        SemearProcesso("04044-00000511/2026-12", p => { p.Criticidade = CtrDominios.Criticidade.Alta; p.ChegadaSgdi = DiasAtras(40); });
        SemearProcesso("04044-00000512/2026-13", p => { p.Criticidade = null; p.ChegadaSgdi = DiasAtras(5); });
        SemearProcesso("04044-00000513/2026-14", p => { p.Criticidade = CtrDominios.Criticidade.Media; p.ChegadaSgdi = DiasAtras(20); });
        SemearProcesso("04044-00000514/2026-15", p => { p.Criticidade = CtrDominios.Criticidade.Alta; p.ChegadaSgdi = DiasAtras(30); });

        var padrao = await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });
        Assert.Equal(new[]
        {
            "04044-00000514/2026-15", // Alta, chegada mais recente
            "04044-00000511/2026-12", // Alta
            "04044-00000513/2026-14", // Média
            "04044-00000510/2026-11", // Baixa
            "04044-00000512/2026-13"  // sem criticidade, por último
        }, padrao.Items.Select(p => p.NumeroProcesso).ToArray());

        var invertida = await _processos.ListarAsync(new CtrProcessoFiltro
        { OrderBy = "Criticidade", OrderDirection = "desc", PageSize = 50 });
        Assert.Equal("04044-00000512/2026-13", invertida.Items[0].NumeroProcesso);
        Assert.Equal("04044-00000511/2026-12", invertida.Items[^1].NumeroProcesso);

        // Ordenar pela chegada continua possível, e aí a criticidade não entra
        var porChegada = await _processos.ListarAsync(new CtrProcessoFiltro
        { OrderBy = "ChegadaSgdi", OrderDirection = "desc", PageSize = 50 });
        Assert.Equal("04044-00000512/2026-13", porChegada.Items[0].NumeroProcesso);
    }

    // ══ 4. Concluído: fora da lista, dentro quando pedido ════════════════════

    [Fact]
    public async Task Listar_ConcluidoFicaForaPorPadraoEEntraQuandoPedidoOuFiltrado()
    {
        SemearProcesso("04044-00000520/2026-11", p => p.ChegadaSgdi = DiasAtras(10));
        SemearProcesso("04044-00000521/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(20);
            p.DataAssinaturaContrato = DiasAtras(2);
        });
        // Restituído E assinado: a assinatura vence, é Concluído
        SemearProcesso("04044-00000522/2026-13", p =>
        {
            p.ChegadaSgdi = DiasAtras(30);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(10);
            p.RestituidoMotivo = "Faltou documento";
            p.DataAssinaturaContrato = DiasAtras(1);
        });

        var padrao = await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });
        Assert.Equal("04044-00000520/2026-11", Assert.Single(padrao.Items).NumeroProcesso);

        var comConcluidos = await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50, IncluirConcluidos = true });
        Assert.Equal(3, comConcluidos.TotalItems);

        var soConcluidos = await _processos.ListarAsync(new CtrProcessoFiltro
        { Situacao = CtrDominios.Situacao.Concluido, PageSize = 50 });
        Assert.Equal(2, soConcluidos.TotalItems);
        Assert.All(soConcluidos.Items, p => Assert.Equal(CtrDominios.Situacao.Concluido, p.Situacao));

        var restituidos = await _processos.ListarAsync(new CtrProcessoFiltro
        { Situacao = CtrDominios.Situacao.Restituido, PageSize = 50 });
        Assert.Equal(0, restituidos.TotalItems);

        var emAnalise = await _processos.ListarAsync(new CtrProcessoFiltro
        { Situacao = CtrDominios.Situacao.EmAnaliseSgdi, PageSize = 50 });
        Assert.Equal("04044-00000520/2026-11", Assert.Single(emAnalise.Items).NumeroProcesso);

        // O export segue a mesma regra da lista
        var export = CtrCsv.Ler(await _processos.ExportarCsvAsync(new CtrProcessoFiltro()));
        Assert.Equal("04044-00000520/2026-11", Assert.Single(export).NumeroProcesso);
        var exportCompleto = CtrCsv.Ler(await _processos.ExportarCsvAsync(new CtrProcessoFiltro { IncluirConcluidos = true }));
        Assert.Equal(3, exportCompleto.Count);
    }

    [Fact]
    public async Task Checkpoint_AssinaturaDoContrato_ConcluiELimparReabre()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000530/2026-11", p => p.ChegadaSgdi = DiasAtras(10));

        var concluido = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.AssinaturaContrato,
            Data = DiasAtras(1)
        }, ctx);
        Assert.Equal(DiasAtras(1), concluido.DataAssinaturaContrato);
        Assert.Equal(CtrDominios.Situacao.Concluido, concluido.Situacao);

        var reaberto = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.AssinaturaContrato,
            Data = null
        }, ctx);
        Assert.Null(reaberto.DataAssinaturaContrato);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseSgdi, reaberto.Situacao);
    }

    [Fact]
    public async Task Checkpoint_AssinaturaDoContrato_RecusaNaoSeAplicaEDataFutura()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00000531/2026-12", p => p.ChegadaSgdi = DiasAtras(10));

        var naoSeAplica = await Assert.ThrowsAsync<ApiException>(() => _processos.RegistrarCheckpointAsync(
            processo.Id, new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.AssinaturaContrato, NaoSeAplica = true }, ctx));
        Assert.Equal((int)ErrorCode.CtrProcessoInvalido, naoSeAplica.Error.Code);

        var futura = await Assert.ThrowsAsync<ApiException>(() => _processos.RegistrarCheckpointAsync(
            processo.Id, new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.AssinaturaContrato, Data = Hoje.AddDays(1) }, ctx));
        Assert.Equal((int)ErrorCode.CtrDatasIncoerentes, futura.Error.Code);

        // Nada foi gravado
        Assert.Null(Context.CtrProcessos.Single(p => p.Id == processo.Id).DataAssinaturaContrato);
    }

    // ══ 5. CSV: as sete colunas dos critérios ════════════════════════════════

    [Fact]
    public void Csv_Cabecalho_TerminaComAsSeteColunasDosCriterios()
    {
        Assert.Equal(29, CtrCsv.Cabecalho.Length);
        Assert.Equal(7, CtrCsv.ColunasCriterios.Length);
        Assert.Equal(CtrCsv.ColunasCriterios, CtrCsv.Cabecalho[^7..]);
        Assert.StartsWith("Critério I - ", CtrCsv.ColunasCriterios[0]);
        Assert.StartsWith("Critério VII - ", CtrCsv.ColunasCriterios[6]);
    }

    /// <summary>Uma linha com as 29 colunas do nosso export, das quais só as informadas são preenchidas.</summary>
    private static string Linha29(string numero, string criticidade, params (int Coluna, string Valor)[] celulas)
    {
        var linha = new string[CtrCsv.Cabecalho.Length];
        Array.Fill(linha, string.Empty);
        linha[0] = numero;
        linha[1] = "Economia";
        linha[2] = "SEEC";
        linha[4] = "Switches";
        linha[5] = CtrDominios.CategoriaObjeto.InfraestruturaRede;
        linha[6] = "22/05/2026";
        linha[12] = "Não";
        linha[17] = criticidade;
        foreach (var (coluna, valor) in celulas) linha[coluna] = valor;
        return string.Join(";", linha);
    }

    [Fact]
    public void Csv_Ler_RespostasPreenchidas_DerivamACriticidadeEAsAusentesRecebemOPadrao()
    {
        // Colunas 22..28 = critérios I..VII; V=Sim (3) e II=alto (3) dão Alta
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + Linha29("04044-00000540/2026-11", "", (26, "Sim"), (23, "alto")) + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Erro);
        var dados = linha.Dados!;
        Assert.NotNull(dados.CriteriosCriticidade);
        Assert.Equal(CtrDominios.CriterioCriticidade.Sim, dados.CriteriosCriticidade![CtrDominios.CriterioCriticidade.TecnologiasEmergentes]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Alto, dados.CriteriosCriticidade[CtrDominios.CriterioCriticidade.ImpactoServicos]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao, dados.CriteriosCriticidade[CtrDominios.CriterioCriticidade.AlinhamentoEgd]);
        Assert.Equal(CtrDominios.CriterioCriticidade.Nenhum, dados.CriteriosCriticidade[CtrDominios.CriterioCriticidade.RiscosSeguranca]);

        // A criticidade só é derivada na validação do service (o parser guarda as respostas)
        var candidato = new CtrProcesso();
        CtrProcessoService.AplicarDto(candidato, dados);
        CtrProcessoService.ValidarProcesso(candidato, numeroDuplicado: false);
        Assert.Equal(CtrDominios.Criticidade.Alta, candidato.Criticidade);
    }

    [Fact]
    public void Csv_Ler_CriticidadeQueNaoConfereComOsCriterios_RejeitaALinha()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + Linha29("04044-00000541/2026-12", "Baixa", (26, "Sim"), (23, "Alto")) + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Dados);
        Assert.Contains("não confere com os critérios", linha.Erro);
        Assert.Contains("seria Alta", linha.Erro);
    }

    [Fact]
    public void Csv_Ler_CriticidadeQueConfereComOsCriterios_EhAceita()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + Linha29("04044-00000542/2026-13", "Alta", (28, "Sim")) + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Erro);
        Assert.Equal(CtrDominios.Criticidade.Alta, linha.Dados!.Criticidade);
        Assert.Equal(CtrDominios.CriterioCriticidade.Sim, linha.Dados.CriteriosCriticidade![CtrDominios.CriterioCriticidade.ValorEstimado]);
    }

    [Fact]
    public void Csv_Ler_RespostaForaDoDominio_RejeitaALinhaNomeandoAColuna()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + Linha29("04044-00000543/2026-14", "", (22, "Talvez")) + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Dados);
        Assert.Contains("resposta inválida em Critério I", linha.Erro);
        Assert.Contains("Talvez", linha.Erro);
    }

    [Fact]
    public void Csv_Ler_BlocoTodoVazio_DeixaOsCriteriosNulosEACriticidadeDaColuna()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + Linha29("04044-00000544/2026-15", "Média") + "\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Erro);
        Assert.Null(linha.Dados!.CriteriosCriticidade);
        Assert.Equal(CtrDominios.Criticidade.Media, linha.Dados.Criticidade);
        Assert.True(linha.ColunasOpcionais.CriteriosCriticidade);
    }

    [Fact]
    public async Task Csv_RoundTrip_DasRespostas_EhFiel()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00000550/2026-11", p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.CriteriosCriticidade = CtrCriticidade.Serializar(CtrCriticidade.Normalizar(Respostas("III=Sim,IV=Médio,VI=Alto")));
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var celulas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1].Split(';');
        Assert.Equal(29, celulas.Length);
        Assert.Equal("Alta", celulas[17]);
        Assert.Equal(new[] { "Não", "Nenhum", "Sim", "Médio", "Não", "Alto", "Não" }, celulas[22..]);

        var previa = await NovoImportacaoService().PreviaAsync(bytes);
        var linha = Assert.Single(previa.Linhas);
        Assert.Equal(CtrImportacaoAcao.Atualizar, linha.Acao);
        Assert.Null(linha.Motivo);
        Assert.Equal(CtrDominios.CriterioCriticidade.Medio, linha.Dados!.CriteriosCriticidade![CtrDominios.CriterioCriticidade.ImpactoArquitetura]);

        await NovoImportacaoService().ImportarAsync(bytes, ctx);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000550/2026-11");
        Assert.Equal(CtrDominios.Criticidade.Alta, gravado.Criticidade);
        Assert.Contains("\"IV\":\"Médio\"", gravado.CriteriosCriticidade);
    }

    [Fact]
    public async Task Importar_PlanilhaLegadaSemAsColunas_PreservaAsRespostasEACriticidade()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearProcesso("04044-00000551/2026-12", p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            p.CriteriosCriticidade = CtrCriticidade.Serializar(CtrCriticidade.Normalizar(Respostas("VII=Sim")));
            p.Criticidade = CtrDominios.Criticidade.Alta;
        });

        var legada = "Analises de contratações;;;;;;;;;;;\r\n"
            + "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;"
            + "Chegada da analise - SGDI;Chegada da analise- SUBGD;Chegada da analise- UGTIC;"
            + "Data de Retorno ao Gab SGDI;Data de Retorno ao Órgão Comunicante;Observação\r\n"
            + "04044-00000551/2026-12;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;\r\n";

        var relatorio = await NovoImportacaoService().ImportarAsync(Bytes1252(legada), ctx);

        Assert.Equal(1, relatorio.Atualizados);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == "04044-00000551/2026-12");
        Assert.Equal(CtrDominios.Criticidade.Alta, gravado.Criticidade);
        Assert.Contains("\"VII\":\"Sim\"", gravado.CriteriosCriticidade);
    }

    // ══ 6. Migration: só a coluna nova, só em ctr_processo ═══════════════════

    [Fact]
    public void Migration_SoAcrescentaAColunaDosCriteriosEmCtrProcesso()
    {
        // Leitura por reflexão, como em CtrMigrationRodadaChefiaTest (o projeto de teste não
        // referencia EntityFrameworkCore.Relational)
        var tipo = typeof(CtrProcesso).Assembly
            .GetType("demanda_service.Migrations.CtrCriticidadeCriteriosAssinaturaConclui");
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        var operacoes = ((IEnumerable)tipo.GetProperty("UpOperations")!.GetValue(migration)!).Cast<object>().ToList();

        var operacao = Assert.Single(operacoes);
        Assert.Equal("AddColumnOperation", operacao.GetType().Name);
        Assert.Equal("ctr_processo", Texto(operacao, "Table"));
        Assert.Equal("criticidade_criterios", Texto(operacao, "Name"));
        Assert.Equal("jsonb", Texto(operacao, "ColumnType"));
        Assert.True((bool)operacao.GetType().GetProperty("IsNullable")!.GetValue(operacao)!);
    }

    private static string? Texto(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?
            .GetValue(operacao) as string;
}
