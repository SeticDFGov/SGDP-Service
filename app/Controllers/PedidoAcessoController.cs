using api.Acesso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service;
using service.Interface;

namespace Controllers;

/// <summary>
/// Pedidos de acesso aos módulos. Pedir e ver os próprios pedidos vale para
/// qualquer usuário autenticado, inclusive quem ainda não tem módulo algum (é para
/// ele que o botão existe), por isso a classe não exige a política de um módulo.
/// Decidir é da administração (todos os módulos que o sistema libera), da SGDI
/// (só o PGIA) e do administrador da Governança Estratégica (só esse módulo); quem
/// não decide nada recebe 403 na fila e zero na contagem.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pedidos-acesso")]
public class PedidoAcessoController : ControllerBase
{
    private readonly IPedidoAcessoService _service;

    public PedidoAcessoController(IPedidoAcessoService service)
    {
        _service = service;
    }

    /// <summary>
    /// Registra o pedido de quem está logado: { Modulo, Justificativa }. Havendo
    /// pedido pendente do mesmo módulo, devolve esse.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] PedidoAcessoCreateDTO dto)
    {
        try
        {
            return Ok(await _service.CriarAsync(User, dto));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    /// <summary>Último pedido de cada módulo de quem está logado (para os cards da tela inicial).</summary>
    [HttpGet("meus")]
    public async Task<IActionResult> Meus()
    {
        return Ok(await _service.MeusPedidosAsync(User));
    }

    /// <summary>
    /// Fila paginada dos pedidos que quem consulta decide. Filtros: Situacao,
    /// Modulo e Filtro (parte do nome ou do e-mail).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] PedidosAcessoConsulta consulta)
    {
        var decisor = await _service.DecisorAsync(User);
        if (!decisor.PodeDecidir) return Forbid();

        return Ok(await _service.ListarAsync(consulta, decisor));
    }

    /// <summary>
    /// Quantos pedidos esperam a decisão de quem consulta (contador da aba e dos
    /// cards). Quem não decide nada recebe zero, sem erro.
    /// </summary>
    [HttpGet("pendentes")]
    public async Task<IActionResult> Pendentes([FromQuery] string? modulo)
    {
        var decisor = await _service.DecisorAsync(User);
        var total = decisor.PodeDecidir ? await _service.ContarPendentesAsync(decisor, modulo) : 0;
        return Ok(new PedidosAcessoPendentesResponse { Total = total });
    }

    /// <summary>
    /// Aprova e libera o módulo na hora. No PGIA, { PapelPgia } define o papel (nulo =
    /// agente). Na Governança Estratégica, { PapelPlanejamento } é obrigatório.
    /// </summary>
    [HttpPost("{id:long}/aprovar")]
    public async Task<IActionResult> Aprovar(long id, [FromBody] PedidoAcessoAprovarDTO dto)
    {
        var decisor = await _service.DecisorAsync(User);
        if (!decisor.PodeDecidir) return Forbid();
        if (string.IsNullOrEmpty(decisor.Email)) return Unauthorized();

        try
        {
            return Ok(await _service.AprovarAsync(id, dto, decisor));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    /// <summary>Recusa com { Motivo } obrigatório, que a pessoa lê no card do módulo.</summary>
    [HttpPost("{id:long}/recusar")]
    public async Task<IActionResult> Recusar(long id, [FromBody] PedidoAcessoRecusarDTO dto)
    {
        var decisor = await _service.DecisorAsync(User);
        if (!decisor.PodeDecidir) return Forbid();
        if (string.IsNullOrEmpty(decisor.Email)) return Unauthorized();

        try
        {
            return Ok(await _service.RecusarAsync(id, dto, decisor));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    // Erro de regra volta com código e mensagem, como no AcessoController
    private IActionResult Erro(ApiException ex) => (ErrorCode)ex.Error.Code switch
    {
        ErrorCode.PedidoAcessoNaoEncontrado or ErrorCode.AcessoUsuarioNaoEncontrado =>
            NotFound(new { ex.Error.Code, ex.Error.Message }),
        ErrorCode.PedidoAcessoJaDecidido => Conflict(new { ex.Error.Code, ex.Error.Message }),
        ErrorCode.PedidoAcessoForaDoEscopo => Forbid(),
        _ => BadRequest(new { ex.Error.Code, ex.Error.Message })
    };
}
