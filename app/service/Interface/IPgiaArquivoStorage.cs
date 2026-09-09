namespace service.Interface;

/// <summary>
/// Guarda do conteúdo binário dos anexos do PGIA, endereçado pelo id do documento
/// (1:1). Hoje a implementação grava no próprio banco (bytea em tabela separada).
///
/// **Ponto de troca:** quando a Infra entregar o servidor de arquivos, basta OUTRA
/// implementação desta interface (gravando lá e devolvendo os bytes/stream por
/// documento). Endpoints, telas e metadados de pgia_documento não mudam — nada
/// fora daqui conhece onde o binário mora.
/// </summary>
public interface IPgiaArquivoStorage
{
    /// <summary>
    /// Grava o conteúdo do documento. Reenvio SOBRESCREVE (upsert no 1:1).
    /// Não faz SaveChanges: quem chama fecha a transação junto com os metadados.
    /// </summary>
    Task SalvarAsync(long documentoId, byte[] conteudo, CancellationToken cancellationToken = default);

    /// <summary>Conteúdo do anexo; null quando o documento não tem arquivo.</summary>
    Task<byte[]?> AbrirAsync(long documentoId, CancellationToken cancellationToken = default);

    /// <summary>Remove o conteúdo, se houver. Não faz SaveChanges.</summary>
    Task ApagarAsync(long documentoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desfaz um INSERT que colidiu (outra requisição anexou primeiro) e reaplica
    /// o conteúdo como atualização. Não faz SaveChanges.
    /// </summary>
    Task ReconciliarConflitoAsync(long documentoId, byte[] conteudo, CancellationToken cancellationToken = default);
}
