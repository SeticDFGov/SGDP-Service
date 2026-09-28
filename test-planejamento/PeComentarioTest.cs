using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Comentários dos passos do PDTIC (E4), pelo quadro de papéis do plano (seção 4.1): o
/// administrador do módulo, a SGDI e o admin geral comentam (a Secretaria do CGTIC não); a
/// equipe do órgão responde e resolve; quem comentou também resolve; um nível de resposta;
/// comentário resolvido não recebe resposta; quem vê o órgão lê, com filtro por passo.
/// </summary>
public class PeComentarioTest : PePdticTestBase
{
    private long PassoAbrangencia => Passo("preparacao.abrangencia").Id;

    [Fact]
    public async Task SgdiEAdministradorComentam_EquipeDoOrgaoRespondeEResolve()
    {
        var pdtic = await AbrirSesAsync();

        var conversa = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "  Falta a vigência.  " },
            await ContextoDe(UserPeSgdi));
        Assert.Equal("Falta a vigência.", conversa.Texto);
        Assert.Equal("Sérgio da SGDI", conversa.AutorNome);
        Assert.Equal(UserPeSgdi.Email, conversa.AutorEmail);
        Assert.Equal(PassoAbrangencia, conversa.PassoId);
        Assert.Null(conversa.ResolvidoEm);
        Assert.Empty(conversa.Respostas);
        Assert.Equal("Paula Administradora", (await Comentarios.CriarAsync(pdtic.Id,
            new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "Outro" }, await ContextoDe(UserPeAdmin))).AutorNome);

        // A Secretaria do CGTIC, a equipe do órgão e a consulta não abrem comentário
        foreach (var user in new[] { UserPeCgtic, UserOrgaoSes, UserConsultaSes })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "Dúvida" }, await ContextoDe(user))));

        // A equipe do órgão responde (a resposta vem na conversa); quem comentou, a consulta e a CGTIC, não
        var respondida = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = conversa.Id, Texto = "Incluída." }, await Orgao());
        Assert.Equal(conversa.Id, respondida.Id);
        var resposta = Assert.Single(respondida.Respostas);
        Assert.Equal("Incluída.", resposta.Texto);
        Assert.Equal("Otávio da Saúde", resposta.AutorNome);
        foreach (var user in new[] { UserPeSgdi, UserConsultaSes, UserPeCgtic, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = conversa.Id, Texto = "Ok" }, await ContextoDe(user))));

        // Todos que veem o órgão leem, inclusive a consulta e a Secretaria do CGTIC
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserPeCgtic, UserAdminGeral })
            Assert.Single((await Comentarios.ListarAsync(pdtic.Id, null, await ContextoDe(user)))[0].Respostas);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
            await Comentarios.ListarAsync(pdtic.Id, null, await ContextoDe(UserOrgaoSeec))));

        // A equipe resolve; resolvido não recebe resposta; resolver de novo não muda nada
        var resolvido = await Comentarios.ResolverAsync(conversa.Id, await Orgao());
        Assert.NotNull(resolvido.ResolvidoEm);
        Assert.Equal(UserOrgaoSes.Email, resolvido.ResolvidoPor);
        Assert.Equal(Codigo(ErrorCode.PeComentarioResolvido), await ErroAsync(async () =>
            await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = conversa.Id, Texto = "Mais uma" }, await Orgao())));
        var denovo = await Comentarios.ResolverAsync(conversa.Id, await ContextoDe(UserAdminGeral));
        Assert.Equal(UserOrgaoSes.Email, denovo.ResolvidoPor);
    }

    [Fact]
    public async Task QuemComentouResolve_OutroPapelGlobalNao()
    {
        var pdtic = await AbrirSesAsync();
        var conversa = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "Revise." },
            await ContextoDe(UserPeAdmin));

        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserConsultaSes, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Comentarios.ResolverAsync(conversa.Id, await ContextoDe(user))));
        Assert.Equal(UserPeAdmin.Email, (await Comentarios.ResolverAsync(conversa.Id, await ContextoDe(UserPeAdmin))).ResolvidoPor);
        Assert.Equal(Codigo(ErrorCode.PeComentarioNaoEncontrado), await ErroAsync(async () => await Comentarios.ResolverAsync(999, await Orgao())));
    }

    [Fact]
    public async Task Validacoes_Texto_Passo_UmNivelDeResposta()
    {
        var pdtic = await AbrirSesAsync();
        var sgdi = await ContextoDe(UserPeSgdi);

        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = " " }, sgdi)));
        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = new string('a', 2001) }, sgdi)));
        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { Texto = "Sem passo" }, sgdi)));
        // Passo fora da trilha do órgão (a SWOT não existe no Básico)
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = Passo("diagnostico.swot").Id, Texto = "Oi" }, sgdi)));

        var conversa = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "Primeiro" }, sgdi);
        var orgao = await Orgao();
        var comResposta = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = conversa.Id, Texto = "Resposta" }, orgao);
        var resposta = comResposta.Respostas[0];

        // Resposta a uma resposta, passo diferente do comentário e resolver a resposta: 400
        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = resposta.Id, Texto = "Tréplica" }, orgao)));
        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() => Comentarios.CriarAsync(pdtic.Id,
            new PeComentarioCriarDTO { PaiId = conversa.Id, PassoId = Passo("preparacao.nomes").Id, Texto = "Outro passo" }, orgao)));
        Assert.Equal(Codigo(ErrorCode.PeComentarioInvalido), await ErroAsync(() => Comentarios.ResolverAsync(resposta.Id, orgao)));
        Assert.Equal(Codigo(ErrorCode.PeComentarioNaoEncontrado), await ErroAsync(() =>
            Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = 999, Texto = "?" }, orgao)));

        // A resposta não se resolve sozinha
        Assert.Null(Context.PeComentarios.AsNoTracking().Single(c => c.Id == resposta.Id).ResolvidoEm);
    }

    [Fact]
    public async Task Lista_PorPasso_EmOrdem_ComAsRespostasDentro()
    {
        var pdtic = await AbrirSesAsync();
        var sgdi = await ContextoDe(UserPeSgdi);
        var orgao = await Orgao();
        var nomes = Passo("preparacao.nomes").Id;
        var primeiro = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "A" }, sgdi);
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = nomes, Texto = "B" }, sgdi);
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = PassoAbrangencia, Texto = "C" }, await ContextoDe(UserPeAdmin));
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = primeiro.Id, Texto = "A1" }, orgao);
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = primeiro.Id, Texto = "A2" }, orgao);

        var todos = await Comentarios.ListarAsync(pdtic.Id, null, orgao);
        Assert.Equal(new[] { "A", "B", "C" }, todos.Select(c => c.Texto));
        Assert.Equal(new[] { "A1", "A2" }, todos[0].Respostas.Select(r => r.Texto));

        var doPasso = await Comentarios.ListarAsync(pdtic.Id, PassoAbrangencia, orgao);
        Assert.Equal(new[] { "A", "C" }, doPasso.Select(c => c.Texto));
        Assert.Equal(2, doPasso[0].Respostas.Count);
    }
}
