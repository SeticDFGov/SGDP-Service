using api.Common;
using api.Pgia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using service.Interface;

namespace Controllers.Pgia;

/// <summary>
/// Superfície pública do PGIA: Registro Público de Sistemas de IA (art. 24) e
/// solicitações do cidadão (arts. 11, IV e 23).
///
/// Único controller anônimo do módulo. Sem [Authorize] na classe e com
/// [AllowAnonymous] explícito em cada action, de propósito: as respostas trazem
/// apenas os campos do art. 24 e nunca dados pessoais. Não acrescente aqui nenhuma
/// action que devolva dado interno do inventário ou do solicitante.
/// </summary>
[ApiController]
[Route("api/pgia/publico")]
[EnableRateLimiting("pgia-publico")]
public class PgiaPublicoController : ControllerBase
{
    private readonly IPgiaPublicoService _service;

    public PgiaPublicoController(IPgiaPublicoService service)
    {
        _service = service;
    }

    /// <summary>
    /// Registro Público dos sistemas de IA publicados pelos órgãos (art. 24, I a V)
    /// </summary>
    [HttpGet("registro")]
    [AllowAnonymous]
    public async Task<IActionResult> ListarRegistroPublico([FromQuery] PagedRequest request)
    {
        return Ok(await _service.ListarRegistroPublicoAsync(request));
    }

    /// <summary>
    /// Abre uma solicitação sobre decisão apoiada por IA; devolve o protocolo (art. 23)
    /// </summary>
    [HttpPost("solicitacao")]
    [AllowAnonymous]
    public async Task<IActionResult> CriarSolicitacao([FromBody] PgiaSolicitacaoCreateDTO dto)
    {
        return Ok(await _service.CriarSolicitacaoAsync(dto));
    }

    /// <summary>
    /// Acompanha uma solicitação pelo protocolo (sem os dados do solicitante)
    /// </summary>
    [HttpGet("solicitacao/{protocolo}")]
    [AllowAnonymous]
    public async Task<IActionResult> ConsultarPorProtocolo(string protocolo)
    {
        var solicitacao = await _service.ConsultarPorProtocoloAsync(protocolo);
        // Protocolo desconhecido é só 404: nada a revelar sobre o que existe
        if (solicitacao == null) return NotFound();

        return Ok(solicitacao);
    }
}
