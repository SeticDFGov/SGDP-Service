using api.Planejamento;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Acesso;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Pessoas e papéis do módulo: o papel de quem está logado, a lista de quem tem papel,
/// a busca de candidatas e a gravação do papel. Papel e acesso andam juntos (a
/// concessão em acesso_modulo), toda mudança entra no histórico e dar o papel encerra
/// o pedido de acesso pendente do módulo.
/// </summary>
public class PePessoaServiceTest : PeTestBase
{
    private async Task<PePessoaResponse> Definir(User autor, User alvo, string? papel) =>
        await Service.DefinirPapelAsync(await ContextoDe(autor), alvo.Id, papel);

    private static async Task<ErrorCode> CodigoDoErro(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(acao);
        return (ErrorCode)ex.Error.Code;
    }

    // ── meu-papel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MeuPapel_EquipeDoOrgao_TrazOrgaoEUnidade()
    {
        var meu = Service.MeuPapel(await ContextoDe(UserOrgaoSes));

        Assert.Equal(PapeisPlanejamento.Orgao, meu.Papel);
        Assert.False(meu.EhAdminGeral);
        Assert.Equal(OrgaoSes.Id, meu.OrgaoId);
        Assert.Equal("SES", meu.OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Saúde", meu.OrgaoNome);
        Assert.Equal("SES", meu.UnidadeNome);
    }

    [Fact]
    public async Task MeuPapel_ConsultaDoOrgao()
    {
        var meu = Service.MeuPapel(await ContextoDe(UserConsultaSes));

        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, meu.Papel);
        Assert.Equal(OrgaoSes.Id, meu.OrgaoId);
    }

    [Theory]
    [InlineData("paula.admin@sgdi.df.gov.br", PapeisPlanejamento.Admin)]
    [InlineData("sergio@sgdi.df.gov.br", PapeisPlanejamento.Sgdi)]
    [InlineData("carla@cgtic.df.gov.br", PapeisPlanejamento.Cgtic)]
    public async Task MeuPapel_PapelGlobal_NaUnidadeCentralSemOrgao(string email, string papel)
    {
        var meu = Service.MeuPapel(await ContextoDe(UserPorEmail[email]));

        Assert.Equal(papel, meu.Papel);
        Assert.False(meu.EhAdminGeral);
        Assert.Null(meu.OrgaoId);
        Assert.Null(meu.OrgaoSigla);
        Assert.Equal("Unidade Central de Teste", meu.UnidadeNome);
    }

    [Fact]
    public async Task MeuPapel_AdminGeralSemPapel()
    {
        var meu = Service.MeuPapel(await ContextoDe(UserAdminGeral));

        Assert.True(meu.EhAdminGeral);
        Assert.Null(meu.Papel);
    }

    [Fact]
    public async Task MeuPapel_AdminGeralComPapel_TrazOsDois()
    {
        DarPapel(UserAdminGeral, PapeisPlanejamento.Sgdi);

        var meu = Service.MeuPapel(await ContextoDe(UserAdminGeral));

        Assert.True(meu.EhAdminGeral);
        Assert.Equal(PapeisPlanejamento.Sgdi, meu.Papel);
    }

    // ── Lista de pessoas ──────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_SoQuemTemPapel_EmOrdemDeNome_ComOrgaoEAdminGeral()
    {
        DarPapel(UserAdminGeral, PapeisPlanejamento.Admin);
        RetratoAdmin(UserAdminGeral);

        var pagina = await Service.ListarAsync(new PePessoasConsulta());

        Assert.Equal(6, pagina.TotalItems);
        Assert.Equal(
            new[] { "Ana Admin", "Carla do CGTIC", "Cecília Consulta", "Otávio da Saúde", "Paula Administradora", "Sérgio da SGDI" },
            pagina.Items.Select(p => p.Nome));
        Assert.DoesNotContain(pagina.Items, p => p.UserId == UserSemPapel.Id);

        var otavio = Assert.Single(pagina.Items, p => p.UserId == UserOrgaoSes.Id);
        Assert.Equal(PapeisPlanejamento.Orgao, otavio.Papel);
        Assert.Equal(OrgaoSes.Id, otavio.OrgaoId);
        Assert.Equal("SES", otavio.OrgaoSigla);
        Assert.Equal(OrgaoSes.Nome, otavio.OrgaoNome);
        Assert.Equal("SES", otavio.UnidadeNome);
        Assert.Equal(AutorSemente, otavio.ConcedidoPor);
        Assert.NotNull(otavio.ConcedidoEm);
        Assert.False(otavio.EhAdminGeral);

        var paula = Assert.Single(pagina.Items, p => p.UserId == UserPeAdmin.Id);
        Assert.Null(paula.OrgaoId);

        Assert.True(Assert.Single(pagina.Items, p => p.UserId == UserAdminGeral.Id).EhAdminGeral);
    }

    [Fact]
    public async Task Listar_FiltraPorNomeOuEmail_SemDiferenciarMaiusculas()
    {
        var porNome = await Service.ListarAsync(new PePessoasConsulta { Filtro = "  SAÚDE " });
        Assert.Equal(new[] { UserOrgaoSes.Id }, porNome.Items.Select(p => p.UserId));

        var porEmail = await Service.ListarAsync(new PePessoasConsulta { Filtro = "@SGDI.df" });
        Assert.Equal(new[] { UserPeAdmin.Id, UserPeSgdi.Id }, porEmail.Items.Select(p => p.UserId));
    }

    [Fact]
    public async Task Listar_FiltraPorPapel_EPapelForaDoDominioDevolveVazio()
    {
        var orgao = await Service.ListarAsync(new PePessoasConsulta { Papel = PapeisPlanejamento.OrgaoConsulta });
        Assert.Equal(new[] { UserConsultaSes.Id }, orgao.Items.Select(p => p.UserId));

        var desconhecido = await Service.ListarAsync(new PePessoasConsulta { Papel = "pgia_sgdi" });
        Assert.Empty(desconhecido.Items);
        Assert.Equal(0, desconhecido.TotalItems);
    }

    [Fact]
    public async Task Listar_PaginacaoSaneada()
    {
        var semTamanho = await Service.ListarAsync(new PePessoasConsulta { PageSize = 0 });
        Assert.Equal(20, semTamanho.PageSize);

        var enorme = await Service.ListarAsync(new PePessoasConsulta { PageSize = 5000 });
        Assert.Equal(100, enorme.PageSize);

        var segunda = await Service.ListarAsync(new PePessoasConsulta { Page = 2, PageSize = 2 });
        Assert.Equal(new[] { "Otávio da Saúde", "Paula Administradora" }, segunda.Items.Select(p => p.Nome));
        Assert.Equal(3, segunda.TotalPages);

        var alemDoFim = await Service.ListarAsync(new PePessoasConsulta { Page = int.MaxValue, PageSize = 100 });
        Assert.Empty(alemDoFim.Items);
    }

    // ── Candidatas ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("br")]
    [InlineData("  br  ")]
    public async Task Candidatas_MenosDeTresLetras_ListaVazia(string? filtro)
    {
        Assert.Empty(await Service.CandidatasAsync(filtro));
    }

    [Fact]
    public async Task Candidatas_SemQuemJaTemPapel_ESoQuemJaEntrou()
    {
        // "df.gov.br" casa com todo mundo; o pré-cadastro (sem KeycloakId) nunca entrou
        NovoUser("pre@df.gov.br", "Pré-cadastro", Perfis.Basico, UnidadeSes, jaEntrou: false);
        Context.SaveChanges();

        var candidatas = await Service.CandidatasAsync("DF.GOV.BR");

        Assert.Equal(new[] { UserAdminGeral.Id, UserSemPapel.Id, UserSemUnidade.Id }, candidatas.Select(c => c.UserId));
    }

    [Fact]
    public async Task Candidatas_TrazUnidadeESiglaDoOrgao()
    {
        var bruno = Assert.Single(await Service.CandidatasAsync("bruno"));

        Assert.Equal(UserSemPapel.Nome, bruno.Nome);
        Assert.Equal(UserSemPapel.Email, bruno.Email);
        Assert.Equal("SEEC", bruno.UnidadeNome);
        Assert.Equal("SEEC", bruno.OrgaoSigla);

        var davi = Assert.Single(await Service.CandidatasAsync("davi"));
        Assert.Null(davi.UnidadeNome);
        Assert.Null(davi.OrgaoSigla);
    }

    [Fact]
    public async Task Candidatas_AteVinte_EmOrdemDeNome()
    {
        for (var i = 1; i <= 25; i++)
            NovoUser($"servidor{i:00}@educacao.df.gov.br", $"Servidor {i:00}", Perfis.Basico, UnidadeSeec);
        Context.SaveChanges();

        var candidatas = await Service.CandidatasAsync("educacao");

        Assert.Equal(20, candidatas.Count);
        Assert.Equal("Servidor 01", candidatas.First().Nome);
        Assert.Equal("Servidor 20", candidatas.Last().Nome);
    }

    // ── Dar, trocar e tirar o papel ───────────────────────────────────────────

    [Fact]
    public async Task DefinirPapel_CriaPapelConcessaoEHistorico()
    {
        var resposta = await Definir(UserPeAdmin, UserSemPapel, PapeisPlanejamento.Sgdi);

        Assert.Equal(PapeisPlanejamento.Sgdi, resposta.Papel);
        Assert.Equal(UserPeAdmin.Email, resposta.ConcedidoPor);
        Assert.Equal(OrgaoSeec.Id, resposta.OrgaoId);

        var papel = PapelNoBanco(UserSemPapel)!;
        Assert.Equal(PapeisPlanejamento.Sgdi, papel.Papel);
        Assert.Equal(UserPeAdmin.Email, papel.ConcedidoPor);
        Assert.Null(papel.AlteradoEm);

        var concessao = Assert.Single(ConcessoesDoModulo(UserSemPapel));
        Assert.Equal(UserPeAdmin.Email, concessao.ConcedidoPor);

        var historico = Assert.Single(HistoricoDe(UserSemPapel));
        Assert.Null(historico.PapelAnterior);
        Assert.Equal(PapeisPlanejamento.Sgdi, historico.PapelNovo);
        Assert.Equal(PeDominios.OrigemPapel.Pessoas, historico.Origem);
        Assert.Equal(UserPeAdmin.Email, historico.AlteradoPor);
        Assert.Null(historico.PedidoAcessoId);

        // O acesso vale já na próxima requisição da pessoa (claims transformation)
        Assert.Contains(ModulosSgdp.Planejamento,
            await Acessos.ModulosDoPrincipalAsync(PrincipalDe(UserSemPapel.KeycloakId, UserSemPapel.Email, Perfis.Basico)));
    }

    [Fact]
    public async Task DefinirPapel_Trocar_AtualizaOPapelEGravaHistorico()
    {
        var antes = PapelNoBanco(UserOrgaoSes)!;

        var resposta = await Definir(UserPeAdmin, UserOrgaoSes, PapeisPlanejamento.OrgaoConsulta);

        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, resposta.Papel);
        var depois = PapelNoBanco(UserOrgaoSes)!;
        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, depois.Papel);
        Assert.Equal(antes.ConcedidoEm, depois.ConcedidoEm);
        Assert.Equal(AutorSemente, depois.ConcedidoPor);
        Assert.Equal(UserPeAdmin.Email, depois.AlteradoPor);
        Assert.NotNull(depois.AlteradoEm);

        var historico = Assert.Single(HistoricoDe(UserOrgaoSes));
        Assert.Equal(PapeisPlanejamento.Orgao, historico.PapelAnterior);
        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, historico.PapelNovo);

        // A concessão continua uma só, a de antes
        Assert.Equal(AutorSemente, Assert.Single(ConcessoesDoModulo(UserOrgaoSes)).ConcedidoPor);
    }

    [Fact]
    public async Task DefinirPapel_MesmoPapel_NaoGravaNada()
    {
        await Definir(UserPeAdmin, UserPeSgdi, PapeisPlanejamento.Sgdi);

        Assert.Empty(HistoricoDe(UserPeSgdi));
        Assert.Null(PapelNoBanco(UserPeSgdi)!.AlteradoEm);
    }

    [Fact]
    public async Task DefinirPapel_Retirar_TiraPapelEConcessao()
    {
        var resposta = await Definir(UserPeAdmin, UserConsultaSes, null);

        Assert.Equal(UserConsultaSes.Id, resposta.UserId);
        Assert.Null(resposta.Papel);
        Assert.Null(resposta.ConcedidoEm);
        Assert.Null(resposta.ConcedidoPor);
        Assert.Equal(UserConsultaSes.Nome, resposta.Nome);

        Assert.Null(PapelNoBanco(UserConsultaSes));
        Assert.Empty(ConcessoesDoModulo(UserConsultaSes));

        var historico = Assert.Single(HistoricoDe(UserConsultaSes));
        Assert.Equal(PapeisPlanejamento.OrgaoConsulta, historico.PapelAnterior);
        Assert.Null(historico.PapelNovo);

        Assert.DoesNotContain(ModulosSgdp.Planejamento,
            await Acessos.ModulosDoPrincipalAsync(PrincipalDe(UserConsultaSes.KeycloakId, UserConsultaSes.Email, Perfis.Basico)));
    }

    [Fact]
    public async Task DefinirPapel_RetirarDeQuemNaoTem_NaoGravaNada()
    {
        var resposta = await Definir(UserPeAdmin, UserSemPapel, null);

        Assert.Null(resposta.Papel);
        Assert.Empty(HistoricoDe(UserSemPapel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PapeisPlanejamento.Sgdi)]
    [InlineData(PapeisPlanejamento.OrgaoConsulta)]
    public async Task DefinirPapel_AdminDoModuloNaoTiraNemTrocaOProprioPapel(string? papel)
    {
        Assert.Equal(ErrorCode.PeAutoRebaixamento,
            await CodigoDoErro(() => Definir(UserPeAdmin, UserPeAdmin, papel)));

        Assert.Equal(PapeisPlanejamento.Admin, PapelNoBanco(UserPeAdmin)!.Papel);
        Assert.Single(ConcessoesDoModulo(UserPeAdmin));
        Assert.Empty(HistoricoDe(UserPeAdmin));
    }

    [Fact]
    public async Task DefinirPapel_AdminDoModuloMantendoOProprioPapel_Aceita()
    {
        var resposta = await Definir(UserPeAdmin, UserPeAdmin, PapeisPlanejamento.Admin);

        Assert.Equal(PapeisPlanejamento.Admin, resposta.Papel);
        Assert.Empty(HistoricoDe(UserPeAdmin));
    }

    [Fact]
    public async Task DefinirPapel_AdminDoModuloDaEtiraOPapelDeAdministradorDeOutraPessoa()
    {
        await Definir(UserPeAdmin, UserPeSgdi, PapeisPlanejamento.Admin);
        Assert.Equal(PapeisPlanejamento.Admin, PapelNoBanco(UserPeSgdi)!.Papel);

        // O novo administrador pode rebaixar o primeiro (só não pode rebaixar a si mesmo)
        await Definir(UserPeSgdi, UserPeAdmin, PapeisPlanejamento.Sgdi);
        Assert.Equal(PapeisPlanejamento.Sgdi, PapelNoBanco(UserPeAdmin)!.Papel);
    }

    [Fact]
    public async Task DefinirPapel_AdminGeralPodeTudo_InclusiveTirarOProprioPapel()
    {
        DarPapel(UserAdminGeral, PapeisPlanejamento.Admin);

        var resposta = await Definir(UserAdminGeral, UserAdminGeral, null);

        Assert.Null(resposta.Papel);
        Assert.Null(PapelNoBanco(UserAdminGeral));
    }

    [Theory]
    [InlineData("pgia_sgdi")]
    [InlineData("pe_xpto")]
    [InlineData("PE_ADMIN")]
    public async Task DefinirPapel_PapelForaDoDominio_Recusa(string papel)
    {
        Assert.Equal(ErrorCode.PePapelInvalido, await CodigoDoErro(() => Definir(UserPeAdmin, UserSemPapel, papel)));

        Assert.Null(PapelNoBanco(UserSemPapel));
        Assert.Empty(ConcessoesDoModulo(UserSemPapel));
    }

    [Fact]
    public async Task DefinirPapel_UsuarioInexistente_Recusa()
    {
        var ctx = await ContextoDe(UserPeAdmin);

        Assert.Equal(ErrorCode.PeUsuarioNaoEncontrado,
            await CodigoDoErro(() => Service.DefinirPapelAsync(ctx, Guid.NewGuid(), PapeisPlanejamento.Orgao)));
    }

    [Fact]
    public async Task DefinirPapel_PapelDeOrgaoParaQuemNaoTemUnidade_GravaComOrgaoNulo()
    {
        // Não bloqueia: o front avisa que a pessoa ainda não tem órgão
        var resposta = await Definir(UserPeAdmin, UserSemUnidade, PapeisPlanejamento.Orgao);

        Assert.Equal(PapeisPlanejamento.Orgao, resposta.Papel);
        Assert.Null(resposta.OrgaoId);
        Assert.Null(resposta.UnidadeNome);
        Assert.Single(ConcessoesDoModulo(UserSemUnidade));
    }

    [Fact]
    public async Task DefinirPapel_EncerraOPedidoPendenteDoModulo_ComoAprovado()
    {
        var planejamento = NovoPedido(UserSemPapel, ModulosSgdp.Planejamento);
        var pgia = NovoPedido(UserSemPapel, ModulosSgdp.Pgia);

        await Definir(UserPeAdmin, UserSemPapel, PapeisPlanejamento.Orgao);

        var encerrado = Context.PedidosAcesso.AsNoTracking().Single(p => p.Id == planejamento.Id);
        Assert.Equal(SituacaoPedidoAcesso.Aprovado, encerrado.Situacao);
        Assert.Equal(UserPeAdmin.Email, encerrado.DecididoPor);
        Assert.NotNull(encerrado.DecididoEm);
        Assert.Null(encerrado.PapelPgia);

        // Pedido de outro módulo continua na fila
        Assert.Equal(SituacaoPedidoAcesso.Pendente, Context.PedidosAcesso.AsNoTracking().Single(p => p.Id == pgia.Id).Situacao);
    }

    [Fact]
    public async Task DefinirPapel_Retirar_NaoMexeEmPedido()
    {
        var pedido = NovoPedido(UserConsultaSes, ModulosSgdp.Planejamento);

        await Definir(UserPeAdmin, UserConsultaSes, null);

        Assert.Equal(SituacaoPedidoAcesso.Pendente, Context.PedidosAcesso.AsNoTracking().Single(p => p.Id == pedido.Id).Situacao);
    }

    private PedidoAcesso NovoPedido(User user, string modulo)
    {
        var pedido = new PedidoAcesso
        {
            UserId = user.Id,
            Modulo = modulo,
            Situacao = SituacaoPedidoAcesso.Pendente,
            CriadoEm = DateTime.UtcNow
        };
        Context.PedidosAcesso.Add(pedido);
        Context.SaveChanges();
        return pedido;
    }
}
