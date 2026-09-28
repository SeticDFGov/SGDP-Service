using Microsoft.EntityFrameworkCore;
using Models;

namespace service.Planejamento;

/// <summary>
/// Os nomes das pessoas nas respostas do módulo (F1, achado C19 da revisão final): ao lado de
/// cada "...Por" (o e-mail de quem fez, guardado na auditoria), o "...PorNome", com o nome do
/// cadastro do usuário (Users.Nome). Sem cadastro com aquele e-mail, ou com o nome em branco,
/// vale o próprio e-mail (os autores do sistema, como "carregador-modelo" e "modo-local", ficam
/// como estão). O e-mail é comparado sem diferenciar maiúsculas, e os nomes de uma lista inteira
/// saem de uma consulta só (nada de uma consulta por item).
/// </summary>
public sealed class PeNomes
{
    private readonly Dictionary<string, string> _nomes;

    /// <summary>Sem nome nenhum carregado: cada e-mail vale como está.</summary>
    public static readonly PeNomes Vazio = new(new Dictionary<string, string>());

    private PeNomes(Dictionary<string, string> nomes)
    {
        _nomes = nomes;
    }

    /// <summary>Os nomes dos e-mails dados (os nulos e em branco ficam de fora), numa consulta só.</summary>
    public static async Task<PeNomes> CarregarAsync(AppDbContext context, IEnumerable<string?> emails)
    {
        var lista = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (lista.Count == 0) return Vazio;

        var usuarios = await context.Users.AsNoTracking()
            .Where(u => u.Email != null && lista.Contains(u.Email.ToLower()))
            .Select(u => new { u.Email, u.Nome })
            .ToListAsync();
        var nomes = new Dictionary<string, string>();
        // Dois cadastros com o mesmo e-mail (caixa diferente): vale o primeiro com nome, pela ordem do e-mail
        foreach (var usuario in usuarios.OrderBy(u => u.Email, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(usuario.Email) || string.IsNullOrWhiteSpace(usuario.Nome)) continue;
            nomes.TryAdd(usuario.Email.Trim().ToLowerInvariant(), usuario.Nome.Trim());
        }
        return new PeNomes(nomes);
    }

    /// <summary>O nome da pessoa do e-mail; sem nome no cadastro, o próprio e-mail; nulo quando o e-mail é nulo.</summary>
    public string? De(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return email;
        return _nomes.TryGetValue(email.Trim().ToLowerInvariant(), out var nome) ? nome : email.Trim();
    }

    /// <summary>O nome da pessoa (nunca nulo: sem e-mail, texto vazio).</summary>
    public string DeObrigatorio(string? email) => De(email) ?? string.Empty;
}
