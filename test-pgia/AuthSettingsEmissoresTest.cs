using Xunit;

namespace test.pgia;

/// <summary>
/// Emissores aceitos na validação do JWT (login OIDC pelo Keycloak em
/// https://subgd-api.df.gov.br/keycloak): o público e o endereço interno, sem repetir.
/// </summary>
public class AuthSettingsEmissoresTest
{
    private const string Interno = "http://keycloak.default.svc.cluster.local:8080/keycloak/realms/SISTEMA_GEDE";
    private const string Publico = "https://subgd-api.df.gov.br/keycloak/realms/SISTEMA_GEDE";

    [Fact]
    public void SemIssuer_ValeSoOAuthority_ComoAntes()
    {
        Assert.Equal(new[] { Interno }, AuthSettings.EmissoresValidos(Interno, null));
        Assert.Equal(new[] { Interno }, AuthSettings.EmissoresValidos(Interno, "  "));
    }

    [Fact]
    public void ComIssuer_AceitaOPublicoPrimeiroEOAuthority()
    {
        Assert.Equal(new[] { Publico, Interno }, AuthSettings.EmissoresValidos(Interno, Publico));
    }

    [Fact]
    public void IssuerIgualAoAuthority_NaoRepete_EIgnoraABarraFinal()
    {
        Assert.Equal(new[] { Publico }, AuthSettings.EmissoresValidos(Publico, Publico + "/"));
    }
}
