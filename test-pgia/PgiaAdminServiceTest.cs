using api.Pgia;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

public class PgiaAdminServiceTest : PgiaTestBase
{
    private readonly PgiaAdminService _service;

    public PgiaAdminServiceTest()
    {
        _service = new PgiaAdminService(
            new PgiaOrgaoRepositorio(Context),
            new PgiaPrazoRepositorio(Context),
            Context);
    }

    // ── Papéis ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AtribuirPapel_GravaPapelSemTocarNoPerfil()
    {
        var perfilAntes = UserSemPapel.Perfil;

        await _service.AtribuirPapelAsync(new AtribuirPapelPgiaDTO
        {
            Email = UserSemPapel.Email,
            PapelPgia = "pgia_orgao"
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserSemPapel.Email);
        Assert.Equal("pgia_orgao", user.PapelPgia);
        Assert.Equal(perfilAntes, user.Perfil);
    }

    [Fact]
    public async Task AtribuirPapel_NuloRemoveOPapel()
    {
        await _service.AtribuirPapelAsync(new AtribuirPapelPgiaDTO
        {
            Email = UserOrgaoSes.Email,
            PapelPgia = null
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserOrgaoSes.Email);
        Assert.Null(user.PapelPgia);
    }

    [Fact]
    public async Task AtribuirPapel_InvalidoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtribuirPapelAsync(new AtribuirPapelPgiaDTO
            {
                Email = UserSemPapel.Email,
                PapelPgia = "gestor" // perfil do SGDP não é papel PGIA
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaPapelInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task AtribuirPapel_UsuarioInexistenteEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AtribuirPapelAsync(new AtribuirPapelPgiaDTO
            {
                Email = "nao-existe@df.gov.br",
                PapelPgia = "pgia_orgao"
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaUsuarioNaoEncontrado, ex.Error.Code);
    }

    // ── Órgãos ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CriarOrgao_InstanciaAsObrigacoesDoOrgao()
    {
        var unidade = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Nova Unidade" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var criado = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Sigla = "SEDES",
            Nome = "Secretaria de Desenvolvimento Social",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id
        }, UserAdmin.Email);

        var prazos = await Context.PgiaPrazosConformidade
            .Where(p => p.OrgaoId == criado.Id)
            .ToListAsync();

        // 8 obrigações-modelo no seed, menos as 2 exclusivas da SGDI
        Assert.Equal(6, prazos.Count);
        Assert.DoesNotContain(prazos, p => p.Obrigacao.StartsWith("SGDI:"));
        Assert.Contains(prazos, p => p.DataLimite == new DateOnly(2026, 8, 2));
    }

    [Fact]
    public async Task CriarOrgao_SiglaDuplicadaEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Sigla = "SES",
                Nome = "Outro órgão",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.Autarquia
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaOrgaoJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_UnidadeJaVinculadaEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Sigla = "SES2",
                Nome = "Órgão duplicando unidade",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
                UnidadeId = UnidadeSes.id
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaOrgaoJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_NaturezaInvalidaEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Sigla = "XPTO",
                Nome = "Órgão inválido",
                NaturezaJuridica = "Organização social"
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_PrestaServicoSoValeParaEmpresaOuSem()
    {
        // Administração direta: o valor informado é descartado (CHECK do schema)
        var direta = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Sigla = "DIR",
            Nome = "Órgão da administração direta",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            PrestaServicoCidadao = true
        }, UserAdmin.Email);
        Assert.Null(direta.PrestaServicoCidadao);

        var empresa = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Sigla = "EMP",
            Nome = "Empresa pública",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.EmpresaPublica,
            PrestaServicoCidadao = true
        }, UserAdmin.Email);
        Assert.True(empresa.PrestaServicoCidadao);
    }
}
