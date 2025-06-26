using System.Text.Json.Serialization;

public class LdapRequestDto

{
    [JsonPropertyName("Email")]
    public string Email { get; set; } = string.Empty;
    [JsonPropertyName("Senha")]
    public string Senha {get;set;} = string.Empty;

}
