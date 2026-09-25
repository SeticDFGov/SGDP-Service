using System.Text.Json;
using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Planejamento;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// Registros das seções (E3): o mesmo contrato para o PETIC-DF (uma versão), para o catálogo
/// do DF (princípios e diretrizes do ciclo) e, desde a E4, para o PDTIC de cada órgão. Mais
/// os catálogos para os campos de ligação e as planilhas do DF. PETIC-DF e DF: ler, qualquer
/// papel do módulo; editar, pe_admin e admin geral (no PETIC-DF, só a versão em rascunho).
/// PDTIC: ler, quem vê o órgão; editar, a equipe do órgão (e o admin geral), só em
/// elaboração ou devolvido. A autorização fica no IPeRegistroService. Erros como
/// { Code, Message }; a validação traz também Campos (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PeRegistrosController : ControllerBase
{
    private readonly IPeRegistroService _registros;
    private readonly IPePlanilhaService _planilhas;
    private readonly IPePermissionService _permissoes;

    public PeRegistrosController(IPeRegistroService registros, IPePlanilhaService planilhas, IPePermissionService permissoes)
    {
        _registros = registros;
        _planilhas = planilhas;
        _permissoes = permissoes;
    }

    // ── PETIC-DF (uma versão) ───────────────────────────────────────────────

    /// <summary>A seção (campos visíveis) e os registros da versão.</summary>
    [HttpGet("petic/{peticId:long}/secoes/{secaoChave}/registros")]
    public Task<IActionResult> ListarDoPetic(long peticId, string secaoChave) =>
        Executar(async ctx => Ok(await _registros.ListarAsync(PeDono.DoPetic(peticId), secaoChave, ctx)));

    /// <summary>Inclui um registro ({ Dados, Vinculos }); devolve 201 com o registro criado.</summary>
    [HttpPost("petic/{peticId:long}/secoes/{secaoChave}/registros")]
    public Task<IActionResult> CriarNoPetic(long peticId, string secaoChave, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Criado(await _registros.CriarAsync(PeDono.DoPetic(peticId), secaoChave, PeRegistroSalvarDTO.Ler(corpo), ctx)));

    [HttpPut("petic/{peticId:long}/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> AtualizarNoPetic(long peticId, string secaoChave, long id, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _registros.AtualizarAsync(PeDono.DoPetic(peticId), secaoChave, id, PeRegistroSalvarDTO.Ler(corpo), ctx)));

    [HttpDelete("petic/{peticId:long}/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> ExcluirDoPetic(long peticId, string secaoChave, long id) =>
        Executar(async ctx =>
        {
            await _registros.ExcluirAsync(PeDono.DoPetic(peticId), secaoChave, id, ctx);
            return NoContent();
        });

    /// <summary>Nova ordem ({ Ids }, todos os registros da seção); devolve a lista.</summary>
    [HttpPut("petic/{peticId:long}/secoes/{secaoChave}/registros/ordem")]
    public Task<IActionResult> OrdenarNoPetic(long peticId, string secaoChave, [FromBody] PeOrdemDTO dto) =>
        Executar(async ctx => Ok(await _registros.OrdenarAsync(PeDono.DoPetic(peticId), secaoChave, dto, ctx)));

    // ── PDTIC de um órgão (E4) ──────────────────────────────────────────────

    /// <summary>
    /// A seção (campos visíveis no nível do órgão) e os registros do PDTIC. Seção por ciclo (E7,
    /// rodada B): ?cicloId= obrigatório (os registros daquele ciclo); nas outras, ignorado.
    /// </summary>
    [HttpGet("pdtic/{pdticId:long}/secoes/{secaoChave}/registros")]
    public Task<IActionResult> ListarDoPdtic(long pdticId, string secaoChave, [FromQuery] long? cicloId = null) =>
        Executar(async ctx => Ok(await _registros.ListarAsync(PeDono.DoPdtic(pdticId), secaoChave, ctx, cicloId)));

    /// <summary>Inclui um registro ({ Dados, Vinculos }); devolve 201 com o registro criado.</summary>
    [HttpPost("pdtic/{pdticId:long}/secoes/{secaoChave}/registros")]
    public Task<IActionResult> CriarNoPdtic(long pdticId, string secaoChave, [FromBody] JsonElement corpo, [FromQuery] long? cicloId = null) =>
        Executar(async ctx => Criado(await _registros.CriarAsync(PeDono.DoPdtic(pdticId), secaoChave, PeRegistroSalvarDTO.Ler(corpo), ctx, cicloId)));

    [HttpPut("pdtic/{pdticId:long}/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> AtualizarNoPdtic(long pdticId, string secaoChave, long id, [FromBody] JsonElement corpo,
        [FromQuery] long? cicloId = null) =>
        Executar(async ctx => Ok(await _registros.AtualizarAsync(PeDono.DoPdtic(pdticId), secaoChave, id, PeRegistroSalvarDTO.Ler(corpo), ctx,
            cicloId)));

    [HttpDelete("pdtic/{pdticId:long}/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> ExcluirDoPdtic(long pdticId, string secaoChave, long id, [FromQuery] long? cicloId = null) =>
        Executar(async ctx =>
        {
            await _registros.ExcluirAsync(PeDono.DoPdtic(pdticId), secaoChave, id, ctx, cicloId);
            return NoContent();
        });

    /// <summary>Nova ordem ({ Ids }, todos os registros da seção); devolve a lista.</summary>
    [HttpPut("pdtic/{pdticId:long}/secoes/{secaoChave}/registros/ordem")]
    public Task<IActionResult> OrdenarNoPdtic(long pdticId, string secaoChave, [FromBody] PeOrdemDTO dto, [FromQuery] long? cicloId = null) =>
        Executar(async ctx => Ok(await _registros.OrdenarAsync(PeDono.DoPdtic(pdticId), secaoChave, dto, ctx, cicloId)));

    // ── Catálogo do DF ──────────────────────────────────────────────────────

    [HttpGet("df/secoes/{secaoChave}/registros")]
    public Task<IActionResult> ListarDoDf(string secaoChave) =>
        Executar(async ctx => Ok(await _registros.ListarAsync(PeDono.Df, secaoChave, ctx)));

    [HttpPost("df/secoes/{secaoChave}/registros")]
    public Task<IActionResult> CriarNoDf(string secaoChave, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Criado(await _registros.CriarAsync(PeDono.Df, secaoChave, PeRegistroSalvarDTO.Ler(corpo), ctx)));

    [HttpPut("df/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> AtualizarNoDf(string secaoChave, long id, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _registros.AtualizarAsync(PeDono.Df, secaoChave, id, PeRegistroSalvarDTO.Ler(corpo), ctx)));

    [HttpDelete("df/secoes/{secaoChave}/registros/{id:long}")]
    public Task<IActionResult> ExcluirDoDf(string secaoChave, long id) =>
        Executar(async ctx =>
        {
            await _registros.ExcluirAsync(PeDono.Df, secaoChave, id, ctx);
            return NoContent();
        });

    [HttpPut("df/secoes/{secaoChave}/registros/ordem")]
    public Task<IActionResult> OrdenarNoDf(string secaoChave, [FromBody] PeOrdemDTO dto) =>
        Executar(async ctx => Ok(await _registros.OrdenarAsync(PeDono.Df, secaoChave, dto, ctx)));

    /// <summary>Planilha de uma seção do DF (?formato=csv ou xlsx; padrão xlsx).</summary>
    [HttpGet("df/planilha/{secaoChave}")]
    public Task<IActionResult> PlanilhaDoDf(string secaoChave, [FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.DfSecaoAsync(secaoChave, formato, ctx)));

    /// <summary>Atalho da planilha dos princípios (a mesma de df/planilha/principio).</summary>
    [HttpGet("principios/planilha")]
    public Task<IActionResult> PlanilhaDosPrincipios([FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.DfSecaoAsync(
            PeDominios.Catalogo.SecaoDoCatalogo[PeDominios.Catalogo.Principio], formato, ctx)));

    // ── Catálogos (campos de ligação) ───────────────────────────────────────

    /// <summary>
    /// petic_objetivo e petic_eixo (da versão vigente; sem vigente, lista vazia), principio, ou
    /// pgia_sistema (os sistemas de IA do PGIA do órgão do PDTIC pdticId, obrigatório nele).
    /// </summary>
    [HttpGet("catalogos/{catalogo}")]
    public Task<IActionResult> Catalogo(string catalogo, [FromQuery] long? pdticId = null) =>
        Executar(async ctx => Ok(await _registros.CatalogoAsync(catalogo, ctx, pdticId)));

    // ── Apoio ───────────────────────────────────────────────────────────────

    private IActionResult Criado(object item) => StatusCode(StatusCodes.Status201Created, item);

    private IActionResult Arquivo(PePlanilhaArquivo planilha) => File(planilha.Conteudo, planilha.TipoMime, planilha.NomeArquivo);

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}

