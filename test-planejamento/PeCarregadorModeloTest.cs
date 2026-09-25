using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Carregador do modelo inicial: a trilha da seção 7 do plano (títulos, guia, base legal,
/// tipo e a situação em cada nível) e os campos da seção 8, idempotente, sem nunca
/// sobrescrever o que o administrador mudou e sem derrubar a API quando as tabelas ainda
/// não existem (intervalo do deploy).
/// </summary>
public class PeCarregadorModeloTest : PeModeloTestBase
{
    /// <summary>
    /// A trilha da seção 7 do plano, na ordem: chave, título exatamente como no plano, e a
    /// situação em Básico, Intermediário e Avançado ("sim" e "trav." = obrigatório; célula
    /// vazia = desligado; "lista", "padrão", "simples" e afins = passo ligado, simplificado
    /// pelos campos).
    /// </summary>
    public static readonly (string Chave, string Titulo, string Niveis)[] TrilhaDoPlano =
    {
        ("preparacao.abrangencia", "Diga o que o PDTIC abrange e por quanto tempo vale", "o o o"),
        ("preparacao.nomes", "Defina os nomes do órgão: comitê, equipes, autoridade máxima, unidade de TIC", "o o o"),
        ("preparacao.sgtic", "Registre o SGTIC: ato de criação ou a estrutura que faz esse papel, e os membros", "o o o"),
        ("preparacao.equipe", "Monte a equipe de elaboração", "o o o"),
        ("preparacao.metodologia", "Descreva a metodologia e ajuste o fluxo", "o o o"),
        ("preparacao.documentos-referencia", "Liste os documentos de referência", "d o o"),
        ("preparacao.estrategias", "Mostre as estratégias do órgão: PETIC-DF, EGD/DF, PEI, PPA, competências", "o o o"),
        ("preparacao.principios", "Escolha princípios e diretrizes", "o o o"),
        ("preparacao.plano-trabalho", "Faça o plano de trabalho e o cronograma", "d d o"),
        ("preparacao.aprovacao-plano-trabalho", "Registre a aprovação do plano de trabalho", "d d o"),
        ("diagnostico.pdtic-anterior", "Veja o que o PDTIC anterior entregou", "d o o"),
        ("diagnostico.referencial-estrategico", "Descreva o referencial estratégico da TIC: missão, visão, valores e objetivos", "d o o"),
        ("diagnostico.ambiente-tecnologico", "Descreva o ambiente tecnológico e a organização da TIC", "o o o"),
        ("diagnostico.ativos", "Liste as soluções e os ativos de TIC", "o o o"),
        ("diagnostico.swot", "Faça a análise SWOT da TIC", "d o o"),
        ("diagnostico.capacidade", "Estime a capacidade de execução", "d d o"),
        ("diagnostico.plano-levantamento", "Planeje o levantamento das necessidades", "d d o"),
        ("diagnostico.necessidades-informacao", "Levante as necessidades de informação", "d o o"),
        ("diagnostico.necessidades-tic", "Levante as necessidades de TIC: serviços, infraestrutura, contratação e pessoal", "o o o"),
        ("diagnostico.sistemas-ia", "Confira os sistemas de IA (vêm do PGIA) e planeje as aquisições", "o o o"),
        ("diagnostico.inventario", "Consolide o inventário e ligue as necessidades às estratégias", "d o o"),
        ("diagnostico.aprovacao-inventario", "Registre a aprovação do inventário", "d d o"),
        ("planejamento.criterios-priorizacao", "Confirme os critérios de priorização", "d o o"),
        ("planejamento.priorizacao", "Priorize as necessidades", "o o o"),
        ("planejamento.metas-acoes", "Defina metas, indicadores e ações", "o o o"),
        ("planejamento.acoes-tematicas", "Confira as ações de segurança, transformação digital e dados", "o o o"),
        ("planejamento.contratacoes", "Planeje as contratações de TIC, ligadas ao Plano de Contratações Anual", "o o o"),
        ("planejamento.pessoas", "Planeje as pessoas", "d o o"),
        ("planejamento.orcamento", "Planeje o orçamento", "d o o"),
        ("planejamento.fatores-criticos", "Liste os fatores críticos de sucesso", "d d o"),
        ("planejamento.riscos", "Planeje os riscos", "d o o"),
        ("planejamento.documento", "Monte o documento e confira a prévia", "o o o"),
        ("planejamento.aprovacao-sgtic", "Registre a aprovação do SGTIC e envie à SGDI", "o o o"),
        ("planejamento.deliberacao-cgtic", "Acompanhe a deliberação do CGTIC", "o o o"),
        ("planejamento.publicacao", "Registre a publicação", "o o o"),
        ("plano-acompanhamento.quem-acompanha", "Diga quem acompanha: a mesma equipe ou outra, com o ato", "o o o"),
        ("plano-acompanhamento.plano-execucao", "Planeje a execução: projetos de cada ação e quanto cada um contribui", "d d o"),
        ("plano-acompanhamento.plano-monitoramento", "Planeje o monitoramento: periodicidade, indicadores e valores de referência", "d o o"),
        ("plano-acompanhamento.plano-avaliacao", "Planeje a avaliação: indicadores de resultado e metas intermediárias", "d d o"),
        ("plano-acompanhamento.plano-acompanhamento", "Feche o plano de acompanhamento: riscos atualizados e comunicação", "d d o"),
        ("plano-acompanhamento.aprovacao-acompanhamento", "Registre a aprovação do plano de acompanhamento", "d d o"),
        // F1 (C18): o título do 5.1 serve a todos os níveis (o Básico não registra medições nem riscos)
        ("monitoramento.ciclo-monitoramento", "Atualize a situação das ações no ciclo", "o o o"),
        ("monitoramento.relatorio-acompanhamento", "Feche o ciclo no relatório de acompanhamento", "d o o"),
        ("avaliacao-intermediaria.resultados-intermediarios", "Consolide os resultados intermediários", "d o o"),
        ("avaliacao-intermediaria.comparacao-metas", "Compare com as metas e proponha ajustes", "d o o"),
        ("avaliacao-intermediaria.avaliacao-comite", "Registre a avaliação do comitê: seguir ou revisar o PDTIC", "d o o"),
        ("fechamento.indicadores-finais", "Colete os indicadores finais", "d o o"),
        ("fechamento.licoes-aprendidas", "Analise os resultados e registre as lições aprendidas", "o o o"),
        ("fechamento.aprovacao-comite", "Registre a aprovação do comitê", "d o o"),
        ("fechamento.aprovacao-autoridade", "Registre a aprovação da autoridade máxima", "o o o"),
    };

    // ── Conteúdo ──────────────────────────────────────────────────────────────

    [Fact]
    public void Carga_TresNiveis_SeteEtapas_CinquentaPassos_ESecoesCamposEOpcoesDaSecao8()
    {
        Assert.Equal(new[] { "basico", "intermediario", "avancado" },
            Context.PeNiveis.OrderBy(n => n.Ordem).Select(n => n.Codigo));
        Assert.Equal(new[] { "Básico", "Intermediário", "Avançado" },
            Context.PeNiveis.OrderBy(n => n.Ordem).Select(n => n.Nome));

        Assert.Equal(new[] { "Prepare o PDTIC", "Faça o diagnóstico", "Planeje", "Planeje o acompanhamento", "Monitore",
                "Avalie no meio do caminho", "Feche o ciclo" },
            Context.PeEtapas.OrderBy(e => e.Ordem).Select(e => e.Titulo));
        var etapas = Context.PeEtapas.OrderBy(e => e.Ordem).ToList();
        Assert.Equal(new[] { 10, 12, 13, 6, 2, 3, 4 }, etapas.Select(e => Context.PePassos.Count(p => p.EtapaId == e.Id)));

        Assert.Equal(50, Context.PePassos.Count());
        // Do PDTIC; as seções do DF e do PETIC-DF (versão 2) têm teste próprio no PeReferenciaisCargaTest
        Assert.Equal(68, Context.PeSecoes.Count(s => s.Escopo == PeDominios.Escopo.Pdtic));
        // 345 da E2 e o logotipo do dicionário de nomes (versão 3, E5)
        Assert.Equal(346, Context.PeCampos.Count(c => c.Secao!.Escopo == PeDominios.Escopo.Pdtic));
        Assert.Equal(262, Context.PeOpcoes.Count(o => o.Campo!.Secao!.Escopo == PeDominios.Escopo.Pdtic));
        Assert.All(Context.PePassos.ToList(), p => Assert.True(p.Sistema));
        Assert.All(Context.PeCampos.ToList(), c => Assert.True(c.Sistema));

        // Cada item tem uma linha por nível
        Assert.Equal(50 * 3, Context.PePassosNivel.Count());
        Assert.Equal(68 * 3, Context.PeSecoesNivel.Count());
        Assert.Equal(346 * 3, Context.PeCamposNivel.Count());
    }

    [Fact]
    public void Trilha_EhADaSecao7DoPlano_NaOrdem_ComOsTitulosEASituacaoEmCadaNivel()
    {
        var passos = Context.PePassos.Include(p => p.Etapa).ToList()
            .OrderBy(p => p.Etapa!.Ordem).ThenBy(p => p.Ordem).ToList();

        Assert.Equal(TrilhaDoPlano.Select(t => t.Chave), passos.Select(p => p.Chave));
        foreach (var (chave, titulo, niveis) in TrilhaDoPlano)
        {
            Assert.Equal(titulo, passos.Single(p => p.Chave == chave).Titulo);
            Assert.Equal(niveis, SituacoesDoPasso(chave));
        }
    }

    [Theory]
    [InlineData("preparacao.abrangencia", "1.1", null, "dados")]
    [InlineData("preparacao.sgtic", null, "art. 8º, §§ 1º e 2º", "dados")]
    [InlineData("preparacao.estrategias", "1.5", "art. 9º, § 1º, do Decreto nº 48.899/2026", "dados")]
    [InlineData("preparacao.aprovacao-plano-trabalho", "1.8", null, "aprovacao")]
    [InlineData("diagnostico.ativos", "Anexo X, item 19", "art. 12, § 2º, II", "dados")]
    [InlineData("diagnostico.necessidades-tic", "2.8 a 2.11", "art. 12, § 2º, III", "dados")]
    [InlineData("planejamento.acoes-tematicas", null, "art. 12, § 2º, V, VI e IX", "conferencia_temas")]
    [InlineData("planejamento.contratacoes", "2.10 e 3.5", "art. 12, § 1º, e § 2º, IV", "dados")]
    [InlineData("planejamento.documento", "3.8", null, "documento")]
    [InlineData("planejamento.aprovacao-sgtic", "3.9", "art. 7º, V, do Decreto nº 48.899/2026", "envio")]
    [InlineData("planejamento.deliberacao-cgtic", null, "art. 5º", "deliberacao")]
    [InlineData("planejamento.publicacao", "3.10", null, "publicacao")]
    [InlineData("monitoramento.ciclo-monitoramento", "5.1", null, "monitoramento")]
    [InlineData("avaliacao-intermediaria.avaliacao-comite", "6.3", null, "aprovacao")]
    public void Passo_TemGuiaBaseLegalETipo(string chave, string? guia, string? baseLegal, string tipo)
    {
        var passo = Passo(chave);

        Assert.Equal(guia, passo.ReferenciaGuia);
        Assert.Equal(baseLegal, passo.BaseLegal);
        Assert.Equal(tipo, passo.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(passo.OQueFazer));
    }

    [Fact]
    public void Travados_SaoOsNoveConteudosDoArt12_ObrigatoriosEmTodosOsNiveis()
    {
        var travados = Context.PePassos.Where(p => p.Travado).ToList();

        Assert.Equal(
            new[] { "diagnostico.ambiente-tecnologico", "diagnostico.ativos", "diagnostico.necessidades-tic",
                "diagnostico.sistemas-ia", "planejamento.acoes-tematicas", "planejamento.contratacoes", "planejamento.metas-acoes" },
            travados.Select(p => p.Chave).OrderBy(c => c));
        Assert.All(travados, p => Assert.Equal("o o o", SituacoesDoPasso(p.Chave)));
        Assert.All(travados, p => Assert.False(p.AceitaNaoSeAplica));

        // Os nove incisos cobertos, V, VI e IX no passo de conferência dos temas e VIII no dos sistemas de IA
        var incisos = travados.SelectMany(p => p.IncisoDecreto!.Split(',')).OrderBy(i => i).ToList();
        Assert.Equal(PeDominios.Inciso.Todos.OrderBy(i => i), incisos);
        Assert.Equal("V,VI,IX", Passo("planejamento.acoes-tematicas").IncisoDecreto);
        Assert.Equal("VIII", Passo("diagnostico.sistemas-ia").IncisoDecreto);

        // Seções travadas e o campo principal delas: nunca desligados
        var secoes = Context.PeSecoes.Where(s => s.Travada).ToList();
        Assert.Equal(new[] { "acoes", "aquisicoes_ia", "ativos", "contratacoes", "diagnostico_ambiente", "metas", "necessidades" },
            secoes.Select(s => s.Chave).OrderBy(c => c));
        foreach (var secao in secoes)
        {
            Assert.DoesNotContain("d", SituacoesDaSecao(secao.Chave));
            var principal = Context.PeCampos.Single(c => c.SecaoId == secao.Id && c.Principal);
            Assert.DoesNotContain("d", SituacoesDoCampo(secao.Chave, principal.Chave));
        }
    }

    [Fact]
    public void TodaSecao_TemUmCampoPrincipal()
    {
        foreach (var secao in Context.PeSecoes.ToList())
            Assert.Single(Context.PeCampos.Where(c => c.SecaoId == secao.Id && c.Principal));
    }

    [Fact]
    public void Temas_OsTresDoDecretoSaoDoSistemaETravados_ENoCampoTravado()
    {
        var tema = Campo("acoes", "tema");
        Assert.True(tema.Travado);
        Assert.Equal(PeDominios.TipoCampo.ListaMultipla, tema.Tipo);
        Assert.Equal("o o o", SituacoesDoCampo("acoes", "tema"));

        var opcoes = Context.PeOpcoes.Where(o => o.CampoId == tema.Id).OrderBy(o => o.Ordem).ToList();
        Assert.Equal(new[] { "seguranca", "transformacao_digital", "governanca_dados" },
            opcoes.Where(o => o.Travada).Select(o => o.Valor));
        Assert.All(opcoes, o => Assert.True(o.Sistema));
        Assert.Contains("(inciso V)", opcoes[0].Rotulo);
        Assert.Contains("(inciso VI)", opcoes[1].Rotulo);
        Assert.Contains("(inciso IX)", opcoes[2].Rotulo);
    }

    [Fact]
    public void Prioridade_GutDe1a5_ProdutoDosTresCriterios()
    {
        // O front acha a prioridade pelo cálculo (produto ou soma ponderada) e os critérios
        // pelas chaves em Config.campos: números de 1 a 5
        var prioridade = Campo("necessidades", "prioridade");
        Assert.Equal(PeDominios.TipoCampo.Calculado, prioridade.Tipo);
        Assert.Equal(PeDominios.Calculo.Produto, PeConfigCampo.TipoDoCalculo(prioridade.Config));
        Assert.Equal(new[] { "gravidade", "urgencia", "tendencia" }, PeConfigCampo.CamposDoCalculo(prioridade.Config));
        Assert.Single(Context.PeCampos.ToList().Where(c => c.Tipo == PeDominios.TipoCampo.Calculado
            && PeConfigCampo.TipoDoCalculo(c.Config) is PeDominios.Calculo.Produto or PeDominios.Calculo.SomaPonderada));

        foreach (var criterio in new[] { "gravidade", "urgencia", "tendencia" })
        {
            var campo = Campo("necessidades", criterio);
            Assert.Equal(PeDominios.TipoCampo.Numero, campo.Tipo);
            using var config = JsonDocument.Parse(campo.Config);
            Assert.Equal(1, config.RootElement.GetProperty("min").GetInt32());
            Assert.Equal(5, config.RootElement.GetProperty("max").GetInt32());
            Assert.Equal(0, config.RootElement.GetProperty("casas").GetInt32());
            // GUT só nos níveis Intermediário e Avançado; no Básico, a prioridade simples
            Assert.Equal("d o o", SituacoesDoCampo("necessidades", criterio));
        }
        Assert.Equal("o d d", SituacoesDoCampo("necessidades", "prioridade_simples"));
    }

    [Fact]
    public void NivelDeRisco_MatrizProbabilidadeXImpacto_ComBaixoMedioEAlto()
    {
        var nivel = Campo("riscos", "nivel");
        Assert.Equal(PeDominios.Calculo.NivelRisco, PeConfigCampo.TipoDoCalculo(nivel.Config));
        Assert.Equal(new[] { "probabilidade", "impacto" }, PeConfigCampo.CamposDoCalculo(nivel.Config));
        Assert.Equal(new[] { "baixo", "medio", "alto" },
            Context.PeOpcoes.Where(o => o.CampoId == nivel.Id).OrderBy(o => o.Ordem).Select(o => o.Valor));

        using var config = JsonDocument.Parse(nivel.Config);
        var matriz = config.RootElement.GetProperty("matriz");
        Assert.Equal("baixo", matriz.GetProperty("baixa").GetProperty("baixo").GetString());
        Assert.Equal("medio", matriz.GetProperty("baixa").GetProperty("alto").GetString());
        Assert.Equal("medio", matriz.GetProperty("media").GetProperty("medio").GetString());
        Assert.Equal("alto", matriz.GetProperty("alta").GetProperty("medio").GetString());
        Assert.Equal("alto", matriz.GetProperty("alta").GetProperty("alto").GetString());
    }

    [Theory]
    // "Básico: ..." da seção 8 do plano: os campos ligados no Básico
    [InlineData("ativos", "nome,tipo,situacao")]
    [InlineData("necessidades", "descricao,tipo,objetivo_petic,prioridade_simples")]
    [InlineData("acoes", "descricao,tema,responsavel,conclusao,situacao")]
    [InlineData("riscos", "descricao,nivel_simples,acao_preventiva,responsavel")]
    [InlineData("equipe_elaboracao", "nome")]
    public void Basico_LigaSoOsCamposQueOPlanoMarca(string secao, string campos)
    {
        var id = Secao(secao).Id;
        var basico = NivelId("basico");
        var ligados = Context.PeCampos.Where(c => c.SecaoId == id).ToList()
            .Where(c => Context.PeCamposNivel.Any(n => n.CampoId == c.Id && n.NivelId == basico && n.Situacao != "desligado"))
            .OrderBy(c => c.Ordem)
            .Select(c => c.Chave);

        Assert.Equal(campos.Split(',').OrderBy(c => c), ligados.OrderBy(c => c));
    }

    [Fact]
    public void ObjetivoDoPetic_OpcionalNoBasico_ObrigatorioNosOutros()
    {
        Assert.Equal("p o o", SituacoesDoCampo("necessidades", "objetivo_petic"));
        Assert.Equal("p o o", SituacoesDoCampo("metas", "objetivo_petic"));
    }

    [Fact]
    public void Configuracoes_VersaoDoModeloEPeriodicidadeTrimestral()
    {
        // Versão 7 (F1): as correções da revisão final (a 6, da E7 rodada B, trouxe as seções por
        // ciclo, os modelos do RA e do RR e as configurações do acompanhamento)
        Assert.Equal("7", Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo).Valor);
        Assert.Equal("\"trimestral\"",
            Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChavePeriodicidadeMonitoramento).Valor);
        Assert.Equal("15", Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChavePrazoFechamentoCiclo).Valor);
        Assert.Equal("90", Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveDiasAvaliacaoFinal).Valor);
    }

    [Fact]
    public void Json_EmbutidoNaAplicacao_SemTravessao()
    {
        using var stream = typeof(PeCarregadorModelo).Assembly.GetManifestResourceStream(PeCarregadorModelo.Recurso);
        Assert.NotNull(stream);
        var texto = new StreamReader(stream!).ReadToEnd();

        Assert.DoesNotContain('\u2014', texto);
        Assert.DoesNotContain('\u2013', texto);
        Assert.Equal(PeCarregadorModelo.VersaoDaRevisaoFinal, PeCarregadorModelo.LerSeed(texto).Versao);
    }

    // ── Idempotência e "nunca sobrescreve" ────────────────────────────────────

    [Fact]
    public async Task SegundaCarga_NaoFazNada()
    {
        var resultado = await new PeCarregadorModelo(Context).CarregarAsync();

        Assert.False(resultado.Executou);
        Assert.Equal(PeCarregadorModelo.VersaoDaRevisaoFinal, resultado.VersaoAnterior);
        Assert.Equal(50, Context.PePassos.Count());
        // 346 do PDTIC (com o logotipo da versão 3) e 33 do DF e do PETIC-DF (versão 2)
        Assert.Equal(379, Context.PeCampos.Count());
        Assert.Equal(3, Context.PeNiveis.Count());
    }

    [Fact]
    public async Task VersaoNova_SoAcrescentaOQueFalta_ENaoSobrescreveOQueOAdministradorMudou()
    {
        // O administrador renomeia um passo, muda a situação de um campo e desativa uma opção
        await Modelo.AtualizarPassoAsync(Passo("preparacao.nomes").Id, new api.Planejamento.PePassoAtualizarDTO
        {
            Titulo = "Defina os nomes usados pelo órgão",
            Informados = new HashSet<string> { "Titulo" }
        }, EmailAdmin);
        await Modelo.DefinirSituacaoCampoAsync(Campo("ativos", "descricao").Id, Todos("obrigatorio"), EmailAdmin);
        await Modelo.AtualizarOpcaoAsync(Opcao("ativos", "tipo", "licenca").Id,
            new api.Planejamento.PeOpcaoAtualizarDTO { Ativa = false, Informados = new HashSet<string> { "Ativa" } }, EmailAdmin);

        // A versão seguinte do JSON traz um campo novo numa seção que já existe
        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        var ativos = seed.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == "ativos");
        ativos.Campos.Add(new PeSeedCampo { Chave = "fornecedor", Rotulo = "Fornecedor", Tipo = PeDominios.TipoCampo.TextoCurto });

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed);

        Assert.True(resultado.Executou);
        Assert.Equal(1, resultado.Campos);
        Assert.Equal(0, resultado.Passos + resultado.Secoes + resultado.Niveis + resultado.Etapas);
        Assert.Equal("o o o", SituacoesDoCampo("ativos", "fornecedor"));
        Assert.Equal(seed.Versao.ToString(), Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo).Valor);

        // Nada do que o administrador mudou voltou atrás
        Assert.Equal("Defina os nomes usados pelo órgão", Passo("preparacao.nomes").Titulo);
        Assert.Equal("o o o", SituacoesDoCampo("ativos", "descricao"));
        Assert.False(Opcao("ativos", "tipo", "licenca").Ativa);
    }

    [Fact]
    public async Task VersaoJaCarregada_NaoRecriaNemItemQueSumiu()
    {
        // Mesmo que alguém apague uma opção direto no banco, a versão 1 já carregada não é refeita
        var opcao = Context.PeOpcoes.Single(o => o.Id == Opcao("ativos", "tipo", "rede").Id);
        Context.PeOpcoes.Remove(opcao);
        await Context.SaveChangesAsync();

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync();

        Assert.False(resultado.Executou);
        Assert.Empty(Context.PeOpcoes.Where(o => o.Id == opcao.Id));
    }

    // ── Validação do JSON ─────────────────────────────────────────────────────

    [Fact]
    public void Json_PassoTravadoDesligado_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas[1].Passos.Single(p => p.Chave == "diagnostico.ativos").Niveis =
            JsonSerializer.SerializeToElement(new { basico = "desligado", intermediario = "obrigatorio", avancado = "obrigatorio" });

        var ex = Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
        Assert.Contains("diagnostico.ativos", ex.Message);
    }

    [Fact]
    public void Json_SecaoSemCampoPrincipal_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        var secao = seed.Etapas[0].Passos[0].Secoes[0];
        secao.Campos.ForEach(c => c.Principal = false);

        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
    }

    [Fact]
    public void Json_ChaveRepetida_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas[0].Passos[1].Secoes[0].Chave = seed.Etapas[0].Passos[0].Secoes[0].Chave;

        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
    }

    [Fact]
    public async Task Json_ConfigInvalido_RecusaSemGravarNada()
    {
        using var vazio = new PeBancoVazio();
        var seed = PeCarregadorModelo.LerSeed();
        var prioridade = seed.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes)
            .Single(s => s.Chave == "necessidades").Campos.Single(c => c.Chave == "prioridade");
        prioridade.Config = JsonSerializer.SerializeToElement(new { calculo = "produto", campos = new[] { "gravidade", "nao_existe" } });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new PeCarregadorModelo(vazio.Context).CarregarAsync(seed));
        Assert.Contains("necessidades.prioridade", ex.Message);
        Assert.Empty(vazio.Context.PePassos);
    }

    // ── Subida da API sem as tabelas (intervalo do deploy) ────────────────────

    [Fact]
    public async Task ServicoDeSubida_SemAsTabelas_RegistraEDevolveFalso_SemLancar()
    {
        var servicos = new ServiceCollection();
        var nome = Guid.NewGuid().ToString();
        servicos.AddScoped<AppDbContext>(_ => new ContextoSemTabelasPe(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nome).Options));
        servicos.AddScoped<PeCarregadorModelo>();
        await using var provedor = servicos.BuildServiceProvider();

        var servico = new PeCarregadorModeloHostedService(provedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PeCarregadorModeloHostedService>.Instance);

        Assert.False(await servico.TentarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ServicoDeSubida_ComAsTabelas_Carrega()
    {
        using var vazio = new PeBancoVazio();
        var servicos = new ServiceCollection();
        servicos.AddScoped<AppDbContext>(_ => new AppDbContext(vazio.Opcoes));
        servicos.AddScoped<PeCarregadorModelo>();
        await using var provedor = servicos.BuildServiceProvider();

        var servico = new PeCarregadorModeloHostedService(provedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PeCarregadorModeloHostedService>.Instance);

        Assert.True(await servico.TentarAsync(CancellationToken.None));
        Assert.Equal(50, vazio.Context.PePassos.Count());
        // A segunda tentativa (outra réplica subindo) é só a leitura da versão
        Assert.True(await servico.TentarAsync(CancellationToken.None));
        Assert.Equal(50, vazio.Context.PePassos.Count());
    }

    [Fact]
    public void ServicoDeSubida_EsperaCadaVezMais_AteDezMinutos()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), PeCarregadorModeloHostedService.Esperas[0]);
        Assert.Equal(TimeSpan.FromMinutes(10), PeCarregadorModeloHostedService.Esperas[^1]);
    }

    /// <summary>AppDbContext sem as entidades do módulo: ler uma tabela pe_ lança (como a tabela ausente).</summary>
    private sealed class ContextoSemTabelasPe : AppDbContext
    {
        public ContextoSemTabelasPe(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var tipo in modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType)
                         .Where(t => t.Namespace == typeof(PeNivel).Namespace).ToList())
                modelBuilder.Ignore(tipo);
        }
    }
}

/// <summary>Banco InMemory novo, sem nada carregado.</summary>
internal sealed class PeBancoVazio : IDisposable
{
    public DbContextOptions<AppDbContext> Opcoes { get; } =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private AppDbContext? _context;

    public AppDbContext Context => _context ??= new AppDbContext(Opcoes);

    public void Dispose() => _context?.Dispose();
}
