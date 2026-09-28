using System.Security.Claims;
using api.Acesso;
using api.Common;
using app.Models;

namespace service.Interface;

/// <summary>
/// Acesso aos módulos do SGDP (app.Auth.ModulosSgdp): regra única do acesso efetivo,
/// retrato das roles do Keycloak e as operações da tela de gestão de acessos.
/// </summary>
public interface IAcessoModuloService
{
    /// <summary>
    /// Módulos liberados ao usuário da requisição: roles do token + concessões e
    /// papéis gravados no banco. Usuário ainda não cadastrado recebe só o que o
    /// token dá. Usado pela claims transformation (política de cada módulo).
    /// </summary>
    Task<List<string>> ModulosDoPrincipalAsync(ClaimsPrincipal principal);

    /// <summary>Mesma regra, com o usuário já carregado (GET /api/Auth/me).</summary>
    Task<List<string>> ModulosDoUsuarioAsync(ClaimsPrincipal principal, User user);

    /// <summary>
    /// Grava o retrato das roles do Keycloak que dão acesso a módulo (admin, gestor,
    /// pgia) para a tela de gestão mostrar de onde vem cada acesso. Só escreve quando
    /// o retrato muda. Não participa da autorização.
    /// </summary>
    Task SincronizarRetratoKeycloakAsync(Guid userId, ClaimsPrincipal principal);

    Task<PagedResponse<UsuarioAcessoResponse>> ListarUsuariosAsync(AcessoUsuariosConsulta consulta);

    Task<UsuarioAcessoResponse> ObterUsuarioAsync(Guid userId);

    /// <summary>
    /// Grava de uma vez os acessos concedidos no sistema e os papéis de módulo. A
    /// Governança Estratégica (Planejamento e PapelPlanejamento) é opcional: ausente não
    /// muda nada; true exige papel válido (o enviado ou o atual); false tira acesso e papel.
    /// </summary>
    Task<UsuarioAcessoResponse> DefinirAcessosAsync(Guid userId, AcessoUsuarioUpdateDTO dto, string autorEmail);

    /// <summary>
    /// Liga/desliga a concessão (origem sistema) de um módulo concedível. Não vale para a
    /// Governança Estratégica, cujo acesso anda com o papel (DefinirPapelPlanejamentoAsync).
    /// </summary>
    Task DefinirConcessaoAsync(Guid userId, string modulo, bool ativo, string autorEmail);

    /// <summary>
    /// Deixa pronta, SEM gravar, a liberação de um módulo (ModulosSgdp.Liberaveis)
    /// como a tela de gestão faria: concessão do sistema para Demandas e PGIA (com o
    /// papel, quando vier), o papel ctr_analise para a Supervisão Contínua e, na
    /// Governança Estratégica, a concessão com o papel do módulo (obrigatório) e a linha
    /// do histórico ligada ao pedido. Quem chama grava junto com o que mais precisar,
    /// numa transação só (a aprovação de um pedido grava o pedido e o acesso no mesmo
    /// SaveChanges).
    /// </summary>
    Task PrepararLiberacaoAsync(Guid userId, string modulo, string? papelPgia, string autorEmail,
        string? papelPlanejamento, long? pedidoAcessoId);

    /// <summary>
    /// Governança Estratégica: grava o papel da pessoa no módulo (cria ou troca) junto
    /// com a concessão do módulo, ou tira os dois com papel nulo. Toda mudança entra no
    /// histórico com a origem informada (PeDominios.OrigemPapel); dar o papel encerra o
    /// pedido pendente do módulo como aprovado. Uma gravação só.
    /// </summary>
    Task DefinirPapelPlanejamentoAsync(Guid userId, string? papel, string autorEmail, string origem);

    /// <summary>
    /// Encerra como aprovados os pedidos pendentes destes módulos: o acesso chegou
    /// por outro caminho (papel dado pela SGDI, role do Keycloak). autorEmail nulo =
    /// encerrado sozinho, sem decisão de alguém.
    /// </summary>
    Task EncerrarPedidosAtendidosAsync(Guid userId, IEnumerable<string> modulos, string? autorEmail, string? papelPgia);

    /// <summary>
    /// Por usuário: tem concessão do módulo no sistema? o retrato do Keycloak dá o
    /// módulo (role do módulo ou perfil admin)? Em lote (tela de pessoas da SGDI).
    /// </summary>
    Task<Dictionary<Guid, (bool Concedido, bool Keycloak)>> ResumoDoModuloAsync(string modulo, IEnumerable<Guid> userIds);
}
