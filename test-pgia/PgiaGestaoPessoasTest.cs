using System.Security.Claims;
using api.Pgia;
using app.Models;
using Controllers.Pgia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Novo modelo de administração (decisão do dono do produto, a SGDI): a SGDI
/// cadastra o órgão, pré-cadastra as pessoas e entrega o órgão pronto — cadastro
/// de órgão, dados do órgão, dados de agente público e vínculo de acesso são
/// escrita da SGDI/admin; ao papel do órgão restam as designações. Os testes
/// exercitam as actions dos controllers, que é onde o gate mora.
/// </summary>
public class PgiaGestaoPessoasTest : PgiaTestBase
{
    private readonly PgiaPermissionService _permissionService;
    private readonly PgiaGovernancaService _governancaService;
    private readonly PgiaOrgaoController _orgaoController;
    private readonly PgiaGovernancaController _governancaController;

    public PgiaGestaoPessoasTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _governancaService = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));

        _orgaoController = new PgiaOrgaoController(
            new PgiaOrgaoService(
                new PgiaOrgaoRepositorio(Context),
                new PgiaDesignacaoRepositorio(Context),
                new PgiaPrazoRepositorio(Context),
                _permissionService,
                Context),
            _permissionService);

        _governancaController = new PgiaGovernancaController(
            _governancaService,
            new PgiaSistemaService(
                new PgiaSistemaRepositorio(Context),
                new PgiaOrgaoRepositorio(Context),
                new PgiaDesignacaoRepositorio(Context),
                _permissionService),
            new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context)),
            new PgiaAdminService(
                new PgiaOrgaoRepositorio(Context),
                new PgiaPrazoRepositorio(Context),
                Context),
            _permissionService);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>Coloca e-mail e role no ClaimsPrincipal, como o pipeline do JWT faz.</summary>
    private ControllerBase Autenticar(ControllerBase controller, string email)
    {
        var identidade = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, PerfilDe(email))
        }, "Teste");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidade) }
        };
        return controller;
    }

    private PgiaOrgaoController ComoOrgaoController(string email)
    {
        Autenticar(_orgaoController, email);
        return _orgaoController;
    }

    private PgiaGovernancaController ComoGovernancaController(string email)
    {
        Autenticar(_governancaController, email);
        return _governancaController;
    }

    private static PgiaOrgaoDadosDTO NovoDadosDto(string nome) => new()
    {
        Nome = nome,
        NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta
    };

    private static PgiaAgenteInfoDTO NovoAgenteInfoDto(string matricula) => new()
    {
        Matricula = matricula,
        CargoFuncao = "Analista",
        Vinculo = PgiaDominios.Vinculo.ServidorEfetivo
    };

    private void DesignarResponsavelVigente(PgiaOrgao orgao, User agente)
    {
        Context.PgiaResponsaveisIa.Add(new PgiaResponsavelIa
        {
            OrgaoId = orgao.Id,
            AgenteId = agente.Id,
            AtoTipo = "Portaria",
            AtoNumero = "214/2026",
            AtoData = new DateOnly(2026, 7, 20),
            ProcessoSeiComunicacao = "00060-00012345/2026-11",
            DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
            InicioVigencia = new DateOnly(2026, 7, 20),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        Context.SaveChanges();
    }

    private void DesignarEncarregadoVigente(PgiaOrgao orgao, User agente)
    {
        Context.PgiaEncarregadosDados.Add(new PgiaEncarregadoDados
        {
            OrgaoId = orgao.Id,
            AgenteId = agente.Id,
            AtoTipo = "Portaria",
            AtoNumero = "215/2026",
            AtoData = new DateOnly(2026, 7, 20),
            ProcessoSeiComunicacao = "00060-00012399/2026-11",
            DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
            InicioVigencia = new DateOnly(2026, 7, 20),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        Context.SaveChanges();
    }

    private static List<PgiaPessoaAcesso> Pessoas(IActionResult resultado) =>
        Assert.IsType<List<PgiaPessoaAcesso>>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static PgiaPessoaAcesso Cadastro(IActionResult resultado) =>
        Assert.IsType<PgiaPessoaAcesso>(Assert.IsType<OkObjectResult>(resultado).Value);

    // ── Dados do órgão: o órgão perdeu a edição ───────────────────────────────

    [Fact]
    public async Task DadosDoOrgao_PapelOrgaoRecebeForbidNoProprioOrgao()
    {
        var resultado = await ComoOrgaoController(UserOrgaoSes.Email)
            .AtualizarDados(OrgaoSes.Id, NovoDadosDto("Nome novo do órgão"));

        Assert.IsType<ForbidResult>(resultado);

        var orgao = await Context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == OrgaoSes.Id);
        Assert.Equal("Secretaria de Estado de Saúde do Distrito Federal", orgao.Nome);
    }

    [Fact]
    public async Task DadosDoOrgao_SgdiEditaQualquerOrgao()
    {
        var controller = ComoOrgaoController(UserSgdi.Email);

        var ses = Assert.IsType<OkObjectResult>(
            await controller.AtualizarDados(OrgaoSes.Id, NovoDadosDto("Saúde (revisado pela SGDI)")));
        var seec = Assert.IsType<OkObjectResult>(
            await controller.AtualizarDados(OrgaoSeec.Id, NovoDadosDto("Economia (revisado pela SGDI)")));

        Assert.Equal("Saúde (revisado pela SGDI)", Assert.IsType<PgiaOrgaoResponse>(ses.Value).Nome);
        Assert.Equal("Economia (revisado pela SGDI)", Assert.IsType<PgiaOrgaoResponse>(seec.Value).Nome);

        var salvos = await Context.PgiaOrgaos.AsNoTracking().ToListAsync();
        Assert.All(salvos, o => Assert.Equal(UserSgdi.Email, o.AlteradoPor));
    }

    /// <summary>
    /// C5: pela tela "Dados dos órgãos" a SGDI liga/troca a unidade vinculada —
    /// é o conserto de órgão sem unidade. Unidade nula mantém a atual.
    /// </summary>
    [Fact]
    public async Task DadosDoOrgao_SgdiTrocaAUnidade()
    {
        var novaUnidade = new Unidade { id = Guid.NewGuid(), Nome = "Unidade nova da SES" };
        Context.Unidades.Add(novaUnidade);
        await Context.SaveChangesAsync();

        var dto = NovoDadosDto("Secretaria de Saúde");
        dto.UnidadeId = novaUnidade.id;

        var resultado = await ComoOrgaoController(UserSgdi.Email).AtualizarDados(OrgaoSes.Id, dto);
        var orgao = Assert.IsType<PgiaOrgaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(novaUnidade.id, orgao.UnidadeId);

        var salvo = await Context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == OrgaoSes.Id);
        Assert.Equal(novaUnidade.id, salvo.UnidadeId);
    }

    [Fact]
    public async Task DadosDoOrgao_UnidadeJaOcupadaEhRejeitada()
    {
        var dto = NovoDadosDto("Secretaria de Saúde");
        dto.UnidadeId = UnidadeSeec.id; // já é do OrgaoSeec

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            ComoOrgaoController(UserSgdi.Email).AtualizarDados(OrgaoSes.Id, dto));

        Assert.Equal((int)ErrorCode.PgiaOrgaoJaExiste, ex.Error.Code);
        var salvo = await Context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == OrgaoSes.Id);
        Assert.Equal(UnidadeSes.id, salvo.UnidadeId); // inalterada
    }

    [Fact]
    public async Task DadosDoOrgao_UnidadeInexistenteEhRejeitada()
    {
        var dto = NovoDadosDto("Secretaria de Saúde");
        dto.UnidadeId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            ComoOrgaoController(UserSgdi.Email).AtualizarDados(OrgaoSes.Id, dto));

        Assert.Equal((int)ErrorCode.PgiaUnidadeNaoEncontrada, ex.Error.Code);
    }

    [Fact]
    public async Task DadosDoOrgao_UnidadeNulaMantemAAtual()
    {
        // NovoDadosDto não informa UnidadeId (null): não desvincula
        var resultado = await ComoOrgaoController(UserSgdi.Email)
            .AtualizarDados(OrgaoSes.Id, NovoDadosDto("Saúde revisada"));

        var orgao = Assert.IsType<PgiaOrgaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(UnidadeSes.id, orgao.UnidadeId); // preservada
    }

    [Fact]
    public async Task DadosDoOrgao_OrgaoContinuaEnxergandoOProprioOrgao()
    {
        var resultado = await ComoOrgaoController(UserOrgaoSes.Email).GetMeuOrgao();

        var resposta = Assert.IsType<PgiaMeuOrgaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(OrgaoSes.Id, resposta.Orgao?.Id);
    }

    // ── Agente info: o órgão perdeu a edição, manteve a visualização ──────────

    [Fact]
    public async Task AgenteInfo_PapelOrgaoRecebeForbid()
    {
        var resultado = await ComoOrgaoController(UserOrgaoSes.Email)
            .SalvarAgenteInfo(OrgaoSes.Id, UserOrgaoSes.Id, NovoAgenteInfoDto("178.402-1"));

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(await Context.PgiaAgenteInfos.ToListAsync());
    }

    [Fact]
    public async Task AgenteInfo_SgdiSalvaEmQualquerOrgao()
    {
        var controller = ComoOrgaoController(UserSgdi.Email);

        Assert.IsType<OkResult>(
            await controller.SalvarAgenteInfo(OrgaoSes.Id, UserOrgaoSes.Id, NovoAgenteInfoDto("178.402-1")));
        Assert.IsType<OkResult>(
            await controller.SalvarAgenteInfo(OrgaoSeec.Id, UserOrgaoSeec.Id, NovoAgenteInfoDto("209.771-4")));

        var infos = await Context.PgiaAgenteInfos.AsNoTracking().ToListAsync();
        Assert.Equal(2, infos.Count);
        Assert.All(infos, i => Assert.Equal(UserSgdi.Email, i.CriadoPor));
    }

    [Fact]
    public async Task AgenteInfo_OrgaoContinuaListandoAsPessoas()
    {
        var resultado = await ComoOrgaoController(UserOrgaoSes.Email).ListarPessoas(OrgaoSes.Id);

        var pessoas = Assert.IsType<List<PgiaPessoaResponse>>(
            Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(2, pessoas.Count); // Maria e Carlos, ambos da unidade SES
    }

    // ── GET governanca/pessoas ────────────────────────────────────────────────

    [Fact]
    public async Task ListarPessoas_FiltroPorNomeIgnoraMaiusculas()
    {
        var pessoas = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas("ANDRADE", null));

        var maria = Assert.Single(pessoas);
        Assert.Equal(UserOrgaoSes.Id, maria.UserId);
        Assert.Equal("pgia_orgao", maria.PapelPgia);
    }

    [Fact]
    public async Task ListarPessoas_FiltroPorEmail()
    {
        var pessoas = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas("JOAO@SEEC", null));

        Assert.Equal(UserOrgaoSeec.Id, Assert.Single(pessoas).UserId);
    }

    [Fact]
    public async Task ListarPessoas_FiltroPorOrgaoResolveSiglaEUnidade()
    {
        var pessoas = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, OrgaoSes.Id));

        Assert.Equal(2, pessoas.Count); // Maria e Carlos, da unidade SES
        Assert.All(pessoas, p =>
        {
            Assert.Equal(OrgaoSes.Id, p.OrgaoId);
            Assert.Equal("SES", p.OrgaoSigla);
            Assert.Equal(UnidadeSes.id, p.UnidadeId);
            Assert.Equal("Secretaria de Saúde", p.UnidadeNome);
        });
        Assert.DoesNotContain(pessoas, p => p.UserId == UserOrgaoSeec.Id);
    }

    /// <summary>
    /// C6: o GET pessoas traz matrícula/cargo/vínculo de pgia_agente_info, para a
    /// tela "Completar dados" da SGDI pré-preencher e não zerar ao salvar.
    /// </summary>
    [Fact]
    public async Task ListarPessoas_TrazDadosDeAgentePublicoQuandoExistem()
    {
        Context.PgiaAgenteInfos.Add(new PgiaAgenteInfo
        {
            UserId = UserOrgaoSes.Id,
            Matricula = "178.402-1",
            CargoFuncao = "Analista de TI",
            Vinculo = PgiaDominios.Vinculo.ServidorEfetivo,
            CriadoEm = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var pessoas = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, OrgaoSes.Id));

        var maria = Assert.Single(pessoas, p => p.UserId == UserOrgaoSes.Id);
        Assert.Equal("178.402-1", maria.Matricula);
        Assert.Equal("Analista de TI", maria.CargoFuncao);
        Assert.Equal(PgiaDominios.Vinculo.ServidorEfetivo, maria.Vinculo);

        // Carlos, sem agente-info, vem com os campos nulos
        var carlos = Assert.Single(pessoas, p => p.UserId == UserSemPapel.Id);
        Assert.Null(carlos.Matricula);
        Assert.Null(carlos.CargoFuncao);
        Assert.Null(carlos.Vinculo);
    }

    [Fact]
    public async Task ListarPessoas_OrgaoInexistenteOuInativoNaoTemPessoas()
    {
        OrgaoSeec.Ativo = false;
        await Context.SaveChangesAsync();

        var inativo = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, OrgaoSeec.Id));
        var inexistente = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, 987654));

        Assert.Empty(inativo);
        Assert.Empty(inexistente);
    }

    [Fact]
    public async Task ListarPessoas_IncluiUsuarioSemUnidadeESemPapel()
    {
        var recemChegado = new User
        {
            Nome = "Zilda Recém-Chegada",
            Email = "zilda@novo.df.gov.br"
        };
        Context.Users.Add(recemChegado);
        await Context.SaveChangesAsync();

        var pessoas = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, null));

        var zilda = Assert.Single(pessoas, p => p.UserId == recemChegado.Id);
        Assert.Null(zilda.PapelPgia);
        Assert.Null(zilda.UnidadeId);
        Assert.Null(zilda.UnidadeNome);
        Assert.Null(zilda.OrgaoId);
        Assert.Null(zilda.OrgaoSigla);

        // A auditora externa também não tem unidade e continua na lista
        Assert.Contains(pessoas, p => p.UserId == UserAuditoria.Id && p.OrgaoId == null);
    }

    /// <summary>
    /// O cap de 200 vale para QUALQUER combinação de filtro (C4): sem ele, um filtro
    /// largo como "@" devolveria a base inteira. Sempre ordenado por Nome.
    /// </summary>
    [Fact]
    public async Task ListarPessoas_CapDeSegurancaValeSempreEOrdenaPorNome()
    {
        for (var i = 0; i < 300; i++)
        {
            Context.Users.Add(new User
            {
                Nome = $"Pessoa {i:D3}",
                Email = $"pessoa{i:D3}@df.gov.br"
            });
        }
        await Context.SaveChangesAsync();

        var semRecorte = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas(null, null));
        // Filtro largo que casa com todos os 300+ e-mails: ainda assim capado
        var comFiltroLargo = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas("@", null));
        var comFiltroEstreito = Pessoas(await ComoGovernancaController(UserSgdi.Email)
            .ListarPessoas("Pessoa 2", null)); // "Pessoa 200".."Pessoa 299" = 100

        Assert.Equal(PgiaGovernancaService.LimitePessoas, semRecorte.Count);
        Assert.Equal(PgiaGovernancaService.LimitePessoas, comFiltroLargo.Count);
        Assert.Equal(100, comFiltroEstreito.Count); // abaixo do cap, traz todos que casam
        Assert.Equal(semRecorte.Select(p => p.Nome).OrderBy(n => n), semRecorte.Select(p => p.Nome));
    }

    // ── PUT governanca/pessoa/{userId}/vinculo ────────────────────────────────

    [Fact]
    public async Task AtualizarVinculo_DefinePapelEUnidade()
    {
        var resultado = await ComoGovernancaController(UserSgdi.Email)
            .AtualizarVinculoPessoa(UserAuditoria.Id, new PgiaPessoaVinculoDTO
            {
                PapelPgia = "pgia_orgao",
                UnidadeId = UnidadeSes.id
            });

        var pessoa = Assert.IsType<PgiaPessoaAcesso>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("pgia_orgao", pessoa.PapelPgia);
        Assert.Equal(UnidadeSes.id, pessoa.UnidadeId);
        Assert.Equal(OrgaoSes.Id, pessoa.OrgaoId);
        Assert.Equal("SES", pessoa.OrgaoSigla);

        var salvo = await Context.Users.Include(u => u.Unidade).FirstAsync(u => u.Id == UserAuditoria.Id);
        Assert.Equal("pgia_orgao", salvo.PapelPgia);
        Assert.Equal(UnidadeSes.id, salvo.Unidade?.id);
    }

    [Fact]
    public async Task AtualizarVinculo_NulosLimpamOVinculo()
    {
        var pessoa = await _governancaService.AtualizarVinculoPessoaAsync(
            UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = null, UnidadeId = null });

        Assert.Null(pessoa.PapelPgia);
        Assert.Null(pessoa.UnidadeId);
        Assert.Null(pessoa.OrgaoId);
        Assert.Null(pessoa.OrgaoSigla);

        var salvo = await Context.Users.Include(u => u.Unidade).FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Null(salvo.PapelPgia);
        Assert.Null(salvo.Unidade);
    }

    [Fact]
    public async Task AtualizarVinculo_GravaOPapelPgia()
    {
        await _governancaService.AtualizarVinculoPessoaAsync(
            UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_sgdi", UnidadeId = UnidadeSeec.id });

        var salvo = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Equal("pgia_sgdi", salvo.PapelPgia);
    }

    [Fact]
    public async Task AtualizarVinculo_PapelInvalidoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_chefe" }));

        Assert.Equal((int)ErrorCode.PgiaPapelInvalido, ex.Error.Code);
        var salvo = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Equal("pgia_orgao", salvo.PapelPgia);
    }

    [Fact]
    public async Task AtualizarVinculo_UsuarioInexistenteEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                Guid.NewGuid(), new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao" }));

        Assert.Equal((int)ErrorCode.PgiaUsuarioNaoEncontrado, ex.Error.Code);
    }

    [Fact]
    public async Task AtualizarVinculo_UnidadeInexistenteEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { UnidadeId = Guid.NewGuid() }));

        Assert.Equal((int)ErrorCode.PgiaUnidadeNaoEncontrada, ex.Error.Code);
    }

    // ── C7: troca de órgão de quem tem designação vigente ─────────────────────

    [Fact]
    public async Task AtualizarVinculo_ResponsavelVigenteBloqueiaTrocaDeOrgao()
    {
        DesignarResponsavelVigente(OrgaoSes, UserOrgaoSes);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSeec.id }));

        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
        Assert.Contains("SES", ex.Error.Message);

        var salvo = await Context.Users.Include(u => u.Unidade).FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Equal(UnidadeSes.id, salvo.Unidade?.id); // nada mudou
    }

    [Fact]
    public async Task AtualizarVinculo_ResponsavelVigenteBloqueiaDesvinculo()
    {
        DesignarResponsavelVigente(OrgaoSes, UserOrgaoSes);

        // Limpar a unidade (null) também deixaria a designação órfã
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = null, UnidadeId = null }));

        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
    }

    [Fact]
    public async Task AtualizarVinculo_EncarregadoVigenteBloqueiaTrocaDeOrgao()
    {
        DesignarEncarregadoVigente(OrgaoSes, UserOrgaoSes);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.AtualizarVinculoPessoaAsync(
                UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSeec.id }));

        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
        Assert.Contains("Encarregado", ex.Error.Message);
    }

    [Fact]
    public async Task AtualizarVinculo_ResponsavelVigenteMudaSoOPapelNoMesmoOrgao()
    {
        DesignarResponsavelVigente(OrgaoSes, UserOrgaoSes);

        // Mesma unidade: não orfaniza — a troca de papel é permitida
        var pessoa = await _governancaService.AtualizarVinculoPessoaAsync(
            UserOrgaoSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_sgdi", UnidadeId = UnidadeSes.id });

        Assert.Equal("pgia_sgdi", pessoa.PapelPgia);
        Assert.Equal(UnidadeSes.id, pessoa.UnidadeId);
    }

    [Fact]
    public async Task PreCadastro_ResponsavelVigenteBloqueiaTrocaDeOrgao()
    {
        DesignarResponsavelVigente(OrgaoSes, UserOrgaoSes);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
            {
                Email = UserOrgaoSes.Email,
                Nome = "Maria",
                PapelPgia = "pgia_orgao",
                UnidadeId = UnidadeSeec.id // tenta mudar de órgão via pré-cadastro
            }));

        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
    }

    // ── POST governanca/orgao (a SGDI cadastra o órgão) ───────────────────────

    [Fact]
    public async Task CriarOrgao_SgdiCadastraComUnidadeEInstanciaOsPrazos()
    {
        var unidade = new Unidade { id = Guid.NewGuid(), Nome = "Secretaria de Desenvolvimento Social", CodigoExterno = "SEDES" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var resultado = await ComoGovernancaController(UserSgdi.Email).CriarOrgao(new PgiaOrgaoCreateDTO
        {
            Nome = "Secretaria de Estado de Desenvolvimento Social",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id
        });

        var orgao = Assert.IsType<PgiaOrgaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("SEDES", orgao.Sigla);
        Assert.True(orgao.Ativo);
        Assert.Equal(unidade.id, orgao.UnidadeId);

        // Adesão do órgão: as 6 obrigações-modelo não exclusivas da SGDI
        var prazos = await Context.PgiaPrazosConformidade.Where(p => p.OrgaoId == orgao.Id).ToListAsync();
        Assert.Equal(6, prazos.Count);
        Assert.DoesNotContain(prazos, p => p.Obrigacao.StartsWith("SGDI:"));

        var salvo = await Context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == orgao.Id);
        Assert.Equal(UserSgdi.Email, salvo.CriadoPor);
    }

    /// <summary>
    /// C5: pela SGDI a unidade é obrigatória — sem ela o órgão nasceria num beco sem
    /// saída (sem como receber pessoas/designações). O admin ainda pode criar sem.
    /// </summary>
    [Fact]
    public async Task CriarOrgao_SemUnidadeEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            ComoGovernancaController(UserSgdi.Email).CriarOrgao(new PgiaOrgaoCreateDTO
            {
                Nome = "Autarquia sem unidade vinculada",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.Autarquia
            }));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Equal(2, await Context.PgiaOrgaos.CountAsync()); // nada foi criado
    }

    [Fact]
    public async Task CriarOrgao_UnidadeJaVinculadaEhRejeitada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            ComoGovernancaController(UserSgdi.Email).CriarOrgao(new PgiaOrgaoCreateDTO
            {
                Nome = "Órgão duplicando a unidade da SES",
                NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
                UnidadeId = UnidadeSes.id
            }));

        Assert.Equal((int)ErrorCode.PgiaOrgaoJaExiste, ex.Error.Code);
        Assert.Equal(2, await Context.PgiaOrgaos.CountAsync());
    }

    [Theory]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]   // papel de órgão não cadastra órgão
    [InlineData("aud@auditoria.com", false)]     // auditoria externa
    [InlineData("cgtic@sgdi.df.gov.br", false)]  // o comitê delibera, não cadastra
    [InlineData("comum@ses.df.gov.br", false)]   // sem papel PGIA
    public async Task CriarOrgao_SoSgdiEAdmin(string email, bool permitido)
    {
        var unidade = new Unidade { id = Guid.NewGuid(), Nome = "Unidade do órgão novo", CodigoExterno = "NOVO" };
        Context.Unidades.Add(unidade);
        await Context.SaveChangesAsync();

        var resultado = await ComoGovernancaController(email).CriarOrgao(new PgiaOrgaoCreateDTO
        {
            Nome = "Órgão novo",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.Autarquia,
            UnidadeId = unidade.id
        });

        if (permitido)
        {
            Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal(3, await Context.PgiaOrgaos.CountAsync());
        }
        else
        {
            Assert.IsType<ForbidResult>(resultado);
            Assert.Equal(2, await Context.PgiaOrgaos.CountAsync()); // nada foi criado
        }
    }

    // ── POST governanca/pessoa (pré-cadastro por e-mail) ──────────────────────

    [Fact]
    public async Task PreCadastro_CriaPessoaAntesDoPrimeiroLogin()
    {
        var cadastro = Cadastro(await ComoGovernancaController(UserSgdi.Email).CriarPessoa(new PgiaPessoaCadastroDTO
        {
            Email = "novo.responsavel@ses.df.gov.br",
            Nome = "Nara Nova",
            PapelPgia = "pgia_orgao",
            UnidadeId = UnidadeSes.id
        }));

        Assert.False(cadastro.JaExistia); // pessoa nova
        Assert.Equal("pgia_orgao", cadastro.PapelPgia);
        Assert.Equal(OrgaoSes.Id, cadastro.OrgaoId);
        Assert.Equal("SES", cadastro.OrgaoSigla);

        var salvo = await Context.Users.Include(u => u.Unidade)
            .FirstAsync(u => u.Email == "novo.responsavel@ses.df.gov.br");
        Assert.Null(salvo.KeycloakId); // o primeiro login preenche (inclusive o Perfil, via claim)
        Assert.Equal(UnidadeSes.id, salvo.Unidade?.id);
    }

    [Fact]
    public async Task PreCadastro_EmailExistenteAplicaVinculoSemDuplicar()
    {
        var cadastro = await _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
        {
            // Mesma pessoa da base, com o e-mail digitado em outra caixa
            Email = "AUD@Auditoria.COM",
            Nome = "Alice Auditora (grafia diferente)",
            PapelPgia = "pgia_orgao",
            UnidadeId = UnidadeSeec.id
        });

        Assert.True(cadastro.JaExistia);
        Assert.Equal(UserAuditoria.Id, cadastro.UserId);
        Assert.Equal("pgia_orgao", cadastro.PapelPgia);
        Assert.Equal(OrgaoSeec.Id, cadastro.OrgaoId);

        var comEsseEmail = await Context.Users
            .Where(u => u.Email.ToLower() == "aud@auditoria.com")
            .ToListAsync();
        Assert.Single(comEsseEmail);
        // O cadastro existente não é reescrito: só o vínculo muda
        Assert.Equal("Alice Auditora", comEsseEmail[0].Nome);
    }

    /// <summary>
    /// C1 (destrutivo): pré-cadastrar um e-mail já ativo SEM escolher papel/órgão
    /// NÃO pode apagar o vínculo que a pessoa já tem — nulo aqui é "não mexer".
    /// </summary>
    [Fact]
    public async Task PreCadastro_EmailExistenteSemCamposNaoLimpaVinculo()
    {
        // UserOrgaoSes já é pgia_orgao na unidade SES
        var cadastro = await _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
        {
            Email = UserOrgaoSes.Email,
            Nome = "Maria (reenviada sem papel/unidade)",
            PapelPgia = null,
            UnidadeId = null
        });

        Assert.True(cadastro.JaExistia);
        Assert.Equal("pgia_orgao", cadastro.PapelPgia); // preservado
        Assert.Equal(OrgaoSes.Id, cadastro.OrgaoId);    // preservado

        var salvo = await Context.Users.Include(u => u.Unidade).FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Equal("pgia_orgao", salvo.PapelPgia);
        Assert.Equal(UnidadeSes.id, salvo.Unidade?.id);
    }

    /// <summary>
    /// C1 parcial: só o campo informado muda. Aqui vem só o papel; a unidade fica.
    /// </summary>
    [Fact]
    public async Task PreCadastro_EmailExistenteAplicaSoOCampoInformado()
    {
        var cadastro = await _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
        {
            Email = UserOrgaoSes.Email,
            Nome = "Maria",
            PapelPgia = "pgia_sgdi", // muda só o papel
            UnidadeId = null         // unidade preservada
        });

        Assert.Equal("pgia_sgdi", cadastro.PapelPgia);
        Assert.Equal(UnidadeSes.id, cadastro.UnidadeId); // preservada

        var salvo = await Context.Users.Include(u => u.Unidade).FirstAsync(u => u.Id == UserOrgaoSes.Id);
        Assert.Equal("pgia_sgdi", salvo.PapelPgia);
        Assert.Equal(UnidadeSes.id, salvo.Unidade?.id);
    }

    /// <summary>
    /// C2 (dedup): e-mail novo com caixa mista é gravado em minúsculas, então o
    /// 1º login (que o Keycloak normaliza) encontra a MESMA linha, sem duplicar.
    /// </summary>
    [Fact]
    public async Task PreCadastro_NovoEmailGravadoEmMinusculas()
    {
        var cadastro = await _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
        {
            Email = "Novo.Servidor@SES.DF.GOV.BR",
            Nome = "Novo Servidor",
            PapelPgia = "pgia_orgao",
            UnidadeId = UnidadeSes.id
        });

        Assert.False(cadastro.JaExistia);
        Assert.Equal("novo.servidor@ses.df.gov.br", cadastro.Email);

        // Primeiro login com o e-mail normalizado pelo Keycloak: mesma linha
        var logada = await new AuthRepositorio(Context).GetOrCreateUserAsync(
            "kc-novo-1", "NOVO SERVIDOR", "novo.servidor@ses.df.gov.br", "basico");
        Assert.Equal(cadastro.UserId, logada.Id);
        Assert.Single(await Context.Users
            .Where(u => u.Email.ToLower() == "novo.servidor@ses.df.gov.br").ToListAsync());
    }

    /// <summary>
    /// O primeiro login assume o cadastro prévio pelo fallback de e-mail do
    /// GetOrCreateUserAsync (intocado) e preserva unidade e papel PGIA.
    /// </summary>
    [Fact]
    public async Task PreCadastro_PrimeiroLoginAssumeALinhaEPreservaOVinculo()
    {
        var criada = await _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
        {
            Email = "nara@ses.df.gov.br",
            Nome = "Nara Nova",
            PapelPgia = "pgia_orgao",
            UnidadeId = UnidadeSes.id
        });

        var logada = await new AuthRepositorio(Context).GetOrCreateUserAsync(
            "keycloak-abc-123", "NARA NOVA DA SILVA", "nara@ses.df.gov.br");

        Assert.Equal(criada.UserId, logada.Id); // mesma linha, não um segundo cadastro
        Assert.Equal("keycloak-abc-123", logada.KeycloakId);
        Assert.Equal("pgia_orgao", logada.PapelPgia);       // vínculo do PGIA preservado
        Assert.Equal(UnidadeSes.id, logada.Unidade?.id);    // unidade preservada
        Assert.Equal("NARA NOVA DA SILVA", logada.Nome);
        Assert.Single(await Context.Users.Where(u => u.Email == "nara@ses.df.gov.br").ToListAsync());
    }

    [Theory]
    [InlineData("", "Nome Válido")]
    [InlineData("   ", "Nome Válido")]
    [InlineData("valido@ses.df.gov.br", "")]
    [InlineData("sem-arroba", "Nome Válido")]
    [InlineData("sem-ponto@dominio", "Nome Válido")]
    [InlineData("Apelido <a@b.df.gov.br>", "Nome Válido")]
    public async Task PreCadastro_EmailOuNomeInvalidoEhRejeitado(string email, string nome)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
            {
                Email = email,
                Nome = nome
            }));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Equal(7, await Context.Users.CountAsync()); // nada foi criado
    }

    [Fact]
    public async Task PreCadastro_PapelInvalidoEhRejeitado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governancaService.CriarOuVincularPessoaAsync(new PgiaPessoaCadastroDTO
            {
                Email = "nara@ses.df.gov.br",
                Nome = "Nara Nova",
                PapelPgia = "pgia_chefe"
            }));

        Assert.Equal((int)ErrorCode.PgiaPapelInvalido, ex.Error.Code);
        Assert.Equal(7, await Context.Users.CountAsync());
    }

    // ── Autorização dos endpoints novos ───────────────────────────────────────

    [Theory]
    [InlineData("sgdi@sgdi.df.gov.br", true)]
    [InlineData("admin@subgd.df.gov.br", true)]
    [InlineData("maria@ses.df.gov.br", false)]   // papel de órgão não administra vínculo
    [InlineData("aud@auditoria.com", false)]     // auditoria externa
    [InlineData("cgtic@sgdi.df.gov.br", false)]  // o comitê delibera, não cadastra pessoa
    [InlineData("comum@ses.df.gov.br", false)]   // sem papel PGIA
    public async Task GestaoDePessoas_SoSgdiEAdmin(string email, bool permitido)
    {
        var listagem = await ComoGovernancaController(email).ListarPessoas(null, null);
        var vinculo = await ComoGovernancaController(email)
            .AtualizarVinculoPessoa(UserSemPapel.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao" });
        var preCadastro = await ComoGovernancaController(email).CriarPessoa(new PgiaPessoaCadastroDTO
        {
            Email = "pre.cadastrada@ses.df.gov.br",
            Nome = "Pré Cadastrada",
            PapelPgia = "pgia_orgao",
            UnidadeId = UnidadeSes.id
        });

        if (permitido)
        {
            Assert.IsType<OkObjectResult>(listagem);
            Assert.IsType<OkObjectResult>(vinculo);
            Assert.IsType<OkObjectResult>(preCadastro);
        }
        else
        {
            Assert.IsType<ForbidResult>(listagem);
            Assert.IsType<ForbidResult>(vinculo);
            Assert.IsType<ForbidResult>(preCadastro);

            var salvo = await Context.Users.AsNoTracking().FirstAsync(u => u.Id == UserSemPapel.Id);
            Assert.Null(salvo.PapelPgia);
            Assert.Empty(await Context.Users.Where(u => u.Email == "pre.cadastrada@ses.df.gov.br").ToListAsync());
        }
    }
}
