using app.Models;

namespace Models.Pgia;

/// <summary>
/// Artefatos anexados e vínculo com o SEI (arts. 9º, III, 10, § 1º, 30 e 32).
/// Guarda metadados, o nº do processo SEI (canal oficial) e, desde a rodada de
/// anexos, o arquivo em disco: <see cref="UrlStorage"/> é o caminho RELATIVO à
/// raiz do <c>PgiaArquivoStorage</c>. Tabela pgia_documento.
/// </summary>
public class PgiaDocumento
{
    public long Id { get; set; }

    // D28
    public string Tipo { get; set; } = string.Empty;

    // Nullable: documento de escopo central (ata do CGTIC, relatório anual da SGDI)
    // não tem órgão natural; documento de órgão/sistema sempre tem.
    public long? OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // Canal oficial de comunicação com a SGDI, quando o artefato tramita no SEI
    public string? ProcessoSei { get; set; }

    // Nome original informado pelo usuário: vale para exibir e para o
    // Content-Disposition do download; NUNCA compõe o caminho físico.
    public string NomeArquivo { get; set; } = string.Empty;

    // Caminho RELATIVO à raiz de armazenamento ("pgia/{guid}{ext}"); null enquanto
    // o documento é só metadado, sem arquivo anexado.
    public string? UrlStorage { get; set; }

    public string? ContentType { get; set; }

    public long? TamanhoBytes { get; set; }

    public DateTime DataEnvio { get; set; }

    // Agente que enviou (Users do SGDP)
    public Guid EnviadoPor { get; set; }

    public User? EnviadoPorUser { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
