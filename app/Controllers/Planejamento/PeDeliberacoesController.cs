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
/// Deliberações do CGTIC (E3: PETIC-DF; E7: PDTIC). A fila e o histórico: papéis globais e
/// admin geral. Registrar a decisão (Secretaria Executiva do CGTIC): pe_cgtic e admin geral.
/// Erros como { Code, Message } (<see cref="PeRespostas"/>).
/// </summary>
[ApiController]
[Authorize(Policy = ModulosSgdp.PoliticaPlanejamento)]
[Route("api/planejamento/deliberacoes")]
public class PeDeliberacoesController : ControllerBase
{
    private readonly IPeDeliberacaoService _service;
    private readonly IPePermissionService _permissoes;

    public PeDeliberacoesController(IPeDeliberacaoService service, IPePermissionService permissoes)
    {
        _service = service;
        _permissoes = permissoes;
    }

    /// <summary>
    /// A fila: aguardando primeiro (da mais antiga), depois as decididas (da mais nova).
    /// Filtros: Situacao, ObjetoTipo, Page e PageSize (fora do domínio: lista vazia).
    /// </summary>
    [HttpGet]
    public Task<IActionResult> Listar([FromQuery] PeDeliberacoesConsulta consulta) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            if (!_permissoes.PodeVerDeliberacoes(ctx))
                return PeRespostas.SemPermissao("A fila de deliberações é da Secretaria do CGTIC, da SGDI e do administrador do módulo.");
            return Ok(await _service.ListarAsync(consulta));
        });

    /// <summary>
    /// Registra a decisão ({ Decisao: "aprovado" | "devolvido", AtoTipo, AtoNumero, AtoData,
    /// Sei, Observacao }). Aprovado exige número e data do ato; devolvido exige a observação.
    /// </summary>
    [HttpPost("{id:long}/decidir")]
    public Task<IActionResult> Decidir(long id, [FromBody] JsonElement corpo) =>
        PeRespostas.ExecutarAsync(async () =>
        {
            var ctx = await _permissoes.GetContextAsync(User);
            if (ctx == null) return PeRespostas.CadastroNaoEncontrado();
            if (!_permissoes.PodeDecidirDeliberacao(ctx))
                return PeRespostas.SemPermissao("Só a Secretaria Executiva do CGTIC registra a deliberação.");
            return Ok(await _service.DecidirAsync(id, PeCorpo.Ler<PeDecidirDTO>(corpo), ctx));
        });
}
