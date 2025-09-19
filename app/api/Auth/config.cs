public class AuthSettings
{
   
    public const string SectionName = "Auth";

    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; }
    public bool ValidateAudience { get; set; }
    public bool ValidateIssuerSigningKey { get; set; }
    public int ExpireMinutes { get; set; }
    public int RefreshTokenExpireMinutes { get; set; }
    public string url { get; set; } = "https://subgd-api.df.gov.br/autenticar";
}