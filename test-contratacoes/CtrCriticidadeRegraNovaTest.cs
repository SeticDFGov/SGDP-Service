using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using api.Contratacoes;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Pedido de 2026-09-24 da equipe da SGDI (decisões do usuário): o critério II passa a
/// perguntar se a contratação impacta diretamente algum projeto de Transformação Digital do
/// órgão (Sim 0, Não 1, Desconhecido 1; sem resposta vale Desconhecido); o III é invertido
/// (Sim 0, Não 1; o padrão continua Não); a regra nova vale para todos os processos, e a
/// resposta antiga do II (Nenhum a Alto) fica gravada, conta como Desconhecido e aparece como
/// referência até alguém responder à pergunta nova; a tela mostra a composição da soma.
/// </summary>
public class CtrCriticidadeRegraNovaTest : CtrTestBase
{
    private const string Numero = "04044-00000700/2026-11";

    private readonly CtrProcessoService _processos;

    public CtrCriticidadeRegraNovaTest()
    {
        _processos = NovoProcessoService();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>"I=Sim,II=Alto" vira o dicionário de respostas (vazio = todas no padrão).</summary>
    private static Dictionary<string, string> Respostas(string texto) =>
        texto.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(par => par.Split('='))
            .ToDictionary(par => par[0].Trim(), par => par[1].Trim());

    /// <summary>Jsonb como a coluna guarda, depois da normalização.</summary>
    private static string Gravadas(string texto) =>
        CtrCriticidade.Serializar(CtrCriticidade.Normalizar(Respostas(texto)));

    /// <summary>Processo gravado antes de 2026-09-24: II respondido à pergunta anterior.</summary>
    private CtrProcesso SemearComRespostaAnterior(string respostaII = "Alto", string criticidade = CtrDominios.Criticidade.Alta) =>
        SemearProcesso(Numero, p =>
        {
            p.ChegadaSgdi = DiasAtras(10);
            // Como a regra anterior gravou: II graduado e a criticidade calculada por ela
            p.CriteriosCriticidade = Gravadas($"II={respostaII},IV=Alto");
            p.Criticidade = criticidade;
        });

    private static CtrProcessoUpdateDTO EdicaoDe(CtrProcesso p)
    {
        var dto = new CtrProcessoUpdateDTO();
        var gravado = CtrProcessoService.DtoDe(p);
        foreach (var prop in typeof(CtrProcessoCreateDTO).GetProperties())
            prop.SetValue(dto, prop.GetValue(gravado));
        return dto;
    }

    // ══ 1. Critério II: a pergunta nova ══════════════════════════════════════

    [Fact]
    public void CriterioII_TemSimNaoEDesconhecido_NessaOrdem_EOPadraoEhDesconhecido()
    {
        Assert.Equal("II", CtrDominios.CriterioCriticidade.ProjetoTransformacaoDigital);
        Assert.Equal(new[] { "Sim", "Não", "Desconhecido" },
            CtrDominios.CriterioCriticidade.RespostasDe(CtrDominios.CriterioCriticidade.ProjetoTransformacaoDigital));
        Assert.Equal(CtrDominios.CriterioCriticidade.Desconhecido,
            CtrDominios.CriterioCriticidade.RespostaPadrao(CtrDominios.CriterioCriticidade.ProjetoTransformacaoDigital));
        // O II deixou de ser graduado
        Assert.DoesNotContain(CtrDominios.CriterioCriticidade.ProjetoTransformacaoDigital,
            CtrDominios.CriterioCriticidade.Graduados);
    }

    [Theory]
    [InlineData("Sim", 0)]
    [InlineData("Não", 1)]
    [InlineData("Desconhecido", 1)]
    public void CriterioII_Pontos_Sim0_Nao1_Desconhecido1(string resposta, int pontos)
    {
        Assert.Equal(pontos, CtrCriticidade.Pontos(CtrDominios.CriterioCriticidade.ProjetoTransformacaoDigital, resposta));

        var respostas = CtrCriticidade.Normalizar(new Dictionary<string, string> { ["II"] = resposta });
        Assert.Equal(pontos, CtrCriticidade.PontosPorCriterio(respostas)["II"]);
    }

    [Fact]
    public void CriterioII_SemResposta_ValeDesconhecido()
    {
        var semII = CtrCriticidade.Normalizar(Respostas("III=Sim"));
        var comDesconhecido = CtrCriticidade.Normalizar(Respostas("II=Desconhecido,III=Sim"));

        Assert.Equal(CtrDominios.CriterioCriticidade.Desconhecido, semII["II"]);
        Assert.Equal(1, CtrCriticidade.PontosPorCriterio(semII)["II"]);
        Assert.Equal(CtrCriticidade.PontosTotais(comDesconhecido), CtrCriticidade.PontosTotais(semII));

        // Mesmo sem normalizar (o parser da planilha guarda só as respondidas), a conta usa o padrão
        Assert.Equal(1, CtrCriticidade.PontosPorCriterio(Respostas("III=Sim"))["II"]);
    }

    [Theory]
    [InlineData("desconhecido", "Desconhecido")]
    [InlineData("NAO", "Não")]
    [InlineData(" sim ", "Sim")]
    public void CriterioII_Normalizar_AceitaCaixaEAcento(string enviada, string gravada)
    {
        var respostas = CtrCriticidade.Normalizar(new Dictionary<string, string> { ["ii"] = enviada });

        Assert.Equal(gravada, respostas["II"]);
    }

    [Theory]
    [InlineData("III", "Desconhecido")]  // Desconhecido é só do II
    [InlineData("II", "Talvez")]
    [InlineData("II", "Não foi possível avaliar")]
    public void Normalizar_RecusaRespostaForaDoCriterio(string codigo, string resposta)
    {
        var ex = Assert.Throws<ApiException>(() =>
            CtrCriticidade.Normalizar(new Dictionary<string, string> { [codigo] = resposta }));

        Assert.Equal((int)ErrorCode.CtrDominioInvalido, ex.Error.Code);
        Assert.Contains($"Resposta inválida no critério {codigo}", ex.Error.Message);
    }

    // ══ 2. Critério III invertido ════════════════════════════════════════════

    [Theory]
    [InlineData("Sim", 0)]
    [InlineData("Não", 1)]
    public void CriterioIII_Pontos_Sim0_Nao1(string resposta, int pontos)
    {
        Assert.Equal(pontos, CtrCriticidade.Pontos(CtrDominios.CriterioCriticidade.Compartilhamento, resposta));
    }

    [Fact]
    public void CriterioIII_RespostaPadraoContinuaNao_EPassaAValerUmPonto()
    {
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao,
            CtrDominios.CriterioCriticidade.RespostaPadrao(CtrDominios.CriterioCriticidade.Compartilhamento));

        // Sem responder ao III, ele vale o mesmo que Não
        var semIII = CtrCriticidade.Normalizar(Respostas("II=Sim"));
        Assert.Equal(CtrDominios.CriterioCriticidade.Nao, semIII["III"]);
        Assert.Equal(1, CtrCriticidade.PontosPorCriterio(semIII)["III"]);
        Assert.Equal(1, CtrCriticidade.PontosTotais(semIII));
    }

    // ══ 3. A resposta à pergunta anterior do II ══════════════════════════════

    [Theory]
    [InlineData("Nenhum")]
    [InlineData("Baixo")]
    [InlineData("Médio")]
    [InlineData("Alto")]
    public void RespostaAnteriorDoII_ContaComoDesconhecido_EFicaComoReferencia(string anterior)
    {
        var comAnterior = CtrCriticidade.Normalizar(Respostas($"II={anterior},V=Sim"));
        var comDesconhecido = CtrCriticidade.Normalizar(Respostas("II=Desconhecido,V=Sim"));

        // Guardada como veio (grafia do domínio), não trocada pelo padrão
        Assert.Equal(anterior, comAnterior["II"]);
        Assert.Equal(anterior, CtrCriticidade.RespostaPerguntaAnteriorII(comAnterior));
        // Na conta, igual a Desconhecido: antes o "Alto" valia 3 e o "Nenhum" valia 0
        Assert.Equal(1, CtrCriticidade.PontosPorCriterio(comAnterior)["II"]);
        Assert.Equal(CtrCriticidade.PontosTotais(comDesconhecido), CtrCriticidade.PontosTotais(comAnterior));
        Assert.Equal(CtrCriticidade.Calcular(comDesconhecido), CtrCriticidade.Calcular(comAnterior));
    }

    [Theory]
    [InlineData("medio", "Médio")]
    [InlineData("ALTO", "Alto")]
    public void RespostaAnteriorDoII_AceitaCaixaEAcento(string enviada, string gravada)
    {
        Assert.Equal(gravada, CtrCriticidade.ResolverResposta("II", enviada));
    }

    [Theory]
    [InlineData("Sim")]
    [InlineData("Não")]
    [InlineData("Desconhecido")]
    public void RespostaAnteriorDoII_SomeQuandoOIIEhRespondidoAPerguntaNova(string nova)
    {
        Assert.Null(CtrCriticidade.RespostaPerguntaAnteriorII(CtrCriticidade.Normalizar(Respostas($"II={nova}"))));
        Assert.Null(CtrCriticidade.RespostaPerguntaAnteriorII(null));
    }

    [Fact]
    public async Task Get_ProcessoComARespostaAnterior_MostraAReferenciaEAComposicao()
    {
        var processo = SemearComRespostaAnterior("Alto");

        var resposta = await _processos.GetAsync(processo.Id);

        Assert.Equal("Alto", resposta.CriteriosCriticidade!["II"]);
        Assert.Equal("Alto", resposta.RespostaPerguntaAnteriorII);
        // II anterior (1, como Desconhecido) + III Não (1) + IV Alto (3) = 5
        Assert.Equal(1, resposta.PontosPorCriterio!["II"]);
        Assert.Equal(5, resposta.PontosCriticidade);
    }

    [Fact]
    public async Task Atualizar_SemResponderAoII_MantemARespostaAnteriorERecalculaPelaRegraNova()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Alto", CtrDominios.Criticidade.Alta);

        // O corpo manda os critérios SEM o II (cliente que omite a chave)
        var dto = EdicaoDe(processo);
        dto.CriteriosCriticidade = Respostas("I=Não,III=Não,IV=Alto");

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal("Alto", resposta.CriteriosCriticidade!["II"]);
        Assert.Equal("Alto", resposta.RespostaPerguntaAnteriorII);
        // Pela regra anterior era Alta (3 + 3); pela nova, 1 + 1 + 3 = 5, Média
        Assert.Equal(CtrDominios.Criticidade.Media, resposta.Criticidade);
        Assert.Equal(5, resposta.PontosCriticidade);
        Assert.Contains("\"II\":\"Alto\"", Context.CtrProcessos.Single(p => p.Id == processo.Id).CriteriosCriticidade);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Atualizar_ComOIIVazio_MantemARespostaAnterior(string vazio)
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Baixo");

        var dto = EdicaoDe(processo);
        dto.CriteriosCriticidade = new Dictionary<string, string> { ["ii"] = vazio, ["IV"] = "Alto" };

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal("Baixo", resposta.RespostaPerguntaAnteriorII);
    }

    [Fact]
    public async Task Atualizar_DevolvendoOQueVeio_MantemARespostaAnterior()
    {
        // É o que o formulário faz enquanto ninguém responde à pergunta nova
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Médio");

        var resposta = await _processos.AtualizarAsync(processo.Id, EdicaoDe(processo), ctx);

        Assert.Equal("Médio", resposta.RespostaPerguntaAnteriorII);
        Assert.Equal(1, resposta.PontosPorCriterio!["II"]);
    }

    [Fact]
    public async Task Atualizar_RespondendoAPerguntaNova_SubstituiARespostaAnterior()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Alto");

        var dto = EdicaoDe(processo);
        dto.CriteriosCriticidade!["II"] = "Sim";

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal("Sim", resposta.CriteriosCriticidade!["II"]);
        Assert.Null(resposta.RespostaPerguntaAnteriorII);
        // II Sim (0) + III Não (1) + IV Alto (3) = 4, Média
        Assert.Equal(0, resposta.PontosPorCriterio!["II"]);
        Assert.Equal(4, resposta.PontosCriticidade);
    }

    [Fact]
    public async Task Atualizar_SemCriteriosNoCorpo_PreservaTudoComoAntes()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Alto");

        var dto = EdicaoDe(processo);
        dto.CriteriosCriticidade = null;

        var resposta = await _processos.AtualizarAsync(processo.Id, dto, ctx);

        Assert.Equal("Alto", resposta.RespostaPerguntaAnteriorII);
        Assert.Equal(CtrDominios.Criticidade.Media, resposta.Criticidade);
    }

    [Fact]
    public async Task Criar_ComARespostaAnteriorNoII_EhAceito()
    {
        // O front anterior (na janela entre a publicação da API e a do front) ainda manda o II graduado
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.CriteriosCriticidade = Respostas("II=Alto,V=Sim");

        var resposta = await _processos.CriarAsync(dto, ctx);

        Assert.Equal("Alto", resposta.RespostaPerguntaAnteriorII);
        // 1 (II anterior) + 1 (III) + 3 (V) = 5, Média
        Assert.Equal(CtrDominios.Criticidade.Media, resposta.Criticidade);
    }

    [Fact]
    public async Task Checkpoint_RecalculaPelaRegraNovaEMantemARespostaAnterior()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearComRespostaAnterior("Alto", CtrDominios.Criticidade.Alta);

        var resposta = await _processos.RegistrarCheckpointAsync(processo.Id, new CtrCheckpointDTO
        {
            Etapa = CtrDominios.Etapa.ChegadaSubgd,
            Data = DiasAtras(5)
        }, ctx);

        Assert.Equal(CtrDominios.Criticidade.Media, resposta.Criticidade);
        Assert.Equal("Alto", resposta.RespostaPerguntaAnteriorII);
    }

    // ══ 4. Composição da soma (prévia e tela do processo) ════════════════════

    [Fact]
    public void Composicao_TrazOsSeteCriteriosNaOrdem_EOVIISemPontos()
    {
        var respostas = CtrCriticidade.Normalizar(Respostas("I=Sim,II=Não,III=Sim,IV=Médio,V=Sim,VI=Baixo,VII=Sim"));

        var composicao = CtrCriticidade.PontosPorCriterio(respostas);

        Assert.Equal(CtrDominios.CriterioCriticidade.Todos, composicao.Keys);
        Assert.Equal(new[] { -1, 1, 0, 2, 3, 1, 0 }, composicao.Values);
        Assert.Equal(6, CtrCriticidade.SomaDosCriterios(respostas));
        Assert.Equal(6, CtrCriticidade.PontosTotais(respostas));
        // O VII não soma: decide Alta sozinho
        Assert.True(CtrCriticidade.ValorNoLimite(respostas));
        Assert.Equal(CtrDominios.Criticidade.Alta, CtrCriticidade.Calcular(respostas));
    }

    [Fact]
    public void Composicao_ASomaNegativaParaEmZero()
    {
        var respostas = CtrCriticidade.Normalizar(Respostas("I=Sim,II=Sim,III=Sim"));

        Assert.Equal(new[] { -1, 0, 0, 0, 0, 0, 0 }, CtrCriticidade.PontosPorCriterio(respostas).Values);
        Assert.Equal(-1, CtrCriticidade.SomaDosCriterios(respostas));
        Assert.Equal(0, CtrCriticidade.PontosTotais(respostas));
        Assert.Equal(CtrDominios.Criticidade.Baixa, CtrCriticidade.Calcular(respostas));
    }

    [Fact]
    public void Composicao_VIISimDecideAltaMesmoComPoucosPontos()
    {
        var respostas = CtrCriticidade.Normalizar(Respostas("II=Sim,III=Sim,VII=Sim"));

        Assert.Equal(0, CtrCriticidade.PontosTotais(respostas));
        Assert.Equal(0, CtrCriticidade.PontosPorCriterio(respostas)["VII"]);
        Assert.Equal(CtrDominios.Criticidade.Alta, CtrCriticidade.Calcular(respostas));
    }

    [Fact]
    public async Task Resposta_TrazAComposicaoQueSomaOsPontos_ESemRespostasTrazNulo()
    {
        var ctx = await ContextoAnalistaAsync();
        var dto = NovoProcessoDto(Numero);
        dto.CriteriosCriticidade = Respostas("II=Não,IV=Baixo,V=Sim");

        var comRespostas = await _processos.CriarAsync(dto, ctx);

        Assert.Equal(CtrDominios.CriterioCriticidade.Todos, comRespostas.PontosPorCriterio!.Keys);
        Assert.Equal(comRespostas.PontosCriticidade, comRespostas.PontosPorCriterio.Values.Sum());
        Assert.Equal(6, comRespostas.PontosCriticidade);

        // Processo classificado antes da regra (sem respostas): nem pontos nem composição
        var antigo = SemearProcesso("04044-00000701/2026-12", p => p.CriteriosCriticidade = null);
        var semRespostas = await _processos.GetAsync(antigo.Id);
        Assert.Null(semRespostas.PontosCriticidade);
        Assert.Null(semRespostas.PontosPorCriterio);
        Assert.Null(semRespostas.RespostaPerguntaAnteriorII);

        // A lista traz a composição também (a tela do processo e a lista leem o mesmo DTO)
        var lista = await _processos.ListarAsync(new CtrProcessoFiltro { PageSize = 50 });
        Assert.NotNull(lista.Items.Single(p => p.NumeroProcesso == Numero).PontosPorCriterio);
    }

    // ══ 5. Planilha ══════════════════════════════════════════════════════════

    [Fact]
    public void Csv_ColunaDoII_TemONomeDaPerguntaNova()
    {
        Assert.Equal("Critério II - Impacta projeto de Transformação Digital", CtrCsv.ColunasCriterios[1]);
    }

    [Fact]
    public async Task Csv_RespostaAnteriorDoII_SaiNoExportEVoltaSemPerda()
    {
        var ctx = await ContextoAnalistaAsync();
        // A criticidade gravada já é a da regra nova (a migration recalcula todos)
        SemearComRespostaAnterior("Alto", CtrDominios.Criticidade.Media);

        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var linha = Assert.Single((await NovoImportacaoService().PreviaAsync(bytes)).Linhas);

        Assert.Equal(CtrImportacaoAcao.Atualizar, linha.Acao);
        Assert.Null(linha.Motivo);
        Assert.Equal("Alto", linha.Dados!.CriteriosCriticidade!["II"]);

        await NovoImportacaoService().ImportarAsync(bytes, ctx);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Contains("\"II\":\"Alto\"", gravado.CriteriosCriticidade);
        Assert.Equal(CtrDominios.Criticidade.Media, gravado.Criticidade);
    }

    [Fact]
    public async Task Csv_CelulaDoIIVazia_MantemARespostaAnteriorGravada()
    {
        var ctx = await ContextoAnalistaAsync();
        SemearComRespostaAnterior("Alto", CtrDominios.Criticidade.Media);

        // Export, depois o II apagado na planilha (a Criticidade continua conferindo: 5, Média)
        var bytes = await _processos.ExportarCsvAsync(new CtrProcessoFiltro());
        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var linhas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var celulas = linhas[1].Split(';');
        Assert.Equal("Alto", celulas[23]);
        celulas[23] = string.Empty;
        var editado = BytesUtf8ComBom(linhas[0] + "\r\n" + string.Join(";", celulas) + "\r\n");

        var relatorio = await NovoImportacaoService().ImportarAsync(editado, ctx);

        Assert.Equal(1, relatorio.Atualizados);
        var gravado = Context.CtrProcessos.Single(p => p.NumeroProcesso == Numero);
        Assert.Equal("Alto", CtrCriticidade.RespostaPerguntaAnteriorII(CtrCriticidade.Desserializar(gravado.CriteriosCriticidade)));
    }

    [Fact]
    public void Csv_ExportAnteriorARegraNova_ComACriticidadeAntiga_EhRecusadoComAMensagemQueOrienta()
    {
        // Linha de um export feito antes da regra nova: II=Alto e IV=Alto davam Alta (3 + 3);
        // pela regra nova somam 5 (Média), e a coluna Criticidade não confere mais
        var linha = new string[CtrCsv.Cabecalho.Length];
        Array.Fill(linha, string.Empty);
        linha[0] = Numero;
        linha[1] = "Economia";
        linha[2] = "SEEC";
        linha[4] = "Switches";
        linha[5] = CtrDominios.CategoriaObjeto.InfraestruturaRede;
        linha[12] = "Não";
        linha[17] = "Alta";
        linha[23] = "Alto";
        linha[25] = "Alto";
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n" + string.Join(";", linha) + "\r\n";

        var lida = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(lida.Dados);
        Assert.Contains("não confere com os critérios (pelas respostas seria Média)", lida.Erro);
        Assert.Contains("deixe a Criticidade vazia", lida.Erro);
    }

    // ══ 6. Migration: colunas novas e a criticidade de todos pela regra nova ══

    private const string NomeMigration = "demanda_service.Migrations.CtrCriticidadeFormularioAnaliseTecnica";

    private static List<object> Operacoes(string lado)
    {
        var tipo = typeof(CtrProcesso).Assembly.GetType(NomeMigration);
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(operacao);

    private static string TipoDe(object operacao) => operacao.GetType().Name;

    private static string Compacto(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    [Fact]
    public void Migration_SoAcrescentaSeisColunasNullableETresChecksEmCtrProcesso()
    {
        var operacoes = Operacoes("UpOperations");

        // Nada é removido nem alterado: só colunas, CHECKs novos e o recálculo
        Assert.All(operacoes, o => Assert.Contains(TipoDe(o),
            new[] { "AddColumnOperation", "AddCheckConstraintOperation", "SqlOperation" }));

        var colunas = operacoes.Where(o => TipoDe(o) == "AddColumnOperation").ToDictionary(o => (string)Valor(o, "Name")!);
        Assert.All(colunas.Values, c => Assert.Equal("ctr_processo", Valor(c, "Table")));
        Assert.All(colunas.Values, c => Assert.True((bool)Valor(c, "IsNullable")!));
        Assert.Equal(
            new[]
            {
                "analise_tecnica_area", "analise_tecnica_encaminhada_em", "analise_tecnica_retorno_em",
                "analise_tecnica_retorno_resumo", "esclarecimento_documento_sei", "numero_sei_formulario"
            },
            colunas.Keys.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("character varying(25)", Valor(colunas["numero_sei_formulario"], "ColumnType"));
        Assert.Equal("character varying(60)", Valor(colunas["esclarecimento_documento_sei"], "ColumnType"));
        Assert.Equal("character varying(10)", Valor(colunas["analise_tecnica_area"], "ColumnType"));
        Assert.Equal("date", Valor(colunas["analise_tecnica_encaminhada_em"], "ColumnType"));
        Assert.Equal("date", Valor(colunas["analise_tecnica_retorno_em"], "ColumnType"));
        Assert.Equal("character varying(1000)", Valor(colunas["analise_tecnica_retorno_resumo"], "ColumnType"));

        var checks = operacoes.Where(o => TipoDe(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => (string)Valor(o, "Name")!, o => (string)Valor(o, "Sql")!);
        Assert.All(operacoes.Where(o => TipoDe(o) == "AddCheckConstraintOperation"),
            c => Assert.Equal("ctr_processo", Valor(c, "Table")));
        Assert.Equal(
            new[] { "ck_ctr_processo_analise_tecnica", "ck_ctr_processo_analise_tecnica_area", "ck_ctr_processo_esclarecimento_documento" },
            checks.Keys.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("analise_tecnica_area IS NULL OR analise_tecnica_area IN ('SUBSIS','SUBINFRA')",
            checks["ck_ctr_processo_analise_tecnica_area"]);
        Assert.Equal("esclarecimento_documento_sei IS NULL OR esclarecimento_solicitado_em IS NOT NULL",
            checks["ck_ctr_processo_esclarecimento_documento"]);
        Assert.Contains("analise_tecnica_retorno_em >= analise_tecnica_encaminhada_em",
            checks["ck_ctr_processo_analise_tecnica"]);
    }

    [Fact]
    public void Migration_RecalculaSoACriticidadeDosProcessosComRespostas_PelaRegraNova_NoFim()
    {
        var operacoes = Operacoes("UpOperations");

        var sql = Assert.Single(operacoes, o => TipoDe(o) == "SqlOperation");
        // Depois das colunas e dos CHECKs (é a última operação)
        Assert.Equal(operacoes.Count - 1, operacoes.IndexOf(sql));

        var texto = Compacto((string)Valor(sql, "Sql")!);
        Assert.StartsWith("UPDATE ctr_processo AS p SET criticidade = CASE", texto);
        // Só a coluna derivada é regravada: nenhuma resposta é tocada
        Assert.DoesNotContain("SET criticidade_criterios", texto);
        Assert.DoesNotContain("DELETE", texto);
        Assert.DoesNotContain("pgia_", texto);
        Assert.DoesNotContain("\"Users\"", texto);
        Assert.Contains("WHERE criticidade_criterios IS NOT NULL AND jsonb_typeof(criticidade_criterios) = 'object'", texto);
        // A regra nova: II e III valem 0 no Sim e 1 no resto; o VII decide Alta
        Assert.Contains("(CASE WHEN criticidade_criterios ->> 'II' = 'Sim' THEN 0 ELSE 1 END)", texto);
        Assert.Contains("(CASE WHEN criticidade_criterios ->> 'III' = 'Sim' THEN 0 ELSE 1 END)", texto);
        Assert.Contains("(CASE WHEN criticidade_criterios ->> 'I' = 'Sim' THEN -1 ELSE 0 END)", texto);
        Assert.Contains("(CASE WHEN criticidade_criterios ->> 'V' = 'Sim' THEN 3 ELSE 0 END)", texto);
        Assert.Contains("COALESCE(criticidade_criterios ->> 'VII', '') = 'Sim' AS valor_no_limite", texto);
        Assert.Contains($"WHEN c.pontos >= {CtrCriticidade.PontosMinimosAlta} THEN 'Alta'", texto);
        Assert.Contains($"WHEN c.pontos >= {CtrCriticidade.PontosMinimosMedia} THEN 'Média'", texto);
        Assert.Contains("GREATEST(0,", texto);
    }

    [Fact]
    public void Migration_DownVoltaARegraAnteriorAntesDeTirarAsColunas()
    {
        var operacoes = Operacoes("DownOperations");

        // Primeiro o recálculo pela regra anterior, depois os três CHECKs e as seis colunas
        Assert.Equal("SqlOperation", TipoDe(operacoes[0]));
        var texto = Compacto((string)Valor(operacoes[0], "Sql")!);
        Assert.Contains("(CASE criticidade_criterios ->> 'II' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)", texto);
        Assert.Contains("(CASE WHEN criticidade_criterios ->> 'III' = 'Sim' THEN 1 ELSE 0 END)", texto);

        Assert.Equal(3, operacoes.Count(o => TipoDe(o) == "DropCheckConstraintOperation"));
        Assert.Equal(6, operacoes.Count(o => TipoDe(o) == "DropColumnOperation"));
        Assert.All(operacoes.Skip(1), o => Assert.Equal("ctr_processo", Valor(o, "Table")));
    }
}
