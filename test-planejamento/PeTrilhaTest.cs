using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Resolução da trilha efetiva de um órgão (GET modelo/trilha): nível do órgão (ou o
/// padrão), ajustes por órgão, travado que nunca desliga, numeração pela posição entre
/// os passos visíveis, etapa sem passo visível que some, seção e campo que só aparecem
/// quando o pai aparece, e campo de ligação que some com a seção que ele liga.
/// </summary>
public class PeTrilhaTest : PeModeloTestBase
{
    // ── Nível ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrgaoSemNivel_UsaOPadrao_OPrimeiroAtivo_Basico()
    {
        var trilha = await TrilhaAsync(OrgaoSes);

        Assert.Equal(OrgaoSes.Id, trilha.OrgaoId);
        Assert.Equal("SES", trilha.OrgaoSigla);
        Assert.Equal("Básico", trilha.NivelNome);
        Assert.True(trilha.NivelPadrao);
        Assert.True(trilha.NivelAtivo);
        Assert.Equal(23, PassosDa(trilha).Count);
    }

    [Fact]
    public async Task Basico_EtapaSemPassoVisivelSome_ENumeracaoEhPelaPosicao()
    {
        var trilha = await TrilhaAsync(OrgaoSes);

        // A avaliação intermediária não tem passo no Básico: "Feche o ciclo" vira a etapa 6
        Assert.Equal(new[] { "preparacao", "diagnostico", "planejamento", "plano-acompanhamento", "monitoramento", "fechamento" },
            trilha.Etapas.Select(e => e.Chave));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, trilha.Etapas.Select(e => e.Numero));
        Assert.Equal(new[] { 7, 4, 8, 1, 1, 2 }, trilha.Etapas.Select(e => e.Passos.Count));

        // Sem documentos de referência, as estratégias são o 1.6 (no Avançado, 1.7)
        Assert.Equal("1.6", NaTrilha(trilha, "preparacao.estrategias")!.Numero);
        Assert.Equal("2.1", NaTrilha(trilha, "diagnostico.ambiente-tecnologico")!.Numero);
        Assert.Equal("2.3", NaTrilha(trilha, "diagnostico.necessidades-tic")!.Numero);
        Assert.Equal("3.1", NaTrilha(trilha, "planejamento.priorizacao")!.Numero);
        Assert.Equal("6.2", NaTrilha(trilha, "fechamento.aprovacao-autoridade")!.Numero);
        Assert.Null(NaTrilha(trilha, "preparacao.documentos-referencia"));
    }

    [Fact]
    public async Task Intermediario_QuarentaPassos_SeteEtapas()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");

        var trilha = await TrilhaAsync(OrgaoSes);

        Assert.Equal("Intermediário", trilha.NivelNome);
        Assert.False(trilha.NivelPadrao);
        Assert.Equal(new[] { 8, 9, 12, 2, 2, 3, 4 }, trilha.Etapas.Select(e => e.Passos.Count));
        Assert.Equal(40, PassosDa(trilha).Count);
    }

    [Fact]
    public async Task Avancado_TodosOsCinquentaPassos_ComANumeracaoDoPlano()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");

        var passos = PassosDa(await TrilhaAsync(OrgaoSes));

        Assert.Equal(PeCarregadorModeloTest.TrilhaDoPlano.Select(t => t.Chave), passos.Select(p => p.Chave));
        // A numeração da seção 7 do plano é a do Avançado
        var esperada = PeCarregadorModeloTest.TrilhaDoPlano
            .GroupBy(t => t.Chave.Split('.')[0])
            .SelectMany((g, e) => g.Select((t, p) => $"{e + 1}.{p + 1}"));
        Assert.Equal(esperada, passos.Select(p => p.Numero));
        Assert.Equal("1.7", passos.Single(p => p.Chave == "preparacao.estrategias").Numero);
        Assert.Equal("3.13", passos.Single(p => p.Chave == "planejamento.publicacao").Numero);
    }

    [Fact]
    public async Task Passo_TrazOQueFazer_BaseLegal_Guia_Tipo_Situacao()
    {
        var passo = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.ativos")!;

        Assert.Equal("Liste as soluções e os ativos de TIC", passo.Titulo);
        Assert.Equal("art. 12, § 2º, II", passo.BaseLegal);
        Assert.Equal("Anexo X, item 19", passo.ReferenciaGuia);
        Assert.Equal("dados", passo.Tipo);
        Assert.Equal("II", passo.IncisoDecreto);
        Assert.True(passo.Travado);
        Assert.Equal("obrigatorio", passo.Situacao);
        Assert.False(passo.AjustadoParaOrgao);
        Assert.False(string.IsNullOrWhiteSpace(passo.OQueFazer));
    }

    [Fact]
    public async Task NivelDesativado_OOrgaoContinuaNele()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        await Modelo.AtualizarNivelAsync(NivelId("intermediario"),
            new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } }, EmailAdmin);

        var trilha = await TrilhaAsync(OrgaoSes);

        Assert.Equal("Intermediário", trilha.NivelNome);
        Assert.False(trilha.NivelAtivo);
        Assert.False(trilha.NivelPadrao);
        Assert.Equal(40, PassosDa(trilha).Count);

        // Quem não tem nível escolhido segue no padrão (o primeiro ativo)
        Assert.Equal("Básico", (await TrilhaAsync(OrgaoSeec)).NivelNome);
    }

    // ── Ajustes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ajuste_LigaPassoDesligadoNoNivel_ERenumera()
    {
        var plano = Passo("preparacao.plano-trabalho");
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "passo", AlvoId = plano.Id, Situacao = "opcional", Justificativa = "O órgão quer o plano de trabalho" }
        }, EmailAdmin);

        var trilha = await TrilhaAsync(OrgaoSes);
        var passo = NaTrilha(trilha, "preparacao.plano-trabalho")!;

        Assert.Equal("1.8", passo.Numero);
        Assert.Equal("opcional", passo.Situacao);
        Assert.True(passo.AjustadoParaOrgao);
        // O ajuste é só deste órgão
        Assert.Null(NaTrilha(await TrilhaAsync(OrgaoSeec), "preparacao.plano-trabalho"));
    }

    [Fact]
    public async Task Ajuste_DesligaPassoLigado_EOsSeguintesSobem()
    {
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "passo", AlvoId = Passo("preparacao.nomes").Id, Situacao = "desligado" }
        }, EmailAdmin);

        var trilha = await TrilhaAsync(OrgaoSes);

        Assert.Null(NaTrilha(trilha, "preparacao.nomes"));
        Assert.Equal("1.2", NaTrilha(trilha, "preparacao.sgtic")!.Numero);
    }

    [Fact]
    public async Task Travado_NuncaDesliga_NemComAjusteGravadoDireto_NemEmNivelSemLinha()
    {
        // Ajuste "desligado" gravado por fora do serviço (o serviço recusa)
        Context.PeOrgaosAjuste.Add(new PeOrgaoAjuste
        {
            OrgaoId = OrgaoSes.Id, AlvoTipo = "passo", AlvoId = Passo("diagnostico.ativos").Id, Situacao = "desligado",
            CriadoEm = DateTime.UtcNow, CriadoPor = "teste"
        });
        // Nível sem nenhuma linha de situação
        var vazio = new PeNivel { Codigo = "vazio", Nome = "Vazio", Ordem = 9, Ativo = true, CriadoEm = DateTime.UtcNow, CriadoPor = "teste" };
        Context.PeNiveis.Add(vazio);
        await Context.SaveChangesAsync();

        var ativos = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.ativos")!;
        Assert.Equal("obrigatorio", ativos.Situacao);

        await Orgaos.DefinirNivelAsync(OrgaoSeec.Id, new PeOrgaoNivelDTO { NivelId = vazio.Id, Justificativa = "Teste" }, EmailAdmin);
        var trilha = await TrilhaAsync(OrgaoSeec);
        var passos = PassosDa(trilha);

        // Só os sete passos travados, obrigatórios, com as seções e os campos travados
        Assert.Equal(7, passos.Count);
        Assert.All(passos, p => Assert.True(p.Travado));
        Assert.All(passos, p => Assert.Equal("obrigatorio", p.Situacao));
        var acoes = passos.Single(p => p.Chave == "planejamento.metas-acoes").Secoes.Single(s => s.Chave == "acoes");
        Assert.Equal(new[] { "descricao", "tema" }, acoes.Campos.Select(c => c.Chave));
    }

    // ── Seções e campos ───────────────────────────────────────────────────────

    [Fact]
    public async Task SecaoDesligada_NaoAparece_NemOsCamposDela()
    {
        var basico = NaTrilha(await TrilhaAsync(OrgaoSes), "preparacao.equipe")!;
        Assert.Equal(new[] { "equipe_elaboracao" }, basico.Secoes.Select(s => s.Chave));
        Assert.Equal(new[] { "nome" }, basico.Secoes.Single().Campos.Select(c => c.Chave));

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var avancado = NaTrilha(await TrilhaAsync(OrgaoSes), "preparacao.equipe")!;
        Assert.Equal(new[] { "equipe_elaboracao", "equipe_designacao" }, avancado.Secoes.Select(s => s.Chave));
        Assert.Equal(5, avancado.Secoes[0].Campos.Count);
    }

    [Fact]
    public async Task SecaoDesligadaPorAjuste_LevaOsCampos_EOPassoFica()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "secao", AlvoId = Secao("quadro_pessoal_tic").Id, Situacao = "desligado" }
        }, EmailAdmin);

        var passo = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.ambiente-tecnologico")!;

        Assert.Equal(new[] { "diagnostico_ambiente" }, passo.Secoes.Select(s => s.Chave));
    }

    [Fact]
    public async Task CampoDoNivel_AparecePelaSituacao_ComObrigatorio()
    {
        var basico = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Secoes.Single();
        Assert.DoesNotContain(basico.Campos, c => c.Chave == "gravidade");
        Assert.False(basico.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        Assert.True(basico.Campos.Single(c => c.Chave == "prioridade_simples").Obrigatorio);
        Assert.True(basico.Campos.Single(c => c.Chave == "descricao").Principal);
        Assert.True(basico.Travada);

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var intermediario = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Secoes.Single();
        // Sem PETIC-DF vigente, a ligação com ele fica opcional também na trilha (E4); com a
        // vigente, volta a ser obrigatória (PeTrilhaPeticTest)
        Assert.False(intermediario.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        Assert.True(intermediario.Campos.Single(c => c.Chave == "gravidade").Obrigatorio);
        Assert.DoesNotContain(intermediario.Campos, c => c.Chave == "prioridade_simples");
    }

    [Fact]
    public async Task CampoDeLigacao_SomeQuandoASecaoLigadaNaoAparece()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var antes = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Secoes.Single();
        Assert.Contains(antes.Campos, c => c.Chave == "fraqueza");

        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "secao", AlvoId = Secao("swot_fraquezas").Id, Situacao = "desligado" }
        }, EmailAdmin);

        var depois = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Secoes.Single();
        Assert.DoesNotContain(depois.Campos, c => c.Chave == "fraqueza");
        Assert.Contains(depois.Campos, c => c.Chave == "estrategia");
    }

    [Fact]
    public async Task Campo_TrazConfigComoObjeto_EOpcoesAtivasNaOrdem()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        await Modelo.AtualizarOpcaoAsync(Opcao("necessidades", "tipo", "pessoal").Id,
            new PeOpcaoAtualizarDTO { Ativa = false, Informados = new HashSet<string> { "Ativa" } }, EmailAdmin);

        var secao = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Secoes.Single();

        var prioridade = secao.Campos.Single(c => c.Chave == "prioridade");
        Assert.Equal(JsonValueKind.Object, prioridade.Config.ValueKind);
        Assert.Equal("produto", prioridade.Config.GetProperty("calculo").GetString());
        Assert.False(prioridade.Obrigatorio);

        Assert.Equal(new[] { "servico", "infraestrutura", "contratacao", "outro" },
            secao.Campos.Single(c => c.Chave == "tipo").Opcoes.Select(o => o.Valor));
        var gravidade = secao.Campos.Single(c => c.Chave == "gravidade");
        Assert.Equal("numero", gravidade.Tipo);
        Assert.Equal(5, gravidade.Config.GetProperty("max").GetInt32());
        Assert.Empty(gravidade.Opcoes);
    }

    [Fact]
    public async Task PassoApagado_SaiDaTrilha()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var criado = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId,
            Titulo = "Registre a carteira de projetos",
            OQueFazer = "Liste os projetos em andamento."
        }, EmailAdmin);

        var comNovo = await TrilhaAsync(OrgaoSes);
        var novo = NaTrilha(comNovo, criado.Chave)!;
        Assert.Equal("1.11", novo.Numero);
        Assert.Equal("opcional", novo.Situacao);

        await Modelo.ExcluirPassoAsync(criado.Id, EmailAdmin);
        Assert.Null(NaTrilha(await TrilhaAsync(OrgaoSes), criado.Chave));
    }

    // ── Erros ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrgaoDesativadoOuInexistente_404()
    {
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(() => Modelo.TrilhaAsync(999_999)));

        var orgao = Context.PgiaOrgaos.Single(o => o.Id == OrgaoSeec.Id);
        orgao.Ativo = false;
        await Context.SaveChangesAsync();
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(() => Modelo.TrilhaAsync(OrgaoSeec.Id)));
    }
}

/// <summary>Trilha antes de o modelo inicial ser carregado (tabelas vazias).</summary>
public class PeTrilhaSemModeloTest : PeModeloTestBase
{
    public PeTrilhaSemModeloTest() : base(carregar: false) { }

    [Fact]
    public async Task SemNivelAlgum_409ModeloIndisponivel()
    {
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), await ErroAsync(() => Modelo.TrilhaAsync(OrgaoSes.Id)));
    }

    [Fact]
    public async Task ListaDeOrgaos_SemNivel_VemSemNivel()
    {
        var lista = await Orgaos.ListarAsync(new PeOrgaosConsulta());

        Assert.All(lista, o => Assert.Null(o.NivelId));
        Assert.All(lista, o => Assert.True(o.NivelPadrao));
    }
}
