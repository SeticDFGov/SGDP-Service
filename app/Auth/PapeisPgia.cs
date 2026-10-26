namespace app.Auth;

/// <summary>
/// Papéis do módulo PGIA (Decreto nº 48.901/2026), guardados em Users.PapelPgia.
/// Não são perfis do SGDP nem roles do Keycloak. O papel de órgão também pode vir
/// da designação vigente, sem nada gravado (ver <see cref="Efetivo"/>).
/// </summary>
public static class PapeisPgia
{
    public const string Orgao = "pgia_orgao";         // Responsável de IA e equipe do órgão (arts. 9º a 11)
    public const string Sgdi = "pgia_sgdi";           // órgão central da política (art. 8º)
    public const string Cgtic = "pgia_cgtic";         // secretaria do comitê (art. 7º)
    public const string Auditoria = "pgia_auditoria"; // entidade auditora externa (arts. 25, § 2º e 34)

    public static readonly string[] Todos = { Orgao, Sgdi, Cgtic, Auditoria };

    public static bool EhValido(string? papel) =>
        papel == null || Todos.Contains(papel);

    /// <summary>
    /// Papel que vale no PGIA: o gravado em Users.PapelPgia; sem ele, quem é
    /// Responsável de IA, Encarregado de Dados ou Curador de Dados de Negócio vigente
    /// no órgão da própria unidade age como <see cref="Orgao"/>. Assim a carga das
    /// designações não grava papel no banco: o acesso ao módulo continua vindo do
    /// Keycloak (role pgia) e o papel acompanha a designação (encerrou, some).
    /// </summary>
    public static string? Efetivo(string? gravado, bool temDesignacaoVigenteNoOrgao) =>
        gravado ?? (temDesignacaoVigenteNoOrgao ? Orgao : null);
}
