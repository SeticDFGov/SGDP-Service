namespace api;
public class UsuarioDTO
{
    public string NomeCompleto { get; set; }
    public string Email { get; set; }
    public string DisplayName { get; set; }
    public string Nome { get; set; }
    public string Sobrenome { get; set; }
    public List<string> Grupos { get; set; }
}

public class LdapLoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}
