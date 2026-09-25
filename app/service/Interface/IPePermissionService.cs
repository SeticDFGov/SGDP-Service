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

    /// <summary>Lê o PETIC-DF, os princípios, as diretrizes do ciclo e os catálogos: qualquer papel e o admin geral.</summary>
    bool PodeLerReferenciais(PeUserContext ctx);

    /// <summary>
    /// Vê uma versão do PETIC-DF: papéis globais e admin geral, todas; papéis de órgão, só as
    /// aprovadas (a vigente e as substituídas). Rascunho e versão em deliberação não aparecem
    /// para o órgão (nem na lista, nem nos registros, planilhas e anexos).
    /// </summary>
    bool PodeVerVersaoPetic(PeUserContext ctx, string situacao);

    /// <summary>Edita o PETIC-DF (rascunho), o catálogo do DF e envia ao CGTIC: pe_admin e admin geral.</summary>
    bool PodeEditarReferenciais(PeUserContext ctx);

    /// <summary>Vê a fila de deliberações do CGTIC: papéis globais e admin geral.</summary>
    bool PodeVerDeliberacoes(PeUserContext ctx);

    /// <summary>Registra a decisão do CGTIC (Secretaria Executiva): pe_cgtic e admin geral.</summary>
    bool PodeDecidirDeliberacao(PeUserContext ctx);

    /// <summary>Envia arquivo (anexo de um campo): quem edita alguma coisa no módulo (pe_admin, pe_cgtic, pe_orgao e admin geral).</summary>
    bool PodeEnviarArquivo(PeUserContext ctx);

    // ── PDTIC dos órgãos (E4) ───────────────────────────────────────────────

    /// <summary>
    /// Abre e edita o PDTIC de um órgão (registros, "não se aplica"): a equipe do órgão
    /// (pe_orgao) no próprio órgão e o admin geral em qualquer um. A consulta do órgão e os
    /// papéis globais só leem (a leitura é o PodeVerOrgao).
    /// </summary>
    bool PodeEditarPdtic(PeUserContext ctx, long orgaoId);

    /// <summary>
    /// F3: escolhe a forma de um passo para o órgão no modo livre: a equipe do PDTIC (pe_orgao) no
    /// próprio órgão, o administrador do módulo e o admin geral.
    /// </summary>
    bool PodeEscolherFormaDoPasso(PeUserContext ctx, long orgaoId);

    /// <summary>
    /// Comenta um passo do PDTIC (comentário principal): pe_admin, pe_sgdi e admin geral. A
    /// Secretaria do CGTIC não comenta (plano, seção 4.1); quem responde é a equipe do órgão.
    /// </summary>
    bool PodeComentarPdtic(PeUserContext ctx);

    /// <summary>Planilhas consolidadas de todos os órgãos e a lista dos PDTICs: papéis globais e admin geral.</summary>
    bool PodeVerConsolidado(PeUserContext ctx);

    // ── Painéis da SGDI (E8) ────────────────────────────────────────────────

    /// <summary>
    /// O painel, a conformidade, a árvore do PETIC-DF, a lista de todas as inadimplências e a
    /// página de qualquer órgão: papéis globais (pe_admin, pe_sgdi, pe_cgtic) e admin geral. A
    /// equipe e a consulta do órgão veem só a página e as inadimplências do próprio órgão
    /// (PodeVerOrgao), em leitura.
    /// </summary>
    bool PodeVerPaineis(PeUserContext ctx);

    /// <summary>Notifica, aceita a justificativa, registra a inadimplência e o saneamento: pe_admin, pe_sgdi e admin geral.</summary>
    bool PodeRegistrarInadimplencia(PeUserContext ctx);

    /// <summary>
    /// Órgão ativo de cada unidade, em lote (uma consulta): mesma resolução do
    /// PgiaPermissionService (Users.Unidade para o pgia_orgao ativo daquela unidade).
    /// </summary>
    Task<Dictionary<Guid, PeOrgaoResumo>> OrgaosPorUnidadeAsync(IEnumerable<Guid> unidadeIds);
}
