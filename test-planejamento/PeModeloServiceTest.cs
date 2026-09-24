using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Regras da escrita do modelo (PeModeloService): travado não desliga, item do sistema
/// não muda de tipo nem de chave e não é apagado, chave única, opção do sistema ou em uso
/// só é desativada, campo em cálculo e seção ligada ficam protegidos, config conferido,
/// criação com os padrões do contrato, ordem e histórico com antes e depois.
/// </summary>
public class PeModeloServiceTest : PeModeloTestBase
{
    private static HashSet<string> Inf(params string[] nomes) => nomes.ToHashSet();

    // ── Travado ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PassoTravado_NaoDesliga_MasFicaOpcionalOuObrigatorio()
    {
        var ativos = Passo("diagnostico.ativos").Id;

        Assert.Equal(Codigo(ErrorCode.PeItemTravado),
            await ErroAsync(() => Modelo.DefinirSituacaoPassoAsync(ativos, So("basico", "desligado"), EmailAdmin)));
        Assert.Equal("o o o", SituacoesDoPasso("diagnostico.ativos"));

        await Modelo.DefinirSituacaoPassoAsync(ativos, So("basico", "opcional"), EmailAdmin);
        Assert.Equal("p o o", SituacoesDoPasso("diagnostico.ativos"));
    }

    [Fact]
    public async Task SecaoTravada_CampoPrincipalDela_ECampoTravado_NaoDesligam()
    {
        Assert.Equal(Codigo(ErrorCode.PeItemTravado),
            await ErroAsync(() => Modelo.DefinirSituacaoSecaoAsync(Secao("metas").Id, Todos("desligado"), EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado),
            await ErroAsync(() => Modelo.DefinirSituacaoCampoAsync(Campo("metas", "descricao").Id, So("avancado", "desligado"), EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado),
            await ErroAsync(() => Modelo.DefinirSituacaoCampoAsync(Campo("acoes", "tema").Id, So("basico", "desligado"), EmailAdmin)));

        // Os outros campos de dentro podem ser simplificados
        await Modelo.DefinirSituacaoCampoAsync(Campo("metas", "indicador").Id, So("basico", "desligado"), EmailAdmin);
        Assert.Equal("d o o", SituacoesDoCampo("metas", "indicador"));
    }

    [Fact]
    public async Task OpcaoTravada_NaoDesativaNemApaga_MasPodeSerRenomeada()
    {
        var seguranca = Opcao("acoes", "tema", "seguranca").Id;

        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Modelo.AtualizarOpcaoAsync(seguranca,
            new PeOpcaoAtualizarDTO { Ativa = false, Informados = Inf("Ativa") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Modelo.ExcluirOpcaoAsync(seguranca, EmailAdmin)));

        var renomeada = await Modelo.AtualizarOpcaoAsync(seguranca,
            new PeOpcaoAtualizarDTO { Rotulo = "Segurança da informação (inciso V)", Informados = Inf("Rotulo") }, EmailAdmin);
        Assert.Equal("Segurança da informação (inciso V)", renomeada.Rotulo);
        Assert.True(renomeada.Ativa);
    }

    [Fact]
    public async Task PassoTravado_NaoAceitaNaoSeAplica()
    {
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Modelo.AtualizarPassoAsync(Passo("diagnostico.ativos").Id,
            new PePassoAtualizarDTO { AceitaNaoSeAplica = true, Informados = Inf("AceitaNaoSeAplica") }, EmailAdmin)));
    }

    // ── Item do sistema ───────────────────────────────────────────────────────

    [Fact]
    public async Task ItemDoSistema_NaoMudaTipoNemChave_ENaoEhApagado()
    {
        var passo = Passo("preparacao.nomes").Id;
        var secao = Secao("nomes").Id;
        var campo = Campo("nomes", "comite").Id;

        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.AtualizarPassoAsync(passo,
            new PePassoAtualizarDTO { Tipo = "fluxo", Informados = Inf("Tipo") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.AtualizarPassoAsync(passo,
            new PePassoAtualizarDTO { Chave = "preparacao.outro", Informados = Inf("Chave") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.ExcluirPassoAsync(passo, EmailAdmin)));

        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.AtualizarSecaoAsync(secao,
            new PeSecaoAtualizarDTO { Tipo = "tabela", Informados = Inf("Tipo") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.ExcluirSecaoAsync(secao, EmailAdmin)));

        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.AtualizarCampoAsync(campo,
            new PeCampoAtualizarDTO { Tipo = "texto_longo", Informados = Inf("Tipo") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.AtualizarCampoAsync(campo,
            new PeCampoAtualizarDTO { Chave = "comite_nome", Informados = Inf("Chave") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemDoSistema), await ErroAsync(() => Modelo.ExcluirCampoAsync(campo, EmailAdmin)));

        // Mandar o mesmo tipo e a mesma chave não é mudança
        var mesmo = await Modelo.AtualizarCampoAsync(campo,
            new PeCampoAtualizarDTO { Tipo = "texto_curto", Chave = "comite", Rotulo = "Nome do comitê", Informados = Inf("Tipo", "Chave", "Rotulo") },
            EmailAdmin);
        Assert.Equal("Nome do comitê", mesmo.Rotulo);
    }

    [Fact]
    public async Task ItemDoSistema_RenomeiaGanhaAjudaEMudaDeSituacao_ComHistorico()
    {
        var id = Passo("preparacao.nomes").Id;

        var atualizado = await Modelo.AtualizarPassoAsync(id, new PePassoAtualizarDTO
        {
            Titulo = "Defina os nomes que o órgão usa",
            BaseLegal = "art. 8º",
            Informados = Inf("Titulo", "BaseLegal")
        }, EmailAdmin);
        await Modelo.DefinirSituacaoPassoAsync(id, So("basico", "opcional"), EmailAdmin);

        Assert.Equal("Defina os nomes que o órgão usa", atualizado.Titulo);
        Assert.Equal("art. 8º", atualizado.BaseLegal);
        var historico = HistoricoDe("passo", id);
        Assert.Equal(new[] { "alteracao", "situacao" }, historico.Select(h => h.Acao));
        Assert.All(historico, h => Assert.Equal(EmailAdmin, h.AlteradoPor));
        Assert.Contains("Defina os nomes do órgão", historico[0].Antes);
        Assert.Contains("Defina os nomes que o órgão usa", historico[0].Depois);
        Assert.Contains("\"obrigatorio\"", historico[1].Antes);
        Assert.Contains("\"opcional\"", historico[1].Depois);
    }

    // ── PUT parcial ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_CampoAusenteNaoMuda_NuloLimpaOOpcional_VazioRecusaOObrigatorio()
    {
        var id = Passo("preparacao.sgtic").Id;

        var soTitulo = await Modelo.AtualizarPassoAsync(id,
            new PePassoAtualizarDTO { Titulo = "Registre o SGTIC", Informados = Inf("Titulo") }, EmailAdmin);
        Assert.Equal("art. 8º, §§ 1º e 2º", soTitulo.BaseLegal);

        var limpa = await Modelo.AtualizarPassoAsync(id,
            new PePassoAtualizarDTO { BaseLegal = null, Informados = Inf("BaseLegal") }, EmailAdmin);
        Assert.Null(limpa.BaseLegal);
        Assert.Equal("Registre o SGTIC", limpa.Titulo);

        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.AtualizarPassoAsync(id,
            new PePassoAtualizarDTO { Titulo = "  ", Informados = Inf("Titulo") }, EmailAdmin)));
    }

    [Fact]
    public async Task Put_SemMudancaDeFato_NaoGravaHistorico()
    {
        var id = Passo("preparacao.sgtic").Id;
        await Modelo.AtualizarPassoAsync(id, new PePassoAtualizarDTO { Titulo = Passo("preparacao.sgtic").Titulo, Informados = Inf("Titulo") },
            EmailAdmin);

        Assert.Empty(HistoricoDe("passo", id));
    }

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CriarPasso_TipoDados_NasceOpcionalNoNivelMaisAlto_EChaveDoTitulo()
    {
        var criado = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId,
            Titulo = "Faça o levantamento de soluções",
            OQueFazer = "Liste as soluções."
        }, EmailAdmin);

        Assert.Equal("preparacao.faca-o-levantamento-de-solucoes", criado.Chave);
        Assert.Equal("dados", criado.Tipo);
        Assert.False(criado.Sistema);
        Assert.True(criado.AceitaNaoSeAplica);
        Assert.Equal(11, criado.Ordem);
        Assert.Equal("d d p", SituacoesDoPasso(criado.Chave));
        Assert.Equal("criacao", Assert.Single(HistoricoDe("passo", criado.Id)).Acao);

        // Mesmo título de novo: chave com sufixo
        var outro = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId, Titulo = "Faça o levantamento de soluções", OQueFazer = "Outra vez."
        }, EmailAdmin);
        Assert.Equal("preparacao.faca-o-levantamento-de-solucoes-2", outro.Chave);
    }

    [Fact]
    public async Task CriarSecaoECampo_NascemDesligados_EChaveSemAcento()
    {
        var secao = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO
        {
            PassoId = Passo("preparacao.metodologia").Id, Titulo = "Técnicas usadas", Tipo = "tabela", PrefixoCodigo = "tu"
        }, EmailAdmin);
        Assert.Equal("tecnicas_usadas", secao.Chave);
        Assert.Equal("TU", secao.PrefixoCodigo);
        Assert.Equal("pdtic", secao.Escopo);
        Assert.All(secao.Niveis.Values, s => Assert.Equal("desligado", s));

        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao.Id, Rotulo = "Técnica", Tipo = "texto_curto", Config = Corpo(new { max = 120 })
        }, EmailAdmin);
        Assert.Equal("tecnica", campo.Chave);
        Assert.Equal(120, campo.Config.GetProperty("max").GetInt32());
        Assert.False(campo.Principal);
        Assert.All(campo.Niveis.Values, s => Assert.Equal("desligado", s));
    }

    [Fact]
    public async Task CriarSecaoForaDoPdtic_UsaASituacaoGeral()
    {
        var secao = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO { Escopo = "petic", Titulo = "Diretrizes", Tipo = "tabela" }, EmailAdmin);

        Assert.Null(secao.PassoId);
        Assert.Equal("desligado", secao.SituacaoGeral);
        Assert.Empty(secao.Niveis);

        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos),
            await ErroAsync(() => Modelo.DefinirSituacaoSecaoAsync(secao.Id, Todos("obrigatorio"), EmailAdmin)));
        var ligada = await Modelo.DefinirSituacaoSecaoAsync(secao.Id, new PeSituacoesDTO { SituacaoGeral = "obrigatorio" }, EmailAdmin);
        Assert.Equal("obrigatorio", ligada.SituacaoGeral);

        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao.Id, Rotulo = "Diretriz", Tipo = "texto_longo" }, EmailAdmin);
        Assert.Equal("desligado", campo.SituacaoGeral);
        var modelo = await Modelo.ObterModeloAsync(false);
        Assert.Contains(modelo.SecoesForaDoPdtic, s => s.Id == secao.Id && s.Campos.Any(c => c.Id == campo.Id));
    }

    [Fact]
    public async Task ChaveUnica_EmCadaNivelDoModelo()
    {
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Chave = "comite", Rotulo = "Outro", Tipo = "texto_curto" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarSecaoAsync(
            new PeSecaoCriarDTO { PassoId = Passo("preparacao.nomes").Id, Chave = "nomes", Titulo = "Outra", Tipo = "formulario" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarNivelAsync(
            new PeNivelCriarDTO { Nome = "Outro", Codigo = "basico" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarOpcaoAsync(
            Campo("ativos", "tipo").Id, new PeOpcaoCriarDTO { Valor = "rede", Rotulo = "Rede" }, EmailAdmin)));

        // A chave de um campo apagado continua ocupada (os dados dele ficam no jsonb)
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Rotulo = "Apelido", Tipo = "texto_curto" }, EmailAdmin);
        await Modelo.ExcluirCampoAsync(campo.Id, EmailAdmin);
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Chave = "apelido", Rotulo = "Apelido", Tipo = "texto_curto" }, EmailAdmin)));
        var gerada = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Rotulo = "Apelido", Tipo = "texto_curto" }, EmailAdmin);
        Assert.Equal("apelido_2", gerada.Chave);
    }

    [Fact]
    public async Task ChaveInformadaForaDoFormato_400()
    {
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Chave = "Com Espaço", Rotulo = "X", Tipo = "texto_curto" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Rotulo = "X", Tipo = "cor" }, EmailAdmin)));
    }

    // ── Exclusão lógica e "desligar e religar não perde nada" ─────────────────

    [Fact]
    public async Task ExcluirPassoCriado_ExclusaoLogica_OsFilhosFicamGuardados()
    {
        var passo = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId, Titulo = "Liste os parceiros", OQueFazer = "Liste."
        }, EmailAdmin);
        var secao = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO { PassoId = passo.Id, Titulo = "Parceiros", Tipo = "tabela" }, EmailAdmin);
        await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao.Id, Rotulo = "Nome", Tipo = "texto_curto" }, EmailAdmin);

        var excluido = await Modelo.ExcluirPassoAsync(passo.Id, EmailAdmin);

        Assert.True(excluido.Excluido);
        Assert.NotNull(Context.PePassos.AsNoTracking().Single(p => p.Id == passo.Id).ExcluidoEm);
        Assert.Single(Context.PeCampos.AsNoTracking().Where(c => c.SecaoId == secao.Id));
        Assert.DoesNotContain((await Modelo.ObterModeloAsync(false)).Etapas.SelectMany(e => e.Passos), p => p.Id == passo.Id);
        var comExcluidos = (await Modelo.ObterModeloAsync(true)).Etapas.SelectMany(e => e.Passos).Single(p => p.Id == passo.Id);
        Assert.True(comExcluidos.Excluido);
        Assert.Equal("exclusao", HistoricoDe("passo", passo.Id).Last().Acao);

        // Apagado não é editado
        Assert.Equal(Codigo(ErrorCode.PeItemExcluido), await ErroAsync(() => Modelo.AtualizarPassoAsync(passo.Id,
            new PePassoAtualizarDTO { Titulo = "Outro", Informados = Inf("Titulo") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemExcluido), await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = secao.Id, Rotulo = "Outro", Tipo = "texto_curto" }, EmailAdmin)));
    }

    [Fact]
    public async Task DesligarEReligar_NaoPerdeNada()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var id = Passo("diagnostico.swot").Id;
        var antes = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.swot")!;

        await Modelo.DefinirSituacaoPassoAsync(id, Todos("desligado"), EmailAdmin);
        Assert.Null(NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.swot"));
        Assert.Equal(4, Context.PeSecoes.Count(s => s.PassoId == id));

        await Modelo.DefinirSituacaoPassoAsync(id, Em("desligado", "obrigatorio", "obrigatorio"), EmailAdmin);
        var depois = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.swot")!;
        Assert.Equal(JsonSerializer.Serialize(antes), JsonSerializer.Serialize(depois));
    }

    // ── Opções ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExcluirOpcao_DoSistemaSoDesativa_CriadaENuncaUsadaApaga()
    {
        var doSistema = await Modelo.ExcluirOpcaoAsync(Opcao("ativos", "tipo", "licenca").Id, EmailAdmin);
        Assert.NotNull(doSistema);
        Assert.False(doSistema!.Ativa);
        Assert.False(Opcao("ativos", "tipo", "licenca").Ativa);

        var criada = await Modelo.CriarOpcaoAsync(Campo("ativos", "tipo").Id, new PeOpcaoCriarDTO { Rotulo = "Plataforma", Cor = "roxo" }, EmailAdmin);
        Assert.Equal("plataforma", criada.Valor);
        Assert.Null(await Modelo.ExcluirOpcaoAsync(criada.Id, EmailAdmin));
        Assert.Empty(Context.PeOpcoes.AsNoTracking().Where(o => o.Id == criada.Id));
        Assert.Equal(new[] { "criacao", "remocao" }, HistoricoDe("opcao", criada.Id).Select(h => h.Acao));
    }

    [Fact]
    public async Task ExcluirOpcao_EmUsoNaMatrizDoRisco_SoDesativa()
    {
        var probabilidade = Campo("riscos", "probabilidade").Id;
        var nova = await Modelo.CriarOpcaoAsync(probabilidade, new PeOpcaoCriarDTO { Valor = "muito_alta", Rotulo = "Muito alta" }, EmailAdmin);
        var nivel = Campo("riscos", "nivel");
        var matriz = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
            JsonDocument.Parse(nivel.Config).RootElement.GetProperty("matriz").GetRawText())!;
        matriz["muito_alta"] = new() { ["baixo"] = "medio", ["medio"] = "alto", ["alto"] = "alto" };
        await Modelo.AtualizarCampoAsync(nivel.Id, new PeCampoAtualizarDTO
        {
            Config = Corpo(new { calculo = "nivel_risco", campos = new[] { "probabilidade", "impacto" }, matriz }),
            Informados = Inf("Config")
        }, EmailAdmin);

        var resultado = await Modelo.ExcluirOpcaoAsync(nova.Id, EmailAdmin);

        Assert.NotNull(resultado);
        Assert.False(resultado!.Ativa);
    }

    [Fact]
    public async Task Opcao_DeListaNoCalculo_PrecisaDeValorNumerico()
    {
        // Critério em lista (escala com rótulos) que entra na soma ponderada da prioridade
        var secao = Secao("necessidades").Id;
        var alcance = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao, Rotulo = "Alcance", Tipo = "lista" }, EmailAdmin);
        await Modelo.CriarOpcaoAsync(alcance.Id, new PeOpcaoCriarDTO { Valor = "1", Rotulo = "Uma área" }, EmailAdmin);
        await Modelo.AtualizarCampoAsync(Campo("necessidades", "prioridade").Id, new PeCampoAtualizarDTO
        {
            Config = Corpo(new
            {
                calculo = "soma_ponderada",
                campos = new[] { "gravidade", "urgencia", "tendencia", "alcance" },
                pesos = new { gravidade = 1, urgencia = 1, tendencia = 1, alcance = 2 }
            }),
            Informados = Inf("Config")
        }, EmailAdmin);

        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarOpcaoAsync(alcance.Id,
            new PeOpcaoCriarDTO { Rotulo = "Todo o órgão" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarOpcaoAsync(alcance.Id,
            new PeOpcaoCriarDTO { Valor = "todo", Rotulo = "Todo o órgão" }, EmailAdmin)));

        var tres = await Modelo.CriarOpcaoAsync(alcance.Id, new PeOpcaoCriarDTO { Valor = "3", Rotulo = "Todo o órgão" }, EmailAdmin);
        Assert.Equal("3", tres.Valor);

        // Lista com opção não numérica não entra no cálculo
        var nomes = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao, Rotulo = "Porte", Tipo = "lista" }, EmailAdmin);
        await Modelo.CriarOpcaoAsync(nomes.Id, new PeOpcaoCriarDTO { Rotulo = "Grande" }, EmailAdmin);
        Assert.Equal(Codigo(ErrorCode.PeConfigInvalida), await ErroAsync(() => Modelo.AtualizarCampoAsync(Campo("necessidades", "prioridade").Id,
            new PeCampoAtualizarDTO
            {
                Config = Corpo(new { calculo = "produto", campos = new[] { "gravidade", "porte" } }),
                Informados = Inf("Config")
            }, EmailAdmin)));
    }

    [Fact]
    public async Task CriarNivel_CodigoDoFront_EhNormalizado()
    {
        var nivel = await Modelo.CriarNivelAsync(new PeNivelCriarDTO { Nome = "Piloto dois", Codigo = "Piloto-Dois" }, EmailAdmin);

        Assert.Equal("piloto_dois", nivel.Codigo);
        Assert.Equal(Codigo(ErrorCode.PeChaveDuplicada), await ErroAsync(() => Modelo.CriarNivelAsync(
            new PeNivelCriarDTO { Nome = "Piloto 2", Codigo = "piloto_dois" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarNivelAsync(
            new PeNivelCriarDTO { Nome = "Dois", Codigo = "2" }, EmailAdmin)));
    }

    [Fact]
    public async Task Etapa_OrdemAtualReenviada_NaoMexe()
    {
        var etapa = Context.PeEtapas.AsNoTracking().Single(e => e.Chave == "diagnostico");

        var atualizada = await Modelo.AtualizarEtapaAsync(etapa.Id,
            new PeEtapaAtualizarDTO { Titulo = "Faça o diagnóstico da TIC", Descricao = etapa.Descricao, Ordem = etapa.Ordem }, EmailAdmin);

        Assert.Equal(2, atualizada.Ordem);
        Assert.Equal(new[] { "alteracao" }, HistoricoDe("etapa", etapa.Id).Select(h => h.Acao));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, Context.PeEtapas.AsNoTracking().OrderBy(e => e.Ordem).Select(e => e.Ordem));
    }

    [Fact]
    public async Task Opcao_SoEmCampoDeLista()
    {
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.CriarOpcaoAsync(Campo("nomes", "comite").Id,
            new PeOpcaoCriarDTO { Rotulo = "X" }, EmailAdmin)));
    }

    // ── Config e uso ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Config_Invalido_400()
    {
        var secao = Secao("necessidades").Id;

        async Task<int> Criar(string tipo, object config) => await ErroAsync(() => Modelo.CriarCampoAsync(
            new PeCampoCriarDTO { SecaoId = secao, Rotulo = "Teste", Tipo = tipo, Config = Corpo(config) }, EmailAdmin));

        var invalido = Codigo(ErrorCode.PeConfigInvalida);
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "formula", campos = new[] { "gravidade" } }));
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "produto", campos = new[] { "gravidade", "nao_existe" } }));
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "produto", campos = new[] { "gravidade", "descricao" } }));
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "soma_ponderada", campos = new[] { "gravidade", "urgencia" } }));
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "subtracao", campos = new[] { "gravidade" } }));
        Assert.Equal(invalido, await Criar("calculado", new { calculo = "produto", campos = new[] { "prioridade", "gravidade" } }));
        Assert.Equal(invalido, await Criar("ligacao_secao", new { secao = "nomes" }));
        Assert.Equal(invalido, await Criar("ligacao_secao", new { secao = "nao_existe" }));
        Assert.Equal(invalido, await Criar("ligacao_catalogo", new { catalogo = "pessoas" }));
        Assert.Equal(invalido, await Criar("arquivo", new { tipos = new[] { "exe" } }));
        Assert.Equal(invalido, await Criar("numero", new { min = 5, max = 1 }));
        Assert.Equal(invalido, await Criar("data", new { formato = "dd/mm" }));
    }

    [Fact]
    public async Task Config_ChaveComNulo_ContaComoAusente()
    {
        // O formulário do front manda todas as chaves do config, as que não usa com nulo
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = Secao("nomes").Id, Rotulo = "Apelido", Tipo = "texto_curto",
            Config = Corpo(new { max = 80, min = (int?)null, calculo = (string?)null, campos = (string[]?)null, pesos = (object?)null })
        }, EmailAdmin);

        Assert.Equal("{\"max\":80}", campo.Config.GetRawText());
    }

    [Fact]
    public async Task Config_SomaPonderada_ComPesos_Vale()
    {
        var prioridade = Campo("necessidades", "prioridade");
        var campo = await Modelo.AtualizarCampoAsync(prioridade.Id, new PeCampoAtualizarDTO
        {
            Config = Corpo(new
            {
                Calculo = "soma_ponderada",
                Campos = new[] { "gravidade", "urgencia", "tendencia" },
                Pesos = new { gravidade = 2, urgencia = 1.5, tendencia = 1 }
            }),
            Informados = Inf("Config")
        }, EmailAdmin);

        Assert.Equal("soma_ponderada", campo.Config.GetProperty("calculo").GetString());
        Assert.Equal(2m, campo.Config.GetProperty("pesos").GetProperty("gravidade").GetDecimal());
    }

    [Fact]
    public async Task CampoNoCalculo_NaoEhApagado_NemMudaDeChaveOuDeTipo()
    {
        // Campo criado pelo administrador que entra num cálculo criado por ele
        var secao = Secao("necessidades").Id;
        var peso = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao, Rotulo = "Alcance", Tipo = "numero" }, EmailAdmin);
        await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Nota", Tipo = "calculado",
            Config = Corpo(new { calculo = "produto", campos = new[] { "gravidade", "alcance" } })
        }, EmailAdmin);

        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.ExcluirCampoAsync(peso.Id, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(peso.Id,
            new PeCampoAtualizarDTO { Chave = "abrangencia", Informados = Inf("Chave") }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(peso.Id,
            new PeCampoAtualizarDTO { Tipo = "texto_curto", Informados = Inf("Tipo") }, EmailAdmin)));
    }

    [Fact]
    public async Task SecaoLigada_NaoEhApagada()
    {
        var alvo = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO { PassoId = Passo("preparacao.nomes").Id, Titulo = "Parceiros", Tipo = "tabela" }, EmailAdmin);
        await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = Secao("sgtic_membros").Id, Rotulo = "Parceiro", Tipo = "ligacao_secao", Config = Corpo(new { secao = alvo.Chave })
        }, EmailAdmin);

        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.ExcluirSecaoAsync(alvo.Id, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarSecaoAsync(alvo.Id,
            new PeSecaoAtualizarDTO { Tipo = "formulario", Informados = Inf("Tipo") }, EmailAdmin)));
    }

    [Fact]
    public async Task CampoCriado_MudaDeTipoComConfigNovo()
    {
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Rotulo = "Telefone", Tipo = "texto_curto" }, EmailAdmin);

        var numero = await Modelo.AtualizarCampoAsync(campo.Id, new PeCampoAtualizarDTO
        {
            Tipo = "numero", Config = Corpo(new { casas = 0 }), Informados = Inf("Tipo", "Config")
        }, EmailAdmin);
        Assert.Equal("numero", numero.Tipo);
        Assert.Equal(0, numero.Config.GetProperty("casas").GetInt32());

        // Tipo novo sem config: o atual é conferido para o tipo novo
        Assert.Equal(Codigo(ErrorCode.PeConfigInvalida), await ErroAsync(() => Modelo.AtualizarCampoAsync(campo.Id,
            new PeCampoAtualizarDTO { Tipo = "texto_rico", Informados = Inf("Tipo") }, EmailAdmin)));
    }

    // ── Ordem ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrdemDosPassos_ListaInteiraDaEtapa_ERenumeraATrilha()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var ids = Context.PePassos.AsNoTracking().Where(p => p.EtapaId == Passo("preparacao.nomes").EtapaId)
            .OrderBy(p => p.Ordem).Select(p => p.Id).ToList();

        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida), await ErroAsync(() => Modelo.OrdenarPassosAsync(
            new PeOrdemDTO { Ids = ids.Skip(1).ToList() }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida), await ErroAsync(() => Modelo.OrdenarPassosAsync(
            new PeOrdemDTO { Ids = ids.Append(ids[0]).ToList() }, EmailAdmin)));

        var invertida = Enumerable.Reverse(ids).ToList();
        var etapa = await Modelo.OrdenarPassosAsync(new PeOrdemDTO { Ids = invertida }, EmailAdmin);

        Assert.Equal(invertida, etapa.Passos.Select(p => p.Id));
        Assert.Equal("1.10", NaTrilha(await TrilhaAsync(OrgaoSes), "preparacao.abrangencia")!.Numero);
        Assert.Equal("ordem", HistoricoDe("passo", ids[0]).Single().Acao);
    }

    [Fact]
    public async Task OrdemDaEtapa_ComoPosicao()
    {
        var etapa = await Modelo.AtualizarEtapaAsync(Context.PeEtapas.Single(e => e.Chave == "fechamento").Id,
            new PeEtapaAtualizarDTO { Ordem = 1, Titulo = "Feche o ciclo do PDTIC", Informados = Inf("Ordem", "Titulo") }, EmailAdmin);

        Assert.Equal(1, etapa.Ordem);
        Assert.Equal("Feche o ciclo do PDTIC", etapa.Titulo);
        Assert.Equal(new[] { "fechamento", "preparacao", "diagnostico", "planejamento", "plano-acompanhamento", "monitoramento",
                "avaliacao-intermediaria" },
            Context.PeEtapas.AsNoTracking().OrderBy(e => e.Ordem).Select(e => e.Chave));
        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida), await ErroAsync(() => Modelo.AtualizarEtapaAsync(etapa.Id,
            new PeEtapaAtualizarDTO { Ordem = 9, Informados = Inf("Ordem") }, EmailAdmin)));
    }

    // ── Níveis ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CriarNivel_CopiandoDeOutro_TemAsMesmasSituacoes()
    {
        var novo = await Modelo.CriarNivelAsync(new PeNivelCriarDTO
        {
            Nome = "Intermediário com avaliação", CopiarDe = NivelId("intermediario")
        }, EmailAdmin);

        Assert.Equal("intermediario_com_avaliacao", novo.Codigo);
        Assert.Equal(4, novo.Ordem);
        var modelo = await Modelo.ObterModeloAsync(false);
        var passos = modelo.Etapas.SelectMany(e => e.Passos).ToList();
        Assert.All(passos, p => Assert.Equal(p.Niveis[NivelId("intermediario").ToString()], p.Niveis[novo.Id.ToString()]));
    }

    [Fact]
    public async Task CriarNivel_EmBranco_SoOsTravadosObrigatorios()
    {
        var novo = await Modelo.CriarNivelAsync(new PeNivelCriarDTO { Nome = "Piloto" }, EmailAdmin);

        var passos = (await Modelo.ObterModeloAsync(false)).Etapas.SelectMany(e => e.Passos).ToList();
        Assert.All(passos, p => Assert.Equal(p.Travado ? "obrigatorio" : "desligado", p.Niveis[novo.Id.ToString()]));
        var tema = passos.SelectMany(p => p.Secoes).Single(s => s.Chave == "acoes").Campos.Single(c => c.Chave == "tema");
        Assert.Equal("obrigatorio", tema.Niveis[novo.Id.ToString()]);
    }

    [Fact]
    public async Task Nivel_UltimoAtivoNaoDesativa_EmUsoDesativa()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        await Modelo.AtualizarNivelAsync(NivelId("basico"), new PeNivelAtualizarDTO { Ativo = false, Informados = Inf("Ativo") }, EmailAdmin);
        await Modelo.AtualizarNivelAsync(NivelId("intermediario"), new PeNivelAtualizarDTO { Ativo = false, Informados = Inf("Ativo") }, EmailAdmin);

        Assert.Equal(Codigo(ErrorCode.PeUltimoNivelAtivo), await ErroAsync(() => Modelo.AtualizarNivelAsync(NivelId("avancado"),
            new PeNivelAtualizarDTO { Ativo = false, Informados = Inf("Ativo") }, EmailAdmin)));

        var modelo = await Modelo.ObterModeloAsync(false);
        Assert.Equal(1, modelo.Niveis.Single(n => n.Codigo == "basico").Orgaos);
        Assert.False(modelo.Niveis.Single(n => n.Codigo == "basico").Ativo);
    }

    [Fact]
    public async Task OrdemDosNiveis_TodosOsNiveis()
    {
        var ids = new[] { NivelId("avancado"), NivelId("intermediario"), NivelId("basico") }.ToList();

        var niveis = await Modelo.OrdenarNiveisAsync(new PeOrdemDTO { Ids = ids }, EmailAdmin);

        Assert.Equal(new[] { "avancado", "intermediario", "basico" }, niveis.Select(n => n.Codigo));
        // O padrão passa a ser o primeiro ativo da nova ordem
        Assert.Equal("Avançado", (await TrilhaAsync(OrgaoSes)).NivelNome);
    }

    // ── Histórico ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Historico_FiltraPorEntidadeEId_DoMaisNovo_EPagina()
    {
        var id = Passo("preparacao.nomes").Id;
        for (var i = 1; i <= 3; i++)
            await Modelo.AtualizarPassoAsync(id, new PePassoAtualizarDTO { Titulo = $"Título {i}", Informados = Inf("Titulo") }, EmailAdmin);
        await Modelo.AtualizarNivelAsync(NivelId("basico"), new PeNivelAtualizarDTO { Nome = "Inicial", Informados = Inf("Nome") }, EmailAdmin);

        var doPasso = await Modelo.HistoricoAsync(new PeHistoricoConsulta { Entidade = "passo", EntidadeId = id, PageSize = 2 });
        Assert.Equal(3, doPasso.TotalItems);
        Assert.Equal(2, doPasso.Items.Count);
        Assert.Contains("Título 3", doPasso.Items[0].Depois!.Value.GetRawText());
        Assert.Equal(JsonValueKind.Object, doPasso.Items[0].Antes!.Value.ValueKind);

        Assert.Equal(4, (await Modelo.HistoricoAsync(new PeHistoricoConsulta())).TotalItems);
        Assert.Empty((await Modelo.HistoricoAsync(new PeHistoricoConsulta { Entidade = "pessoa" })).Items);
    }

    [Fact]
    public async Task GetModelo_NiveisComTodosOsNiveis_ConfigComoObjeto()
    {
        var modelo = await Modelo.ObterModeloAsync(false);

        Assert.Equal(3, modelo.Niveis.Count);
        Assert.Equal(7, modelo.Etapas.Count);
        // Desde a versão 2 do modelo inicial (E3): as 2 seções do catálogo do DF e as 8 do PETIC-DF
        Assert.Equal(new[] { "principio", "diretriz_ciclo", "petic_identidade", "petic_diretriz", "petic_objetivo_programa",
                "petic_objetivo", "petic_prioridade", "petic_indicador", "petic_iniciativa", "petic_eixo" },
            modelo.SecoesForaDoPdtic.Select(s => s.Chave));
        Assert.All(modelo.SecoesForaDoPdtic, s => Assert.Empty(s.Niveis));
        var passo = modelo.Etapas[0].Passos[0];
        Assert.Equal(3, passo.Niveis.Count);
        Assert.All(passo.Niveis.Keys, k => Assert.True(long.TryParse(k, out _)));
        var nivel = modelo.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == "riscos")
            .Campos.Single(c => c.Chave == "nivel");
        Assert.Equal(JsonValueKind.Object, nivel.Config.ValueKind);
        Assert.Equal(3, nivel.Opcoes.Count);
        Assert.True(modelo.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == "acoes")
            .Campos.Single(c => c.Chave == "descricao").Travado);
    }
}
