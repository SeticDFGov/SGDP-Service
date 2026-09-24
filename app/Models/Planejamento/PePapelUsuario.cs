using app.Models;

namespace Models.Planejamento;

/// <summary>
/// Papel de uma pessoa no módulo Governança Estratégica (app.Auth.PapeisPlanejamento),
/// uma linha por usuário. Anda junto com a concessão do módulo em acesso_modulo: quem
/// dá o papel dá o acesso, quem tira o papel tira o acesso (AcessoModuloService). Toda
/// mudança deixa uma linha em <see cref="PePapelUsuarioHistorico"/>.
/// Tabela pe_papel_usuario; mapeamento em PeModelConfiguration. Users não ganha coluna.
/// </summary>
public class PePapelUsuario
{
    // Chave e FK para Users: apagar o usuário apaga o papel
    public Guid UserId { get; set; }

    public User? User { get; set; }

    // app.Auth.PapeisPlanejamento.Todos
    public string Papel { get; set; } = string.Empty;

    // Primeira concessão (UTC) e quem a fez (e-mail; "modo-local" nos testes locais)
    public DateTime ConcedidoEm { get; set; }

    public string ConcedidoPor { get; set; } = string.Empty;

    // Última troca de papel (nulas enquanto o papel for o da concessão)
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
