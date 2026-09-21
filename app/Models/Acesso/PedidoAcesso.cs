using app.Models;

namespace Models.Acesso;

/// <summary>
/// Pedido de acesso a um módulo, feito pela própria pessoa no card da tela inicial
/// (botão "Pedir acesso"). Fica pendente até alguém decidir: a administração decide
/// todos; a SGDI decide também os do PGIA, porque é ela quem cuida das pessoas do
/// módulo. Aprovar grava o acesso como a tela de gestão de acessos grava; recusar
/// exige um motivo, que a pessoa lê no card. Se o módulo for liberado por outro
/// caminho (tela de gestão, Keycloak, papel), o pedido pendente é encerrado como
/// aprovado.
/// </summary>
public class PedidoAcesso
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    // app.Auth.ModulosSgdp.Liberaveis
    public string Modulo { get; set; } = string.Empty;

    // Opcional: por que a pessoa precisa do módulo
    public string? Justificativa { get; set; }

    // app.Auth.SituacaoPedidoAcesso.Todas
    public string Situacao { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public DateTime? DecididoEm { get; set; }

    // E-mail de quem decidiu. Nulo num pedido aprovado quer dizer que ele foi
    // encerrado sozinho: a pessoa recebeu o acesso por outro caminho (Keycloak, papel)
    public string? DecididoPor { get; set; }

    // Papel no PGIA dado na aprovação (histórico; o papel em vigor fica em Users)
    public string? PapelPgia { get; set; }

    // Obrigatório na recusa
    public string? MotivoRecusa { get; set; }
}
