using System.Linq;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio;
using Xunit;

namespace test.pgia;

/// <summary>
/// Sincronização de Unidade via claim "unidade_codigo" (grupo Keycloak) no login
/// (AuthRepositorio.GetOrCreateUserAsync). Cobre o caso feliz e a trava que preserva
/// a integridade das designações vigentes (GarantirQueNaoOrfanizaDesignacaoAsync),
/// e o auto-provisionamento de pgia_orgao a partir do grupo (1 grupo = 1 unidade
/// = 1 órgão, sem cadastro manual prévio da SGDI).
/// </summary>
public class AuthRepositorioUnidadeKeycloakTest : PgiaTestBase
{
    [Fact]
    public async Task Login_ComUnidadeCodigoNovo_CriaUnidadeEAssocia()
    {
        var repo = new AuthRepositorio(Context);

        var user = await repo.GetOrCreateUserAsync(
            "kc-nova-unidade", "Pessoa Nova", "pessoa.nova@ses.df.gov.br", "SES-NOVA");

        Assert.NotNull(user.Unidade);
        Assert.Equal("SES-NOVA", user.Unidade!.CodigoExterno);
    }

    [Fact]
    public async Task Login_ComUnidadeCodigoExistente_ReaproveitaUnidade()
    {
        UnidadeSes.CodigoExterno = "SES";
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        var user = await repo.GetOrCreateUserAsync(
            "kc-existente", "Pessoa Existente", "pessoa.existente@ses.df.gov.br", "SES");

        Assert.Equal(UnidadeSes.id, user.Unidade?.id);
    }

    [Fact]
    public async Task Login_SemDesignacaoVigente_AtualizaUnidadeParaOGrupoDoToken()
    {
        UnidadeSes.CodigoExterno = "SES";
        UnidadeSeec.CodigoExterno = "SEEC";
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        var logado = await repo.GetOrCreateUserAsync(
            "kc-orgao-seec", "João Pires", UserOrgaoSeec.Email, "SES");

        Assert.Equal(UnidadeSes.id, logado.Unidade?.id);
    }

    [Fact]
    public async Task Login_ComDesignacaoResponsavelIaVigente_NaoSobrescreveUnidade()
    {
        UnidadeSes.CodigoExterno = "SES";
        UnidadeSeec.CodigoExterno = "SEEC";
        Context.PgiaResponsaveisIa.Add(new PgiaResponsavelIa
        {
            OrgaoId = OrgaoSes.Id,
            AgenteId = UserOrgaoSes.Id,
            AtoTipo = "Portaria",
            AtoNumero = "1/2026",
            AtoData = DateOnly.FromDateTime(DateTime.UtcNow),
            ProcessoSeiComunicacao = "00000-00000000/2026-00",
            DataComunicacaoSgdi = DateOnly.FromDateTime(DateTime.UtcNow),
            InicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        // Token do Keycloak diverge (grupo SEEC), mas a pessoa é Responsável de IA
        // vigente da SES — o login não pode orfanizar a designação silenciosamente.
        var logado = await repo.GetOrCreateUserAsync(
            "kc-responsavel-ses", "Maria Andrade", UserOrgaoSes.Email, "SEEC");

        Assert.Equal(UnidadeSes.id, logado.Unidade?.id);
    }

    [Fact]
    public async Task Login_ComDesignacaoEncarregadoDadosVigente_NaoSobrescreveUnidade()
    {
        UnidadeSes.CodigoExterno = "SES";
        UnidadeSeec.CodigoExterno = "SEEC";
        Context.PgiaEncarregadosDados.Add(new PgiaEncarregadoDados
        {
            OrgaoId = OrgaoSes.Id,
            AgenteId = UserOrgaoSes.Id,
            AtoTipo = "Portaria",
            AtoNumero = "2/2026",
            AtoData = DateOnly.FromDateTime(DateTime.UtcNow),
            ProcessoSeiComunicacao = "00000-00000000/2026-00",
            DataComunicacaoSgdi = DateOnly.FromDateTime(DateTime.UtcNow),
            InicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        var logado = await repo.GetOrCreateUserAsync(
            "kc-encarregado-ses", "Maria Andrade", UserOrgaoSes.Email, "SEEC");

        Assert.Equal(UnidadeSes.id, logado.Unidade?.id);
    }

    [Fact]
    public async Task Login_SemClaimUnidadeCodigo_NaoMexeNaUnidade()
    {
        var repo = new AuthRepositorio(Context);

        var logado = await repo.GetOrCreateUserAsync(
            "kc-sem-claim", "João Pires", UserOrgaoSeec.Email);

        Assert.Equal(UnidadeSeec.id, logado.Unidade?.id);
    }

    [Fact]
    public async Task Login_ComGrupoNovo_ProvisionaOrgaoAutomaticamente()
    {
        var repo = new AuthRepositorio(Context);

        var user = await repo.GetOrCreateUserAsync(
            "kc-grupo-novo", "Pessoa Nova", "pessoa.nova@novaunidade.df.gov.br", "NOVA-UNIDADE");

        var orgao = await Context.PgiaOrgaos.FirstOrDefaultAsync(o => o.UnidadeId == user.Unidade!.id);
        Assert.NotNull(orgao);
        Assert.Equal("NOVA-UNIDADE", orgao!.Sigla);
        Assert.Equal("NOVA-UNIDADE", orgao.Nome);
        Assert.True(orgao.Ativo);
    }

    [Fact]
    public async Task Login_ComGrupoNovo_InstanciaObrigacoesDeConformidade()
    {
        var repo = new AuthRepositorio(Context);

        var user = await repo.GetOrCreateUserAsync(
            "kc-grupo-com-prazos", "Pessoa Nova", "pessoa.nova@comprazos.df.gov.br", "COM-PRAZOS");

        var orgao = await Context.PgiaOrgaos.FirstAsync(o => o.UnidadeId == user.Unidade!.id);
        var prazos = await Context.PgiaPrazosConformidade
            .Where(p => p.OrgaoId == orgao.Id).ToListAsync();

        // 8 obrigações-modelo no seed, exceto as "SGDI: ..." (exclusivas da SGDI)
        Assert.NotEmpty(prazos);
        Assert.DoesNotContain(prazos, p => p.Obrigacao.StartsWith("SGDI:"));
    }

    [Fact]
    public async Task Login_ComGrupoJaProvisionado_NaoDuplicaOrgao()
    {
        var repo = new AuthRepositorio(Context);

        await repo.GetOrCreateUserAsync(
            "kc-repetido-1", "Pessoa Um", "um@repetido.df.gov.br", "REPETIDO");
        await repo.GetOrCreateUserAsync(
            "kc-repetido-2", "Pessoa Dois", "dois@repetido.df.gov.br", "REPETIDO");

        var unidade = await Context.Unidades.FirstAsync(u => u.CodigoExterno == "REPETIDO");
        var orgaos = await Context.PgiaOrgaos.Where(o => o.UnidadeId == unidade.id).ToListAsync();

        Assert.Single(orgaos);
    }

    [Fact]
    public async Task Login_ComUnidadeJaComOrgao_NaoProvisionaOutro()
    {
        // UnidadeSes já tem OrgaoSes na fixture — login de alguém desse grupo não
        // pode criar um segundo pgia_orgao para a mesma unidade.
        UnidadeSes.CodigoExterno = "SES";
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        await repo.GetOrCreateUserAsync(
            "kc-ses-novo", "Pessoa Nova da SES", "pessoa.nova@ses.df.gov.br", "SES");

        var orgaos = await Context.PgiaOrgaos.Where(o => o.UnidadeId == UnidadeSes.id).ToListAsync();
        Assert.Single(orgaos);
        Assert.Equal(OrgaoSes.Id, orgaos[0].Id);
    }

    [Fact]
    public async Task Login_ComSiglaDeUnidadeJaUsada_GeraSiglaAlternativa()
    {
        // Sigla do órgão auto-provisionado nasce do nome da unidade; se colidir
        // com uma sigla já existente, o provisionamento não pode falhar.
        var unidadeColidente = new Unidade { Nome = "SES", CodigoExterno = "SES-COLIDENTE" };
        Context.Unidades.Add(unidadeColidente);
        await Context.SaveChangesAsync();

        var repo = new AuthRepositorio(Context);
        var user = await repo.GetOrCreateUserAsync(
            "kc-sigla-colidente", "Pessoa Colidente", "pessoa@colidente.df.gov.br", "SES-COLIDENTE");

        var orgao = await Context.PgiaOrgaos.FirstAsync(o => o.UnidadeId == user.Unidade!.id);
        Assert.NotEqual("SES", orgao.Sigla);
        Assert.StartsWith("SES", orgao.Sigla);
    }
}
