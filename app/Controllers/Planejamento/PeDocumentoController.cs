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
/// Documento do PDTIC (E5): a estrutura resolvida para a prévia, a edição dos textos e dos
/// capítulos pela equipe do órgão, o PDF com as versões e o modelo do documento (capítulos e
/// blocos) do administrador do módulo. Ler o documento: quem vê o órgão. Editar e gerar o PDF:
/// a equipe do órgão (e o admin geral) com o PDTIC em elaboração ou devolvido. Ler o modelo:
/// qualquer papel do módulo; alterar: pe_admin e admin geral. Sem as tabelas pe_doc (o PR
/// publica o código antes da migration), 409 com corpo. Erros como { Code, Message }
/// (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PeDocumentoController : ControllerBase
{
    private const string SoAdministrador = "Só o administrador do módulo altera o modelo do documento.";

    private readonly IPeDocumentoService _documentos;
    private readonly IPeDocModeloService _modelo;
    private readonly IPePermissionService _permissoes;

    public PeDocumentoController(IPeDocumentoService documentos, IPeDocModeloService modelo, IPePermissionService permissoes)
    {
        _documentos = documentos;
        _modelo = modelo;
        _permissoes = permissoes;
    }

    // ── Documento do órgão ──────────────────────────────────────────────────

    /// <summary>A estrutura resolvida: capítulos numerados, blocos com o texto efetivo, marcadores, dados e versões.</summary>
    [HttpGet("pdtic/{id:long}/documento")]
    public Task<IActionResult> Obter(long id) =>
        Executar(async ctx => Ok(await _documentos.ObterAsync(id, ctx)));

    /// <summary>Grava o texto do órgão num bloco de texto ({ Texto }); devolve o bloco.</summary>
    [HttpPut("pdtic/{id:long}/documento/blocos/{blocoId:long}")]
    public Task<IActionResult> SalvarTexto(long id, long blocoId, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _documentos.SalvarTextoAsync(id, blocoId, PeDocTextoDTO.Ler(corpo), ctx)));

    /// <summary>Volta o bloco ao texto do modelo; devolve o bloco.</summary>
    [HttpDelete("pdtic/{id:long}/documento/blocos/{blocoId:long}")]
    public Task<IActionResult> RestaurarTexto(long id, long blocoId) =>
        Executar(async ctx => Ok(await _documentos.RestaurarTextoAsync(id, blocoId, ctx)));

    /// <summary>Esconde ou mostra o capítulo e grava o título próprio ({ Oculto, TituloProprio }); devolve o capítulo.</summary>
    [HttpPut("pdtic/{id:long}/documento/capitulos/{capituloId:long}")]
    public Task<IActionResult> AtualizarCapitulo(long id, long capituloId, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _documentos.AtualizarCapituloAsync(id, capituloId, PeCorpoParcial.Ler<PeDocCapituloOrgaoDTO>(corpo), ctx)));

    /// <summary>Gera o PDF e guarda a versão minuta; devolve 201 com a versão.</summary>
    [HttpPost("pdtic/{id:long}/documento/pdf")]
    public Task<IActionResult> GerarPdf(long id) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created, await _documentos.GerarPdfAsync(id, ctx)));

    /// <summary>As versões geradas, da mais nova para a mais antiga.</summary>
    [HttpGet("pdtic/{id:long}/documento/versoes")]
    public Task<IActionResult> Versoes(long id) =>
        Executar(async ctx => Ok(await _documentos.VersoesAsync(id, ctx)));

    /// <summary>O PDF de uma versão (PDTIC_SIGLA_vVERSAO_NUMERO.pdf, nosniff).</summary>
    [HttpGet("pdtic/{id:long}/documento/versoes/{numero:int}/arquivo")]
    public Task<IActionResult> Arquivo(long id, int numero) =>
        Executar(async ctx =>
        {
            var arquivo = await _documentos.ArquivoDaVersaoAsync(id, numero, ctx);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(arquivo.Conteudo, PeDocumentoService.MimePdf, arquivo.NomeArquivo);
        });

    // ── Modelo do documento (administrador) ─────────────────────────────────

    /// <summary>O modelo ativo do tipo (?tipo=pdtic), com os capítulos, os blocos e os marcadores.</summary>
    [HttpGet("modelo/documento")]
    public Task<IActionResult> ObterModelo([FromQuery] string? tipo) =>
        Executar(async ctx => _permissoes.PodeLerModelo(ctx)
            ? Ok(await _modelo.ObterAsync(tipo))
            : PeRespostas.SemPermissao("Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo."));

    /// <summary>Cria um capítulo ({ Tipo, PaiId, Chave, Titulo, Numerado, Obrigatorio, PassoChave }); 201.</summary>
    [HttpPost("modelo/documento/capitulos")]
    public Task<IActionResult> CriarCapitulo([FromBody] JsonElement corpo) =>
        Escrever(async ctx => StatusCode(StatusCodes.Status201Created,
            await _modelo.CriarCapituloAsync(PeCorpo.Ler<PeDocCapituloCriarDTO>(corpo), ctx)));

    /// <summary>Nova ordem dos capítulos de um mesmo nível ({ Ids }); devolve o modelo.</summary>
    [HttpPut("modelo/documento/capitulos/ordem")]
    public Task<IActionResult> OrdenarCapitulos([FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _modelo.OrdenarCapitulosAsync(PeCorpo.Ler<PeOrdemDTO>(corpo), ctx)));

    /// <summary>Título, numeração, obrigatoriedade e passo do capítulo (campo ausente não muda).</summary>
    [HttpPut("modelo/documento/capitulos/{id:long}")]
    public Task<IActionResult> AtualizarCapituloDoModelo(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _modelo.AtualizarCapituloAsync(id, PeCorpoParcial.Ler<PeDocCapituloAtualizarDTO>(corpo), ctx)));

    /// <summary>Apaga (exclusão lógica) o capítulo criado pelo administrador; 204.</summary>
    [HttpDelete("modelo/documento/capitulos/{id:long}")]
    public Task<IActionResult> ExcluirCapitulo(long id) =>
        Escrever(async ctx =>
        {
            await _modelo.ExcluirCapituloAsync(id, ctx);
            return NoContent();
        });

    /// <summary>Cria um bloco ({ CapituloId, Tipo, Config }); 201.</summary>
    [HttpPost("modelo/documento/blocos")]
    public Task<IActionResult> CriarBloco([FromBody] JsonElement corpo) =>
        Escrever(async ctx => StatusCode(StatusCodes.Status201Created,
            await _modelo.CriarBlocoAsync(PeCorpo.Ler<PeDocBlocoCriarDTO>(corpo), ctx)));

    /// <summary>Nova ordem dos blocos de um capítulo ({ Ids }); devolve o modelo.</summary>
    [HttpPut("modelo/documento/blocos/ordem")]
    public Task<IActionResult> OrdenarBlocos([FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _modelo.OrdenarBlocosAsync(PeCorpo.Ler<PeOrdemDTO>(corpo), ctx)));

    /// <summary>O config do bloco ({ Config }); o tipo não muda.</summary>
    [HttpPut("modelo/documento/blocos/{id:long}")]
    public Task<IActionResult> AtualizarBloco(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _modelo.AtualizarBlocoAsync(id, PeCorpo.Ler<PeDocBlocoAtualizarDTO>(corpo), ctx)));

    /// <summary>Apaga (exclusão lógica) o bloco; 204.</summary>
    [HttpDelete("modelo/documento/blocos/{id:long}")]
    public Task<IActionResult> ExcluirBloco(long id) =>
        Escrever(async ctx =>
        {
            await _modelo.ExcluirBlocoAsync(id, ctx);
            return NoContent();
        });

    // ── Apoio ───────────────────────────────────────────────────────────────

    private Task<IActionResult> Escrever(Func<PeUserContext, Task<IActionResult>> acao) =>
        Executar(ctx => _permissoes.PodeConfigurarModelo(ctx) ? acao(ctx) : Task.FromResult(PeRespostas.SemPermissao(SoAdministrador)));

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
