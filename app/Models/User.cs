using System.ComponentModel.DataAnnotations;

namespace app.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? KeycloakId { get; set; }

    [Required]
    public string Nome { get; set; }

    [Required]
    public string Email { get; set; }

    // Papel no módulo PGIA (Decreto nº 48.901/2026); null = sem função no PGIA.
    // Administrado por endpoint do próprio sistema, nunca pelo Keycloak.
    [StringLength(30)]
    public string? PapelPgia { get; set; }

    public Unidade? Unidade { get; set; }
}
