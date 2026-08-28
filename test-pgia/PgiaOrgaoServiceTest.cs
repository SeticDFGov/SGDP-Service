using api.Pgia;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

public class PgiaOrgaoServiceTest : PgiaTestBase
{
    private readonly PgiaOrgaoService _service;
    private readonly PgiaPermissionService _permissionService;

    public PgiaOrgaoServiceTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaOrgaoService(
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            new PgiaPrazoRepositorio(Context),
            _permissionService);
    }

    private PgiaResponsavelIaCreateDTO NovoResponsavelDto(Guid agenteId) => new()
    {
        AgenteId = agenteId,
        AtoTipo = "Portaria",
        AtoNumero = "214/2026",
        AtoData = new DateOnly(2026, 7, 20),
        ProcessoSeiComunicacao = "00060-00012345/2026-11",
        DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
        InicioVigencia = new DateOnly(2026, 7, 20),
        AcumulaFuncaoTic = false
    };

    // ── Designações ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DesignarResponsavel_CriaDesignacaoVigente()
    {
        var resposta = await _service.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovoResponsavelDto(UserOrgaoSes.Id), UserOrgaoSes.Email);

        Assert.True(resposta.Ativo);
        Assert.Equal(UserOrgaoSes.Id, resposta.AgenteId);
        Assert.Equal("Maria Andrade", resposta.AgenteNome);
    }

    [Fact]
    public async Task DesignarResponsavel_NovoTitularPreservaHistorico()
    {
        await _service.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovoResponsavelDto(UserOrgaoSes.Id), UserOrgaoSes.Email);

        var dto2 = NovoResponsavelDto(UserSemPapel.Id); // outra pessoa da mesma unidade
        dto2.AtoNumero = "300/2026";
        dto2.InicioVigencia = new DateOnly(2026, 9, 1);
        await _service.DesignarResponsavelIaAsync(OrgaoSes.Id, dto2, UserOrgaoSes.Email);

        var todas = await Context.PgiaResponsaveisIa
            .Where(r => r.OrgaoId == OrgaoSes.Id)
            .ToListAsync();

        Assert.Equal(2, todas.Count);
        var antiga = todas.First(r => r.AtoNumero == "214/2026");
        var vigente = todas.First(r => r.AtoNumero == "300/2026");
        Assert.False(antiga.Ativo);
        Assert.Equal(new DateOnly(2026, 9, 1), antiga.FimVigencia);
        Assert.True(vigente.Ativo);
    }

    [Fact]
    public async Task DesignarResponsavel_CapacitacaoSoQuandoAcumulaTic()
    {
        var dto = NovoResponsavelDto(UserOrgaoSes.Id);
        dto.AcumulaFuncaoTic = false;
        dto.CapacitacaoAdequada = true; // deve ser descartado

        var resposta = await _service.DesignarResponsavelIaAsync(OrgaoSes.Id, dto, UserOrgaoSes.Email);
        Assert.Null(resposta.CapacitacaoAdequada);
    }

    [Fact]
    public async Task DesignarResponsavel_AgenteDeOutraUnidadeEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.DesignarResponsavelIaAsync(
                OrgaoSes.Id, NovoResponsavelDto(UserOrgaoSeec.Id), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaAgenteDeOutroOrgao, ex.Error.Code);
    }

    [Fact]
    public async Task DesignarEncarregado_OrgaoSemUnidadeEhRejeitado()
    {
        var semUnidade = new PgiaOrgao
        {
            Sigla = "SEMU",
            Nome = "Órgão sem unidade",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.Autarquia,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaOrgaos.Add(semUnidade);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.DesignarEncarregadoDadosAsync(
                semUnidade.Id, NovoResponsavelDto(UserOrgaoSes.Id), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaOrgaoSemUnidadeVinculada, ex.Error.Code);
    }

    // ── Pessoas e agente_info ─────────────────────────────────────────────────

    [Fact]
    public async Task ListarPessoas_TrazUsuariosDaUnidadeComInfo()
    {
        await _service.SalvarAgenteInfoAsync(OrgaoSes.Id, UserOrgaoSes.Id, new PgiaAgenteInfoDTO
        {
            Matricula = "178.402-1",
            CargoFuncao = "Analista",
            Vinculo = PgiaDominios.Vinculo.ServidorEfetivo
        }, UserOrgaoSes.Email);

        var pessoas = await _service.ListarPessoasAsync(OrgaoSes.Id);

        Assert.Equal(2, pessoas.Count); // Maria e Carlos, ambos da unidade SES
        var maria = pessoas.First(p => p.UserId == UserOrgaoSes.Id);
        Assert.Equal("178.402-1", maria.Matricula);
        Assert.Equal(PgiaDominios.Vinculo.ServidorEfetivo, maria.Vinculo);
        Assert.DoesNotContain(pessoas, p => p.UserId == UserOrgaoSeec.Id);
    }

    [Fact]
    public async Task SalvarAgenteInfo_VinculoInvalidoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.SalvarAgenteInfoAsync(OrgaoSes.Id, UserOrgaoSes.Id, new PgiaAgenteInfoDTO
            {
                Vinculo = "Voluntário"
            }, UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task SalvarAgenteInfo_PessoaDeOutraUnidadeEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.SalvarAgenteInfoAsync(OrgaoSes.Id, UserOrgaoSeec.Id, new PgiaAgenteInfoDTO
            {
                Vinculo = PgiaDominios.Vinculo.Comissionado
            }, UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaAgenteDeOutroOrgao, ex.Error.Code);
    }

    // ── Prazos ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Prazos_SituacaoCalculadaComoNaViewDoSchema()
    {
        Context.PgiaPrazosConformidade.AddRange(
            new PgiaPrazoConformidade { Obrigacao = "Vencida", BaseLegal = "art. 35", OrgaoId = OrgaoSes.Id, DataLimite = new DateOnly(2020, 1, 1), CriadoEm = DateTime.UtcNow },
            new PgiaPrazoConformidade { Obrigacao = "No prazo", BaseLegal = "art. 35", OrgaoId = OrgaoSes.Id, DataLimite = new DateOnly(2099, 1, 1), CriadoEm = DateTime.UtcNow },
            new PgiaPrazoConformidade { Obrigacao = "Cumprida no prazo", BaseLegal = "art. 35", OrgaoId = OrgaoSes.Id, DataLimite = new DateOnly(2099, 1, 1), CumpridoEm = new DateOnly(2026, 1, 1), CriadoEm = DateTime.UtcNow },
            new PgiaPrazoConformidade { Obrigacao = "Cumprida em atraso", BaseLegal = "art. 35", OrgaoId = OrgaoSes.Id, DataLimite = new DateOnly(2020, 1, 1), CumpridoEm = new DateOnly(2026, 1, 1), CriadoEm = DateTime.UtcNow });
        await Context.SaveChangesAsync();

        var prazos = await _service.ListarPrazosOrgaoAsync(OrgaoSes.Id);

        Assert.Equal(PgiaDominios.SituacaoPrazo.Vencida, prazos.First(p => p.Obrigacao == "Vencida").Situacao);
        Assert.Equal(PgiaDominios.SituacaoPrazo.NoPrazo, prazos.First(p => p.Obrigacao == "No prazo").Situacao);
        Assert.Equal(PgiaDominios.SituacaoPrazo.CumpridaNoPrazo, prazos.First(p => p.Obrigacao == "Cumprida no prazo").Situacao);
        Assert.Equal(PgiaDominios.SituacaoPrazo.CumpridaEmAtraso, prazos.First(p => p.Obrigacao == "Cumprida em atraso").Situacao);
    }

    [Fact]
    public async Task Prazos_SeedDasOitoObrigacoesCentraisExiste()
    {
        var centrais = await _service.ListarPrazosCentraisAsync();

        Assert.Equal(8, centrais.Count);
        Assert.Contains(centrais, p => p.DataLimite == new DateOnly(2026, 8, 2));
        Assert.Contains(centrais, p => p.Obrigacao.StartsWith("SGDI:"));
    }

    [Fact]
    public async Task MarcarCumprimento_GravaDataEAuditoria()
    {
        var prazo = new PgiaPrazoConformidade
        {
            Obrigacao = "Teste",
            BaseLegal = "art. 35",
            OrgaoId = OrgaoSes.Id,
            DataLimite = new DateOnly(2026, 10, 1),
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaPrazosConformidade.Add(prazo);
        await Context.SaveChangesAsync();

        var resposta = await _service.MarcarCumprimentoAsync(prazo.Id, new PgiaMarcarCumprimentoDTO
        {
            CumpridoEm = new DateOnly(2026, 9, 15)
        }, UserOrgaoSes.Email);

        Assert.Equal(new DateOnly(2026, 9, 15), resposta.CumpridoEm);
        Assert.Equal(PgiaDominios.SituacaoPrazo.CumpridaNoPrazo, resposta.Situacao);

        var salvo = await Context.PgiaPrazosConformidade.FirstAsync(p => p.Id == prazo.Id);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);
    }
}
