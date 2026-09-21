using app.Models;

namespace Models.Acesso;

/// <summary>
/// Acesso de um usuário a um módulo do SGDP (app.Auth.ModulosSgdp).
///
/// Origem "sistema": concessão feita na tela de gestão de acessos — é o que o
/// administrador liga e desliga. Origem "keycloak": retrato das roles do token
/// no último login, gravado só para a tela mostrar de onde vem cada acesso (a
/// autorização de cada requisição lê as roles do próprio token, nunca este retrato).
/// </summary>
public class AcessoModulo
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    // ModulosSgdp.Todos
    public string Modulo { get; set; } = string.Empty;

    // OrigemAcesso.Todos
    public string Origem { get; set; } = string.Empty;

    public DateTime ConcedidoEm { get; set; }

    // E-mail de quem concedeu (ou "keycloak" no retrato do login)
    public string ConcedidoPor { get; set; } = string.Empty;
}
