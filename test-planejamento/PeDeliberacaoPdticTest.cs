using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A deliberação do CGTIC sobre o PDTIC (E7, rodada A), na mesma fila da E3: o item traz
/// "PDTIC SES 1.0", o órgão e o PDF enviado; só a Secretaria do CGTIC (e o admin geral)
/// decide; aprovado (com o ato e a data) põe o PDTIC em "aprovado" e a versão enviada em
/// "aprovada"; devolvido (com a observação) volta o PDTIC a ser editável, a trilha mostra a
/// devolução e o reenvio cria outra deliberação.
/// </summary>
public class PeDeliberacaoPdticTest : PeAprovacaoTestBase
{
    [Fact]
    public async Task Fila_TrazOPdticComOOrgaoEOPdf_EOFiltroPorTipo()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);

        var fila = await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { ObjetoTipo = "pdtic" });
        var item = Assert.Single(fila.Items);
        Assert.Equal("PDTIC SES 1.0", item.Titulo);
        Assert.Equal("SES", item.OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Saúde", item.OrgaoNome);
        Assert.Equal(new { PdticId = pdtic.Id, Numero = 2 }, new { item.Documento!.PdticId, item.Documento.Numero });
        Assert.Empty((await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { ObjetoTipo = "petic" })).Items);

        // A Secretaria do CGTIC baixa o PDF enviado (vê todos os órgãos)
        var pdf = await Documentos.ArquivoDaVersaoAsync(item.Documento.PdticId, item.Documento.Numero, await Cgtic());
        Assert.NotEmpty(pdf.Conteudo);
    }

    [Fact]
    public async Task Aprovar_ExigeOAto_PoeOPdticEmAprovado_EAVersaoEnviadaEmAprovada()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var deliberacao = DeliberacaoAguardando(pdtic.Id);

        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(deliberacao.Id, new PeDecidirDTO { Decisao = "aprovado", AtoData = "2026-09-10" }, await Cgtic())));
        Assert.Equal(PeDominios.SituacaoPdtic.EmAprovacao, PdticNoBanco(pdtic.Id).Situacao);

        var decidida = await AprovarNoCgticAsync(pdtic.Id);
        Assert.Equal("aprovado", decidida.Situacao);
        Assert.Equal("PDTIC SES 1.0", decidida.Titulo);
        Assert.Equal(2, decidida.Documento!.Numero);

        var aprovado = await Pdtics.ObterAsync(pdtic.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.Aprovado, aprovado.Situacao);
        Assert.NotNull(aprovado.AprovadoEm);
        Assert.Equal("aprovado", aprovado.Deliberacao!.Situacao);
        Assert.Equal("12/2026", aprovado.Deliberacao.AtoNumero);
        Assert.Equal("aprovada", Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id && v.Numero == 2).Situacao);
        Assert.Equal("minuta", Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id && v.Numero == 1).Situacao);

        // Decidida não muda
        Assert.Equal(Codigo(ErrorCode.PeDeliberacaoJaDecidida), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(deliberacao.Id, new PeDecidirDTO { Decisao = "devolvido", Observacao = "x" }, await Cgtic())));
    }

    [Fact]
    public async Task Devolver_VoltaAEditar_ATrilhaMostraADevolucao_EOReenvioCriaOutraDeliberacao()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);

        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(DeliberacaoAguardando(pdtic.Id).Id, new PeDecidirDTO { Decisao = "devolvido" }, await Cgtic())));
        await DevolverNoCgticAsync(pdtic.Id, "Detalhe as metas de segurança.");

        var devolvido = await Pdtics.ObterAsync(pdtic.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.Devolvido, devolvido.Situacao);
        Assert.True(devolvido.PodeEditar);
        Assert.Equal("devolvido", devolvido.Deliberacao!.Situacao);
        Assert.Equal("Detalhe as metas de segurança.", devolvido.Deliberacao.Observacao);
        Assert.Equal("enviada", Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id && v.Numero == 2).Situacao);

        // A trilha: a deliberação em atenção, com a observação; o envio volta a pender; é o próximo passo
        var situacao = await SituacaoAsync(pdtic.Id);
        var deliberacao = situacao.Passos.Single(p => p.Chave == "planejamento.deliberacao-cgtic");
        var envio = situacao.Passos.Single(p => p.Chave == "planejamento.aprovacao-sgtic");
        Assert.Equal(PeDominios.SituacaoPasso.Atencao, deliberacao.Situacao);
        var aviso = Assert.Single(deliberacao.Avisos);
        Assert.StartsWith("O CGTIC devolveu o PDTIC em ", aviso);
        Assert.Contains(": Detalhe as metas de segurança. Ajuste o que foi pedido e envie de novo no passo " + envio.Numero + ".", aviso);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, envio.Situacao);
        Assert.Equal(deliberacao.Numero, situacao.ProximoPasso);

        // Ajusta, registra de novo a aprovação do SGTIC (D3: a versão ajustada volta ao comitê
        // interno) e reenvia: outra deliberação, com outro PDF; a devolvida fica no histórico
        await IncluirNoPdticAsync(pdtic.Id, "ativos", new { nome = "Firewall", tipo = "infraestrutura", situacao = "em_operacao" });
        await AprovacaoSgticAsync(pdtic.Id, data: HojeIso());
        var reenviado = await EnviarAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPdtic.EmAprovacao, reenviado.Situacao);
        Assert.Equal("aguardando", reenviado.Deliberacao!.Situacao);
        Assert.Equal(3, reenviado.Deliberacao.Documento!.Numero);
        var doPdtic = Context.PeDeliberacoes.AsNoTracking().Where(d => d.ObjetoTipo == "pdtic" && d.ObjetoId == pdtic.Id).OrderBy(d => d.Id).ToList();
        Assert.Equal(new[] { "devolvido", "aguardando" }, doPdtic.Select(d => d.Situacao));
    }

    [Fact]
    public async Task SoASecretariaDoCgticDecide_ENaoDecideOQueNaoEstaEmAprovacao()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var id = DeliberacaoAguardando(pdtic.Id).Id;
        var corpo = Json(new { Decisao = "aprovado", AtoNumero = "1/2026", AtoData = "2026-09-10" });

        foreach (var user in new[] { UserPeSgdi, UserPeAdmin, UserOrgaoSes, UserConsultaSes })
        {
            var (status, resposta) = Resultado(await ControladorDeliberacoes(user).Decidir(id, corpo));
            Assert.Equal(StatusCodes.Status403Forbidden, status);
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), CodigoDe(resposta));
        }

        // O PDTIC mudou por fora (não está mais em aprovação): 409
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Devolvido);
        Assert.Equal(Codigo(ErrorCode.PeConflitoGravacao), await ErroAsync(async () =>
            await Deliberacoes.DecidirAsync(id, new PeDecidirDTO { Decisao = "aprovado", AtoNumero = "1/2026", AtoData = "2026-09-10" }, await Cgtic())));
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.EmAprovacao);

        var (ok, decidida) = Resultado(await ControladorDeliberacoes(UserAdminGeral).Decidir(id, corpo));
        Assert.Equal(StatusCodes.Status200OK, ok);
        Assert.Equal("aprovado", Assert.IsType<PeDeliberacaoResponse>(decidida).Situacao);
    }
}
