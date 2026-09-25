using System.Globalization;
using api.Planejamento;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Models.Planejamento;
using service.Planejamento;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E8 (painéis da SGDI, conformidade, inadimplência, árvore do PETIC-DF e
/// página do órgão): a base do acompanhamento (E7, rodada B) com os serviços da E8, atalhos para
/// criar órgãos a mais, abrir e preencher PDTICs pelo admin geral (que edita qualquer órgão),
/// aprovar com o ato de hoje, gravar inadimplências à mão e montar os controllers.
/// </summary>
public abstract class PePaineisTestBase : PeAcompanhamentoTestBase
{
    protected readonly PeInadimplenciaService Inadimplencias;
    protected readonly PePainelService Paineis;

    protected PePaineisTestBase()
    {
        Inadimplencias = new PeInadimplenciaService(Context, Permissoes);
        Paineis = new PePainelService(Context, Permissoes, Pdtics, Acompanhamento, Orgaos);
    }

    protected static DateOnly HojeData() => PeCiclos.Hoje();

    protected static string Iso(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string Br(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    protected Task<PeUserContext> AdminGeral() => ContextoDe(UserAdminGeral);

    protected Task<PeUserContext> Sgdi() => ContextoDe(UserPeSgdi);

    /// <summary>Um órgão ativo a mais, com a unidade dele.</summary>
    protected PgiaOrgao NovoOrgao(string sigla, string? nome = null)
    {
        var unidade = new Unidade { id = Guid.NewGuid(), Nome = sigla, CodigoExterno = sigla };
        Context.Unidades.Add(unidade);
        var orgao = new PgiaOrgao
        {
            Sigla = sigla,
            Nome = nome ?? $"Órgão {sigla}",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaOrgaos.Add(orgao);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        return orgao;
    }

    /// <summary>Abre o PDTIC do órgão pelo admin geral (versão 1.0, em elaboração).</summary>
    protected async Task<long> AbrirPdticAsync(PgiaOrgao orgao) =>
        (await Pdtics.AbrirAsync(new PePdticCriarDTO { OrgaoId = orgao.Id }, await AdminGeral())).Id;

    /// <summary>A etapa 1 do Básico feita (abrangência, nomes, SGTIC, equipe, metodologia, estratégias e princípios), pelo admin geral.</summary>
    protected async Task PreencherEtapa1Async(long id)
    {
        var admin = UserAdminGeral;
        await IncluirNoPdticAsync(id, "abrangencia", new
        {
            tipo_abrangencia = "todo_com_vinculadas", unidades = "Sede", vigencia_inicio = "2026-01-01", vigencia_fim = "2029-12-31", periodicidade_revisao = "anual"
        }, user: admin);
        await IncluirNoPdticAsync(id, "nomes", new { comite = "SGTIC", equipe_elaboracao = "Equipe", autoridade_cargo = "Secretário", autoridade_nome = "Fulano", unidade_tic = "TIC" }, user: admin);
        await IncluirNoPdticAsync(id, "sgtic", new { forma = "subcomite", ato_tipo = "portaria", ato_numero = "12/2026", ato_data = "2026-01-10" }, user: admin);
        await IncluirNoPdticAsync(id, "sgtic_membros", new { nome = "Maria da Silva", papel = "presidente" }, user: admin);
        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Ana Souza" }, user: admin);
        await IncluirNoPdticAsync(id, "metodologia", new { metodologia_adotada = "guia_sisp" }, user: admin);
        await IncluirNoPdticAsync(id, "alinhamento_petic", new { }, user: admin);
        await IncluirNoPdticAsync(id, "principios_diretrizes", new { principio = "Priorizar soluções corporativas.", origem = "art4" }, user: admin);
    }

    /// <summary>A decisão "aprovado" do CGTIC com o ato de hoje (a regra da revisão conta a partir do ato).</summary>
    protected async Task<PeDeliberacaoResponse> AprovarHojeAsync(long pdticId) =>
        await Deliberacoes.DecidirAsync(DeliberacaoAguardando(pdticId).Id, new PeDecidirDTO
        {
            Decisao = "aprovado",
            AtoTipo = "Resolução",
            AtoNumero = "7/2026",
            AtoData = Iso(HojeData()),
            Sei = "00040-00012345/2026-11"
        }, await Cgtic());

    /// <summary>O caminho da SES no Básico até a publicação de hoje, com a aprovação de hoje.</summary>
    protected async Task<long> PublicadoHojeAsync()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await AprovarHojeAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);
        return pdtic.Id;
    }

    /// <summary>Muda a data do ato da aprovação do CGTIC do PDTIC, à mão.</summary>
    protected void DataDoAto(long pdticId, DateOnly data)
    {
        var deliberacao = Context.PeDeliberacoes.Single(d => d.ObjetoTipo == "pdtic" && d.ObjetoId == pdticId && d.Situacao == "aprovado");
        deliberacao.AtoData = data;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <summary>Uma inadimplência gravada à mão (para o painel e a conformidade).</summary>
    protected PeInadimplencia InadimplenciaNoBanco(PgiaOrgao orgao, string situacao, DateOnly? notificadoEm = null)
    {
        var notificado = notificadoEm ?? HojeData().AddDays(-30);
        var registrada = situacao is "inadimplente" or "saneado";
        var inadimplencia = new PeInadimplencia
        {
            OrgaoId = orgao.Id,
            Obrigacao = "Comunicar à SGDI o PDTIC aprovado pelo comitê interno (art. 7º, V, do Decreto nº 48.899/2026)",
            PrazoDescumprido = "30 dias depois da aprovação",
            NotificadoEm = notificado,
            Documento = "Ofício nº 1/2026-SGDI",
            Prazo = PeInadimplenciaService.Prazo(notificado),
            Situacao = situacao,
            Justificativa = situacao == "justificado" ? "Justificativa aceita." : null,
            Motivo = registrada ? "descumprimento_prazo" : null,
            NotaMotivacao = registrada ? "Nota de motivação." : null,
            RegistradoEm = registrada ? DateTime.UtcNow : null,
            RegistradoPor = registrada ? UserPeSgdi.Email : null,
            SaneadoEm = situacao == "saneado" ? HojeData() : null,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = UserPeSgdi.Email
        };
        Context.PeInadimplencias.Add(inadimplencia);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        return inadimplencia;
    }

    /// <summary>Uma deliberação aguardando à mão (o PDTIC em aprovação sem passar pelo envio).</summary>
    protected void DeliberacaoAguardandoNoBanco(long pdticId)
    {
        Context.PeDeliberacoes.Add(new PeDeliberacao
        {
            ObjetoTipo = PeDominios.ObjetoDeliberacao.Pdtic,
            ObjetoId = pdticId,
            VersaoObjeto = "1.0",
            EnviadoEm = DateTime.UtcNow,
            EnviadoPor = UserOrgaoSes.Email,
            Situacao = PeDominios.SituacaoDeliberacao.Aguardando,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = UserOrgaoSes.Email
        });
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    protected async Task<PeConformidadeOrgaoResponse> LinhaAsync(PgiaOrgao orgao, User? user = null) =>
        (await Paineis.ConformidadeAsync(new PeConformidadeConsulta(), await ContextoDe(user ?? UserPeSgdi))).Orgaos.Single(o => o.OrgaoId == orgao.Id);

    protected async Task<PePainelGeralResponse> PainelAsync(long? nivelId = null, string? situacao = null, User? user = null) =>
        await Paineis.PainelAsync(new PePainelConsulta { NivelId = nivelId, Situacao = situacao }, await ContextoDe(user ?? UserPeSgdi));

    protected static int Quantidade(PePainelGeralResponse painel, string situacao) => painel.PorSituacao.Single(s => s.Chave == situacao).Quantidade;

    // ── Controllers ───────────────────────────────────────────────────────────

    protected PePaineisController ControladorPaineis(User user) => new(Paineis, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected PeInadimplenciasController ControladorInadimplencias(User user) => new(Inadimplencias, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected List<PeCiclo> CiclosNoBanco(long pdticId) => Context.PeCiclos.AsNoTracking().Where(c => c.PdticId == pdticId).ToList();
}
