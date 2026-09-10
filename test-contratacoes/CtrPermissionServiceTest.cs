using app.Auth;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Matriz papel x ação x recurso: admin do SGDP e ctr_analise entram em tudo;
/// qualquer outro perfil sem o papel fica de fora (o módulo é interno da SGDI).
/// </summary>
public class CtrPermissionServiceTest : CtrTestBase
{
    private static readonly string[] Recursos =
    {
        CtrResources.Processo, CtrResources.Manifestacao, CtrResources.Painel,
        CtrResources.Importacao, CtrResources.Papel
    };

    [Fact]
    public async Task GetContext_UsuarioInexistente_DevolveNulo()
    {
        Assert.Null(await Permissoes.GetContextAsync("ninguem@df.gov.br", Perfis.Basico));
    }

    [Fact]
    public async Task GetContext_TrazPapelEPerfil()
    {
        var ctx = await Permissoes.GetContextAsync(UserAnalista.Email, PerfilDe(UserAnalista));

        Assert.NotNull(ctx);
        Assert.Equal(Perfis.Basico, ctx!.Perfil);
        Assert.Equal(PapeisContratacoes.Analise, ctx.PapelContratacoes);
        Assert.False(ctx.IsAdmin);
        Assert.Equal(PapeisContratacoes.Analise, ctx.PapelEfetivo);
    }

    [Fact]
    public async Task Admin_TemAcessoTotal()
    {
        var ctx = (await Permissoes.GetContextAsync(UserAdmin.Email, PerfilDe(UserAdmin)))!;

        Assert.True(ctx.IsAdmin);
        Assert.Equal(Perfis.Admin, ctx.PapelEfetivo);
        Assert.True(Permissoes.PodeAcessar(ctx));
        foreach (var recurso in Recursos)
        {
            Assert.True(Permissoes.CanView(ctx, recurso));
            Assert.True(Permissoes.CanEdit(ctx, recurso));
        }
    }

    [Fact]
    public async Task Analista_TemAcessoTotal()
    {
        var ctx = await ContextoAnalistaAsync();

        Assert.True(Permissoes.PodeAcessar(ctx));
        foreach (var recurso in Recursos)
        {
            Assert.True(Permissoes.CanView(ctx, recurso));
            Assert.True(Permissoes.CanEdit(ctx, recurso));
        }
    }

    [Theory]
    [InlineData("basico@subgd.df.gov.br")]
    [InlineData("gestor@subgd.df.gov.br")]
    public async Task SemPapel_NaoEntraNoModulo(string email)
    {
        var ctx = (await Permissoes.GetContextAsync(email, PerfilDe(email)))!;

        Assert.False(Permissoes.PodeAcessar(ctx));
        foreach (var recurso in Recursos)
        {
            Assert.False(Permissoes.CanView(ctx, recurso));
            Assert.False(Permissoes.CanEdit(ctx, recurso));
        }
    }

    [Fact]
    public async Task PapelPgia_NaoDaAcessoAoModuloDeContratacoes()
    {
        // O papel do PGIA é de outro módulo: não abre esta porta
        UserBasico.PapelPgia = PapeisPgia.Sgdi;
        await Context.SaveChangesAsync();

        var ctx = (await Permissoes.GetContextAsync(UserBasico.Email, PerfilDe(UserBasico)))!;

        Assert.False(Permissoes.PodeAcessar(ctx));
        Assert.False(Permissoes.CanView(ctx, CtrResources.Processo));
    }
}
