using app.Auth;
using app.Models;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Ponto único de autorização do módulo: contexto de quem está logado (papel, admin
/// geral e órgão resolvido pela unidade, como no PGIA) e as três perguntas desta
/// entrega (gerir pessoas, ver todos os órgãos, ser do órgão). Também os domínios que
/// o resto do módulo usa (papéis e o id do módulo).
/// </summary>
public class PePermissionServiceTest : PeTestBase
{
    // ── Domínios ──────────────────────────────────────────────────────────────

    [Fact]
    public void Papeis_CincoCodigos_NuloNaoEValido()
    {
        Assert.Equal(new[] { "pe_admin", "pe_sgdi", "pe_cgtic", "pe_orgao", "pe_orgao_consulta" }, PapeisPlanejamento.Todos);
        Assert.All(PapeisPlanejamento.Todos, p => Assert.True(PapeisPlanejamento.EhValido(p)));

        // No módulo não existe acesso sem papel: nulo não é papel válido
        Assert.False(PapeisPlanejamento.EhValido(null));
        Assert.False(PapeisPlanejamento.EhValido(""));
        Assert.False(PapeisPlanejamento.EhValido("PE_ADMIN"));
        Assert.False(PapeisPlanejamento.EhValido(PapeisPgia.Sgdi));
    }

    [Fact]
    public void Papeis_CadaUmEOuDeOrgaoOuGlobal()
    {
        Assert.True(PapeisPlanejamento.EhDeOrgao(PapeisPlanejamento.Orgao));
        Assert.True(PapeisPlanejamento.EhDeOrgao(PapeisPlanejamento.OrgaoConsulta));
        Assert.True(PapeisPlanejamento.EhGlobal(PapeisPlanejamento.Admin));
        Assert.True(PapeisPlanejamento.EhGlobal(PapeisPlanejamento.Sgdi));
        Assert.True(PapeisPlanejamento.EhGlobal(PapeisPlanejamento.Cgtic));

        Assert.All(PapeisPlanejamento.Todos, p =>
            Assert.True(PapeisPlanejamento.EhDeOrgao(p) ^ PapeisPlanejamento.EhGlobal(p)));
        Assert.False(PapeisPlanejamento.EhDeOrgao(null));
        Assert.False(PapeisPlanejamento.EhGlobal(null));
    }

    [Fact]
    public void Modulo_EntraNasTresListas_EtemPoliticaPropria()
    {
        Assert.Equal("planejamento", ModulosSgdp.Planejamento);
        Assert.Contains(ModulosSgdp.Planejamento, ModulosSgdp.Todos);
        Assert.Contains(ModulosSgdp.Planejamento, ModulosSgdp.Concedidos);
        Assert.Contains(ModulosSgdp.Planejamento, ModulosSgdp.Liberaveis);
        Assert.Equal("modulo:planejamento", ModulosSgdp.PoliticaPlanejamento);
        Assert.True(ModulosSgdp.EhValido(ModulosSgdp.Planejamento));
    }

    // ── Contexto de quem está logado ──────────────────────────────────────────

    [Fact]
    public async Task Contexto_EquipeDoOrgao_TrazPapelEOrgaoDaUnidade()
    {
        var ctx = await ContextoDe(UserOrgaoSes);

        Assert.Equal(UserOrgaoSes.Id, ctx.UserId);
        Assert.Equal(UserOrgaoSes.Email, ctx.Email);
        Assert.Equal(PapeisPlanejamento.Orgao, ctx.Papel);
        Assert.False(ctx.EhAdminGeral);
        Assert.Equal(OrgaoSes.Id, ctx.OrgaoId);
        Assert.Equal("SES", ctx.OrgaoSigla);
        Assert.Equal(OrgaoSes.Nome, ctx.OrgaoNome);
        Assert.Equal(UnidadeSes.id, ctx.UnidadeId);
        Assert.Equal(UnidadeSes.Nome, ctx.UnidadeNome);
    }

    [Fact]
    public async Task Contexto_AdminGeral_SemPapel()
    {
        var ctx = await ContextoDe(UserAdminGeral);

        Assert.True(ctx.EhAdminGeral);
        Assert.Null(ctx.Papel);
        Assert.Null(ctx.OrgaoId);   // unidade central não tem órgão
    }

    [Fact]
    public async Task Contexto_OrgaoDesativado_NaoResolve()
    {
        // Mesma regra do PGIA: órgão desativado não dá escopo (falha fechada)
        OrgaoSes.Ativo = false;
        Context.SaveChanges();

        var ctx = await ContextoDe(UserOrgaoSes);

        Assert.Equal(PapeisPlanejamento.Orgao, ctx.Papel);
        Assert.Null(ctx.OrgaoId);
        Assert.Equal(UnidadeSes.Nome, ctx.UnidadeNome);
    }

    [Fact]
    public async Task Contexto_UsuarioAindaNaoCadastrado_Nulo()
    {
        Assert.Null(await Permissoes.GetContextAsync(PrincipalDe("kc-novo", "novo@df.gov.br", Perfis.Basico)));
        Assert.Null(await Permissoes.GetContextAsync(PrincipalDe(null, null, Perfis.Admin)));
    }

    [Fact]
    public async Task Contexto_PeloKeycloakIdAntesDoEmail()
    {
        // Mesma ordem do GetOrCreateUserAsync: o sub decide quando os dois casam
        var ctx = await Permissoes.GetContextAsync(PrincipalDe(UserPeSgdi.KeycloakId, UserOrgaoSes.Email, Perfis.Basico));

        Assert.Equal(UserPeSgdi.Id, ctx!.UserId);
        Assert.Equal(PapeisPlanejamento.Sgdi, ctx.Papel);
    }

    [Fact]
    public async Task Contexto_PreCadastroSemKeycloakId_EncontradoPeloEmail()
    {
        var preCadastro = NovoUser("pre@saude.df.gov.br", "Pré-cadastro", Perfis.Basico, UnidadeSes, jaEntrou: false);
        Context.SaveChanges();

        var ctx = await Permissoes.GetContextAsync(PrincipalDe("kc-ainda-nao-gravado", preCadastro.Email, Perfis.Basico));

        Assert.Equal(preCadastro.Id, ctx!.UserId);
        Assert.Equal(OrgaoSes.Id, ctx.OrgaoId);
    }

    // ── As três perguntas desta entrega ───────────────────────────────────────

    public static IEnumerable<object[]> Pessoas() => new[]
    {
        new object[] { "paula.admin@sgdi.df.gov.br", true, true },   // pe_admin
        new object[] { "admin@subgd.df.gov.br", true, true },        // admin geral sem papel
        new object[] { "sergio@sgdi.df.gov.br", false, true },       // pe_sgdi
        new object[] { "carla@cgtic.df.gov.br", false, true },       // pe_cgtic
        new object[] { "otavio@saude.df.gov.br", false, false },     // pe_orgao
        new object[] { "cecilia@saude.df.gov.br", false, false },    // pe_orgao_consulta
        new object[] { "bruno@economia.df.gov.br", false, false }    // sem papel
    };

    [Theory]
    [MemberData(nameof(Pessoas))]
    public async Task PodeGerirPessoas_EVeTodosOsOrgaos(string email, bool gerePessoas, bool veTodos)
    {
        var ctx = await ContextoDe(UserPorEmail[email]);

        Assert.Equal(gerePessoas, Permissoes.PodeGerirPessoas(ctx));
        Assert.Equal(veTodos, Permissoes.VeTodosOsOrgaos(ctx));
    }

    [Fact]
    public async Task EhDoOrgao_SoPapelDeOrgaoNoProprioOrgao()
    {
        var equipe = await ContextoDe(UserOrgaoSes);
        var consulta = await ContextoDe(UserConsultaSes);

        Assert.True(Permissoes.EhDoOrgao(equipe, OrgaoSes.Id));
        Assert.True(Permissoes.EhDoOrgao(consulta, OrgaoSes.Id));
        Assert.False(Permissoes.EhDoOrgao(equipe, OrgaoSeec.Id));

        // Papéis globais e o admin geral enxergam todos, mas não são "do órgão"
        Assert.False(Permissoes.EhDoOrgao(await ContextoDe(UserPeAdmin), OrgaoSes.Id));
        Assert.False(Permissoes.EhDoOrgao(await ContextoDe(UserAdminGeral), OrgaoSes.Id));

        // Unidade da SEEC, mas sem papel no módulo
        Assert.False(Permissoes.EhDoOrgao(await ContextoDe(UserSemPapel), OrgaoSeec.Id));
    }

    [Fact]
    public async Task EhDoOrgao_PapelDeOrgaoSemUnidade_NaoEDeNenhum()
    {
        DarPapel(UserSemUnidade, PapeisPlanejamento.Orgao);
        var ctx = await ContextoDe(UserSemUnidade);

        Assert.Null(ctx.OrgaoId);
        Assert.False(Permissoes.EhDoOrgao(ctx, OrgaoSes.Id));
        Assert.False(Permissoes.VeTodosOsOrgaos(ctx));
    }

    [Fact]
    public async Task OrgaosPorUnidade_EmLote_SoOrgaoAtivo()
    {
        OrgaoSeec.Ativo = false;
        Context.SaveChanges();

        var orgaos = await Permissoes.OrgaosPorUnidadeAsync(new[] { UnidadeSes.id, UnidadeSeec.id, UnidadeCentral.id, UnidadeSes.id });

        var ses = Assert.Single(orgaos).Value;
        Assert.Equal(new service.Planejamento.PeOrgaoResumo(OrgaoSes.Id, "SES", OrgaoSes.Nome), ses);
        Assert.Empty(await Permissoes.OrgaosPorUnidadeAsync(Array.Empty<Guid>()));
    }
}
