using System.Security.Claims;
using api.Pgia;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Pgia;
using service;
using service.Interface;
using service.Pgia;

namespace Controllers.Pgia;

/// <summary>
/// Documentos do PGIA e seus anexos (arts. 9º, III, 10, § 1º, 30 e 32).
/// Cria o documento em qualquer escopo (sistema, órgão ou central) e grava o
/// arquivo em disco. Os documentos de sistema continuam podendo nascer pelo
/// <c>POST api/pgia/sistema/{id}/documentos</c>, que não mudou.
/// </summary>
[ApiController]
[Authorize(Roles = "admin,pgia")]
[Route("api/pgia/documento")]
public class PgiaDocumentoController : ControllerBase
{
    private readonly IPgiaDocumentoService _service;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaDocumentoController(IPgiaDocumentoService service, IPgiaPermissionService permissionService)
    {
        _service = service;
        _permissionService = permissionService;
    }

    private string? GetUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value;
    private string GetUserPerfil() => User.FindFirst(ClaimTypes.Role)?.Value ?? Perfis.Basico;

    private async Task<PgiaUserContext?> GetContextAsync()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return null;
        return await _permissionService.GetContextAsync(email, GetUserPerfil());
    }

    /// <summary>Documento sem órgão (ata do CGTIC, relatório anual) é do escopo central.</summary>
    private static bool EhEscopoCentral(PgiaUserContext ctx) =>
        ctx.PapelEfetivo is Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic;

    /// <summary>
    /// Escopo de um documento já gravado: com órgão, vale o acesso àquele órgão;
    /// sem órgão, só as instâncias centrais.
    /// </summary>
    private bool PodeAlcancarDocumento(PgiaUserContext ctx, PgiaDocumento documento) =>
        documento.OrgaoId != null
            ? _permissionService.CanAccessOrgao(ctx, documento.OrgaoId.Value)
            : EhEscopoCentral(ctx);

    /// <summary>
    /// Cria o documento (só metadados) no escopo informado: sistema, órgão ou
    /// central. O arquivo sobe depois, no POST {id}/arquivo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] PgiaDocumentoAvulsoCreateDTO dto)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanCreate(ctx, PgiaResources.Documento)) return Forbid();

        // Escopo de sistema herda o órgão do sistema; escopo de órgão exige acesso
        // àquele órgão; sem nenhum dos dois, é documento central.
        if (dto.SistemaIaId != null)
        {
            var sistema = await _service.GetSistemaParaEscopoAsync(dto.SistemaIaId.Value);
            if (sistema == null) return NotFound();
            if (!_permissionService.CanAccessOrgao(ctx, sistema.OrgaoId)) return Forbid();
        }
        else if (dto.OrgaoId != null)
        {
            if (!_permissionService.CanAccessOrgao(ctx, dto.OrgaoId.Value)) return Forbid();
        }
        else if (!EhEscopoCentral(ctx))
        {
            return Forbid();
        }

        return Ok(await _service.CriarAsync(dto, ctx));
    }

    /// <summary>
    /// Anexa (ou substitui) o arquivo do documento. multipart/form-data, campo
    /// "arquivo". Máximo de 25 MB; extensões pdf, doc(x), odt, xls(x), ods, csv,
    /// txt, png, jpg/jpeg. Mesmo par de gates da edição do documento.
    /// </summary>
    [HttpPost("{id:long}/arquivo")]
    [RequestSizeLimit(PgiaDocumentoService.LimiteRequisicaoBytes)]
    public async Task<IActionResult> EnviarArquivo(long id, IFormFile? arquivo)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanEdit(ctx, PgiaResources.Documento)) return Forbid();

        var documento = await _service.GetEntidadeAsync(id);
        if (documento == null) return NotFound();
        if (!PodeAlcancarDocumento(ctx, documento)) return Forbid();

        // Substituir arquivo que outra pessoa anexou exige escopo central ou ser
        // quem enviou o atual (integridade probatória) — 403 para o front
        if (!PgiaDocumentoService.PodeSubstituirArquivo(documento, ctx)) return Forbid();

        if (arquivo == null || arquivo.Length == 0)
            throw new ApiException(ErrorCode.PgiaArquivoInvalido, "Envie um arquivo no campo \"arquivo\".");

        await using var conteudo = arquivo.OpenReadStream();
        var upload = new PgiaArquivoUpload
        {
            NomeOriginal = arquivo.FileName,
            ContentType = arquivo.ContentType,
            TamanhoBytes = arquivo.Length,
            Conteudo = conteudo
        };

        return Ok(await _service.SalvarArquivoAsync(id, upload, ctx, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Baixa o arquivo do documento, com o nome original no Content-Disposition.
    /// Gate de leitura equivalente ao de ver o documento.
    /// </summary>
    [HttpGet("{id:long}/arquivo")]
    public async Task<IActionResult> BaixarArquivo(long id)
    {
        var ctx = await GetContextAsync();
        if (ctx == null) return Unauthorized();
        if (!_permissionService.CanView(ctx, PgiaResources.Documento)) return Forbid();

        var documento = await _service.GetEntidadeAsync(id);
        if (documento == null) return NotFound();
        if (!PodeAlcancarDocumento(ctx, documento)) return Forbid();

        var arquivo = await _service.AbrirArquivoAsync(id);
        if (arquivo == null) return NotFound(); // documento só com metadados

        // O tipo servido é o derivado da extensão (allowlist), nunca o que o
        // cliente mandou no upload; o nosniff impede o navegador de "corrigir"
        // isso sozinho e tratar um anexo como HTML/script.
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // FileContentResult: ContentType do documento e nome original no
        // Content-Disposition (o binário veio do bytea, não de um caminho)
        return File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }
}
