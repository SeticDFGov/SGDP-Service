using api.Auth;
using api.Pgia;
using app.Auth;
using app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;
using Models.Acesso;
using Models.Planejamento;
using service.Interface;

namespace Controllers;

/// <summary>
/// MODO LOCAL (execução independente, sem Keycloak) — somente testes.
/// Só responde quando Auth:ModoLocal=true; fora disso, 404 em tudo.
/// O Program.cs recusa a inicialização com a flag ligada fora de Development,
/// então este controller nunca opera em produção.
///
/// Além de emitir o token, provisiona o usuário de teste: papel PGIA, papel do
/// módulo Supervisão Contínua das Contratações, unidade e um Órgão de Teste (com os prazos de
/// adesão, via o serviço real) — para as personas da tela /auth/local entrarem
/// direto na visão de cada papel. Na Governança Estratégica, o papel do módulo vem
/// com a concessão (pelo IAcessoModuloService, como em produção) e as personas de
/// órgão ficam na unidade SES de teste, com o órgão SES.
/// Usa o AppDbContext diretamente de propósito: é ferramenta de teste, e criar
/// camada de serviço própria só para isso espalharia código de teste no módulo.
/// </summary>
[ApiController]
[Route("api/authlocal")]
public class AuthLocalController : ControllerBase
{
    private const string SiglaOrgaoDeTeste = "TESTE";
    private const string NomeUnidadeOrgao = "Órgão de Teste do PGIA";
    private const string NomeOrgaoDeTeste = "Secretaria de Teste do PGIA";
    private const string NomeUnidadeCentral = "Unidade Central de Teste";

    // Órgão das personas de órgão da Governança Estratégica. A unidade imita a que o
    // login do Keycloak cria para um grupo (nome e código iguais à sigla)
    private const string SiglaOrgaoPlanejamento = "SES";
    private const string NomeOrgaoPlanejamento = "Secretaria de Estado de Saúde";

    // Autor das gravações feitas pelo modo local (concessões, papéis e histórico)
    private const string AutorModoLocal = "modo-local";

    private readonly IConfiguration _configuration;
    private readonly AuthSettings _authSettings;
    private readonly AppDbContext _context;
    private readonly IPgiaAdminService _adminService;
    private readonly IAcessoModuloService _acessos;

    public AuthLocalController(
        IConfiguration configuration,
        IOptions<AuthSettings> authSettings,
        AppDbContext context,
        IPgiaAdminService adminService,
        IAcessoModuloService acessos)
    {
        _configuration = configuration;
        _authSettings = authSettings.Value;
        _context = context;
        _adminService = adminService;
        _acessos = acessos;
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

        // Papel do módulo Supervisão Contínua das Contratações (ctr_analise). Mesma semântica
        // do PapelPgia: ausente/null não mexe no papel já existente.
        public string? PapelContratacoes { get; set; }

        // Módulos concedidos no sistema (ModulosSgdp.Concedidos: demandas, pgia,
        // planejamento), como a tela de gestão de acessos faria. Ausente/null não mexe
        // nas concessões atuais. "planejamento" sem PapelPlanejamento só vale para quem
        // já tem papel no módulo (o acesso anda junto com o papel).
        public List<string>? ConcederModulos { get; set; }

        // Papel na Governança Estratégica (PapeisPlanejamento). Grava o papel e a
        // concessão do módulo juntos, com histórico (origem modo_local). Ausente/null não
        // mexe no papel existente. Papel de órgão (pe_orgao, pe_orgao_consulta) põe a
        // pessoa na unidade SES de teste, com o órgão SES, salvo quando ela já vai para o
        // Órgão de Teste do PGIA; papel global usa a unidade central de teste.
        public string? PapelPlanejamento { get; set; }
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

        if (!PapeisContratacoes.EhValido(request.PapelContratacoes))
            return BadRequest("Papel de supervisão contínua das contratações inválido (use ctr_analise ou deixe vazio).");

        if (request.ConcederModulos != null && request.ConcederModulos.Any(m => !ModulosSgdp.Concedidos.Contains(m)))
            return BadRequest("Módulo concedível inválido (use demandas, pgia ou planejamento).");

        if (request.PapelPlanejamento != null && !PapeisPlanejamento.EhValido(request.PapelPlanejamento))
            return BadRequest("Papel na Governança Estratégica inválido (pe_admin, pe_sgdi, pe_cgtic, pe_orgao ou pe_orgao_consulta).");

        // Na Governança Estratégica não existe acesso sem papel
        if (request.ConcederModulos?.Contains(ModulosSgdp.Planejamento) == true && request.PapelPlanejamento == null
            && !await JaTemPapelPlanejamentoAsync(request.Email.Trim()))
            return BadRequest("Para conceder a Governança Estratégica, informe também o PapelPlanejamento: o acesso ao módulo vem junto com o papel.");

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
        // Papel de órgão na Governança Estratégica: a pessoa fica num órgão (SES de
        // teste), a não ser que a persona já vá para o Órgão de Teste do PGIA
        var precisaOrgaoPlanejamento = !precisaOrgao && PapeisPlanejamento.EhDeOrgao(request.PapelPlanejamento);
        // O userConfiguredGuard do front exige unidade: a persona de contratações e a
        // de papel global na Governança Estratégica também precisam de uma (a central).
        var precisaUnidade = precisaOrgao || request.PapelPgia != null || request.PapelContratacoes != null
            || request.PapelPlanejamento != null;

        Unidade? unidade = null;
        if (precisaOrgao)
            unidade = await GarantirOrgaoDeTesteAsync(email, SiglaOrgaoDeTeste, NomeUnidadeOrgao, NomeOrgaoDeTeste);
        else if (precisaOrgaoPlanejamento)
            unidade = await GarantirOrgaoDeTesteAsync(email, SiglaOrgaoPlanejamento, SiglaOrgaoPlanejamento, NomeOrgaoPlanejamento);
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
        if (request.PapelContratacoes != null) user.PapelContratacoes = request.PapelContratacoes;
        if (unidade != null) user.Unidade = unidade;

        await _context.SaveChangesAsync();

        // Concessões do sistema pedidas pela persona (a mesma linha que a tela de
        // gestão de acessos grava). A da Governança Estratégica anda com o papel e
        // é gravada logo abaixo
        if (request.ConcederModulos != null)
        {
            var existentes = await _context.AcessosModulo
                .Where(a => a.UserId == user.Id && a.Origem == OrigemAcesso.Sistema)
                .Select(a => a.Modulo)
                .ToListAsync();

            foreach (var modulo in request.ConcederModulos.Distinct()
                         .Where(m => m != ModulosSgdp.Planejamento && !existentes.Contains(m)))
            {
                _context.AcessosModulo.Add(new AcessoModulo
                {
                    UserId = user.Id,
                    Modulo = modulo,
                    Origem = OrigemAcesso.Sistema,
                    ConcedidoEm = DateTime.UtcNow,
                    ConcedidoPor = AutorModoLocal
                });
            }
            await _context.SaveChangesAsync();
        }

        // Governança Estratégica: papel e concessão juntos, com histórico, pelo mesmo
        // serviço que as telas usam
        if (request.PapelPlanejamento != null)
            await _acessos.DefinirPapelPlanejamentoAsync(user.Id, request.PapelPlanejamento,
                AutorModoLocal, PeDominios.OrigemPapel.ModoLocal);
    }

    /// <summary>
    /// Pessoa de teste que já tem papel na Governança Estratégica (mesma busca do
    /// provisionamento: sub determinístico, depois e-mail). Só é consultada quando a
    /// persona pede o módulo sem informar o papel.
    /// </summary>
    private async Task<bool> JaTemPapelPlanejamentoAsync(string email)
    {
        var keycloakId = ModoLocalTokens.SubjectDoEmail(email).ToString();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId)
            ?? await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null && await _context.PePapeisUsuario.AnyAsync(p => p.UserId == user.Id);
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
    /// Devolve a unidade que mapeia o usuário a esse órgão. Serve ao Órgão de Teste do
    /// PGIA (sigla TESTE) e ao órgão das personas de órgão da Governança Estratégica
    /// (sigla SES): os dois módulos usam os mesmos órgãos.
    /// </summary>
    private async Task<Unidade> GarantirOrgaoDeTesteAsync(string email, string sigla, string nomeUnidade, string nomeOrgao)
    {
        // Se o órgão de teste já existe e tem unidade, ela é a fonte da verdade
        var orgao = await _context.PgiaOrgaos
            .FirstOrDefaultAsync(o => o.Ativo && o.Sigla == sigla);

        if (orgao?.UnidadeId != null)
            return (await _context.Unidades.FirstAsync(u => u.id == orgao.UnidadeId));

        var unidade = await GarantirUnidadeAsync(nomeUnidade, codigoExterno: sigla);

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
            // A sigla nasce sozinha a partir do CodigoExterno da unidade (a sigla pedida).
            await _adminService.CriarOrgaoAsync(new PgiaOrgaoCreateDTO
            {
                Nome = nomeOrgao,
                NaturezaJuridica = "Administração direta",
                UnidadeId = unidade.id
            }, email);

        return unidade;
    }
}
