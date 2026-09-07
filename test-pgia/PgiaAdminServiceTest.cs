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
    public async Task AtribuirPapel_GravaPapel()
    {
        await _service.AtribuirPapelAsync(new AtribuirPapelPgiaDTO
        {
            Email = UserSemPapel.Email,
            PapelPgia = "pgia_orgao"
        }, UserAdmin.Email);

        var user = await Context.Users.FirstAsync(u => u.Email == UserSemPapel.Email);
        Assert.Equal("pgia_orgao", user.PapelPgia);
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
        var unidade = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Nova Unidade", CodigoExterno = "SEDES" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var criado = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
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
    public async Task CriarOrgao_SiglaNasceDoCodigoExternoDaUnidade()
    {
        var unidade = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Nova Unidade", CodigoExterno = "SEDES" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var criado = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Nome = "Secretaria de Desenvolvimento Social",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id
        }, UserAdmin.Email);

        Assert.Equal("SEDES", criado.Sigla);
    }

    [Fact]
    public async Task CriarOrgao_UnidadeJaVinculadaEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Nome = "Órgão duplicando unidade",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
                UnidadeId = UnidadeSes.id
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaOrgaoJaExiste, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_SemUnidadeEhRejeitada()
    {
        // Não existe mais órgão sem unidade: é dela que a sigla nasce.
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Nome = "Órgão sem unidade",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.Autarquia
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_NaturezaInvalidaEhRejeitada()
    {
        var unidade = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Unidade XPTO", CodigoExterno = "XPTO" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Nome = "Órgão inválido",
                NaturezaJuridica = "Organização social",
                UnidadeId = unidade.id
            }, UserAdmin.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task CriarOrgao_PrestaServicoSoValeParaEmpresaOuSem()
    {
        var unidadeDireta = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Unidade Direta", CodigoExterno = "DIR" };
        var unidadeEmpresa = new app.Models.Unidade { id = Guid.NewGuid(), Nome = "Unidade Empresa", CodigoExterno = "EMP" };
        Context.Unidades.AddRange(unidadeDireta, unidadeEmpresa);
        await Context.SaveChangesAsync();

        // Administração direta: o valor informado é descartado (CHECK do schema)
        var direta = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Nome = "Órgão da administração direta",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            PrestaServicoCidadao = true,
            UnidadeId = unidadeDireta.id
        }, UserAdmin.Email);
        Assert.Null(direta.PrestaServicoCidadao);

        var empresa = await _service.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
        {
            Nome = "Empresa pública",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.EmpresaPublica,
            PrestaServicoCidadao = true,
            UnidadeId = unidadeEmpresa.id
        }, UserAdmin.Email);
        Assert.True(empresa.PrestaServicoCidadao);
    }
}
