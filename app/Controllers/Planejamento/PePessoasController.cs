using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service;
using service.Interface;

namespace Controllers.Planejamento;

/// <summary>
/// Pessoas e papéis do módulo Governança Estratégica (Decreto nº 48.900/2026). Toda
/// action exige a política do módulo e monta o contexto pelo IPePermissionService.
/// O papel de quem está logado vale para qualquer pessoa com o módulo; a gestão de
/// pessoas é do administrador do módulo (pe_admin) e do admin geral do SGDP.
/// Erros saem como { Code, Message } com 400, 403, 404 ou 409, no padrão do
/// AcessoController (faixa 1000 a 1099 do ErrorCode).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PePessoasController : ControllerBase
{
    private readonly IPePessoaService _service;
    private readonly IPePermissionService _permissoes;

    public PePessoasController(IPePessoaService service, IPePermissionService permissoes)
    {
        _service = service;
        _permissoes = permissoes;
    }

    /// <summary>
    /// Papel de quem está logado no módulo, com o órgão e a unidade. Papel nulo com
    /// EhAdminGeral falso = acesso sem papel (fale com o administrador do módulo).
    /// </summary>
    [HttpGet("meu-papel")]
    public async Task<IActionResult> MeuPapel()
    {
        var ctx = await _permissoes.GetContextAsync(User);
        if (ctx == null) return CadastroNaoEncontrado();

        return Ok(_service.MeuPapel(ctx));
    }

    /// <summary>
    /// Pessoas com papel no módulo, paginado. Filtros: parte do nome ou do e-mail e o
    /// código de um papel (fora do domínio devolve lista vazia).
    /// </summary>
    [HttpGet("pessoas")]
    public async Task<IActionResult> ListarPessoas([FromQuery] PePessoasConsulta consulta)
    {
        var ctx = await _permissoes.GetContextAsync(User);
        if (ctx == null) return CadastroNaoEncontrado();
        if (!_permissoes.PodeGerirPessoas(ctx)) return SemPermissao();

        return Ok(await _service.ListarAsync(consulta));
    }

    /// <summary>
    /// Até 20 pessoas que já entraram no SGDP e ainda não têm papel no módulo, pelo nome
    /// ou pelo e-mail (a partir de 3 letras).
    /// </summary>
    [HttpGet("pessoas/candidatas")]
    public async Task<IActionResult> ListarCandidatas([FromQuery] string? filtro)
    {
        var ctx = await _permissoes.GetContextAsync(User);
        if (ctx == null) return CadastroNaoEncontrado();
        if (!_permissoes.PodeGerirPessoas(ctx)) return SemPermissao();

        return Ok(await _service.CandidatasAsync(filtro));
    }

    /// <summary>
    /// Dá ou troca o papel ({ Papel: código }) ou tira o papel e o acesso ao módulo
    /// ({ Papel: null }). Devolve a pessoa como a lista mostra (sem papel, com Papel,
    /// ConcedidoEm e ConcedidoPor nulos).
    /// </summary>
    [HttpPut("pessoas/{userId:guid}/papel")]
    public async Task<IActionResult> DefinirPapel(Guid userId, [FromBody] PePapelDTO dto)
    {
        var ctx = await _permissoes.GetContextAsync(User);
        if (ctx == null) return CadastroNaoEncontrado();
        if (!_permissoes.PodeGerirPessoas(ctx)) return SemPermissao();

        try
        {
            return Ok(await _service.DefinirPapelAsync(ctx, userId, dto.Papel));
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
    }

    private IActionResult CadastroNaoEncontrado() => NotFound(new
    {
        Code = (int)ErrorCode.PeUsuarioNaoEncontrado,
        Message = "Seu cadastro ainda não foi encontrado. Saia e entre de novo no sistema."
    });

    private IActionResult SemPermissao() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        Code = (int)ErrorCode.PeSemPermissao,
        Message = "Só o administrador do módulo cuida das pessoas e dos papéis."
    });

    private IActionResult Erro(ApiException ex) => (ErrorCode)ex.Error.Code switch
    {
        ErrorCode.PeUsuarioNaoEncontrado or ErrorCode.AcessoUsuarioNaoEncontrado =>
            NotFound(new { ex.Error.Code, ex.Error.Message }),
        ErrorCode.PeSemPermissao => StatusCode(StatusCodes.Status403Forbidden, new { ex.Error.Code, ex.Error.Message }),
        ErrorCode.PeAutoRebaixamento => Conflict(new { ex.Error.Code, ex.Error.Message }),
        _ => BadRequest(new { ex.Error.Code, ex.Error.Message })
    };
}
