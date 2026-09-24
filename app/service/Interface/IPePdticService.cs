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
    /// <summary>
    /// O PDTIC atual do órgão (sem orgaoId, o da própria pessoa): a versão em elaboração (a
    /// revisão, quando há), senão a vigente; nulo quando não há nenhuma.
    /// </summary>
    Task<PePdticResponse?> AtualAsync(long? orgaoId, PeUserContext ctx);

    /// <summary>Todas as versões do PDTIC do órgão (sem orgaoId, o da própria pessoa), da mais nova para a mais antiga (E7).</summary>
    Task<List<PePdticResponse>> VersoesAsync(long? orgaoId, PeUserContext ctx);

    Task<PePdticResponse> ObterAsync(long id, PeUserContext ctx);

    /// <summary>A resposta do PDTIC sem conferir quem lê (quem chama já conferiu; o caminho da aprovação usa).</summary>
    Task<PePdticResponse> ResponderAsync(long id, PeUserContext ctx);

    /// <summary>Abre o PDTIC (versão 1.0, em elaboração); 409 se o órgão já tem um em andamento (em elaboração ou vigente).</summary>
    Task<PePdticResponse> AbrirAsync(PePdticCriarDTO dto, PeUserContext ctx);

    /// <summary>Os PDTICs atuais de todos os órgãos (papéis globais e admin geral), por sigla.</summary>
    Task<PagedResponse<PePdticResponse>> ListarAsync(PePdticConsulta consulta, PeUserContext ctx);

    Task<PePdticSituacaoResponse> SituacaoAsync(long id, PeUserContext ctx);

    /// <summary>
    /// A situação dos passos já com o PDTIC e a trilha do órgão em mãos (sem conferir quem lê; a
    /// E7 e a E8 usam). Com ctx, o PodeEditar de cada passo; sem ele, falso.
    /// </summary>
    Task<PePdticSituacaoResponse> CalcularSituacaoAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeUserContext? ctx = null);

    /// <summary>A situação dos passos com o que falta em cada passo pendente (o envio ao CGTIC usa).</summary>
    Task<PeSituacaoDetalhada> DetalharSituacaoAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeUserContext? ctx = null);

    Task<PePassoSituacaoResponse> MarcarNaoSeAplicaAsync(long id, long passoId, PeNaoSeAplicaDTO dto, PeUserContext ctx);

    Task<PePassoSituacaoResponse> DesmarcarNaoSeAplicaAsync(long id, long passoId, PeUserContext ctx);

    Task<PeTemasResponse> TemasAsync(long id, PeUserContext ctx);

    Task<List<PeSistemaIaPgiaResponse>> SistemasIaAsync(long id, PeUserContext ctx);
}

/// <summary>
/// O caminho da aprovação do PDTIC (E7, rodada A): a prévia do envio, enviar ao CGTIC,
/// publicar, encerrar, revisar e registrar o PDTIC aprovado fora do sistema. A decisão do
/// CGTIC fica no IPeDeliberacaoService. Erros como ApiException (1100 a 1119, e os anteriores
/// do módulo); o envio com pendências lança PePendenciasException, e a publicação e o registro
/// externo sem os dados, PeValidacaoException (com Campos).
/// </summary>
public interface IPePdticAprovacaoService
{
    /// <summary>O que falta para enviar ao CGTIC, passo a passo, e se quem chama pode enviar agora.</summary>
    Task<PeEnvioResponse> EnvioAsync(long id, PeUserContext ctx);

    /// <summary>Envia ao CGTIC (a equipe do órgão, em elaboração ou devolvido): gera o PDF enviado e cria a deliberação.</summary>
    Task<PePdticResponse> EnviarAsync(long id, PeUserContext ctx);

    /// <summary>Registra a publicação (a equipe do órgão, com o PDTIC aprovado, a data e o endereço).</summary>
    Task<PePdticResponse> PublicarAsync(long id, PeUserContext ctx);

    /// <summary>Encerra (a equipe, com a aprovação da autoridade máxima; o administrador, com o motivo, depois da vigência).</summary>
    Task<PePdticResponse> EncerrarAsync(long id, PeEncerrarDTO dto, PeUserContext ctx);

    /// <summary>Abre a revisão do PDTIC vigente (a versão seguinte, em elaboração, com a cópia dos dados); devolve o PDTIC novo.</summary>
    Task<PePdticResponse> RevisarAsync(long id, PeRevisaoDTO dto, PeUserContext ctx);

    /// <summary>Registra o PDTIC aprovado fora do sistema (já publicado), para o órgão acompanhar.</summary>
    Task<PePdticResponse> RegistrarExternoAsync(PeRegistroExternoDTO dto, PeUserContext ctx);
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
