using System.Net.Mail;

namespace service.Pgia;

/// <summary>
/// Validações compartilhadas entre os services do PGIA.
/// </summary>
public static class PgiaValidacoes
{
    /// <summary>
    /// Formato mínimo de e-mail: endereço único, sem apelido, com domínio pontuado.
    /// Mesma regra do pré-cadastro de pessoa e do responsável por risco declarado.
    /// </summary>
    public static bool EmailValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        return MailAddress.TryCreate(email, out var endereco)
            && endereco.Address == email
            && endereco.Host.Contains('.');
    }
}
