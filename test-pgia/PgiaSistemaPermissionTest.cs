using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Escopo de dados do inventário: GetFilteredSistemasQuery mantém cada órgão
/// vendo só os próprios sistemas, com SGDI, CGTIC e admin enxergando todos.
/// </summary>
public class PgiaSistemaPermissionTest : PgiaTestBase
{
    private readonly PgiaPermissionService _service;

    public PgiaSistemaPermissionTest()
    {
        _service = new PgiaPermissionService(Context);

        CriarSistema(OrgaoSes, UserOrgaoSes, "Triagem clínica");
        CriarSistema(OrgaoSeec, UserOrgaoSeec, "Classificador de despesas");
    }

    private void CriarSistema(PgiaOrgao orgao, User agente, string denominacao)
    {
        var responsavel = new PgiaResponsavelIa
        {
            OrgaoId = orgao.Id,
            AgenteId = agente.Id,
            AtoTipo = "Portaria",
            AtoNumero = "214/2026",
            AtoData = new DateOnly(2026, 7, 20),
            ProcessoSeiComunicacao = "00060-00012345/2026-11",
            DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
            InicioVigencia = new DateOnly(2026, 7, 20),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaResponsaveisIa.Add(responsavel);
        Context.SaveChanges();

        Context.PgiaSistemasIa.Add(new PgiaSistemaIa
        {
            OrgaoId = orgao.Id,
            Denominacao = denominacao,
            Finalidade = "Finalidade de teste",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA generativa",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            ClassificacaoRiscoAtual = PgiaDominios.ResultadoRisco.Baixo,
            ResponsavelIaId = responsavel.Id,
            CriadoEm = DateTime.UtcNow
        });
        Context.SaveChanges();
    }

    [Fact]
    public async Task FiltroDeSistemas_PapelOrgaoSoVeOsDoProprioOrgao()
    {
        var ctx = await _service.GetContextAsync(UserOrgaoSes.Email, PerfilDe(UserOrgaoSes));
        var sistemas = await _service.GetFilteredSistemasQuery(ctx!).ToListAsync();

        Assert.Single(sistemas);
        Assert.Equal(OrgaoSes.Id, sistemas[0].OrgaoId);
        Assert.Equal("Triagem clínica", sistemas[0].Denominacao);
    }

    [Fact]
    public async Task FiltroDeSistemas_PapelOrgaoNaoEnxergaOOutroOrgao()
    {
        var ctx = await _service.GetContextAsync(UserOrgaoSeec.Email, PerfilDe(UserOrgaoSeec));
        var sistemas = await _service.GetFilteredSistemasQuery(ctx!).ToListAsync();

        Assert.Single(sistemas);
        Assert.Equal(OrgaoSeec.Id, sistemas[0].OrgaoId);
        Assert.DoesNotContain(sistemas, s => s.OrgaoId == OrgaoSes.Id);
    }

    [Theory]
    [InlineData("sgdi@sgdi.df.gov.br")]
    [InlineData("cgtic@sgdi.df.gov.br")]
    [InlineData("admin@subgd.df.gov.br")]
    public async Task FiltroDeSistemas_EscopoCentralVeTodos(string email)
    {
        var ctx = await _service.GetContextAsync(email, PerfilDe(email));
        var sistemas = await _service.GetFilteredSistemasQuery(ctx!).ToListAsync();

        Assert.Equal(2, sistemas.Count);
    }

    [Theory]
    [InlineData("comum@ses.df.gov.br")]
    [InlineData("aud@auditoria.com")]
    public async Task FiltroDeSistemas_SemPapelNoInventarioNaoVeNada(string email)
    {
        var ctx = await _service.GetContextAsync(email, PerfilDe(email));
        var sistemas = await _service.GetFilteredSistemasQuery(ctx!).ToListAsync();

        Assert.Empty(sistemas);
    }

    [Fact]
    public async Task FiltroDeSistemas_TrazOOrgaoParaAListagem()
    {
        var ctx = await _service.GetContextAsync(UserSgdi.Email, PerfilDe(UserSgdi));
        var sistemas = await _service.GetFilteredSistemasQuery(ctx!).ToListAsync();

        Assert.All(sistemas, s => Assert.NotNull(s.Orgao));
        Assert.Contains(sistemas, s => s.Orgao!.Sigla == "SES");
        Assert.Contains(sistemas, s => s.Orgao!.Sigla == "SEEC");
    }
}
