using api.Common;
using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Pessoas e papéis do módulo Governança Estratégica: o papel de quem está logado, a
/// lista de quem tem papel, a busca de quem pode receber um e a gravação do papel
/// (sempre com a concessão do módulo, o histórico e o encerramento do pedido
/// pendente). A autorização (quem pode gerir pessoas) fica no controller, pelo
/// IPePermissionService; a regra do auto rebaixamento fica aqui.
/// </summary>
public interface IPePessoaService
{
    /// <summary>Papel, órgão e unidade de quem está logado (GET meu-papel).</summary>
    PeMeuPapelResponse MeuPapel(PeUserContext ctx);

    /// <summary>Pessoas com papel no módulo, paginado; filtros por nome ou e-mail e por papel.</summary>
    Task<PagedResponse<PePessoaResponse>> ListarAsync(PePessoasConsulta consulta);

    /// <summary>
    /// Até 20 usuários que já entraram no SGDP, sem papel no módulo, cujo nome ou e-mail
    /// contém o filtro (mínimo de 3 letras; menos que isso devolve lista vazia).
    /// </summary>
    Task<List<PeCandidataResponse>> CandidatasAsync(string? filtro);

    /// <summary>
    /// Dá, troca ou tira (papel nulo) o papel da pessoa, com a concessão do módulo, o
    /// histórico (origem "pessoas") e o encerramento do pedido pendente, numa gravação
    /// só. O pe_admin não tira nem troca o próprio papel; o admin geral pode tudo.
    /// </summary>
    Task<PePessoaResponse> DefinirPapelAsync(PeUserContext autor, Guid userId, string? papel);
}
