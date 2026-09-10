namespace app.Auth;

/// <summary>
/// Papéis do módulo Análises de Contratações, guardados em Users.PapelContratacoes.
/// Não são perfis do SGDP nem roles do Keycloak — espelho de <see cref="PapeisPgia"/>.
/// </summary>
public static class PapeisContratacoes
{
    // Analista da SGDI que acompanha as análises de contratações de TIC
    public const string Analise = "ctr_analise";

    public static readonly string[] Todos = { Analise };

    public static bool EhValido(string? papel) =>
        papel == null || Todos.Contains(papel);
}
