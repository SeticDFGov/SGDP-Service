namespace api.Auth;

public class ConfigAuth
{
    public readonly string url;
    public readonly string issuer;
    public readonly string audience;
    public readonly string Key;

    public ConfigAuth()
    {
        issuer = Environment.GetEnvironmentVariable("issuer") ?? throw new InvalidOperationException("Issuer não definida");
        audience = Environment.GetEnvironmentVariable("audience") ?? throw new InvalidOperationException("audience não definida");
        url = Environment.GetEnvironmentVariable("LDAP_URL") ?? throw new InvalidOperationException("LDAP_URL não definida no ambiente.");
        Key = Environment.GetEnvironmentVariable("Key") ?? throw new InvalidOperationException("Chave não definina no ambiente");
    } 
}