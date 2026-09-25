using System.Text.Json;
using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// Fluxos (E6): os fluxos do guia como modelo (o administrador do módulo edita; todo papel lê),
/// a cópia que o órgão adapta no PDTIC (a equipe do órgão edita, com o PDTIC em elaboração ou
/// devolvido), o desenho automático em SVG (image/svg+xml) e o cronograma sugerido do plano de
/// trabalho. Desde a E9, a geometria do mesmo desenho (JSON), de onde o SVG sai, para o editor
/// visual. Definição inválida: 400 { Code, Message, Erros } (na geometria, só a ilegível). Sem
/// as tabelas pe_fluxo (o PR publica o código antes da migration) ou antes de o carregador
/// trazer os fluxos: 409 com corpo. Erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PeFluxosController : ControllerBase
{
    private readonly IPeFluxoService _fluxos;
    private readonly IPePermissionService _permissoes;

    public PeFluxosController(IPeFluxoService fluxos, IPePermissionService permissoes)
    {
        _fluxos = fluxos;
        _permissoes = permissoes;
    }

    // ── Modelos (fluxos do guia) ────────────────────────────────────────────

    /// <summary>Os fluxos do guia como modelo: [ { Chave, Nome, FiguraGuia, Ordem, Definicao } ].</summary>
    [HttpGet("modelo/fluxos")]
    public Task<IActionResult> Modelos() =>
        Executar(async ctx => Ok(await _fluxos.ModelosAsync(ctx)));

    /// <summary>Grava o nome e a definição de um modelo ({ Nome, Definicao }); devolve o fluxo.</summary>
    [HttpPut("modelo/fluxos/{chave}")]
    public Task<IActionResult> SalvarModelo(string chave, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _fluxos.SalvarModeloAsync(chave, PeFluxoSalvarDTO.Ler(corpo), ctx)));

    /// <summary>O desenho do modelo (image/svg+xml), com os nomes padrão.</summary>
    [HttpGet("modelo/fluxos/{chave}/svg")]
    public Task<IActionResult> SvgDoModelo(string chave) =>
        Executar(async ctx => Svg(await _fluxos.SvgDoModeloAsync(chave, ctx)));

    /// <summary>A geometria do desenho do modelo (E9), com os nomes padrão: a mesma de onde sai o SVG.</summary>
    [HttpGet("modelo/fluxos/{chave}/geometria")]
    public Task<IActionResult> GeometriaDoModelo(string chave) =>
        Executar(async ctx => Ok(await _fluxos.GeometriaDoModeloAsync(chave, ctx)));

    // ── Cópia do órgão ──────────────────────────────────────────────────────

    /// <summary>Os fluxos do PDTIC: [ { Chave, Nome, FiguraGuia, Personalizado, ModeloMudou, AlteradoEm, AlteradoPor } ].</summary>
    [HttpGet("pdtic/{id:long}/fluxos")]
    public Task<IActionResult> DoPdtic(long id) =>
        Executar(async ctx => Ok(await _fluxos.DoPdticAsync(id, ctx)));

    /// <summary>O fluxo do PDTIC (a cópia do órgão ou, sem cópia, o modelo).</summary>
    [HttpGet("pdtic/{id:long}/fluxos/{chave}")]
    public Task<IActionResult> Obter(long id, string chave) =>
        Executar(async ctx => Ok(await _fluxos.ObterAsync(id, chave, ctx)));

    /// <summary>Grava a cópia do órgão ({ Nome, Definicao }); devolve o fluxo.</summary>
    [HttpPut("pdtic/{id:long}/fluxos/{chave}")]
    public Task<IActionResult> Salvar(long id, string chave, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _fluxos.SalvarAsync(id, chave, PeFluxoSalvarDTO.Ler(corpo), ctx)));

    /// <summary>Volta o fluxo ao modelo do guia (apaga a cópia do órgão); devolve o fluxo.</summary>
    [HttpPost("pdtic/{id:long}/fluxos/{chave}/restaurar")]
    public Task<IActionResult> Restaurar(long id, string chave) =>
        Executar(async ctx => Ok(await _fluxos.RestaurarAsync(id, chave, ctx)));

    /// <summary>O desenho do fluxo do PDTIC (image/svg+xml), com os nomes do dicionário do órgão.</summary>
    [HttpGet("pdtic/{id:long}/fluxos/{chave}/svg")]
    public Task<IActionResult> SvgDoPdtic(long id, string chave) =>
        Executar(async ctx => Svg(await _fluxos.SvgAsync(id, chave, ctx)));

    /// <summary>A geometria do desenho do fluxo do PDTIC (E9), com os nomes do órgão: a mesma de onde sai o SVG.</summary>
    [HttpGet("pdtic/{id:long}/fluxos/{chave}/geometria")]
    public Task<IActionResult> GeometriaDoPdtic(long id, string chave) =>
        Executar(async ctx => Ok(await _fluxos.GeometriaDoPdticAsync(id, chave, ctx)));

    // ── Editor ──────────────────────────────────────────────────────────────

    /// <summary>O desenho de uma definição ainda não gravada ({ Definicao, PdticId, Nome }), conferida antes.</summary>
    [HttpPost("fluxos/desenho")]
    public Task<IActionResult> Desenho([FromBody] JsonElement corpo) =>
        Executar(async ctx => Svg(await _fluxos.DesenhoAsync(PeFluxoDesenhoDTO.Ler(corpo), ctx)));

    /// <summary>
    /// A geometria de uma definição ainda não gravada ({ Definicao, PdticId, Nome }), a cada
    /// mudança do editor visual (E9): a definição incompleta também é desenhada, com os problemas
    /// em Erros; 400 { Code, Message, Erros } só para a ilegível. Não grava nada.
    /// </summary>
    [HttpPost("fluxos/geometria")]
    public Task<IActionResult> Geometria([FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _fluxos.GeometriaAsync(PeFluxoDesenhoDTO.Ler(corpo), ctx)));

    /// <summary>Confere uma definição sem gravar ({ Definicao }): { Valida, Erros, Definicao com os números }.</summary>
    [HttpPost("fluxos/validacao")]
    public Task<IActionResult> Validar([FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _fluxos.ValidarAsync(PeFluxoDesenhoDTO.Ler(corpo), ctx)));

    /// <summary>Os nomes do dicionário para raias e passos (?pdticId= traz os do órgão).</summary>
    [HttpGet("fluxos/nomes")]
    public Task<IActionResult> Nomes([FromQuery] long? pdticId) =>
        Executar(async ctx => Ok(await _fluxos.NomesAsync(pdticId, ctx)));

    // ── Cronograma do plano de trabalho ─────────────────────────────────────

    /// <summary>Cria as linhas sugeridas do cronograma (só no cronograma vazio); 201 com os registros.</summary>
    [HttpPost("pdtic/{id:long}/cronograma/sugerir")]
    public Task<IActionResult> SugerirCronograma(long id) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created, await _fluxos.SugerirCronogramaAsync(id, ctx)));

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static IActionResult Svg(string svg) =>
        new ContentResult { Content = svg, ContentType = PeFluxoService.TipoSvg + "; charset=utf-8", StatusCode = StatusCodes.Status200OK };

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
