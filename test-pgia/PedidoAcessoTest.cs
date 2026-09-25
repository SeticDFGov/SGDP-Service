using System.Security.Claims;
using api.Acesso;
using api.Pgia;
using app.Auth;
using app.Models;
using Controllers;
using Controllers.Pgia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;
using Models.Acesso;
using Models.Planejamento;
using Repositorio;
using Repositorio.Pgia;
using service;
using service.Acesso;
using service.Interface;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Pedidos de acesso: a pessoa pede o módulo no card da tela inicial, a
/// administração decide todos e a SGDI decide os do PGIA. Aprovar libera na hora,
/// pelo mesmo caminho da tela de gestão de acessos; recusar exige motivo, que a
/// pessoa lê no card. Liberar o módulo por outro caminho encerra o pedido.
/// </summary>
public class PedidoAcessoTest : PgiaTestBase
{
    private readonly AcessoModuloService _acessos;
    private readonly PedidoAcessoService _service;

    public PedidoAcessoTest()
    {
        _acessos = new AcessoModuloService(Context);
        _service = new PedidoAcessoService(Context, _acessos);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>Principal como o pipeline do JWT monta: e-mail, sub e roles (perfil primeiro).</summary>
    private static ClaimsPrincipal Principal(string email, params string[] roles) =>
        PrincipalComSub(null, email, roles);

    private static ClaimsPrincipal PrincipalComSub(string? keycloakId, string email, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        if (keycloakId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, keycloakId));
        foreach (var role in ModulosSgdp.RolesComPerfilPrimeiro(roles))
            claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Teste"));
    }

    /// <summary>Quem pede nos testes: usuário sem papel, sem concessão e sem role de módulo.</summary>
    private ClaimsPrincipal Solicitante => Principal(UserSemPapel.Email, Perfis.Basico);

    private Task<DecisorPedidos> Admin() => _service.DecisorAsync(Principal(UserAdmin.Email, Perfis.Admin));

    private Task<DecisorPedidos> Sgdi() => _service.DecisorAsync(Principal(UserSgdi.Email, Perfis.Basico));

    private Task<MeuPedidoAcessoResponse> Pedir(string modulo, string? justificativa = null, ClaimsPrincipal? quem = null) =>
        _service.CriarAsync(quem ?? Solicitante, new PedidoAcessoCreateDTO { Modulo = modulo, Justificativa = justificativa });

    private PedidoAcesso PedidoNoBanco(long id) =>
        Context.PedidosAcesso.AsNoTracking().Single(p => p.Id == id);

    private User UsuarioNoBanco(User user) =>
        Context.Users.AsNoTracking().Single(u => u.Id == user.Id);

    private List<AcessoModulo> ConcessoesDe(User user) =>
        Context.AcessosModulo.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.Origem == OrigemAcesso.Sistema).ToList();

    private static async Task<ErrorCode> CodigoDoErro(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(acao);
        return (ErrorCode)ex.Error.Code;
    }

    private PedidoAcessoController Controlador(ClaimsPrincipal principal) => new(_service)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
    };

    // ── Quem pede ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pedir_SemAcessoAlgum_FicaPendente_EOCardMostra()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia, "  Preciso registrar o uso de IA na minha área  ");

        Assert.Equal(SituacaoPedidoAcesso.Pendente, pedido.Situacao);
        Assert.Equal("Preciso registrar o uso de IA na minha área", PedidoNoBanco(pedido.Id).Justificativa);

        var meus = await _service.MeusPedidosAsync(Solicitante);
        var meu = Assert.Single(meus);
        Assert.Equal(ModulosSgdp.Pgia, meu.Modulo);
        Assert.Equal(SituacaoPedidoAcesso.Pendente, meu.Situacao);
        Assert.Null(meu.MotivoRecusa);
    }

    [Fact]
    public async Task Pedir_DuasVezes_DevolveOMesmoPedido()
    {
        var primeiro = await Pedir(ModulosSgdp.Demandas);
        var segundo = await Pedir(ModulosSgdp.Demandas, "outra justificativa");

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Single(Context.PedidosAcesso.AsNoTracking().Where(p => p.UserId == UserSemPapel.Id));
    }

    [Fact]
    public async Task Pedir_JustificativaEmBranco_GravaNula()
    {
        var pedido = await Pedir(ModulosSgdp.Contratacoes, "   ");
        Assert.Null(PedidoNoBanco(pedido.Id).Justificativa);
    }

    [Fact]
    public async Task Pedir_ModuloQueJaTem_Recusa()
    {
        // O perfil gestor do Keycloak já abre Demandas
        var gestor = Principal(UserSemPapel.Email, Perfis.Gestor);

        Assert.Equal(ErrorCode.PedidoAcessoInvalido, await CodigoDoErro(() => Pedir(ModulosSgdp.Demandas, quem: gestor)));
        Assert.Empty(Context.PedidosAcesso.AsNoTracking());
    }

    [Fact]
    public async Task Pedir_Administracao_Recusa_ExplicandoQueVemDoKeycloak()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => Pedir(ModulosSgdp.Administracao));

        Assert.Equal((int)ErrorCode.PedidoAcessoInvalido, ex.Error.Code);
        Assert.Contains("Keycloak", ex.Error.Message);
    }

    [Theory]
    [InlineData("xpto")]
    [InlineData("")]
    public async Task Pedir_ModuloForaDoDominio_Recusa(string modulo)
    {
        Assert.Equal(ErrorCode.PedidoAcessoInvalido, await CodigoDoErro(() => Pedir(modulo)));
    }

    [Fact]
    public async Task Pedir_UsuarioAindaNaoCadastrado_404()
    {
        var desconhecido = Principal("ninguem@df.gov.br", Perfis.Basico);
        Assert.Equal(ErrorCode.AcessoUsuarioNaoEncontrado,
            await CodigoDoErro(() => Pedir(ModulosSgdp.Pgia, quem: desconhecido)));
    }

    [Fact]
    public async Task MeusPedidos_MostraSoOUltimoDeCadaModulo()
    {
        var recusado = await Pedir(ModulosSgdp.Pgia);
        await _service.RecusarAsync(recusado.Id, new PedidoAcessoRecusarDTO { Motivo = "Falta a indicação da chefia." }, await Admin());
        var novo = await Pedir(ModulosSgdp.Pgia, "Segue a indicação da chefia");
        await Pedir(ModulosSgdp.Demandas);

        var meus = await _service.MeusPedidosAsync(Solicitante);

        Assert.Equal(2, meus.Count);
        var pgia = Assert.Single(meus, m => m.Modulo == ModulosSgdp.Pgia);
        Assert.Equal(novo.Id, pgia.Id);
        Assert.Equal(SituacaoPedidoAcesso.Pendente, pgia.Situacao);
    }

    // ── Aprovação ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_AprovaPgiaComPapel_LiberaNaHora()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        var aprovado = await _service.AprovarAsync(pedido.Id,
            new PedidoAcessoAprovarDTO { PapelPgia = PapeisPgia.Orgao }, await Admin());

        Assert.Equal(SituacaoPedidoAcesso.Aprovado, aprovado.Situacao);
        Assert.Equal(UserAdmin.Email, aprovado.DecididoPor);
        Assert.Equal(PapeisPgia.Orgao, aprovado.PapelPgia);
        Assert.NotNull(aprovado.DecididoEm);
        Assert.Equal(PapeisPgia.Orgao, UsuarioNoBanco(UserSemPapel).PapelPgia);
        Assert.Contains(ConcessoesDe(UserSemPapel), a => a.Modulo == ModulosSgdp.Pgia && a.ConcedidoPor == UserAdmin.Email);
        // Vale já na próxima requisição da pessoa, com o mesmo token
        Assert.Contains(ModulosSgdp.Pgia, await _acessos.ModulosDoPrincipalAsync(Solicitante));
    }

    [Fact]
    public async Task Aprovar_PgiaSemPapel_EntraComoAgente()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        var aprovado = await _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO(), await Admin());

        Assert.Null(aprovado.PapelPgia);
        Assert.Null(UsuarioNoBanco(UserSemPapel).PapelPgia);
        Assert.Contains(ModulosSgdp.Pgia, await _acessos.ModulosDoPrincipalAsync(Solicitante));
    }

    [Fact]
    public async Task Admin_AprovaDemandas_GravaAConcessao()
    {
        var pedido = await Pedir(ModulosSgdp.Demandas);

        await _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO(), await Admin());

        Assert.Contains(ConcessoesDe(UserSemPapel), a => a.Modulo == ModulosSgdp.Demandas);
        Assert.Equal(new[] { ModulosSgdp.Demandas }, await _acessos.ModulosDoPrincipalAsync(Solicitante));
    }

    [Fact]
    public async Task Admin_AprovaContratacoes_GravaOPapelDoModulo()
    {
        var pedido = await Pedir(ModulosSgdp.Contratacoes);

        await _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO(), await Admin());

        Assert.Equal(PapeisContratacoes.Analise, UsuarioNoBanco(UserSemPapel).PapelContratacoes);
        Assert.Empty(ConcessoesDe(UserSemPapel));
        Assert.Equal(new[] { ModulosSgdp.Contratacoes }, await _acessos.ModulosDoPrincipalAsync(Solicitante));
    }

    [Fact]
    public async Task Aprovar_PapelEmPedidoQueNaoEDoPgia_RecusaSemLiberarNada()
    {
        var pedido = await Pedir(ModulosSgdp.Demandas);
        var admin = await Admin();

        Assert.Equal(ErrorCode.AcessoInvalido, await CodigoDoErro(() =>
            _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO { PapelPgia = PapeisPgia.Orgao }, admin)));

        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
        Assert.Empty(ConcessoesDe(UserSemPapel));
        Assert.Null(UsuarioNoBanco(UserSemPapel).PapelPgia);
    }

    [Fact]
    public async Task Aprovar_PapelForaDoDominio_Recusa()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);
        var admin = await Admin();

        Assert.Equal(ErrorCode.AcessoInvalido, await CodigoDoErro(() =>
            _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO { PapelPgia = "pgia_xpto" }, admin)));
        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
    }

    [Fact]
    public async Task Decidir_PedidoJaDecidido_Conflito()
    {
        var pedido = await Pedir(ModulosSgdp.Demandas);
        var admin = await Admin();
        await _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO(), admin);

        Assert.Equal(ErrorCode.PedidoAcessoJaDecidido, await CodigoDoErro(() =>
            _service.RecusarAsync(pedido.Id, new PedidoAcessoRecusarDTO { Motivo = "tarde demais" }, admin)));
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, PedidoNoBanco(pedido.Id).Situacao);
    }

    [Fact]
    public async Task Decidir_PedidoInexistente_404()
    {
        Assert.Equal(ErrorCode.PedidoAcessoNaoEncontrado, await CodigoDoErro(async () =>
            await _service.AprovarAsync(999_999, new PedidoAcessoAprovarDTO(), await Admin())));
    }

    [Fact]
    public async Task DuasPessoasDecidindoAoMesmoTempo_ASegundaERecusada_ENadaELiberado()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        // Segundo contexto sobre a mesma base: a SGDI carrega o pedido ainda pendente...
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(NomeBanco).Options;
        using var outroContexto = new AppDbContext(opcoes);
        var outroServico = new PedidoAcessoService(outroContexto, new AcessoModuloService(outroContexto));
        var sgdi = await outroServico.DecisorAsync(Principal(UserSgdi.Email, Perfis.Basico));
        var carregadoPelaSgdi = await outroContexto.PedidosAcesso.SingleAsync(p => p.Id == pedido.Id);

        // ...o admin recusa primeiro...
        await _service.RecusarAsync(pedido.Id, new PedidoAcessoRecusarDTO { Motivo = "Não é da área." }, await Admin());

        // ...e a aprovação da SGDI, gravada depois, é recusada. (Que nem o acesso é
        // liberado é garantia da transação do PostgreSQL: o InMemory não tem transação.)
        Assert.Equal(ErrorCode.PedidoAcessoJaDecidido, await CodigoDoErro(() =>
            outroServico.AprovarAsync(carregadoPelaSgdi.Id, new PedidoAcessoAprovarDTO(), sgdi)));

        Assert.Equal(SituacaoPedidoAcesso.Recusado, PedidoNoBanco(pedido.Id).Situacao);
        Assert.Equal("Não é da área.", PedidoNoBanco(pedido.Id).MotivoRecusa);
    }

    // ── Recusa ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Recusar_SemMotivo_Recusa(string motivo)
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        Assert.Equal(ErrorCode.PedidoAcessoInvalido, await CodigoDoErro(async () =>
            await _service.RecusarAsync(pedido.Id, new PedidoAcessoRecusarDTO { Motivo = motivo }, await Admin())));
        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
    }

    [Fact]
    public async Task Recusar_ComMotivo_APessoaVeNoCard_EPodePedirDeNovo()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        var recusado = await _service.RecusarAsync(pedido.Id,
            new PedidoAcessoRecusarDTO { Motivo = "  Peça pelo Responsável de IA do seu órgão.  " }, await Admin());

        Assert.Equal(SituacaoPedidoAcesso.Recusado, recusado.Situacao);
        var meu = Assert.Single(await _service.MeusPedidosAsync(Solicitante));
        Assert.Equal(SituacaoPedidoAcesso.Recusado, meu.Situacao);
        Assert.Equal("Peça pelo Responsável de IA do seu órgão.", meu.MotivoRecusa);
        Assert.NotNull(meu.DecididoEm);
        Assert.Empty(await _acessos.ModulosDoPrincipalAsync(Solicitante));

        var novo = await Pedir(ModulosSgdp.Pgia);
        Assert.NotEqual(pedido.Id, novo.Id);
        Assert.Equal(SituacaoPedidoAcesso.Pendente, novo.Situacao);
    }

    // ── Quem decide o quê ─────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_DecideTodosOsModulosQueOSistemaLibera()
    {
        var admin = await Admin();
        Assert.Equal(ModulosSgdp.Liberaveis.OrderBy(m => m), admin.Modulos.OrderBy(m => m));
        Assert.DoesNotContain(ModulosSgdp.Administracao, admin.Modulos);
    }

    [Fact]
    public async Task Sgdi_DecideSoOsPedidosDoPgia()
    {
        var sgdi = await Sgdi();
        Assert.Equal(new[] { ModulosSgdp.Pgia }, sgdi.Modulos);

        var pgia = await Pedir(ModulosSgdp.Pgia);
        var demandas = await Pedir(ModulosSgdp.Demandas);

        var fila = await _service.ListarAsync(new PedidosAcessoConsulta(), sgdi);
        Assert.Equal(new[] { pgia.Id }, fila.Items.Select(p => p.Id));
        Assert.Equal(1, await _service.ContarPendentesAsync(sgdi, null));
        Assert.Empty((await _service.ListarAsync(new PedidosAcessoConsulta { Modulo = ModulosSgdp.Demandas }, sgdi)).Items);

        Assert.Equal(ErrorCode.PedidoAcessoForaDoEscopo, await CodigoDoErro(() =>
            _service.AprovarAsync(demandas.Id, new PedidoAcessoAprovarDTO(), sgdi)));

        var aprovado = await _service.AprovarAsync(pgia.Id, new PedidoAcessoAprovarDTO { PapelPgia = PapeisPgia.Orgao }, sgdi);
        Assert.Equal(UserSgdi.Email, aprovado.DecididoPor);
    }

    [Theory]
    [InlineData("maria@ses.df.gov.br")]   // pgia_orgao
    [InlineData("cgtic@sgdi.df.gov.br")]  // pgia_cgtic
    [InlineData("comum@ses.df.gov.br")]   // sem papel
    public async Task OutrosPapeis_NaoDecidemNada(string email)
    {
        await Pedir(ModulosSgdp.Pgia);
        var principal = Principal(email, Perfis.Gestor, ModulosSgdp.RolePgia);

        var decisor = await _service.DecisorAsync(principal);
        Assert.False(decisor.PodeDecidir);

        Assert.IsType<ForbidResult>(await Controlador(principal).Listar(new PedidosAcessoConsulta()));
        var contagem = Assert.IsType<OkObjectResult>(await Controlador(principal).Pendentes(null));
        Assert.Equal(0, ((PedidosAcessoPendentesResponse)contagem.Value!).Total);
        Assert.IsType<ForbidResult>(await Controlador(principal).Aprovar(1, new PedidoAcessoAprovarDTO()));
    }

    // ── Fila ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fila_PendentesPrimeiroPorOrdemDeChegada_DepoisOsDecididos()
    {
        var joao = Principal(UserOrgaoSeec.Email, Perfis.Basico);      // tem PGIA pelo papel
        var alice = Principal(UserAuditoria.Email, Perfis.Basico);     // tem PGIA pelo papel

        var p1 = await Pedir(ModulosSgdp.Demandas);
        var p2 = await Pedir(ModulosSgdp.Demandas, quem: joao);
        var p3 = await Pedir(ModulosSgdp.Contratacoes, quem: alice);
        var p4 = await Pedir(ModulosSgdp.Pgia);
        var admin = await Admin();
        await _service.RecusarAsync(p2.Id, new PedidoAcessoRecusarDTO { Motivo = "Duplicado." }, admin);
        await _service.AprovarAsync(p3.Id, new PedidoAcessoAprovarDTO(), admin);

        var todos = await _service.ListarAsync(new PedidosAcessoConsulta(), admin);
        Assert.Equal(new[] { p1.Id, p4.Id, p3.Id, p2.Id }, todos.Items.Select(p => p.Id));
        Assert.Equal(4, todos.TotalItems);

        var recusados = await _service.ListarAsync(new PedidosAcessoConsulta { Situacao = SituacaoPedidoAcesso.Recusado }, admin);
        var recusado = Assert.Single(recusados.Items);
        Assert.Equal("Duplicado.", recusado.MotivoRecusa);
        Assert.Equal(UserOrgaoSeec.Nome, recusado.Nome);
        Assert.Equal(UnidadeSeec.Nome, recusado.UnidadeNome);

        var porNome = await _service.ListarAsync(new PedidosAcessoConsulta { Filtro = "CARLOS" }, admin);
        Assert.Equal(new[] { p1.Id, p4.Id }, porNome.Items.Select(p => p.Id));

        // Filtro que o servidor não entende devolve nada, nunca "todos" em silêncio
        Assert.Empty((await _service.ListarAsync(new PedidosAcessoConsulta { Situacao = "xpto" }, admin)).Items);
        Assert.Empty((await _service.ListarAsync(new PedidosAcessoConsulta { Modulo = ModulosSgdp.Administracao }, admin)).Items);

        Assert.Equal(2, await _service.ContarPendentesAsync(admin, null));
        Assert.Equal(1, await _service.ContarPendentesAsync(admin, ModulosSgdp.Pgia));
    }

    [Fact]
    public async Task Fila_PaginaSaneada()
    {
        await Pedir(ModulosSgdp.Demandas);
        var fila = await _service.ListarAsync(new PedidosAcessoConsulta { Page = int.MaxValue, PageSize = 0 }, await Admin());

        Assert.Equal(20, fila.PageSize);
        Assert.Equal(1, fila.TotalItems);
    }

    // ── Acesso liberado por outro caminho encerra o pedido ────────────────────

    [Fact]
    public async Task GestaoDeAcessos_LiberarOModulo_EncerraOPedidoComoAprovado()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        await _acessos.DefinirAcessosAsync(UserSemPapel.Id,
            new AcessoUsuarioUpdateDTO { Pgia = true, PapelPgia = PapeisPgia.Cgtic }, UserAdmin.Email);

        var noBanco = PedidoNoBanco(pedido.Id);
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, noBanco.Situacao);
        Assert.Equal(UserAdmin.Email, noBanco.DecididoPor);
        Assert.Equal(PapeisPgia.Cgtic, noBanco.PapelPgia);
    }

    [Fact]
    public async Task GestaoDeAcessos_SalvarOutroModulo_NaoMexeNoPedido()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        await _acessos.DefinirAcessosAsync(UserSemPapel.Id, new AcessoUsuarioUpdateDTO { Demandas = true }, UserAdmin.Email);

        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
    }

    [Fact]
    public async Task Sgdi_LiberarComoAgente_EncerraOPedido()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        await _acessos.DefinirConcessaoAsync(UserSemPapel.Id, ModulosSgdp.Pgia, true, UserSgdi.Email);

        var noBanco = PedidoNoBanco(pedido.Id);
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, noBanco.Situacao);
        Assert.Equal(UserSgdi.Email, noBanco.DecididoPor);
    }

    [Fact]
    public async Task Sgdi_DarPapelNaTelaDePessoas_EncerraOPedido()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        var resultado = await ComoGovernanca(UserSgdi).AtualizarVinculoPessoa(UserSemPapel.Id,
            new PgiaPessoaVinculoDTO { PapelPgia = PapeisPgia.Orgao, UnidadeId = UnidadeSes.id });

        Assert.IsType<OkObjectResult>(resultado);
        var noBanco = PedidoNoBanco(pedido.Id);
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, noBanco.Situacao);
        Assert.Equal(UserSgdi.Email, noBanco.DecididoPor);
        Assert.Equal(PapeisPgia.Orgao, noBanco.PapelPgia);
    }

    [Fact]
    public async Task Me_AcessoQueChegouPeloKeycloak_EncerraOPedidoSozinho()
    {
        var pedido = await Pedir(ModulosSgdp.Pgia);

        // A pessoa entrou no grupo do órgão no Keycloak e fez login de novo
        var controller = new AuthController(new AuthRepositorio(Context), null!, Options.Create(new AuthSettings()), _acessos)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = PrincipalComSub("kc-carlos", UserSemPapel.Email, Perfis.Basico, ModulosSgdp.RolePgia)
                }
            }
        };
        Assert.IsType<OkObjectResult>(await controller.GetCurrentUser());

        var noBanco = PedidoNoBanco(pedido.Id);
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, noBanco.Situacao);
        Assert.Null(noBanco.DecididoPor);
    }

    // ── Controller ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Controller_QuemNaoTemModuloAlgum_ConseguePedir()
    {
        var resultado = await Controlador(Solicitante).Criar(new PedidoAcessoCreateDTO { Modulo = ModulosSgdp.Pgia });

        var corpo = Assert.IsType<OkObjectResult>(resultado).Value as MeuPedidoAcessoResponse;
        Assert.Equal(SituacaoPedidoAcesso.Pendente, corpo!.Situacao);

        var meus = Assert.IsType<OkObjectResult>(await Controlador(Solicitante).Meus());
        Assert.Single((List<MeuPedidoAcessoResponse>)meus.Value!);
    }

    [Fact]
    public async Task Controller_ErrosDeRegra_VoltamComCodigoEMensagem()
    {
        var admin = Principal(UserAdmin.Email, Perfis.Admin);
        var sgdi = Principal(UserSgdi.Email, Perfis.Basico);

        Assert.IsType<BadRequestObjectResult>(
            await Controlador(Solicitante).Criar(new PedidoAcessoCreateDTO { Modulo = ModulosSgdp.Administracao }));

        var demandas = await Pedir(ModulosSgdp.Demandas);
        Assert.IsType<ForbidResult>(await Controlador(sgdi).Aprovar(demandas.Id, new PedidoAcessoAprovarDTO()));
        Assert.IsType<BadRequestObjectResult>(
            await Controlador(admin).Recusar(demandas.Id, new PedidoAcessoRecusarDTO { Motivo = " " }));
        Assert.IsType<NotFoundObjectResult>(await Controlador(admin).Aprovar(999_999, new PedidoAcessoAprovarDTO()));

        Assert.IsType<OkObjectResult>(await Controlador(admin).Aprovar(demandas.Id, new PedidoAcessoAprovarDTO()));
        Assert.IsType<ConflictObjectResult>(await Controlador(admin).Aprovar(demandas.Id, new PedidoAcessoAprovarDTO()));
    }

    private PgiaGovernancaController ComoGovernanca(User quem)
    {
        var permissao = new PgiaPermissionService(Context);
        var controller = new PgiaGovernancaController(
            new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context)),
            new PgiaSistemaService(
                new PgiaSistemaRepositorio(Context),
                new PgiaOrgaoRepositorio(Context),
                new PgiaDesignacaoRepositorio(Context),
                permissao),
            new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context)),
            new PgiaAdminService(new PgiaOrgaoRepositorio(Context), new PgiaPrazoRepositorio(Context), Context),
            permissao,
            _acessos);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = Principal(quem.Email, PerfilDe(quem)) }
        };
        return controller;
    }

    // ── Governança Estratégica (planejamento) ─────────────────────────────────

    /// <summary>
    /// Principal com a claim do módulo, como a claims transformation deixa quem tem a
    /// concessão. Sem ela, o DecisorAsync nem consulta a tabela do papel (regra do deploy).
    /// </summary>
    private static ClaimsPrincipal ComModuloPlanejamento(string email, params string[] roles)
    {
        var principal = Principal(email, roles);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ModulosSgdp.ClaimModulo, ModulosSgdp.Planejamento));
        return principal;
    }

    /// <summary>Papel e concessão do módulo, gravados juntos (como o serviço grava).</summary>
    private void DarPapelPlanejamento(User user, string papel)
    {
        Context.PePapeisUsuario.Add(new PePapelUsuario
        {
            UserId = user.Id,
            Papel = papel,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = "semente@sgdi.df.gov.br"
        });
        Context.AcessosModulo.Add(new AcessoModulo
        {
            UserId = user.Id,
            Modulo = ModulosSgdp.Planejamento,
            Origem = OrigemAcesso.Sistema,
            ConcedidoEm = DateTime.UtcNow,
            ConcedidoPor = "semente@sgdi.df.gov.br"
        });
        Context.SaveChanges();
    }

    private User NovaPessoa(string email, string nome)
    {
        var user = new User { Email = email, Nome = nome };
        Context.Users.Add(user);
        Context.SaveChanges();
        return user;
    }

    private PePapelUsuario? PapelPlanejamentoDe(User user) =>
        Context.PePapeisUsuario.AsNoTracking().SingleOrDefault(p => p.UserId == user.Id);

    private List<PePapelUsuarioHistorico> HistoricoPlanejamentoDe(User user) =>
        Context.PePapeisUsuarioHistorico.AsNoTracking().Where(h => h.UserId == user.Id).ToList();

    [Fact]
    public async Task Pedir_Planejamento_FicaPendente()
    {
        var pedido = await Pedir(ModulosSgdp.Planejamento, "Vou elaborar o PDTIC da minha secretaria");

        Assert.Equal(SituacaoPedidoAcesso.Pendente, pedido.Situacao);
        Assert.Equal(ModulosSgdp.Planejamento, PedidoNoBanco(pedido.Id).Modulo);
        Assert.Equal(ModulosSgdp.Planejamento, Assert.Single(await _service.MeusPedidosAsync(Solicitante)).Modulo);
    }

    [Fact]
    public async Task Pedir_PlanejamentoQueJaTem_Recusa()
    {
        DarPapelPlanejamento(UserSemPapel, PapeisPlanejamento.Orgao);

        Assert.Equal(ErrorCode.PedidoAcessoInvalido, await CodigoDoErro(() => Pedir(ModulosSgdp.Planejamento)));
    }

    [Fact]
    public async Task Admin_AprovaPlanejamentoComPapel_GravaConcessaoPapelEHistorico()
    {
        var pedido = await Pedir(ModulosSgdp.Planejamento);

        var aprovado = await _service.AprovarAsync(pedido.Id,
            new PedidoAcessoAprovarDTO { PapelPlanejamento = PapeisPlanejamento.Orgao }, await Admin());

        Assert.Equal(SituacaoPedidoAcesso.Aprovado, aprovado.Situacao);
        Assert.Equal(UserAdmin.Email, aprovado.DecididoPor);
        Assert.Equal(PapeisPlanejamento.Orgao, aprovado.PapelPlanejamento);
        Assert.Null(aprovado.PapelPgia);
        // pedido_acesso não guarda o papel do módulo (não ganhou coluna)
        Assert.Null(PedidoNoBanco(pedido.Id).PapelPgia);

        Assert.Equal(PapeisPlanejamento.Orgao, PapelPlanejamentoDe(UserSemPapel)!.Papel);
        Assert.Contains(ConcessoesDe(UserSemPapel), a => a.Modulo == ModulosSgdp.Planejamento && a.ConcedidoPor == UserAdmin.Email);

        var historico = Assert.Single(HistoricoPlanejamentoDe(UserSemPapel));
        Assert.Null(historico.PapelAnterior);
        Assert.Equal(PapeisPlanejamento.Orgao, historico.PapelNovo);
        Assert.Equal(PeDominios.OrigemPapel.Pedido, historico.Origem);
        Assert.Equal(pedido.Id, historico.PedidoAcessoId);
        Assert.Equal(UserAdmin.Email, historico.AlteradoPor);

        // Vale já na próxima requisição da pessoa, com o mesmo token
        Assert.Contains(ModulosSgdp.Planejamento, await _acessos.ModulosDoPrincipalAsync(Solicitante));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pgia_sgdi")]
    [InlineData("pe_xpto")]
    public async Task Aprovar_PlanejamentoSemPapelValido_Recusa_SemLiberarNada(string? papel)
    {
        var pedido = await Pedir(ModulosSgdp.Planejamento);
        var admin = await Admin();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO { PapelPlanejamento = papel }, admin));

        Assert.Equal((int)ErrorCode.PedidoAcessoInvalido, ex.Error.Code);
        Assert.Contains("Governança Estratégica", ex.Error.Message);
        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
        Assert.Empty(ConcessoesDe(UserSemPapel));
        Assert.Null(PapelPlanejamentoDe(UserSemPapel));
        Assert.Empty(HistoricoPlanejamentoDe(UserSemPapel));
    }

    [Fact]
    public async Task Aprovar_PapelPlanejamentoEmPedidoDeOutroModulo_Recusa()
    {
        var pedido = await Pedir(ModulosSgdp.Demandas);

        Assert.Equal(ErrorCode.PedidoAcessoInvalido, await CodigoDoErro(async () =>
            await _service.AprovarAsync(pedido.Id,
                new PedidoAcessoAprovarDTO { PapelPlanejamento = PapeisPlanejamento.Sgdi }, await Admin())));

        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
        Assert.Empty(ConcessoesDe(UserSemPapel));
    }

    [Fact]
    public async Task Aprovar_PapelPgiaEmPedidoDoPlanejamento_Recusa()
    {
        var pedido = await Pedir(ModulosSgdp.Planejamento);

        Assert.Equal(ErrorCode.AcessoInvalido, await CodigoDoErro(async () =>
            await _service.AprovarAsync(pedido.Id, new PedidoAcessoAprovarDTO
            {
                PapelPgia = PapeisPgia.Orgao,
                PapelPlanejamento = PapeisPlanejamento.Orgao
            }, await Admin())));

        Assert.Equal(SituacaoPedidoAcesso.Pendente, PedidoNoBanco(pedido.Id).Situacao);
        Assert.Null(UsuarioNoBanco(UserSemPapel).PapelPgia);
        Assert.Null(PapelPlanejamentoDe(UserSemPapel));
    }

    [Fact]
    public async Task PeAdmin_DecideSoOsPedidosDoPlanejamento()
    {
        var paula = NovaPessoa("paula@sgdi.df.gov.br", "Paula Planejamento");
        DarPapelPlanejamento(paula, PapeisPlanejamento.Admin);

        var peAdmin = await _service.DecisorAsync(ComModuloPlanejamento(paula.Email, Perfis.Basico));
        Assert.Equal(new[] { ModulosSgdp.Planejamento }, peAdmin.Modulos);
        Assert.Equal(paula.Email, peAdmin.Email);

        var planejamento = await Pedir(ModulosSgdp.Planejamento);
        var demandas = await Pedir(ModulosSgdp.Demandas);
        await Pedir(ModulosSgdp.Pgia);

        var fila = await _service.ListarAsync(new PedidosAcessoConsulta(), peAdmin);
        Assert.Equal(new[] { planejamento.Id }, fila.Items.Select(p => p.Id));
        Assert.Equal(1, await _service.ContarPendentesAsync(peAdmin, null));
        Assert.Equal(1, await _service.ContarPendentesAsync(peAdmin, ModulosSgdp.Planejamento));
        Assert.Equal(0, await _service.ContarPendentesAsync(peAdmin, ModulosSgdp.Demandas));

        // Pedido de Demandas não é com o administrador do módulo (905)
        Assert.Equal(ErrorCode.PedidoAcessoForaDoEscopo, await CodigoDoErro(() =>
            _service.AprovarAsync(demandas.Id, new PedidoAcessoAprovarDTO(), peAdmin)));
        Assert.Equal(ErrorCode.PedidoAcessoForaDoEscopo, await CodigoDoErro(() =>
            _service.RecusarAsync(demandas.Id, new PedidoAcessoRecusarDTO { Motivo = "Não é daqui." }, peAdmin)));

        var aprovado = await _service.AprovarAsync(planejamento.Id,
            new PedidoAcessoAprovarDTO { PapelPlanejamento = PapeisPlanejamento.OrgaoConsulta }, peAdmin);
        Assert.Equal(paula.Email, aprovado.DecididoPor);
        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, aprovado.PapelPlanejamento);
    }

    [Fact]
    public async Task SgdiDoPgiaQueTambemEAdministradorDoPlanejamento_DecideOsDois()
    {
        DarPapelPlanejamento(UserSgdi, PapeisPlanejamento.Admin);

        var decisor = await _service.DecisorAsync(ComModuloPlanejamento(UserSgdi.Email, Perfis.Basico));
        Assert.Equal(new[] { ModulosSgdp.Pgia, ModulosSgdp.Planejamento }, decisor.Modulos);

        var pgia = await Pedir(ModulosSgdp.Pgia);
        var planejamento = await Pedir(ModulosSgdp.Planejamento, quem: Principal(UserOrgaoSeec.Email, Perfis.Basico));
        await Pedir(ModulosSgdp.Demandas);

        var fila = await _service.ListarAsync(new PedidosAcessoConsulta(), decisor);
        Assert.Equal(new[] { pgia.Id, planejamento.Id }, fila.Items.Select(p => p.Id));
        Assert.Equal(2, await _service.ContarPendentesAsync(decisor, null));
    }

    [Fact]
    public async Task AdministradorDoPlanejamentoSemAClaimDoModulo_NaoDecide()
    {
        // Regra do deploy: sem a claim do módulo, a tabela do papel nem é consultada
        DarPapelPlanejamento(UserCgtic, PapeisPlanejamento.Admin);

        Assert.False((await _service.DecisorAsync(Principal(UserCgtic.Email, Perfis.Basico))).PodeDecidir);
    }

    [Theory]
    [InlineData(PapeisPlanejamento.Sgdi)]
    [InlineData(PapeisPlanejamento.Cgtic)]
    [InlineData(PapeisPlanejamento.Orgao)]
    [InlineData(PapeisPlanejamento.OrgaoConsulta)]
    public async Task OutrosPapeisDoPlanejamento_NaoDecidem(string papel)
    {
        DarPapelPlanejamento(UserCgtic, papel);
        var principal = ComModuloPlanejamento(UserCgtic.Email, Perfis.Basico);

        Assert.False((await _service.DecisorAsync(principal)).PodeDecidir);
        Assert.IsType<ForbidResult>(await Controlador(principal).Listar(new PedidosAcessoConsulta()));
    }

    [Fact]
    public async Task Fila_MostraOPapelDeHoje_SoNosPedidosDoPlanejamento()
    {
        var pgia = await Pedir(ModulosSgdp.Pgia);
        var planejamento = await Pedir(ModulosSgdp.Planejamento);
        var admin = await Admin();
        await _service.AprovarAsync(planejamento.Id,
            new PedidoAcessoAprovarDTO { PapelPlanejamento = PapeisPlanejamento.Orgao }, admin);

        // Depois a pessoa troca de papel: a fila mostra o papel de hoje
        await _acessos.DefinirPapelPlanejamentoAsync(UserSemPapel.Id, PapeisPlanejamento.OrgaoConsulta,
            UserAdmin.Email, PeDominios.OrigemPapel.Pessoas);

        var fila = await _service.ListarAsync(new PedidosAcessoConsulta(), admin);
        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, fila.Items.Single(p => p.Id == planejamento.Id).PapelPlanejamento);
        Assert.Null(fila.Items.Single(p => p.Id == pgia.Id).PapelPlanejamento);
    }

    [Fact]
    public async Task Controller_AprovarPlanejamento_SemPapel400_ComPapel200_EPeAdminEmDemandas403()
    {
        var planejamento = await Pedir(ModulosSgdp.Planejamento);
        var demandas = await Pedir(ModulosSgdp.Demandas);
        var admin = Principal(UserAdmin.Email, Perfis.Admin);

        var semPapel = Assert.IsType<BadRequestObjectResult>(
            await Controlador(admin).Aprovar(planejamento.Id, new PedidoAcessoAprovarDTO()));
        Assert.Equal((int)ErrorCode.PedidoAcessoInvalido, semPapel.Value!.GetType().GetProperty("Code")!.GetValue(semPapel.Value));

        DarPapelPlanejamento(UserCgtic, PapeisPlanejamento.Admin);
        var peAdmin = ComModuloPlanejamento(UserCgtic.Email, Perfis.Basico);

        var contagem = Assert.IsType<OkObjectResult>(await Controlador(peAdmin).Pendentes(ModulosSgdp.Planejamento));
        Assert.Equal(1, ((PedidosAcessoPendentesResponse)contagem.Value!).Total);
        Assert.IsType<ForbidResult>(await Controlador(peAdmin).Aprovar(demandas.Id, new PedidoAcessoAprovarDTO()));
        Assert.IsType<OkObjectResult>(await Controlador(peAdmin).Aprovar(planejamento.Id,
            new PedidoAcessoAprovarDTO { PapelPlanejamento = PapeisPlanejamento.Sgdi }));
    }
}
