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
/// Versões do PETIC-DF (E3): a lista, a vigente, o rascunho (criar, mudar, apagar), o envio
/// ao CGTIC e as planilhas. Ler: qualquer papel do módulo. Criar, mudar, apagar e enviar:
/// pe_admin e admin geral. Os registros de cada seção ficam no PeRegistrosController
/// (petic/{id}/secoes/{chave}/registros). Erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento/petic")]
public class PePeticController : ControllerBase
{
    private const string SemPapel = "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.";
    private const string SoAdministrador = "Só o administrador do módulo cuida das versões do PETIC-DF.";

    private readonly IPePeticService _service;
    private readonly IPePlanilhaService _planilhas;
    private readonly IPePermissionService _permissoes;

    public PePeticController(IPePeticService service, IPePlanilhaService planilhas, IPePermissionService permissoes)
    {
        _service = service;
        _planilhas = planilhas;
        _permissoes = permissoes;
    }

    /// <summary>
    /// As versões, da mais nova para a mais antiga, com a deliberação mais recente de cada uma.
    /// Papel de órgão vê só as aprovadas (a vigente e as substituídas).
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Listar() =>
        Executar(_permissoes.PodeLerReferenciais, async ctx => Ok(await _service.ListarAsync(ctx)), SemPapel);

    /// <summary>A versão aprovada (vigente), ou 204 sem corpo quando ainda não há.</summary>
    [HttpGet("vigente")]
    public Task<IActionResult> Vigente() =>
        Executar(_permissoes.PodeLerReferenciais, async _ =>
        {
            var vigente = await _service.VigenteAsync();
            return vigente == null ? NoContent() : Ok(vigente);
        }, SemPapel);

    [HttpGet("{id:long}")]
    public Task<IActionResult> Obter(long id) =>
        Executar(_permissoes.PodeLerReferenciais, async ctx => Ok(await _service.ObterAsync(id, ctx)), SemPapel);

    /// <summary>
    /// Cria o rascunho ({ Titulo, VigenciaInicio, VigenciaFim, CopiarDaVigente }); 409 se já
    /// houver versão em rascunho ou em deliberação. Devolve 201 com a versão.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> Criar([FromBody] JsonElement corpo) =>
        Executar(_permissoes.PodeEditarReferenciais,
            async ctx => StatusCode(StatusCodes.Status201Created, await _service.CriarAsync(PeCorpo.Ler<PePeticCriarDTO>(corpo), ctx)),
            SoAdministrador);

    /// <summary>Título e vigência do rascunho (campo ausente não muda; nulo limpa a data).</summary>
    [HttpPut("{id:long}")]
    public Task<IActionResult> Atualizar(long id, [FromBody] JsonElement corpo) =>
        Executar(_permissoes.PodeEditarReferenciais,
            async ctx => Ok(await _service.AtualizarAsync(id, PeCorpoParcial.Ler<PePeticAtualizarDTO>(corpo), ctx)),
            SoAdministrador);

    /// <summary>
    /// A prévia do envio (F1, A06): { PodeEnviar, Pendencias: [{ SecaoChave, SecaoTitulo, Motivo }],
    /// Motivo }. Lê quem lê os referenciais (o papel de órgão não vê o rascunho: 404).
    /// </summary>
    [HttpGet("{id:long}/envio")]
    public Task<IActionResult> Envio(long id) =>
        Executar(_permissoes.PodeLerReferenciais, async ctx => Ok(await _service.EnvioAsync(id, ctx)), SemPapel);

    /// <summary>
    /// Envia ao CGTIC: confere as pendências (400 PePeticIncompleto com { Code, Message, Pendencias },
    /// a lista da prévia), cria a deliberação e devolve a versão em deliberação.
    /// </summary>
    [HttpPost("{id:long}/enviar")]
    public Task<IActionResult> Enviar(long id) =>
        Executar(_permissoes.PodeEditarReferenciais, async ctx => Ok(await _service.EnviarAsync(id, ctx)), SoAdministrador);

    /// <summary>Apaga o rascunho que nunca foi enviado (204).</summary>
    [HttpDelete("{id:long}")]
    public Task<IActionResult> Excluir(long id) =>
        Executar(_permissoes.PodeEditarReferenciais, async ctx =>
        {
            await _service.ExcluirAsync(id, ctx);
            return NoContent();
        }, SoAdministrador);

    // ── Planilhas ───────────────────────────────────────────────────────────

    /// <summary>Planilha de uma seção (?formato=csv ou xlsx; padrão xlsx).</summary>
    [HttpGet("{id:long}/planilha/{secaoChave}")]
    public Task<IActionResult> PlanilhaDaSecao(long id, string secaoChave, [FromQuery] string? formato) =>
        Executar(_permissoes.PodeLerReferenciais, async ctx =>
        {
            var planilha = await _planilhas.PeticSecaoAsync(id, secaoChave, formato, ctx);
            return File(planilha.Conteudo, planilha.TipoMime, planilha.NomeArquivo);
        }, SemPapel);

    /// <summary>Planilha completa (uma aba por seção e a Leia-me; só xlsx).</summary>
    [HttpGet("{id:long}/planilha")]
    public Task<IActionResult> PlanilhaCompleta(long id, [FromQuery] string? formato) =>
        Executar(_permissoes.PodeLerReferenciais, async ctx =>
        {
            var planilha = await _planilhas.PeticCompletaAsync(id, formato, ctx);
            return File(planilha.Conteudo, planilha.TipoMime, planilha.NomeArquivo);
        }, SemPapel);

    private Task<IActionResult> Executar(Func<PeUserContext, bool> pode, Func<PeUserContext, Task<IActionResult>> acao,
        string semPermissao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            if (!pode(ctx)) return PeRespostas.SemPermissao(semPermissao);
            return await acao(ctx);
        });
}
