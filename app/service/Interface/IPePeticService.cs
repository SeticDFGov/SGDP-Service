using api.Common;
using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Versões do PETIC-DF (art. 11 do Decreto nº 48.900/2026): uma em rascunho (ou em
/// deliberação) por vez, criada vazia ou copiada da vigente; o envio ao CGTIC cria a
/// deliberação. A autorização (quem lê e quem edita) fica no controller; a regra da
/// situação (só rascunho muda) fica aqui. Erros como ApiException (1030 a 1049).
/// </summary>
public interface IPePeticService
{
    /// <summary>
    /// As versões que quem chama vê (papel de órgão: só as aprovadas), da mais nova para a mais
    /// antiga, com a deliberação mais recente de cada uma.
    /// </summary>
    Task<List<PePeticResponse>> ListarAsync(PeUserContext ctx);

    /// <summary>A versão aprovada (a vigente) ou nulo.</summary>
    Task<PePeticResponse?> VigenteAsync();

    /// <summary>Uma versão (papel de órgão: rascunho e em deliberação dão 404).</summary>
    Task<PePeticResponse> ObterAsync(long id, PeUserContext ctx);

    Task<PePeticResponse> CriarAsync(PePeticCriarDTO dto, PeUserContext ctx);

    Task<PePeticResponse> AtualizarAsync(long id, PePeticAtualizarDTO dto, PeUserContext ctx);

    /// <summary>Confere as pendências, cria a deliberação e põe a versão em deliberação.</summary>
    Task<PePeticResponse> EnviarAsync(long id, PeUserContext ctx);

    /// <summary>Apaga o rascunho que nunca foi ao CGTIC (com os registros dele).</summary>
    Task ExcluirAsync(long id, PeUserContext ctx);
}

/// <summary>
/// Deliberações do CGTIC registradas pela Secretaria Executiva: a fila (aguardando primeiro,
/// da mais antiga; depois as decididas, da mais nova) e a decisão, que aprova (com ato e
/// data; a versão anterior fica substituída) ou devolve (com observação; volta a rascunho).
/// </summary>
public interface IPeDeliberacaoService
{
    Task<PagedResponse<PeDeliberacaoResponse>> ListarAsync(PeDeliberacoesConsulta consulta);

    Task<PeDeliberacaoResponse> DecidirAsync(long id, PeDecidirDTO dto, PeUserContext ctx);
}
