using System.Security.Claims;
using System.Text;
using api.Pgia;
using Controllers.Pgia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Interface;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Anexos dos documentos do PGIA. Por decisão do responsável pelo sistema, o
/// binário fica no PRÓPRIO BANCO (bytea em pgia_documento_arquivo, tabela
/// separada e 1:1) — trocar pelo servidor de arquivos da Infra é só outra
/// implementação de IPgiaArquivoStorage.
/// </summary>
public class PgiaAnexoDocumentoTest : PgiaTestBase
{
    private readonly PgiaPermissionService _permissionService;
    private readonly PgiaDocumentoService _service;
    private readonly IPgiaArquivoStorage _storage;
    private readonly PgiaDocumentoController _controller;

    public PgiaAnexoDocumentoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _storage = new PgiaArquivoStorage(Context);
        _service = new PgiaDocumentoService(new PgiaDocumentoRepositorio(Context), _storage);
        _controller = new PgiaDocumentoController(_service, _permissionService);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private PgiaDocumentoController Como(string email)
    {
        var identidade = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, email) }, "Teste");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidade) }
        };
        return _controller;
    }

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email))!;

    /// <summary>Documento só com metadados, no órgão informado (null = central).</summary>
    private PgiaDocumento NovoDocumento(long? orgaoId, string nome = "parecer.pdf")
    {
        var documento = new PgiaDocumento
        {
            Tipo = PgiaDominios.TipoDocumento.Todos.First(),
            OrgaoId = orgaoId,
            NomeArquivo = nome,
            DataEnvio = DateTime.UtcNow,
            EnviadoPor = UserOrgaoSes.Id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaDocumentos.Add(documento);
        Context.SaveChanges();
        return documento;
    }

    private static IFormFile Arquivo(
        string nome, byte[]? conteudo = null, string contentType = "application/pdf")
    {
        var bytes = conteudo ?? Encoding.UTF8.GetBytes("conteudo de teste");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "arquivo", nome)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    /// <summary>Outro AppDbContext sobre o MESMO banco InMemory, para simular concorrência.</summary>
    private AppDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(NomeBanco).Options);

    private static PgiaArquivoResponse Enviado(IActionResult resultado) =>
        Assert.IsType<PgiaArquivoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_GravaConteudoEMetadados()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var bytes = Encoding.UTF8.GetBytes("relatório em pdf");

        var resposta = Enviado(await Como(UserOrgaoSes.Email)
            .EnviarArquivo(documento.Id, Arquivo("Parecer Final.pdf", bytes)));

        Assert.Equal(documento.Id, resposta.DocumentoId);
        Assert.Equal("Parecer Final.pdf", resposta.NomeArquivo);
        Assert.Equal("application/pdf", resposta.ContentType);
        Assert.Equal(bytes.Length, resposta.TamanhoBytes);

        // Metadados na pgia_documento
        var salvo = await Context.PgiaDocumentos.AsNoTracking().FirstAsync(d => d.Id == documento.Id);
        Assert.Equal("Parecer Final.pdf", salvo.NomeArquivo);
        Assert.Equal("application/pdf", salvo.ContentType);
        Assert.Equal(bytes.Length, salvo.TamanhoBytes);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);

        // Binário na tabela separada
        var arquivo = await Context.PgiaDocumentoArquivos.AsNoTracking()
            .SingleAsync(a => a.DocumentoId == documento.Id);
        Assert.Equal(bytes, arquivo.Conteudo);
    }

    [Fact]
    public async Task Upload_ReenvioSubstituiOConteudoSemDuplicarLinha()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("v1.pdf", Encoding.UTF8.GetBytes("versão 1")));

        var novos = Encoding.UTF8.GetBytes("versão 2 — bem maior que a primeira");
        var resposta = Enviado(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("v2.pdf", novos)));

        Assert.Equal("v2.pdf", resposta.NomeArquivo);
        Assert.Equal(novos.Length, resposta.TamanhoBytes);

        // 1:1 — continua uma linha só, com o conteúdo novo
        var arquivos = await Context.PgiaDocumentoArquivos.AsNoTracking()
            .Where(a => a.DocumentoId == documento.Id).ToListAsync();
        Assert.Single(arquivos);
        Assert.Equal(novos, arquivos[0].Conteudo);

        var salvo = await Context.PgiaDocumentos.AsNoTracking().FirstAsync(d => d.Id == documento.Id);
        Assert.Equal("v2.pdf", salvo.NomeArquivo);
        Assert.Equal(novos.Length, salvo.TamanhoBytes);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.sh")]
    [InlineData("planilha.xlsm")]
    [InlineData("sem-extensao")]
    public async Task Upload_ExtensaoForaDaAllowlistEhRecusada(string nome)
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo(nome)));

        Assert.Equal((int)ErrorCode.PgiaArquivoInvalido, ex.Error.Code);
        Assert.Empty(await Context.PgiaDocumentoArquivos.ToListAsync());
    }

    [Theory]
    [InlineData("contrato.pdf")]
    [InlineData("CONTRATO.PDF")]
    [InlineData("planilha.XLSX")]
    [InlineData("foto.jpeg")]
    public async Task Upload_ExtensaoPermitidaAceitaIgnorandoCaixa(string nome)
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resposta = Enviado(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo(nome)));

        Assert.Equal(nome, resposta.NomeArquivo);
        Assert.Single(await Context.PgiaDocumentoArquivos.ToListAsync());
    }

    [Fact]
    public async Task Upload_AcimaDoLimiteEhRecusado()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var grande = new byte[PgiaDocumentoService.TamanhoMaximoBytes + 1];

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("enorme.pdf", grande)));

        Assert.Equal((int)ErrorCode.PgiaArquivoInvalido, ex.Error.Code);
        Assert.Contains("25 MB", ex.Error.Message);
        Assert.Empty(await Context.PgiaDocumentoArquivos.ToListAsync());
    }

    [Fact]
    public async Task Upload_SemArquivoEhRecusado()
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, null));

        Assert.Equal((int)ErrorCode.PgiaArquivoInvalido, ex.Error.Code);
    }

    /// <summary>
    /// O nome original nunca vira caminho: o binário mora no banco, endereçado
    /// pelo id do documento. Um nome malicioso só é saneado para exibição.
    /// </summary>
    [Theory]
    [InlineData("..\\..\\windows\\system32\\x.pdf", "x.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("C:\\Users\\alvo\\segredo.pdf", "segredo.pdf")]
    [InlineData("....//evasao.pdf", "evasao.pdf")]
    public async Task Upload_NomeMaliciosoEhSaneadoENaoViraCaminho(string enviado, string esperado)
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resposta = Enviado(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo(enviado)));

        Assert.Equal(esperado, resposta.NomeArquivo);
        Assert.DoesNotContain("..", resposta.NomeArquivo);
        Assert.DoesNotContain("/", resposta.NomeArquivo);
        Assert.DoesNotContain("\\", resposta.NomeArquivo);

        // O endereçamento é pelo id do documento — nada de caminho
        var arquivo = await Context.PgiaDocumentoArquivos.AsNoTracking()
            .SingleAsync(a => a.DocumentoId == documento.Id);
        Assert.Equal(documento.Id, arquivo.DocumentoId);
    }

    [Fact]
    public async Task Upload_NomeMuitoLongoEhTruncado()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var nomeLongo = new string('a', 300) + ".pdf";

        var resposta = Enviado(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo(nomeLongo)));

        // Encurta, mas preserva a extensão (é o que a allowlist confere)
        Assert.Equal(200, resposta.NomeArquivo.Length);
        Assert.EndsWith(".pdf", resposta.NomeArquivo);
        Assert.Single(await Context.PgiaDocumentoArquivos.ToListAsync());
    }

    // ── Download ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Download_DevolveBytesContentTypeENomeOriginal()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var bytes = Encoding.UTF8.GetBytes("conteúdo baixado");
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("Ata da Reunião.pdf", bytes));

        var resultado = await Como(UserOrgaoSes.Email).BaixarArquivo(documento.Id);

        var file = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal(bytes, file.FileContents);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Ata da Reunião.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task Download_DocumentoSemArquivoDa404()
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resultado = await Como(UserOrgaoSes.Email).BaixarArquivo(documento.Id);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task Download_DocumentoInexistenteDa404()
    {
        var resultado = await Como(UserSgdi.Email).BaixarArquivo(987654);
        Assert.IsType<NotFoundResult>(resultado);
    }

    // ── O blob não vaza para as listagens ─────────────────────────────────────

    /// <summary>
    /// O binário está em tabela separada e SEM navegação do lado do documento:
    /// listar documentos não pode arrastar o conteúdo.
    /// </summary>
    [Fact]
    public async Task Listagem_NaoCarregaOConteudoDoArquivo()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var bytes = Encoding.UTF8.GetBytes("conteúdo pesado que não pode vazar");
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("pesado.pdf", bytes));

        // A entidade de documento não expõe propriedade de conteúdo
        var propriedades = typeof(PgiaDocumento).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Conteudo", propriedades);
        Assert.DoesNotContain("Arquivo", propriedades);

        // Nem o modelo do EF liga documento → arquivo (nenhuma navegação a incluir)
        var navegacoes = Context.Model.FindEntityType(typeof(PgiaDocumento))!
            .GetNavigations().Select(n => n.Name).ToList();
        Assert.DoesNotContain("Arquivo", navegacoes);
        Assert.DoesNotContain("Conteudo", navegacoes);

        // O DTO devolvido às telas leva metadados, não bytes
        var resposta = PgiaDocumentoService.Map(
            await Context.PgiaDocumentos.AsNoTracking().FirstAsync(d => d.Id == documento.Id));
        Assert.True(resposta.TemArquivo);
        Assert.Equal(bytes.Length, resposta.TamanhoBytes);
        Assert.DoesNotContain("Conteudo", typeof(PgiaDocumentoResponse).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task Storage_ApagarRemoveSoOBinarioEPreservaOsMetadados()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("descartavel.pdf"));

        await _storage.ApagarAsync(documento.Id);
        await Context.SaveChangesAsync();

        Assert.Empty(await Context.PgiaDocumentoArquivos.ToListAsync());
        Assert.Null(await _storage.AbrirAsync(documento.Id));
        // O documento (metadados) continua lá
        Assert.NotNull(await Context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == documento.Id));
    }

    // ── Escopo e papéis ───────────────────────────────────────────────────────

    [Fact]
    public async Task Escopo_OrgaoNaoAlcancaDocumentoDeOutroOrgao()
    {
        var documentoSeec = NovoDocumento(OrgaoSeec.Id);

        var upload = await Como(UserOrgaoSes.Email).EnviarArquivo(documentoSeec.Id, Arquivo("intruso.pdf"));
        var download = await Como(UserOrgaoSes.Email).BaixarArquivo(documentoSeec.Id);

        Assert.IsType<ForbidResult>(upload);
        Assert.IsType<ForbidResult>(download);
        Assert.Empty(await Context.PgiaDocumentoArquivos.ToListAsync());
    }

    [Fact]
    public async Task Escopo_SgdiAnexaEmQualquerOrgao()
    {
        var documentoSes = NovoDocumento(OrgaoSes.Id);
        var documentoSeec = NovoDocumento(OrgaoSeec.Id);

        Assert.IsType<OkObjectResult>(await Como(UserSgdi.Email).EnviarArquivo(documentoSes.Id, Arquivo("a.pdf")));
        Assert.IsType<OkObjectResult>(await Como(UserSgdi.Email).EnviarArquivo(documentoSeec.Id, Arquivo("b.pdf")));

        Assert.Equal(2, await Context.PgiaDocumentoArquivos.CountAsync());
    }

    [Fact]
    public async Task Escopo_AuditoriaExternaNaoAnexaNemBaixa()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserSgdi.Email).EnviarArquivo(documento.Id, Arquivo("parecer.pdf"));

        var upload = await Como(UserAuditoria.Email).EnviarArquivo(documento.Id, Arquivo("outro.pdf"));
        var download = await Como(UserAuditoria.Email).BaixarArquivo(documento.Id);

        Assert.IsType<ForbidResult>(upload);
        Assert.IsType<ForbidResult>(download);
    }

    [Fact]
    public async Task Escopo_SemPapelPgiaNaoAlcancaDocumento()
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        Assert.IsType<ForbidResult>(await Como(UserSemPapel.Email).EnviarArquivo(documento.Id, Arquivo("x.pdf")));
        Assert.IsType<ForbidResult>(await Como(UserSemPapel.Email).BaixarArquivo(documento.Id));
    }

    /// <summary>
    /// Documento central (ata do CGTIC, relatório anual da SGDI) não tem órgão:
    /// só as instâncias centrais o alcançam.
    /// </summary>
    [Fact]
    public async Task Central_DocumentoSemOrgaoSoParaEscopoCentral()
    {
        var documento = NovoDocumento(orgaoId: null, nome: "ata-cgtic.pdf");

        Assert.IsType<OkObjectResult>(await Como(UserCgtic.Email).EnviarArquivo(documento.Id, Arquivo("ata.pdf")));
        Assert.IsType<FileContentResult>(await Como(UserSgdi.Email).BaixarArquivo(documento.Id));
        Assert.IsType<ForbidResult>(await Como(UserOrgaoSes.Email).BaixarArquivo(documento.Id));
        Assert.IsType<ForbidResult>(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("x.pdf")));
    }

    // ── Criação de documento nos contextos sem rota própria ───────────────────

    [Fact]
    public async Task Criar_DocumentoDoOrgaoPeloProprioOrgao()
    {
        var dto = new PgiaDocumentoAvulsoCreateDTO
        {
            Tipo = "Certificado de capacitação",
            OrgaoId = OrgaoSes.Id,
            NomeArquivo = "certificado.pdf",
            ProcessoSei = "00060-00012345/2026-11"
        };

        var resultado = await Como(UserOrgaoSes.Email).Criar(dto);

        var criado = Assert.IsType<PgiaDocumentoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(OrgaoSes.Id, criado.OrgaoId);
        Assert.False(criado.TemArquivo); // nasce só com metadados
        Assert.Equal("Certificado de capacitação", criado.Tipo);
    }

    [Fact]
    public async Task Criar_DocumentoCentralSoPeloEscopoCentral()
    {
        var dto = new PgiaDocumentoAvulsoCreateDTO
        {
            Tipo = "Ata ou deliberação",
            NomeArquivo = "ata-comite.pdf"
        };

        var doCgtic = await Como(UserCgtic.Email).Criar(dto);
        var doOrgao = await Como(UserOrgaoSes.Email).Criar(dto);

        var criado = Assert.IsType<PgiaDocumentoResponse>(Assert.IsType<OkObjectResult>(doCgtic).Value);
        Assert.Null(criado.OrgaoId);
        Assert.IsType<ForbidResult>(doOrgao);
    }

    [Fact]
    public async Task Criar_DocumentoDeSistemaHerdaOOrgaoDoSistema()
    {
        var sistema = new PgiaSistemaIa
        {
            OrgaoId = OrgaoSeec.Id,
            Denominacao = "Sistema da SEEC",
            Finalidade = "Teste",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "Outra",
            StatusCicloVida = "Planejamento",
            EscopoDados = "Somente dados públicos",
            ClassificacaoRiscoAtual = "Baixo Risco",
            SituacaoHomologacao = "Aguardando SGDI",
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaSistemasIa.Add(sistema);
        await Context.SaveChangesAsync();

        var dto = new PgiaDocumentoAvulsoCreateDTO
        {
            Tipo = "Contrato",
            SistemaIaId = sistema.Id,
            NomeArquivo = "contrato.pdf"
        };

        var criado = Assert.IsType<PgiaDocumentoResponse>(
            Assert.IsType<OkObjectResult>(await Como(UserSgdi.Email).Criar(dto)).Value);

        Assert.Equal(OrgaoSeec.Id, criado.OrgaoId); // herdado do sistema
        Assert.Equal(sistema.Id, criado.SistemaIaId);

        // Órgão de outro órgão não cria documento nesse sistema
        Assert.IsType<ForbidResult>(await Como(UserOrgaoSes.Email).Criar(dto));
    }

    // ── C1/C2: o MIME vem da extensão, nunca do cliente ───────────────────────

    /// <summary>
    /// Content-Type do cliente é entrada não confiável: valor sem "/" quebrava o
    /// download para sempre (FormatException no File()), acima de 100 caracteres
    /// estourava o varchar(100), e "text/html" seria ecoado no download.
    /// </summary>
    [Theory]
    [InlineData("lixo-sem-barra")]
    [InlineData("text/html")]
    [InlineData("")]
    [InlineData("application/x-")]
    public async Task Upload_IgnoraContentTypeDoClienteEUsaODaExtensao(string contentTypeCliente)
    {
        var documento = NovoDocumento(OrgaoSes.Id);

        var resposta = Enviado(await Como(UserOrgaoSes.Email)
            .EnviarArquivo(documento.Id, Arquivo("anexo.pdf", contentType: contentTypeCliente)));

        Assert.Equal("application/pdf", resposta.ContentType);
        var salvo = await Context.PgiaDocumentos.AsNoTracking().FirstAsync(d => d.Id == documento.Id);
        Assert.Equal("application/pdf", salvo.ContentType);
    }

    [Fact]
    public async Task Upload_ContentTypeEnormeNaoEstouraAColuna()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var enorme = "application/" + new string('x', 500);

        var resposta = Enviado(await Como(UserOrgaoSes.Email)
            .EnviarArquivo(documento.Id, Arquivo("planilha.xlsx", contentType: enorme)));

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.ContentType);
        Assert.True(resposta.ContentType.Length <= 100); // cabe no varchar(100)
    }

    [Theory]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("planilha.csv", "text/csv")]
    [InlineData("nota.txt", "text/plain")]
    [InlineData("foto.PNG", "image/png")]
    [InlineData("foto.jpeg", "image/jpeg")]
    public async Task Download_ServeOMimeDaExtensaoComNosniff(string nome, string mimeEsperado)
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo(nome, contentType: "text/html"));

        var controller = Como(UserOrgaoSes.Email);
        var resultado = await controller.BaixarArquivo(documento.Id);

        var file = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal(mimeEsperado, file.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
    }

    // ── C3: substituição de arquivo alheio ────────────────────────────────────

    [Fact]
    public async Task Substituicao_DonoTrocaOProprioArquivo()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("v1.pdf"));

        var resposta = Enviado(await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("v2.pdf")));

        Assert.Equal("v2.pdf", resposta.NomeArquivo);
    }

    [Fact]
    public async Task Substituicao_OrgaoNaoTrocaOArquivoAnexadoPelaSgdi()
    {
        // Documento do órgão, mas o arquivo atual foi anexado pela SGDI
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserSgdi.Email).EnviarArquivo(documento.Id, Arquivo("parecer-sgdi.pdf"));

        var resultado = await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("trocado.pdf"));

        Assert.IsType<ForbidResult>(resultado);
        var salvo = await Context.PgiaDocumentos.AsNoTracking().FirstAsync(d => d.Id == documento.Id);
        Assert.Equal("parecer-sgdi.pdf", salvo.NomeArquivo); // prova preservada
    }

    [Fact]
    public async Task Substituicao_SgdiTrocaQualquerArquivo()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("do-orgao.pdf"));

        var resposta = Enviado(await Como(UserSgdi.Email).EnviarArquivo(documento.Id, Arquivo("da-sgdi.pdf")));

        Assert.Equal("da-sgdi.pdf", resposta.NomeArquivo);
    }

    [Fact]
    public async Task Substituicao_ServiceTambemRecusaComMensagemClara()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        await Como(UserSgdi.Email).EnviarArquivo(documento.Id, Arquivo("parecer-sgdi.pdf"));

        var ctxOrgao = await CtxAsync(UserOrgaoSes.Email);
        var upload = new PgiaArquivoUpload
        {
            NomeOriginal = "trocado.pdf",
            ContentType = "application/pdf",
            TamanhoBytes = 10,
            Conteudo = new MemoryStream(new byte[10])
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.SalvarArquivoAsync(documento.Id, upload, ctxOrgao));

        Assert.Equal((int)ErrorCode.PgiaArquivoInvalido, ex.Error.Code);
        Assert.Contains("já tem um arquivo anexado por outra pessoa", ex.Error.Message);
    }

    // ── C6: fronteira dos 25 MB ───────────────────────────────────────────────

    /// <summary>
    /// O corpo do multipart tem overhead além do binário. Com o [RequestSizeLimit]
    /// no mesmo valor do teto do arquivo, 25 MB exatos morriam no model binding
    /// (400 genérico) e a mensagem amigável do service era inalcançável. Com 1 MB
    /// de folga, o arquivo chega ao service — e, sendo o teto "≤ 25 MB", 25 MB
    /// exatos são ACEITOS; quem passa do teto é recusado com a mensagem própria.
    /// </summary>
    [Fact]
    public async Task Limite_VinteCincoMbExatosChegamAoServiceESaoAceitos()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var exatos = new byte[PgiaDocumentoService.TamanhoMaximoBytes];

        var resposta = Enviado(await Como(UserOrgaoSes.Email)
            .EnviarArquivo(documento.Id, Arquivo("limite.pdf", exatos)));

        Assert.Equal(PgiaDocumentoService.TamanhoMaximoBytes, resposta.TamanhoBytes);
        // A folga do corpo é o que permitiu o arquivo chegar até aqui
        Assert.True(PgiaDocumentoService.LimiteRequisicaoBytes > PgiaDocumentoService.TamanhoMaximoBytes);
    }

    /// <summary>
    /// Um byte acima do teto: recusado PELO SERVICE, com a mensagem amigável —
    /// não pelo model binding, que só entraria em cena acima do corpo de 26 MB.
    /// </summary>
    [Fact]
    public async Task Limite_AcimaDoTetoRecusadoPeloServiceComMensagem()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var acima = new byte[PgiaDocumentoService.TamanhoMaximoBytes + 1];

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            Como(UserOrgaoSes.Email).EnviarArquivo(documento.Id, Arquivo("acima.pdf", acima)));

        Assert.Equal((int)ErrorCode.PgiaArquivoInvalido, ex.Error.Code);
        Assert.Contains("25 MB", ex.Error.Message);
        Assert.True(acima.Length < PgiaDocumentoService.LimiteRequisicaoBytes); // passou do binding
    }

    [Fact]
    public async Task Limite_UmByteAbaixoDoTetoEhAceito()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var quase = new byte[PgiaDocumentoService.TamanhoMaximoBytes - 1];

        var resposta = Enviado(await Como(UserOrgaoSes.Email)
            .EnviarArquivo(documento.Id, Arquivo("quase.pdf", quase)));

        Assert.Equal(quase.Length, resposta.TamanhoBytes);
    }

    // ── Corrida no primeiro anexo ─────────────────────────────────────────────

    /// <summary>
    /// Read-then-insert: se outra requisição anexar entre a leitura e o INSERT,
    /// a PK duplicada dava 500. Agora o conflito vira atualização.
    /// </summary>
    [Fact]
    public async Task Concorrencia_ConflitoNoPrimeiroAnexoViraAtualizacao()
    {
        var documento = NovoDocumento(OrgaoSes.Id);
        var meu = Encoding.UTF8.GetBytes("conteúdo desta requisição");

        // Esta requisição não viu nada e encaminhou um INSERT...
        await _storage.SalvarAsync(documento.Id, meu);
        // ...mas a outra chegou primeiro e gravou a linha
        await using (var outro = NovoContexto())
        {
            outro.PgiaDocumentoArquivos.Add(new PgiaDocumentoArquivo
            {
                DocumentoId = documento.Id,
                Conteudo = Encoding.UTF8.GetBytes("conteúdo da outra requisição")
            });
            await outro.SaveChangesAsync();
        }

        // A colisão de PK aparece de forma diferente por provedor: o Npgsql a
        // envolve num DbUpdateException (23505), que é o que o service captura;
        // o InMemory levanta ArgumentException. O que importa aqui é a recuperação.
        await Assert.ThrowsAnyAsync<Exception>(() => Context.SaveChangesAsync());

        // Reconciliação: refaz como atualização, sem estourar
        await _storage.ReconciliarConflitoAsync(documento.Id, meu);
        await Context.SaveChangesAsync();

        await using var conferencia = NovoContexto();
        var linhas = await conferencia.PgiaDocumentoArquivos
            .Where(a => a.DocumentoId == documento.Id).ToListAsync();
        Assert.Single(linhas);
        Assert.Equal(meu, linhas[0].Conteudo);
    }

    // ── Documento de sistema: nome saneado e UrlStorage ignorado ──────────────

    [Fact]
    public async Task CriarDocumento_SaneiaONomeEIgnoraUrlStorageDoCliente()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var criado = await _service.CriarAsync(new PgiaDocumentoAvulsoCreateDTO
        {
            Tipo = "Contrato",
            OrgaoId = OrgaoSes.Id,
            NomeArquivo = @"..\..\etc\passwd.pdf",
            UrlStorage = "pgia/roubado.pdf"
        }, ctx);

        Assert.Equal("passwd.pdf", criado.NomeArquivo);
        Assert.Null(criado.UrlStorage); // interno do armazenamento
    }

    [Fact]
    public async Task Criar_TipoForaDoDominioEhRejeitado()
    {
        var ctx = await CtxAsync(UserSgdi.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarAsync(new PgiaDocumentoAvulsoCreateDTO
            {
                Tipo = "Bilhete",
                OrgaoId = OrgaoSes.Id,
                NomeArquivo = "x.pdf"
            }, ctx));

        Assert.Equal((int)ErrorCode.PgiaDominioInvalido, ex.Error.Code);
    }
}
