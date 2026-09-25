using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Revisão do PDTIC (E7, rodada A): a versão seguinte ("1.1"), em elaboração, a partir da
/// vigente; exige a avaliação do comitê (6.3) com "revisar" quando o passo está na trilha do
/// órgão, senão a justificativa; copia os registros (códigos, ordem, sequências e ligações
/// refeitas), os fluxos, os textos e os capítulos do documento e os "não se aplica"; não copia
/// as seções dos passos de aprovação, de envio e de publicação, os comentários, as deliberações
/// nem as versões do documento; a vigente continua até a nova ser aprovada pelo CGTIC e aí fica
/// substituída; no máximo uma versão em elaboração e uma vigente por órgão.
/// </summary>
public class PeRevisaoTest : PeAprovacaoTestBase
{
    private async Task<PePdticResponse> RevisarAsync(long id, string? justificativa = "O órgão mudou de estrutura.", app.Models.User? user = null) =>
        await Aprovacao.RevisarAsync(id, new PeRevisaoDTO { Justificativa = justificativa }, await ContextoDe(user ?? UserOrgaoSes));

    private List<PeRegistro> RegistrosDe(long pdticId) =>
        Context.PeRegistros.AsNoTracking().Where(r => r.PdticId == pdticId).OrderBy(r => r.SecaoId).ThenBy(r => r.Ordem).ToList();

    [Fact]
    public async Task Revisao_NoBasico_ExigeAJustificativa_ECriaAVersao11()
    {
        var vigente = await PublicadoAsync();

        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria), await ErroAsync(() => RevisarAsync(vigente.Id, null)));
        var nova = await RevisarAsync(vigente.Id);

        Assert.Equal("1.1", nova.Versao);
        Assert.Equal(PeDominios.SituacaoPdtic.EmElaboracao, nova.Situacao);
        Assert.Equal(vigente.Id, nova.AnteriorId);
        Assert.Equal(vigente.Id, nova.Revisao!.AnteriorId);
        Assert.Equal("1.0", nova.Revisao.AnteriorVersao);
        Assert.Equal("O órgão mudou de estrutura.", nova.Revisao.Justificativa);
        Assert.Equal(new DateOnly(2026, 1, 1), nova.VigenciaInicio);
        Assert.Equal(new DateOnly(2029, 12, 31), nova.VigenciaFim);
        Assert.True(nova.PodeEditar);
        Assert.Null(nova.Deliberacao);
        Assert.Null(vigente.Revisao);

        // As duas versões convivem: a vigente segue publicada; o atual é a revisão; as versões, da mais nova
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, PdticNoBanco(vigente.Id).Situacao);
        Assert.Equal(nova.Id, (await Pdtics.AtualAsync(null, await Orgao()))!.Id);
        Assert.Equal(new[] { "1.1", "1.0" }, (await Pdtics.VersoesAsync(null, await Orgao())).Select(v => v.Versao));
        Assert.Equal(new[] { "1.1", "1.0" }, (await Pdtics.VersoesAsync(OrgaoSes.Id, await ContextoDe(UserPeSgdi))).Select(v => v.Versao));

        // A revisão recém-aberta recomenda o primeiro passo da etapa 2; depois de mexer, o fluxo normal
        var situacao = await SituacaoAsync(nova.Id);
        Assert.Equal(situacao.Passos.Single(p => p.Chave == "diagnostico.ambiente-tecnologico").Numero, situacao.ProximoPasso);
        await IncluirNoPdticAsync(nova.Id, "ativos", new { nome = "Rede nova", tipo = "rede", situacao = "em_implantacao" });
        Assert.Equal(situacao.Passos.Single(p => p.Chave == "planejamento.documento").Numero, (await SituacaoAsync(nova.Id)).ProximoPasso);

        // Uma versão em elaboração por vez: outra revisão e um PDTIC novo são recusados
        Assert.Equal(Codigo(ErrorCode.PeRevisaoEmAndamento), await ErroAsync(() => RevisarAsync(vigente.Id)));
        Assert.Equal(Codigo(ErrorCode.PePdticJaExiste), await ErroAsync(AbrirSesAsync));
    }

    [Fact]
    public async Task Revisao_CopiaOsDados_ComAsLigacoesRefeitas_SemOsDaAprovacao()
    {
        var (_, objetivo) = await VigenteAsync();
        await AjustarPassosAsync(OrgaoSes, ("preparacao.documentos-referencia", "opcional"));
        var pdtic = await ProntoParaEnviarAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var orgao = await Orgao();

        // Dados que a revisão leva: a meta ligada ao objetivo do PETIC-DF, um código apagado,
        // o fluxo adaptado, o texto e o capítulo do documento e o "não se aplica"
        var meta = (await Registros.ListarAsync(dono, "metas", orgao)).Registros.Single();
        var necessidade = (await Registros.ListarAsync(dono, "necessidades", orgao)).Registros.Single();
        await Registros.AtualizarAsync(dono, "metas", meta.Id, Salvar(null, new { necessidades = new[] { necessidade.Id }, objetivo_petic = new[] { objetivo.Id } }), orgao);
        var apagado = await IncluirNoPdticAsync(pdtic.Id, "ativos", new { nome = "Temporário", tipo = "outro", situacao = "em_desativacao" });
        await Registros.ExcluirAsync(dono, "ativos", apagado.Id, orgao);
        var fluxos = new service.Planejamento.PeFluxoService(Context, Registros, Permissoes);
        var definicao = await fluxos.ObterAsync(pdtic.Id, "preparacao", orgao);
        await fluxos.SalvarAsync(pdtic.Id, "preparacao", PeFluxoSalvarDTO.Ler(JsonSerializer.SerializeToElement(new { Nome = "Preparação da Saúde", definicao.Definicao })), orgao);
        var introducao = BlocoDoModelo("introducao").Id;
        await Documentos.SalvarTextoAsync(pdtic.Id, introducao, Json(Rico("Introdução da Saúde.")), orgao);
        await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("termos").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, orgao);
        await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("preparacao.documentos-referencia").Id,
            new PeNaoSeAplicaDTO { Justificativa = "Sem documentos além do PPA." }, orgao);
        var comentario = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = Passo("diagnostico.ativos").Id, Texto = "Confira." },
            await ContextoDe(UserPeSgdi));
        await Comentarios.ResolverAsync(comentario.Id, orgao);

        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);
        var nova = await RevisarAsync(pdtic.Id);

        // Os registros: os mesmos códigos e a mesma ordem, menos a aprovação do SGTIC e a publicação
        var antigos = RegistrosDe(pdtic.Id);
        var copiados = RegistrosDe(nova.Id);
        var semCopia = new[] { Secao("aprovacao_sgtic").Id, Secao("publicacao").Id };
        Assert.Equal(antigos.Where(r => !semCopia.Contains(r.SecaoId)).Select(r => (r.SecaoId, r.Codigo, r.Ordem, r.Dados)),
            copiados.Select(r => (r.SecaoId, r.Codigo, r.Ordem, r.Dados)));
        Assert.DoesNotContain(copiados, r => semCopia.Contains(r.SecaoId));
        Assert.All(copiados, r => Assert.DoesNotContain(r.Id, antigos.Select(a => a.Id)));

        // As ligações: com a seção, para a cópia; com o catálogo, o mesmo item
        var novaMeta = (await Registros.ListarAsync(PeDono.DoPdtic(nova.Id), "metas", orgao)).Registros.Single();
        var novaNecessidade = (await Registros.ListarAsync(PeDono.DoPdtic(nova.Id), "necessidades", orgao)).Registros.Single();
        Assert.Equal(novaNecessidade.Id, Assert.Single(novaMeta.Vinculos["necessidades"]).RegistroId);
        Assert.NotEqual(necessidade.Id, novaNecessidade.Id);
        Assert.Equal(objetivo.Id, Assert.Single(novaMeta.Vinculos["objetivo_petic"]).RegistroId);

        // A sequência: o código apagado não volta
        Assert.Equal("AT03", (await IncluirNoPdticAsync(nova.Id, "ativos", new { nome = "Novo", tipo = "sistema", situacao = "em_implantacao" })).Codigo);

        // Fluxo, texto, capítulo e "não se aplica" copiados
        var fluxo = await fluxos.ObterAsync(nova.Id, "preparacao", orgao);
        Assert.True(fluxo.Personalizado);
        Assert.Equal("Preparação da Saúde", fluxo.Nome);
        var documento = await Documentos.ObterAsync(nova.Id, orgao);
        var texto = Texto(documento, "introducao");
        Assert.True(texto.EditadoPeloOrgao);
        Assert.Equal("Introdução da Saúde.", TextoDe(texto.TextoBruto));
        Assert.True(Cap(documento, "termos").Oculto);
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, (await PassoDaSituacaoAsync(nova.Id, "preparacao.documentos-referencia")).Situacao);

        // Sem comentários, deliberações nem versões do documento
        Assert.Empty(Context.PeComentarios.Where(c => c.PdticId == nova.Id));
        Assert.Empty(Context.PeDeliberacoes.Where(d => d.ObjetoTipo == "pdtic" && d.ObjetoId == nova.Id));
        Assert.Empty(Context.PeDocVersoes.Where(v => v.PdticId == nova.Id));
        Assert.Empty(documento.Versoes);
    }

    [Fact]
    public async Task Revisao_ATextoRicoComImagem_DaVersaoRevista_ContinuaValendo()
    {
        var pdtic = await AbrirSesAsync();
        var imagem = await EnviarArquivoAsync(UserOrgaoSes, "organograma.png", PngDeVerdade());
        await PreencherElaboracaoAsync(pdtic.Id);
        var dono = PeDono.DoPdtic(pdtic.Id);
        var orgao = await Orgao();
        var ambiente = (await Registros.ListarAsync(dono, "diagnostico_ambiente", orgao)).Registros.Single();
        var comImagem = new Dictionary<string, object>
        {
            ["diagnostico"] = Doc(Paragrafo("A TIC em um desenho."), new { type = "image", attrs = new { src = $"api/planejamento/arquivos/{imagem.Id}", alt = "Organograma" } })
        };
        await Registros.AtualizarAsync(dono, "diagnostico_ambiente", ambiente.Id, Salvar(comImagem), orgao);
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);

        var nova = await RevisarAsync(pdtic.Id);
        var novoAmbiente = (await Registros.ListarAsync(PeDono.DoPdtic(nova.Id), "diagnostico_ambiente", orgao)).Registros.Single();
        // O mesmo texto (com a imagem da versão revista) grava na revisão, e o PDF sai
        await Registros.AtualizarAsync(PeDono.DoPdtic(nova.Id), "diagnostico_ambiente", novoAmbiente.Id, Salvar(comImagem), orgao);
        Assert.Equal(1, (await Documentos.GerarPdfAsync(nova.Id, orgao)).Numero);
        // A imagem continua do registro da versão revista (serve às duas versões)
        Assert.Equal(ambiente.Id, Context.PeArquivos.AsNoTracking().Single(a => a.Id == imagem.Id).DonoId);
        Assert.NotEmpty((await Arquivos.BaixarAsync(imagem.Id, await ContextoDe(UserConsultaSes))).Conteudo);
    }

    [Fact]
    public async Task Revisao_AprovadaPeloCgtic_SubstituiAVigente()
    {
        var vigente = await PublicadoAsync();
        var nova = await RevisarAsync(vigente.Id);

        // A revisão passa pelo caminho inteiro de novo: a minuta e a aprovação do SGTIC não vêm da anterior
        var envio = await Aprovacao.EnvioAsync(nova.Id, await Orgao());
        Assert.Equal(new[] { "planejamento.documento", "planejamento.aprovacao-sgtic" },
            envio.Pendencias.Select(p => Context.PePassos.AsNoTracking().Single(x => x.Id == p.PassoId).Chave));
        await Documentos.GerarPdfAsync(nova.Id, await Orgao());
        await AprovacaoSgticAsync(nova.Id);
        await EnviarAsync(nova.Id);
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, PdticNoBanco(vigente.Id).Situacao);

        await AprovarNoCgticAsync(nova.Id);
        Assert.Equal(PeDominios.SituacaoPdtic.Substituido, PdticNoBanco(vigente.Id).Situacao);
        Assert.Equal(PeDominios.SituacaoPdtic.Aprovado, PdticNoBanco(nova.Id).Situacao);
        Assert.Equal(nova.Id, (await Pdtics.AtualAsync(null, await Orgao()))!.Id);

        var publicada = await PublicarAsync(nova.Id);
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, publicada.Situacao);
        // A substituída não muda mais
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(() => RevisarAsync(vigente.Id)));
    }

    [Fact]
    public async Task Revisao_ComOPasso63NaTrilha_ExigeADecisaoRevisar()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.EmAcompanhamento);
        var numero = (await PassoDaSituacaoAsync(pdtic.Id, "avaliacao-intermediaria.avaliacao-comite")).Numero;

        var ex = await Assert.ThrowsAsync<ApiException>(() => RevisarAsync(pdtic.Id));
        Assert.Equal((int)ErrorCode.PeRevisaoRecusada, ex.Error.Code);
        Assert.Equal($"Para abrir a revisão, registre na avaliação intermediária (passo {numero}) a avaliação do comitê com a decisão \"Revisar o PDTIC\".",
            ex.Error.Message);

        // A decisão é a da avaliação intermediária (E7, rodada B: a seção é por ciclo de avaliação)
        var avaliacao = await Acompanhamento.CriarCicloAsync(pdtic.Id, new PeCicloCriarDTO { Tipo = "avaliacao" }, await Orgao());
        var registro = await IncluirNoPdticAsync(pdtic.Id, "avaliacao_comite", new { decisao = "seguir", data = "2026-06-30" }, cicloId: avaliacao.Id);
        Assert.Equal(Codigo(ErrorCode.PeRevisaoRecusada), await ErroAsync(() => RevisarAsync(pdtic.Id)));
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "avaliacao_comite", registro.Id,
            Salvar(new { decisao = "revisar", data = "2026-06-30" }), await Orgao(), avaliacao.Id);

        // Com a decisão do comitê, a justificativa é opcional
        var nova = await RevisarAsync(pdtic.Id, null);
        Assert.Equal("1.1", nova.Versao);
        Assert.Null(nova.Revisao!.Justificativa);
        // A avaliação do comitê é de um ciclo (e do passo de aprovação): não vai para a revisão, nem o ciclo
        Assert.Empty(RegistrosDe(nova.Id).Where(r => r.SecaoId == Secao("avaliacao_comite").Id));
        Assert.Empty(Context.PeCiclos.Where(c => c.PdticId == nova.Id));
    }

    [Fact]
    public async Task Revisao_SoDaVigente_EPelaEquipeDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(() => RevisarAsync(pdtic.Id)));

        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado);
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => RevisarAsync(pdtic.Id, user: user)));
        Assert.Equal("1.1", (await RevisarAsync(pdtic.Id, user: UserAdminGeral)).Versao);
    }

    [Fact]
    public void ProximaRevisao_OMesmoNumeroPrincipal_EAMaiorRevisaoMaisUm()
    {
        Assert.Equal("1.1", service.Planejamento.PeEdicaoPdtic.ProximaRevisao("1.0", new[] { "1.0" }));
        Assert.Equal("1.3", service.Planejamento.PeEdicaoPdtic.ProximaRevisao("1.1", new[] { "1.0", "1.1", "1.2" }));
        Assert.Equal("2.1", service.Planejamento.PeEdicaoPdtic.ProximaRevisao("2.0", new[] { "1.0", "1.1", "2.0" }));
    }
}
