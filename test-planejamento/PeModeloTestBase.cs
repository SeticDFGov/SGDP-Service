using System.Security.Claims;
using System.Text.Json;
using api.Planejamento;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Base dos testes do modelo configurável (E2): a base da E1 (órgãos SES e SEEC, uma
/// pessoa por papel) com o modelo inicial já carregado pelo carregador real, mais os
/// serviços, os controllers e atalhos para achar níveis, passos, seções, campos e opções
/// pela chave.
/// </summary>
public abstract class PeModeloTestBase : PeTestBase
{
    protected readonly PeModeloService Modelo;
    protected readonly PeOrgaoService Orgaos;
    protected readonly string EmailAdmin;

    protected PeModeloTestBase(bool carregar = true)
    {
        Modelo = new PeModeloService(Context);
        Orgaos = new PeOrgaoService(Context);
        EmailAdmin = UserPeAdmin.Email;
        if (carregar) new PeCarregadorModelo(Context).CarregarAsync().GetAwaiter().GetResult();
    }

    // ── Atalhos ───────────────────────────────────────────────────────────────

    protected PeNivel Nivel(string codigo) => Context.PeNiveis.AsNoTracking().Single(n => n.Codigo == codigo);

    protected long NivelId(string codigo) => Nivel(codigo).Id;

    protected PePasso Passo(string chave) => Context.PePassos.AsNoTracking().Single(p => p.Chave == chave);

    protected PeSecao Secao(string chave) => Context.PeSecoes.AsNoTracking().Single(s => s.Chave == chave);

    protected PeCampo Campo(string secao, string campo) =>
        Context.PeCampos.AsNoTracking().Include(c => c.Secao).Single(c => c.Secao!.Chave == secao && c.Chave == campo);

    protected PeOpcao Opcao(string secao, string campo, string valor)
    {
        var id = Campo(secao, campo).Id;
        return Context.PeOpcoes.AsNoTracking().Single(o => o.CampoId == id && o.Valor == valor);
    }

    /// <summary>Situação de um passo, seção ou campo em cada nível: "B I A" (o = obrigatório, p = opcional, d = desligado).</summary>
    protected string SituacoesDoPasso(string chave)
    {
        var id = Passo(chave).Id;
        return Resumo(Context.PePassosNivel.AsNoTracking().Where(n => n.PassoId == id).ToList());
    }

    protected string SituacoesDaSecao(string chave)
    {
        var id = Secao(chave).Id;
        return Resumo(Context.PeSecoesNivel.AsNoTracking().Where(n => n.SecaoId == id).ToList());
    }

    protected string SituacoesDoCampo(string secao, string campo)
    {
        var id = Campo(secao, campo).Id;
        return Resumo(Context.PeCamposNivel.AsNoTracking().Where(n => n.CampoId == id).ToList());
    }

    private string Resumo<T>(List<T> linhas) where T : IPeSituacaoNivel
    {
        string Uma(string codigo)
        {
            var nivelId = NivelId(codigo);
            var s = linhas.FirstOrDefault(l => l.NivelId == nivelId)?.Situacao ?? PeDominios.Situacao.Desligado;
            return s switch { "obrigatorio" => "o", "opcional" => "p", _ => "d" };
        }
        return $"{Uma("basico")} {Uma("intermediario")} {Uma("avancado")}";
    }

    /// <summary>{ Niveis } com a mesma situação nos três níveis do modelo inicial.</summary>
    protected PeSituacoesDTO Todos(string situacao) => Em(situacao, situacao, situacao);

    protected PeSituacoesDTO Em(string basico, string intermediario, string avancado) => new()
    {
        Niveis = new Dictionary<string, string?>
        {
            [NivelId("basico").ToString()] = basico,
            [NivelId("intermediario").ToString()] = intermediario,
            [NivelId("avancado").ToString()] = avancado
        }
    };

    protected PeSituacoesDTO So(string codigo, string situacao) => new()
    {
        Niveis = new Dictionary<string, string?> { [NivelId(codigo).ToString()] = situacao }
    };

    // ── Trilha ────────────────────────────────────────────────────────────────

    protected Task<PeTrilhaResponse> TrilhaAsync(PgiaOrgao orgao) => Modelo.TrilhaAsync(orgao.Id);

    protected static PeTrilhaPasso? NaTrilha(PeTrilhaResponse trilha, string chave) =>
        trilha.Etapas.SelectMany(e => e.Passos).FirstOrDefault(p => p.Chave == chave);

    protected static List<PeTrilhaPasso> PassosDa(PeTrilhaResponse trilha) => trilha.Etapas.SelectMany(e => e.Passos).ToList();

    protected async Task DefinirNivelDoOrgaoAsync(PgiaOrgao orgao, string codigo) =>
        await Orgaos.DefinirNivelAsync(orgao.Id, new PeOrgaoNivelDTO { NivelId = NivelId(codigo), Justificativa = "Teste" }, EmailAdmin);

    // ── Erros ─────────────────────────────────────────────────────────────────

    protected static async Task<int> ErroAsync(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(acao);
        return ex.Error.Code;
    }

    protected static int Codigo(ErrorCode codigo) => (int)codigo;

    // ── Histórico ─────────────────────────────────────────────────────────────

    protected List<PeModeloHistorico> HistoricoDe(string entidade, long id) =>
        Context.PeModeloHistorico.AsNoTracking()
            .Where(h => h.Entidade == entidade && h.EntidadeId == id)
            .OrderBy(h => h.Id)
            .ToList();

    // ── Controllers ───────────────────────────────────────────────────────────

    protected PeModeloController ControladorModelo(User user) => ControladorModeloDe(Principal(user));

    protected PeModeloController ControladorModeloDe(ClaimsPrincipal principal) => new(Modelo, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
    };

    protected PeOrgaosController ControladorOrgaos(User user) => new(Orgaos, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected static JsonElement Corpo(object valor) => JsonSerializer.SerializeToElement(valor);

    /// <summary>Status e corpo de um IActionResult (ObjectResult ou StatusCodeResult).</summary>
    protected static (int Status, object? Valor) Resultado(IActionResult resultado) => resultado switch
    {
        ObjectResult o => (o.StatusCode ?? StatusCodes.Status200OK, o.Value),
        StatusCodeResult s => (s.StatusCode, null),
        _ => throw new InvalidOperationException("Resultado inesperado: " + resultado.GetType().Name)
    };
}
