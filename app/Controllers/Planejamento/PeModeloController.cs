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
/// Modelo configurável e níveis de maturidade do módulo Governança Estratégica (E2):
/// o catálogo, a trilha resolvida de um órgão, a escrita do administrador e o histórico.
/// Ler o modelo e a trilha: qualquer papel do módulo (a trilha de um órgão, só quem vê o
/// órgão: papéis globais e admin geral, ou papel de órgão no próprio). Alterar: pe_admin
/// e admin geral. Histórico: papéis globais e admin geral. Os PUT de um item recebem só
/// os campos editáveis (campo ausente não muda nada) e devolvem o item na forma do
/// GET modelo; os POST devolvem 201 com o item criado. Erros como { Code, Message }
/// (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento/modelo")]
public class PeModeloController : ControllerBase
{
    private const string SoAdministrador = "Só o administrador do módulo altera o modelo e os níveis.";

    private readonly IPeModeloService _service;
    private readonly IPePermissionService _permissoes;

    public PeModeloController(IPeModeloService service, IPePermissionService permissoes)
    {
        _service = service;
        _permissoes = permissoes;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    /// <summary>
    /// O catálogo inteiro: níveis, etapas com passos, seções, campos e opções, e as seções
    /// fora do PDTIC. Itens apagados só com ?incluirExcluidos=true, e só para quem altera o
    /// modelo (para os outros papéis o parâmetro é ignorado).
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Obter([FromQuery] bool incluirExcluidos = false) =>
        Executar(_permissoes.PodeLerModelo, async ctx =>
            Ok(await _service.ObterModeloAsync(incluirExcluidos && _permissoes.PodeConfigurarModelo(ctx))),
            "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");

    /// <summary>
    /// A trilha resolvida de um órgão: só o que está ligado para ele, na ordem e numerado
    /// pela posição. Papel de órgão: o próprio órgão (sem orgaoId = o dele). Papéis globais
    /// e admin geral: qualquer órgão (orgaoId obrigatório, a menos que a pessoa tenha um).
    /// </summary>
    [HttpGet("trilha")]
    public Task<IActionResult> Trilha([FromQuery] long? orgaoId) =>
        Executar(_permissoes.PodeLerModelo, async ctx =>
        {
            long alvo;
            if (orgaoId != null)
            {
                if (!_permissoes.PodeVerOrgao(ctx, orgaoId.Value))
                    return PeRespostas.SemPermissao("Você só vê o passo a passo do seu próprio órgão.");
                alvo = orgaoId.Value;
            }
            else if (ctx.OrgaoId != null && PapeisPlanejamento.EhDeOrgao(ctx.Papel))
            {
                alvo = ctx.OrgaoId.Value;
            }
            else if (_permissoes.VeTodosOsOrgaos(ctx))
            {
                if (ctx.OrgaoId == null)
                    return PeRespostas.Erro(new service.ApiException(ErrorCode.PeOrgaoObrigatorio, "Escolha o órgão para ver o passo a passo dele."));
                alvo = ctx.OrgaoId.Value;
            }
            else
            {
                return PeRespostas.Erro(new service.ApiException(ErrorCode.PeOrgaoNaoEncontrado,
                    "Seu usuário ainda não está ligado a um órgão. Fale com o administrador do módulo."));
            }
            return Ok(await _service.TrilhaAsync(alvo));
        }, "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");

    /// <summary>
    /// O que mudou no modelo (e no nível e nos ajustes dos órgãos), do mais novo para o mais
    /// antigo, paginado. Filtros: entidade (nivel, etapa, passo, secao, campo, opcao,
    /// orgao_nivel, orgao_ajuste; fora do domínio devolve lista vazia) e id do item.
    /// </summary>
    [HttpGet("historico")]
    public Task<IActionResult> Historico([FromQuery] PeHistoricoConsulta consulta) =>
        Executar(_permissoes.VeTodosOsOrgaos, async _ => Ok(await _service.HistoricoAsync(consulta)),
            "O histórico do modelo é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");

    // ── Modo dos níveis (F3) ────────────────────────────────────────────────

    /// <summary>
    /// Troca o modo dos níveis ({ Modo }: "livre" ou "definido"; o administrador do módulo e o admin
    /// geral) e devolve { ModoNiveis }. Valor fora do domínio: 400; antes da versão 8 do modelo
    /// inicial (o intervalo da atualização): 409 com corpo. A troca fica no histórico do modelo
    /// (entidade configuracao).
    /// </summary>
    [HttpPut("modo-niveis")]
    public Task<IActionResult> DefinirModoNiveis([FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.DefinirModoNiveisAsync(PeCorpo.Ler<PeModoNiveisDTO>(corpo), ctx.Email)));

    // ── Níveis ──────────────────────────────────────────────────────────────

    /// <summary>Cria um nível ({ Nome, Descricao, Codigo?, CopiarDe? }).</summary>
    [HttpPost("niveis")]
    public Task<IActionResult> CriarNivel([FromBody] PeNivelCriarDTO dto) =>
        Escrever(async ctx => Criado(await _service.CriarNivelAsync(dto, ctx.Email)));

    /// <summary>Renomeia, descreve, ativa ou desativa um nível ({ Nome, Descricao, Ativo }).</summary>
    [HttpPut("niveis/{id:long}")]
    public Task<IActionResult> AtualizarNivel(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarNivelAsync(id, PeCorpoParcial.Ler<PeNivelAtualizarDTO>(corpo), ctx.Email)));

    /// <summary>Nova ordem dos níveis ({ Ids: [...] }, todos os níveis).</summary>
    [HttpPut("niveis/ordem")]
    public Task<IActionResult> OrdenarNiveis([FromBody] PeOrdemDTO dto) =>
        Escrever(async ctx => Ok(await _service.OrdenarNiveisAsync(dto, ctx.Email)));

    // ── Etapas ──────────────────────────────────────────────────────────────

    /// <summary>Título, descrição, referência do guia e posição da etapa.</summary>
    [HttpPut("etapas/{id:long}")]
    public Task<IActionResult> AtualizarEtapa(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarEtapaAsync(id, PeCorpoParcial.Ler<PeEtapaAtualizarDTO>(corpo), ctx.Email)));

    // ── Passos ──────────────────────────────────────────────────────────────

    /// <summary>Cria um passo do tipo dados numa etapa (nasce opcional no nível ativo mais alto).</summary>
    [HttpPost("passos")]
    public Task<IActionResult> CriarPasso([FromBody] PePassoCriarDTO dto) =>
        Escrever(async ctx => Criado(await _service.CriarPassoAsync(dto, ctx.Email)));

    [HttpPut("passos/{id:long}")]
    public Task<IActionResult> AtualizarPasso(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarPassoAsync(id, PeCorpoParcial.Ler<PePassoAtualizarDTO>(corpo), ctx.Email)));

    /// <summary>Situação em cada nível ({ Niveis: { "id do nível": "obrigatorio" } }).</summary>
    [HttpPut("passos/{id:long}/niveis")]
    public Task<IActionResult> SituacaoPasso(long id, [FromBody] PeSituacoesDTO dto) =>
        Escrever(async ctx => Ok(await _service.DefinirSituacaoPassoAsync(id, dto, ctx.Email)));

    /// <summary>Apaga (exclusão lógica) o passo criado pelo administrador; os dados ficam guardados.</summary>
    [HttpDelete("passos/{id:long}")]
    public Task<IActionResult> ExcluirPasso(long id) =>
        Escrever(async ctx => Ok(await _service.ExcluirPassoAsync(id, ctx.Email)));

    /// <summary>Nova ordem dos passos de uma etapa ({ Ids }); devolve a etapa.</summary>
    [HttpPut("passos/ordem")]
    public Task<IActionResult> OrdenarPassos([FromBody] PeOrdemDTO dto) =>
        Escrever(async ctx => Ok(await _service.OrdenarPassosAsync(dto, ctx.Email)));

    // ── Seções ──────────────────────────────────────────────────────────────

    /// <summary>Cria uma seção num passo (ou, fora do PDTIC, num escopo); nasce desligada.</summary>
    [HttpPost("secoes")]
    public Task<IActionResult> CriarSecao([FromBody] PeSecaoCriarDTO dto) =>
        Escrever(async ctx => Criado(await _service.CriarSecaoAsync(dto, ctx.Email)));

    [HttpPut("secoes/{id:long}")]
    public Task<IActionResult> AtualizarSecao(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarSecaoAsync(id, PeCorpoParcial.Ler<PeSecaoAtualizarDTO>(corpo), ctx.Email)));

    /// <summary>Situação em cada nível, ou { SituacaoGeral } fora do PDTIC.</summary>
    [HttpPut("secoes/{id:long}/niveis")]
    public Task<IActionResult> SituacaoSecao(long id, [FromBody] PeSituacoesDTO dto) =>
        Escrever(async ctx => Ok(await _service.DefinirSituacaoSecaoAsync(id, dto, ctx.Email)));

    [HttpDelete("secoes/{id:long}")]
    public Task<IActionResult> ExcluirSecao(long id) =>
        Escrever(async ctx => Ok(await _service.ExcluirSecaoAsync(id, ctx.Email)));

    /// <summary>Nova ordem das seções de um passo ({ Ids }); devolve as seções na nova ordem.</summary>
    [HttpPut("secoes/ordem")]
    public Task<IActionResult> OrdenarSecoes([FromBody] PeOrdemDTO dto) =>
        Escrever(async ctx => Ok(await _service.OrdenarSecoesAsync(dto, ctx.Email)));

    // ── Campos ──────────────────────────────────────────────────────────────

    /// <summary>Cria um campo numa seção (sem Chave, o servidor gera pelo rótulo); nasce desligado.</summary>
    [HttpPost("campos")]
    public Task<IActionResult> CriarCampo([FromBody] PeCampoCriarDTO dto) =>
        Escrever(async ctx => Criado(await _service.CriarCampoAsync(dto, ctx.Email)));

    [HttpPut("campos/{id:long}")]
    public Task<IActionResult> AtualizarCampo(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarCampoAsync(id, PeCorpoParcial.Ler<PeCampoAtualizarDTO>(corpo), ctx.Email)));

    [HttpPut("campos/{id:long}/niveis")]
    public Task<IActionResult> SituacaoCampo(long id, [FromBody] PeSituacoesDTO dto) =>
        Escrever(async ctx => Ok(await _service.DefinirSituacaoCampoAsync(id, dto, ctx.Email)));

    [HttpDelete("campos/{id:long}")]
    public Task<IActionResult> ExcluirCampo(long id) =>
        Escrever(async ctx => Ok(await _service.ExcluirCampoAsync(id, ctx.Email)));

    /// <summary>Nova ordem dos campos de uma seção ({ Ids }); devolve a seção.</summary>
    [HttpPut("campos/ordem")]
    public Task<IActionResult> OrdenarCampos([FromBody] PeOrdemDTO dto) =>
        Escrever(async ctx => Ok(await _service.OrdenarCamposAsync(dto, ctx.Email)));

    // ── Opções ──────────────────────────────────────────────────────────────

    /// <summary>Cria uma opção num campo de lista ({ Valor?, Rotulo, Cor }).</summary>
    [HttpPost("campos/{id:long}/opcoes")]
    public Task<IActionResult> CriarOpcao(long id, [FromBody] PeOpcaoCriarDTO dto) =>
        Escrever(async ctx => Criado(await _service.CriarOpcaoAsync(id, dto, ctx.Email)));

    /// <summary>Nova ordem das opções de um campo ({ Ids }, ativas e inativas); devolve o campo.</summary>
    [HttpPut("campos/{id:long}/opcoes/ordem")]
    public Task<IActionResult> OrdenarOpcoes(long id, [FromBody] PeOrdemDTO dto) =>
        Escrever(async ctx => Ok(await _service.OrdenarOpcoesAsync(id, dto, ctx.Email)));

    /// <summary>Rótulo, cor e ativa ({ Rotulo, Cor, Ativa }); o valor não muda.</summary>
    [HttpPut("opcoes/{id:long}")]
    public Task<IActionResult> AtualizarOpcao(long id, [FromBody] JsonElement corpo) =>
        Escrever(async ctx => Ok(await _service.AtualizarOpcaoAsync(id, PeCorpoParcial.Ler<PeOpcaoAtualizarDTO>(corpo), ctx.Email)));

    /// <summary>
    /// Opção criada pelo administrador que ninguém usa: apaga (204). Do sistema ou em uso:
    /// só desativa (200 com a opção). Travada: 409.
    /// </summary>
    [HttpDelete("opcoes/{id:long}")]
    public Task<IActionResult> ExcluirOpcao(long id) =>
        Escrever(async ctx =>
        {
            var opcao = await _service.ExcluirOpcaoAsync(id, ctx.Email);
            return opcao == null ? NoContent() : Ok(opcao);
        });

    // ── Apoio ───────────────────────────────────────────────────────────────

    private IActionResult Criado(object item) => StatusCode(StatusCodes.Status201Created, item);

    private Task<IActionResult> Escrever(Func<PeUserContext, Task<IActionResult>> acao) =>
        Executar(_permissoes.PodeConfigurarModelo, acao, SoAdministrador);

    private Task<IActionResult> Executar(Func<PeUserContext, bool> pode, Func<PeUserContext, Task<IActionResult>> acao,
        string semPermissao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            if (!pode(ctx)) return PeRespostas.SemPermissao(semPermissao);
            return await acao(ctx);
        });
}
