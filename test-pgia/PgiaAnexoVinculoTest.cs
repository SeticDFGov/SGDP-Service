using api.Pgia;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// FKs de documento das designações (cópia do ato, art. 10) e da auditoria
/// técnica (relatório completo, art. 34). Todas nullable e com DELETE RESTRICT:
/// apagar o documento não pode derrubar nem esvaziar a prova.
/// </summary>
public class PgiaAnexoVinculoTest : PgiaTestBase
{
    private readonly PgiaPermissionService _permissionService;
    private readonly PgiaOrgaoService _orgaoService;
    private readonly PgiaRelatorioService _relatorioService;

    public PgiaAnexoVinculoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _orgaoService = new PgiaOrgaoService(
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            new PgiaPrazoRepositorio(Context),
            _permissionService,
            Context);
        _relatorioService = new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context));
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private PgiaDocumento NovoDocumento(long? orgaoId, string tipo = "Ato de designação")
    {
        var documento = new PgiaDocumento
        {
            Tipo = tipo,
            OrgaoId = orgaoId,
            NomeArquivo = "ato.pdf",
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = UserOrgaoSes.Id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documento);
        Context.SaveChanges();
        return documento;
    }

    private static PgiaResponsavelIaCreateDTO NovaDesignacao(Guid agenteId, long? documentoId = null) => new()
    {
        AgenteId = agenteId,
        AtoTipo = "Portaria",
        AtoNumero = "214/2026",
        AtoData = new DateOnly(2026, 7, 20),
        ProcessoSeiComunicacao = "00060-00012345/2026-11",
        DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
        InicioVigencia = new DateOnly(2026, 7, 20),
        AcumulaFuncaoTic = false,
        DocumentoId = documentoId
    };

    private async Task<PgiaSistemaIa> NovoSistemaAsync(long orgaoId)
    {
        var sistema = new PgiaSistemaIa
        {
            OrgaoId = orgaoId,
            Denominacao = "Sistema auditado",
            Finalidade = "Teste",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "Outra",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            ClassificacaoRiscoAtual = "Alto Risco",
            SituacaoHomologacao = "Aguardando CGTIC",
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaSistemasIa.Add(sistema);
        await Context.SaveChangesAsync();
        return sistema;
    }

    private static PgiaAuditoriaCreateDTO NovaAuditoria(long sistemaId, long? documentoId = null) => new()
    {
        SistemaIaId = sistemaId,
        Tipo = PgiaDominios.TipoAuditoria.Todos.First(),
        EntidadeAuditora = "Auditoria Externa Ltda",
        ExternaFornecedor = true,
        DataInicio = new DateOnly(2026, 9, 1),
        DocumentoId = documentoId
    };

    // ── Designações: cópia do ato ─────────────────────────────────────────────

    [Fact]
    public async Task Designacao_AceitaDocumentoDoProprioOrgaoEDevolveNaResposta()
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resposta = await _orgaoService.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, documento.Id), UserOrgaoSes.Email);

        Assert.Equal(documento.Id, resposta.DocumentoId);

        var salva = await Context.PgiaResponsaveisIa.AsNoTracking()
            .FirstAsync(r => r.OrgaoId == OrgaoSes.Id && r.Ativo);
        Assert.Equal(documento.Id, salva.DocumentoId);
    }

    [Fact]
    public async Task Designacao_SemDocumentoContinuaValida()
    {
        var resposta = await _orgaoService.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id), UserOrgaoSes.Email);

        Assert.Null(resposta.DocumentoId);
    }

    [Fact]
    public async Task Designacao_DocumentoDeOutroOrgaoEhRejeitado()
    {
        var documentoSeec = NovoDocumento(OrgaoSeec.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _orgaoService.DesignarResponsavelIaAsync(
                OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, documentoSeec.Id), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Empty(await Context.PgiaResponsaveisIa.ToListAsync());
    }

    [Fact]
    public async Task Designacao_DocumentoCentralNaoServeDeCopiaDoAto()
    {
        // A cópia do ato é do órgão que designou; documento central não vale
        var central = NovoDocumento(orgaoId: null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _orgaoService.DesignarResponsavelIaAsync(
                OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, central.Id), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    [Fact]
    public async Task Designacao_DocumentoInexistenteEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _orgaoService.DesignarResponsavelIaAsync(
                OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, 987654), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task Encarregado_AceitaDocumentoDoProprioOrgao()
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resposta = await _orgaoService.DesignarEncarregadoDadosAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, documento.Id), UserOrgaoSes.Email);

        Assert.Equal(documento.Id, resposta.DocumentoId);

        var salvo = await Context.PgiaEncarregadosDados.AsNoTracking()
            .FirstAsync(e => e.OrgaoId == OrgaoSes.Id && e.Ativo);
        Assert.Equal(documento.Id, salvo.DocumentoId);
    }

    [Fact]
    public async Task Encarregado_DocumentoDeOutroOrgaoEhRejeitado()
    {
        var documentoSeec = NovoDocumento(OrgaoSeec.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _orgaoService.DesignarEncarregadoDadosAsync(
                OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, documentoSeec.Id), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }

    // ── Auditoria técnica: relatório completo ─────────────────────────────────

    [Fact]
    public async Task Auditoria_AceitaDocumentoDoOrgaoAuditado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id);
        var documento = NovoDocumento(OrgaoSes.Id, "Parecer de auditoria");
        var ctx = (await _permissionService.GetContextAsync(UserSgdi.Email))!;

        var resposta = await _relatorioService.CriarAuditoriaAsync(
            NovaAuditoria(sistema.Id, documento.Id), ctx);

        Assert.Equal(documento.Id, resposta.DocumentoId);
        var salva = await Context.PgiaAuditoriasTecnicas.AsNoTracking().FirstAsync(a => a.Id == resposta.Id);
        Assert.Equal(documento.Id, salva.DocumentoId);
    }

    [Fact]
    public async Task Auditoria_AceitaDocumentoCentralDaSgdi()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id);
        var central = NovoDocumento(orgaoId: null, "Parecer de auditoria");
        var ctx = (await _permissionService.GetContextAsync(UserSgdi.Email))!;

        var resposta = await _relatorioService.CriarAuditoriaAsync(
            NovaAuditoria(sistema.Id, central.Id), ctx);

        Assert.Equal(central.Id, resposta.DocumentoId);
    }

    [Fact]
    public async Task Auditoria_DocumentoDeTerceiroOrgaoEhRejeitado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id);
        var deOutroOrgao = NovoDocumento(OrgaoSeec.Id, "Parecer de auditoria");
        var ctx = (await _permissionService.GetContextAsync(UserSgdi.Email))!;

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _relatorioService.CriarAuditoriaAsync(NovaAuditoria(sistema.Id, deOutroOrgao.Id), ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Empty(await Context.PgiaAuditoriasTecnicas.ToListAsync());
    }

    [Fact]
    public async Task Auditoria_EdicaoAnexaORelatorioDepois()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id);
        var ctx = (await _permissionService.GetContextAsync(UserSgdi.Email))!;
        var criada = await _relatorioService.CriarAuditoriaAsync(NovaAuditoria(sistema.Id), ctx);
        Assert.Null(criada.DocumentoId);

        var documento = NovoDocumento(OrgaoSes.Id, "Parecer de auditoria");
        var atualizada = await _relatorioService.AtualizarAuditoriaAsync(
            criada.Id, NovaAuditoria(sistema.Id, documento.Id), ctx);

        Assert.Equal(documento.Id, atualizada.DocumentoId);
    }

    // ── DELETE RESTRICT: o documento não pode ser apagado sob a designação ─────

    /// <summary>
    /// Restrict (não SetNull nem Cascade): a cópia do ato é prova da designação —
    /// o banco recusa apagar o documento enquanto ele estiver vinculado, em vez de
    /// derrubar a designação ou esvaziar o vínculo em silêncio.
    /// </summary>
    [Fact]
    public async Task DeleteRestrict_DocumentoVinculadoNaoDerrubaADesignacao()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await _orgaoService.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, documento.Id), UserOrgaoSes.Email);

        // O InMemory não aplica FK, mas a intenção do modelo é verificável:
        var fk = Context.Model.FindEntityType(typeof(PgiaResponsavelIa))!
            .GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(PgiaDocumento));
        Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.True(fk.Properties.Single().IsNullable); // opcional: designação vive sem anexo

        // E a designação continua de pé com o vínculo
        var salva = await Context.PgiaResponsaveisIa.AsNoTracking().FirstAsync(r => r.Ativo);
        Assert.Equal(documento.Id, salva.DocumentoId);
    }

    [Theory]
    [InlineData(typeof(PgiaEncarregadoDados))]
    [InlineData(typeof(PgiaAuditoriaTecnica))]
    public void DeleteRestrict_ValeParaEncarregadoEAuditoria(Type entidade)
    {
        var fk = Context.Model.FindEntityType(entidade)!
            .GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(PgiaDocumento));

        Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.True(fk.Properties.Single().IsNullable);
    }

    /// <summary>
    /// A autorização excepcional já tinha a FK de "avaliação de riscos"
    /// (AvaliacaoRiscosDocId) desde a fase 2 — nada foi acrescentado lá.
    /// </summary>
    [Fact]
    public void Autorizacao_JaTinhaFkDeAvaliacaoDeRiscos()
    {
        var fk = Context.Model.FindEntityType(typeof(PgiaAutorizacaoExcepcional))!
            .GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(PgiaDocumento));

        Assert.Equal("AvaliacaoRiscosDocId", fk.Properties.Single().Name);
        Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict, fk.DeleteBehavior);
    }
}
