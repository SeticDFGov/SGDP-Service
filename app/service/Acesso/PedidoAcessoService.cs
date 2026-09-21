using System.Security.Claims;
using api.Acesso;
using api.Common;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Acesso;
using service.Interface;

namespace service.Acesso;

/// <summary>
/// Pedidos de acesso aos módulos (tabela pedido_acesso). Escreve direto no
/// AppDbContext, como o AcessoModuloService, e libera o módulo pelo
/// <see cref="IAcessoModuloService.PrepararLiberacaoAsync"/>: aprovar um pedido
/// grava exatamente o que a tela de gestão de acessos gravaria.
/// </summary>
public class PedidoAcessoService : IPedidoAcessoService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;
    private const int TamanhoMaximoTexto = 500;

    private readonly AppDbContext _context;
    private readonly IAcessoModuloService _acessos;

    public PedidoAcessoService(AppDbContext context, IAcessoModuloService acessos)
    {
        _context = context;
        _acessos = acessos;
    }

    // ── Quem pede ─────────────────────────────────────────────────────────────

    public async Task<MeuPedidoAcessoResponse> CriarAsync(ClaimsPrincipal principal, PedidoAcessoCreateDTO dto)
    {
        var modulo = (dto.Modulo ?? string.Empty).Trim();

        if (modulo == ModulosSgdp.Administracao)
            throw new ApiException(ErrorCode.PedidoAcessoInvalido,
                "A Administração é liberada só pelo perfil admin do Keycloak. Fale com os administradores do sistema.");

        if (!ModulosSgdp.Liberaveis.Contains(modulo))
            throw new ApiException(ErrorCode.PedidoAcessoInvalido, $"Módulo inválido: {modulo}");

        var justificativa = string.IsNullOrWhiteSpace(dto.Justificativa) ? null : dto.Justificativa.Trim();
        if (justificativa?.Length > TamanhoMaximoTexto)
            throw new ApiException(ErrorCode.PedidoAcessoInvalido,
                $"A justificativa pode ter até {TamanhoMaximoTexto} caracteres.");

        var user = await UsuarioDoPrincipalAsync(principal)
            ?? throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado,
                "Seu cadastro ainda não foi encontrado. Saia e entre de novo no sistema.");

        var modulos = await _acessos.ModulosDoUsuarioAsync(principal, user);
        if (modulos.Contains(modulo))
            throw new ApiException(ErrorCode.PedidoAcessoInvalido, "Você já tem acesso a este módulo.");

        var pendente = await PendenteAsync(user.Id, modulo);
        if (pendente != null) return MapearMeu(pendente);

        var pedido = new PedidoAcesso
        {
            UserId = user.Id,
            Modulo = modulo,
            Justificativa = justificativa,
            Situacao = SituacaoPedidoAcesso.Pendente,
            CriadoEm = DateTime.UtcNow
        };
        _context.PedidosAcesso.Add(pedido);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dois cliques ao mesmo tempo: o índice único dos pendentes recusa o
            // segundo, e quem pediu recebe o pedido que ficou
            _context.Entry(pedido).State = EntityState.Detached;
            var existente = await PendenteAsync(user.Id, modulo);
            if (existente == null) throw;
            return MapearMeu(existente);
        }

        return MapearMeu(pedido);
    }

    public async Task<List<MeuPedidoAcessoResponse>> MeusPedidosAsync(ClaimsPrincipal principal)
    {
        var user = await UsuarioDoPrincipalAsync(principal);
        if (user == null) return new List<MeuPedidoAcessoResponse>();

        var pedidos = await _context.PedidosAcesso.AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        // O card mostra só o último pedido de cada módulo (o Id é a ordem de chegada)
        return pedidos.GroupBy(p => p.Modulo).Select(g => MapearMeu(g.First())).ToList();
    }

    // ── Quem decide ───────────────────────────────────────────────────────────

    public async Task<DecisorPedidos> DecisorAsync(ClaimsPrincipal principal)
    {
        var email = EmailDo(principal);

        // Admin do SGDP: todos os módulos que o sistema libera
        if (principal.IsInRole(Perfis.Admin))
            return new DecisorPedidos(email ?? string.Empty, ModulosSgdp.Liberaveis);

        // A SGDI cuida das pessoas do PGIA (tela Pessoas e acessos): decide os
        // pedidos desse módulo
        var user = await UsuarioDoPrincipalAsync(principal);
        if (user?.PapelPgia == PapeisPgia.Sgdi)
            return new DecisorPedidos(email ?? user.Email, new[] { ModulosSgdp.Pgia });

        return new DecisorPedidos(email ?? string.Empty, Array.Empty<string>());
    }

    public async Task<PagedResponse<PedidoAcessoResponse>> ListarAsync(PedidosAcessoConsulta consulta, DecisorPedidos decisor)
    {
        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        // Teto da página: (page - 1) * pageSize não pode estourar o int
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = DoEscopo(decisor, consulta.Modulo);

        if (!string.IsNullOrWhiteSpace(consulta.Situacao))
        {
            // Situação fora do domínio devolve lista vazia, nunca "todas" em silêncio
            var situacao = consulta.Situacao.Trim();
            query = SituacaoPedidoAcesso.Todas.Contains(situacao)
                ? query.Where(p => p.Situacao == situacao)
                : query.Where(p => false);
        }

        if (!string.IsNullOrWhiteSpace(consulta.Filtro))
        {
            // ToLower().Contains() vale no Npgsql e no InMemory (ILike não valeria)
            var filtro = consulta.Filtro.Trim().ToLower();
            query = query.Where(p => p.User!.Nome.ToLower().Contains(filtro) || p.User!.Email.ToLower().Contains(filtro));
        }

        var total = await query.CountAsync();

        // Pendentes primeiro, por ordem de chegada (é uma fila); depois os
        // decididos, do mais recente para o mais antigo. O Id é a ordem de chegada.
        const string pendente = SituacaoPedidoAcesso.Pendente;
        var pedidos = await query
            .Include(p => p.User).ThenInclude(u => u!.Unidade)
            .OrderBy(p => p.Situacao == pendente ? 0 : 1)
            .ThenBy(p => p.Situacao == pendente ? p.Id : -p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return new PagedResponse<PedidoAcessoResponse>(pedidos.Select(Mapear).ToList(), total, page, pageSize);
    }

    public async Task<int> ContarPendentesAsync(DecisorPedidos decisor, string? modulo) =>
        await DoEscopo(decisor, modulo).CountAsync(p => p.Situacao == SituacaoPedidoAcesso.Pendente);

    public async Task<PedidoAcessoResponse> AprovarAsync(long pedidoId, PedidoAcessoAprovarDTO dto, DecisorPedidos decisor)
    {
        var pedido = await PedidoParaDecidirAsync(pedidoId, decisor);
        var papel = string.IsNullOrWhiteSpace(dto.PapelPgia) ? null : dto.PapelPgia.Trim();

        // A mesma gravação da tela de gestão de acessos (valida o módulo e o papel)
        await _acessos.PrepararLiberacaoAsync(pedido.UserId, pedido.Modulo, papel, decisor.Email);

        pedido.Situacao = SituacaoPedidoAcesso.Aprovado;
        pedido.DecididoEm = DateTime.UtcNow;
        pedido.DecididoPor = decisor.Email;
        pedido.PapelPgia = papel;

        await SalvarDecisaoAsync();
        return await ObterAsync(pedidoId);
    }

    public async Task<PedidoAcessoResponse> RecusarAsync(long pedidoId, PedidoAcessoRecusarDTO dto, DecisorPedidos decisor)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
            throw new ApiException(ErrorCode.PedidoAcessoInvalido,
                "Escreva o motivo da recusa. A pessoa vai ler no card do módulo.");
        if (motivo.Length > TamanhoMaximoTexto)
            throw new ApiException(ErrorCode.PedidoAcessoInvalido,
                $"O motivo pode ter até {TamanhoMaximoTexto} caracteres.");

        var pedido = await PedidoParaDecidirAsync(pedidoId, decisor);
        pedido.Situacao = SituacaoPedidoAcesso.Recusado;
        pedido.DecididoEm = DateTime.UtcNow;
        pedido.DecididoPor = decisor.Email;
        pedido.MotivoRecusa = motivo;

        await SalvarDecisaoAsync();
        return await ObterAsync(pedidoId);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private async Task<PedidoAcesso> PedidoParaDecidirAsync(long pedidoId, DecisorPedidos decisor)
    {
        var pedido = await _context.PedidosAcesso.FirstOrDefaultAsync(p => p.Id == pedidoId)
            ?? throw new ApiException(ErrorCode.PedidoAcessoNaoEncontrado, "Pedido de acesso não encontrado.");

        if (!decisor.Modulos.Contains(pedido.Modulo))
            throw new ApiException(ErrorCode.PedidoAcessoForaDoEscopo, "Você não decide os pedidos deste módulo.");

        if (pedido.Situacao != SituacaoPedidoAcesso.Pendente)
            throw new ApiException(ErrorCode.PedidoAcessoJaDecidido, "Este pedido já foi decidido. Atualize a lista.");

        return pedido;
    }

    /// <summary>
    /// Grava a decisão. Se outra pessoa decidiu o mesmo pedido no mesmo instante, o
    /// token de concorrência da situação recusa esta gravação inteira (nem o acesso
    /// é liberado) e quem chegou depois é avisado.
    /// </summary>
    private async Task SalvarDecisaoAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            throw new ApiException(ErrorCode.PedidoAcessoJaDecidido,
                "Outra pessoa decidiu este pedido agora há pouco. Atualize a lista.");
        }
    }

    private async Task<PedidoAcessoResponse> ObterAsync(long pedidoId)
    {
        var pedido = await _context.PedidosAcesso.AsNoTracking()
            .Include(p => p.User).ThenInclude(u => u!.Unidade)
            .FirstAsync(p => p.Id == pedidoId);
        return Mapear(pedido);
    }

    /// <summary>
    /// Pedidos dos módulos que quem consulta decide. Módulo pedido fora desse
    /// escopo não devolve nada (nunca "todos" em silêncio).
    /// </summary>
    private IQueryable<PedidoAcesso> DoEscopo(DecisorPedidos decisor, string? modulo)
    {
        var modulos = decisor.Modulos.ToList();
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            var escolhido = modulo.Trim();
            modulos = modulos.Where(m => m == escolhido).ToList();
        }
        return _context.PedidosAcesso.Where(p => modulos.Contains(p.Modulo));
    }

    private Task<PedidoAcesso?> PendenteAsync(Guid userId, string modulo) =>
        _context.PedidosAcesso.FirstOrDefaultAsync(p =>
            p.UserId == userId && p.Modulo == modulo && p.Situacao == SituacaoPedidoAcesso.Pendente);

    /// <summary>Mesma ordem de busca do GetOrCreateUserAsync: primeiro o KeycloakId, depois o e-mail.</summary>
    private async Task<User?> UsuarioDoPrincipalAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = EmailDo(principal);
        if (keycloakId == null && email == null) return null;

        var candidatos = await _context.Users
            .Where(u => (keycloakId != null && u.KeycloakId == keycloakId) || (email != null && u.Email == email))
            .ToListAsync();

        return candidatos.FirstOrDefault(u => keycloakId != null && u.KeycloakId == keycloakId)
            ?? candidatos.FirstOrDefault();
    }

    private static string? EmailDo(ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;

    private static MeuPedidoAcessoResponse MapearMeu(PedidoAcesso p) => new()
    {
        Id = p.Id,
        Modulo = p.Modulo,
        Situacao = p.Situacao,
        CriadoEm = p.CriadoEm,
        DecididoEm = p.DecididoEm,
        MotivoRecusa = p.Situacao == SituacaoPedidoAcesso.Recusado ? p.MotivoRecusa : null
    };

    private static PedidoAcessoResponse Mapear(PedidoAcesso p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        Nome = p.User?.Nome ?? string.Empty,
        Email = p.User?.Email ?? string.Empty,
        UnidadeNome = p.User?.Unidade?.Nome,
        Modulo = p.Modulo,
        Justificativa = p.Justificativa,
        Situacao = p.Situacao,
        CriadoEm = p.CriadoEm,
        DecididoEm = p.DecididoEm,
        DecididoPor = p.DecididoPor,
        PapelPgia = p.PapelPgia,
        MotivoRecusa = p.MotivoRecusa
    };
}
