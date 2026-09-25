using System.ComponentModel.DataAnnotations;

namespace api.Acesso;

/// <summary>Pedido de acesso feito pela própria pessoa (botão "Pedir acesso" do card).</summary>
public class PedidoAcessoCreateDTO
{
    // app.Auth.ModulosSgdp.Liberaveis
    [Required]
    [StringLength(30)]
    public string Modulo { get; set; } = string.Empty;

    // Opcional: por que a pessoa precisa do módulo
    [StringLength(500)]
    public string? Justificativa { get; set; }
}

/// <summary>
/// Pedido visto por quem pediu: o card de cada módulo mostra a situação do último
/// pedido (aguardando, ou recusado com o motivo).
/// </summary>
public class MeuPedidoAcessoResponse
{
    public long Id { get; set; }

    public string Modulo { get; set; } = string.Empty;

    // app.Auth.SituacaoPedidoAcesso
    public string Situacao { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public DateTime? DecididoEm { get; set; }

    // Só na recusa
    public string? MotivoRecusa { get; set; }
}

/// <summary>
/// Pedido visto por quem decide (administração, SGDI no PGIA e administrador do módulo
/// na Governança Estratégica).
/// </summary>
public class PedidoAcessoResponse
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? UnidadeNome { get; set; }

    public string Modulo { get; set; } = string.Empty;

    public string? Justificativa { get; set; }

    public string Situacao { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public DateTime? DecididoEm { get; set; }

    // E-mail de quem decidiu; nulo num aprovado = a pessoa recebeu o acesso por outro caminho
    public string? DecididoPor { get; set; }

    // Papel no PGIA dado na aprovação
    public string? PapelPgia { get; set; }

    // Só nos pedidos da Governança Estratégica: o papel que a pessoa tem HOJE no módulo
    // (lido de pe_papel_usuario; pedido_acesso não guarda esse papel). Nulo nos demais
    public string? PapelPlanejamento { get; set; }

    public string? MotivoRecusa { get; set; }
}

/// <summary>
/// Filtros da fila de pedidos. O parâmetro da action se chama "consulta", nunca o
/// nome de uma propriedade (armadilha do model binding registrada no módulo de
/// contratações).
/// </summary>
public class PedidosAcessoConsulta
{
    // SituacaoPedidoAcesso.Todas; vazio = todas
    public string? Situacao { get; set; }

    // Um dos módulos que quem consulta decide; vazio = todos eles
    public string? Modulo { get; set; }

    // Parte do nome ou do e-mail de quem pediu
    public string? Filtro { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Aprovação: libera o módulo na hora (e, na Governança Estratégica, dá o papel junto).</summary>
public class PedidoAcessoAprovarDTO
{
    // Só no PGIA: PapeisPgia.Todos, ou nulo para a pessoa entrar como agente público (art. 13)
    [StringLength(30)]
    public string? PapelPgia { get; set; }

    // Só na Governança Estratégica, e lá obrigatório: PapeisPlanejamento.Todos (o acesso
    // ao módulo vem junto com o papel). Em pedido de outro módulo deve vir nulo
    [StringLength(30)]
    public string? PapelPlanejamento { get; set; }
}

/// <summary>Recusa: o motivo é obrigatório e a pessoa o lê no card do módulo.</summary>
public class PedidoAcessoRecusarDTO
{
    [Required]
    [StringLength(500)]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Quantos pedidos esperam a decisão de quem consulta.</summary>
public class PedidosAcessoPendentesResponse
{
    public int Total { get; set; }
}
