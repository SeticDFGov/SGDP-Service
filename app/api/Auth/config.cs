namespace api.Auth;

public class ConfigAuth
{
    public readonly string url;
    public readonly string issuer;
    public readonly string audience;
    public readonly string Key;

    public ConfigAuth()
    {
        issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException("Issuer não definida");
        audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException("audience não definida");
        url = Environment.GetEnvironmentVariable("LDAP_URL") ?? throw new InvalidOperationException("LDAP_URL não definida no ambiente.");
        Key = Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new InvalidOperationException("Chave não definina no ambiente");
    } 
}