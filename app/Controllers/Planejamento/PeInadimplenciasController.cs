using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// A inadimplência do art. 11 do Decreto nº 48.899/2026 (E8): a lista de todas (papéis globais e
/// admin geral; a equipe e a consulta do órgão, as do próprio), as de um órgão, notificar,
/// justificar, registrar e sanear (pe_admin, pe_sgdi e admin geral). Todas as escritas devolvem a
/// inadimplência; a notificação, 201. Os corpos são lidos à mão (<see cref="PeCorpo"/>); a
/// validação volta 400 com Campos. Erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PeInadimplenciasController : ControllerBase
{
    private readonly IPeInadimplenciaService _inadimplencias;
    private readonly IPePermissionService _permissoes;

    public PeInadimplenciasController(IPeInadimplenciaService inadimplencias, IPePermissionService permissoes)
    {
        _inadimplencias = inadimplencias;
        _permissoes = permissoes;
    }

    /// <summary>As inadimplências (?Situacao=&amp;OrgaoId=&amp;Page=&amp;PageSize=), as vigentes primeiro.</summary>
    [HttpGet("inadimplencias")]
    public Task<IActionResult> Listar([FromQuery] PeInadimplenciasConsulta consulta) =>
        Executar(async ctx => Ok(await _inadimplencias.ListarAsync(consulta, ctx)));

    /// <summary>As inadimplências de um órgão (quem vê o órgão).</summary>
    [HttpGet("orgaos/{id:long}/inadimplencias")]
    public Task<IActionResult> DoOrgao(long id) =>
        Executar(async ctx => Ok(await _inadimplencias.DoOrgaoAsync(id, ctx)));

    /// <summary>Notifica o órgão ({ Obrigacao, PrazoDescumprido, NotificadoEm, Documento, Sei }); devolve 201 com o prazo de 5 dias úteis.</summary>
    [HttpPost("orgaos/{id:long}/inadimplencias")]
    public Task<IActionResult> Notificar(long id, [FromBody] System.Text.Json.JsonElement corpo) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created,
            await _inadimplencias.NotificarAsync(id, PeCorpo.Ler<PeInadimplenciaNotificarDTO>(corpo), ctx)));

    /// <summary>A justificativa aceita ({ Justificativa }): encerra a notificação.</summary>
    [HttpPost("inadimplencias/{id:long}/justificar")]
    public Task<IActionResult> Justificar(long id, [FromBody] System.Text.Json.JsonElement corpo) =>
        Executar(async ctx => Ok(await _inadimplencias.JustificarAsync(id, PeCorpo.Ler<PeInadimplenciaJustificarDTO>(corpo), ctx)));

    /// <summary>Registra a inadimplência ({ Motivo, NotaMotivacao, ComunicadoControleEm }), só depois do prazo (antes, 409).</summary>
    [HttpPost("inadimplencias/{id:long}/registrar")]
    public Task<IActionResult> Registrar(long id, [FromBody] System.Text.Json.JsonElement corpo) =>
        Executar(async ctx => Ok(await _inadimplencias.RegistrarAsync(id, PeCorpo.Ler<PeInadimplenciaRegistrarDTO>(corpo), ctx)));

    /// <summary>Registra o saneamento ({ SaneadoEm, Observacao }): a marca sai do painel.</summary>
    [HttpPost("inadimplencias/{id:long}/sanear")]
    public Task<IActionResult> Sanear(long id, [FromBody] System.Text.Json.JsonElement corpo) =>
        Executar(async ctx => Ok(await _inadimplencias.SanearAsync(id, PeCorpo.Ler<PeInadimplenciaSanearDTO>(corpo), ctx)));

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
