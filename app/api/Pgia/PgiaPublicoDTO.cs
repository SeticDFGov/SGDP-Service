using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

// ── Registro Público de Sistemas de IA (art. 24) — endpoint anônimo ──────────

/// <summary>
/// SOMENTE os campos do art. 24, incisos I a V (v_registro_publico do schema).
/// Nunca inclui dados pessoais nem campos internos do inventário.
/// </summary>
public class PgiaRegistroPublicoItemResponse
{
    public long Id { get; set; }

    public string Denominacao { get; set; } = string.Empty;        // I

    public string Finalidade { get; set; } = string.Empty;         // I

    public string OrgaoSigla { get; set; } = string.Empty;         // I

    public string OrgaoNome { get; set; } = string.Empty;

    public string? ClassificacaoRiscoAtual { get; set; }           // II

    public string? NaturezaDecisoes { get; set; }                  // III

    public string? EfeitosCidadao { get; set; }                    // III

    public string? AiaResultadoUrl { get; set; }                   // IV

    public int IndicadoresPublicados { get; set; }                 // V

    public DateOnly? DataPublicacaoRegistro { get; set; }
}

// ── Solicitação do cidadão (arts. 11, IV e 23) — endpoint anônimo ────────────

/// <summary>
/// Abertura pública, com validação de entrada e limites de tamanho.
/// O protocolo é gerado no servidor; o sistema escolhido deve estar publicado.
/// </summary>
public class PgiaSolicitacaoCreateDTO
{
    public long SistemaIaId { get; set; }

    // D21 (PgiaDominios.TipoSolicitacao)
    [StringLength(50)]
    public string Tipo { get; set; } = string.Empty;

    [StringLength(200)]
    public string SolicitanteNome { get; set; } = string.Empty;

    [StringLength(200)]
    public string SolicitanteContato { get; set; } = string.Empty;

    // Condicional: revisão de decisão e impugnação
    [StringLength(300)]
    public string? ReferenciaDecisao { get; set; }

    [StringLength(4000)]
    public string Descricao { get; set; } = string.Empty;
}

/// <summary>
/// Devolvido na abertura: só o protocolo para acompanhamento.
/// </summary>
public class PgiaSolicitacaoProtocoloResponse
{
    public string Protocolo { get; set; } = string.Empty;
}

/// <summary>
/// Acompanhamento público pelo protocolo: sem os dados pessoais do solicitante
/// (minimização, art. 19, II).
/// </summary>
public class PgiaSolicitacaoPublicaResponse
{
    public string Protocolo { get; set; } = string.Empty;

    public string SistemaDenominacao { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public DateTime DataAbertura { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Resposta { get; set; }

    public DateTime? DataResposta { get; set; }
}

// ── Tratamento pelo órgão (área autenticada) ─────────────────────────────────

public class PgiaSolicitacaoOrgaoResponse : PgiaSolicitacaoPublicaResponse
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public string SolicitanteNome { get; set; } = string.Empty;

    public string SolicitanteContato { get; set; } = string.Empty;

    public string? ReferenciaDecisao { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string? RespondidoPorNome { get; set; }

    public bool EncaminhadaDpo { get; set; }
}

/// <summary>
/// Resposta em linguagem simples por agente competente (art. 23, II e III).
/// </summary>
public class PgiaSolicitacaoRespostaDTO
{
    public string Resposta { get; set; } = string.Empty;
}
