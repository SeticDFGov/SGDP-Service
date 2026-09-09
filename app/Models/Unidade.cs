using System.ComponentModel.DataAnnotations;

namespace app.Models;

public class Unidade
{
    [Key]
    public Guid id {get;set;} = Guid.NewGuid();
    [Required]
    public string Nome {get;set;}

    // Código do grupo Keycloak correspondente (claim "unidade_codigo"), usado para
    // sincronizar a unidade do usuário automaticamente no login.
    public string? CodigoExterno {get;set;}
}