using api.Common;
using api.Planejamento;

namespace service.Interface;

/// <summary>
/// Modelo configurável do módulo Governança Estratégica (E2): o catálogo (níveis,
/// etapas, passos, seções, campos e opções), a trilha resolvida de um órgão, a escrita
/// do administrador com as regras (travado não desliga, item do sistema não muda de
/// tipo nem de chave e não é apagado, chave única, opção em uso só é desativada) e o
/// histórico com antes e depois. A autorização (quem lê e quem altera) fica no
/// controller, pelo IPePermissionService. Erros como ApiException na faixa 1010 a 1029.
/// </summary>
public interface IPeModeloService
{
    Task<PeModeloResponse> ObterModeloAsync(bool incluirExcluidos);

    /// <summary>Trilha resolvida do órgão (nível escolhido ou o padrão, ajustes, travados).</summary>
    Task<PeTrilhaResponse> TrilhaAsync(long orgaoId);

    Task<PagedResponse<PeHistoricoResponse>> HistoricoAsync(PeHistoricoConsulta consulta);

    Task<PeNivelResponse> CriarNivelAsync(PeNivelCriarDTO dto, string autor);

    Task<PeNivelResponse> AtualizarNivelAsync(long id, PeNivelAtualizarDTO dto, string autor);

    Task<List<PeNivelResponse>> OrdenarNiveisAsync(PeOrdemDTO dto, string autor);

    Task<PeEtapaResponse> AtualizarEtapaAsync(long id, PeEtapaAtualizarDTO dto, string autor);

    Task<PePassoResponse> CriarPassoAsync(PePassoCriarDTO dto, string autor);

    Task<PePassoResponse> AtualizarPassoAsync(long id, PePassoAtualizarDTO dto, string autor);

    Task<PePassoResponse> DefinirSituacaoPassoAsync(long id, PeSituacoesDTO dto, string autor);

    Task<PePassoResponse> ExcluirPassoAsync(long id, string autor);

    /// <summary>Nova ordem dos passos de uma etapa; devolve a etapa.</summary>
    Task<PeEtapaResponse> OrdenarPassosAsync(PeOrdemDTO dto, string autor);

    Task<PeSecaoResponse> CriarSecaoAsync(PeSecaoCriarDTO dto, string autor);

    Task<PeSecaoResponse> AtualizarSecaoAsync(long id, PeSecaoAtualizarDTO dto, string autor);

    Task<PeSecaoResponse> DefinirSituacaoSecaoAsync(long id, PeSituacoesDTO dto, string autor);

    Task<PeSecaoResponse> ExcluirSecaoAsync(long id, string autor);

    /// <summary>Nova ordem das seções de um passo (ou de um escopo fora do PDTIC); devolve a lista na nova ordem.</summary>
    Task<List<PeSecaoResponse>> OrdenarSecoesAsync(PeOrdemDTO dto, string autor);

    Task<PeCampoResponse> CriarCampoAsync(PeCampoCriarDTO dto, string autor);

    Task<PeCampoResponse> AtualizarCampoAsync(long id, PeCampoAtualizarDTO dto, string autor);

    Task<PeCampoResponse> DefinirSituacaoCampoAsync(long id, PeSituacoesDTO dto, string autor);

    Task<PeCampoResponse> ExcluirCampoAsync(long id, string autor);

    /// <summary>Nova ordem dos campos de uma seção; devolve a seção.</summary>
    Task<PeSecaoResponse> OrdenarCamposAsync(PeOrdemDTO dto, string autor);

    Task<PeOpcaoResponse> CriarOpcaoAsync(long campoId, PeOpcaoCriarDTO dto, string autor);

    Task<PeOpcaoResponse> AtualizarOpcaoAsync(long id, PeOpcaoAtualizarDTO dto, string autor);

    /// <summary>
    /// Apaga a opção criada pelo administrador que ninguém usa (devolve nulo) ou só a
    /// desativa, quando é do sistema ou está em uso (devolve a opção). Opção travada: 409.
    /// </summary>
    Task<PeOpcaoResponse?> ExcluirOpcaoAsync(long id, string autor);

    /// <summary>Nova ordem das opções de um campo (ativas e inativas); devolve o campo.</summary>
    Task<PeCampoResponse> OrdenarOpcoesAsync(long campoId, PeOrdemDTO dto, string autor);
}

/// <summary>
/// Nível e ajustes de cada órgão (pgia_orgao) no módulo Governança Estratégica: a lista
/// dos órgãos com o nível, a troca de nível com justificativa e histórico, e os ajustes
/// por cima do nível (item travado nunca desliga).
/// </summary>
public interface IPeOrgaoService
{
    Task<List<PeOrgaoNivelResponse>> ListarAsync(PeOrgaosConsulta consulta);

    Task<PeOrgaoNivelResponse> DefinirNivelAsync(long orgaoId, PeOrgaoNivelDTO dto, string autor);

    Task<List<PeOrgaoNivelHistoricoResponse>> HistoricoNivelAsync(long orgaoId);

    Task<List<PeOrgaoAjusteResponse>> AjustesAsync(long orgaoId);

    /// <summary>Grava a lista inteira: o que não vem na lista (ou vem com Situacao nula) sai.</summary>
    Task<List<PeOrgaoAjusteResponse>> DefinirAjustesAsync(long orgaoId, List<PeOrgaoAjusteDTO> ajustes, string autor);
}
