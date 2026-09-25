using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A inadimplência do art. 11 do Decreto nº 48.899/2026 (E8): o prazo de 5 dias úteis (sem
/// sábado, domingo e feriado do DateTimeHelper), a notificação com os campos obrigatórios, a
/// justificativa aceita, o registro só depois do prazo e com o motivo e a nota de motivação, o
/// saneamento (que tira a marca do painel), a lista com as vigentes primeiro e quem lê e quem grava.
/// </summary>
public class PeInadimplenciaTest : PePaineisTestBase
{
    private static PeInadimplenciaNotificarDTO Notificacao(DateOnly notificadoEm) => new()
    {
        Obrigacao = "Comunicar à SGDI o PDTIC aprovado pelo comitê interno (art. 7º, V)",
        PrazoDescumprido = "30 dias depois da aprovação pelo SGTIC",
        NotificadoEm = Iso(notificadoEm),
        Documento = "Ofício nº 12/2026-SGDI",
        Sei = "00040-00012345/2026-11"
    };

    private async Task<PeInadimplenciaResponse> NotificarAsync(DateOnly notificadoEm, Models.Pgia.PgiaOrgao? orgao = null) =>
        await Inadimplencias.NotificarAsync((orgao ?? OrgaoSes).Id, Notificacao(notificadoEm), await Sgdi());

    private static async Task<IReadOnlyDictionary<string, string>> CamposAsync(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<PeValidacaoException>(acao);
        Assert.Equal((int)ErrorCode.PeInadimplenciaInvalida, ex.Error.Code);
        return ex.Campos;
    }

    // ── Prazo de 5 dias úteis ───────────────────────────────────────────────

    [Fact]
    public void Prazo_CincoDiasUteis_PulaOFimDeSemanaEOsFeriados()
    {
        // Só o fim de semana: sexta 25/09/2026 dá sexta 02/10/2026
        Assert.Equal(new DateOnly(2026, 10, 2), PeInadimplenciaService.Prazo(new DateOnly(2026, 9, 25)));
        // Tiradentes e a fundação de Brasília (terça 21/04/2026): quinta 16/04 dá sexta 24/04, e não quinta 23/04
        Assert.Equal(new DateOnly(2026, 4, 24), PeInadimplenciaService.Prazo(new DateOnly(2026, 4, 16)));
        // A Paixão de Cristo (sexta 03/04/2026, antes da Páscoa de 05/04)
        Assert.Equal(new DateOnly(2026, 4, 9), PeInadimplenciaService.Prazo(new DateOnly(2026, 4, 1)));
        // A Consciência Negra (sexta 20/11/2026) e o Dia do Evangélico do DF (segunda 30/11/2026)
        Assert.Equal(new DateOnly(2026, 11, 24), PeInadimplenciaService.Prazo(new DateOnly(2026, 11, 16)));
        Assert.Equal(new DateOnly(2026, 12, 2), PeInadimplenciaService.Prazo(new DateOnly(2026, 11, 24)));

        // Os feriados do DateTimeHelper
        Assert.True(DateTimeHelper.EhFeriado(new DateTime(2026, 1, 1)));
        Assert.True(DateTimeHelper.EhFeriado(new DateTime(2026, 12, 25)));
        Assert.True(DateTimeHelper.EhFeriado(new DateTime(2027, 3, 26)));
        Assert.False(DateTimeHelper.EhFeriado(new DateTime(2026, 2, 16)));
        Assert.True(DateTimeHelper.EhFeriado(new DateTime(2024, 11, 20)));
        Assert.False(DateTimeHelper.EhFeriado(new DateTime(2023, 11, 20)));
        Assert.Equal(new[] { new DateTime(2024, 3, 31), new DateTime(2025, 4, 20), new DateTime(2026, 4, 5), new DateTime(2027, 3, 28) },
            new[] { 2024, 2025, 2026, 2027 }.Select(DateTimeHelper.Pascoa));
        Assert.False(DateTimeHelper.EhDiaUtil(new DateTime(2026, 4, 21)));
        Assert.False(DateTimeHelper.EhDiaUtil(new DateTime(2026, 4, 18)));
        Assert.True(DateTimeHelper.EhDiaUtil(new DateTime(2026, 4, 22)));
    }

    [Fact]
    public void DiasUteisRestantes_AteOPrazo()
    {
        var prazo = new DateOnly(2026, 4, 24);
        Assert.Equal(5, PeInadimplenciaService.DiasUteisAte(prazo, new DateOnly(2026, 4, 16)));
        Assert.Equal(3, PeInadimplenciaService.DiasUteisAte(prazo, new DateOnly(2026, 4, 21)));
        Assert.Equal(0, PeInadimplenciaService.DiasUteisAte(prazo, prazo));
        Assert.Equal(0, PeInadimplenciaService.DiasUteisAte(prazo, prazo.AddDays(1)));
    }

    // ── Notificar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Notificar_GravaOPrazoDeCincoDiasUteis()
    {
        var hoje = HojeData();

        var notificada = await NotificarAsync(hoje);

        Assert.Equal((OrgaoSes.Id, "SES", "Secretaria de Estado de Saúde"), (notificada.OrgaoId, notificada.OrgaoSigla, notificada.OrgaoNome));
        Assert.Equal(("notificado", "Notificado"), (notificada.Situacao, notificada.SituacaoRotulo));
        Assert.Equal(hoje, notificada.NotificadoEm);
        Assert.Equal(PeInadimplenciaService.Prazo(hoje), notificada.Prazo);
        Assert.True(notificada.Prazo > hoje);
        Assert.Equal((5, false), (notificada.DiasUteisRestantes, notificada.Vencido));
        Assert.Equal(("Ofício nº 12/2026-SGDI", "00040-00012345/2026-11"), (notificada.Documento, notificada.Sei));
        Assert.Equal(UserPeSgdi.Email, notificada.CriadoPor);
        Assert.Null(notificada.Motivo);
        Assert.Null(notificada.RegistradoEm);
        var noBanco = Context.PeInadimplencias.AsNoTracking().Single();
        Assert.Equal(notificada.Prazo, noBanco.Prazo);
    }

    [Fact]
    public async Task Notificar_SemOsDados_400ComCampos()
    {
        var sgdi = await Sgdi();

        var campos = await CamposAsync(() => Inadimplencias.NotificarAsync(OrgaoSes.Id, new PeInadimplenciaNotificarDTO(), sgdi));
        Assert.Equal(new[] { "Documento", "NotificadoEm", "Obrigacao", "PrazoDescumprido" }, campos.Keys.OrderBy(k => k, StringComparer.Ordinal));

        var errado = Notificacao(HojeData().AddDays(3));
        errado.Sei = "12345";
        errado.Obrigacao = new string('a', 501);
        campos = await CamposAsync(() => Inadimplencias.NotificarAsync(OrgaoSes.Id, errado, sgdi));
        Assert.Equal(new[] { "NotificadoEm", "Obrigacao", "Sei" }, campos.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("A data da notificação não pode ser depois de hoje.", campos["NotificadoEm"]);

        errado = Notificacao(HojeData());
        errado.NotificadoEm = "31/12/2026";
        campos = await CamposAsync(() => Inadimplencias.NotificarAsync(OrgaoSes.Id, errado, sgdi));
        Assert.Equal("Use uma data válida, no formato aaaa-mm-dd.", campos["NotificadoEm"]);
        Assert.Empty(Context.PeInadimplencias.AsNoTracking());
    }

    [Fact]
    public async Task Notificar_SoAsgdiOAdministradorEOAdminGeral()
    {
        var dto = Notificacao(HojeData());
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserPeCgtic })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Inadimplencias.NotificarAsync(OrgaoSes.Id, dto, await ContextoDe(user))));

        Assert.Equal("notificado", (await Inadimplencias.NotificarAsync(OrgaoSeec.Id, dto, await ContextoDe(UserPeAdmin))).Situacao);
        Assert.Equal("notificado", (await Inadimplencias.NotificarAsync(OrgaoSes.Id, dto, await AdminGeral())).Situacao);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(async () => await Inadimplencias.NotificarAsync(999999, dto, await Sgdi())));
    }

    // ── Justificar ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Justificar_EncerraANotificada_ESoEla()
    {
        var notificada = await NotificarAsync(HojeData());
        var sgdi = await Sgdi();

        var campos = await CamposAsync(() => Inadimplencias.JustificarAsync(notificada.Id, new PeInadimplenciaJustificarDTO { Justificativa = "  " }, sgdi));
        Assert.Equal("Justificativa", Assert.Single(campos.Keys));

        var justificada = await Inadimplencias.JustificarAsync(notificada.Id,
            new PeInadimplenciaJustificarDTO { Justificativa = "O órgão mostrou que o PDTIC foi comunicado por outro processo SEI." }, sgdi);
        Assert.Equal(("justificado", "Justificado"), (justificada.Situacao, justificada.SituacaoRotulo));
        Assert.Equal("O órgão mostrou que o PDTIC foi comunicado por outro processo SEI.", justificada.Justificativa);
        Assert.Equal(0, justificada.DiasUteisRestantes);

        // Justificada não volta: nem outra justificativa, nem o registro, nem o saneamento
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida),
            await ErroAsync(() => Inadimplencias.JustificarAsync(notificada.Id, new PeInadimplenciaJustificarDTO { Justificativa = "De novo." }, sgdi)));
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida),
            await ErroAsync(() => Inadimplencias.RegistrarAsync(notificada.Id, new PeInadimplenciaRegistrarDTO { Motivo = "omissao_reiterada", NotaMotivacao = "Nota." }, sgdi)));
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida),
            await ErroAsync(() => Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO { SaneadoEm = Iso(HojeData()) }, sgdi)));
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaNaoEncontrada),
            await ErroAsync(() => Inadimplencias.JustificarAsync(999999, new PeInadimplenciaJustificarDTO { Justificativa = "Texto." }, sgdi)));
    }

    // ── Registrar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Registrar_AntesDoPrazo409()
    {
        var notificada = await NotificarAsync(HojeData());

        var ex = await Assert.ThrowsAsync<ApiException>(async () => await Inadimplencias.RegistrarAsync(notificada.Id,
            new PeInadimplenciaRegistrarDTO { Motivo = "descumprimento_prazo", NotaMotivacao = "Nota de motivação." }, await Sgdi()));
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaPrazoAberto), ex.Error.Code);
        Assert.Equal($"O prazo para regularizar ou justificar vai até {Br(notificada.Prazo)}. A inadimplência só pode ser registrada "
                     + $"a partir de {Br(notificada.Prazo.AddDays(1))} (art. 11, II, do Decreto nº 48.899/2026).", ex.Error.Message);
        Assert.Equal("notificado", Context.PeInadimplencias.AsNoTracking().Single().Situacao);
    }

    [Fact]
    public async Task Registrar_DepoisDoPrazo_ComOMotivoObrigatorioEANotaDeMotivacao()
    {
        var notificadoEm = HojeData().AddDays(-30);
        var notificada = await NotificarAsync(notificadoEm);
        Assert.True(notificada.Vencido);
        Assert.Equal(0, notificada.DiasUteisRestantes);
        var sgdi = await Sgdi();

        var campos = await CamposAsync(() => Inadimplencias.RegistrarAsync(notificada.Id, new PeInadimplenciaRegistrarDTO(), sgdi));
        Assert.Equal(new[] { "Motivo", "NotaMotivacao" }, campos.Keys.OrderBy(k => k, StringComparer.Ordinal));
        campos = await CamposAsync(() => Inadimplencias.RegistrarAsync(notificada.Id, new PeInadimplenciaRegistrarDTO
        {
            Motivo = "atraso", NotaMotivacao = "Nota.", ComunicadoControleEm = Iso(notificadoEm.AddDays(-1))
        }, sgdi));
        Assert.Equal(new[] { "ComunicadoControleEm", "Motivo" }, campos.Keys.OrderBy(k => k, StringComparer.Ordinal));

        // A equipe e a consulta do órgão não registram; a Secretaria do CGTIC também não
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserPeCgtic })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Inadimplencias.RegistrarAsync(notificada.Id,
                new PeInadimplenciaRegistrarDTO { Motivo = "omissao_reiterada", NotaMotivacao = "Nota." }, await ContextoDe(user))));

        var registrada = await Inadimplencias.RegistrarAsync(notificada.Id, new PeInadimplenciaRegistrarDTO
        {
            Motivo = "omissao_reiterada",
            NotaMotivacao = "(a) PDTIC aprovado sem comunicação; (b) notificado; (c) portfólio incompleto; (d) comunicar em 5 dias.",
            ComunicadoControleEm = Iso(HojeData())
        }, sgdi);
        Assert.Equal(("inadimplente", "Inadimplente", "omissao_reiterada", "Omissão reiterada"),
            (registrada.Situacao, registrada.SituacaoRotulo, registrada.Motivo, registrada.MotivoRotulo));
        Assert.Equal((HojeData(), UserPeSgdi.Email), (registrada.ComunicadoControleEm!.Value, registrada.RegistradoPor));
        Assert.NotNull(registrada.RegistradoEm);
        Assert.StartsWith("(a) PDTIC aprovado", registrada.NotaMotivacao);

        // Registrada não se registra de novo
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida), await ErroAsync(() => Inadimplencias.RegistrarAsync(notificada.Id,
            new PeInadimplenciaRegistrarDTO { Motivo = "omissao_reiterada", NotaMotivacao = "Nota." }, sgdi)));
    }

    // ── Sanear ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sanear_DaInadimplente_GuardaAData_ETiraDoPainelEDaConformidade()
    {
        var notificadoEm = HojeData().AddDays(-30);
        var notificada = await NotificarAsync(notificadoEm);
        var sgdi = await Sgdi();
        await Inadimplencias.RegistrarAsync(notificada.Id, new PeInadimplenciaRegistrarDTO { Motivo = "descumprimento_prazo", NotaMotivacao = "Nota." }, sgdi);

        Assert.Equal(1, (await PainelAsync()).Alertas.Inadimplentes);
        var marca = (await LinhaAsync(OrgaoSes)).Inadimplencia!;
        Assert.Equal((notificada.Id, "inadimplente", notificada.Prazo), (marca.Id, marca.Situacao, marca.Prazo));

        var campos = await CamposAsync(() => Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO(), sgdi));
        Assert.Equal("SaneadoEm", Assert.Single(campos.Keys));
        campos = await CamposAsync(() => Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO { SaneadoEm = Iso(notificadoEm.AddDays(-2)) }, sgdi));
        Assert.Equal("A data do saneamento não pode ser antes da notificação.", campos["SaneadoEm"]);
        campos = await CamposAsync(() => Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO { SaneadoEm = Iso(HojeData().AddDays(1)) }, sgdi));
        Assert.Equal("A data do saneamento não pode ser depois de hoje.", campos["SaneadoEm"]);

        var saneada = await Inadimplencias.SanearAsync(notificada.Id,
            new PeInadimplenciaSanearDTO { SaneadoEm = Iso(HojeData().AddDays(-1)), Observacao = "O órgão comunicou o PDTIC pelo SEI." }, sgdi);
        Assert.Equal(("saneado", HojeData().AddDays(-1), "O órgão comunicou o PDTIC pelo SEI."), (saneada.Situacao, saneada.SaneadoEm!.Value, saneada.Observacao));
        // O registro da inadimplência fica (é o histórico); a marca sai
        Assert.Equal("descumprimento_prazo", saneada.Motivo);
        Assert.Equal(0, (await PainelAsync()).Alertas.Inadimplentes);
        Assert.Null((await LinhaAsync(OrgaoSes)).Inadimplencia);
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaSituacaoInvalida),
            await ErroAsync(() => Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO { SaneadoEm = Iso(HojeData()) }, sgdi)));
    }

    [Fact]
    public async Task Sanear_DaNotificada_AntesDoRegistro()
    {
        var notificada = await NotificarAsync(HojeData().AddDays(-2));
        Assert.Equal("notificado", (await LinhaAsync(OrgaoSes)).Inadimplencia!.Situacao);

        var saneada = await Inadimplencias.SanearAsync(notificada.Id, new PeInadimplenciaSanearDTO { SaneadoEm = Iso(HojeData()) }, await ContextoDe(UserPeAdmin));

        Assert.Equal("saneado", saneada.Situacao);
        Assert.Null(saneada.Motivo);
        Assert.Null((await LinhaAsync(OrgaoSes)).Inadimplencia);
    }

    // ── Lista ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_VigentesPrimeiroPeloPrazo_FiltrosEPagina()
    {
        var hoje = HojeData();
        var saneada = InadimplenciaNoBanco(OrgaoSes, "saneado", hoje.AddDays(-10));
        var justificada = InadimplenciaNoBanco(OrgaoSes, "justificado", hoje.AddDays(-5));
        var inadimplente = InadimplenciaNoBanco(OrgaoSeec, "inadimplente", hoje.AddDays(-40));
        var notificada = InadimplenciaNoBanco(OrgaoSes, "notificado", hoje.AddDays(-1));
        var sgdi = await Sgdi();

        var lista = await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta(), sgdi);
        Assert.Equal(new[] { inadimplente.Id, notificada.Id, justificada.Id, saneada.Id }, lista.Items.Select(i => i.Id));
        Assert.Equal(4, lista.TotalItems);
        Assert.Equal(("SEEC", "descumprimento_prazo", "Descumprimento de prazo"), (lista.Items[0].OrgaoSigla, lista.Items[0].Motivo, lista.Items[0].MotivoRotulo));

        Assert.Equal(new[] { notificada.Id }, (await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta { Situacao = "notificado" }, sgdi)).Items.Select(i => i.Id));
        Assert.Empty((await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta { Situacao = "vencida" }, sgdi)).Items);
        Assert.Equal(3, (await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta { OrgaoId = OrgaoSes.Id }, sgdi)).TotalItems);
        var pagina = await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta { Page = 2, PageSize = 3 }, sgdi);
        Assert.Equal((saneada.Id, 2, 2), (Assert.Single(pagina.Items).Id, pagina.CurrentPage, pagina.TotalPages));

        // A Secretaria do CGTIC lê tudo
        Assert.Equal(4, (await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta(), await Cgtic())).TotalItems);
    }

    [Fact]
    public async Task Orgao_LeSoAsDoProprio_EmLeitura()
    {
        InadimplenciaNoBanco(OrgaoSes, "notificado");
        InadimplenciaNoBanco(OrgaoSeec, "inadimplente");
        var equipe = await Orgao();
        var consulta = await ContextoDe(UserConsultaSes);

        var lista = await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta(), equipe);
        Assert.Equal("SES", Assert.Single(lista.Items).OrgaoSigla);
        Assert.Equal("SES", Assert.Single(await Inadimplencias.DoOrgaoAsync(OrgaoSes.Id, consulta)).OrgaoSigla);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(() => Inadimplencias.ListarAsync(new PeInadimplenciasConsulta { OrgaoId = OrgaoSeec.Id }, equipe)));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => Inadimplencias.DoOrgaoAsync(OrgaoSeec.Id, consulta)));
        // Quem não tem papel no módulo não lê nada
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Inadimplencias.ListarAsync(new PeInadimplenciasConsulta(), await ContextoDe(UserSemPapel))));
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(async () => await Inadimplencias.DoOrgaoAsync(999999, await Sgdi())));
    }
}
