using api.Common;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// O PDTIC de cada órgão (E4): abrir, ler, listar os atuais, a situação de cada passo da
/// trilha (com os avisos do guia), o "não se aplica", a conferência dos temas das ações e os
/// sistemas de IA do PGIA do órgão. A autorização fica aqui, pelo IPePermissionService;
/// erros como ApiException (1050 a 1069, e os anteriores do módulo).
/// </summary>
public interface IPePdticService
{
    /// <summary>O PDTIC atual do órgão (sem orgaoId, o da própria pessoa), ou nulo.</summary>
    Task<PePdticResponse?> AtualAsync(long? orgaoId, PeUserContext ctx);

    Task<PePdticResponse> ObterAsync(long id, PeUserContext ctx);

    /// <summary>Abre o PDTIC (versão 1.0, em elaboração); 409 se o órgão já tem um atual.</summary>
    Task<PePdticResponse> AbrirAsync(PePdticCriarDTO dto, PeUserContext ctx);

    /// <summary>Os PDTICs atuais de todos os órgãos (papéis globais e admin geral), por sigla.</summary>
    Task<PagedResponse<PePdticResponse>> ListarAsync(PePdticConsulta consulta, PeUserContext ctx);

    Task<PePdticSituacaoResponse> SituacaoAsync(long id, PeUserContext ctx);

    /// <summary>A situação dos passos já com o PDTIC e a trilha em mãos (sem conferir quem chama; a E7 e a E8 usam).</summary>
    Task<PePdticSituacaoResponse> CalcularSituacaoAsync(PePdtic pdtic, PeTrilhaOrgao trilha);

    Task<PePassoSituacaoResponse> MarcarNaoSeAplicaAsync(long id, long passoId, PeNaoSeAplicaDTO dto, PeUserContext ctx);

    Task<PePassoSituacaoResponse> DesmarcarNaoSeAplicaAsync(long id, long passoId, PeUserContext ctx);

    Task<PeTemasResponse> TemasAsync(long id, PeUserContext ctx);

    Task<List<PeSistemaIaPgiaResponse>> SistemasIaAsync(long id, PeUserContext ctx);
}

/// <summary>Comentários dos passos do PDTIC (E4).</summary>
public interface IPeComentarioService
{
    /// <summary>Os comentários principais (de um passo ou de todos), cada um com as respostas.</summary>
    Task<List<PeComentarioResponse>> ListarAsync(long pdticId, long? passoId, PeUserContext ctx);

    /// <summary>Comenta (PaiId nulo) ou responde; devolve a conversa (o comentário principal com as respostas).</summary>
    Task<PeComentarioResponse> CriarAsync(long pdticId, PeComentarioCriarDTO dto, PeUserContext ctx);

    /// <summary>Marca o comentário principal como resolvido (a equipe do órgão ou quem comentou).</summary>
    Task<PeComentarioResponse> ResolverAsync(long id, PeUserContext ctx);
}
