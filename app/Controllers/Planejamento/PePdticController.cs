using System.Text.Json;
using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// PDTIC dos órgãos (E4 e E7): abrir e ler o PDTIC, as versões do órgão, a lista dos atuais
/// (papéis globais), a situação de cada passo da trilha, o "não se aplica", os temas das ações
/// (incisos V, VI e IX), os sistemas de IA do PGIA (inciso VIII), os comentários dos passos,
/// as planilhas do órgão e consolidadas e o caminho da aprovação (a prévia do envio, enviar ao
/// CGTIC, publicar, encerrar, revisar e registrar o PDTIC aprovado fora do sistema). A
/// decisão do CGTIC fica no PeDeliberacoesController; os registros de cada seção, no
/// PeRegistrosController (pdtic/{id}/secoes/{chave}/registros). A autorização fica nos
/// serviços (ler: quem vê o órgão; editar, enviar, publicar e revisar: a equipe do órgão e o
/// admin geral; encerrar: a equipe, com a aprovação da autoridade máxima, ou o administrador do
/// módulo e o admin geral, depois da vigência; comentar: pe_admin, pe_sgdi e admin geral;
/// responder: a equipe do órgão; resolver: a equipe do órgão ou quem comentou; consolidado e
/// lista dos PDTICs: papéis globais e admin geral). Erros como { Code, Message }
/// (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PePdticController : ControllerBase
{
    private readonly IPePdticService _pdtics;
    private readonly IPeComentarioService _comentarios;
    private readonly IPePlanilhaService _planilhas;
    private readonly IPePermissionService _permissoes;
    private readonly IPePdticAprovacaoService _aprovacao;

    public PePdticController(IPePdticService pdtics, IPeComentarioService comentarios, IPePlanilhaService planilhas,
        IPePermissionService permissoes, IPePdticAprovacaoService aprovacao)
    {
        _pdtics = pdtics;
        _comentarios = comentarios;
        _planilhas = planilhas;
        _permissoes = permissoes;
        _aprovacao = aprovacao;
    }

    // ── PDTIC ───────────────────────────────────────────────────────────────

    /// <summary>
    /// O PDTIC atual do órgão (sem orgaoId, o da própria pessoa): a versão em elaboração (a
    /// revisão, quando há), senão a vigente; 204 sem corpo quando não há.
    /// </summary>
    [HttpGet("pdtic/atual")]
    public Task<IActionResult> Atual([FromQuery] long? orgaoId) =>
        Executar(async ctx =>
        {
            var atual = await _pdtics.AtualAsync(orgaoId, ctx);
            return atual == null ? NoContent() : Ok(atual);
        });

    /// <summary>Todas as versões do PDTIC do órgão (sem orgaoId, o da própria pessoa), da mais nova para a mais antiga.</summary>
    [HttpGet("pdtic/versoes")]
    public Task<IActionResult> Versoes([FromQuery] long? orgaoId) =>
        Executar(async ctx => Ok(await _pdtics.VersoesAsync(orgaoId, ctx)));

    /// <summary>
    /// Abre o PDTIC ({ OrgaoId }: nulo para a equipe do órgão; o órgão escolhido pelo admin
    /// geral). Versão 1.0, em elaboração; 409 se o órgão já tem um atual. Devolve 201.
    /// </summary>
    [HttpPost("pdtic")]
    public Task<IActionResult> Abrir([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement corpo) =>
        Executar(async ctx =>
        {
            var dto = corpo.ValueKind == JsonValueKind.Undefined ? new PePdticCriarDTO() : PeCorpo.Ler<PePdticCriarDTO>(corpo);
            return StatusCode(StatusCodes.Status201Created, await _pdtics.AbrirAsync(dto, ctx));
        });

    [HttpGet("pdtic/{id:long}")]
    public Task<IActionResult> Obter(long id) =>
        Executar(async ctx => Ok(await _pdtics.ObterAsync(id, ctx)));

    /// <summary>
    /// Os PDTICs atuais de todos os órgãos (papéis globais e admin geral): ?Situacao=&amp;Filtro=&amp;Page=&amp;PageSize=.
    /// Cada item com NivelAlcancadoId e NivelAlcancadoNome (F3).
    /// </summary>
    [HttpGet("pdtic")]
    public Task<IActionResult> Listar([FromQuery] PePdticConsulta consulta) =>
        Executar(async ctx => Ok(await _pdtics.ListarAsync(consulta, ctx)));

    // ── Caminho da aprovação (E7) ───────────────────────────────────────────

    /// <summary>A prévia do envio ao CGTIC: { PodeEnviar, Pendencias: [{ PassoId, PassoNumero, PassoTitulo, Motivo }], Motivo }.</summary>
    [HttpGet("pdtic/{id:long}/envio")]
    public Task<IActionResult> Envio(long id) =>
        Executar(async ctx => Ok(await _aprovacao.EnvioAsync(id, ctx)));

    /// <summary>
    /// Envia ao CGTIC (vale como a comunicação à SGDI, art. 7º, V, do Decreto nº 48.899/2026):
    /// gera o PDF enviado e cria a deliberação. Devolve o PDTIC; com pendências, 400 com a lista.
    /// </summary>
    [HttpPost("pdtic/{id:long}/enviar")]
    public Task<IActionResult> Enviar(long id) =>
        Executar(async ctx => Ok(await _aprovacao.EnviarAsync(id, ctx)));

    /// <summary>Registra a publicação (PDTIC aprovado, com a data e o endereço da seção publicacao); devolve o PDTIC.</summary>
    [HttpPost("pdtic/{id:long}/publicar")]
    public Task<IActionResult> Publicar(long id) =>
        Executar(async ctx => Ok(await _aprovacao.PublicarAsync(id, ctx)));

    /// <summary>Encerra o PDTIC ({ Motivo }: obrigatório quando o administrador encerra pela vigência vencida); devolve o PDTIC.</summary>
    [HttpPost("pdtic/{id:long}/encerrar")]
    public Task<IActionResult> Encerrar(long id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement corpo) =>
        Executar(async ctx =>
        {
            var dto = corpo.ValueKind == JsonValueKind.Undefined ? new PeEncerrarDTO() : PeCorpo.Ler<PeEncerrarDTO>(corpo);
            return Ok(await _aprovacao.EncerrarAsync(id, dto, ctx));
        });

    /// <summary>
    /// Abre a revisão do PDTIC vigente ({ Justificativa }: obrigatória sem o passo 6.3 na trilha
    /// do órgão). Devolve 201 com o PDTIC novo (a versão seguinte, em elaboração).
    /// </summary>
    [HttpPost("pdtic/{id:long}/revisao")]
    public Task<IActionResult> Revisao(long id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement corpo) =>
        Executar(async ctx =>
        {
            var dto = corpo.ValueKind == JsonValueKind.Undefined ? new PeRevisaoDTO() : PeCorpo.Ler<PeRevisaoDTO>(corpo);
            return StatusCode(StatusCodes.Status201Created, await _aprovacao.RevisarAsync(id, dto, ctx));
        });

    /// <summary>
    /// Registra o PDTIC aprovado fora do sistema (a equipe do órgão, o próprio; o admin geral,
    /// qualquer órgão), já publicado, com o PDF enviado antes por POST arquivos. Devolve 201.
    /// </summary>
    [HttpPost("pdtic/registrar-externo")]
    public Task<IActionResult> RegistrarExterno([FromBody] JsonElement corpo) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created,
            await _aprovacao.RegistrarExternoAsync(PeCorpo.Ler<PeRegistroExternoDTO>(corpo), ctx)));

    // ── Trilha do PDTIC ─────────────────────────────────────────────────────

    /// <summary>
    /// A situação de cada passo (feito, pendente, atencao, nao_se_aplica, continuo, aguardando,
    /// atrasado, externo e, desde a F3, opcional), com o Motivo, o PodeEditar de cada passo, os
    /// avisos e o próximo passo; desde a F3, também o Obrigatorio, a validação da equipe
    /// (AceitaValidacao e Validacao) de cada passo e o nível que o PDTIC alcançou (Nivel).
    /// </summary>
    [HttpGet("pdtic/{id:long}/situacao")]
    public Task<IActionResult> Situacao(long id) =>
        Executar(async ctx => Ok(await _pdtics.SituacaoAsync(id, ctx)));

    /// <summary>Marca o passo como "não se aplica" ({ Justificativa }); devolve a situação do passo.</summary>
    [HttpPut("pdtic/{id:long}/passos/{passoId:long}/nao-se-aplica")]
    public Task<IActionResult> MarcarNaoSeAplica(long id, long passoId, [FromBody] JsonElement corpo) =>
        Executar(async ctx => Ok(await _pdtics.MarcarNaoSeAplicaAsync(id, passoId, PeCorpo.Ler<PeNaoSeAplicaDTO>(corpo), ctx)));

    /// <summary>Desfaz o "não se aplica"; devolve a situação do passo.</summary>
    [HttpDelete("pdtic/{id:long}/passos/{passoId:long}/nao-se-aplica")]
    public Task<IActionResult> DesmarcarNaoSeAplica(long id, long passoId) =>
        Executar(async ctx => Ok(await _pdtics.DesmarcarNaoSeAplicaAsync(id, passoId, ctx)));

    /// <summary>
    /// F3: marca o passo como validado pela equipe (a equipe do PDTIC do órgão e o admin geral, com
    /// o passo feito e editável agora); devolve a situação do passo. 409 PeValidacaoRecusada (1145)
    /// com o passo não feito ou de um tipo que não recebe a validação.
    /// </summary>
    [HttpPut("pdtic/{id:long}/passos/{passoId:long}/validacao")]
    public Task<IActionResult> Validar(long id, long passoId) =>
        Executar(async ctx => Ok(await _pdtics.ValidarPassoAsync(id, passoId, ctx)));

    /// <summary>F3: desfaz a validação da equipe (sem marca, não faz nada); devolve a situação do passo.</summary>
    [HttpDelete("pdtic/{id:long}/passos/{passoId:long}/validacao")]
    public Task<IActionResult> DesfazerValidacao(long id, long passoId) =>
        Executar(async ctx => Ok(await _pdtics.DesfazerValidacaoAsync(id, passoId, ctx)));

    /// <summary>Os três temas do decreto (incisos V, VI e IX) com as ações e a justificativa de tema sem ação.</summary>
    [HttpGet("pdtic/{id:long}/temas")]
    public Task<IActionResult> Temas(long id) =>
        Executar(async ctx => Ok(await _pdtics.TemasAsync(id, ctx)));

    /// <summary>Os sistemas de IA do inventário do PGIA do órgão (só leitura).</summary>
    [HttpGet("pdtic/{id:long}/ia/pgia")]
    public Task<IActionResult> SistemasIa(long id) =>
        Executar(async ctx => Ok(await _pdtics.SistemasIaAsync(id, ctx)));

    // ── Comentários ─────────────────────────────────────────────────────────

    /// <summary>Os comentários principais (de um passo com ?passoId=, ou de todos), cada um com as respostas.</summary>
    [HttpGet("pdtic/{id:long}/comentarios")]
    public Task<IActionResult> Comentarios(long id, [FromQuery] long? passoId) =>
        Executar(async ctx => Ok(await _comentarios.ListarAsync(id, passoId, ctx)));

    /// <summary>
    /// Comenta ({ PassoId, PaiId: null, Texto }: pe_admin, pe_sgdi e admin geral) ou responde
    /// (PaiId: a equipe do órgão); devolve 201 com a conversa.
    /// </summary>
    [HttpPost("pdtic/{id:long}/comentarios")]
    public Task<IActionResult> Comentar(long id, [FromBody] JsonElement corpo) =>
        Executar(async ctx => StatusCode(StatusCodes.Status201Created,
            await _comentarios.CriarAsync(id, PeCorpo.Ler<PeComentarioCriarDTO>(corpo), ctx)));

    /// <summary>Marca o comentário principal como resolvido (a equipe do órgão ou quem comentou).</summary>
    [HttpPost("comentarios/{id:long}/resolver")]
    public Task<IActionResult> Resolver(long id) =>
        Executar(async ctx => Ok(await _comentarios.ResolverAsync(id, ctx)));

    // ── Planilhas ───────────────────────────────────────────────────────────

    /// <summary>Planilha de uma seção do PDTIC (?formato=csv ou xlsx; padrão xlsx), com as colunas do nível do órgão.</summary>
    [HttpGet("pdtic/{id:long}/planilha/{secaoChave}")]
    public Task<IActionResult> PlanilhaDaSecao(long id, string secaoChave, [FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.PdticSecaoAsync(id, secaoChave, formato, ctx)));

    /// <summary>
    /// Planilha de um passo do PDTIC (E8; ?formato=csv ou xlsx, padrão xlsx; ?cicloId= obrigatório
    /// no passo com seção por ciclo): em xlsx, uma aba por seção do passo e a Leia-me; em csv, o
    /// arquivo da seção quando o passo tem uma só, ou um zip com um csv por seção.
    /// </summary>
    [HttpGet("pdtic/{id:long}/planilha/passo/{passoChave}")]
    public Task<IActionResult> PlanilhaDoPasso(long id, string passoChave, [FromQuery] string? formato, [FromQuery] long? cicloId) =>
        Executar(async ctx => Arquivo(await _planilhas.PdticPassoAsync(id, passoChave, formato, cicloId, ctx)));

    /// <summary>Planilha completa do PDTIC (uma aba por seção e a Leia-me; só xlsx).</summary>
    [HttpGet("pdtic/{id:long}/planilha")]
    public Task<IActionResult> PlanilhaCompleta(long id, [FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.PdticCompletaAsync(id, formato, ctx)));

    /// <summary>Consolidado de uma seção, uma linha por registro de todos os PDTICs atuais (papéis globais e admin geral).</summary>
    [HttpGet("consolidado/{secaoChave}")]
    public Task<IActionResult> ConsolidadoDaSecao(string secaoChave, [FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.ConsolidadoSecaoAsync(secaoChave, formato, ctx)));

    /// <summary>Consolidado com todas as seções (uma aba por seção; só xlsx).</summary>
    [HttpGet("consolidado")]
    public Task<IActionResult> ConsolidadoCompleto([FromQuery] string? formato) =>
        Executar(async ctx => Arquivo(await _planilhas.ConsolidadoCompletoAsync(formato, ctx)));

    // ── Apoio ───────────────────────────────────────────────────────────────

    private IActionResult Arquivo(PePlanilhaArquivo planilha) => File(planilha.Conteudo, planilha.TipoMime, planilha.NomeArquivo);

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
