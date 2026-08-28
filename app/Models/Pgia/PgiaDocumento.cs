using app.Models;

namespace Models.Pgia;

/// <summary>
/// Metadados dos artefatos anexados e vínculo com o SEI (arts. 9º, III, 10, § 1º, 30 e 32).
/// O upload binário fica adiado (seção 4.2 da análise): guarda-se só metadados e o nº do
/// processo SEI, que é o canal oficial. Tabela pgia_documento.
/// </summary>
public class PgiaDocumento
{
    public long Id { get; set; }

    // D28
    public string Tipo { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // Canal oficial de comunicação com a SGDI, quando o artefato tramita no SEI
    public string? ProcessoSei { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    public string? UrlStorage { get; set; }

    public DateTime DataEnvio { get; set; }

    // Agente que enviou (Users do SGDP)
    public Guid EnviadoPor { get; set; }

    public User? EnviadoPorUser { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
