using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

// ── Não conformidades (arts. 8º, X e 9º, II) ─────────────────────────────────

public class PgiaNaoConformidadeCreateDTO
{
    // Preenchido pela SGDI ao registrar descumprimento de outro órgão;
    // ignorado (usa o órgão do usuário) quando quem registra é o SGTIC do órgão
    public long? OrgaoId { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // D36 (PgiaDominios.OrigemNaoConformidade)
    [StringLength(30)]
    public string Origem { get; set; } = string.Empty;

    public DateOnly? ReportadaSgdiEm { get; set; }
}

public class PgiaNaoConformidadeUpdateDTO
{
    public string Descricao { get; set; } = string.Empty;

    [StringLength(40)]
    public string Situacao { get; set; } = string.Empty;

    public DateOnly? ReportadaSgdiEm { get; set; }

    public DateOnly? ComunicadaControleInternoEm { get; set; }
}

public class PgiaNaoConformidadeResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Origem { get; set; } = string.Empty;

    public DateOnly DataRegistro { get; set; }

    public DateOnly? ReportadaSgdiEm { get; set; }

    public string Situacao { get; set; } = string.Empty;

    public DateOnly? ComunicadaControleInternoEm { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Capacitação ProCapIA/DF (arts. 28, 29 e 35, III) ─────────────────────────

public class PgiaCapacitacaoCreateDTO
{
    public Guid AgenteId { get; set; }

    // D19 (PgiaDominios.TrilhaCapacitacao)
    [StringLength(50)]
    public string Trilha { get; set; } = string.Empty;

    // D20 (PgiaDominios.StatusCapacitacao)
    [StringLength(20)]
    public string Status { get; set; } = "Prevista";

    // Obrigatória quando Status = Concluída
    public DateOnly? DataConclusao { get; set; }

    public bool PrevistaPlanoCapacitacao { get; set; }

    public long? CertificadoDocId { get; set; }
}

public class PgiaCapacitacaoUpdateDTO : PgiaCapacitacaoCreateDTO
{
}

public class PgiaCapacitacaoResponse
{
    public long Id { get; set; }

    public Guid AgenteId { get; set; }

    public string AgenteNome { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public string Trilha { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateOnly? DataConclusao { get; set; }

    public bool PrevistaPlanoCapacitacao { get; set; }

    public long? CertificadoDocId { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Registro de uso de IA (art. 13, IV e V) ──────────────────────────────────

public class PgiaRegistroUsoCreateDTO
{
    // Exatamente uma fonte: sistema do órgão OU plataforma homologada
    public long? SistemaIaId { get; set; }

    public long? PlataformaId { get; set; }

    public string ProdutoRef { get; set; } = string.Empty;

    [StringLength(25)]
    public string? ProcessoSei { get; set; }

    public DateOnly DataUso { get; set; }

    // Declaração de revisão crítica antes de divulgar, decidir ou usar (art. 13, V)
    public bool RevisaoHumanaConfirmada { get; set; }
}

/// <summary>
/// Fontes disponíveis para o registro de uso e para o aviso de incidente: sistemas do
/// órgão do agente e plataformas homologadas pela SGDI. Aberto a qualquer autenticado
/// com órgão resolvido (art. 13), sem exigir papel PGIA.
/// </summary>
public class PgiaUsoFonteSistema
{
    public long Id { get; set; }

    public string Denominacao { get; set; } = string.Empty;
}

public class PgiaUsoFontePlataforma
{
    public long Id { get; set; }

    public string Nome { get; set; } = string.Empty;
}

public class PgiaUsoFontesResponse
{
    public long? OrgaoId { get; set; }

    public string? OrgaoSigla { get; set; }

    public List<PgiaUsoFonteSistema> Sistemas { get; set; } = new();

    public List<PgiaUsoFontePlataforma> Plataformas { get; set; } = new();
}

public class PgiaRegistroUsoResponse
{
    public long Id { get; set; }

    public string AgenteNome { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public long? SistemaIaId { get; set; }

    public string? SistemaDenominacao { get; set; }

    public long? PlataformaId { get; set; }

    public string? PlataformaNome { get; set; }

    public string ProdutoRef { get; set; } = string.Empty;

    public string? ProcessoSei { get; set; }

    public DateOnly DataUso { get; set; }

    public bool RevisaoHumanaConfirmada { get; set; }

    public DateTime CriadoEm { get; set; }
}
