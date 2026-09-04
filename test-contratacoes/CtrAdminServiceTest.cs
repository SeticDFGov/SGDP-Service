using api.Contratacoes;
using app.Auth;
using Microsoft.EntityFrameworkCore;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Atribuição do papel do módulo pela tela de admin do SGDP.
/// </summary>
public class CtrAdminServiceTest : CtrTestBase
{
    private readonly CtrAdminService _service;

    public CtrAdminServiceTest()
    {
        _service = NovoAdminService();
    }

    [Fact]
    public async Task AtribuirPapel_GravaSemTocarNoPerfil()
    {
        var perfilAntes = UserBasico.Perfil;

        await _service.AtribuirPapelAsync(new AtribuirPapelContratacoesDTO
        {
            Email = UserBasico.Email,
            PapelContratacoes = PapeisContratacoes.Analise
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserBasico.Email);
        Assert.Equal(PapeisContratacoes.Analise, user.PapelContratacoes);
        Assert.Equal(perfilAntes, user.Perfil);
        Assert.True(Permissoes.PodeAcessar((await Permissoes.GetContextAsync(user.Email))!));
    }

    [Fact]
    public async Task AtribuirPapel_NuloRemoveOPapel()
    {
        await _service.AtribuirPapelAsync(new AtribuirPapelContratacoesDTO
        {
            Email = UserAnalista.Email,
            PapelContratacoes = null
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserAnalista.Email);
        Assert.Null(user.PapelContratacoes);
        Assert.False(Permissoes.PodeAcessar((await Permissoes.GetContextAsync(user.Email))!));
    }

    [Fact]
    public async Task AtribuirPapel_NaoMexeNoPapelPgia()
    {
        UserBasico.PapelPgia = PapeisPgia.Orgao;
        await Context.SaveChangesAsync();

        await _service.AtribuirPapelAsync(new AtribuirPapelContratacoesDTO
        {
            Email = UserBasico.Email,
            PapelContratacoes = PapeisContratacoes.Analise
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserBasico.Email);
        Assert.Equal(PapeisPgia.Orgao, user.PapelPgia);
        Assert.Equal(PapeisContratacoes.Analise, user.PapelContratacoes);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("pgia_sgdi")]
    [InlineData("ctr_qualquer")]
    public async Task AtribuirPapel_InvalidoEhRecusado(string papel)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtribuirPapelAsync(new AtribuirPapelContratacoesDTO
            {
                Email = UserBasico.Email,
                PapelContratacoes = papel
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.CtrPapelInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task AtribuirPapel_UsuarioInexistenteEhRecusado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtribuirPapelAsync(new AtribuirPapelContratacoesDTO
            {
                Email = "nao-existe@df.gov.br",
                PapelContratacoes = PapeisContratacoes.Analise
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.CtrUsuarioNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public void Papeis_TemUmValorSoEValidamNulo()
    {
        Assert.Equal(new[] { "ctr_analise" }, PapeisContratacoes.Todos);
        Assert.True(PapeisContratacoes.EhValido(null));
        Assert.True(PapeisContratacoes.EhValido("ctr_analise"));
        Assert.False(PapeisContratacoes.EhValido("pgia_orgao"));
    }
}
