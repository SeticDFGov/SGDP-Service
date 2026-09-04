namespace Models.Pgia;

/// <summary>
/// Conteúdo binário do anexo de um documento (1:1 com pgia_documento).
/// Fica em tabela SEPARADA de propósito: o blob nunca pode ser arrastado por
/// Include/listagem de documento — só o endpoint de download seleciona
/// <see cref="Conteudo"/>. Os metadados (nome, tipo, tamanho) vivem na
/// pgia_documento e bastam para as telas. Tabela pgia_documento_arquivo.
///
/// Não existe propriedade de navegação do lado do PgiaDocumento justamente
/// para que nenhum Include consiga alcançar o binário sem querer.
/// </summary>
public class PgiaDocumentoArquivo
{
    // PK e FK ao mesmo tempo: um documento tem no máximo um arquivo
    public long DocumentoId { get; set; }

    public PgiaDocumento? Documento { get; set; }

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}
