using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaDocumentoService
{
    /// <summary>Entidade crua, para o controller checar o escopo antes de agir.</summary>
    Task<PgiaDocumento?> GetEntidadeAsync(long id);

    /// <summary>Sistema referenciado, para o controller conferir o órgão do escopo.</summary>
    Task<PgiaSistemaIa?> GetSistemaParaEscopoAsync(long sistemaId);

    /// <summary>
    /// Cria o documento (só metadados) no escopo escolhido: sistema, órgão ou central.
    /// O controller já validou a permissão sobre o escopo.
    /// </summary>
    Task<PgiaDocumentoResponse> CriarAsync(PgiaDocumentoAvulsoCreateDTO dto, PgiaUserContext ctx);

    /// <summary>
    /// Grava o anexo em disco e atualiza os metadados. Reenvio SUBSTITUI: o
    /// arquivo físico anterior é apagado.
    /// </summary>
    Task<PgiaArquivoResponse> SalvarArquivoAsync(
        long documentoId, PgiaArquivoUpload upload, PgiaUserContext ctx, CancellationToken cancellationToken = default);

    /// <summary>Abre o anexo para download; null quando o documento não tem arquivo.</summary>
    Task<PgiaArquivoDownload?> AbrirArquivoAsync(long documentoId);
}

/// <summary>Arquivo recebido no multipart, desacoplado do IFormFile.</summary>
public class PgiaArquivoUpload
{
    public string NomeOriginal { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long TamanhoBytes { get; set; }

    public Stream Conteudo { get; set; } = Stream.Null;
}

/// <summary>Anexo pronto para o FileContentResult.</summary>
public class PgiaArquivoDownload
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = string.Empty;

    public string NomeArquivo { get; set; } = string.Empty;
}
