using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaPermissionService
{
    /// <summary>
    /// Resolve o contexto PGIA do usuário: papel (Users.PapelPgia), perfil admin do SGDP
    /// (vindo da claim do token, nunca persistido) e o órgão vinculado à Unidade do
    /// usuário. Null se o usuário não existir.
    /// </summary>
    Task<PgiaUserContext?> GetContextAsync(string email, string perfil);

    /// <summary>
    /// Papel PGIA que vale para o usuário (<see cref="app.Auth.PapeisPgia.Efetivo"/>): o
    /// gravado ou, sem ele, pgia_orgao para quem tem designação vigente no órgão ativo
    /// da própria unidade. Usado pelo contexto do PGIA e pelo GET /api/Auth/me.
    /// </summary>
    Task<string?> PapelEfetivoAsync(Guid userId, string? papelGravado, Guid? unidadeId);

    bool CanView(PgiaUserContext ctx, string resource);
    bool CanCreate(PgiaUserContext ctx, string resource);
    bool CanEdit(PgiaUserContext ctx, string resource);

    /// <summary>Escopo de dados: o usuário pode acessar dados deste órgão?</summary>
    bool CanAccessOrgao(PgiaUserContext ctx, long orgaoId);

    /// <summary>Órgãos visíveis ao usuário (todos para admin/sgdi/cgtic; só o próprio para pgia_orgao).</summary>
    IQueryable<PgiaOrgao> GetFilteredOrgaosQuery(PgiaUserContext ctx);

    /// <summary>Sistemas de IA visíveis ao usuário, no mesmo escopo por órgão.</summary>
    IQueryable<PgiaSistemaIa> GetFilteredSistemasQuery(PgiaUserContext ctx);

    /// <summary>Art. 13: qualquer autenticado com órgão resolvido registra uso e avisa incidentes.</summary>
    bool PodeRegistrarUso(PgiaUserContext ctx);
}
