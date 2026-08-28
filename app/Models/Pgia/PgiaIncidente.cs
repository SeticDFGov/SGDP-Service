using app.Models;

namespace Models.Pgia;

/// <summary>
/// Comunicação e apuração de incidentes graves (arts. 8º, IX, 10, IV, 13, II, 30 e 32, II).
/// Tabela pgia_incidente.
/// </summary>
public class PgiaIncidente
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    /// <summary>
    /// D15, incisos do art. 30. Divergência registrada do schema, que a declara NOT NULL:
    /// aqui o agente avisa o Responsável de IA antes (art. 13, II) e só a comunicação
    /// formal à SGDI exige o enquadramento, então o aviso nasce sem hipótese.
    /// </summary>
    public string? Hipotese { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public DateTime DataOcorrencia { get; set; }

    public DateTime DataDeteccao { get; set; }

    // Agente notifica o Responsável de IA (art. 13, II)
    public DateTime NotificadoResponsavelEm { get; set; }

    public Guid ComunicadoPor { get; set; }

    public User? ComunicadoPorUser { get; set; }

    // Pelo SEI, no prazo de norma complementar (art. 30, caput).
    // Nulo enquanto o incidente é só o aviso interno (divergência do schema, idem Hipotese).
    public DateTime? DataComunicacaoSgdi { get; set; }

    public string? ProcessoSei { get; set; }

    // D16 (arts. 8º, IX e 30, § único)
    public string StatusApuracao { get; set; } = PgiaDominios.StatusApuracao.Recebida;

    public string? RecomendacoesSgdi { get; set; }

    public bool? PropostaCgtic { get; set; }

    public DateOnly? EncaminhadoOrgaoCompetenteEm { get; set; }

    public bool SistemaSuspenso { get; set; }

    public DateOnly? DataSuspensao { get; set; }

    // Compõem o relatório semestral (art. 32, II)
    public string? MedidasAdotadas { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
