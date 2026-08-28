namespace api.Pgia;

/// <summary>
/// Atribuição de papel PGIA a um usuário (PUT api/pgia/admin/papel).
/// PapelPgia nulo remove o papel.
/// </summary>
public class AtribuirPapelPgiaDTO
{
    public string Email { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string? PapelPgia { get; set; }
}
