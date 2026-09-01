using Microsoft.EntityFrameworkCore;
using service.Pgia;
using Xunit;

namespace test.pgia;

public class PgiaPermissionServiceTest : PgiaTestBase
{
    private readonly PgiaPermissionService _service;

    public PgiaPermissionServiceTest()
    {
        _service = new PgiaPermissionService(Context);
    }

    // ── Contexto ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contexto_ResolveOrgaoPelaUnidadeDoUsuario()
    {
        var ctx = await _service.GetContextAsync(UserOrgaoSes.Email);

        Assert.NotNull(ctx);
        Assert.Equal("pgia_orgao", ctx!.PapelPgia);
        Assert.Equal(OrgaoSes.Id, ctx.OrgaoId);
        Assert.False(ctx.IsAdmin);
    }

    /// <summary>
    /// C3 (falha fechada): órgão desativado não resolve mais o escopo — o papel de
    /// órgão de uma unidade cujo órgão foi desativado fica com OrgaoId nulo.
    /// </summary>
    [Fact]
    public async Task Contexto_OrgaoDesativadoNaoResolveEscopo()
    {
        OrgaoSes.Ativo = false;
        await Context.SaveChangesAsync();

        var ctx = await _service.GetContextAsync(UserOrgaoSes.Email);

        Assert.NotNull(ctx);
        Assert.Equal(UnidadeSes.id, ctx!.UnidadeId); // a unidade continua
        Assert.Null(ctx.OrgaoId);                    // mas o órgão inativo não conta
        Assert.False(_service.CanAccessOrgao(ctx, OrgaoSes.Id));
    }

    [Fact]
    public async Task Contexto_AdminDoSgdpTemPapelEfetivoAdmin()
    {
        var ctx = await _service.GetContextAsync(UserAdmin.Email);

        Assert.NotNull(ctx);
        Assert.True(ctx!.IsAdmin);
        Assert.Equal("admin", ctx.PapelEfetivo);
        Assert.Null(ctx.PapelPgia);
    }

    [Fact]
    public async Task Contexto_UsuarioInexistenteRetornaNulo()
    {
        var ctx = await _service.GetContextAsync("nao-existe@df.gov.br");
        Assert.Null(ctx);
    }

    // ── Matriz papel x ação x recurso ─────────────────────────────────────────

    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", true)]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("cgtic@sgdi.df.gov.br", true)]
    [InlineData("aud@auditoria.com", false)]
    [InlineData("comum@ses.df.gov.br", false)]
    public async Task CanView_Prazo_PorPapel(string email, bool esperado)
    {
        var ctx = await _service.GetContextAsync(email);
        Assert.Equal(esperado, _service.CanView(ctx!, PgiaResources.Prazo));
    }

    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", true)]
    [InlineData("sgdi@sgdi.df.gov.br", false)]
    [InlineData("cgtic@sgdi.df.gov.br", false)]
    [InlineData("aud@auditoria.com", false)]
    [InlineData("comum@ses.df.gov.br", false)]
    public async Task CanCreate_Designacao_PorPapel(string email, bool esperado)
    {
        var ctx = await _service.GetContextAsync(email);
        Assert.Equal(esperado, _service.CanCreate(ctx!, PgiaResources.Designacao));
    }

    /// <summary>
    /// A SGDI entrega o órgão pronto: os dados do órgão são escrita dela e do
    /// admin; o papel do órgão só visualiza (decisão do dono do produto).
    /// </summary>
    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("cgtic@sgdi.df.gov.br", false)]
    [InlineData("comum@ses.df.gov.br", false)]
    public async Task CanEdit_DadosDoOrgao_PorPapel(string email, bool esperado)
    {
        var ctx = await _service.GetContextAsync(email);
        Assert.Equal(esperado, _service.CanEdit(ctx!, PgiaResources.OrgaoDados));
    }

    /// <summary>
    /// Matrícula, cargo e vínculo das pessoas também são pré-cadastro da SGDI;
    /// o órgão mantém a visualização.
    /// </summary>
    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("cgtic@sgdi.df.gov.br", false)]
    [InlineData("aud@auditoria.com", false)]
    [InlineData("comum@ses.df.gov.br", false)]
    public async Task CanEdit_AgenteInfo_PorPapel(string email, bool esperado)
    {
        var ctx = await _service.GetContextAsync(email);
        Assert.Equal(esperado, _service.CanEdit(ctx!, PgiaResources.AgenteInfo));
    }

    [Fact]
    public async Task CanView_AgenteInfo_OrgaoContinuaVendoAsPessoas()
    {
        var orgao = await _service.GetContextAsync(UserOrgaoSes.Email);
        Assert.True(_service.CanView(orgao!, PgiaResources.AgenteInfo));
    }

    /// <summary>
    /// Vínculo de acesso (papel PGIA + unidade) é exclusivo da SGDI e do admin —
    /// gate dos endpoints de gestão de pessoas, que não têm visualização própria.
    /// </summary>
    [Theory]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]
    [InlineData("cgtic@sgdi.df.gov.br", false)]
    [InlineData("aud@auditoria.com", false)]
    [InlineData("comum@ses.df.gov.br", false)]
    public async Task CanEdit_PessoaVinculo_SomenteSgdiEAdmin(string email, bool esperado)
    {
        var ctx = await _service.GetContextAsync(email);
        Assert.Equal(esperado, _service.CanEdit(ctx!, PgiaResources.PessoaVinculo));
    }

    /// <summary>
    /// As designações continuam com o órgão, no próprio órgão (não mudaram).
    /// </summary>
    [Fact]
    public async Task CanEdit_Designacao_ContinuaComOOrgao()
    {
        var orgao = await _service.GetContextAsync(UserOrgaoSes.Email);

        Assert.True(_service.CanEdit(orgao!, PgiaResources.Designacao));
        Assert.True(_service.CanCreate(orgao!, PgiaResources.Designacao));
        Assert.True(_service.CanAccessOrgao(orgao!, OrgaoSes.Id));
        Assert.False(_service.CanAccessOrgao(orgao!, OrgaoSeec.Id));
    }

    [Fact]
    public async Task CanCreate_Orgao_SomenteAdminESgdi()
    {
        var admin = await _service.GetContextAsync(UserAdmin.Email);
        var sgdi = await _service.GetContextAsync(UserSgdi.Email);
        var orgao = await _service.GetContextAsync(UserOrgaoSes.Email);

        Assert.True(_service.CanCreate(admin!, PgiaResources.Orgao));
        Assert.True(_service.CanCreate(sgdi!, PgiaResources.Orgao));
        Assert.False(_service.CanCreate(orgao!, PgiaResources.Orgao));
    }

    // ── Escopo de dados por órgão ─────────────────────────────────────────────

    [Fact]
    public async Task PapelOrgao_NaoAcessaOutroOrgao()
    {
        var ctx = await _service.GetContextAsync(UserOrgaoSes.Email);

        Assert.True(_service.CanAccessOrgao(ctx!, OrgaoSes.Id));
        Assert.False(_service.CanAccessOrgao(ctx!, OrgaoSeec.Id));
    }

    [Fact]
    public async Task SgdiCgticEAdmin_AcessamQualquerOrgao()
    {
        foreach (var email in new[] { UserSgdi.Email, UserCgtic.Email, UserAdmin.Email })
        {
            var ctx = await _service.GetContextAsync(email);
            Assert.True(_service.CanAccessOrgao(ctx!, OrgaoSes.Id));
            Assert.True(_service.CanAccessOrgao(ctx!, OrgaoSeec.Id));
        }
    }

    [Fact]
    public async Task UsuarioSemPapel_NaoAcessaOrgaoAlgum()
    {
        var ctx = await _service.GetContextAsync(UserSemPapel.Email);

        Assert.False(_service.CanAccessOrgao(ctx!, OrgaoSes.Id));
        Assert.False(_service.CanAccessOrgao(ctx!, OrgaoSeec.Id));
    }

    [Fact]
    public async Task FiltroDeOrgaos_PapelOrgaoSoVeOProprio()
    {
        var ctx = await _service.GetContextAsync(UserOrgaoSes.Email);
        var orgaos = await _service.GetFilteredOrgaosQuery(ctx!).ToListAsync();

        Assert.Single(orgaos);
        Assert.Equal(OrgaoSes.Id, orgaos[0].Id);
    }

    [Fact]
    public async Task FiltroDeOrgaos_SgdiVeTodos()
    {
        var ctx = await _service.GetContextAsync(UserSgdi.Email);
        var orgaos = await _service.GetFilteredOrgaosQuery(ctx!).ToListAsync();

        Assert.Equal(2, orgaos.Count);
    }

    [Fact]
    public async Task FiltroDeOrgaos_SemPapelNaoVeNada()
    {
        var ctx = await _service.GetContextAsync(UserSemPapel.Email);
        var orgaos = await _service.GetFilteredOrgaosQuery(ctx!).ToListAsync();

        Assert.Empty(orgaos);
    }
}
