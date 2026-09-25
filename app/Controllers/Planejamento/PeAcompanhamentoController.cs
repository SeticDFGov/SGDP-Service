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
/// Acompanhamento do PDTIC (E7, rodada B): os ciclos de monitoramento (criados sozinhos pela
/// periodicidade quando a lista é lida) e de avaliação intermediária (abertos pela equipe), o
/// fechamento (com as pendências e o RA do ciclo em minuta) e a reabertura, as grades da situação
/// das ações e das medições dos indicadores e o painel do PDTIC (AC-PDTIC). O relatório de cada
/// ciclo (RA) e o de resultados (RR) ficam no PeDocumentoController. Ler: quem vê o órgão
/// (inclusive a consulta do órgão e os papéis globais). Gravar, abrir, fechar e reabrir: a equipe
/// do órgão e o admin geral, com o PDTIC vigente. Antes de o carregador trazer a versão 6 do
/// modelo (e sem as colunas novas, no intervalo do deploy), 409 com corpo. Erros como
/// { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PeAcompanhamentoController : ControllerBase
{
    private readonly IPeAcompanhamentoService _acompanhamento;
    private readonly IPePermissionService _permissoes;

    public PeAcompanhamentoController(IPeAcompanhamentoService acompanhamento, IPePermissionService permissoes)
    {
        _acompanhamento = acompanhamento;
        _permissoes = permissoes;
    }

    /// <summary>
    /// Os ciclos do PDTIC (?tipo=monitoramento ou avaliacao; sem tipo, os dois; fora do domínio,
    /// lista vazia): o monitoramento pela ordem do início e depois as avaliações.
    /// </summary>
    [HttpGet("pdtic/{id:long}/ciclos")]
    public Task<IActionResult> Ciclos(long id, [FromQuery] string? tipo) =>
        Executar(async ctx => Ok(await _acompanhamento.ListarCiclosAsync(id, tipo, ctx)));

    /// <summary>Abre uma avaliação intermediária ({ Tipo: "avaliacao", Rotulo }); 201 com o ciclo.</summary>
    [HttpPost("pdtic/{id:long}/ciclos")]
    public Task<IActionResult> CriarCiclo(long id, [FromBody] JsonElement corpo) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created,
            await _acompanhamento.CriarCicloAsync(id, PeCorpo.Ler<PeCicloCriarDTO>(corpo), ctx)));

    /// <summary>Fecha o ciclo (400 com as Pendencias) e gera o RA do ciclo em minuta; devolve o ciclo.</summary>
    [HttpPost("ciclos/{cicloId:long}/fechar")]
    public Task<IActionResult> Fechar(long cicloId) =>
        Executar(async ctx => Ok(await _acompanhamento.FecharCicloAsync(cicloId, ctx)));

    /// <summary>Reabre o ciclo fechado; devolve o ciclo.</summary>
    [HttpPost("ciclos/{cicloId:long}/reabrir")]
    public Task<IActionResult> Reabrir(long cicloId) =>
        Executar(async ctx => Ok(await _acompanhamento.ReabrirCicloAsync(cicloId, ctx)));

    /// <summary>A grade da situação das ações no ciclo de monitoramento.</summary>
    [HttpGet("ciclos/{cicloId:long}/acoes")]
    public Task<IActionResult> Acoes(long cicloId) =>
        Executar(async ctx => Ok(await _acompanhamento.AcoesAsync(cicloId, ctx)));

    /// <summary>Grava as linhas enviadas da grade das ações ({ Itens }); devolve a grade inteira (400 com Campos "AcaoId.campo").</summary>
    [HttpPut("ciclos/{cicloId:long}/acoes")]
    public Task<IActionResult> SalvarAcoes(long cicloId, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _acompanhamento.SalvarAcoesAsync(cicloId, PeCorpo.Ler<PeCicloAcoesDTO>(corpo), ctx)));

    /// <summary>A grade das medições dos indicadores no ciclo de monitoramento.</summary>
    [HttpGet("ciclos/{cicloId:long}/medicoes")]
    public Task<IActionResult> Medicoes(long cicloId) =>
        Executar(async ctx => Ok(await _acompanhamento.MedicoesAsync(cicloId, ctx)));

    /// <summary>Grava as linhas enviadas da grade das medições ({ Itens }); devolve a grade inteira (400 com Campos "IndicadorId.campo").</summary>
    [HttpPut("ciclos/{cicloId:long}/medicoes")]
    public Task<IActionResult> SalvarMedicoes(long cicloId, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _acompanhamento.SalvarMedicoesAsync(cicloId, PeCorpo.Ler<PeCicloMedicoesDTO>(corpo), ctx)));

    /// <summary>O painel do PDTIC (AC-PDTIC) no ciclo dado ou no último ciclo de monitoramento com dado.</summary>
    [HttpGet("pdtic/{id:long}/painel-acompanhamento")]
    public Task<IActionResult> Painel(long id, [FromQuery] long? cicloId) =>
        Executar(async ctx => Ok(await _acompanhamento.PainelAsync(id, cicloId, ctx)));

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
