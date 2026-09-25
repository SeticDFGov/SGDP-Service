using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, partes C2 e C3: o passo a passo de um órgão no modo livre e a forma de cada passo. No modo
/// livre, todo órgão parte do nível base (o Básico): o passo ligado nele segue a situação dele ali,
/// os outros passos do guia aparecem opcionais, a forma do passo é a escolhida pelo órgão (senão a
/// do nível base, senão a do primeiro nível em que o passo está ligado), as formas oferecidas não se
/// repetem e o nível escolhido do órgão não vale (fica guardado). A escolha da forma
/// (PUT orgaos/{orgaoId}/passos/{passoId}/detalhe) confere o modo, o passo, a forma e o PDTIC atual.
/// </summary>
public class PeF3TrilhaLivreTest : PeAprovacaoTestBase
{
    public PeF3TrilhaLivreTest()
    {
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
    }

    private Task<PeTrilhaResponse> DefinirFormaAsync(string passo, string? nivel, PeUserContext? ctx = null) =>
        Orgaos.DefinirDetalheAsync(OrgaoSes.Id, Passo(passo).Id,
            new PePassoDetalheDTO { NivelId = nivel == null ? null : NivelId(nivel) }, (ctx ?? Admin().GetAwaiter().GetResult()).Email);

    [Fact]
    public async Task Livre_TodosOsPassosDoGuia_ONivelBase_EANumeracaoDoAvancado()
    {
        var trilha = await TrilhaAsync(OrgaoSes);
        var passos = PassosDa(trilha);

        Assert.Equal(PeDominios.ModoNiveis.Livre, trilha.ModoNiveis);
        Assert.Equal((NivelId("basico"), "Básico", true, true), (trilha.NivelId, trilha.NivelNome, trilha.NivelPadrao, trilha.NivelAtivo));
        Assert.Equal(50, passos.Count);
        Assert.Equal(7, trilha.Etapas.Count);

        // A numeração é a do Avançado de hoje (todos os passos visíveis)
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin);
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var avancado = PassosDa(await TrilhaAsync(OrgaoSes)).ToDictionary(p => p.Chave, p => p.Numero);
        Assert.Equal(avancado, passos.ToDictionary(p => p.Chave, p => p.Numero));

        // O que é do Básico segue a situação dele; o resto aparece opcional
        Assert.Equal(23, passos.Count(p => p.Situacao == PeDominios.Situacao.Obrigatorio));
        Assert.Equal(27, passos.Count(p => p.Situacao == PeDominios.Situacao.Opcional));
        Assert.Equal(PeDominios.Situacao.Obrigatorio, NaTrilha(trilha, "diagnostico.ativos")!.Situacao);
        Assert.Equal(PeDominios.Situacao.Opcional, NaTrilha(trilha, "diagnostico.swot")!.Situacao);
        Assert.Equal(PeDominios.Situacao.Opcional, NaTrilha(trilha, "preparacao.plano-trabalho")!.Situacao);
    }

    [Fact]
    public async Task Livre_ONivelEscolhidoDoOrgaoNaoVale_EVoltaNoDefinido()
    {
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin);
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        Assert.Equal(40, PassosDa(await TrilhaAsync(OrgaoSes)).Count);

        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "livre" }, EmailAdmin);
        var livre = await TrilhaAsync(OrgaoSes);
        Assert.Equal((NivelId("basico"), true), (livre.NivelId, livre.NivelPadrao));
        Assert.Equal(PeDominios.Situacao.Opcional, NaTrilha(livre, "diagnostico.swot")!.Situacao);
        // O nível de hoje do órgão nas respostas do PDTIC também é o base
        var pdtic = await AbrirSesAsync();
        Assert.Equal(NivelId("basico"), pdtic.NivelId);

        // A escolha ficou guardada: no definido, volta a valer
        Assert.Equal(NivelId("intermediario"), Context.PeOrgaosConfig.AsNoTracking().Single(c => c.OrgaoId == OrgaoSes.Id).NivelId);
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin);
        var definido = await TrilhaAsync(OrgaoSes);
        Assert.Equal((NivelId("intermediario"), 40), (definido.NivelId, PassosDa(definido).Count));
        Assert.Equal(PeDominios.Situacao.Obrigatorio, NaTrilha(definido, "diagnostico.swot")!.Situacao);
        Assert.All(PassosDa(definido), p => Assert.Null(p.Detalhe));
        Assert.All(PassosDa(definido), p => Assert.Empty(p.OpcoesDetalhe));
    }

    [Fact]
    public async Task Livre_OAjusteDoOrgaoVale_ETravadoNuncaDesliga()
    {
        await AjustarPassosAsync(OrgaoSes, ("diagnostico.swot", "desligado"), ("preparacao.plano-trabalho", "obrigatorio"),
            ("preparacao.equipe", "opcional"));
        // O travado desligado por fora (a tela recusa): continua obrigatório
        Context.PeOrgaosAjuste.Add(new PeOrgaoAjuste
        {
            OrgaoId = OrgaoSes.Id,
            AlvoTipo = PeDominios.AlvoAjuste.Passo,
            AlvoId = Passo("diagnostico.ativos").Id,
            Situacao = PeDominios.Situacao.Desligado,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = EmailAdmin
        });
        Context.SaveChanges();
        var trilha = await TrilhaAsync(OrgaoSes);

        Assert.Null(NaTrilha(trilha, "diagnostico.swot"));
        Assert.Equal(PeDominios.Situacao.Obrigatorio, NaTrilha(trilha, "preparacao.plano-trabalho")!.Situacao);
        Assert.Equal(PeDominios.Situacao.Obrigatorio, NaTrilha(trilha, "diagnostico.ativos")!.Situacao);
        Assert.Equal(PeDominios.Situacao.Opcional, NaTrilha(trilha, "preparacao.equipe")!.Situacao);
        Assert.True(NaTrilha(trilha, "preparacao.equipe")!.AjustadoParaOrgao);
        Assert.Equal(49, PassosDa(trilha).Count);
    }

    [Fact]
    public async Task Livre_AFormaPadrao_EAsFormasOferecidas_SemRepetir()
    {
        var trilha = await TrilhaAsync(OrgaoSes);

        // Passo ligado no Básico: a forma padrão é a do Básico; as formas são as distintas, do nível mais baixo
        var necessidades = NaTrilha(trilha, "diagnostico.necessidades-tic")!;
        Assert.Equal((NivelId("basico"), "Básico"), (necessidades.Detalhe!.NivelId, necessidades.Detalhe.NivelNome));
        Assert.False(necessidades.DetalheEscolhido);
        Assert.Equal(new[] { "Básico", "Intermediário" }, necessidades.OpcoesDetalhe.Select(o => o.NivelNome));
        Assert.Equal(necessidades.Secoes.Count, necessidades.OpcoesDetalhe[0].Secoes);
        Assert.Equal(necessidades.Secoes.Sum(s => s.Campos.Count), necessidades.OpcoesDetalhe[0].Campos);
        Assert.True(necessidades.OpcoesDetalhe[1].Campos > necessidades.OpcoesDetalhe[0].Campos);
        // No Básico, a prioridade simples; sem os critérios GUT
        var campos = necessidades.Secoes.Single().Campos.Select(c => c.Chave).ToList();
        Assert.Contains("prioridade_simples", campos);
        Assert.DoesNotContain("gravidade", campos);

        // Três formas distintas na metodologia
        Assert.Equal(new[] { "Básico", "Intermediário", "Avançado" }, NaTrilha(trilha, "preparacao.metodologia")!.OpcoesDetalhe.Select(o => o.NivelNome));

        // Passo fora do Básico: a forma padrão é a do primeiro nível em que está ligado
        var referencial = NaTrilha(trilha, "diagnostico.referencial-estrategico")!;
        Assert.Equal("Intermediário", referencial.Detalhe!.NivelNome);
        Assert.Equal(new[] { "Intermediário", "Avançado" }, referencial.OpcoesDetalhe.Select(o => o.NivelNome));

        // Três níveis e duas formas: o Avançado dá a mesma forma do Intermediário e não se repete
        var contratacoes = NaTrilha(trilha, "planejamento.contratacoes")!;
        Assert.Equal(new[] { "Básico", "Intermediário" }, contratacoes.OpcoesDetalhe.Select(o => o.NivelNome));
        Assert.Equal("Básico", contratacoes.Detalhe!.NivelNome);

        // Passo com a mesma forma em todos os níveis: sem escolha (lista vazia), com a forma em uso
        var aprovacao = NaTrilha(trilha, "planejamento.aprovacao-sgtic")!;
        Assert.Empty(aprovacao.OpcoesDetalhe);
        Assert.Equal("Básico", aprovacao.Detalhe!.NivelNome);
        var plano = NaTrilha(trilha, "preparacao.plano-trabalho")!;
        Assert.Empty(plano.OpcoesDetalhe);
        Assert.Equal("Avançado", plano.Detalhe!.NivelNome);
    }

    [Fact]
    public async Task Livre_AFormaEscolhida_TrocaAsSecoesEOsCampos_EOPadraoApagaAEscolha()
    {
        var resposta = await DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario");
        var passo = NaTrilha(resposta, "diagnostico.necessidades-tic")!;
        Assert.Equal(("Intermediário", true), (passo.Detalhe!.NivelNome, passo.DetalheEscolhido));
        var campos = passo.Secoes.Single().Campos.Select(c => c.Chave).ToList();
        Assert.Contains("gravidade", campos);
        Assert.Equal(passo.OpcoesDetalhe[1].Campos, passo.Secoes.Sum(s => s.Campos.Count));
        var linha = Context.PeOrgaosPassoDetalhe.AsNoTracking().Single(d => d.OrgaoId == OrgaoSes.Id && d.PassoId == passo.Id);
        Assert.Equal((NivelId("intermediario"), EmailAdmin), (linha.NivelId, linha.AlteradoPor));

        // A trilha lida de novo tem a mesma forma (e só este passo mudou)
        var trilha = await TrilhaAsync(OrgaoSes);
        Assert.Equal("Intermediário", NaTrilha(trilha, "diagnostico.necessidades-tic")!.Detalhe!.NivelNome);
        Assert.Equal("Básico", NaTrilha(trilha, "diagnostico.ativos")!.Detalhe!.NivelNome);

        // A forma padrão de novo (pelo nível dela ou por nulo) apaga a escolha
        resposta = await DefinirFormaAsync("diagnostico.necessidades-tic", "basico");
        Assert.False(NaTrilha(resposta, "diagnostico.necessidades-tic")!.DetalheEscolhido);
        Assert.False(Context.PeOrgaosPassoDetalhe.Any());
        await DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario");
        resposta = await DefinirFormaAsync("diagnostico.necessidades-tic", null);
        Assert.Equal("Básico", NaTrilha(resposta, "diagnostico.necessidades-tic")!.Detalhe!.NivelNome);
        Assert.False(Context.PeOrgaosPassoDetalhe.Any());
    }

    [Fact]
    public async Task Livre_MudarAFormaNaoApagaDado_OCampoQueSaiGuardaOValor()
    {
        var pdtic = await AbrirSesAsync();
        await DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario");
        var necessidade = await IncluirNoPdticAsync(pdtic.Id, "necessidades", new
        {
            descricao = "Substituir o sistema de regulação.", tipo = "servico", origem = "swot", areas = "Regulação", priorizada = true,
            gravidade = 5, urgencia = 4, tendencia = 3
        });
        Assert.Equal("5", Valor(necessidade, "gravidade"));

        // De volta ao Básico: o campo some da resposta, mas o valor fica guardado
        await DefinirFormaAsync("diagnostico.necessidades-tic", null);
        var noBasico = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "necessidades", await Orgao())).Registros.Single();
        Assert.False(noBasico.Dados.ContainsKey("gravidade"));
        Assert.Contains("\"gravidade\"", RegistroNoBanco(necessidade.Id).Dados);

        await DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario");
        var deVolta = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "necessidades", await Orgao())).Registros.Single();
        Assert.Equal("5", Valor(deVolta, "gravidade"));
    }

    [Fact]
    public async Task Detalhe_Recusas_ModoDefinido_PassoQueNaoAparece_FormaForaDaLista_EPdticFechado()
    {
        // Forma fora da lista (o nível em que o passo não está, ou o que repete a forma de outro): 400;
        // passo sem escolha (uma forma só) também
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => DefinirFormaAsync("diagnostico.referencial-estrategico", "basico")));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => DefinirFormaAsync("planejamento.contratacoes", "avancado")));
        var ex = await Assert.ThrowsAnyAsync<ApiException>(() => DefinirFormaAsync("planejamento.aprovacao-sgtic", "intermediario"));
        Assert.Equal("Escolha uma das formas deste passo.", ex.Error.Message);

        // Passo que não aparece para o órgão: 404
        await AjustarPassosAsync(OrgaoSes, ("diagnostico.swot", "desligado"));
        var fora = await Assert.ThrowsAnyAsync<ApiException>(() => DefinirFormaAsync("diagnostico.swot", "avancado"));
        Assert.Equal(((int)ErrorCode.PePassoIndisponivel, "Este passo não aparece para o órgão. Atualize a tela."), (fora.Error.Code, fora.Error.Message));

        // O passo fechado no PDTIC atual do órgão: 409 com a mensagem da situação
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var fechado = await Assert.ThrowsAnyAsync<ApiException>(() => DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario"));
        Assert.Equal(((int)ErrorCode.PeDetalheRecusado, "Este PDTIC foi enviado ao CGTIC e não muda até a decisão."), (fechado.Error.Code, fechado.Error.Message));
        // O passo das etapas 4 a 7 também espera a publicação
        var espera = await Assert.ThrowsAnyAsync<ApiException>(() => DefinirFormaAsync("monitoramento.ciclo-monitoramento", "intermediario"));
        Assert.Equal((int)ErrorCode.PeDetalheRecusado, espera.Error.Code);

        // No modo definido: 409 PeDetalheRecusado
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin);
        var definido = await Assert.ThrowsAnyAsync<ApiException>(() => DefinirFormaAsync("diagnostico.necessidades-tic", "intermediario"));
        Assert.Equal(((int)ErrorCode.PeDetalheRecusado, "No modo definido, a forma dos passos segue o nível que o administrador escolheu para o órgão."),
            (definido.Error.Code, definido.Error.Message));
    }

    [Fact]
    public async Task Detalhe_SemPdticAtualPode_ENoPdticVigenteSoOAcompanhamento()
    {
        // Sem PDTIC: pode
        await DefinirFormaAsync("preparacao.metodologia", "avancado");
        Assert.Equal("Avançado", NaTrilha(await TrilhaAsync(OrgaoSes), "preparacao.metodologia")!.Detalhe!.NivelNome);
        // A forma padrão de novo apaga a escolha (o PDTIC do teste preenche a metodologia do Básico)
        await DefinirFormaAsync("preparacao.metodologia", "basico");
        Assert.Empty(Context.PeOrgaosPassoDetalhe.AsNoTracking());

        // Com o PDTIC publicado (vigente): a elaboração não muda; o acompanhamento, sim
        var pdtic = await ProntoParaEnviarAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado);
        Assert.Equal((int)ErrorCode.PeDetalheRecusado, (await Assert.ThrowsAnyAsync<ApiException>(() =>
            DefinirFormaAsync("preparacao.metodologia", "intermediario"))).Error.Code);
        var resposta = await DefinirFormaAsync("monitoramento.ciclo-monitoramento", "intermediario");
        Assert.Equal("Intermediário", NaTrilha(resposta, "monitoramento.ciclo-monitoramento")!.Detalhe!.NivelNome);
    }

    [Fact]
    public async Task Detalhe_Permissoes_EAsRotas()
    {
        var passo = Passo("diagnostico.necessidades-tic").Id;
        var corpo = Corpo(new { NivelId = NivelId("intermediario") });

        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserOrgaoSeec })
        {
            var (status, erro) = Resultado(await ControladorOrgaos(user).DefinirDetalhe(OrgaoSes.Id, passo, corpo));
            Assert.Equal(403, status);
            Assert.Equal((int)ErrorCode.PeSemPermissao, JsonSerializer.SerializeToElement(erro).GetProperty("Code").GetInt32());
        }
        foreach (var user in new[] { UserOrgaoSes, UserPeAdmin, UserAdminGeral })
        {
            var (status, trilha) = Resultado(await ControladorOrgaos(user).DefinirDetalhe(OrgaoSes.Id, passo, corpo));
            Assert.Equal(200, status);
            Assert.True(NaTrilha(Assert.IsType<PeTrilhaResponse>(trilha), "diagnostico.necessidades-tic")!.DetalheEscolhido);
        }
        // A mesma escolha de novo não grava nada: fica quem escolheu primeiro
        Assert.Equal(UserOrgaoSes.Email, Context.PeOrgaosPassoDetalhe.AsNoTracking().Single().AlteradoPor);

        // O modo definido: 409 com o código 1146
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin);
        var (conflito, corpoErro) = Resultado(await ControladorOrgaos(UserOrgaoSes).DefinirDetalhe(OrgaoSes.Id, passo, corpo));
        Assert.Equal(409, conflito);
        Assert.Equal(1146, JsonSerializer.SerializeToElement(corpoErro).GetProperty("Code").GetInt32());

        // Antes da versão 8 (o intervalo do deploy): 409 PeModeloIndisponivel
        VersaoDoModelo(7);
        var (intervalo, corpoIntervalo) = Resultado(await ControladorOrgaos(UserOrgaoSes).DefinirDetalhe(OrgaoSes.Id, passo, corpo));
        Assert.Equal(409, intervalo);
        Assert.Equal((int)ErrorCode.PeModeloIndisponivel, JsonSerializer.SerializeToElement(corpoIntervalo).GetProperty("Code").GetInt32());
    }

    [Fact]
    public async Task Livre_ONivelDesativado_NaoEForma_EAEscolhaDeleNaoVale()
    {
        await DefinirFormaAsync("diagnostico.ativos", "intermediario");
        await Modelo.AtualizarNivelAsync(NivelId("intermediario"), new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } }, EmailAdmin);

        var ativos = NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.ativos")!;
        Assert.Equal(("Básico", false), (ativos.Detalhe!.NivelNome, ativos.DetalheEscolhido));
        Assert.DoesNotContain(ativos.OpcoesDetalhe, o => o.NivelNome == "Intermediário");
    }

    /// <summary>A versão do conteúdo semeado, à mão (7 = antes da F3: o modo livre e a validação desligados).</summary>
    private void VersaoDoModelo(int versao)
    {
        var linha = Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo);
        linha.Valor = versao.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }
}
