using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Aviso rápido de problema (art. 13, II): qualquer agente do órgão notifica o
/// Responsável de IA. A comunicação formal à SGDI vem depois, pelo Responsável.
/// </summary>
public class PgiaIncidenteAvisoDTO
{
    public long SistemaIaId { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // Opcionais no aviso; a hipótese vira obrigatória na comunicação formal
    [StringLength(60)]
    public string? Hipotese { get; set; }

    public DateTime? DataOcorrencia { get; set; }

    public DateTime? DataDeteccao { get; set; }
}

/// <summary>
/// Registro formal do incidente pelo Responsável de IA, já com a comunicação
/// à SGDI pelo SEI (arts. 13, II e 30).
/// </summary>
public class PgiaIncidenteCreateDTO
{
    public long SistemaIaId { get; set; }

    // D15 (PgiaDominios.HipoteseIncidente): incisos I a V do art. 30
    [StringLength(60)]
    public string Hipotese { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public DateTime DataOcorrencia { get; set; }

    public DateTime DataDeteccao { get; set; }

    public DateTime NotificadoResponsavelEm { get; set; }

    public DateTime DataComunicacaoSgdi { get; set; }

    [StringLength(25)]
    public string ProcessoSei { get; set; } = string.Empty;

    public string? MedidasAdotadas { get; set; }
}

/// <summary>
/// Comunicação formal de um aviso já registrado (completa hipótese, SEI e data).
/// </summary>
public class PgiaIncidenteComunicarDTO
{
    [StringLength(60)]
    public string Hipotese { get; set; } = string.Empty;

    // Corrigem as datas aproximadas informadas no aviso, quando a apuração interna
    // do órgão precisa a ocorrência e a detecção
    public DateTime? DataOcorrencia { get; set; }

    public DateTime? DataDeteccao { get; set; }

    public DateTime DataComunicacaoSgdi { get; set; }

    [StringLength(25)]
    public string ProcessoSei { get; set; } = string.Empty;

    public string? MedidasAdotadas { get; set; }
}

/// <summary>
/// Apuração pela SGDI (arts. 8º, IX e 30, § único).
/// </summary>
public class PgiaIncidenteApuracaoDTO
{
    // D16 (PgiaDominios.StatusApuracao)
    [StringLength(40)]
    public string StatusApuracao { get; set; } = string.Empty;

    public string? RecomendacoesSgdi { get; set; }

    public bool? PropostaCgtic { get; set; }

    public DateOnly? EncaminhadoOrgaoCompetenteEm { get; set; }

    public bool SistemaSuspenso { get; set; }

    public DateOnly? DataSuspensao { get; set; }
}

public class PgiaIncidenteMedidasDTO
{
    public string? MedidasAdotadas { get; set; }
}

public class PgiaIncidenteResponse
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public string SistemaDenominacao { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? Hipotese { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public DateTime DataOcorrencia { get; set; }

    public DateTime DataDeteccao { get; set; }

    public DateTime NotificadoResponsavelEm { get; set; }

    public string ComunicadoPorNome { get; set; } = string.Empty;

    public DateTime? DataComunicacaoSgdi { get; set; }

    public string? ProcessoSei { get; set; }

    public string StatusApuracao { get; set; } = string.Empty;

    public string? RecomendacoesSgdi { get; set; }

    public bool? PropostaCgtic { get; set; }

    public DateOnly? EncaminhadoOrgaoCompetenteEm { get; set; }

    public bool SistemaSuspenso { get; set; }

    public DateOnly? DataSuspensao { get; set; }

    public string? MedidasAdotadas { get; set; }

    public DateTime CriadoEm { get; set; }
}
