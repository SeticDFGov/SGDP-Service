using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service;
using service.Contratacoes;
using service.Interface;

namespace Controllers.Contratacoes;

/// <summary>
/// Importação da planilha legada de análises de contratações (multipart, campo
/// "arquivo"): prévia sem gravar e importação idempotente (upsert por nº do processo).
/// </summary>
[ApiController]
[Authorize]
[Route("api/contratacoes/importacao")]
public class CtrImportacaoController : ControllerBase
{
    /// <summary>Teto do corpo da requisição: 5 MB (a planilha tem dezenas de KB).</summary>
    public const int LimiteRequisicaoBytes = 5 * 1024 * 1024;

    private readonly ICtrImportacaoService _service;
    private readonly ICtrPermissionService _permissionService;

    public CtrImportacaoController(ICtrImportacaoService service, ICtrPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;
    private string GetUserPerfil() => User.FindFirst(ClaimTypes.Role)?.Value ?? "basico";

    private async Task<CtrUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email, GetUserPerfil());
    }

    /// <summary>
    /// Prévia: interpreta o arquivo e devolve a ação de cada linha, sem gravar nada
    /// </summary>
    [HttpPost("previa")]
    [RequestSizeLimit(LimiteRequisicaoBytes)]
    public async Task<IActionResult> Previa(IFormFile? arquivo)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Importacao)) return Forbid();

        var conteudo = await LerArquivoAsync(arquivo);
        return Ok(await _service.PreviaAsync(conteudo));
    }

    /// <summary>
    /// Importa a planilha: cria/atualiza os processos válidos e relata as rejeições
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(LimiteRequisicaoBytes)]
    public async Task<IActionResult> Importar(IFormFile? arquivo)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, CtrResources.Importacao)) return Forbid();

        var conteudo = await LerArquivoAsync(arquivo);
        return Ok(await _service.ImportarAsync(conteudo, ctx));
    }

    private static async Task<byte[]> LerArquivoAsync(IFormFile? arquivo)
    {
        if (arquivo == null || arquivo.Length == 0)
            throw new ApiException(ErrorCode.CtrImportacaoInvalida, "Envie a planilha no campo \"arquivo\".");

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (extensao != ".csv")
            throw new ApiException(ErrorCode.CtrImportacaoInvalida,
                "A importação aceita apenas arquivos .csv (exporte a planilha como CSV separado por ponto e vírgula).");

        using var memoria = new MemoryStream();
        await using (var origem = arquivo.OpenReadStream())
        {
            await origem.CopyToAsync(memoria);
        }

        return memoria.ToArray();
    }
}
