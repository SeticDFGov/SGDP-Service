using System.Security.Claims;
using api.Acesso;
using api.Common;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Acesso;
using Models.Planejamento;
using service.Interface;

namespace service.Acesso;

/// <summary>
/// Acesso aos módulos do SGDP. Fonte única da regra do acesso efetivo
/// (<see cref="CalcularModulos"/>), usada pela claims transformation de toda
/// requisição, pelo GET /api/Auth/me e pela tela de gestão de acessos.
/// Escreve direto no AppDbContext (mesmo padrão do PgiaAdminService/CtrAdminService):
/// o AuthRepositorio e o GetOrCreateUserAsync não são tocados.
///
/// Governança Estratégica: o acesso ao módulo (linha de acesso_modulo) e o papel do
/// módulo (pe_papel_usuario, com o histórico) são gravados e retirados juntos, sempre
/// por aqui. Regra do deploy: o caminho de cada requisição (CalcularModulos,
/// ModulosDoPrincipalAsync, ModulosDoUsuarioAsync) nunca lê tabela pe_; as telas só
/// leem o papel de quem tem a concessão do módulo, que não existe antes da migration.
/// </summary>
public class AcessoModuloService : IAcessoModuloService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;

    /// <summary>Filtro da listagem: usuários sem acesso a módulo algum.</summary>
    public const string FiltroSemAcesso = "nenhum";

    private readonly AppDbContext _context;

    public AcessoModuloService(AppDbContext context)
    {
        _context = context;
    }

    // ── Regra do acesso efetivo ───────────────────────────────────────────────

    /// <summary>
    /// Regra única do acesso a módulos. Soma o que vem do Keycloak (roles admin,
    /// gestor e "pgia" do grupo) ao que o sistema concede (linhas de acesso_modulo
    /// com origem "sistema") e aos papéis de módulo (PapelPgia, PapelContratacoes).
    /// Sem nenhuma dessas fontes, a lista é vazia: o usuário não enxerga módulo algum.
    /// A Governança Estratégica vem só da concessão: o papel do módulo não entra aqui.
    /// </summary>
    public static List<string> CalcularModulos(
        bool admin, bool gestor, bool rolePgia,
        string? papelPgia, string? papelContratacoes,
        IEnumerable<string> concedidos)
    {
        // Admin do SGDP: todos os módulos, inclusive a Administração
        if (admin) return ModulosSgdp.Todos.ToList();

        var concessoes = concedidos as ICollection<string> ?? concedidos.ToList();
        var modulos = new List<string>();

        if (gestor || concessoes.Contains(ModulosSgdp.Demandas))
            modulos.Add(ModulosSgdp.Demandas);

        // Papel PGIA (só os valores do domínio) também é porta de entrada do módulo
        if (rolePgia || PapeisPgia.Todos.Contains(papelPgia) || concessoes.Contains(ModulosSgdp.Pgia))
            modulos.Add(ModulosSgdp.Pgia);

        if (papelContratacoes == PapeisContratacoes.Analise)
            modulos.Add(ModulosSgdp.Contratacoes);

        // A concessão é gravada junto com o papel do módulo; ler o papel aqui quebraria
        // o login no intervalo entre publicar o código e rodar a migration
        if (concessoes.Contains(ModulosSgdp.Planejamento))
            modulos.Add(ModulosSgdp.Planejamento);

        return modulos;
    }

    /// <summary>
    /// Módulos que as roles do token abrem por si sós — é o retrato gravado no login
    /// (origem "keycloak"): admin → Administração, gestor → Demandas, pgia → PGIA.
    /// </summary>
    public static HashSet<string> RetratoDasRoles(ClaimsPrincipal principal)
    {
        var retrato = new HashSet<string>();
        if (principal.IsInRole(Perfis.Admin)) retrato.Add(ModulosSgdp.Administracao);
        if (principal.IsInRole(Perfis.Gestor)) retrato.Add(ModulosSgdp.Demandas);
        if (principal.IsInRole(ModulosSgdp.RolePgia)) retrato.Add(ModulosSgdp.Pgia);
        return retrato;
    }

    public async Task<List<string>> ModulosDoPrincipalAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;

        string? papelPgia = null;
        string? papelContratacoes = null;
        List<string> concedidos = new();

        if (keycloakId != null || email != null)
        {
            // Mesma ordem de busca do GetOrCreateUserAsync: primeiro o KeycloakId, depois
            // o e-mail (pré-cadastro da SGDI ainda sem KeycloakId). Uma consulta só.
            var candidatos = await _context.Users
                .Where(u => (keycloakId != null && u.KeycloakId == keycloakId)
                    || (email != null && u.Email == email))
                .Select(u => new
                {
                    u.KeycloakId,
                    u.PapelPgia,
                    u.PapelContratacoes,
                    Concedidos = _context.AcessosModulo
                        .Where(a => a.UserId == u.Id && a.Origem == OrigemAcesso.Sistema)
                        .Select(a => a.Modulo)
                        .ToList()
                })
                .ToListAsync();

            var dados = candidatos.FirstOrDefault(c => keycloakId != null && c.KeycloakId == keycloakId)
                ?? candidatos.FirstOrDefault();

            if (dados != null)
            {
                papelPgia = dados.PapelPgia;
                papelContratacoes = dados.PapelContratacoes;
                concedidos = dados.Concedidos;
            }
        }

        return CalcularModulos(
            principal.IsInRole(Perfis.Admin),
            principal.IsInRole(Perfis.Gestor),
            principal.IsInRole(ModulosSgdp.RolePgia),
            papelPgia,
            papelContratacoes,
            concedidos);
    }

    public async Task<List<string>> ModulosDoUsuarioAsync(ClaimsPrincipal principal, User user)
    {
        var concedidos = await _context.AcessosModulo
            .Where(a => a.UserId == user.Id && a.Origem == OrigemAcesso.Sistema)
            .Select(a => a.Modulo)
            .ToListAsync();

        return CalcularModulos(
            principal.IsInRole(Perfis.Admin),
            principal.IsInRole(Perfis.Gestor),
            principal.IsInRole(ModulosSgdp.RolePgia),
            user.PapelPgia,
            user.PapelContratacoes,
            concedidos);
    }

    // ── Retrato do Keycloak (informativo) ─────────────────────────────────────

    public async Task SincronizarRetratoKeycloakAsync(Guid userId, ClaimsPrincipal principal)
    {
        var esperado = RetratoDasRoles(principal);

        var atuais = await _context.AcessosModulo
            .Where(a => a.UserId == userId && a.Origem == OrigemAcesso.Keycloak)
            .ToListAsync();

        var mudou = false;
        foreach (var obsoleto in atuais.Where(a => !esperado.Contains(a.Modulo)))
        {
            _context.AcessosModulo.Remove(obsoleto);
            mudou = true;
        }

        foreach (var modulo in esperado.Where(m => atuais.All(a => a.Modulo != m)))
        {
            _context.AcessosModulo.Add(new AcessoModulo
            {
                UserId = userId,
                Modulo = modulo,
                Origem = OrigemAcesso.Keycloak,
                ConcedidoEm = DateTime.UtcNow,
                ConcedidoPor = OrigemAcesso.Keycloak
            });
            mudou = true;
        }

        if (!mudou) return;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dois logins simultâneos do mesmo usuário gravando o mesmo retrato: o
            // índice único recusa o segundo. O retrato é só informativo — descarta e
            // segue, sem derrubar o /me.
            foreach (var entrada in _context.ChangeTracker.Entries<AcessoModulo>().ToList())
                entrada.State = EntityState.Detached;
        }
    }

    // ── Tela de gestão de acessos ─────────────────────────────────────────────

    public async Task<PagedResponse<UsuarioAcessoResponse>> ListarUsuariosAsync(AcessoUsuariosConsulta consulta)
    {
        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        // Teto da página: (page - 1) * pageSize não pode estourar o int
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = _context.Users.Include(u => u.Unidade).AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Filtro))
        {
            // ToLower().Contains() vale no Npgsql e no InMemory (ILike não valeria)
            var filtro = consulta.Filtro.Trim().ToLower();
            query = query.Where(u => u.Nome.ToLower().Contains(filtro) || u.Email.ToLower().Contains(filtro));
        }

        query = FiltrarPorModulo(query, consulta.Modulo);

        var total = await query.CountAsync();
        var usuarios = await query
            .OrderBy(u => u.Nome).ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Os acessos da página inteira numa consulta só (sem uma ida ao banco por pessoa)
        var ids = usuarios.Select(u => u.Id).ToList();
        var acessos = await _context.AcessosModulo
            .Where(a => ids.Contains(a.UserId))
            .ToListAsync();
        var porUsuario = acessos.ToLookup(a => a.UserId);
        var papeisPlanejamento = await PapeisPlanejamentoAsync(acessos);

        var itens = usuarios
            .Select(u => Mapear(u, porUsuario[u.Id], papeisPlanejamento.GetValueOrDefault(u.Id)))
            .ToList();
        return new PagedResponse<UsuarioAcessoResponse>(itens, total, page, pageSize);
    }

    /// <summary>
    /// Filtro por módulo como predicado EF (a tabela nunca é materializada para
    /// filtrar). Usa o retrato do Keycloak, então reflete o último login de cada um.
    /// Valor fora do domínio devolve lista vazia — filtro que o servidor não entende
    /// não pode virar "todos" em silêncio.
    /// </summary>
    private IQueryable<User> FiltrarPorModulo(IQueryable<User> query, string? modulo)
    {
        if (string.IsNullOrWhiteSpace(modulo)) return query;

        var acessos = _context.AcessosModulo;
        const string admin = ModulosSgdp.Administracao;
        const string keycloak = OrigemAcesso.Keycloak;
        const string sistema = OrigemAcesso.Sistema;
        const string analise = PapeisContratacoes.Analise;

        return modulo switch
        {
            ModulosSgdp.Administracao => query.Where(u =>
                acessos.Any(a => a.UserId == u.Id && a.Modulo == admin && a.Origem == keycloak)),

            ModulosSgdp.Demandas => query.Where(u =>
                acessos.Any(a => a.UserId == u.Id
                    && (a.Modulo == ModulosSgdp.Demandas || (a.Modulo == admin && a.Origem == keycloak)))),

            ModulosSgdp.Pgia => query.Where(u =>
                u.PapelPgia != null
                || acessos.Any(a => a.UserId == u.Id
                    && (a.Modulo == ModulosSgdp.Pgia || (a.Modulo == admin && a.Origem == keycloak)))),

            ModulosSgdp.Contratacoes => query.Where(u =>
                u.PapelContratacoes == analise
                || acessos.Any(a => a.UserId == u.Id && a.Modulo == admin && a.Origem == keycloak)),

            // Só a concessão (que anda com o papel) ou o admin do Keycloak: nenhuma role
            // do Keycloak abre este módulo, e o filtro não lê a tabela do papel
            ModulosSgdp.Planejamento => query.Where(u =>
                acessos.Any(a => a.UserId == u.Id
                    && ((a.Modulo == ModulosSgdp.Planejamento && a.Origem == sistema)
                        || (a.Modulo == admin && a.Origem == keycloak)))),

            FiltroSemAcesso => query.Where(u =>
                u.PapelPgia == null
                && (u.PapelContratacoes == null || u.PapelContratacoes != analise)
                && !acessos.Any(a => a.UserId == u.Id)),

            _ => query.Where(u => false)
        };
    }

    public async Task<UsuarioAcessoResponse> ObterUsuarioAsync(Guid userId)
    {
        var user = await _context.Users.Include(u => u.Unidade).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado, "Usuário não encontrado.");

        var acessos = await _context.AcessosModulo.Where(a => a.UserId == userId).ToListAsync();
        var papeisPlanejamento = await PapeisPlanejamentoAsync(acessos);
        return Mapear(user, acessos, papeisPlanejamento.GetValueOrDefault(userId));
    }

    public async Task<UsuarioAcessoResponse> DefinirAcessosAsync(Guid userId, AcessoUsuarioUpdateDTO dto, string autorEmail)
    {
        if (!PapeisPgia.EhValido(dto.PapelPgia))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Papel no PGIA inválido: {dto.PapelPgia}");

        if (dto.PapelPgia != null && !dto.Pgia)
            throw new ApiException(ErrorCode.AcessoInvalido,
                "Para definir o papel no PGIA, libere também o acesso ao módulo PGIA.");

        // Governança Estratégica: os dois campos são opcionais. Planejamento nulo (front
        // antigo, que não conhece o módulo) não muda nada, nem o acesso nem o papel
        var papelPlanejamento = string.IsNullOrWhiteSpace(dto.PapelPlanejamento) ? null : dto.PapelPlanejamento.Trim();
        if (papelPlanejamento != null && !PapeisPlanejamento.EhValido(papelPlanejamento))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Papel na Governança Estratégica inválido: {papelPlanejamento}");

        if (papelPlanejamento != null && dto.Planejamento != true)
            throw new ApiException(ErrorCode.AcessoInvalido,
                "Para definir o papel na Governança Estratégica, libere também o acesso ao módulo.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado, "Usuário não encontrado.");

        // O papel em vigor só é lido quando a tela mexe no módulo; ligar o módulo sem
        // papel novo mantém o que a pessoa já tem, e sem papel algum não liga
        var papelAtual = dto.Planejamento == null
            ? null
            : await _context.PePapeisUsuario.FirstOrDefaultAsync(p => p.UserId == userId);
        var papelFinal = papelPlanejamento ?? papelAtual?.Papel;
        if (dto.Planejamento == true && !PapeisPlanejamento.EhValido(papelFinal))
            throw new ApiException(ErrorCode.AcessoInvalido,
                "Escolha o papel da pessoa na Governança Estratégica: o acesso ao módulo vem junto com o papel.");

        var concedidos = await _context.AcessosModulo
            .Where(a => a.UserId == userId && a.Origem == OrigemAcesso.Sistema)
            .ToListAsync();

        AplicarConcessao(userId, ModulosSgdp.Demandas, dto.Demandas, autorEmail, concedidos);
        AplicarConcessao(userId, ModulosSgdp.Pgia, dto.Pgia, autorEmail, concedidos);

        // Tirar o PGIA tira também o papel dentro dele; o Perfil do SGDP nunca muda
        user.PapelPgia = dto.Pgia ? dto.PapelPgia : null;
        user.PapelContratacoes = dto.Contratacoes ? PapeisContratacoes.Analise : null;

        // Governança Estratégica: acesso e papel juntos (false tira os dois)
        if (dto.Planejamento != null)
            PrepararPapelPlanejamento(userId, papelAtual, dto.Planejamento.Value ? papelFinal : null,
                autorEmail, PeDominios.OrigemPapel.GestaoAcessos, null, concedidos);

        // Quem pediu um dos módulos liberados agora sai da fila de pedidos
        var liberados = new List<string>();
        if (dto.Demandas) liberados.Add(ModulosSgdp.Demandas);
        if (dto.Pgia) liberados.Add(ModulosSgdp.Pgia);
        if (dto.Contratacoes) liberados.Add(ModulosSgdp.Contratacoes);
        if (dto.Planejamento == true) liberados.Add(ModulosSgdp.Planejamento);
        await MarcarPedidosAtendidosAsync(userId, liberados, autorEmail, user.PapelPgia);

        await SalvarAceitandoPedidoJaDecididoAsync();
        return await ObterUsuarioAsync(userId);
    }

    public async Task DefinirConcessaoAsync(Guid userId, string modulo, bool ativo, string autorEmail)
    {
        if (!ModulosSgdp.Concedidos.Contains(modulo))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Módulo não concedível pela tela: {modulo}");

        // Na Governança Estratégica não existe concessão sem papel: o acesso é gravado
        // junto com o papel do módulo (DefinirPapelPlanejamentoAsync)
        if (modulo == ModulosSgdp.Planejamento)
            throw new ApiException(ErrorCode.AcessoInvalido,
                "O acesso à Governança Estratégica é dado junto com o papel do módulo.");

        var existe = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!existe)
            throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado, "Usuário não encontrado.");

        var concedidos = await _context.AcessosModulo
            .Where(a => a.UserId == userId && a.Origem == OrigemAcesso.Sistema && a.Modulo == modulo)
            .ToListAsync();

        AplicarConcessao(userId, modulo, ativo, autorEmail, concedidos);
        // Liberar pela SGDI ("Liberar como agente") também atende o pedido pendente
        if (ativo) await MarcarPedidosAtendidosAsync(userId, new List<string> { modulo }, autorEmail, null);
        await SalvarAceitandoPedidoJaDecididoAsync();
    }

    public async Task PrepararLiberacaoAsync(Guid userId, string modulo, string? papelPgia, string autorEmail,
        string? papelPlanejamento, long? pedidoAcessoId)
    {
        if (!ModulosSgdp.Liberaveis.Contains(modulo))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Módulo não liberado pelo sistema: {modulo}");

        if (!PapeisPgia.EhValido(papelPgia))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Papel no PGIA inválido: {papelPgia}");

        if (papelPgia != null && modulo != ModulosSgdp.Pgia)
            throw new ApiException(ErrorCode.AcessoInvalido, "O papel no PGIA só vale para o módulo PGIA.");

        // Governança Estratégica: o acesso vem junto com o papel do módulo, obrigatório
        if (modulo == ModulosSgdp.Planejamento && !PapeisPlanejamento.EhValido(papelPlanejamento))
            throw new ApiException(ErrorCode.AcessoInvalido,
                "Escolha o papel da pessoa na Governança Estratégica: o acesso ao módulo vem junto com o papel.");

        if (papelPlanejamento != null && modulo != ModulosSgdp.Planejamento)
            throw new ApiException(ErrorCode.AcessoInvalido,
                "O papel na Governança Estratégica só vale para esse módulo.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado, "Usuário não encontrado.");

        // Supervisão Contínua: o acesso é o próprio papel do módulo (não há linha de concessão)
        if (modulo == ModulosSgdp.Contratacoes)
        {
            user.PapelContratacoes = PapeisContratacoes.Analise;
            return;
        }

        var concedidos = await _context.AcessosModulo
            .Where(a => a.UserId == userId && a.Origem == OrigemAcesso.Sistema && a.Modulo == modulo)
            .ToListAsync();

        if (modulo == ModulosSgdp.Planejamento)
        {
            var atual = await _context.PePapeisUsuario.FirstOrDefaultAsync(p => p.UserId == userId);
            PrepararPapelPlanejamento(userId, atual, papelPlanejamento, autorEmail,
                PeDominios.OrigemPapel.Pedido, pedidoAcessoId, concedidos);
            return;
        }

        AplicarConcessao(userId, modulo, true, autorEmail, concedidos);

        // Liberar como agente (sem papel) não tira um papel que a pessoa já tenha
        if (papelPgia != null) user.PapelPgia = papelPgia;
    }

    public async Task DefinirPapelPlanejamentoAsync(Guid userId, string? papel, string autorEmail, string origem)
    {
        if (papel != null && !PapeisPlanejamento.EhValido(papel))
            throw new ApiException(ErrorCode.AcessoInvalido, $"Papel na Governança Estratégica inválido: {papel}");

        if (!PeDominios.OrigemPapel.Todas.Contains(origem))
            throw new ArgumentException($"Origem de papel desconhecida: {origem}", nameof(origem));

        var existe = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!existe)
            throw new ApiException(ErrorCode.AcessoUsuarioNaoEncontrado, "Usuário não encontrado.");

        var concedidos = await _context.AcessosModulo
            .Where(a => a.UserId == userId && a.Origem == OrigemAcesso.Sistema && a.Modulo == ModulosSgdp.Planejamento)
            .ToListAsync();
        var atual = await _context.PePapeisUsuario.FirstOrDefaultAsync(p => p.UserId == userId);

        PrepararPapelPlanejamento(userId, atual, papel, autorEmail, origem, null, concedidos);

        // Deu o papel (e com ele o acesso): o pedido pendente do módulo sai da fila
        if (papel != null)
            await MarcarPedidosAtendidosAsync(userId, new List<string> { ModulosSgdp.Planejamento }, autorEmail, null);

        await SalvarAceitandoPedidoJaDecididoAsync();
    }

    public async Task EncerrarPedidosAtendidosAsync(Guid userId, IEnumerable<string> modulos, string? autorEmail, string? papelPgia)
    {
        var lista = modulos.ToList();
        if (lista.Count == 0) return;

        await MarcarPedidosAtendidosAsync(userId, lista, autorEmail, papelPgia);
        await SalvarAceitandoPedidoJaDecididoAsync();
    }

    // ── Governança Estratégica: papel e acesso juntos ─────────────────────────

    /// <summary>
    /// Deixa pronto, SEM gravar, o papel na Governança Estratégica junto com a
    /// concessão do módulo: papel dá o acesso, papel nulo tira os dois. Toda mudança de
    /// papel entra no histórico; papel igual ao atual não grava nada (nem histórico).
    /// </summary>
    private void PrepararPapelPlanejamento(Guid userId, PePapelUsuario? atual, string? papel,
        string autorEmail, string origem, long? pedidoAcessoId, List<AcessoModulo> concedidos)
    {
        var agora = DateTime.UtcNow;
        var anterior = atual?.Papel;

        if (papel == null)
        {
            if (atual != null) _context.PePapeisUsuario.Remove(atual);
        }
        else if (atual == null)
        {
            _context.PePapeisUsuario.Add(new PePapelUsuario
            {
                UserId = userId,
                Papel = papel,
                ConcedidoEm = agora,
                ConcedidoPor = autorEmail
            });
        }
        else if (atual.Papel != papel)
        {
            atual.Papel = papel;
            atual.AlteradoEm = agora;
            atual.AlteradoPor = autorEmail;
        }

        if (anterior != papel)
        {
            _context.PePapeisUsuarioHistorico.Add(new PePapelUsuarioHistorico
            {
                UserId = userId,
                PapelAnterior = anterior,
                PapelNovo = papel,
                Origem = origem,
                PedidoAcessoId = pedidoAcessoId,
                AlteradoEm = agora,
                AlteradoPor = autorEmail
            });
        }

        AplicarConcessao(userId, ModulosSgdp.Planejamento, papel != null, autorEmail, concedidos);
    }

    /// <summary>
    /// Papel na Governança Estratégica de quem tem a concessão do módulo, em lote (uma
    /// consulta por página). Só consulta pe_papel_usuario quando alguém da página tem a
    /// concessão: antes da migration do módulo o CHECK antigo de acesso_modulo não deixa
    /// existir essa linha, e a gestão de acessos segue de pé no intervalo do deploy.
    /// </summary>
    private async Task<Dictionary<Guid, string>> PapeisPlanejamentoAsync(IEnumerable<AcessoModulo> acessos)
    {
        var ids = acessos
            .Where(a => a.Modulo == ModulosSgdp.Planejamento && a.Origem == OrigemAcesso.Sistema)
            .Select(a => a.UserId)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _context.PePapeisUsuario.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.Papel);
    }

    // ── Pedidos de acesso atendidos por outro caminho ─────────────────────────

    /// <summary>
    /// Marca como aprovados os pedidos pendentes destes módulos, sem gravar. O papel
    /// só é anotado no pedido do PGIA (o da Governança Estratégica fica no histórico
    /// de papéis do módulo).
    /// </summary>
    private async Task MarcarPedidosAtendidosAsync(Guid userId, List<string> modulos, string? autorEmail, string? papelPgia)
    {
        if (modulos.Count == 0) return;

        var pendentes = await _context.PedidosAcesso
            .Where(p => p.UserId == userId && p.Situacao == SituacaoPedidoAcesso.Pendente && modulos.Contains(p.Modulo))
            .ToListAsync();

        var agora = DateTime.UtcNow;
        // A consulta devolve a instância já rastreada: um pedido que esta mesma
        // requisição acabou de decidir não é mexido de novo
        foreach (var pedido in pendentes.Where(p => p.Situacao == SituacaoPedidoAcesso.Pendente))
        {
            pedido.Situacao = SituacaoPedidoAcesso.Aprovado;
            pedido.DecididoEm = agora;
            pedido.DecididoPor = autorEmail;
            pedido.PapelPgia = pedido.Modulo == ModulosSgdp.Pgia ? papelPgia : null;
        }
    }

    /// <summary>
    /// Grava tolerando um pedido que outra pessoa decidiu no mesmo instante (o token
    /// de concorrência da situação recusa a segunda gravação): ele já saiu da fila,
    /// então basta deixá-lo como está e gravar o resto, que é o acesso em si.
    /// </summary>
    private async Task SalvarAceitandoPedidoJaDecididoAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.All(e => e.Entity is PedidoAcesso))
        {
            foreach (var entrada in ex.Entries)
                entrada.State = EntityState.Detached;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<Guid, (bool Concedido, bool Keycloak)>> ResumoDoModuloAsync(
        string modulo, IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        // O retrato de admin também conta como acesso vindo do Keycloak: o perfil
        // admin abre todos os módulos
        var linhas = await _context.AcessosModulo
            .Where(a => ids.Contains(a.UserId)
                && (a.Modulo == modulo || (a.Modulo == ModulosSgdp.Administracao && a.Origem == OrigemAcesso.Keycloak)))
            .Select(a => new { a.UserId, a.Modulo, a.Origem })
            .ToListAsync();

        return ids.ToDictionary(
            id => id,
            id => (linhas.Any(l => l.UserId == id && l.Origem == OrigemAcesso.Sistema && l.Modulo == modulo),
                   linhas.Any(l => l.UserId == id && l.Origem == OrigemAcesso.Keycloak)));
    }

    private void AplicarConcessao(Guid userId, string modulo, bool ativo, string autorEmail, List<AcessoModulo> concedidos)
    {
        var existente = concedidos.FirstOrDefault(a => a.Modulo == modulo && a.Origem == OrigemAcesso.Sistema);

        if (ativo && existente == null)
        {
            _context.AcessosModulo.Add(new AcessoModulo
            {
                UserId = userId,
                Modulo = modulo,
                Origem = OrigemAcesso.Sistema,
                ConcedidoEm = DateTime.UtcNow,
                ConcedidoPor = autorEmail
            });
        }
        else if (!ativo && existente != null)
        {
            _context.AcessosModulo.Remove(existente);
        }
    }

    private static UsuarioAcessoResponse Mapear(User user, IEnumerable<AcessoModulo> acessos, string? papelPlanejamento)
    {
        var lista = acessos.ToList();
        var concedidos = lista.Where(a => a.Origem == OrigemAcesso.Sistema).ToList();
        var retrato = lista.Where(a => a.Origem == OrigemAcesso.Keycloak).ToList();

        return new UsuarioAcessoResponse
        {
            Id = user.Id,
            Nome = user.Nome,
            Email = user.Email,
            UnidadeId = user.Unidade?.id,
            UnidadeNome = user.Unidade?.Nome,
            PapelPgia = user.PapelPgia,
            PapelContratacoes = user.PapelContratacoes,
            PapelPlanejamento = papelPlanejamento,
            Concedidos = concedidos.OrderBy(a => a.Modulo).Select(Mapear).ToList(),
            Keycloak = retrato.OrderBy(a => a.Modulo).Select(Mapear).ToList(),
            // Para os outros usuários a tela não tem o token: o retrato do último
            // login faz o papel das roles
            Modulos = CalcularModulos(
                retrato.Any(a => a.Modulo == ModulosSgdp.Administracao),
                retrato.Any(a => a.Modulo == ModulosSgdp.Demandas),
                retrato.Any(a => a.Modulo == ModulosSgdp.Pgia),
                user.PapelPgia,
                user.PapelContratacoes,
                concedidos.Select(a => a.Modulo))
        };
    }

    private static AcessoModuloResponse Mapear(AcessoModulo a) => new()
    {
        Modulo = a.Modulo,
        ConcedidoEm = a.ConcedidoEm,
        ConcedidoPor = a.ConcedidoPor
    };
}
