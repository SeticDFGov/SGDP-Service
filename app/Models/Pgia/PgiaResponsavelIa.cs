using app.Models;

namespace Models.Pgia;

/// <summary>
/// Designações de Responsável de IA (arts. 2º, IX, 10 e 35, I). Ao menos um por órgão;
/// no máximo uma designação ativa por órgão (índice único parcial). O histórico fica preservado.
/// </summary>
public class PgiaResponsavelIa
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // Pessoa designada (Users do SGDP)
    public Guid AgenteId { get; set; }

    public User? Agente { get; set; }

    public string AtoTipo { get; set; } = string.Empty;

    public string AtoNumero { get; set; } = string.Empty;

    public DateOnly AtoData { get; set; }

    // Canal oficial de comunicação com a SGDI (art. 10, § 1º)
    public string ProcessoSeiComunicacao { get; set; } = string.Empty;

    // Até 02/08/2026 e a cada alteração (arts. 10, § 1º e 35, I)
    public DateOnly DataComunicacaoSgdi { get; set; }

    // Acumulação com a função de TIC é permitida pela PGTIC/DF (art. 10, § 2º)
    public bool AcumulaFuncaoTic { get; set; }

    // Condicional: exigida quando acumula a função de TIC (arts. 10, § 2º e 29, III)
    public bool? CapacitacaoAdequada { get; set; }

    // Cópia do ato de designação anexada (pgia_documento); opcional
    public long? DocumentoId { get; set; }

    public DateOnly InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
