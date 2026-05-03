namespace api.Auth;

public class KeycloakLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}

public class KeycloakRefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
