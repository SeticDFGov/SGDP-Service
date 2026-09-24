using System.Security.Claims;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Ponto único de autorização do módulo Governança Estratégica, no espírito do
/// IPgiaPermissionService: papel do módulo (ou perfil admin do SGDP) e órgão da pessoa.
/// As próximas entregas estendem com recurso + ação + órgão. O contexto é montado sob
/// demanda nas actions do módulo, nunca no caminho de toda requisição.
/// </summary>
public interface IPePermissionService
{
    /// <summary>
    /// Contexto de quem está logado: usuário (pelo KeycloakId e depois pelo e-mail, como
    /// o GetOrCreateUserAsync), papel em pe_papel_usuario, perfil admin do token e órgão
    /// da unidade. Nulo quando o usuário ainda não existe no SGDP.
    /// </summary>
    Task<PeUserContext?> GetContextAsync(ClaimsPrincipal principal);

    /// <summary>Tela "Pessoas e acessos" (dar e tirar papéis): pe_admin e admin geral.</summary>
    bool PodeGerirPessoas(PeUserContext ctx);

    /// <summary>Enxerga todos os órgãos: admin geral e os papéis globais (pe_admin, pe_sgdi, pe_cgtic).</summary>
    bool VeTodosOsOrgaos(PeUserContext ctx);

    /// <summary>
    /// Papel de órgão (pe_orgao ou pe_orgao_consulta) cujo órgão resolvido é este. Admin
    /// geral e papéis globais não são "do órgão" (use VeTodosOsOrgaos para eles).
    /// </summary>
    bool EhDoOrgao(PeUserContext ctx, long orgaoId);

    /// <summary>Lê o modelo e a trilha (GET modelo): qualquer papel do módulo e o admin geral.</summary>
    bool PodeLerModelo(PeUserContext ctx);

    /// <summary>Altera o modelo, os níveis e o nível e os ajustes de cada órgão: pe_admin e admin geral.</summary>
    bool PodeConfigurarModelo(PeUserContext ctx);

    /// <summary>Vê um órgão: papéis globais e admin geral (todos) ou papel de órgão no próprio.</summary>
    bool PodeVerOrgao(PeUserContext ctx, long orgaoId);

    /// <summary>
    /// Órgão ativo de cada unidade, em lote (uma consulta): mesma resolução do
    /// PgiaPermissionService (Users.Unidade para o pgia_orgao ativo daquela unidade).
    /// </summary>
    Task<Dictionary<Guid, PeOrgaoResumo>> OrgaosPorUnidadeAsync(IEnumerable<Guid> unidadeIds);
}
