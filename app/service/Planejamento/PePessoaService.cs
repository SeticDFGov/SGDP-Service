using api.Common;
using api.Planejamento;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Pessoas e papéis do módulo Governança Estratégica. Lê direto do AppDbContext (como o
/// AcessoModuloService) e grava o papel SEMPRE pelo
/// <see cref="IAcessoModuloService.DefinirPapelPlanejamentoAsync"/>, que junta papel,
/// concessão do módulo, histórico e encerramento do pedido pendente numa gravação só.
/// O órgão de cada pessoa sai do <see cref="IPePermissionService"/>, em lote.
/// </summary>
public class PePessoaService : IPePessoaService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;

    /// <summary>Máximo de pessoas devolvidas pela busca de candidatas.</summary>
    public const int LimiteCandidatas = 20;

    /// <summary>Tamanho mínimo do texto da busca de candidatas.</summary>
    public const int MinimoLetrasBusca = 3;

    private readonly AppDbContext _context;
    private readonly IAcessoModuloService _acessos;
    private readonly IPePermissionService _permissoes;

    public PePessoaService(AppDbContext context, IAcessoModuloService acessos, IPePermissionService permissoes)
    {
        _context = context;
        _acessos = acessos;
        _permissoes = permissoes;
    }

    public PeMeuPapelResponse MeuPapel(PeUserContext ctx) => new()
    {
        Papel = ctx.Papel,
        EhAdminGeral = ctx.EhAdminGeral,
        OrgaoId = ctx.OrgaoId,
        OrgaoNome = ctx.OrgaoNome,
        OrgaoSigla = ctx.OrgaoSigla,
        UnidadeNome = ctx.UnidadeNome
    };

    public async Task<PagedResponse<PePessoaResponse>> ListarAsync(PePessoasConsulta consulta)
    {
        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        // Teto da página: (page - 1) * pageSize não pode estourar o int
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = _context.PePapeisUsuario.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Filtro))
        {
            // ToLower().Contains() vale no Npgsql e no InMemory (ILike não valeria)
            var filtro = consulta.Filtro.Trim().ToLower();
            query = query.Where(p => p.User!.Nome.ToLower().Contains(filtro) || p.User!.Email.ToLower().Contains(filtro));
        }

        if (!string.IsNullOrWhiteSpace(consulta.Papel))
        {
            // Papel fora do domínio devolve lista vazia, nunca "todos" em silêncio
            var papel = consulta.Papel.Trim();
            query = PapeisPlanejamento.EhValido(papel)
                ? query.Where(p => p.Papel == papel)
                : query.Where(p => false);
        }

        var total = await query.CountAsync();
        var linhas = await query
            .Include(p => p.User).ThenInclude(u => u!.Unidade)
            .OrderBy(p => p.User!.Nome).ThenBy(p => p.User!.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var usuarios = linhas.Select(l => l.User!).ToList();
        var orgaos = await OrgaosAsync(usuarios);
        var admins = await AdminsGeraisAsync(usuarios.Select(u => u.Id));

        var itens = linhas.Select(l => Mapear(l.User!, l, orgaos, admins)).ToList();
        return new PagedResponse<PePessoaResponse>(itens, total, page, pageSize);
    }

    public async Task<List<PeCandidataResponse>> CandidatasAsync(string? filtro)
    {
        var termo = (filtro ?? string.Empty).Trim();
        if (termo.Length < MinimoLetrasBusca) return new List<PeCandidataResponse>();

        var busca = termo.ToLower();
        // Já entrou no SGDP pelo menos uma vez = tem KeycloakId (o pré-cadastro por
        // e-mail da SGDI no PGIA nasce sem ele)
        var usuarios = await _context.Users.AsNoTracking()
            .Include(u => u.Unidade)
            .Where(u => u.KeycloakId != null
                && !_context.PePapeisUsuario.Any(p => p.UserId == u.Id)
                && (u.Nome.ToLower().Contains(busca) || u.Email.ToLower().Contains(busca)))
            .OrderBy(u => u.Nome).ThenBy(u => u.Email)
            .Take(LimiteCandidatas)
            .ToListAsync();

        var orgaos = await OrgaosAsync(usuarios);
        return usuarios.Select(u => new PeCandidataResponse
        {
            UserId = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            UnidadeNome = u.Unidade?.Nome,
            OrgaoSigla = u.Unidade != null && orgaos.TryGetValue(u.Unidade.id, out var orgao) ? orgao.Sigla : null
        }).ToList();
    }

    public async Task<PePessoaResponse> DefinirPapelAsync(PeUserContext autor, Guid userId, string? papel)
    {
        var papelNovo = string.IsNullOrWhiteSpace(papel) ? null : papel.Trim();
        if (papelNovo != null && !PapeisPlanejamento.EhValido(papelNovo))
            throw new ApiException(ErrorCode.PePapelInvalido,
                $"Papel inválido: {papelNovo}. Escolha um dos cinco papéis do módulo.");

        var existe = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!existe)
            throw new ApiException(ErrorCode.PeUsuarioNaoEncontrado, "Pessoa não encontrada. Atualize a lista.");

        // O administrador do módulo não tira nem troca o próprio papel: sem isso a SGDI
        // poderia se trancar fora da tela de pessoas. O admin geral pode tudo
        if (!autor.EhAdminGeral && autor.UserId == userId
            && autor.Papel == PapeisPlanejamento.Admin && papelNovo != PapeisPlanejamento.Admin)
            throw new ApiException(ErrorCode.PeAutoRebaixamento,
                "Você não pode tirar nem trocar o seu próprio papel de administrador do módulo. Se precisar, peça a outro administrador.");

        await _acessos.DefinirPapelPlanejamentoAsync(userId, papelNovo, autor.Email, PeDominios.OrigemPapel.Pessoas);
        return await ObterAsync(userId);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A pessoa como a lista a mostra. Sem papel (acabou de ser retirado), volta com
    /// Papel, ConcedidoEm e ConcedidoPor nulos e os demais campos preenchidos.
    /// </summary>
    private async Task<PePessoaResponse> ObterAsync(Guid userId)
    {
        var user = await _context.Users.AsNoTracking().Include(u => u.Unidade).FirstAsync(u => u.Id == userId);
        var papel = await _context.PePapeisUsuario.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var orgaos = await OrgaosAsync(new[] { user });
        var admins = await AdminsGeraisAsync(new[] { userId });
        return Mapear(user, papel, orgaos, admins);
    }

    private Task<Dictionary<Guid, PeOrgaoResumo>> OrgaosAsync(IEnumerable<User> usuarios) =>
        _permissoes.OrgaosPorUnidadeAsync(usuarios.Where(u => u.Unidade != null).Select(u => u.Unidade!.id));

    /// <summary>
    /// Quem é admin geral pelo retrato do último login (acesso_modulo com origem
    /// keycloak): a tela não tem o token dos outros, como na gestão de acessos.
    /// </summary>
    private async Task<HashSet<Guid>> AdminsGeraisAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();

        var admins = await _context.AcessosModulo.AsNoTracking()
            .Where(a => ids.Contains(a.UserId)
                && a.Modulo == ModulosSgdp.Administracao && a.Origem == OrigemAcesso.Keycloak)
            .Select(a => a.UserId)
            .ToListAsync();
        return admins.ToHashSet();
    }

    private static PePessoaResponse Mapear(User user, PePapelUsuario? papel,
        IReadOnlyDictionary<Guid, PeOrgaoResumo> orgaos, IReadOnlySet<Guid> admins)
    {
        PeOrgaoResumo? orgao = null;
        if (user.Unidade != null) orgaos.TryGetValue(user.Unidade.id, out orgao);

        return new PePessoaResponse
        {
            UserId = user.Id,
            Nome = user.Nome,
            Email = user.Email,
            UnidadeNome = user.Unidade?.Nome,
            OrgaoId = orgao?.Id,
            OrgaoSigla = orgao?.Sigla,
            OrgaoNome = orgao?.Nome,
            Papel = papel?.Papel,
            ConcedidoEm = papel?.ConcedidoEm,
            ConcedidoPor = papel?.ConcedidoPor,
            EhAdminGeral = admins.Contains(user.Id)
        };
    }
}
