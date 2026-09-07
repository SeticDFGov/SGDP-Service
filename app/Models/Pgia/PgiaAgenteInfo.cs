using app.Models;

namespace Models.Pgia;

/// <summary>
/// Extensão 1:1 opcional de Users com os dados de agente público do PGIA
/// (matrícula, cargo e vínculo — art. 3º). Substitui a tabela agente_publico
/// do schema validado: Users já é o cadastro de pessoas.
/// </summary>
public class PgiaAgenteInfo
{
    // PK compartilhada com Users
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    // D11. Alcance da política (art. 3º)
    public string Vinculo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
