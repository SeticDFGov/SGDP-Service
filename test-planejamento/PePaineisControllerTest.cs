using api.Common;
using api.Planejamento;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using service;
using service.Interface;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas da E8 com os status e os corpos do contrato: o painel, a conformidade (com a
/// planilha), a árvore e a página do órgão (200, e 403 e 404 com corpo); a inadimplência
/// (notificar com 201, justificar, registrar com 409 antes do prazo, sanear, 400 com Campos, 404,
/// corpo que não é objeto); e o intervalo do deploy: sem a tabela nova (42P01), 409 com corpo.
/// </summary>
public class PePaineisControllerTest : PePaineisTestBase
{
    private static object? Propriedade(object? corpo, string nome) => corpo!.GetType().GetProperty(nome)!.GetValue(corpo);

    [Fact]
    public async Task Paineis_200ParaASgdi_E403ComCorpoParaOOrgao()
    {
        await AbrirSesAsync();
        var sgdi = ControladorPaineis(UserPeSgdi);

        var (status, corpo) = Resultado(await sgdi.Painel(new PePainelConsulta()));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(2, Assert.IsType<PePainelGeralResponse>(corpo).TotalOrgaos);
        (status, corpo) = Resultado(await sgdi.Conformidade(new PeConformidadeConsulta { Grupo = "baixa" }));
        Assert.Equal(2, Assert.IsType<PeConformidadeResponse>(corpo).Orgaos.Count);
        (status, corpo) = Resultado(await sgdi.Arvore(null));
        Assert.Null(Assert.IsType<PeArvorePeticResponse>(corpo).Petic);
        (status, corpo) = Resultado(await sgdi.Resumo(OrgaoSes.Id));
        Assert.Equal("SES", Assert.IsType<PeOrgaoResumoResponse>(corpo).Orgao.Sigla);

        var arquivo = Assert.IsType<FileContentResult>(await sgdi.ConformidadePlanilha(new PeConformidadeConsulta(), "csv"));
        Assert.Equal((PePlanilhaService.MimeCsv, $"PDTIC_conformidade_{Iso(HojeData())}.csv"), (arquivo.ContentType, arquivo.FileDownloadName));
        (status, corpo) = Resultado(await sgdi.ConformidadePlanilha(new PeConformidadeConsulta(), "ods"));
        Assert.Equal((StatusCodes.Status400BadRequest, Codigo(ErrorCode.PePlanilhaInvalida)), (status, CodigoDe(corpo)));

        var orgao = ControladorPaineis(UserOrgaoSes);
        foreach (var resultado in new[] { await orgao.Painel(new PePainelConsulta()), await orgao.Conformidade(new PeConformidadeConsulta()), await orgao.Arvore(null),
                     await orgao.ConformidadePlanilha(new PeConformidadeConsulta(), "xlsx"), await orgao.Resumo(OrgaoSeec.Id) })
        {
            (status, corpo) = Resultado(resultado);
            Assert.Equal((StatusCodes.Status403Forbidden, Codigo(ErrorCode.PeSemPermissao)), (status, CodigoDe(corpo)));
            Assert.NotNull(Propriedade(corpo, "Message"));
        }
        // A página do próprio órgão, sim
        (status, _) = Resultado(await orgao.Resumo(OrgaoSes.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        (status, corpo) = Resultado(await sgdi.Resumo(999999));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeOrgaoNaoEncontrado)), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task Inadimplencia_Rotas_ComOsStatusDoContrato()
    {
        var sgdi = ControladorInadimplencias(UserPeSgdi);
        var notificacao = new
        {
            Obrigacao = "Comunicar à SGDI o PDTIC aprovado pelo comitê interno (art. 7º, V)",
            PrazoDescumprido = "30 dias depois da aprovação",
            NotificadoEm = Iso(HojeData()),
            Documento = "Ofício nº 12/2026-SGDI"
        };

        var (status, corpo) = Resultado(await sgdi.Notificar(OrgaoSes.Id, Corpo(notificacao)));
        Assert.Equal(StatusCodes.Status201Created, status);
        var criada = Assert.IsType<PeInadimplenciaResponse>(corpo);
        Assert.Equal(("notificado", 5), (criada.Situacao, criada.DiasUteisRestantes));

        // Registrar antes do prazo: 409 com corpo
        (status, corpo) = Resultado(await sgdi.Registrar(criada.Id, Corpo(new { Motivo = "descumprimento_prazo", NotaMotivacao = "Nota." })));
        Assert.Equal((StatusCodes.Status409Conflict, Codigo(ErrorCode.PeInadimplenciaPrazoAberto)), (status, CodigoDe(corpo)));
        Assert.NotNull(Propriedade(corpo, "Message"));

        // Validação: 400 com Campos; corpo que não é objeto: 400 PeDadosInvalidos
        (status, corpo) = Resultado(await sgdi.Notificar(OrgaoSes.Id, Corpo(new { Documento = "Ofício" })));
        Assert.Equal((StatusCodes.Status400BadRequest, Codigo(ErrorCode.PeInadimplenciaInvalida)), (status, CodigoDe(corpo)));
        var campos = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(Propriedade(corpo, "Campos"));
        Assert.Equal(new[] { "NotificadoEm", "Obrigacao", "PrazoDescumprido" }, campos.Keys.OrderBy(k => k, StringComparer.Ordinal));
        (status, corpo) = Resultado(await sgdi.Justificar(criada.Id, Corpo("aceita")));
        Assert.Equal((StatusCodes.Status400BadRequest, Codigo(ErrorCode.PeDadosInvalidos)), (status, CodigoDe(corpo)));

        (status, corpo) = Resultado(await sgdi.Justificar(criada.Id, Corpo(new { Justificativa = "Justificativa aceita pela SGDI." })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("justificado", Assert.IsType<PeInadimplenciaResponse>(corpo).Situacao);
        (status, corpo) = Resultado(await sgdi.Sanear(criada.Id, Corpo(new { SaneadoEm = Iso(HojeData()) })));
        Assert.Equal((StatusCodes.Status409Conflict, Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await sgdi.Sanear(999999, Corpo(new { SaneadoEm = Iso(HojeData()) })));
        Assert.Equal((StatusCodes.Status404NotFound, Codigo(ErrorCode.PeInadimplenciaNaoEncontrada)), (status, CodigoDe(corpo)));

        // Registrar depois do prazo e sanear
        var antiga = InadimplenciaNoBanco(OrgaoSeec, "notificado", HojeData().AddDays(-20));
        (status, corpo) = Resultado(await sgdi.Registrar(antiga.Id, Corpo(new { Motivo = "recusa_injustificada", NotaMotivacao = "Nota de motivação.", ComunicadoControleEm = (string?)null })));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Recusa injustificada de comunicação", Assert.IsType<PeInadimplenciaResponse>(corpo).MotivoRotulo);
        (status, corpo) = Resultado(await sgdi.Sanear(antiga.Id, Corpo(new { SaneadoEm = Iso(HojeData()), Observacao = "Regularizado." })));
        Assert.Equal("saneado", Assert.IsType<PeInadimplenciaResponse>(corpo).Situacao);

        // Listas: a paginada (vigentes primeiro) e a do órgão; a equipe do órgão lê as dela e não notifica
        (status, corpo) = Resultado(await sgdi.Listar(new PeInadimplenciasConsulta()));
        Assert.Equal(2, Assert.IsType<PagedResponse<PeInadimplenciaResponse>>(corpo).TotalItems);
        var equipe = ControladorInadimplencias(UserOrgaoSes);
        (status, corpo) = Resultado(await equipe.DoOrgao(OrgaoSes.Id));
        Assert.Equal("SES", Assert.Single(Assert.IsType<List<PeInadimplenciaResponse>>(corpo)).OrgaoSigla);
        (status, corpo) = Resultado(await equipe.Notificar(OrgaoSes.Id, Corpo(notificacao)));
        Assert.Equal((StatusCodes.Status403Forbidden, Codigo(ErrorCode.PeSemPermissao)), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await equipe.DoOrgao(OrgaoSeec.Id));
        Assert.Equal((StatusCodes.Status403Forbidden, Codigo(ErrorCode.PeSemPermissao)), (status, CodigoDe(corpo)));
    }

    // ── Intervalo do deploy ─────────────────────────────────────────────────

    /// <summary>A tabela nova ainda não existe no PostgreSQL (o PR publicou o código antes da migration).</summary>
    private static PostgresException SemTabela() => new("relação \"pe_inadimplencia\" não existe", "ERROR", "ERROR", "42P01");

    private sealed class PaineisSemTabela : IPePainelService
    {
        public Task<PePainelGeralResponse> PainelAsync(PePainelConsulta consulta, PeUserContext ctx) => throw SemTabela();
        public Task<PeConformidadeResponse> ConformidadeAsync(PeConformidadeConsulta consulta, PeUserContext ctx) => throw SemTabela();
        public Task<PePlanilhaArquivo> ConformidadePlanilhaAsync(PeConformidadeConsulta consulta, string? formato, PeUserContext ctx) => throw SemTabela();
        public Task<PeArvorePeticResponse> ArvoreAsync(long? orgaoId, PeUserContext ctx) => throw SemTabela();
        public Task<PeOrgaoResumoResponse> ResumoAsync(long orgaoId, PeUserContext ctx) => throw SemTabela();
    }

    private sealed class InadimplenciasSemTabela : IPeInadimplenciaService
    {
        public Task<PagedResponse<PeInadimplenciaResponse>> ListarAsync(PeInadimplenciasConsulta consulta, PeUserContext ctx) => throw SemTabela();
        public Task<List<PeInadimplenciaResponse>> DoOrgaoAsync(long orgaoId, PeUserContext ctx) => throw SemTabela();
        public Task<PeInadimplenciaResponse> NotificarAsync(long orgaoId, PeInadimplenciaNotificarDTO dto, PeUserContext ctx) => throw SemTabela();
        public Task<PeInadimplenciaResponse> JustificarAsync(long id, PeInadimplenciaJustificarDTO dto, PeUserContext ctx) => throw SemTabela();
        public Task<PeInadimplenciaResponse> RegistrarAsync(long id, PeInadimplenciaRegistrarDTO dto, PeUserContext ctx) => throw SemTabela();
        public Task<PeInadimplenciaResponse> SanearAsync(long id, PeInadimplenciaSanearDTO dto, PeUserContext ctx) => throw SemTabela();
    }

    [Fact]
    public async Task IntervaloDoDeploy_SemATabelaNova_409ComCorpo()
    {
        var principal = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(UserPeSgdi) } };
        var paineis = new PePaineisController(new PaineisSemTabela(), Permissoes) { ControllerContext = principal };
        var inadimplencias = new PeInadimplenciasController(new InadimplenciasSemTabela(), Permissoes) { ControllerContext = principal };
        var corpo = Corpo(new { Justificativa = "Texto." });

        foreach (var resultado in new[]
                 {
                     await paineis.Painel(new PePainelConsulta()), await paineis.Conformidade(new PeConformidadeConsulta()),
                     await paineis.ConformidadePlanilha(new PeConformidadeConsulta(), "csv"), await paineis.Resumo(OrgaoSes.Id),
                     await inadimplencias.Listar(new PeInadimplenciasConsulta()), await inadimplencias.DoOrgao(OrgaoSes.Id),
                     await inadimplencias.Justificar(1, corpo)
                 })
        {
            var (status, valor) = Resultado(resultado);
            Assert.Equal((StatusCodes.Status409Conflict, Codigo(ErrorCode.PeModeloIndisponivel)), (status, CodigoDe(valor)));
            Assert.Equal("A Governança Estratégica está sendo atualizada. Tente de novo em alguns minutos.", Propriedade(valor, "Message"));
        }
    }
}
