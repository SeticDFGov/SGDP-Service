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
/// Anexos do módulo (E3): envio (multipart, campo "arquivo") e download, com o binário no
/// banco. Envia quem edita alguma coisa no módulo; baixa quem pode ver o dono do registro
/// que aponta para o arquivo (sem dono: só quem enviou). O download manda
/// X-Content-Type-Options: nosniff. Erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento/arquivos")]
public class PeArquivosController : ControllerBase
{
    private readonly IPeArquivoService _service;
    private readonly IPePermissionService _permissoes;

    public PeArquivosController(IPeArquivoService service, IPePermissionService permissoes)
    {
        _service = service;
        _permissoes = permissoes;
    }

    /// <summary>
    /// Envia um arquivo (PDF, PNG ou JPEG; até 25 MB, imagens até 5 MB). Devolve 201 com
    /// { Id, Nome, TipoMime, Tamanho }; o id vai no campo de arquivo do registro.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(PeArquivoService.LimiteRequisicaoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PeArquivoService.LimiteRequisicaoBytes)]
    public Task<IActionResult> Enviar(IFormFile? arquivo) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            if (arquivo == null || arquivo.Length == 0)
                throw new service.ApiException(ErrorCode.PeArquivoInvalido, "Envie um arquivo no campo \"arquivo\".");

            await using var conteudo = arquivo.OpenReadStream();
            var upload = new PeArquivoUpload { NomeOriginal = arquivo.FileName, Tamanho = arquivo.Length, Conteudo = conteudo };
            return StatusCode(StatusCodes.Status201Created, await _service.EnviarAsync(upload, ctx, HttpContext.RequestAborted));
        });

    /// <summary>Baixa o arquivo com o nome original e o tipo da extensão (nosniff).</summary>
    [HttpGet("{id:long}")]
    public Task<IActionResult> Baixar(long id) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();

            var arquivo = await _service.BaixarAsync(id, ctx);
            // O tipo é o da extensão (lista fechada); o nosniff impede o navegador de "adivinhar" outro
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(arquivo.Conteudo, arquivo.TipoMime, arquivo.Nome);
        });
}
