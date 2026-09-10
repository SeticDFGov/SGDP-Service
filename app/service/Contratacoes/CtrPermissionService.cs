using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace service.Contratacoes;

/// <summary>
/// Contexto do usuário no módulo Análises de Contratações. O papel efetivo considera
/// o perfil admin do SGDP (acesso total) e, fora isso, apenas Users.PapelContratacoes.
/// </summary>
public class CtrUserContext
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Perfil { get; set; } = string.Empty;

    public string? PapelContratacoes { get; set; }

    public bool IsAdmin => Perfil == Perfis.Admin;

    public string PapelEfetivo => IsAdmin ? Perfis.Admin : PapelContratacoes ?? string.Empty;
}

/// <summary>
/// Recursos do módulo usados na matriz papel x ação x recurso. Hoje o módulo é
/// ferramenta central da SGDI (quem entra vê e gere tudo), mas a matriz já nasce
/// por recurso para a ferramenta crescer sem refazer a autorização.
/// </summary>
public static class CtrResources
{
    public const string Processo = "processo";          // processos de contratação em análise
    public const string Manifestacao = "manifestacao";  // manifestações da SGDI ao TCDF
    public const string Painel = "painel";              // indicadores agregados
    public const string Importacao = "importacao";      // importação da planilha legada
    public const string Papel = "papel";                // atribuição do papel do módulo
}

/// <summary>
/// Autorização do módulo Análises de Contratações, espelhando o desenho do
/// PgiaPermissionService (papel + ação + recurso) sem tocar no PermissionService
/// do SGDP. Sem recorte por órgão: é ferramenta interna da equipe central.
/// Todo endpoint valida no backend; a checagem do front é só usabilidade.
/// </summary>
public class CtrPermissionService : ICtrPermissionService
{
    private readonly AppDbContext _context;

    public CtrPermissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CtrUserContext?> GetContextAsync(string email, string perfil)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return null;

        return new CtrUserContext
        {
            UserId = user.Id,
            Email = user.Email,
            Perfil = perfil,
            PapelContratacoes = user.PapelContratacoes
        };
    }

    public bool CanView(CtrUserContext ctx, string resource)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            PapeisContratacoes.Analise => true,
            _ => false
        };
    }

    /// <summary>
    /// Criar e editar são a mesma permissão neste módulo (por isso não há CanCreate):
    /// quem analisa contratação cadastra, edita e registra manifestação.
    /// </summary>
    public bool CanEdit(CtrUserContext ctx, string resource)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            PapeisContratacoes.Analise => true,
            _ => false
        };
    }

    public bool PodeAcessar(CtrUserContext ctx) =>
        ctx.IsAdmin || ctx.PapelContratacoes == PapeisContratacoes.Analise;
}
