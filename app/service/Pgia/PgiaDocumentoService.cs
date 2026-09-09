using api.Pgia;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Documentos do PGIA: metadados e o anexo em disco (arts. 9º, III, 30 e 32).
/// O arquivo é gravado pelo <see cref="IPgiaArquivoStorage"/> com nome interno;
/// aqui ficam as regras de tamanho, extensão e substituição.
/// </summary>
public class PgiaDocumentoService : IPgiaDocumentoService
{
    /// <summary>Teto do anexo: 25 MB (o mesmo do [RequestSizeLimit] da action).</summary>
    public const long TamanhoMaximoBytes = 25L * 1024 * 1024;

    /// <summary>
    /// Extensões aceitas e o MIME de CADA uma. O Content-Type do cliente é
    /// ignorado por completo: ele é entrada não confiável — valor sem "/" quebrava
    /// o download para sempre (FormatException no File()), valor acima de 100
    /// caracteres estourava o varchar(100), e um "text/html" mentiroso seria
    /// devolvido no download. O tipo sai da extensão, que já passou pela allowlist.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> MimePorExtensao =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".odt"] = "application/vnd.oasis.opendocument.text",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
            [".csv"] = "text/csv",
            [".txt"] = "text/plain",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg"
        };

    /// <summary>Extensões aceitas (as chaves do mapa de MIME).</summary>
    public static IEnumerable<string> ExtensoesPermitidas => MimePorExtensao.Keys;

    /// <summary>
    /// Teto do CORPO da requisição no [RequestSizeLimit]: 1 MB acima do teto do
    /// arquivo, porque o multipart carrega cabeçalhos e delimitadores além do
    /// binário. Sem essa folga, um arquivo de exatos 25 MB era barrado pelo model
    /// binding com um 400 genérico e a mensagem amigável do service nunca aparecia.
    /// </summary>
    public const long LimiteRequisicaoBytes = TamanhoMaximoBytes + (1L * 1024 * 1024);

    private const string ContentTypePadrao = "application/octet-stream";
    private const int TamanhoMaximoNomeArquivo = 200;

    private readonly IPgiaDocumentoRepositorio _repositorio;
    private readonly IPgiaArquivoStorage _storage;

    public PgiaDocumentoService(IPgiaDocumentoRepositorio repositorio, IPgiaArquivoStorage storage)
    {
        _repositorio = repositorio;
        _storage = storage;
    }

    public async Task<PgiaDocumento?> GetEntidadeAsync(long id)
    {
        return await _repositorio.GetByIdAsync(id);
    }

    public async Task<PgiaSistemaIa?> GetSistemaParaEscopoAsync(long sistemaId)
    {
        return await _repositorio.GetSistemaByIdAsync(sistemaId);
    }

    public async Task<PgiaDocumentoResponse> CriarAsync(PgiaDocumentoAvulsoCreateDTO dto, PgiaUserContext ctx)
    {
        if (!PgiaDominios.TipoDocumento.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de documento inválido: {dto.Tipo}");

        if (string.IsNullOrWhiteSpace(dto.NomeArquivo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe o nome do arquivo do documento.");

        long? orgaoId = dto.OrgaoId;
        long? sistemaId = null;

        if (dto.SistemaIaId != null)
        {
            var sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId.Value)
                ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

            sistemaId = sistema.Id;
            // O documento pertence ao órgão dono do sistema, não ao de quem envia
            orgaoId = sistema.OrgaoId;
        }
        else if (orgaoId != null)
        {
            _ = await _repositorio.GetOrgaoByIdAsync(orgaoId.Value)
                ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);
        }

        var agora = DateTime.UtcNow;
        var documento = new PgiaDocumento
        {
            Tipo = dto.Tipo,
            OrgaoId = orgaoId, // nulo = documento de escopo central
            SistemaIaId = sistemaId,
            ProcessoSei = string.IsNullOrWhiteSpace(dto.ProcessoSei) ? null : dto.ProcessoSei.Trim(),
            NomeArquivo = SanearNomeArquivo(dto.NomeArquivo),
            DataEnvio = agora,
            EnviadoPor = ctx.UserId,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        _repositorio.Add(documento);
        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetByIdAsync(documento.Id);
        return Map(salvo ?? documento);
    }

    public async Task<PgiaArquivoResponse> SalvarArquivoAsync(
        long documentoId, PgiaArquivoUpload upload, PgiaUserContext ctx, CancellationToken cancellationToken = default)
    {
        var documento = await _repositorio.GetByIdAsync(documentoId)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        // Integridade probatória (ver PodeSubstituirArquivo). O controller já
        // barra com 403; aqui fica a regra autoritativa, com a mensagem.
        if (!PodeSubstituirArquivo(documento, ctx))
            throw new ApiException(ErrorCode.PgiaArquivoInvalido,
                "Este documento já tem um arquivo anexado por outra pessoa. " +
                "A substituição é da SGDI, do CGTIC ou de quem enviou o arquivo atual.");

        if (upload.TamanhoBytes <= 0)
            throw new ApiException(ErrorCode.PgiaArquivoInvalido, "Envie um arquivo no campo \"arquivo\".");

        if (upload.TamanhoBytes > TamanhoMaximoBytes)
            throw new ApiException(ErrorCode.PgiaArquivoInvalido,
                $"O arquivo excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        var nomeOriginal = SanearNomeArquivo(upload.NomeOriginal);
        if (string.IsNullOrWhiteSpace(nomeOriginal))
            throw new ApiException(ErrorCode.PgiaArquivoInvalido, "O arquivo enviado não tem nome.");

        var extensao = Path.GetExtension(nomeOriginal).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
            throw new ApiException(ErrorCode.PgiaArquivoInvalido,
                $"Extensão de arquivo não permitida: {(string.IsNullOrEmpty(extensao) ? "(sem extensão)" : extensao)}. " +
                $"Aceitas: {string.Join(", ", ExtensoesPermitidas)}.");

        // Lê o conteúdo uma vez; o tamanho real manda (o Length do form é dica)
        var conteudo = await LerConteudoAsync(upload.Conteudo, cancellationToken);
        if (conteudo.Length > TamanhoMaximoBytes)
            throw new ApiException(ErrorCode.PgiaArquivoInvalido,
                $"O arquivo excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        // Upsert no 1:1 — reenvio sobrescreve a linha do arquivo. Binário e
        // metadados fecham no MESMO SaveChanges: nunca ficam descasados.
        await _storage.SalvarAsync(documento.Id, conteudo, cancellationToken);

        documento.NomeArquivo = nomeOriginal;
        // MIME da EXTENSÃO (allowlist), nunca o Content-Type que o cliente mandou
        documento.ContentType = MimePorExtensao[extensao];
        documento.TamanhoBytes = conteudo.Length;
        documento.DataEnvio = DateTime.UtcNow;
        documento.EnviadoPor = ctx.UserId;
        documento.AlteradoEm = DateTime.UtcNow;
        documento.AlteradoPor = ctx.Email;

        try
        {
            await _repositorio.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Corrida no PRIMEIRO anexo: outra requisição inseriu a linha do
            // arquivo entre a nossa leitura e o INSERT (read-then-insert). Refaz
            // como atualização em vez de estourar 500 por PK duplicada.
            await _storage.ReconciliarConflitoAsync(documento.Id, conteudo, cancellationToken);
            await _repositorio.SaveChangesAsync();
        }

        return new PgiaArquivoResponse
        {
            DocumentoId = documento.Id,
            NomeArquivo = documento.NomeArquivo,
            ContentType = documento.ContentType!,
            TamanhoBytes = documento.TamanhoBytes ?? 0
        };
    }

    /// <summary>
    /// Integridade probatória: a PRIMEIRA anexação segue o gate de escopo do
    /// documento (quem alcança o documento anexa). SUBSTITUIR arquivo que outra
    /// pessoa anexou trocaria a prova — ex.: o órgão auditado sobrescrever o
    /// parecer que a SGDI juntou. Só o escopo central (SGDI/CGTIC/admin) ou quem
    /// enviou o arquivo atual substitui. Fonte única usada pelo controller (403)
    /// e pelo service (mensagem).
    /// </summary>
    public static bool PodeSubstituirArquivo(PgiaDocumento documento, PgiaUserContext ctx) =>
        documento.TamanhoBytes == null            // primeira anexação
        || PgiaGovernancaService.EhEscopoCentral(ctx)
        || documento.EnviadoPor == ctx.UserId;    // dono do arquivo atual

    public async Task<PgiaArquivoDownload?> AbrirArquivoAsync(long documentoId)
    {
        var documento = await _repositorio.GetByIdAsync(documentoId)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        // Único ponto que toca o bytea
        var conteudo = await _storage.AbrirAsync(documentoId);
        if (conteudo == null) return null; // documento só com metadados

        return new PgiaArquivoDownload
        {
            Conteudo = conteudo,
            ContentType = string.IsNullOrWhiteSpace(documento.ContentType) ? ContentTypePadrao : documento.ContentType,
            NomeArquivo = documento.NomeArquivo
        };
    }

    private static async Task<byte[]> LerConteudoAsync(Stream origem, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await origem.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }

    /// <summary>
    /// Nome de exibição: sem componente de caminho (o navegador de alguns clientes
    /// manda "C:\pasta\arquivo.pdf") e com tamanho limitado. É o que vai para o
    /// Content-Disposition — o caminho físico usa nome interno gerado.
    /// </summary>
    public static string SanearNomeArquivo(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return string.Empty;

        // Corta tudo até a última barra (dos dois tipos) e dois-pontos de drive
        var somenteNome = nome.Trim();
        var corte = somenteNome.LastIndexOfAny(new[] { '/', '\\', ':' });
        if (corte >= 0) somenteNome = somenteNome[(corte + 1)..];

        // Sobra "..": não é nome de arquivo
        somenteNome = somenteNome.Trim().TrimStart('.', ' ');

        if (somenteNome.Length <= TamanhoMaximoNomeArquivo) return somenteNome;

        // Ao encurtar, preserva a extensão: ela é o que a allowlist confere e o
        // que o sistema operacional do usuário usa para abrir o arquivo
        var extensao = Path.GetExtension(somenteNome);
        if (extensao.Length == 0 || extensao.Length >= TamanhoMaximoNomeArquivo)
            return somenteNome[..TamanhoMaximoNomeArquivo];

        var baseNome = somenteNome[..(TamanhoMaximoNomeArquivo - extensao.Length)];
        return baseNome + extensao;
    }

    /// <summary>Mapeamento único do documento, usado também pelo PgiaSistemaService.</summary>
    public static PgiaDocumentoResponse Map(PgiaDocumento d) => new()
    {
        Id = d.Id,
        Tipo = d.Tipo,
        OrgaoId = d.OrgaoId,
        SistemaIaId = d.SistemaIaId,
        ProcessoSei = d.ProcessoSei,
        NomeArquivo = d.NomeArquivo,
        UrlStorage = d.UrlStorage,
        ContentType = d.ContentType,
        TamanhoBytes = d.TamanhoBytes,
        // Sinal de "tem anexo" pelos METADADOS: não encosta na tabela do binário
        TemArquivo = d.TamanhoBytes != null,
        DataEnvio = d.DataEnvio,
        EnviadoPorNome = d.EnviadoPorUser?.Nome ?? string.Empty
    };
}
