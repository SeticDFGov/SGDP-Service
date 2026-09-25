namespace app.Auth;

/// <summary>
/// Papéis do módulo Governança Estratégica (<see cref="ModulosSgdp.Planejamento"/>),
/// guardados na tabela pe_papel_usuario (uma linha por usuário), e não numa coluna de
/// Users como no PGIA e na Supervisão Contínua. Não são perfis do SGDP nem roles do
/// Keycloak. O perfil admin do SGDP tem tudo no módulo sem precisar de papel.
///
/// Regra do deploy: o cálculo do acesso de cada requisição (claims transformation,
/// AcessoModuloService.CalcularModulos e o GET /api/Auth/me) nunca lê pe_papel_usuario.
/// O acesso ao módulo é a linha de acesso_modulo, gravada e retirada junto com o papel;
/// o papel chega ao front por GET api/planejamento/meu-papel, que só quem tem o módulo
/// chama. Assim, no intervalo entre publicar o código e rodar a migration, só as telas
/// do módulo novo falham.
/// </summary>
public static class PapeisPlanejamento
{
    // Administrador do módulo (SGDI, gestão estratégica): configura, dá acesso e papéis
    // (inclusive o de administrador) e decide os pedidos de acesso do módulo
    public const string Admin = "pe_admin";
    // Equipe da SGDI: vê todos os órgãos, comenta, registra notificação e inadimplência
    // e exporta o consolidado
    public const string Sgdi = "pe_sgdi";
    // Secretaria Executiva do CGTIC (art. 9º do Decreto nº 48.900/2026): recebe os PDTICs
    // e o PETIC enviados e registra a deliberação do comitê
    public const string Cgtic = "pe_cgtic";
    // Equipe do PDTIC (EqPDTIC; na tela, desde a F3, "Equipe do PDTIC"): elabora e acompanha
    // o PDTIC do próprio órgão
    public const string Orgao = "pe_orgao";
    // Consulta do PDTIC (autoridade máxima, membros do SGTIC): só lê o PDTIC do próprio órgão
    public const string OrgaoConsulta = "pe_orgao_consulta";

    public static readonly string[] Todos = { Admin, Sgdi, Cgtic, Orgao, OrgaoConsulta };

    /// <summary>
    /// Um dos cinco papéis. Diferente de PapeisPgia.EhValido, nulo NÃO é válido: no
    /// módulo não existe acesso sem papel (quem recebe nulo trata como "tirar o papel").
    /// </summary>
    public static bool EhValido(string? papel) => papel != null && Todos.Contains(papel);

    /// <summary>
    /// Papéis presos ao órgão da pessoa: o órgão sai da unidade dela (Users.Unidade para
    /// o pgia_orgao ativo daquela unidade), como no PGIA.
    /// </summary>
    public static bool EhDeOrgao(string? papel) => papel is Orgao or OrgaoConsulta;

    /// <summary>Papéis que enxergam todos os órgãos.</summary>
    public static bool EhGlobal(string? papel) => papel is Admin or Sgdi or Cgtic;
}
