using service.Contratacoes;

namespace service.Interface;

public interface ICtrPermissionService
{
    /// <summary>
    /// Resolve o contexto do usuário no módulo: papel (Users.PapelContratacoes) e
    /// perfil (da claim do token, nunca persistido). Null se o usuário não existir.
    /// </summary>
    Task<CtrUserContext?> GetContextAsync(string email, string perfil);

    bool CanView(CtrUserContext ctx, string resource);

    /// <summary>Criar e editar são a mesma permissão neste módulo.</summary>
    bool CanEdit(CtrUserContext ctx, string resource);

    /// <summary>Acesso ao módulo (admin ou ctr_analise) — usado pelo front via /me.</summary>
    bool PodeAcessar(CtrUserContext ctx);
}
