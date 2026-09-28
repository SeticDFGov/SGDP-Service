using api.Planejamento;
using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;
using service.Planejamento;

namespace Controllers.Planejamento;

/// <summary>
/// Os painéis da SGDI (E8): o painel com os números dos órgãos, a conformidade (com a planilha),
/// a árvore do PETIC-DF e a página de cada órgão. Painel, conformidade e árvore: papéis globais e
/// admin geral; a página de um órgão, também a equipe e a consulta do próprio órgão. A autorização
/// fica no serviço; erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento")]
public class PePaineisController : ControllerBase
{
    private readonly IPePainelService _paineis;
    private readonly IPePermissionService _permissoes;

    public PePaineisController(IPePainelService paineis, IPePermissionService permissoes)
    {
        _paineis = paineis;
        _permissoes = permissoes;
    }

    /// <summary>O painel: órgãos por situação (e, em elaboração, por etapa), por nível, as ligações com o PETIC-DF, ações, riscos, execução e alertas.</summary>
    [HttpGet("painel")]
    public Task<IActionResult> Painel([FromQuery] PePainelConsulta consulta) =>
        Executar(async ctx => Ok(await _paineis.PainelAsync(consulta, ctx)));

    /// <summary>A conformidade de cada órgão (só itens de TIC): ?Grupo=&amp;NivelId=&amp;Filtro=&amp;Situacao=&amp;Alerta=.</summary>
    [HttpGet("conformidade")]
    public Task<IActionResult> Conformidade([FromQuery] PeConformidadeConsulta consulta) =>
        Executar(async ctx => Ok(await _paineis.ConformidadeAsync(consulta, ctx)));

    /// <summary>
    /// A conformidade numa planilha (?formato=csv ou xlsx; padrão xlsx): PDTIC_conformidade_aaaa-mm-dd,
    /// com os mesmos filtros da tela (Grupo, NivelId, Filtro, Situacao e Alerta; F2).
    /// </summary>
    [HttpGet("conformidade/planilha")]
    public Task<IActionResult> ConformidadePlanilha([FromQuery] PeConformidadeConsulta consulta, [FromQuery] string? formato) =>
        Executar(async ctx =>
        {
            var planilha = await _paineis.ConformidadePlanilhaAsync(consulta, formato, ctx);
            return File(planilha.Conteudo, planilha.TipoMime, planilha.NomeArquivo);
        });

    /// <summary>A árvore do PETIC-DF vigente: objetivos, indicadores e, por órgão, as necessidades e as metas ligadas (?orgaoId= filtra).</summary>
    [HttpGet("petic/arvore")]
    public Task<IActionResult> Arvore([FromQuery] long? orgaoId) =>
        Executar(async ctx => Ok(await _paineis.ArvoreAsync(orgaoId, ctx)));

    /// <summary>A página do órgão: tudo do órgão numa resposta.</summary>
    [HttpGet("orgaos/{id:long}/resumo")]
    public Task<IActionResult> Resumo(long id) =>
        Executar(async ctx => Ok(await _paineis.ResumoAsync(id, ctx)));

    private Task<IActionResult> Executar(Func<PeUserContext, Task<IActionResult>> acao) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            return await acao(ctx);
        });
}
