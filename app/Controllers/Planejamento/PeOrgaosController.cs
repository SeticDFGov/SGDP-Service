using System.Text.Json;
using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// Nível de maturidade e ajustes de cada órgão no módulo Governança Estratégica (E2).
/// Lista dos órgãos: papéis globais e admin geral. Histórico do nível e ajustes de um
/// órgão: quem vê o órgão (papéis globais, admin geral e o papel de órgão no próprio).
/// Trocar o nível e gravar os ajustes: pe_admin e admin geral. Erros como
/// { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento/orgaos")]
public class PeOrgaosController : ControllerBase
{
    private readonly IPeOrgaoService _service;
    private readonly IPePermissionService _permissoes;

    public PeOrgaosController(IPeOrgaoService service, IPePermissionService permissoes)
    {
        _service = service;
        _permissoes = permissoes;
    }

    /// <summary>Órgãos ativos com o nível (ou o padrão) e quantos ajustes têm; filtro por sigla ou nome.</summary>
    [HttpGet]
    public Task<IActionResult> Listar([FromQuery] PeOrgaosConsulta consulta) =>
        Executar(ctx => _permissoes.VeTodosOsOrgaos(ctx), async _ => Ok(await _service.ListarAsync(consulta)),
            "A lista de órgãos é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");

    /// <summary>
    /// Troca o nível do órgão ({ NivelId, Justificativa }, justificativa obrigatória). Desde a F1,
    /// NivelId nulo devolve o órgão ao nível padrão (o histórico marca NivelNovoPadrao).
    /// </summary>
    [HttpPut("{id:long}/nivel")]
    public Task<IActionResult> DefinirNivel(long id, [FromBody] PeOrgaoNivelDTO dto) =>
        Executar(ctx => _permissoes.PodeConfigurarModelo(ctx), async ctx => Ok(await _service.DefinirNivelAsync(id, dto, ctx.Email)),
            "Só o administrador do módulo define o nível de cada órgão.");

    /// <summary>Trocas de nível do órgão, da mais nova para a mais antiga.</summary>
    [HttpGet("{id:long}/nivel/historico")]
    public Task<IActionResult> HistoricoNivel(long id) =>
        Executar(ctx => _permissoes.PodeVerOrgao(ctx, id), async _ => Ok(await _service.HistoricoNivelAsync(id)),
            "Você só vê o seu próprio órgão.");

    /// <summary>Ajustes do órgão por cima do nível.</summary>
    [HttpGet("{id:long}/ajustes")]
    public Task<IActionResult> Ajustes(long id) =>
        Executar(ctx => _permissoes.PodeVerOrgao(ctx, id), async _ => Ok(await _service.AjustesAsync(id)),
            "Você só vê o seu próprio órgão.");

    /// <summary>
    /// Grava a lista inteira de ajustes ([ { AlvoTipo, AlvoId, Situacao, Justificativa } ]):
    /// o que não vem na lista, ou vem com Situacao nula, deixa de ter ajuste.
    /// </summary>
    [HttpPut("{id:long}/ajustes")]
    public Task<IActionResult> DefinirAjustes(long id, [FromBody] List<PeOrgaoAjusteDTO> ajustes) =>
        Executar(ctx => _permissoes.PodeConfigurarModelo(ctx), async ctx => Ok(await _service.DefinirAjustesAsync(id, ajustes, ctx.Email)),
            "Só o administrador do módulo ajusta o passo a passo de um órgão.");

    /// <summary>
    /// F3: escolhe a forma de um passo para o órgão no modo livre ({ NivelId }: um nível das
    /// OpcoesDetalhe do passo; nulo volta à forma padrão) e devolve o passo a passo do órgão. A
    /// equipe do PDTIC do próprio órgão, o administrador do módulo e o admin geral. No modo
    /// definido, ou com o passo fechado no PDTIC atual do órgão, 409 PeDetalheRecusado (1146).
    /// </summary>
    [HttpPut("{orgaoId:long}/passos/{passoId:long}/detalhe")]
    public Task<IActionResult> DefinirDetalhe(long orgaoId, long passoId, [FromBody] JsonElement corpo) =>
        Executar(ctx => _permissoes.PodeEscolherFormaDoPasso(ctx, orgaoId),
            async ctx => Ok(await _service.DefinirDetalheAsync(orgaoId, passoId, PeCorpo.Ler<PePassoDetalheDTO>(corpo), ctx.Email)),
            "Quem escolhe a forma dos passos é a equipe do PDTIC do órgão ou o administrador do módulo.");

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
