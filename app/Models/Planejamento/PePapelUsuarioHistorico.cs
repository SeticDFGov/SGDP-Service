using app.Models;

namespace Models.Planejamento;

/// <summary>
/// Trilha de auditoria dos papéis do módulo Governança Estratégica: cada concessão,
/// troca ou retirada de papel vira uma linha, com a origem da mudança e quem a fez.
/// Tabela pe_papel_usuario_historico; mapeamento em PeModelConfiguration.
/// </summary>
public class PePapelUsuarioHistorico
{
    public long Id { get; set; }

    // FK para Users: apagar o usuário apaga o histórico dele
    public Guid UserId { get; set; }

    public User? User { get; set; }

    // Papel antes e depois da mudança; nulo = sem papel (na concessão o anterior é
    // nulo; na retirada, o novo)
    public string? PapelAnterior { get; set; }

    public string? PapelNovo { get; set; }

    // PeDominios.OrigemPapel
    public string Origem { get; set; } = string.Empty;

    // Pedido de acesso aprovado, quando a origem é "pedido". Sem FK: o histórico não
    // depende do pedido
    public long? PedidoAcessoId { get; set; }

    public DateTime AlteradoEm { get; set; }

    // E-mail de quem mudou ("modo-local" nos testes locais)
    public string AlteradoPor { get; set; } = string.Empty;
}
