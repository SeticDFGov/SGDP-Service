using Microsoft.AspNetCore.Authorization;

namespace app.Auth;

/// <summary>
/// Módulos do SGDP e as regras de acesso a cada um. Isolamento real: quem não tem
/// acesso a um módulo não o enxerga no front NEM na API. Cada controller exige a
/// política do seu módulo (<see cref="PoliticaDemandas"/> etc.).
///
/// O acesso efetivo soma duas fontes (decisão do dono do produto, compatível com
/// produção):
/// 1. Keycloak: role admin (todos os módulos), role gestor (Demandas) e role "pgia"
///    do grupo/órgão (PGIA, como agente do art. 13);
/// 2. o próprio sistema: concessões da tela de gestão de acessos (tabela
///    acesso_modulo: Demandas, PGIA e Governança Estratégica) e os papéis de módulo
///    já existentes (Users.PapelPgia dá acesso ao PGIA; Users.PapelContratacoes dá
///    acesso à Supervisão Contínua). Na Governança Estratégica o acesso é só a linha
///    de acesso_modulo, gravada junto com o papel do módulo (pe_papel_usuario); o
///    cálculo do acesso não lê a tabela do papel (regra do deploy, ver
///    <see cref="PapeisPlanejamento"/>).
/// Usuário sem role e sem concessão não enxerga nada.
/// </summary>
public static class ModulosSgdp
{
    // Painel e demandas/entregáveis (o SGDP original)
    public const string Demandas = "demandas";
    // Governança de IA (Decreto nº 48.901/2026)
    public const string Pgia = "pgia";
    // Supervisão Contínua das Contratações (rota /analises no front)
    public const string Contratacoes = "contratacoes";
    // Governança Estratégica (Decreto nº 48.900/2026: princípios, PETIC-DF e o PDTIC de
    // cada órgão; rota /planejamento no front). Acesso por concessão em acesso_modulo,
    // sempre gravada junto com o papel do módulo (PapeisPlanejamento)
    public const string Planejamento = "planejamento";
    // Administração do SGDP: só a role admin do Keycloak
    public const string Administracao = "administracao";

    public static readonly string[] Todos = { Demandas, Pgia, Contratacoes, Planejamento, Administracao };

    /// <summary>
    /// Módulos que a tela de gestão de acessos concede por linha em acesso_modulo.
    /// Contratações não entra: o acesso a ela é o próprio papel do módulo
    /// (Users.PapelContratacoes), gravado pela mesma tela. Na Governança Estratégica a
    /// concessão anda junto com o papel do módulo (pe_papel_usuario): quem grava uma
    /// grava a outra, e quem tira uma tira a outra. Administração só vem do Keycloak.
    /// </summary>
    public static readonly string[] Concedidos = { Demandas, Pgia, Planejamento };

    /// <summary>
    /// Módulos que o próprio sistema libera: pela tela de gestão de acessos e pelos
    /// pedidos de acesso. Administração fica de fora porque vem só do perfil admin
    /// do Keycloak.
    /// </summary>
    public static readonly string[] Liberaveis = { Demandas, Pgia, Contratacoes, Planejamento };

    /// <summary>Role do Keycloak (atribuída ao grupo do órgão) que abre o PGIA.</summary>
    public const string RolePgia = "pgia";

    /// <summary>Claim com cada módulo liberado ao usuário, acrescentada por requisição.</summary>
    public const string ClaimModulo = "sgdp_modulo";

    public const string PoliticaDemandas = "modulo:" + Demandas;
    public const string PoliticaPgia = "modulo:" + Pgia;
    public const string PoliticaContratacoes = "modulo:" + Contratacoes;
    public const string PoliticaPlanejamento = "modulo:" + Planejamento;
    public const string PoliticaAdministracao = "modulo:" + Administracao;

    public static string Politica(string modulo) => "modulo:" + modulo;

    public static bool EhValido(string? modulo) => modulo != null && Todos.Contains(modulo);

    /// <summary>
    /// Uma política por módulo: exige a claim <see cref="ClaimModulo"/> que a
    /// <see cref="ModuloAcessoClaimsTransformation"/> acrescenta a cada requisição.
    /// </summary>
    public static void AdicionarPoliticas(AuthorizationOptions options)
    {
        foreach (var modulo in Todos)
            options.AddPolicy(Politica(modulo), politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimModulo, modulo));
    }

    /// <summary>
    /// Ordem das roles do token nas claims: o perfil do SGDP primeiro, por
    /// precedência (admin > gestor > basico), depois as demais roles do token sem
    /// repetir. Os controllers leem o perfil com FindFirst(ClaimTypes.Role) e a ordem
    /// que o Keycloak manda não é garantida — um gestor que também está num grupo
    /// PGIA chegava com o perfil "pgia" e perdia a edição de demandas.
    /// </summary>
    public static List<string> RolesComPerfilPrimeiro(IReadOnlyCollection<string> rolesDoToken)
    {
        var perfil = rolesDoToken.Contains(Perfis.Admin) ? Perfis.Admin
            : rolesDoToken.Contains(Perfis.Gestor) ? Perfis.Gestor
            : Perfis.Basico;

        var ordenadas = new List<string> { perfil };
        ordenadas.AddRange(rolesDoToken.Where(r => r != perfil).Distinct());
        return ordenadas;
    }
}

/// <summary>Origem de uma linha de acesso_modulo.</summary>
public static class OrigemAcesso
{
    // Concedido na tela de gestão de acessos (editável)
    public const string Sistema = "sistema";
    // Retrato das roles do Keycloak no último login (só leitura, informativo:
    // a autorização usa sempre as roles do token da requisição)
    public const string Keycloak = "keycloak";

    public static readonly string[] Todos = { Sistema, Keycloak };
}

/// <summary>Situação de um pedido de acesso (tabela pedido_acesso).</summary>
public static class SituacaoPedidoAcesso
{
    // Na fila, esperando a administração (ou a SGDI, no PGIA, e o administrador do
    // módulo, na Governança Estratégica)
    public const string Pendente = "pendente";
    // Módulo liberado: pela decisão ou porque o acesso chegou por outro caminho
    public const string Aprovado = "aprovado";
    // Recusado com motivo, que a pessoa lê no card do módulo
    public const string Recusado = "recusado";

    public static readonly string[] Todas = { Pendente, Aprovado, Recusado };
}
