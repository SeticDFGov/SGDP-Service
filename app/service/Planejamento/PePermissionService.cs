using System.Security.Claims;
using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Contexto de quem está logado no módulo Governança Estratégica. O perfil admin do
/// SGDP (EhAdminGeral) tem tudo sem precisar de papel.
/// </summary>
public class PeUserContext
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    // Perfil admin do SGDP (role admin do token)
    public bool EhAdminGeral { get; set; }

    // app.Auth.PapeisPlanejamento.Todos ou nulo
    public string? Papel { get; set; }

    public Guid? UnidadeId { get; set; }

    public string? UnidadeNome { get; set; }

    // Órgão (pgia_orgao ATIVO) da unidade da pessoa; nulo sem unidade ou sem órgão
    public long? OrgaoId { get; set; }

    public string? OrgaoSigla { get; set; }

    public string? OrgaoNome { get; set; }
}

/// <summary>Órgão resolvido de uma unidade.</summary>
public record PeOrgaoResumo(long Id, string Sigla, string Nome);

/// <summary>
/// Autorização do módulo Governança Estratégica: papel do módulo (pe_papel_usuario) ou
/// perfil admin do SGDP, mais o órgão da pessoa. Espelha o desenho do
/// PgiaPermissionService sem tocá-lo, inclusive a resolução do órgão: a unidade da
/// pessoa aponta para o pgia_orgao ATIVO daquela unidade (1:1), e órgão desativado não
/// dá escopo (falha fechada). Toda validação acontece no backend; a checagem do front é
/// só usabilidade.
/// </summary>
public class PePermissionService : IPePermissionService
{
    private readonly AppDbContext _context;

    public PePermissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PeUserContext?> GetContextAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
        if (keycloakId == null && email == null) return null;

        // Mesma ordem de busca do GetOrCreateUserAsync: primeiro o KeycloakId, depois o e-mail
        var candidatos = await _context.Users.AsNoTracking()
            .Include(u => u.Unidade)
            .Where(u => (keycloakId != null && u.KeycloakId == keycloakId) || (email != null && u.Email == email))
            .ToListAsync();
        var user = candidatos.FirstOrDefault(u => keycloakId != null && u.KeycloakId == keycloakId)
            ?? candidatos.FirstOrDefault();
        if (user == null) return null;

        var ctx = new PeUserContext
        {
            UserId = user.Id,
            Email = email ?? user.Email,
            EhAdminGeral = principal.IsInRole(Perfis.Admin),
            Papel = await _context.PePapeisUsuario.AsNoTracking()
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Papel)
                .FirstOrDefaultAsync(),
            UnidadeId = user.Unidade?.id,
            UnidadeNome = user.Unidade?.Nome
        };

        if (ctx.UnidadeId != null)
        {
            var orgaos = await OrgaosPorUnidadeAsync(new[] { ctx.UnidadeId.Value });
            if (orgaos.TryGetValue(ctx.UnidadeId.Value, out var orgao))
            {
                ctx.OrgaoId = orgao.Id;
                ctx.OrgaoSigla = orgao.Sigla;
                ctx.OrgaoNome = orgao.Nome;
            }
        }

        return ctx;
    }

    public bool PodeGerirPessoas(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel == PapeisPlanejamento.Admin;

    public bool VeTodosOsOrgaos(PeUserContext ctx) =>
        ctx.EhAdminGeral || PapeisPlanejamento.EhGlobal(ctx.Papel);

    public bool PodeLerModelo(PeUserContext ctx) =>
        ctx.EhAdminGeral || PapeisPlanejamento.EhValido(ctx.Papel);

    public bool PodeConfigurarModelo(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel == PapeisPlanejamento.Admin;

    public bool PodeVerOrgao(PeUserContext ctx, long orgaoId) =>
        VeTodosOsOrgaos(ctx) || EhDoOrgao(ctx, orgaoId);

    // ── Referenciais (E3) ───────────────────────────────────────────────────

    public bool PodeLerReferenciais(PeUserContext ctx) =>
        ctx.EhAdminGeral || PapeisPlanejamento.EhValido(ctx.Papel);

    public bool PodeVerVersaoPetic(PeUserContext ctx, string situacao) =>
        ctx.EhAdminGeral || !PapeisPlanejamento.EhDeOrgao(ctx.Papel)
        || situacao is Models.Planejamento.PeDominios.SituacaoPetic.Aprovado or Models.Planejamento.PeDominios.SituacaoPetic.Substituido;

    public bool PodeEditarReferenciais(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel == PapeisPlanejamento.Admin;

    public bool PodeVerDeliberacoes(PeUserContext ctx) => VeTodosOsOrgaos(ctx);

    public bool PodeDecidirDeliberacao(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel == PapeisPlanejamento.Cgtic;

    public bool PodeEnviarArquivo(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel is PapeisPlanejamento.Admin or PapeisPlanejamento.Cgtic or PapeisPlanejamento.Orgao;

    public bool EhDoOrgao(PeUserContext ctx, long orgaoId) =>
        PapeisPlanejamento.EhDeOrgao(ctx.Papel) && ctx.OrgaoId == orgaoId;

    // ── PDTIC dos órgãos (E4) ───────────────────────────────────────────────

    public bool PodeEditarPdtic(PeUserContext ctx, long orgaoId) =>
        ctx.EhAdminGeral || (ctx.Papel == PapeisPlanejamento.Orgao && ctx.OrgaoId == orgaoId);

    // Plano, seção 4.1: o administrador do módulo e a SGDI comentam; a Secretaria do CGTIC não
    public bool PodeComentarPdtic(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel is PapeisPlanejamento.Admin or PapeisPlanejamento.Sgdi;

    public bool PodeVerConsolidado(PeUserContext ctx) => VeTodosOsOrgaos(ctx);

    // ── Painéis da SGDI (E8) ────────────────────────────────────────────────

    // Plano, seção 12: o painel, a conformidade, a árvore do PETIC-DF, a lista das inadimplências e
    // a página de qualquer órgão são dos papéis globais e do admin geral
    public bool PodeVerPaineis(PeUserContext ctx) => VeTodosOsOrgaos(ctx);

    // Plano, seção 4.1: notificação e inadimplência são do administrador do módulo e da SGDI
    public bool PodeRegistrarInadimplencia(PeUserContext ctx) =>
        ctx.EhAdminGeral || ctx.Papel is PapeisPlanejamento.Admin or PapeisPlanejamento.Sgdi;

    public async Task<Dictionary<Guid, PeOrgaoResumo>> OrgaosPorUnidadeAsync(IEnumerable<Guid> unidadeIds)
    {
        var ids = unidadeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, PeOrgaoResumo>();

        var orgaos = await _context.PgiaOrgaos.AsNoTracking()
            .Where(o => o.Ativo && o.UnidadeId != null && ids.Contains(o.UnidadeId.Value))
            .Select(o => new { o.Id, o.Sigla, o.Nome, UnidadeId = o.UnidadeId!.Value })
            .ToListAsync();

        // O índice único ux_pgia_orgao_unidade garante um órgão por unidade no
        // PostgreSQL; o menor Id desempata onde o índice não existe (InMemory)
        return orgaos
            .GroupBy(o => o.UnidadeId)
            .ToDictionary(g => g.Key, g =>
            {
                var o = g.OrderBy(x => x.Id).First();
                return new PeOrgaoResumo(o.Id, o.Sigla, o.Nome);
            });
    }
}
