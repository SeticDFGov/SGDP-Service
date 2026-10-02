using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using service.Interface;

namespace Controllers.Contratacoes;

/// <summary>
/// Superfície pública da Supervisão Contínua das Contratações: o painel aberto das
/// contratações de TIC com contrato assinado (página /transparencia-contratacoes do front,
/// que outros sites podem incorporar).
///
/// Único controller anônimo do módulo. Sem [Authorize] na classe e com [AllowAnonymous]
/// explícito em cada action, de propósito: a resposta traz só órgão, objeto, tipo, datas e
/// valor estimado das contratações já assinadas. Não acrescente aqui action que devolva dado
/// interno do processo (trâmite, criticidade, riscos, manifestações, observação ou pessoas).
/// </summary>
[ApiController]
[Route("api/contratacoes/publico")]
[EnableRateLimiting(PoliticaLimite)]
public class CtrPublicoController : ControllerBase
{
    /// <summary>Política do limitador por IP (Program.cs), separada da do PGIA.</summary>
    public const string PoliticaLimite = "contratacoes-publico";

    private readonly ICtrProcessoService _service;

    public CtrPublicoController(ICtrProcessoService service)
    {
        _service = service;
    }

    /// <summary>
    /// Contratações com contrato assinado dos processos de supervisão contínua, só com os campos abertos
    /// </summary>
    [HttpGet("painel")]
    [AllowAnonymous]
    public async Task<IActionResult> Painel()
    {
        return Ok(await _service.MontarPainelPublicoAsync());
    }
}
