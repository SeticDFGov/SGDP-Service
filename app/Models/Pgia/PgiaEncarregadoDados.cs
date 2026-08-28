using app.Models;

namespace Models.Pgia;

/// <summary>
/// Designações de Encarregado de Dados / DPO (arts. 2º, X, 11 e 35, I; LGPD art. 41).
/// Comunicação à SGDI até 02/08/2026. No máximo uma designação ativa por órgão.
/// </summary>
public class PgiaEncarregadoDados
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

    public string ProcessoSeiComunicacao { get; set; } = string.Empty;

    // Até 02/08/2026 (art. 35, I)
    public DateOnly DataComunicacaoSgdi { get; set; }

    public DateOnly InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
