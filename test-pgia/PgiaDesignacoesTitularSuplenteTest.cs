using System.Collections;
using System.Reflection;
using System.Security.Claims;
using api.Pgia;
using app.Models;
using Controllers.Pgia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio;
using Repositorio.Interface;
using Repositorio.Pgia;
using service;
using service.Acesso;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Designações com titular e suplente, Curador de Dados de Negócio e o ato pendente da
/// carga da planilha da SGDI (pedido de 2026-10-22). Exercita as actions dos controllers
/// (onde o gate mora), o serviço e, por inspeção, a migration (o InMemory não roda
/// migration nem índices parciais: a garantia do banco foi conferida num PostgreSQL).
/// </summary>
public class PgiaDesignacoesTitularSuplenteTest : PgiaTestBase
{
    private const string Titular = PgiaDominios.PosicaoDesignacao.Titular;
    private const string Suplente = PgiaDominios.PosicaoDesignacao.Suplente;

    private readonly PgiaPermissionService _permissionService;
    private readonly PgiaOrgaoService _orgaoService;
    private readonly PgiaOrgaoController _orgaoController;
    private readonly PgiaGovernancaController _governancaController;
    private readonly PgiaSistemaService _sistemaService;

    // Segunda pessoa da SES (equipe do órgão), para titular e suplente
    private readonly User _outraSes;

    public PgiaDesignacoesTitularSuplenteTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _orgaoService = new PgiaOrgaoService(
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            new PgiaPrazoRepositorio(Context),
            _permissionService,
            Context);
        _orgaoController = new PgiaOrgaoController(_orgaoService, _permissionService);

        _sistemaService = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);

        _governancaController = NovoGovernancaController(new PgiaGovernancaRepositorio(Context));

        _outraSes = new User { Email = "jose@ses.df.gov.br", Nome = "José Lima", PapelPgia = "pgia_orgao", Unidade = UnidadeSes };
        Context.Users.Add(_outraSes);
        PerfilPorUserId[_outraSes.Id] = "basico";
        PerfilPorEmail[_outraSes.Email] = "basico";
        Context.SaveChanges();
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private PgiaGovernancaController NovoGovernancaController(IPgiaGovernancaRepositorio repositorio) =>
        new(
            new PgiaGovernancaService(repositorio),
            _sistemaService,
            new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context)),
            new PgiaAdminService(new PgiaOrgaoRepositorio(Context), new PgiaPrazoRepositorio(Context), Context),
            _permissionService,
            new AcessoModuloService(Context));

    private T Como<T>(T controller, string email) where T : ControllerBase
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

    private static PgiaDesignacaoCreateDTO NovaDesignacao(Guid agenteId, string? posicao = null, string numero = "214/2026",
        DateOnly? inicio = null) => new()
    {
        AgenteId = agenteId,
        Posicao = posicao,
        AtoTipo = "Portaria",
        AtoNumero = numero,
        AtoData = new DateOnly(2026, 7, 20),
        ProcessoSeiComunicacao = "00060-00012345/2026-11",
        DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
        InicioVigencia = inicio ?? new DateOnly(2026, 7, 20)
    };

    private static PgiaResponsavelIaCreateDTO NovoResponsavel(Guid agenteId, string? posicao = null, string numero = "214/2026",
        DateOnly? inicio = null) => new()
    {
        AgenteId = agenteId,
        Posicao = posicao,
        AtoTipo = "Portaria",
        AtoNumero = numero,
        AtoData = new DateOnly(2026, 7, 20),
        ProcessoSeiComunicacao = "00060-00012345/2026-11",
        DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
        InicioVigencia = inicio ?? new DateOnly(2026, 7, 20)
    };

    private static PgiaDesignacaoAtualizarDTO AtoCompleto(string numero = "300/2026") => new()
    {
        AtoTipo = "Portaria",
        AtoNumero = numero,
        AtoData = new DateOnly(2026, 8, 1),
        ProcessoSeiComunicacao = "00060-00099999/2026-11",
        DataComunicacaoSgdi = new DateOnly(2026, 8, 3),
        InicioVigencia = new DateOnly(2026, 8, 1)
    };

    /// <summary>Linha como a carga SQL da planilha grava: ato pendente, sem ato, sem datas.</summary>
    private T Importada<T>(T designacao, PgiaOrgao orgao, User agente, string posicao, string? processoSei = null)
        where T : class, IPgiaDesignacao
    {
        designacao.OrgaoId = orgao.Id;
        designacao.AgenteId = agente.Id;
        designacao.Posicao = posicao;
        designacao.AtoPendente = true;
        designacao.ProcessoSeiComunicacao = processoSei;
        designacao.Ativo = true;
        designacao.CriadoEm = DateTime.UtcNow;
        designacao.CriadoPor = "carga-planilha-sgdi";
        Context.Set<T>().Add(designacao);
        Context.SaveChanges();
        return designacao;
    }

    private static PgiaDesignacaoResponse Resposta(IActionResult resultado) =>
        Assert.IsType<PgiaDesignacaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static List<PgiaDesignacaoResponse> Lista(IActionResult resultado) =>
        Assert.IsType<List<PgiaDesignacaoResponse>>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static List<PgiaPessoaAcesso> Pessoas(IActionResult resultado) =>
        Assert.IsType<List<PgiaPessoaAcesso>>(Assert.IsType<OkObjectResult>(resultado).Value);

    // ── Titular e suplente ────────────────────────────────────────────────────

    [Fact]
    public async Task Designar_SemPosicao_EhTitular_EOFrontAntigoContinuaFuncionando()
    {
        var resposta = Resposta(await Como(_orgaoController, UserOrgaoSes.Email)
            .DesignarResponsavelIa(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id)));

        Assert.Equal(Titular, resposta.Posicao);
        Assert.False(resposta.AtoPendente);
        Assert.True(resposta.Ativo);
        Assert.Equal("Maria Andrade", resposta.AgenteNome);
    }

    [Theory]
    [InlineData("suplente", Suplente)]
    [InlineData(" SUPLENTE ", Suplente)]
    [InlineData("Titular", Titular)]
    [InlineData("", Titular)]
    public async Task Designar_PosicaoSemDiferenciarMaiusculas(string posicao, string esperada)
    {
        var resposta = await _orgaoService.DesignarEncarregadoDadosAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, posicao), UserOrgaoSes.Email);

        Assert.Equal(esperada, resposta.Posicao);
    }

    [Fact]
    public async Task Designar_PosicaoForaDoDominio_EhRecusada()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarCuradorDadosAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, "Substituto"), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Empty(Context.PgiaCuradoresDados);
    }

    [Fact]
    public async Task TitularESuplente_FicamVigentesAoMesmoTempo()
    {
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(_outraSes.Id, Suplente), UserOrgaoSes.Email);

        var vigentes = await Context.PgiaResponsaveisIa.AsNoTracking().Where(r => r.Ativo).ToListAsync();
        Assert.Equal(2, vigentes.Count);
        Assert.Equal(UserOrgaoSes.Id, vigentes.Single(r => r.Posicao == Titular).AgenteId);
        Assert.Equal(_outraSes.Id, vigentes.Single(r => r.Posicao == Suplente).AgenteId);

        var meu = await _orgaoService.GetMeuOrgaoAsync((await _permissionService.GetContextAsync(UserOrgaoSes.Email, "gestor"))!);
        Assert.Equal(UserOrgaoSes.Id, meu.ResponsavelIaVigente!.AgenteId);
        Assert.Equal(_outraSes.Id, meu.ResponsavelIaSuplenteVigente!.AgenteId);
        Assert.Null(meu.EncarregadoDadosVigente);
        Assert.Null(meu.CuradorDadosVigente);
    }

    [Fact]
    public async Task TrocaDoTitular_EncerraSoOTitular_ESuplenteContinua()
    {
        var maria = UserOrgaoSes;
        var terceira = new User { Email = "ana@ses.df.gov.br", Nome = "Ana Souza", Unidade = UnidadeSes };
        Context.Users.Add(terceira);
        Context.SaveChanges();

        await _orgaoService.DesignarEncarregadoDadosAsync(OrgaoSes.Id, NovaDesignacao(maria.Id, Titular, "1/2026"), maria.Email);
        var suplente = await _orgaoService.DesignarEncarregadoDadosAsync(OrgaoSes.Id, NovaDesignacao(_outraSes.Id, Suplente, "2/2026"), maria.Email);

        var novoTitular = await _orgaoService.DesignarEncarregadoDadosAsync(OrgaoSes.Id,
            NovaDesignacao(terceira.Id, Titular, "3/2026", new DateOnly(2026, 9, 1)), maria.Email);

        var todas = await Context.PgiaEncarregadosDados.AsNoTracking().ToListAsync();
        Assert.Equal(3, todas.Count);
        var antigo = todas.Single(e => e.AtoNumero == "1/2026");
        Assert.False(antigo.Ativo);
        Assert.Equal(new DateOnly(2026, 9, 1), antigo.FimVigencia);
        Assert.True(todas.Single(e => e.Id == suplente.Id).Ativo);
        Assert.Null(todas.Single(e => e.Id == suplente.Id).FimVigencia);
        Assert.True(todas.Single(e => e.Id == novoTitular.Id).Ativo);

        var historico = await _orgaoService.ListarEncarregadosDadosAsync(OrgaoSes.Id);
        Assert.Equal(3, historico.Count);
    }

    [Fact]
    public async Task TrocaDoSuplente_EncerraSoOSuplente()
    {
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, Titular, "1/2026"), UserOrgaoSes.Email);
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(_outraSes.Id, Suplente, "2/2026"), UserOrgaoSes.Email);
        // O mesmo suplente redesignado (novo ato) substitui o anterior da posição
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(_outraSes.Id, Suplente, "3/2026"), UserOrgaoSes.Email);

        var vigentes = await Context.PgiaCuradoresDados.AsNoTracking().Where(c => c.Ativo).ToListAsync();
        Assert.Equal(new[] { "1/2026", "3/2026" }, vigentes.Select(c => c.AtoNumero).OrderBy(n => n));
        Assert.False(Context.PgiaCuradoresDados.AsNoTracking().Single(c => c.AtoNumero == "2/2026").Ativo);
    }

    [Fact]
    public async Task MesmaPessoa_NaoEhTitularESuplenteDaMesmaDesignacao()
    {
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarResponsavelIaAsync(
            OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Suplente), UserOrgaoSes.Email));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Contains("titular vigente", ex.Error.Message);
        Assert.Single(Context.PgiaResponsaveisIa.AsNoTracking().Where(r => r.Ativo));
    }

    [Fact]
    public async Task MesmaPessoa_PodeTerDesignacoesDiferentes()
    {
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);
        await _orgaoService.DesignarEncarregadoDadosAsync(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, Suplente), UserOrgaoSes.Email);
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);

        Assert.Single(Context.PgiaResponsaveisIa.AsNoTracking().Where(r => r.Ativo));
        Assert.Single(Context.PgiaEncarregadosDados.AsNoTracking().Where(r => r.Ativo));
        Assert.Single(Context.PgiaCuradoresDados.AsNoTracking().Where(r => r.Ativo));
    }

    [Fact]
    public async Task Inventario_UsaOTitular_ESemEleOSuplente()
    {
        // Só o suplente (da carga, com o ato pendente): o órgão já pode cadastrar sistemas
        var suplente = Importada(new PgiaResponsavelIa(), OrgaoSes, _outraSes, Suplente);
        var repositorio = new PgiaDesignacaoRepositorio(Context);
        Assert.Equal(suplente.Id, (await repositorio.GetResponsavelVigenteAsync(OrgaoSes.Id))!.Id);

        var titular = await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);
        Assert.Equal(titular.Id, (await repositorio.GetResponsavelVigenteAsync(OrgaoSes.Id))!.Id);
    }

    // ── Curador de Dados de Negócio: CRUD e permissões ────────────────────────

    [Fact]
    public async Task Curador_OrgaoDesignaListaEAtualizaNoProprioOrgao()
    {
        var controller = Como(_orgaoController, UserOrgaoSes.Email);

        var criado = Resposta(await controller.DesignarCuradorDados(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id)));
        Assert.Equal(Titular, criado.Posicao);
        Assert.Equal("214/2026", criado.AtoNumero);
        Assert.Null(criado.AcumulaFuncaoTic);

        var lista = Lista(await controller.ListarCuradoresDados(OrgaoSes.Id));
        Assert.Equal(criado.Id, Assert.Single(lista).Id);

        var atualizado = Resposta(await controller.AtualizarCuradorDados(OrgaoSes.Id, criado.Id, AtoCompleto("999/2026")));
        Assert.Equal("999/2026", atualizado.AtoNumero);
        Assert.Equal(new DateOnly(2026, 8, 1), atualizado.InicioVigencia);

        var salvo = await Context.PgiaCuradoresDados.AsNoTracking().SingleAsync();
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);
        Assert.Equal(UserOrgaoSes.Email, salvo.CriadoPor);
    }

    [Fact]
    public async Task Curador_OutroOrgao_CgticEAuditoria_RecebemForbidNaEscrita()
    {
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserOrgaoSeec.Email)
            .DesignarCuradorDados(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id)));
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserCgtic.Email)
            .DesignarCuradorDados(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id)));
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserAuditoria.Email)
            .ListarCuradoresDados(OrgaoSes.Id));
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserSemPapel.Email)
            .ListarCuradoresDados(OrgaoSes.Id));
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserOrgaoSeec.Email)
            .ListarCuradoresDados(OrgaoSes.Id));
        Assert.Empty(Context.PgiaCuradoresDados);
    }

    [Fact]
    public async Task Curador_SgdiEAdminVeem_SgdiNaoDesigna()
    {
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id), UserOrgaoSes.Email);

        Assert.Single(Lista(await Como(_orgaoController, UserSgdi.Email).ListarCuradoresDados(OrgaoSes.Id)));
        Assert.Single(Lista(await Como(_orgaoController, UserAdmin.Email).ListarCuradoresDados(OrgaoSes.Id)));
        Assert.Single(Lista(await Como(_orgaoController, UserCgtic.Email).ListarCuradoresDados(OrgaoSes.Id)));

        // Designar continua só do órgão (e do admin), como o Encarregado
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserSgdi.Email)
            .DesignarCuradorDados(OrgaoSes.Id, NovaDesignacao(_outraSes.Id, Suplente)));
        Resposta(await Como(_orgaoController, UserAdmin.Email)
            .DesignarCuradorDados(OrgaoSes.Id, NovaDesignacao(_outraSes.Id, Suplente)));
    }

    [Fact]
    public async Task Curador_PessoaDeOutraUnidade_EDocumentoDeOutroOrgao_SaoRecusados()
    {
        var pessoaFora = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarCuradorDadosAsync(
            OrgaoSes.Id, NovaDesignacao(UserOrgaoSeec.Id), UserOrgaoSes.Email));
        Assert.Equal((int)ErrorCode.PgiaAgenteDeOutroOrgao, pessoaFora.Error.Code);

        var doc = new PgiaDocumento
        {
            Tipo = "Ato de designação", OrgaoId = OrgaoSeec.Id, NomeArquivo = "ato.pdf",
            DataEnvio = DateTime.UtcNow, EnviadoPor = UserOrgaoSeec.Id, CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(doc);
        Context.SaveChanges();

        var dto = NovaDesignacao(UserOrgaoSes.Id);
        dto.DocumentoId = doc.Id;
        var docFora = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, dto, UserOrgaoSes.Email));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, docFora.Error.Code);
    }

    [Fact]
    public async Task Curador_DocumentoDoProprioOrgao_FicaNaDesignacao_ComFkRestrict()
    {
        var doc = new PgiaDocumento
        {
            Tipo = "Ato de designação", OrgaoId = OrgaoSes.Id, NomeArquivo = "ato.pdf",
            DataEnvio = DateTime.UtcNow, EnviadoPor = UserOrgaoSes.Id, CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(doc);
        Context.SaveChanges();

        var dto = NovaDesignacao(UserOrgaoSes.Id);
        dto.DocumentoId = doc.Id;
        var resposta = await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, dto, UserOrgaoSes.Email);
        Assert.Equal(doc.Id, resposta.DocumentoId);

        var fk = Context.Model.FindEntityType(typeof(PgiaCuradorDados))!
            .GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(PgiaDocumento));
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Contains("Ato de designação", PgiaDominios.TipoDocumento.Todos);
    }

    [Fact]
    public void Matriz_CuradorComoODesignacao_SgdiEditaOAto_SemMudarOResto()
    {
        PgiaUserContext Ctx(string papel, bool admin = false) => new()
        {
            Email = "x@x", Perfil = admin ? "admin" : "basico", PapelPgia = admin ? null : papel, OrgaoId = OrgaoSes.Id
        };

        var orgao = Ctx("pgia_orgao");
        var sgdi = Ctx("pgia_sgdi");
        var cgtic = Ctx("pgia_cgtic");
        var auditoria = Ctx("pgia_auditoria");
        var admin = Ctx("", admin: true);

        foreach (var recurso in new[] { PgiaResources.Designacao, PgiaResources.CuradorDados })
        {
            Assert.True(_permissionService.CanCreate(orgao, recurso));
            Assert.True(_permissionService.CanEdit(orgao, recurso));
            Assert.True(_permissionService.CanCreate(admin, recurso));
            Assert.True(_permissionService.CanEdit(admin, recurso));
            Assert.False(_permissionService.CanCreate(sgdi, recurso));
            Assert.True(_permissionService.CanEdit(sgdi, recurso));
            Assert.True(_permissionService.CanView(sgdi, recurso));
            Assert.False(_permissionService.CanCreate(cgtic, recurso));
            Assert.False(_permissionService.CanEdit(cgtic, recurso));
            Assert.False(_permissionService.CanView(auditoria, recurso));
        }

        // O que já valia para a SGDI segue igual (amostra da matriz)
        Assert.False(_permissionService.CanCreate(sgdi, PgiaResources.Sistema));
        Assert.False(_permissionService.CanEdit(sgdi, PgiaResources.Sistema));
        Assert.True(_permissionService.CanEdit(sgdi, PgiaResources.PessoaVinculo));
    }

    // ── C7: a troca de unidade considera o curador ────────────────────────────

    [Fact]
    public async Task C7_CuradorVigente_ImpedeTrocarAUnidadeParaForaDoOrgao()
    {
        Importada(new PgiaCuradorDados(), OrgaoSes, _outraSes, Suplente);

        var ex = await Assert.ThrowsAsync<ApiException>(() => Como(_governancaController, UserSgdi.Email)
            .AtualizarVinculoPessoa(_outraSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSeec.id }));

        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
        Assert.Contains("Curador de Dados de Negócio suplente vigente da SES", ex.Error.Message);

        // Na mesma unidade passa
        Assert.IsType<OkObjectResult>(await Como(_governancaController, UserSgdi.Email)
            .AtualizarVinculoPessoa(_outraSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSes.id }));
    }

    [Fact]
    public async Task C7_SuplenteDoResponsavel_TambemImpedeATroca()
    {
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(_outraSes.Id, Suplente), UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() => Como(_governancaController, UserSgdi.Email)
            .AtualizarVinculoPessoa(_outraSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = null }));
        Assert.Equal((int)ErrorCode.PgiaDesignacaoVigenteImpedeTroca, ex.Error.Code);
        Assert.Contains("Responsável de IA suplente vigente da SES", ex.Error.Message);
    }

    [Fact]
    public async Task C7_CuradorEncerrado_NaoImpede()
    {
        var curador = Importada(new PgiaCuradorDados(), OrgaoSes, _outraSes, Titular);
        curador.Ativo = false;
        Context.SaveChanges();

        Assert.IsType<OkObjectResult>(await Como(_governancaController, UserSgdi.Email)
            .AtualizarVinculoPessoa(_outraSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSeec.id }));
    }

    [Fact]
    public async Task Login_CuradorVigente_NaoTrocaAUnidadePeloGrupoDoToken()
    {
        Importada(new PgiaCuradorDados(), OrgaoSes, _outraSes, Titular);

        var user = await new AuthRepositorio(Context).GetOrCreateUserAsync("kc-jose", "José Lima", _outraSes.Email, "SEEC-GRUPO");

        Assert.Equal(UnidadeSes.id, user.Unidade!.id);
    }

    // ── Ato pendente da carga ─────────────────────────────────────────────────

    [Fact]
    public void AtoPendente_CheckEPosicao_NasTresTabelas_ComOMesmoTexto()
    {
        foreach (var tipo in new[] { typeof(PgiaResponsavelIa), typeof(PgiaEncarregadoDados), typeof(PgiaCuradorDados) })
        {
            var entidade = Context.Model.FindEntityType(tipo)!;
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.AtoTipo))!.IsNullable);
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.AtoNumero))!.IsNullable);
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.AtoData))!.IsNullable);
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.ProcessoSeiComunicacao))!.IsNullable);
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.DataComunicacaoSgdi))!.IsNullable);
            Assert.True(entidade.FindProperty(nameof(IPgiaDesignacao.InicioVigencia))!.IsNullable);
            Assert.False(entidade.FindProperty(nameof(IPgiaDesignacao.Posicao))!.IsNullable);
            Assert.False(entidade.FindProperty(nameof(IPgiaDesignacao.AtoPendente))!.IsNullable);
        }

        Assert.StartsWith("ato_pendente OR (", PgiaModelChecks.AtoPendente);
        foreach (var coluna in new[] { "ato_tipo", "ato_numero", "ato_data", "processo_sei_comunicacao", "data_comunicacao_sgdi", "inicio_vigencia" })
            Assert.Contains($"{coluna} IS NOT NULL", PgiaModelChecks.AtoPendente);
        Assert.Equal("posicao IN ('Titular','Suplente')", PgiaModelChecks.Posicao);
    }

    [Fact]
    public async Task CriacaoPelaApi_ExigeTudo_EAMarcaNuncaNasceLigada()
    {
        var semProcesso = NovaDesignacao(UserOrgaoSes.Id);
        semProcesso.ProcessoSeiComunicacao = "  ";
        var ex1 = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, semProcesso, UserOrgaoSes.Email));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex1.Error.Code);

        var semInicio = NovoResponsavel(UserOrgaoSes.Id);
        semInicio.InicioVigencia = default;
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, semInicio, UserOrgaoSes.Email));
        Assert.Contains("início da vigência", ex2.Error.Message);

        var semDataComunicacao = NovaDesignacao(UserOrgaoSes.Id);
        semDataComunicacao.DataComunicacaoSgdi = default;
        await Assert.ThrowsAsync<ApiException>(() => _orgaoService.DesignarEncarregadoDadosAsync(OrgaoSes.Id, semDataComunicacao, UserOrgaoSes.Email));

        Assert.Empty(Context.PgiaCuradoresDados);
        Assert.Empty(Context.PgiaResponsaveisIa);
        Assert.Empty(Context.PgiaEncarregadosDados);

        // A marca não existe no corpo da criação
        Assert.Null(typeof(PgiaDesignacaoCreateDTO).GetProperty("AtoPendente"));
        Assert.Null(typeof(PgiaDesignacaoAtualizarDTO).GetProperty("AtoPendente"));
    }

    [Fact]
    public async Task Completar_ComTudoPreenchido_DesligaAMarca_ResponsavelComAcumulacao()
    {
        var importada = Importada(new PgiaResponsavelIa(), OrgaoSes, UserOrgaoSes, Titular, "00060-00011111/2026-11");

        var dto = new PgiaResponsavelIaAtualizarDTO
        {
            AtoTipo = "Portaria", AtoNumero = "77/2026", AtoData = new DateOnly(2026, 8, 1),
            ProcessoSeiComunicacao = "00060-00011111/2026-11", DataComunicacaoSgdi = new DateOnly(2026, 8, 2),
            InicioVigencia = new DateOnly(2026, 8, 1), AcumulaFuncaoTic = true, CapacitacaoAdequada = true
        };
        var resposta = Resposta(await Como(_orgaoController, UserOrgaoSes.Email).AtualizarResponsavelIa(OrgaoSes.Id, importada.Id, dto));

        Assert.False(resposta.AtoPendente);
        Assert.Equal("77/2026", resposta.AtoNumero);
        Assert.True(resposta.AcumulaFuncaoTic);
        Assert.True(resposta.CapacitacaoAdequada);
        Assert.Equal(Titular, resposta.Posicao);
        Assert.Equal(UserOrgaoSes.Id, resposta.AgenteId);

        var salva = await Context.PgiaResponsaveisIa.AsNoTracking().SingleAsync();
        Assert.False(salva.AtoPendente);
        Assert.Equal(new DateOnly(2026, 8, 1), salva.InicioVigencia);
        Assert.True(salva.Ativo);
    }

    [Fact]
    public async Task Completar_PelaSgdi_EmQualquerOrgao()
    {
        var importada = Importada(new PgiaEncarregadoDados(), OrgaoSeec, UserOrgaoSeec, Suplente);

        var resposta = Resposta(await Como(_orgaoController, UserSgdi.Email)
            .AtualizarEncarregadoDados(OrgaoSeec.Id, importada.Id, AtoCompleto()));

        Assert.False(resposta.AtoPendente);
        Assert.Equal(Suplente, resposta.Posicao);
        Assert.Equal(UserSgdi.Email, (await Context.PgiaEncarregadosDados.AsNoTracking().SingleAsync()).AlteradoPor);
    }

    [Fact]
    public async Task Completar_CamposFaltando_EhRecusado_EAMarcaContinua()
    {
        var importada = Importada(new PgiaCuradorDados(), OrgaoSes, UserOrgaoSes, Titular);
        var incompleto = AtoCompleto();
        incompleto.AtoData = default;

        var ex = await Assert.ThrowsAsync<ApiException>(() => Como(_orgaoController, UserOrgaoSes.Email)
            .AtualizarCuradorDados(OrgaoSes.Id, importada.Id, incompleto));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);

        var salva = await Context.PgiaCuradoresDados.AsNoTracking().SingleAsync();
        Assert.True(salva.AtoPendente);
        Assert.Null(salva.AtoData);
    }

    [Fact]
    public async Task Completar_OutroOrgao_CgticEIdDeOutroOrgao_SaoRecusados()
    {
        var importada = Importada(new PgiaCuradorDados(), OrgaoSes, UserOrgaoSes, Titular);

        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserOrgaoSeec.Email)
            .AtualizarCuradorDados(OrgaoSes.Id, importada.Id, AtoCompleto()));
        Assert.IsType<ForbidResult>(await Como(_orgaoController, UserCgtic.Email)
            .AtualizarCuradorDados(OrgaoSes.Id, importada.Id, AtoCompleto()));

        // A designação da SES pedida pela rota da SEEC não existe para a SEEC
        var ex = await Assert.ThrowsAsync<ApiException>(() => Como(_orgaoController, UserSgdi.Email)
            .AtualizarCuradorDados(OrgaoSeec.Id, importada.Id, AtoCompleto()));
        Assert.Equal((int)ErrorCode.PgiaDesignacaoNaoEncontrada, ex.Error.Code);

        Assert.True((await Context.PgiaCuradoresDados.AsNoTracking().SingleAsync()).AtoPendente);
    }

    [Fact]
    public async Task Completar_DesignacaoEncerrada_InicioDepoisDoFim_EhRecusado()
    {
        var antiga = Importada(new PgiaEncarregadoDados(), OrgaoSes, UserOrgaoSes, Titular);
        antiga.Ativo = false;
        antiga.FimVigencia = new DateOnly(2026, 7, 31);
        Context.SaveChanges();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _orgaoService.AtualizarEncarregadoDadosAsync(
            OrgaoSes.Id, antiga.Id, AtoCompleto(), UserOrgaoSes.Email));
        Assert.Contains("31/07/2026", ex.Error.Message);

        var dto = AtoCompleto();
        dto.InicioVigencia = new DateOnly(2026, 3, 1);
        var completa = await _orgaoService.AtualizarEncarregadoDadosAsync(OrgaoSes.Id, antiga.Id, dto, UserOrgaoSes.Email);
        Assert.False(completa.AtoPendente);
        Assert.False(completa.Ativo);
    }

    [Fact]
    public async Task TrocaDeUmaImportada_SemInicioDeVigencia_FuncionaEFechaComOInicioDaNova()
    {
        var importada = Importada(new PgiaResponsavelIa(), OrgaoSes, _outraSes, Titular);

        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id,
            NovoResponsavel(UserOrgaoSes.Id, Titular, inicio: new DateOnly(2026, 9, 10)), UserOrgaoSes.Email);

        var antiga = await Context.PgiaResponsaveisIa.AsNoTracking().SingleAsync(r => r.Id == importada.Id);
        Assert.False(antiga.Ativo);
        Assert.True(antiga.AtoPendente);
        Assert.Equal(new DateOnly(2026, 9, 10), antiga.FimVigencia);
    }

    [Fact]
    public async Task LeiturasComNulo_NaoQuebram_ListasMeuOrgaoPessoasEInventario()
    {
        Importada(new PgiaResponsavelIa(), OrgaoSes, UserOrgaoSes, Titular);
        Importada(new PgiaEncarregadoDados(), OrgaoSes, _outraSes, Suplente, "00060-00011111/2026-11");
        Importada(new PgiaCuradorDados(), OrgaoSes, UserOrgaoSes, Titular);

        var orgao = Como(_orgaoController, UserOrgaoSes.Email);
        var responsaveis = Lista(await orgao.ListarResponsaveisIa(OrgaoSes.Id));
        var r = Assert.Single(responsaveis);
        Assert.True(r.AtoPendente);
        Assert.Null(r.AtoTipo);
        Assert.Null(r.AtoData);
        Assert.Null(r.InicioVigencia);
        Assert.Null(r.DataComunicacaoSgdi);
        Assert.False(r.AcumulaFuncaoTic);

        var e = Assert.Single(Lista(await orgao.ListarEncarregadosDados(OrgaoSes.Id)));
        Assert.Equal("00060-00011111/2026-11", e.ProcessoSeiComunicacao);
        Assert.Single(Lista(await orgao.ListarCuradoresDados(OrgaoSes.Id)));

        var meu = Assert.IsType<PgiaMeuOrgaoResponse>(Assert.IsType<OkObjectResult>(await orgao.GetMeuOrgao()).Value);
        Assert.True(meu.ResponsavelIaVigente!.AtoPendente);
        Assert.Null(meu.EncarregadoDadosVigente);
        Assert.Equal(_outraSes.Id, meu.EncarregadoDadosSuplenteVigente!.AgenteId);
        Assert.NotNull(meu.CuradorDadosVigente);

        var pessoas = Pessoas(await Como(_governancaController, UserSgdi.Email).ListarPessoas(null, OrgaoSes.Id));
        Assert.Equal(2, pessoas.Single(p => p.UserId == UserOrgaoSes.Id).Funcoes.Count);

        // O inventário aceita o Responsável com o ato pendente (é a pessoa designada)
        var ctx = (await _permissionService.GetContextAsync(UserOrgaoSes.Email, "gestor"))!;
        var sistema = await _sistemaService.CriarSistemaAsync(OrgaoSes.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = "Assistente 156",
            Finalidade = "Atendimento",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA generativa",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(new PgiaChecklistDTO()),
                OutrosRiscos = OutrosRiscosSeNecessario(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Avaliado pelo Responsável de IA."
            }
        }, ctx);
        Assert.Equal("Maria Andrade", sistema.ResponsavelNome);
    }

    // ── Pessoas e acessos: as funções de cada pessoa ──────────────────────────

    [Fact]
    public async Task Pessoas_TrazemAsFuncoesVigentes_ComPosicaoEOrgao_NaOrdem()
    {
        await _orgaoService.DesignarCuradorDadosAsync(OrgaoSes.Id, NovaDesignacao(UserOrgaoSes.Id, Suplente), UserOrgaoSes.Email);
        await _orgaoService.DesignarResponsavelIaAsync(OrgaoSes.Id, NovoResponsavel(UserOrgaoSes.Id, Titular), UserOrgaoSes.Email);
        Importada(new PgiaEncarregadoDados(), OrgaoSes, UserOrgaoSes, Titular);
        // Encerrada não aparece
        var encerrada = Importada(new PgiaEncarregadoDados(), OrgaoSeec, UserOrgaoSeec, Titular);
        encerrada.Ativo = false;
        Context.SaveChanges();

        var pessoas = Pessoas(await Como(_governancaController, UserSgdi.Email).ListarPessoas(null, null));

        var maria = pessoas.Single(p => p.UserId == UserOrgaoSes.Id);
        Assert.Equal(
            new[]
            {
                ("responsavel_ia", "Responsável de IA", Titular, false),
                ("encarregado_dados", "Encarregado de Dados", Titular, true),
                ("curador_dados", "Curador de Dados de Negócio", Suplente, false)
            },
            maria.Funcoes.Select(f => (f.Designacao, f.DesignacaoNome, f.Posicao, f.AtoPendente)));
        Assert.All(maria.Funcoes, f => Assert.Equal((OrgaoSes.Id, "SES"), (f.OrgaoId, f.OrgaoSigla)));
        Assert.All(maria.Funcoes, f => Assert.True(f.DesignacaoId > 0));

        Assert.Empty(pessoas.Single(p => p.UserId == UserOrgaoSeec.Id).Funcoes);
        Assert.Empty(pessoas.Single(p => p.UserId == UserSgdi.Id).Funcoes);
    }

    [Fact]
    public async Task Pessoas_FuncoesLidasEmLote_UmaChamadaPorListagem()
    {
        foreach (var i in Enumerable.Range(1, 15))
        {
            var pessoa = new User { Email = $"p{i}@ses.df.gov.br", Nome = $"Pessoa {i:00}", Unidade = UnidadeSes };
            Context.Users.Add(pessoa);
            Context.SaveChanges();
            if (i <= 2)
                Importada(new PgiaCuradorDados(), OrgaoSes, pessoa, i == 1 ? Titular : Suplente);
        }

        var contador = ContadorProxy.Criar(new PgiaGovernancaRepositorio(Context));
        var controller = NovoGovernancaController((IPgiaGovernancaRepositorio)contador);

        var pessoas = Pessoas(await Como(controller, UserSgdi.Email).ListarPessoas(null, null));

        Assert.True(pessoas.Count > 15);
        Assert.Equal(1, ((ContadorProxy)(object)contador).Chamadas(nameof(IPgiaGovernancaRepositorio.ListarFuncoesVigentesAsync)));
        Assert.Equal(2, pessoas.Count(p => p.Funcoes.Count == 1 && p.Funcoes[0].Designacao == "curador_dados"));
    }

    [Fact]
    public async Task Pessoas_PutVinculoEPreCadastro_TambemTrazemAsFuncoes()
    {
        Importada(new PgiaCuradorDados(), OrgaoSes, _outraSes, Titular);

        var vinculo = Assert.IsType<PgiaPessoaAcesso>(Assert.IsType<OkObjectResult>(await Como(_governancaController, UserSgdi.Email)
            .AtualizarVinculoPessoa(_outraSes.Id, new PgiaPessoaVinculoDTO { PapelPgia = "pgia_orgao", UnidadeId = UnidadeSes.id })).Value);
        Assert.Equal("curador_dados", Assert.Single(vinculo.Funcoes).Designacao);

        var cadastro = Assert.IsType<PgiaPessoaAcesso>(Assert.IsType<OkObjectResult>(await Como(_governancaController, UserSgdi.Email)
            .CriarPessoa(new PgiaPessoaCadastroDTO { Email = _outraSes.Email.ToUpperInvariant(), Nome = "José", PapelPgia = "pgia_orgao" })).Value);
        Assert.True(cadastro.JaExistia);
        Assert.Single(cadastro.Funcoes);
    }

    // ── Migration ─────────────────────────────────────────────────────────────

    private static List<object> Operacoes(string lado)
    {
        var tipo = typeof(PgiaOrgao).Assembly.GetType("demanda_service.Migrations.PgiaDesignacoesTitularSuplenteCurador");
        Assert.NotNull(tipo);
        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object objeto, string propriedade) =>
        objeto.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(objeto);

    [Fact]
    public void Migration_Up_SoObjetosPgia_IndicesPorOrgaoEPosicao_ChecksETabelaNova()
    {
        var operacoes = Operacoes("UpOperations");
        var tabelas = operacoes.Select(o => (string?)(Valor(o, "Table") ?? Valor(o, "Name"))).Distinct().ToList();
        Assert.All(tabelas, t => Assert.StartsWith("pgia_", t));
        Assert.DoesNotContain(operacoes, o => o.GetType().Name == "SqlOperation");

        var indices = operacoes.Where(o => o.GetType().Name == "CreateIndexOperation")
            .ToDictionary(o => (string)Valor(o, "Name")!,
                o => (Colunas: string.Join(",", (string[])Valor(o, "Columns")!), Unico: (bool)Valor(o, "IsUnique")!, Filtro: (string?)Valor(o, "Filter")));
        Assert.Equal(("orgao_id,posicao", true, "ativo"), indices["ux_pgia_responsavel_ia_vigente"]);
        Assert.Equal(("orgao_id,posicao", true, "ativo"), indices["ux_pgia_encarregado_vigente"]);
        Assert.Equal(("orgao_id,posicao", true, "ativo"), indices["ux_pgia_curador_dados_vigente"]);

        var checks = operacoes.Where(o => o.GetType().Name == "AddCheckConstraintOperation")
            .Select(o => (string)Valor(o, "Name")!).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(new[]
        {
            "ck_pgia_encarregado_dados_ato_pendente", "ck_pgia_encarregado_dados_posicao",
            "ck_pgia_responsavel_ia_ato_pendente", "ck_pgia_responsavel_ia_posicao"
        }, checks);

        // Linhas que já existem ficam titular e sem a marca (padrão das colunas novas)
        var novas = operacoes.Where(o => o.GetType().Name == "AddColumnOperation")
            .ToDictionary(o => $"{Valor(o, "Table")}.{Valor(o, "Name")}", o => Valor(o, "DefaultValue"));
        Assert.Equal("Titular", novas["pgia_responsavel_ia.posicao"]);
        Assert.Equal("Titular", novas["pgia_encarregado_dados.posicao"]);
        Assert.Equal(false, novas["pgia_responsavel_ia.ato_pendente"]);
        Assert.Equal(false, novas["pgia_encarregado_dados.ato_pendente"]);

        var tabela = Assert.Single(operacoes, o => o.GetType().Name == "CreateTableOperation");
        Assert.Equal("pgia_curador_dados", Valor(tabela, "Name"));
        var fks = ((IEnumerable)Valor(tabela, "ForeignKeys")!).Cast<object>()
            .Select(f => ((string)Valor(f, "Name")!, (string)Valor(f, "PrincipalTable")!, Valor(f, "OnDelete")!.ToString()))
            .OrderBy(f => f.Item1, StringComparer.Ordinal).ToList();
        Assert.Equal(new[]
        {
            ("fk_pgia_curador_dados_agente", "Users", "Restrict"),
            ("fk_pgia_curador_dados_doc", "pgia_documento", "Restrict"),
            ("fk_pgia_curador_dados_orgao", "pgia_orgao", "Restrict")
        }, fks.Select(f => (f.Item1, f.Item2, f.Item3!)));
    }

    [Fact]
    public void Migration_Down_AjustaOsDadosAntesDeVoltarOEsquemaAntigo()
    {
        var operacoes = Operacoes("DownOperations");
        var sqls = operacoes.Where(o => o.GetType().Name == "SqlOperation").Select(o => (string)Valor(o, "Sql")!).ToList();
        Assert.Equal(2, sqls.Count);
        Assert.All(sqls, s => Assert.Contains("posicao = 'Suplente'", s));
        Assert.All(sqls, s => Assert.Contains("WHERE ato_pendente", s));

        // Os ajustes vêm antes de tudo; nenhum AlterColumn deixa padrão na coluna
        Assert.Equal("SqlOperation", operacoes[0].GetType().Name);
        Assert.Equal("SqlOperation", operacoes[1].GetType().Name);
        Assert.All(operacoes.Where(o => o.GetType().Name == "AlterColumnOperation"),
            o => Assert.Null(Valor(o, "DefaultValue")));
    }

    /// <summary>Proxy que conta as chamadas do repositório (prova de leitura em lote).</summary>
    public class ContadorProxy : DispatchProxy
    {
        private object _alvo = null!;
        private readonly Dictionary<string, int> _chamadas = new();

        public static IPgiaGovernancaRepositorio Criar(IPgiaGovernancaRepositorio alvo)
        {
            var proxy = Create<IPgiaGovernancaRepositorio, ContadorProxy>();
            ((ContadorProxy)(object)proxy)._alvo = alvo;
            return proxy;
        }

        public int Chamadas(string metodo) => _chamadas.TryGetValue(metodo, out var n) ? n : 0;

        protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
        {
            _chamadas[metodo!.Name] = Chamadas(metodo.Name) + 1;
            return metodo.Invoke(_alvo, argumentos);
        }
    }
}
