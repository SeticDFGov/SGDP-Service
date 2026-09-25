using System.Security.Claims;
using api.Acesso;
using api.Common;

namespace service.Interface;

/// <summary>
/// Quem decide pedidos de acesso e de quais módulos: a administração decide todos
/// os módulos que o sistema libera; a SGDI (PapelPgia pgia_sgdi) decide os do PGIA;
/// o administrador da Governança Estratégica (pe_admin) decide os desse módulo; quem
/// tem os dois papéis decide os dois. Lista vazia = não decide nada.
/// </summary>
public record DecisorPedidos(string Email, IReadOnlyCollection<string> Modulos)
{
    public bool PodeDecidir => Modulos.Count > 0;
}

/// <summary>
/// Pedidos de acesso aos módulos: a pessoa pede no card da tela inicial e a
/// administração (ou a SGDI, no PGIA, e o administrador do módulo, na Governança
/// Estratégica) aprova ou recusa. Aprovar libera o módulo na hora, pelo mesmo caminho
/// da tela de gestão de acessos.
/// </summary>
public interface IPedidoAcessoService
{
    /// <summary>
    /// Registra o pedido de quem está logado. Já havendo pedido pendente do mesmo
    /// módulo, devolve esse (dois cliques não viram dois pedidos).
    /// </summary>
    Task<MeuPedidoAcessoResponse> CriarAsync(ClaimsPrincipal principal, PedidoAcessoCreateDTO dto);

    /// <summary>Último pedido de cada módulo de quem está logado.</summary>
    Task<List<MeuPedidoAcessoResponse>> MeusPedidosAsync(ClaimsPrincipal principal);

    Task<DecisorPedidos> DecisorAsync(ClaimsPrincipal principal);

    /// <summary>Fila paginada: pendentes primeiro, por ordem de chegada; depois os decididos.</summary>
    Task<PagedResponse<PedidoAcessoResponse>> ListarAsync(PedidosAcessoConsulta consulta, DecisorPedidos decisor);

    Task<int> ContarPendentesAsync(DecisorPedidos decisor, string? modulo);

    /// <summary>Libera o módulo e encerra o pedido, numa gravação só.</summary>
    Task<PedidoAcessoResponse> AprovarAsync(long pedidoId, PedidoAcessoAprovarDTO dto, DecisorPedidos decisor);

    /// <summary>Recusa com motivo obrigatório, que a pessoa lê no card do módulo.</summary>
    Task<PedidoAcessoResponse> RecusarAsync(long pedidoId, PedidoAcessoRecusarDTO dto, DecisorPedidos decisor);
}
