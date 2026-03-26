using System.ComponentModel.DataAnnotations;

namespace app.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public string Nome { get; set; }
    [Required]
    public string Email { get; set; }

    public string Perfil { get; set; } = "basico";

    public Unidade? Unidade {get;set;}

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public DateTime? RefreshTokenCreatedAt { get; set; }
    public DateTime? RefreshTokenRevokedAt { get; set; }

    public bool HasValidRefreshToken()
    {
        return !string.IsNullOrEmpty(RefreshToken)
               && RefreshTokenExpiresAt.HasValue
               && RefreshTokenExpiresAt.Value > DateTime.UtcNow
               && !RefreshTokenRevokedAt.HasValue;
    }
}