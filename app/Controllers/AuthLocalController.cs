using api.Auth;
using api.Pgia;
using app.Auth;
using app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;
using service.Interface;

namespace Controllers;

/// <summary>
/// MODO LOCAL (execução independente, sem Keycloak) — somente testes.
/// Só responde quando Auth:ModoLocal=true; fora disso, 404 em tudo.
/// O Program.cs recusa a inicialização com a flag ligada fora de Development,
/// então este controller nunca opera em produção.
///
/// Além de emitir o token, provisiona o usuário de teste: papel PGIA, unidade
/// e um Órgão de Teste (com os prazos de adesão, via o serviço real) — para as
/// personas da tela /auth/local entrarem direto na visão de cada papel.
/// Usa o AppDbContext diretamente de propósito: é ferramenta de teste, e criar
/// camada de serviço própria só para isso espalharia código de teste no módulo.
/// </summary>
[ApiController]
[Route("api/authlocal")]
public class AuthLocalController : ControllerBase
{
    private const string SiglaOrgaoDeTeste = "TESTE";
    private const string NomeUnidadeOrgao = "Órgão de Teste do PGIA";
    private const string NomeUnidadeCentral = "Unidade Central de Teste";

    private readonly IConfiguration _configuration;
    private readonly AuthSettings _authSettings;
    private readonly AppDbContext _context;
    private readonly IPgiaAdminService _adminService;

    public AuthLocalController(
        IConfiguration configuration,
        IOptions<AuthSettings> authSettings,
        AppDbContext context,
        IPgiaAdminService adminService)
    {
        _configuration = configuration;
        _authSettings = authSettings.Value;
        _context = context;
        _adminService = adminService;
    }

    private bool ModoLocalAtivo => _configuration.GetValue<bool>("Auth:ModoLocal");

    public class LoginLocalRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;

        // Papel PGIA a aplicar no usuário de teste. Ausente/null = não mexer no
        // papel já existente (quem manda continua sendo a tela de admin).
        public string? PapelPgia { get; set; }

        // Vincula o usuário à unidade do Órgão de Teste (persona de órgão e
        // persona de agente público do art. 13, que precisa pertencer a um órgão).
        public bool VincularAoOrgaoDeTeste { get; set; }
    }

    /// <summary>
    /// Emite um token local com o mesmo formato dos do Keycloak, para o usuário
    /// de teste informado. O restante do fluxo (/me, perfis, guards) é o real.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] LoginLocalRequest request)
    {
        if (!ModoLocalAtivo) return NotFound();

        var perfis = new[] { Perfis.Admin, Perfis.Gestor, Perfis.Basico };
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nome)
            || !perfis.Contains(request.Perfil))
            return BadRequest("Informe e-mail, nome e um perfil válido (admin, gestor ou basico).");

        if (request.PapelPgia != null && !PapeisPgia.Todos.Contains(request.PapelPgia))
            return BadRequest("Papel PGIA inválido (pgia_orgao, pgia_sgdi, pgia_cgtic ou pgia_auditoria).");

        await ProvisionarUsuarioDeTesteAsync(request);

        // A role "pgia" (portão de entrada do módulo) acompanha qualquer persona
        // que envolva PGIA, igual ao Keycloak real: quem recebe papel PGIA ou é
        // vinculado ao órgão de teste também recebe a role de acesso ao módulo.
        var roles = new List<string> { request.Perfil };
        if (request.PapelPgia != null || request.VincularAoOrgaoDeTeste)
            roles.Add("pgia");

        var validade = TimeSpan.FromHours(8);
        var token = ModoLocalTokens.EmitirToken(
            request.Email.Trim(), request.Nome.Trim(), roles,
            _authSettings.ClientId, validade);

        // Mesmo formato do token do Keycloak que o front já consome
        return Ok(new
        {
            access_token = token,
            token_type = "Bearer",
            expires_in = (int)validade.TotalSeconds,
            refresh_token = token,
            refresh_expires_in = (int)validade.TotalSeconds,
            scope = "modo-local"
        });
    }

    /// <summary>
    /// Deixa o usuário de teste pronto ANTES do /me: papel PGIA e unidade.
    /// O /me (GetOrCreateUserAsync) encontra o usuário pelo mesmo sub
    /// determinístico e preserva Unidade e PapelPgia; o Perfil nunca é
    /// persistido — vem sempre da claim do token, em ambos os fluxos.
    /// </summary>
    private async Task ProvisionarUsuarioDeTesteAsync(LoginLocalRequest request)
    {
        var email = request.Email.Trim();
        var precisaOrgao = request.PapelPgia == PapeisPgia.Orgao || request.VincularAoOrgaoDeTeste;
        var precisaUnidade = precisaOrgao || request.PapelPgia != null;

        Unidade? unidade = null;
        if (precisaOrgao)
            unidade = await GarantirOrgaoDeTesteAsync(email);
        else if (precisaUnidade)
            unidade = await GarantirUnidadeAsync(NomeUnidadeCentral);

        var keycloakId = ModoLocalTokens.SubjectDoEmail(email).ToString();
        var user = await _context.Users.Include(u => u.Unidade)
                .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId)
            ?? await _context.Users.Include(u => u.Unidade)
                .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User { KeycloakId = keycloakId, Nome = request.Nome.Trim(), Email = email };
            _context.Users.Add(user);
        }

        if (request.PapelPgia != null) user.PapelPgia = request.PapelPgia;
        if (unidade != null) user.Unidade = unidade;

        await _context.SaveChangesAsync();
    }

    private async Task<Unidade> GarantirUnidadeAsync(string nome, string? codigoExterno = null)
    {
        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.Nome == nome);
        if (unidade == null)
        {
            unidade = new Unidade { Nome = nome, CodigoExterno = codigoExterno };
            _context.Unidades.Add(unidade);
            await _context.SaveChangesAsync();
        }
        return unidade;
    }

    /// <summary>
    /// Garante um órgão PGIA ativo ligado à unidade de teste, criando-o pelo
    /// serviço real de adesão (que instancia os prazos de conformidade).
    /// Devolve a unidade que mapeia o usuário a esse órgão.
    /// </summary>
    private async Task<Unidade> GarantirOrgaoDeTesteAsync(string email)
    {
        // Se o órgão de teste já existe e tem unidade, ela é a fonte da verdade
        var orgao = await _context.PgiaOrgaos
            .FirstOrDefaultAsync(o => o.Ativo && o.Sigla == SiglaOrgaoDeTeste);

        if (orgao?.UnidadeId != null)
            return (await _context.Unidades.FirstAsync(u => u.id == orgao.UnidadeId));

        var unidade = await GarantirUnidadeAsync(NomeUnidadeOrgao, codigoExterno: SiglaOrgaoDeTeste);

        if (orgao != null)
        {
            // Órgão de teste ficou sem unidade (edição manual): religa
            orgao.UnidadeId = unidade.id;
            await _context.SaveChangesAsync();
            return unidade;
        }

        // A unidade pode já estar ligada a outro órgão ativo (cadastro manual)
        var orgaoDaUnidade = await _context.PgiaOrgaos
            .AnyAsync(o => o.Ativo && o.UnidadeId == unidade.id);
        if (!orgaoDaUnidade)
            // A sigla nasce sozinha a partir do CodigoExterno da unidade (SiglaOrgaoDeTeste).
            await _adminService.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Nome = "Secretaria de Teste do PGIA",
                NaturezaJuridica = "Administração direta",
                UnidadeId = unidade.id
            }, email);

        return unidade;
    }
}
