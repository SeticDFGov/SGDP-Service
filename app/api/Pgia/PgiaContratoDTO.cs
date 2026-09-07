using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

// ── Contratos de IA (arts. 21, 25, 26 e 27) ──────────────────────────────────

/// <summary>
/// Contrato de aquisição de sistema de IA. Os condicionais por risco (art. 25)
/// valem quando há sistema vinculado com classificação: Alto/Moderado → PDTIC;
/// Moderado → homologação técnica da SGDI; Alto → auditoria independente + SLAs.
/// Sem cláusula de vedação de treinamento, exige autorização excepcional (art. 21).
/// </summary>
public class PgiaContratoCreateDTO
{
    public long? SistemaIaId { get; set; }

    [StringLength(30)]
    public string NumeroContrato { get; set; } = string.Empty;

    [StringLength(25)]
    public string ProcessoSei { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    public string FornecedorNome { get; set; } = string.Empty;

    // D32 (PgiaDominios.StatusContrato)
    [StringLength(20)]
    public string Status { get; set; } = "Vigente";

    public bool? PrevistoPdtic { get; set; }

    public long? HomologacaoSgdiDocId { get; set; }

    public bool ClausulaVedacaoTreinamento { get; set; }

    public long? AutorizacaoTreinamentoId { get; set; }

    public bool ReqExplicabilidade { get; set; }

    public bool ReqAuditabilidade { get; set; }

    public bool ReqPortabilidade { get; set; }

    public bool ReqSemAprisionamento { get; set; }

    public bool ReqAcessibilidade { get; set; }

    public bool? ClausulaAuditoriaIndependente { get; set; }

    public decimal? SlaDesempenho { get; set; }

    public decimal? SlaAcuracia { get; set; }

    public decimal? SlaEquidade { get; set; }

    public decimal? SlaDisponibilidade { get; set; }

    public string? SlaPenalidades { get; set; }

    public bool? ConformeGuiaContratacoes { get; set; }
}

public class PgiaContratoUpdateDTO : PgiaContratoCreateDTO
{
}

public class PgiaContratoResponse : PgiaContratoCreateDTO
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? SistemaDenominacao { get; set; }

    public string? SistemaClassificacao { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Triagem de instrumentos anteriores ao decreto (art. 37) ──────────────────

public class PgiaLegadoCreateDTO
{
    // D30 (PgiaDominios.TipoInstrumentoLegado)
    [StringLength(30)]
    public string TipoInstrumento { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Numero { get; set; }

    // D29 (PgiaDominios.EnvolveIa); Incerto é tratado como sim até confirmação
    [StringLength(10)]
    public string EnvolveIa { get; set; } = "Incerto";

    public DateOnly? DataTriagem { get; set; }

    public bool Revisado { get; set; }

    public DateOnly? DataRevisao { get; set; }

    public bool? AditivoClausulaTreinamento { get; set; }

    public DateOnly? DataAditivo { get; set; }

    [StringLength(25)]
    public string? ProcessoSei { get; set; }

    public long? ComprovacaoDocId { get; set; }

    // Registro criado no inventário quando envolve IA (origem "Instrumento vigente em revisão")
    public long? SistemaIaId { get; set; }
}

public class PgiaLegadoUpdateDTO : PgiaLegadoCreateDTO
{
}

public class PgiaLegadoResponse : PgiaLegadoCreateDTO
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? TriadoPorNome { get; set; }

    public string? SistemaDenominacao { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Indicadores de desempenho (arts. 24, V, 25, § 2º, 31 e 32, III) ──────────

/// <summary>
/// Período de referência em duas datas (adaptação registrada: o schema usa
/// daterange; duas colunas date são equivalentes e portáveis no EF).
/// </summary>
public class PgiaIndicadorCreateDTO
{
    public string Nome { get; set; } = string.Empty;

    // D23 (PgiaDominios.CategoriaIndicador)
    [StringLength(30)]
    public string Categoria { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    [StringLength(20)]
    public string? Unidade { get; set; }

    public DateOnly PeriodoInicio { get; set; }

    public DateOnly PeriodoFim { get; set; }

    // Condicional: quando houver ANS (art. 25, § 2º)
    public decimal? Meta { get; set; }

    public bool PublicadoRegistroPublico { get; set; }
}

public class PgiaIndicadorResponse : PgiaIndicadorCreateDTO
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public string SistemaDenominacao { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}
