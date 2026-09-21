using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using service.Interface;

namespace app.Auth;

/// <summary>
/// Acrescenta ao usuário autenticado uma claim <see cref="ModulosSgdp.ClaimModulo"/>
/// por módulo liberado a ele — é o que as políticas "modulo:*" dos controllers
/// conferem. Roda uma vez por requisição autenticada (uma consulta ao banco);
/// requisição anônima (Registro Público do PGIA) passa direto.
///
/// As claims vão numa identidade à parte, sem mexer nas roles do token, e a marca
/// torna a transformação idempotente (o framework pode chamá-la mais de uma vez).
/// </summary>
public class ModuloAcessoClaimsTransformation : IClaimsTransformation
{
    private const string Marca = "sgdp_modulos_resolvidos";

    private readonly IAcessoModuloService _acessos;

    public ModuloAcessoClaimsTransformation(IAcessoModuloService acessos)
    {
        _acessos = acessos;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.HasClaim(c => c.Type == Marca)) return principal;

        var modulos = await _acessos.ModulosDoPrincipalAsync(principal);

        var identidade = new ClaimsIdentity();
        identidade.AddClaim(new Claim(Marca, "1"));
        foreach (var modulo in modulos)
            identidade.AddClaim(new Claim(ModulosSgdp.ClaimModulo, modulo));

        principal.AddIdentity(identidade);
        return principal;
    }
}
