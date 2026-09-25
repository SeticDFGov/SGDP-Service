using System.Globalization;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A situação dos passos no caminho da aprovação (E7, rodada A): o documento (feito com um
/// PDF), a aprovação (feito com a decisão tomada; devolvido pende com o aviso), o envio, a
/// deliberação (aguardando o CGTIC, com o motivo e a data), a publicação (aguardando a
/// aprovação), as etapas 4 a 7 aguardando a publicação (desde a rodada B, o monitoramento pelo
/// ciclo e a etapa 7 esperando os dias antes do fim da vigência), e o próximo passo (atrasado,
/// atenção, pendente; aguardando e externo nunca; o pendente de uma etapa fechada também não).
/// </summary>
public class PeSituacaoAprovacaoTest : PeAprovacaoTestBase
{
    private static string DataDeHoje() =>
        DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.TodayBrasilia()).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Documento_FeitoComUmPdfGerado()
    {
        var pdtic = await AbrirSesAsync();
        var antes = await PassoDaSituacaoAsync(pdtic.Id, "planejamento.documento");
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, antes.Situacao);
        Assert.Null(antes.Motivo);
        Assert.True(antes.PodeEditar);

        await Documentos.GerarPdfAsync(pdtic.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(pdtic.Id, "planejamento.documento")).Situacao);
    }

    [Fact]
    public async Task Aprovacao_FeitaComADecisao_DevolvidoPendeComOAviso()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        const string chave = "preparacao.aprovacao-plano-trabalho";
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoDaSituacaoAsync(pdtic.Id, chave)).Situacao);

        var registro = await IncluirNoPdticAsync(pdtic.Id, "aprovacao_plano_trabalho", new { decisao = "devolvido", data = "2026-03-01", observacao = "Rever o cronograma." });
        var devolvido = await PassoDaSituacaoAsync(pdtic.Id, chave);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, devolvido.Situacao);
        Assert.Equal("Devolvido: ajuste e registre a nova decisão.", Assert.Single(devolvido.Avisos));

        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "aprovacao_plano_trabalho", registro.Id,
            Salvar(new { decisao = "aprovado", data = "2026-03-10" }), await Orgao());
        var aprovado = await PassoDaSituacaoAsync(pdtic.Id, chave);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, aprovado.Situacao);
        Assert.Empty(aprovado.Avisos);
    }

    [Fact]
    public async Task AvaliacaoDoComite_SeguirOuRevisar_ContaComoDecisao()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado);
        const string chave = "avaliacao-intermediaria.avaliacao-comite";
        // Sem avaliação intermediária, o passo espera (E7, rodada B: a seção é por ciclo de avaliação)
        var antes = await PassoDaSituacaoAsync(pdtic.Id, chave);
        Assert.Equal((PeDominios.SituacaoPasso.Aguardando, PePdticService.MotivoSemAvaliacao), (antes.Situacao, antes.Motivo));

        var avaliacao = await Acompanhamento.CriarCicloAsync(pdtic.Id, new PeCicloCriarDTO { Tipo = "avaliacao" }, await Orgao());
        var registro = await IncluirNoPdticAsync(pdtic.Id, "avaliacao_comite", new { decisao = "seguir", data = "2027-06-30" }, cicloId: avaliacao.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(pdtic.Id, chave)).Situacao);
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "avaliacao_comite", registro.Id,
            Salvar(new { decisao = "revisar", data = "2027-06-30" }), await Orgao(), avaliacao.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(pdtic.Id, chave)).Situacao);
    }

    [Fact]
    public async Task CaminhoDaAprovacao_EnvioDeliberacaoPublicacaoEAsEtapasSeguintes()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var situacao = await SituacaoAsync(pdtic.Id);
        PePassoSituacaoResponse De(string chave) => situacao.Passos.Single(p => p.Chave == chave);

        // Em elaboração: envio pendente (é o próximo), deliberação pendente, publicação e etapas 4 a 7 aguardando
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("planejamento.aprovacao-sgtic").Situacao);
        Assert.Equal(De("planejamento.aprovacao-sgtic").Numero, situacao.ProximoPasso);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("planejamento.deliberacao-cgtic").Situacao);
        Assert.Equal((PeDominios.SituacaoPasso.Aguardando, "Disponível depois da aprovação do CGTIC"),
            (De("planejamento.publicacao").Situacao, De("planejamento.publicacao").Motivo));
        foreach (var chave in new[] { "plano-acompanhamento.quem-acompanha", "monitoramento.ciclo-monitoramento",
                     "fechamento.licoes-aprendidas", "fechamento.aprovacao-autoridade" })
        {
            Assert.Equal(PeDominios.SituacaoPasso.Aguardando, De(chave).Situacao);
            Assert.Equal("Disponível depois da publicação do PDTIC", De(chave).Motivo);
            Assert.False(De(chave).PodeEditar);
        }

        // Em aprovação: o envio feito, a deliberação aguardando desde hoje; nada a recomendar
        await EnviarAsync(pdtic.Id);
        situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, De("planejamento.aprovacao-sgtic").Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Aguardando, De("planejamento.deliberacao-cgtic").Situacao);
        Assert.Equal("Aguardando a deliberação do CGTIC desde " + DataDeHoje(), De("planejamento.deliberacao-cgtic").Motivo);
        Assert.Null(situacao.ProximoPasso);

        // Aprovado: a deliberação feita, a publicação pendente e recomendada
        await AprovarNoCgticAsync(pdtic.Id);
        situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, De("planejamento.deliberacao-cgtic").Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("planejamento.publicacao").Situacao);
        Assert.Null(De("planejamento.publicacao").Motivo);
        Assert.True(De("planejamento.publicacao").PodeEditar);
        Assert.Equal(De("planejamento.publicacao").Numero, situacao.ProximoPasso);

        // Publicado: a publicação feita; as etapas 4 a 7 abertas: o monitoramento pelo ciclo que
        // começou na publicação (rodada B) e a etapa 7 esperando os 90 dias antes do fim da vigência
        await PublicarAsync(pdtic.Id);
        situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, De("planejamento.publicacao").Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("plano-acompanhamento.quem-acompanha").Situacao);
        Assert.True(De("plano-acompanhamento.quem-acompanha").PodeEditar);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("monitoramento.ciclo-monitoramento").Situacao);
        Assert.Equal((PeDominios.SituacaoPasso.Aguardando, "Disponível a partir de 02/10/2029, 90 dias antes do fim da vigência (31/12/2029)"),
            (De("fechamento.aprovacao-autoridade").Situacao, De("fechamento.aprovacao-autoridade").Motivo));
        Assert.Equal(De("plano-acompanhamento.quem-acompanha").Numero, situacao.ProximoPasso);
        Assert.All(situacao.Passos.Where(p => p.Situacao is "feito" or "pendente" or "continuo"), p => Assert.Null(p.Motivo));
    }

    [Fact]
    public async Task ProximoPasso_NaoRecomendaOPendenteDeUmaEtapaFechada_NemOAguardando()
    {
        // Os documentos de referência, opcionais para a SES (fora do Básico): pendentes, mas não exigidos no envio
        await AjustarPassosAsync(OrgaoSes, ("preparacao.documentos-referencia", "opcional"));
        var pdtic = await ProntoParaEnviarAsync();
        var referencias = await PassoDaSituacaoAsync(pdtic.Id, "preparacao.documentos-referencia");
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, referencias.Situacao);
        Assert.Equal(referencias.Numero, (await SituacaoAsync(pdtic.Id)).ProximoPasso);
        Assert.True((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).PodeEnviar);

        await EnviarAsync(pdtic.Id);
        Assert.Null((await SituacaoAsync(pdtic.Id)).ProximoPasso);
        await AprovarNoCgticAsync(pdtic.Id);
        Assert.Equal((await PassoDaSituacaoAsync(pdtic.Id, "planejamento.publicacao")).Numero, (await SituacaoAsync(pdtic.Id)).ProximoPasso);
    }

    [Fact]
    public async Task Comentario_NumPassoAguardando_PassaNaFrente()
    {
        var pdtic = await AbrirSesAsync();
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO
        {
            PassoId = Passo("plano-acompanhamento.quem-acompanha").Id,
            Texto = "Pense já em quem vai acompanhar."
        }, await ContextoDe(UserPeSgdi));

        var passo = await PassoDaSituacaoAsync(pdtic.Id, "plano-acompanhamento.quem-acompanha");
        Assert.Equal(PeDominios.SituacaoPasso.Atencao, passo.Situacao);
        Assert.Null(passo.Motivo);
        Assert.Equal(passo.Numero, (await SituacaoAsync(pdtic.Id)).ProximoPasso);
    }

    [Fact]
    public async Task CalcularSituacao_SemQuemChama_PodeEditarFalso()
    {
        var pdtic = await AbrirSesAsync();
        var entidade = PdticNoBanco(pdtic.Id);
        var trilha = await PeTrilhaOrgao.CarregarAsync(Context, OrgaoSes.Id, soAtivo: false);
        var situacao = await Pdtics.CalcularSituacaoAsync(entidade, trilha);
        Assert.Equal(23, situacao.Passos.Count);
        Assert.All(situacao.Passos, p => Assert.False(p.PodeEditar));
        Assert.Equal("1.1", situacao.ProximoPasso);
    }
}
