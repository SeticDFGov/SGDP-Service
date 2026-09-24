using api.Planejamento;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Quem edita o quê, pela situação do PDTIC e pela etapa (E7, rodada A; substitui o "em
/// elaboração ou devolvido" da E4): etapas 1 a 3 em elaboração ou devolvido; a publicação em
/// aprovado ou publicado; etapas 4 a 7 em publicado ou em acompanhamento; em aprovação,
/// encerrado e substituído, nada. Vale para os registros (o PodeEditar da lista e o 409 ao
/// gravar), o PodeEditar de cada passo, o "não se aplica", o documento e os fluxos.
/// </summary>
public class PeEdicaoPorSituacaoTest : PeAprovacaoTestBase
{
    [Theory]
    [InlineData("em_elaboracao", true, false, false)]
    [InlineData("devolvido", true, false, false)]
    [InlineData("em_aprovacao", false, false, false)]
    [InlineData("aprovado", false, true, false)]
    [InlineData("publicado", false, true, true)]
    [InlineData("em_acompanhamento", false, false, true)]
    [InlineData("encerrado", false, false, false)]
    [InlineData("substituido", false, false, false)]
    public async Task Registros_PelaSituacaoEPelaEtapa(string situacao, bool elaboracao, bool publicacao, bool acompanhamento)
    {
        var pdtic = await AbrirSesAsync();
        Situacao(pdtic.Id, situacao);
        var dono = PeDono.DoPdtic(pdtic.Id);
        var orgao = await Orgao();

        async Task Conferir(string secao, string passo, bool edita, object dados)
        {
            Assert.Equal(edita, (await Registros.ListarAsync(dono, secao, orgao)).PodeEditar);
            Assert.Equal(edita, (await PassoDaSituacaoAsync(pdtic.Id, passo)).PodeEditar);
            if (edita)
                await Registros.CriarAsync(dono, secao, Salvar(dados), orgao);
            else
                Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(() => Registros.CriarAsync(dono, secao, Salvar(dados), orgao)));
        }

        await Conferir("nomes", "preparacao.nomes", elaboracao, new
        {
            comite = "SGTIC", equipe_elaboracao = "Equipe", autoridade_cargo = "Secretária", autoridade_nome = "Maria", unidade_tic = "SUTIC"
        });
        await Conferir("publicacao", "planejamento.publicacao", publicacao, new { data = "2026-09-15", endereco = Endereco });
        await Conferir("responsavel_acompanhamento", "plano-acompanhamento.quem-acompanha", acompanhamento, new { modalidade = "mesma_equipe" });

        // A deliberação nunca se edita pela equipe; a consulta não edita nada
        Assert.False((await PassoDaSituacaoAsync(pdtic.Id, "planejamento.deliberacao-cgtic")).PodeEditar);
        Assert.All((await SituacaoAsync(pdtic.Id, UserConsultaSes)).Passos, p => Assert.False(p.PodeEditar));
        Assert.False((await Registros.ListarAsync(dono, "nomes", await ContextoDe(UserConsultaSes))).PodeEditar);
    }

    [Theory]
    [InlineData("em_elaboracao", "")]
    [InlineData("aprovado", "As etapas 1 a 3 não mudam depois do envio ao CGTIC. Para mudar o PDTIC aprovado, abra uma revisão.")]
    [InlineData("publicado", "As etapas 1 a 3 não mudam depois do envio ao CGTIC. Para mudar o PDTIC aprovado, abra uma revisão.")]
    [InlineData("encerrado", "Este PDTIC foi encerrado e não muda mais.")]
    [InlineData("substituido", "Esta versão do PDTIC foi substituída pela revisão aprovada e não muda mais.")]
    public async Task DocumentoEFluxos_SoNaElaboracao(string situacao, string mensagem)
    {
        var pdtic = await AbrirSesAsync();
        var bloco = BlocoDoModelo("introducao").Id;
        Situacao(pdtic.Id, situacao);
        var orgao = await Orgao();

        var documento = await Documentos.ObterAsync(pdtic.Id, orgao);
        var fluxo = await Fluxos().ObterAsync(pdtic.Id, "preparacao", orgao);
        if (mensagem.Length == 0)
        {
            Assert.True(documento.PodeEditar);
            Assert.True(fluxo.PodeEditar);
            await Documentos.SalvarTextoAsync(pdtic.Id, bloco, Json(Rico("Texto do órgão.")), orgao);
            await Documentos.GerarPdfAsync(pdtic.Id, orgao);
            return;
        }
        Assert.False(documento.PodeEditar);
        Assert.False(fluxo.PodeEditar);
        var ex = await Assert.ThrowsAsync<ApiException>(() => Documentos.SalvarTextoAsync(pdtic.Id, bloco, Json(Rico("Texto do órgão.")), orgao));
        Assert.Equal((int)ErrorCode.PePdticFechado, ex.Error.Code);
        Assert.Equal(mensagem, ex.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(() => Documentos.GerarPdfAsync(pdtic.Id, orgao)));
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(() => Fluxos().RestaurarAsync(pdtic.Id, "preparacao", orgao)));
    }

    [Fact]
    public async Task NaoSeAplica_DasEtapas4a7_SoDepoisDaPublicacao()
    {
        // O passo 4.1 opcional para a SES (ajuste), para aceitar o "não se aplica"
        await AjustarPassosAsync(OrgaoSes, ("plano-acompanhamento.quem-acompanha", "opcional"));
        var pdtic = await AbrirSesAsync();
        var passo = Passo("plano-acompanhamento.quem-acompanha").Id;
        var dto = new PeNaoSeAplicaDTO { Justificativa = "O acompanhamento fica com a equipe de elaboração." };

        var ex = await Assert.ThrowsAsync<ApiException>(async () => await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, passo, dto, await Orgao()));
        Assert.Equal((int)ErrorCode.PePdticFechado, ex.Error.Code);
        Assert.Equal("Este passo fica disponível depois da publicação do PDTIC.", ex.Error.Message);

        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado);
        var marcado = await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, passo, dto, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, marcado.Situacao);
        Assert.True(marcado.PodeEditar);
    }

    private PeFluxoService Fluxos() => new(Context, Registros, Permissoes);
}
