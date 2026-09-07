using app.Models;

namespace Models.Pgia;

/// <summary>
/// Órgãos e entidades do GDF alcançados pela PGIA/DF (arts. 1º, 3º e 24, I).
/// Tabela pgia_orgao; mapeamento em PgiaModelConfiguration.
/// </summary>
public class PgiaOrgao
{
    public long Id { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    // D10. Define aplicação integral ou no que couber (art. 1º e § único)
    public string NaturezaJuridica { get; set; } = string.Empty;

    // Só para empresa pública ou sociedade de economia mista (art. 1º, § único)
    public bool? PrestaServicoCidadao { get; set; }

    public bool Ativo { get; set; } = true;

    // Liga o órgão PGIA à Unidade que o usuário escolhe no primeiro acesso;
    // é assim que o sistema descobre de que órgão é cada usuário.
    public Guid? UnidadeId { get; set; }

    public Unidade? Unidade { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
