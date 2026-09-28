using api.Common;
using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Os painéis da SGDI (E8): o painel com os números dos órgãos, a conformidade (só itens de TIC)
/// com a planilha, a árvore do PETIC-DF e a página de cada órgão. Painel, conformidade e árvore:
/// papéis globais (pe_admin, pe_sgdi, pe_cgtic) e admin geral; a página do órgão: também a equipe
/// e a consulta do próprio órgão. O painel e a conformidade leem todos os órgãos de uma vez (poucas
/// consultas, qualquer que seja o número de órgãos) e não gravam nada. Erros como ApiException
/// (1140 a 1159 e os anteriores do módulo).
/// </summary>
public interface IPePainelService
{
    Task<PePainelGeralResponse> PainelAsync(PePainelConsulta consulta, PeUserContext ctx);

    Task<PeConformidadeResponse> ConformidadeAsync(PeConformidadeConsulta consulta, PeUserContext ctx);

    /// <summary>A conformidade numa planilha (csv ou xlsx; padrão xlsx): uma linha por órgão, uma coluna por item.</summary>
    Task<PePlanilhaArquivo> ConformidadePlanilhaAsync(PeConformidadeConsulta consulta, string? formato, PeUserContext ctx);

    /// <summary>Os objetivos do PETIC-DF vigente com os indicadores e, por órgão, as necessidades e as metas ligadas.</summary>
    Task<PeArvorePeticResponse> ArvoreAsync(long? orgaoId, PeUserContext ctx);

    /// <summary>A página do órgão: tudo do órgão numa resposta.</summary>
    Task<PeOrgaoResumoResponse> ResumoAsync(long orgaoId, PeUserContext ctx);
}

/// <summary>
/// A inadimplência do art. 11 do Decreto nº 48.899/2026 (E8): notificar (o prazo de 5 dias úteis
/// para regularizar ou justificar), aceitar a justificativa, registrar a inadimplência depois do
/// prazo (com o motivo e a nota de motivação) e sanear. Ler: papéis globais e admin geral (todos os
/// órgãos) e a equipe e a consulta do órgão (o próprio). Gravar: pe_admin, pe_sgdi e admin geral.
/// Erros: 400 PeInadimplenciaInvalida com Campos, 404 PeInadimplenciaNaoEncontrada, 409
/// PeInadimplenciaSituacaoInvalida e PeInadimplenciaPrazoAberto.
/// </summary>
public interface IPeInadimplenciaService
{
    Task<PagedResponse<PeInadimplenciaResponse>> ListarAsync(PeInadimplenciasConsulta consulta, PeUserContext ctx);

    /// <summary>Todas as inadimplências de um órgão (vigentes primeiro).</summary>
    Task<List<PeInadimplenciaResponse>> DoOrgaoAsync(long orgaoId, PeUserContext ctx);

    Task<PeInadimplenciaResponse> NotificarAsync(long orgaoId, PeInadimplenciaNotificarDTO dto, PeUserContext ctx);

    Task<PeInadimplenciaResponse> JustificarAsync(long id, PeInadimplenciaJustificarDTO dto, PeUserContext ctx);

    Task<PeInadimplenciaResponse> RegistrarAsync(long id, PeInadimplenciaRegistrarDTO dto, PeUserContext ctx);

    Task<PeInadimplenciaResponse> SanearAsync(long id, PeInadimplenciaSanearDTO dto, PeUserContext ctx);
}
