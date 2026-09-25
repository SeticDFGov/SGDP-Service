using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Envio do PDTIC ao CGTIC (E7, rodada A): a prévia com o que falta (passo a passo, com o
/// número, o título e o motivo), as exigências (os passos obrigatórios das etapas 1 a 3
/// anteriores ao envio feitos ou "não se aplica", e a aprovação do SGTIC com "aprovado" e a
/// data), o envio (o PDF como versão "enviada", a deliberação na fila com o PDF e o PDTIC em
/// aprovação), quem envia e o que fica fechado depois.
/// </summary>
public class PeEnvioTest : PeAprovacaoTestBase
{
    [Fact]
    public async Task Previa_DoPdticNovo_ListaOsPassosQueFaltam_NaOrdemDaTrilha()
    {
        var pdtic = await AbrirSesAsync();
        var envio = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());

        Assert.False(envio.PodeEnviar);
        Assert.Equal("Resolva o que falta na lista antes de enviar o PDTIC ao CGTIC.", envio.Motivo);
        var situacao = await SituacaoAsync(pdtic.Id);
        string Numero(string chave) => situacao.Passos.Single(p => p.Chave == chave).Numero;

        // Os obrigatórios das etapas 1 a 3 antes do envio que não estão feitos, e a aprovação do SGTIC.
        // Os princípios não faltam: o PDTIC nasce com os do art. 4º na seção (F2)
        Assert.Equal("feito", situacao.Passos.Single(p => p.Chave == "preparacao.principios").Situacao);
        Assert.Equal(new[]
        {
            Numero("preparacao.abrangencia"), Numero("preparacao.nomes"), Numero("preparacao.sgtic"), Numero("preparacao.equipe"),
            Numero("preparacao.metodologia"), Numero("preparacao.estrategias"),
            Numero("diagnostico.ambiente-tecnologico"), Numero("diagnostico.ativos"), Numero("diagnostico.necessidades-tic"),
            Numero("planejamento.metas-acoes"), Numero("planejamento.acoes-tematicas"), Numero("planejamento.contratacoes"),
            Numero("planejamento.documento"), Numero("planejamento.aprovacao-sgtic")
        }, envio.Pendencias.Select(p => p.PassoNumero));
        var abrangencia = envio.Pendencias[0];
        Assert.Equal(Passo("preparacao.abrangencia").Id, abrangencia.PassoId);
        Assert.Equal("Diga o que o PDTIC abrange e por quanto tempo vale", abrangencia.PassoTitulo);
        Assert.Equal("Preencha \"Abrangência e vigência\".", abrangencia.Motivo);
        Assert.Equal("Inclua pelo menos um item em \"Soluções e ativos de TIC\".",
            envio.Pendencias.Single(p => p.PassoId == Passo("diagnostico.ativos").Id).Motivo);
        Assert.StartsWith("Inclua uma ação com o tema ou justifique o tema sem ação:",
            envio.Pendencias.Single(p => p.PassoId == Passo("planejamento.acoes-tematicas").Id).Motivo);
        Assert.Equal("Gere o PDF do documento e confira a prévia.",
            envio.Pendencias.Single(p => p.PassoId == Passo("planejamento.documento").Id).Motivo);
        Assert.Equal("Registre a aprovação do SGTIC: a decisão \"Aprovado\" e a data.", envio.Pendencias[^1].Motivo);

        // Os passos que o nível não pede e os opcionais não entram (a seção opcional das aquisições de IA)
        Assert.DoesNotContain(envio.Pendencias, p => p.PassoId == Passo("diagnostico.sistemas-ia").Id);
    }

    [Fact]
    public async Task Previa_OsRegistrosIncompletos_ESoAAprovacaoDoSgtic_QuandoOResteEstaFeito()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherElaboracaoAsync(pdtic.Id, sgtic: false);

        var envio = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        var sgtic = Assert.Single(envio.Pendencias);
        Assert.Equal(Passo("planejamento.aprovacao-sgtic").Id, sgtic.PassoId);
        Assert.Equal("Registre a aprovação do SGTIC: a decisão \"Aprovado\" e a data.", sgtic.Motivo);

        // O SGTIC devolveu: a pendência explica
        await AprovacaoSgticAsync(pdtic.Id, "devolvido");
        Assert.Equal("O SGTIC devolveu o PDTIC: ajuste o que foi pedido e registre a nova decisão.",
            Assert.Single((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).Pendencias).Motivo);
        Assert.Contains(PePdticService.AvisoDevolvido, (await PassoDaSituacaoAsync(pdtic.Id, "planejamento.aprovacao-sgtic")).Avisos);

        // Aprovado: pode enviar
        await AprovacaoSgticAsync(pdtic.Id);
        var pronto = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        Assert.True(pronto.PodeEnviar);
        Assert.Empty(pronto.Pendencias);
        Assert.Null(pronto.Motivo);

        // Um registro que o modelo passou a pedir mais volta a pender, com o campo
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "campo", AlvoId = Campo("ativos", "descricao").Id, Situacao = "obrigatorio" }
        }, EmailAdmin);
        var ativos = Assert.Single((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).Pendencias);
        Assert.Equal(Passo("diagnostico.ativos").Id, ativos.PassoId);
        Assert.Equal("AT01: preencha \"Descrição\".", ativos.Motivo);
    }

    [Fact]
    public async Task Envio_GeraOPdfEnviado_CriaADeliberacaoComOPdf_EPoeOPdticEmAprovacao()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var enviado = await EnviarAsync(pdtic.Id);

        Assert.Equal(PeDominios.SituacaoPdtic.EmAprovacao, enviado.Situacao);
        Assert.NotNull(enviado.EnviadoEm);
        Assert.False(enviado.PodeEditar);

        // A minuta continua; o PDF enviado é a versão seguinte, "enviada"
        var versoes = await Documentos.VersoesAsync(pdtic.Id, await Orgao());
        Assert.Equal(new[] { (2, "enviada"), (1, "minuta") }, versoes.Select(v => (v.Numero, v.Situacao)));
        var arquivo = await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 2, await Cgtic());
        Assert.Equal("PDTIC_SES_v1.0_2.pdf", arquivo.NomeArquivo);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(arquivo.Conteudo, 0, 5));

        // A deliberação aguardando, com o PDF enviado
        var deliberacao = enviado.Deliberacao!;
        Assert.Equal("pdtic", deliberacao.ObjetoTipo);
        Assert.Equal(pdtic.Id, deliberacao.ObjetoId);
        Assert.Equal("aguardando", deliberacao.Situacao);
        Assert.Equal("PDTIC SES 1.0", deliberacao.Titulo);
        Assert.Equal("SES", deliberacao.OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Saúde", deliberacao.OrgaoNome);
        Assert.Equal(pdtic.Id, deliberacao.Documento!.PdticId);
        Assert.Equal(2, deliberacao.Documento.Numero);
        Assert.Equal(UserOrgaoSes.Email, deliberacao.EnviadoPor);
        var noBanco = Context.PeDeliberacoes.AsNoTracking().Single(d => d.Id == deliberacao.Id);
        Assert.Equal(Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id && v.Numero == 2).Id, noBanco.DocVersaoId);

        // A prévia agora explica a espera
        var previa = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        Assert.False(previa.PodeEnviar);
        Assert.StartsWith("O PDTIC foi enviado em ", previa.Motivo);
        Assert.EndsWith(" e aguarda a deliberação do CGTIC.", previa.Motivo);
        Assert.Empty(previa.Pendencias);
    }

    [Fact]
    public async Task Envio_ComPendencias_400ComALista_ENaoMudaNada()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherElaboracaoAsync(pdtic.Id, minuta: false);

        var ex = await Assert.ThrowsAsync<PePendenciasException>(() => EnviarAsync(pdtic.Id));
        Assert.Equal((int)ErrorCode.PePdticComPendencias, ex.Error.Code);
        Assert.Equal("Falta um passo para enviar o PDTIC ao CGTIC. Confira a lista.", ex.Error.Message);
        var documento = Assert.Single(ex.Pendencias);
        Assert.Equal(Passo("planejamento.documento").Id, documento.PassoId);

        Assert.Equal(PeDominios.SituacaoPdtic.EmElaboracao, PdticNoBanco(pdtic.Id).Situacao);
        Assert.Empty(Context.PeDeliberacoes.Where(d => d.ObjetoTipo == "pdtic"));
        Assert.Empty(Context.PeDocVersoes.Where(v => v.PdticId == pdtic.Id));
    }

    [Fact]
    public async Task Envio_ComentarioAbertoNumPassoObrigatorio_Pende()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var comentario = await Comentarios.CriarAsync(pdtic.Id,
            new PeComentarioCriarDTO { PassoId = Passo("diagnostico.ativos").Id, Texto = "Inclua a rede." }, await ContextoDe(UserPeSgdi));

        var pendencia = Assert.Single((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).Pendencias);
        Assert.Equal(Passo("diagnostico.ativos").Id, pendencia.PassoId);
        Assert.Equal("Há comentário aberto neste passo: responda e marque como resolvido.", pendencia.Motivo);

        await Comentarios.ResolverAsync(comentario.Id, await Orgao());
        Assert.True((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).PodeEnviar);
    }

    [Fact]
    public async Task Envio_SoAEquipeDoOrgao_EOAdminGeral()
    {
        var pdtic = await ProntoParaEnviarAsync();
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => EnviarAsync(pdtic.Id, user)));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => EnviarAsync(pdtic.Id, UserOrgaoSeec)));

        // A consulta vê a prévia, mas não envia
        var previa = await Aprovacao.EnvioAsync(pdtic.Id, await ContextoDe(UserConsultaSes));
        Assert.False(previa.PodeEnviar);
        Assert.Equal("Quem envia o PDTIC ao CGTIC é a equipe do órgão.", previa.Motivo);
        Assert.Empty(previa.Pendencias);

        var enviado = await EnviarAsync(pdtic.Id, UserAdminGeral);
        Assert.Equal(PeDominios.SituacaoPdtic.EmAprovacao, enviado.Situacao);
    }

    [Fact]
    public async Task DepoisDoEnvio_NadaMuda_ENaoSeEnviaDeNovo()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var dono = PeDono.DoPdtic(pdtic.Id);
        var orgao = await Orgao();
        var ativo = (await Registros.ListarAsync(dono, "ativos", orgao)).Registros.Single();

        var ex = await Assert.ThrowsAsync<ApiException>(() => Registros.AtualizarAsync(dono, "ativos", ativo.Id,
            Salvar(new { nome = "Outro", tipo = "sistema", situacao = "em_operacao" }), orgao));
        Assert.Equal((int)ErrorCode.PePdticFechado, ex.Error.Code);
        Assert.Equal("Este PDTIC foi enviado ao CGTIC e não muda até a decisão.", ex.Error.Message);
        Assert.False((await Registros.ListarAsync(dono, "ativos", await Orgao())).PodeEditar);
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(async () => await Documentos.GerarPdfAsync(pdtic.Id, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(() => EnviarAsync(pdtic.Id)));
        Assert.All((await SituacaoAsync(pdtic.Id)).Passos, p => Assert.False(p.PodeEditar));
    }
}
