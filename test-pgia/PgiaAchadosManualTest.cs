using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using api.Pgia;
using app.Models;
using Controllers.Pgia;
using demanda_service.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Acesso;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Achados do módulo PGIA encontrados ao escrever o manual do usuário: erro com
/// mensagem (filtro escopado nos controllers do PGIA), tipo do instrumento legado
/// que não cabia na coluna, datas no futuro, ata anexada depois do registro da
/// deliberação, parecer vazio e protocolo do cidadão digitado de outro jeito.
/// </summary>
public class PgiaAchadosManualTest : PgiaTestBase
{
    private readonly PgiaPermissionService _permissionService;
    private readonly PgiaGovernancaService _governanca;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaRelatorioService _relatorioService;
    private readonly PgiaOperacaoService _operacaoService;
    private readonly PgiaPublicoService _publicoService;
    private readonly PgiaContratoService _contratoService;

    public PgiaAchadosManualTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _governanca = new PgiaGovernancaService(new PgiaGovernancaRepositorio(Context));
        _sistemaService = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);
        _relatorioService = new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context));
        _operacaoService = new PgiaOperacaoService(new PgiaOperacaoRepositorio(Context));
        _publicoService = new PgiaPublicoService(new PgiaPublicoRepositorio(Context));
        _contratoService = new PgiaContratoService(new PgiaContratoRepositorio(Context));

        Context.PgiaResponsaveisIa.Add(new PgiaResponsavelIa
        {
            OrgaoId = OrgaoSes.Id,
            AgenteId = UserOrgaoSes.Id,
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

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email, PerfilDe(email)))!;

    private async Task<PgiaSistemaResponse> NovoSistemaAsync(string denominacao = "Assistente 156")
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        return await _sistemaService.CriarSistemaAsync(OrgaoSes.Id, new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar o atendimento",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA generativa",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            AfetaCidadao = false,
            InteroperavelPadroesSgdi = true,
            SupervisaoHumanaDescricao = "Servidor revisa cada decisão.",
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(),
                OutrosRiscos = OutrosRiscosSeNecessario(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Sem enquadramento nos arts. 15 a 17."
            }
        }, ctx);
    }

    private PgiaDocumento NovoDocumento(long? orgaoId, string nome = "ata.pdf")
    {
        var documento = new PgiaDocumento
        {
            Tipo = "Ata",
            OrgaoId = orgaoId,
            NomeArquivo = nome,
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = UserCgtic.Id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documento);
        Context.SaveChanges();
        return documento;
    }

    private static PgiaDeliberacaoCreateDTO NovaDeliberacaoDto(DateOnly data, long? documentoId = null) => new()
    {
        Tipo = "Resolução normativa",
        DataDeliberacao = data,
        Resultado = PgiaDominios.ResultadoDeliberacao.Favoravel,
        NumeroAto = "12/2026",
        DocumentoId = documentoId
    };

    private PgiaGovernancaController GovernancaComo(string email)
    {
        var controller = new PgiaGovernancaController(
            _governanca,
            _sistemaService,
            _relatorioService,
            new PgiaAdminService(new PgiaOrgaoRepositorio(Context), new PgiaPrazoRepositorio(Context), Context),
            _permissionService,
            new AcessoModuloService(Context));
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

    private static ExceptionContext ContextoDaExcecao(Exception ex) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>())
        {
            Exception = ex
        };

    // ── 1. Erro com mensagem: filtro escopado nos controllers do PGIA ─────────

    [Fact]
    public void Filtro_ApiExceptionViraBadRequestComCodeEMessage()
    {
        var contexto = ContextoDaExcecao(
            new ApiException(ErrorCode.PgiaDominioInvalido, "A data da deliberação não pode ser depois de hoje."));

        new PgiaErrosAttribute().OnException(contexto);

        Assert.True(contexto.ExceptionHandled);
        var resultado = Assert.IsType<BadRequestObjectResult>(contexto.Result);
        Assert.Equal(400, resultado.StatusCode);
        var corpo = Assert.IsType<PgiaErroResponse>(resultado.Value);
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, corpo.Code);
        Assert.Equal("A data da deliberação não pode ser depois de hoje.", corpo.Message);

        // PascalCase como o resto da API (PropertyNamingPolicy = null no Program.cs)
        var json = JsonSerializer.Serialize(resultado.Value, new JsonSerializerOptions { PropertyNamingPolicy = null });
        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Code", "Message" }, props);
        Assert.Equal(604, doc.RootElement.GetProperty("Code").GetInt32());
    }

    [Fact]
    public void Filtro_SemMensagemPropriaUsaADescricaoPadraoDoCodigo()
    {
        var contexto = ContextoDaExcecao(new ApiException(ErrorCode.PgiaSistemaNaoEncontrado));

        new PgiaErrosAttribute().OnException(contexto);

        var corpo = Assert.IsType<PgiaErroResponse>(Assert.IsType<BadRequestObjectResult>(contexto.Result).Value);
        Assert.Equal((int)ErrorCode.PgiaSistemaNaoEncontrado, corpo.Code);
        Assert.Equal("Sistema de IA não encontrado.", corpo.Message);
    }

    [Fact]
    public void Filtro_CodigoForaDoPgiaSemDescricaoUsaONomeDoCodigo()
    {
        var corpo = PgiaErrosAttribute.Corpo(new ApiException(ErrorCode.AcessoInvalido));
        Assert.Equal((int)ErrorCode.AcessoInvalido, corpo.Code);
        Assert.Equal(nameof(ErrorCode.AcessoInvalido), corpo.Message);
    }

    [Fact]
    public void Filtro_OutraExcecaoSegueOCaminhoDeSempre()
    {
        var contexto = ContextoDaExcecao(new InvalidOperationException("falha"));

        new PgiaErrosAttribute().OnException(contexto);

        Assert.False(contexto.ExceptionHandled);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public void Filtro_EstaEmTodosOsControllersDoPgiaESoNeles()
    {
        var controllers = typeof(PgiaGovernancaController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        var doPgia = controllers.Where(t => t.Namespace == "Controllers.Pgia").ToList();
        // Os 8 de app/Controllers/Pgia, inclusive o público
        Assert.Equal(8, doPgia.Count);
        Assert.Contains(typeof(PgiaPublicoController), doPgia);
        foreach (var controller in doPgia)
            Assert.True(controller.GetCustomAttribute<PgiaErrosAttribute>(inherit: true) != null,
                $"{controller.Name} sem [PgiaErros]");

        // Os outros módulos continuam como estão
        foreach (var controller in controllers.Except(doPgia))
            Assert.True(controller.GetCustomAttribute<PgiaErrosAttribute>(inherit: true) == null,
                $"{controller.Name} não é do PGIA e ganhou [PgiaErros]");
    }

    [Fact]
    public void Filtro_DescricoesPadraoSemTravessao()
    {
        foreach (var codigo in Enum.GetValues<ErrorCode>())
        {
            var descricao = PgiaErrosAttribute.DescricaoPadrao((int)codigo);
            if (descricao == null) continue;
            Assert.DoesNotContain("—", descricao);
            Assert.DoesNotContain("–", descricao);
        }
    }

    // ── 2. Tipo do instrumento legado cabe na coluna ──────────────────────────

    [Fact]
    public void Legado_OsTresTiposDoDominioCabemNoTamanhoConfigurado()
    {
        var propriedade = Context.Model.FindEntityType(typeof(PgiaInstrumentoLegado))!
            .FindProperty(nameof(PgiaInstrumentoLegado.TipoInstrumento))!;
        var tamanhoColuna = propriedade.GetMaxLength();
        Assert.Equal(40, tamanhoColuna);

        var tamanhoDto = typeof(PgiaLegadoCreateDTO)
            .GetProperty(nameof(PgiaLegadoCreateDTO.TipoInstrumento))!
            .GetCustomAttribute<StringLengthAttribute>()!.MaximumLength;

        Assert.Equal(3, PgiaDominios.TipoInstrumentoLegado.Todos.Length);
        foreach (var tipo in PgiaDominios.TipoInstrumentoLegado.Todos)
        {
            Assert.True(tipo.Length <= tamanhoColuna, $"\"{tipo}\" não cabe na coluna");
            Assert.True(tipo.Length <= tamanhoDto, $"\"{tipo}\" não passa no DTO");
        }
    }

    [Fact]
    public async Task Legado_ConvenioOuInstrumentoCongenereEhGravado()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var dto = new PgiaLegadoCreateDTO
        {
            TipoInstrumento = "Convênio ou instrumento congênere",
            Descricao = "Convênio de cooperação técnica com IA",
            EnvolveIa = PgiaDominios.EnvolveIa.Sim,
            DataTriagem = new DateOnly(2026, 9, 20)
        };

        // A validação do DTO (o [StringLength] que o [ApiController] aplica) aceita o valor
        var erros = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), erros, true));

        var legado = await _contratoService.CriarLegadoAsync(OrgaoSes.Id, dto, ctx);
        Assert.Equal("Convênio ou instrumento congênere", legado.TipoInstrumento);
    }

    // ── 3. Datas no futuro ────────────────────────────────────────────────────

    [Fact]
    public async Task Deliberacao_DataDepoisDeHojeEhRecusada()
    {
        var cgtic = await CtxAsync(UserCgtic.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governanca.CriarDeliberacaoAsync(NovaDeliberacaoDto(Hoje.AddDays(1)), cgtic));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Equal("A data da deliberação não pode ser depois de hoje.", ex.Error.Message);
        Assert.Empty(Context.PgiaDeliberacoesCgtic);
    }

    [Fact]
    public async Task Deliberacao_DataDeHojeEhAceita()
    {
        var cgtic = await CtxAsync(UserCgtic.Email);

        var deliberacao = await _governanca.CriarDeliberacaoAsync(NovaDeliberacaoDto(Hoje), cgtic);

        Assert.Equal(Hoje, deliberacao.DataDeliberacao);
    }

    [Fact]
    public async Task Uso_DataDepoisDeHojeEhRecusadaEHojeEhAceita()
    {
        var sistema = await NovoSistemaAsync();
        var agente = await CtxAsync(UserSemPapel.Email);

        PgiaRegistroUsoCreateDTO Dto(DateOnly data) => new()
        {
            SistemaIaId = sistema.Id,
            ProdutoRef = "Ofício 12/2026",
            DataUso = data,
            RevisaoHumanaConfirmada = true
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _operacaoService.CriarUsoAsync(Dto(Hoje.AddDays(1)), agente));
        Assert.Equal((int)ErrorCode.PgiaRegistroUsoInvalido, ex.Error.Code);
        Assert.Equal("A data do uso não pode ser depois de hoje.", ex.Error.Message);

        var uso = await _operacaoService.CriarUsoAsync(Dto(Hoje), agente);
        Assert.Equal(Hoje, uso.DataUso);
    }

    // ── 4. Ata da deliberação anexada depois ──────────────────────────────────

    private async Task<PgiaDeliberacaoResponse> DeliberacaoSemAtaAsync()
    {
        var cgtic = await CtxAsync(UserCgtic.Email);
        return await _governanca.CriarDeliberacaoAsync(NovaDeliberacaoDto(Hoje.AddDays(-3)), cgtic);
    }

    [Theory]
    [InlineData("cgtic@sgdi.df.gov.br")]
    [InlineData("admin@subgd.df.gov.br")]
    public async Task Ata_QuemRegistraDeliberacaoAnexaAAta(string email)
    {
        var deliberacao = await DeliberacaoSemAtaAsync();
        var ata = NovoDocumento(orgaoId: null);

        var resultado = await GovernancaComo(email)
            .DefinirAtaDeliberacao(deliberacao.Id, new PgiaDeliberacaoAtaDTO { DocumentoId = ata.Id });

        var resposta = Assert.IsType<PgiaDeliberacaoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(deliberacao.Id, resposta.Id);
        Assert.Equal(ata.Id, resposta.DocumentoId);
        Assert.Equal(deliberacao.NumeroAto, resposta.NumeroAto);

        var salva = await Context.PgiaDeliberacoesCgtic.SingleAsync();
        Assert.Equal(ata.Id, salva.DocumentoId);
        Assert.Equal(email, salva.AlteradoPor);
        Assert.NotNull(salva.AlteradoEm);
    }

    [Theory]
    [InlineData("sgdi@sgdi.df.gov.br")]
    [InlineData("maria@ses.df.gov.br")]
    [InlineData("aud@auditoria.com")]
    [InlineData("comum@ses.df.gov.br")]
    public async Task Ata_QuemNaoRegistraDeliberacaoRecebe403(string email)
    {
        var deliberacao = await DeliberacaoSemAtaAsync();
        var ata = NovoDocumento(orgaoId: null);

        var resultado = await GovernancaComo(email)
            .DefinirAtaDeliberacao(deliberacao.Id, new PgiaDeliberacaoAtaDTO { DocumentoId = ata.Id });

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await Context.PgiaDeliberacoesCgtic.SingleAsync()).DocumentoId);
    }

    [Fact]
    public async Task Ata_DeliberacaoInexistenteEh404()
    {
        var ata = NovoDocumento(orgaoId: null);

        var resultado = await GovernancaComo(UserCgtic.Email)
            .DefinirAtaDeliberacao(9999, new PgiaDeliberacaoAtaDTO { DocumentoId = ata.Id });

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task Ata_DocumentoDeOrgaoEhRecusado()
    {
        var deliberacao = await DeliberacaoSemAtaAsync();
        var doOrgao = NovoDocumento(OrgaoSes.Id);
        var cgtic = await CtxAsync(UserCgtic.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _governanca.DefinirAtaDeliberacaoAsync(deliberacao.Id,
                new PgiaDeliberacaoAtaDTO { DocumentoId = doOrgao.Id }, cgtic));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Equal("A ata da deliberação precisa ser um documento central, sem órgão.", ex.Error.Message);
        Assert.Null((await Context.PgiaDeliberacoesCgtic.SingleAsync()).DocumentoId);
    }

    [Fact]
    public async Task Ata_DocumentoInexistenteOuAusenteEhRecusado()
    {
        var deliberacao = await DeliberacaoSemAtaAsync();
        var cgtic = await CtxAsync(UserCgtic.Email);

        var inexistente = await Assert.ThrowsAsync<ApiException>(() =>
            _governanca.DefinirAtaDeliberacaoAsync(deliberacao.Id,
                new PgiaDeliberacaoAtaDTO { DocumentoId = 9999 }, cgtic));
        Assert.Equal((int)ErrorCode.PgiaDocumentoNaoEncontrado, inexistente.Error.Code);

        var ausente = await Assert.ThrowsAsync<ApiException>(() =>
            _governanca.DefinirAtaDeliberacaoAsync(deliberacao.Id, new PgiaDeliberacaoAtaDTO(), cgtic));
        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ausente.Error.Code);
        Assert.Equal("Escolha o documento da ata.", ausente.Error.Message);
    }

    [Fact]
    public async Task Ata_TrocarAAtaDeQuemJaTemEhPermitido()
    {
        var antiga = NovoDocumento(orgaoId: null, "ata-antiga.pdf");
        var cgtic = await CtxAsync(UserCgtic.Email);
        var deliberacao = await _governanca.CriarDeliberacaoAsync(
            NovaDeliberacaoDto(Hoje.AddDays(-3), antiga.Id), cgtic);
        var nova = NovoDocumento(orgaoId: null, "ata-nova.pdf");

        var resposta = await _governanca.DefinirAtaDeliberacaoAsync(deliberacao.Id,
            new PgiaDeliberacaoAtaDTO { DocumentoId = nova.Id }, cgtic);

        Assert.Equal(nova.Id, resposta.DocumentoId);
        // A lista devolve o mesmo DTO, já com a ata nova
        var lista = await _governanca.ListarDeliberacoesAsync();
        Assert.Equal(nova.Id, Assert.Single(lista).DocumentoId);
    }

    // ── 5. Parecer da auditoria ───────────────────────────────────────────────

    private async Task<PgiaAuditoriaResponse> NovaAuditoriaAsync()
    {
        var sistema = await NovoSistemaAsync("Triagem");
        var sgdi = await CtxAsync(UserSgdi.Email);
        return await _relatorioService.CriarAuditoriaAsync(new PgiaAuditoriaCreateDTO
        {
            SistemaIaId = sistema.Id,
            Tipo = "Periódica",
            EntidadeAuditora = "Auditoria Independente S/A",
            AuditorUserId = UserAuditoria.Id,
            ExternaFornecedor = true,
            DataInicio = new DateOnly(2026, 9, 1)
        }, sgdi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n ")]
    public async Task Parecer_VazioOuEmBrancoEhRecusado(string? parecer)
    {
        var auditoria = await NovaAuditoriaAsync();
        var auditor = await CtxAsync(UserAuditoria.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _relatorioService.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
            {
                DataInicio = new DateOnly(2026, 9, 1),
                Parecer = parecer
            }, auditor));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
        Assert.Equal("Escreva o parecer da auditoria.", ex.Error.Message);
        Assert.Null((await Context.PgiaAuditoriasTecnicas.SingleAsync()).Parecer);
    }

    [Fact]
    public async Task Parecer_ComTextoEhGravadoSemEspacosNasPontas()
    {
        var auditoria = await NovaAuditoriaAsync();
        var auditor = await CtxAsync(UserAuditoria.Email);

        var resposta = await _relatorioService.RegistrarParecerAsync(auditoria.Id, new PgiaAuditoriaParecerDTO
        {
            DataInicio = new DateOnly(2026, 9, 1),
            Parecer = "  Sistema conforme.  "
        }, auditor);

        Assert.Equal("Sistema conforme.", resposta.Parecer);
    }

    // ── 6. Consulta do protocolo do cidadão ───────────────────────────────────

    [Theory]
    [InlineData("pgia-2026-123456-abcd")]
    [InlineData("  PGIA-2026-123456-ABCD  ")]
    [InlineData(" Pgia-2026-123456-AbCd\t")]
    public async Task Protocolo_ConsultaNormalizaEspacosECaixa(string digitado)
    {
        var sistema = await NovoSistemaAsync();
        Context.PgiaSolicitacoesCidadao.Add(new PgiaSolicitacaoCidadao
        {
            Protocolo = "PGIA-2026-123456-ABCD",
            SistemaIaId = sistema.Id,
            Tipo = "Explicação da decisão",
            SolicitanteNome = "Joana da Silva",
            SolicitanteContato = "joana@exemplo.com",
            Descricao = "Quero entender a decisão.",
            DataAbertura = DateTime.UtcNow,
            Status = PgiaDominios.StatusSolicitacao.Recebida,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = "publico"
        });
        await Context.SaveChangesAsync();

        var encontrada = await _publicoService.ConsultarPorProtocoloAsync(digitado);

        Assert.NotNull(encontrada);
        Assert.Equal("PGIA-2026-123456-ABCD", encontrada!.Protocolo);
    }

    [Fact]
    public async Task Protocolo_DesconhecidoContinuaNulo()
    {
        Assert.Null(await _publicoService.ConsultarPorProtocoloAsync("pgia-2026-000000-zzzz"));
        Assert.Null(await _publicoService.ConsultarPorProtocoloAsync("   "));
    }
}
