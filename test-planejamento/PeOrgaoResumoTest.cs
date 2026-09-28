using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A página do órgão (E8): tudo do órgão numa resposta. O PDTIC de referência (a vigente; sem ela,
/// o da elaboração; sem os dois, o mais recente), as versões, o nível com o histórico, o andamento
/// por etapa e o próximo passo, a linha da conformidade, o "não se aplica", os comentários abertos,
/// as aprovações e deliberações, os documentos com o caminho do download, os ciclos (a lista da
/// E7) e as inadimplências; e quem vê.
/// </summary>
public class PeOrgaoResumoTest : PePaineisTestBase
{
    private async Task<PeOrgaoResumoResponse> ResumoAsync(Models.Pgia.PgiaOrgao orgao, app.Models.User? user = null) =>
        await Paineis.ResumoAsync(orgao.Id, await ContextoDe(user ?? UserPeSgdi));

    [Fact]
    public async Task EmElaboracao_OAndamentoOsComentariosONaoSeAplicaEOsDocumentos()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        var pdtic = await ProntoParaEnviarAsync();
        // A equipe de elaboração opcional para a SES, marcada como "não se aplica"
        await AjustarPassosAsync(OrgaoSes, ("preparacao.equipe", "opcional"));
        await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("preparacao.equipe").Id,
            new PeNaoSeAplicaDTO { Justificativa = "A equipe é a mesma do SGTIC." }, await Orgao());
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = Passo("diagnostico.ativos").Id, Texto = "Faltou o datacenter." }, await Sgdi());
        InadimplenciaNoBanco(OrgaoSes, "notificado");

        var resumo = await ResumoAsync(OrgaoSes);

        Assert.Equal((OrgaoSes.Id, "SES", "Secretaria de Estado de Saúde"), (resumo.Orgao.Id, resumo.Orgao.Sigla, resumo.Orgao.Nome));
        Assert.Equal((NivelId("basico"), "Básico", false), (resumo.Nivel.Id!.Value, resumo.Nivel.Nome, resumo.Nivel.Padrao));
        var troca = Assert.Single(resumo.Nivel.Historico);
        Assert.Equal(("Básico", "Teste", EmailAdmin), (troca.NivelNome, troca.Justificativa, troca.AlteradoPor));

        Assert.Equal((pdtic.Id, "em_elaboracao"), (resumo.Pdtic!.Id, resumo.Pdtic.Situacao));
        Assert.Single(resumo.Versoes);

        // Básico: 6 etapas; a etapa 1 com os 6 obrigatórios feitos (F3: a equipe, opcional pelo ajuste e
        // marcada como "não se aplica", fica fora da conta dos obrigatórios e dos opcionais feitos); a
        // 2 com os ativos em atenção
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, resumo.Andamento.Select(e => e.Etapa));
        Assert.Equal(("Prepare o PDTIC", 6, 6, 0), (resumo.Andamento[0].Titulo, resumo.Andamento[0].Feitos, resumo.Andamento[0].Total,
            resumo.Andamento[0].OpcionaisFeitos));
        Assert.Equal((3, 4), (resumo.Andamento[1].Feitos, resumo.Andamento[1].Total));
        // Etapa 3: o envio e a deliberação pendentes; a publicação aguardando a aprovação
        Assert.Equal((5, 8, 0, 1), (resumo.Andamento[2].Feitos, resumo.Andamento[2].Total, resumo.Andamento[2].Atrasados, resumo.Andamento[2].Aguardando));
        Assert.All(resumo.Andamento.Skip(3), e => Assert.Equal(e.Total, e.Aguardando));
        Assert.Equal("2.2", resumo.ProximoPasso);

        Assert.Equal((pdtic.Id, "em_elaboracao"), (resumo.Conformidade.PdticId!.Value, resumo.Conformidade.PdticSituacao));
        var naoSeAplica = Assert.Single(resumo.NaoSeAplica);
        Assert.Equal(("1.4", "Monte a equipe de elaboração", "A equipe é a mesma do SGTIC.", UserOrgaoSes.Email),
            (naoSeAplica.PassoNumero, naoSeAplica.PassoTitulo, naoSeAplica.Justificativa, naoSeAplica.MarcadoPor));
        var comentario = Assert.Single(resumo.ComentariosAbertos);
        Assert.Equal((Passo("diagnostico.ativos").Id, "2.2", "Faltou o datacenter.", "Sérgio da SGDI"),
            (comentario.PassoId, comentario.PassoNumero, comentario.Texto, comentario.AutorNome));

        // A aprovação do SGTIC, no passo do envio (3.6 no Básico)
        var aprovacao = Assert.Single(resumo.Aprovacoes);
        Assert.Equal(("3.6", "Aprovação do SGTIC", "aprovado", new DateOnly(2026, 9, 1)),
            (aprovacao.PassoNumero, aprovacao.Rotulo, aprovacao.DecisaoValor, aprovacao.Data!.Value));
        Assert.Equal("Aprovado", aprovacao.Decisao);
        Assert.EndsWith("nº 3/2026", aprovacao.Ato);
        Assert.Empty(resumo.Deliberacoes);

        var documento = Assert.Single(resumo.Documentos);
        Assert.Equal(("pdtic", "PDTIC 1.0", 1, "minuta"), (documento.Tipo, documento.Rotulo, documento.Numero, documento.Situacao));
        Assert.Equal($"api/planejamento/pdtic/{pdtic.Id}/documento/versoes/1/arquivo", documento.Url);
        Assert.True(documento.Paginas > 0);
        Assert.Empty(resumo.Ciclos);
        Assert.Equal("notificado", Assert.Single(resumo.Inadimplencias).Situacao);
        Assert.Equal("notificado", resumo.Conformidade.Inadimplencia!.Situacao);
    }

    [Fact]
    public async Task EmAcompanhamento_OsCiclosDaListaDaE7_OsRelatoriosEAsDeliberacoes()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        await PreencherCicloAsync(id, t1.Id);
        await FecharAsync(t1.Id);

        var resumo = await ResumoAsync(OrgaoSes);

        // O primeiro dado do monitoramento pôs o PDTIC em acompanhamento
        Assert.Equal((id, "em_acompanhamento"), (resumo.Pdtic!.Id, resumo.Pdtic.Situacao));
        Assert.Equal(16, resumo.Ciclos.Count);
        var primeiro = resumo.Ciclos[0];
        Assert.Equal((t1.Id, Trimestre1, "fechado", id), (primeiro.Id, primeiro.Rotulo, primeiro.Situacao, primeiro.PdticId));
        Assert.Equal(1, primeiro.Relatorio!.Numero);
        // O 2º trimestre venceu sem fechar: atrasado no ciclo, na etapa 5 e na conformidade
        Assert.Equal("atrasado", resumo.Ciclos[1].Situacao);
        Assert.True(resumo.Andamento.Single(e => e.Titulo == "Monitore").Atrasados > 0);
        Assert.False(resumo.Conformidade.Itens["acompanhamento_em_dia"].Atende);

        var ra = resumo.Documentos.Single(d => d.Tipo == "ra");
        Assert.Equal(("Relatório de acompanhamento · 2026 · 1º trimestre (PDTIC 1.0)", 1, t1.Id), (ra.Rotulo, ra.Numero, ra.CicloId!.Value));
        Assert.Equal($"api/planejamento/pdtic/{id}/ciclos/{t1.Id}/relatorio/versoes/1/arquivo", ra.Url);

        // O RR e a deliberação do PDTIC (a forma da fila da E3)
        await Documentos.GerarPdfAsync(service.Planejamento.PeDocAlvo.Rr(id), await Orgao());
        DeliberacaoAguardandoNoBanco(id);
        var depois = await ResumoAsync(OrgaoSes);
        var rr = depois.Documentos.Single(d => d.Tipo == "rr");
        Assert.Equal(("Relatório de resultados (PDTIC 1.0)", $"api/planejamento/pdtic/{id}/relatorio-resultados/versoes/1/arquivo"), (rr.Rotulo, rr.Url));
        var deliberacao = Assert.Single(depois.Deliberacoes);
        Assert.Equal(("aguardando", "PDTIC SES 1.0", "SES"), (deliberacao.Situacao, deliberacao.Titulo, deliberacao.OrgaoSigla));
    }

    [Fact]
    public async Task SemPdtic_EEncerrado()
    {
        var semPdtic = await ResumoAsync(OrgaoSeec);
        Assert.Null(semPdtic.Pdtic);
        Assert.Empty(semPdtic.Versoes);
        Assert.Empty(semPdtic.Andamento);
        Assert.Null(semPdtic.ProximoPasso);
        Assert.Equal((0, "baixa"), (semPdtic.Conformidade.Percentual, semPdtic.Conformidade.Grupo));
        Assert.Empty(semPdtic.Documentos);
        Assert.Equal(("Básico", true), (semPdtic.Nivel.Nome, semPdtic.Nivel.Padrao));
        Assert.Empty(semPdtic.Nivel.Historico);

        // O PDTIC encerrado continua sendo o da página, e a conformidade não tem PDTIC em vigor para
        // avaliar: a linha mostra o encerrado, como o painel (F2), com nenhum item atendido
        var id = await PublicadoHojeAsync();
        Situacao(id, PeDominios.SituacaoPdtic.Encerrado);
        var encerrado = await ResumoAsync(OrgaoSes);
        Assert.Equal((id, "encerrado"), (encerrado.Pdtic!.Id, encerrado.Pdtic.Situacao));
        Assert.Null(encerrado.ProximoPasso);
        Assert.Equal((id, "encerrado"), (encerrado.Conformidade.PdticId!.Value, encerrado.Conformidade.PdticSituacao));
        Assert.Equal((0, 0), (encerrado.Conformidade.Atendidos, encerrado.Conformidade.Percentual));
        Assert.NotEmpty(encerrado.Andamento);
        Assert.Single(encerrado.Deliberacoes);
    }

    [Fact]
    public async Task QuemVe_OsPapeisGlobaisEOProprioOrgao()
    {
        await AbrirSesAsync();

        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserPeAdmin, UserAdminGeral, UserOrgaoSes, UserConsultaSes })
            Assert.NotNull((await ResumoAsync(OrgaoSes, user)).Pdtic);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => ResumoAsync(OrgaoSes, UserOrgaoSeec)));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => ResumoAsync(OrgaoSeec, UserConsultaSes)));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => ResumoAsync(OrgaoSes, UserSemPapel)));
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(async () => await Paineis.ResumoAsync(999999, await Sgdi())));
        // A equipe do órgão lê a página do próprio órgão com o PDTIC editável; a SGDI, não
        Assert.True((await ResumoAsync(OrgaoSes, UserOrgaoSes)).Pdtic!.PodeEditar);
        Assert.False((await ResumoAsync(OrgaoSes)).Pdtic!.PodeEditar);
        Assert.Empty(Context.PeCiclos.AsNoTracking());
    }
}
