using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

// ── Deliberações do CGTIC (art. 7º) ──────────────────────────────────────────

public class PgiaDeliberacaoCreateDTO
{
    // D13 (PgiaDominios.TipoDeliberacao)
    [StringLength(50)]
    public string Tipo { get; set; } = string.Empty;

    public long? SistemaIaId { get; set; }

    // Objeto da deliberação quando for um contrato de IA (arts. 7º, IV e 25, I)
    public long? ContratoId { get; set; }

    public DateOnly DataDeliberacao { get; set; }

    // D14 (PgiaDominios.ResultadoDeliberacao)
    [StringLength(20)]
    public string Resultado { get; set; } = string.Empty;

    [StringLength(30)]
    public string? NumeroAto { get; set; }

    public string? Ementa { get; set; }

    // Metadados da ata ou ato formal já cadastrados (pgia_documento)
    public long? DocumentoId { get; set; }
}

public class PgiaDeliberacaoResponse
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public long? SistemaIaId { get; set; }

    public long? ContratoId { get; set; }

    public string? SistemaDenominacao { get; set; }

    public string? OrgaoSigla { get; set; }

    public DateOnly DataDeliberacao { get; set; }

    public string Resultado { get; set; } = string.Empty;

    public string? NumeroAto { get; set; }

    public string? Ementa { get; set; }

    public long? DocumentoId { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Plataformas públicas de IA generativa (arts. 8º, IV, 18 e 20) ────────────

public class PgiaPlataformaCreateDTO
{
    public string Nome { get; set; } = string.Empty;

    public string? Fornecedor { get; set; }

    public string? Url { get; set; }

    // D17 (PgiaDominios.StatusHomologacaoPlataforma)
    [StringLength(50)]
    public string StatusHomologacao { get; set; } = "Em avaliação";

    public bool AptaDadosPessoaisSigilosos { get; set; }

    [StringLength(30)]
    public string? AtoHomologacao { get; set; }

    public DateOnly? DataAto { get; set; }

    public DateOnly? PublicadaRelacaoEm { get; set; }

    public string? DiretrizesUso { get; set; }
}

public class PgiaPlataformaUpdateDTO : PgiaPlataformaCreateDTO
{
}

public class PgiaPlataformaResponse : PgiaPlataformaCreateDTO
{
    public long Id { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Normas complementares (arts. 8º, III, 26 e 36) ───────────────────────────

public class PgiaNormaCreateDTO
{
    // D24 (PgiaDominios.TipoNorma)
    [StringLength(40)]
    public string Tipo { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Numero { get; set; }

    public string Ementa { get; set; } = string.Empty;

    // PgiaDominios.EmissorNorma
    [StringLength(20)]
    public string Emissor { get; set; } = string.Empty;

    public DateOnly DataPublicacao { get; set; }

    public string? Url { get; set; }

    public bool Vigente { get; set; } = true;

    // Guia e atualizações de requisitos exigem aprovação prévia do CGTIC (art. 26, §§ 1º e 2º)
    public DateOnly? AprovadaCgticEm { get; set; }
}

public class PgiaNormaUpdateDTO : PgiaNormaCreateDTO
{
}

public class PgiaNormaResponse : PgiaNormaCreateDTO
{
    public long Id { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Autorizações excepcionais (arts. 18, § 2º e 21) ──────────────────────────

public class PgiaAutorizacaoCreateDTO
{
    // D18 (PgiaDominios.TipoAutorizacao)
    [StringLength(60)]
    public string Tipo { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public long? PlataformaId { get; set; }

    // Objeto da autorização quando for um contrato de IA (art. 21)
    public long? ContratoId { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    public long? AvaliacaoRiscosDocId { get; set; }

    // D34 (PgiaDominios.AutorizadaPor)
    [StringLength(30)]
    public string AutorizadaPor { get; set; } = string.Empty;

    // Obrigatória quando o tipo envolve treinamento (art. 21)
    public long? DeliberacaoCgticId { get; set; }

    public DateOnly DataAutorizacao { get; set; }

    public DateOnly? VigenciaFim { get; set; }
}

/// <summary>
/// Atualização da autorização, inclusive a revogação (Ativo = false).
/// </summary>
public class PgiaAutorizacaoUpdateDTO : PgiaAutorizacaoCreateDTO
{
    public bool Ativo { get; set; } = true;
}

public class PgiaAutorizacaoResponse : PgiaAutorizacaoCreateDTO
{
    public long Id { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? PlataformaNome { get; set; }

    public bool Ativo { get; set; }

    public DateTime CriadoEm { get; set; }
}

// ── Homologação do inventário (desenho da chefia) e Registro Público ─────────

/// <summary>
/// Avaliação da SGDI para sistemas de risco Moderado ou Baixo (Aprovar/Vetar com parecer).
/// Sistemas Alto/Excessivo são decididos por deliberação do CGTIC.
/// </summary>
public class PgiaAvaliacaoHomologacaoDTO
{
    public bool Aprovado { get; set; }

    public string Parecer { get; set; } = string.Empty;
}

/// <summary>
/// Item da fila de homologação: o sistema com a classificação vigente aberta
/// (checklist, pontuação e enquadramento) para a instância avaliadora.
/// </summary>
public class PgiaHomologacaoPendenteResponse
{
    public PgiaSistemaResponse Sistema { get; set; } = new();

    public PgiaClassificacaoResponse? ClassificacaoVigente { get; set; }
}

/// <summary>
/// Resumo do sistema para as instâncias centrais escolherem sobre quem deliberar
/// (ex.: suspensão do art. 30), sem carregar o inventário inteiro.
/// </summary>
public class PgiaSistemaResumoResponse
{
    public long Id { get; set; }

    public string Denominacao { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? ClassificacaoRiscoAtual { get; set; }

    public string StatusCicloVida { get; set; } = string.Empty;

    public string SituacaoHomologacao { get; set; } = string.Empty;
}

/// <summary>
/// Marcação de publicação do sistema no Registro Público (art. 24) — ato da SGDI.
/// </summary>
public class PgiaRegistroPublicoDTO
{
    public bool Publicado { get; set; }

    public DateOnly? DataPublicacao { get; set; }
}
