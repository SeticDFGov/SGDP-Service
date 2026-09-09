using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

// ── Relatório semestral do órgão à SGDI (art. 32) ────────────────────────────

public class PgiaRelatorioSemestralCreateDTO
{
    public short Ano { get; set; }

    // 1 ou 2
    public short Semestre { get; set; }
}

/// <summary>
/// Registro do envio pelo SEI. O status (no prazo/em atraso) é calculado no
/// servidor: prazo adotado = último dia do mês seguinte ao fim do semestre
/// (adaptação registrada; o decreto remete o prazo a norma complementar).
/// </summary>
public class PgiaRelatorioEnvioDTO
{
    public DateOnly DataEnvio { get; set; }

    [StringLength(25)]
    public string ProcessoSei { get; set; } = string.Empty;

    public long? DocumentoId { get; set; }
}

/// <summary>
/// Atos da SGDI sobre o relatório (art. 32, § único): inadimplência,
/// registro no painel analítico e comunicação ao controle interno.
/// </summary>
public class PgiaRelatorioSituacaoDTO
{
    // D25 (PgiaDominios.StatusRelatorioSemestral)
    [StringLength(30)]
    public string Status { get; set; } = string.Empty;

    public DateOnly? RegistradoPainelEm { get; set; }

    public DateOnly? ComunicadoControleInternoEm { get; set; }
}

public class PgiaRelatorioSemestralResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public short Ano { get; set; }

    public short Semestre { get; set; }

    public DateOnly? DataEnvio { get; set; }

    public string? ProcessoSei { get; set; }

    public long? DocumentoId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateOnly? RegistradoPainelEm { get; set; }

    public DateOnly? ComunicadoControleInternoEm { get; set; }

    public DateOnly PrazoEnvio { get; set; }

    public PgiaRelatorioConteudoResponse? Conteudo { get; set; }

    public DateTime CriadoEm { get; set; }
}

/// <summary>
/// Conteúdo agregado do período (art. 32, I a V), calculado das demais tabelas.
/// Alimenta a tela e o PDF (QuestPDF).
/// </summary>
public class PgiaRelatorioConteudoResponse
{
    // I — sistemas em uso/desenvolvimento e classificações
    public List<PgiaRelatorioSistemaItem> Sistemas { get; set; } = new();

    // II — incidentes e medidas adotadas no período
    public List<PgiaRelatorioIncidenteItem> Incidentes { get; set; } = new();

    // III — indicadores dos sistemas de Alto Risco
    public List<PgiaIndicadorResponse> IndicadoresAltoRisco { get; set; } = new();

    // IV — capacitação realizada
    public List<PgiaCapacitacaoResponse> Capacitacoes { get; set; } = new();

    // V — atualizações do inventário no período (sistemas criados/reclassificados)
    public List<PgiaRelatorioSistemaItem> AtualizacoesInventario { get; set; } = new();
}

public class PgiaRelatorioSistemaItem
{
    public long Id { get; set; }

    public string Denominacao { get; set; } = string.Empty;

    public string StatusCicloVida { get; set; } = string.Empty;

    public string? ClassificacaoRiscoAtual { get; set; }

    public string SituacaoHomologacao { get; set; } = string.Empty;
}

public class PgiaRelatorioIncidenteItem
{
    public long Id { get; set; }

    public string SistemaDenominacao { get; set; } = string.Empty;

    public string? Hipotese { get; set; }

    public DateTime? DataComunicacaoSgdi { get; set; }

    public string StatusApuracao { get; set; } = string.Empty;

    public string? MedidasAdotadas { get; set; }
}

// ── Relatório Anual de Governança de IA (arts. 7º, VII e 33) ─────────────────

public class PgiaRelatorioAnualCreateDTO
{
    public short Ano { get; set; }

    public DateOnly? DataPublicacao { get; set; }

    public string? UrlPublicacao { get; set; }

    public long? DocumentoId { get; set; }

    public DateOnly? ApreciadoCgticEm { get; set; }

    public string? Recomendacoes { get; set; }

    public string? AgendaInovacao { get; set; }
}

public class PgiaRelatorioAnualUpdateDTO : PgiaRelatorioAnualCreateDTO
{
}

public class PgiaRelatorioAnualResponse : PgiaRelatorioAnualCreateDTO
{
    public long Id { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Auditorias técnicas (arts. 12, I, 25, § 2º, 33, II e 34) ─────────────────

/// <summary>
/// Criada e designada pela SGDI. AuditorUserId é adaptação registrada: designa
/// a auditoria a um usuário com papel pgia_auditoria — é assim que o escopo
/// "só as auditorias designadas a ela" se resolve.
/// </summary>
public class PgiaAuditoriaCreateDTO
{
    public long SistemaIaId { get; set; }

    // D26 (PgiaDominios.TipoAuditoria)
    [StringLength(40)]
    public string Tipo { get; set; } = string.Empty;

    public string EntidadeAuditora { get; set; } = string.Empty;

    public Guid? AuditorUserId { get; set; }

    public bool ExternaFornecedor { get; set; }

    public bool? ApoioFapdf { get; set; }

    // Relatório completo da auditoria: documento do órgão do sistema auditado
    // ou documento central da SGDI (quem designa e publica a auditoria)
    public long? DocumentoId { get; set; }

    public DateOnly DataInicio { get; set; }
}

/// <summary>
/// A entidade auditora designada só edita o parecer e as datas dos trabalhos.
/// </summary>
public class PgiaAuditoriaParecerDTO
{
    public DateOnly DataInicio { get; set; }

    public DateOnly? DataFim { get; set; }

    public string? Parecer { get; set; }
}

/// <summary>
/// Publicação do resultado no Portal da Transparência (art. 34) — ato da SGDI.
/// </summary>
public class PgiaAuditoriaPublicacaoDTO
{
    public bool PublicadoPortal { get; set; }

    public DateOnly? DataPublicacao { get; set; }

    public string? Url { get; set; }
}

public class PgiaAuditoriaResponse
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public string SistemaDenominacao { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public string EntidadeAuditora { get; set; } = string.Empty;

    public Guid? AuditorUserId { get; set; }

    public string? AuditorNome { get; set; }

    public bool ExternaFornecedor { get; set; }

    public bool? ApoioFapdf { get; set; }

    public DateOnly DataInicio { get; set; }

    public DateOnly? DataFim { get; set; }

    public string? Parecer { get; set; }

    // Relatório completo anexado (null = ainda não anexado)
    public long? DocumentoId { get; set; }

    public bool PublicadoPortal { get; set; }

    public DateOnly? DataPublicacao { get; set; }

    public string? Url { get; set; }

    public DateTime CriadoEm { get; set; }
}

/// <summary>
/// Usuários com papel pgia_auditoria, para a SGDI designar (apoio de tela).
/// </summary>
public class PgiaAuditorResponse
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
