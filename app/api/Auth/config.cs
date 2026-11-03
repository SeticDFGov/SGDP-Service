public class AuthSettings
{
    public const string SectionName = "Auth";

    // Endereço do realm pelo qual a API fala com o Keycloak: busca as chaves (.well-known) e
    // chama o token endpoint do login antigo. Pode ser o endereço interno do cluster.
    public string Authority { get; set; } = string.Empty;

    // Emissor (iss) dos tokens: o endereço público do realm (KC_HOSTNAME do Keycloak), o mesmo
    // que o navegador usa no login OIDC. Vazio = o próprio Authority (comportamento anterior).
    public string? Issuer { get; set; }

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Emissores aceitos na validação do token: o público e o Authority, sem repetir. Aceitar os
    /// dois cobre a troca de endereço do Keycloak sem derrubar quem tem um token emitido antes.
    /// </summary>
    public static string[] EmissoresValidos(string authority, string? issuer) =>
        new[] { issuer, authority }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim().TrimEnd('/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
