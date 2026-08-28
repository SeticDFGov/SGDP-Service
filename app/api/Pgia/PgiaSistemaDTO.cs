using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Respostas do questionário de classificação (arts. 15 a 17): incisos marcados
/// por artigo, em algarismos romanos ("I".."IX"). Grava em jsonb como
/// {"q15":[...],"q16":[...],"q17":[...]} (formato do schema_pgia_48901.sql).
/// </summary>
public class PgiaChecklistDTO
{
    public List<string> Q15 { get; set; } = new();

    public List<string> Q16 { get; set; } = new();

    public List<string> Q17 { get; set; } = new();
}

/// <summary>
/// Classificação de risco de um sistema. O resultado e o enquadramento legal são
/// calculados no servidor a partir do checklist (o front mostra a prévia).
/// </summary>
public class PgiaClassificacaoCreateDTO
{
    public PgiaChecklistDTO Checklist { get; set; } = new();

    // D31 (PgiaDominios.MotivoClassificacao)
    [StringLength(40)]
    public string Motivo { get; set; } = string.Empty;

    public DateOnly DataClassificacao { get; set; }

    public string Justificativa { get; set; } = string.Empty;
}

/// <summary>
/// Campos de inventário do sistema de IA (etapa 2 do formulário, art. 2º, XI e arts. 14 a 20).
/// Os condicionais são validados no service conforme o formulário/decreto.
/// </summary>
public class PgiaSistemaBaseDTO
{
    public string Denominacao { get; set; } = string.Empty;

    public string Finalidade { get; set; } = string.Empty;

    // varchar(40): o valor de domínio "Instrumento vigente em revisão" não cabe
    // no varchar(20) do schema_pgia_48901.sql (defeito do schema, corrigido aqui)
    [StringLength(40)]
    public string OrigemRegistro { get; set; } = string.Empty;

    // Condicional: obrigatória quando OrigemRegistro = "Outros"
    public string? OrigemRegistroDescricao { get; set; }

    [StringLength(40)]
    public string TipoSistema { get; set; } = string.Empty;

    [StringLength(40)]
    public string Tecnologia { get; set; } = string.Empty;

    [StringLength(30)]
    public string StatusCicloVida { get; set; } = string.Empty;

    // Condicional: obrigatória se em uso (Implantado/Monitoramento)
    public DateOnly? DataImplantacao { get; set; }

    [StringLength(30)]
    public string EscopoDados { get; set; } = string.Empty;

    public bool AfetaCidadao { get; set; }

    // Condicionais: obrigatórios se AfetaCidadao (art. 24, III)
    public string? NaturezaDecisoes { get; set; }

    public string? EfeitosCidadao { get; set; }

    // Bloco LGPD — condicionais: obrigatórios se EscopoDados inclui dados pessoais (art. 19)
    [StringLength(60)]
    public string? BaseLegalLgpd { get; set; }

    public string? CategoriasDadosPessoais { get; set; }

    public string? FinalidadeTratamentoDados { get; set; }

    public string? MedidasSeguranca { get; set; }

    public bool InteroperavelPadroesSgdi { get; set; }

    // Condicional: obrigatória se TipoSistema = Contratado (art. 27)
    public string? JustificativaNaoRedundancia { get; set; }

    public DateOnly? DataAnaliseSgtic { get; set; }

    public string? ParecerSgtic { get; set; }

    // Condicional Alto Risco: obrigatória (arts. 5º, II e 23, II)
    public string? SupervisaoHumanaDescricao { get; set; }

    // Condicional Risco Moderado: obrigatório (art. 17, § 1º)
    public bool? AvisoInteracaoIa { get; set; }

    // Condicional: se gera conteúdo sintético, quando tecnicamente viável (art. 17, § 2º)
    public bool? IdentificadorAutenticidade { get; set; }

    [StringLength(25)]
    public string? ProcessoSei { get; set; }

    // Remessa ao Inventário Centralizado (arts. 9º, III e 10, II)
    public DateOnly? ComunicadoSgdiEm { get; set; }
}

/// <summary>
/// Criação de sistema no inventário: inventário + classificação inicial num só envio,
/// como no formulário do mock.
/// </summary>
public class PgiaSistemaCreateDTO : PgiaSistemaBaseDTO
{
    public PgiaClassificacaoCreateDTO Classificacao { get; set; } = new();
}

/// <summary>
/// Atualização dos campos de inventário (reclassificação é endpoint próprio).
/// </summary>
public class PgiaSistemaUpdateDTO : PgiaSistemaBaseDTO
{
}

public class PgiaSistemaResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string Denominacao { get; set; } = string.Empty;

    public string Finalidade { get; set; } = string.Empty;

    public string OrigemRegistro { get; set; } = string.Empty;

    public string? OrigemRegistroDescricao { get; set; }

    public string TipoSistema { get; set; } = string.Empty;

    public string Tecnologia { get; set; } = string.Empty;

    public string StatusCicloVida { get; set; } = string.Empty;

    public DateOnly? DataImplantacao { get; set; }

    public DateOnly? DataDescontinuacao { get; set; }

    public string EscopoDados { get; set; } = string.Empty;

    public bool AfetaCidadao { get; set; }

    public string? NaturezaDecisoes { get; set; }

    public string? EfeitosCidadao { get; set; }

    public string? ClassificacaoRiscoAtual { get; set; }

    public string? EnquadramentoLegal { get; set; }

    public string? SupervisaoHumanaDescricao { get; set; }

    public bool? AvisoInteracaoIa { get; set; }

    public bool? IdentificadorAutenticidade { get; set; }

    public string? BaseLegalLgpd { get; set; }

    public string? CategoriasDadosPessoais { get; set; }

    public string? FinalidadeTratamentoDados { get; set; }

    public string? MedidasSeguranca { get; set; }

    public bool InteroperavelPadroesSgdi { get; set; }

    public string? JustificativaNaoRedundancia { get; set; }

    public DateOnly? DataAnaliseSgtic { get; set; }

    public string? ParecerSgtic { get; set; }

    public long ResponsavelIaId { get; set; }

    public string ResponsavelNome { get; set; } = string.Empty;

    public string? ProcessoSei { get; set; }

    public DateOnly? ComunicadoSgdiEm { get; set; }

    public bool PublicadoRegistroPublico { get; set; }

    public DateOnly? DataPublicacaoRegistro { get; set; }

    // Homologação do inventário (PgiaDominios.SituacaoHomologacao):
    // Baixo/Moderado avaliados pela SGDI; Alto/Excessivo delegados ao CGTIC
    public string SituacaoHomologacao { get; set; } = string.Empty;

    public string? AvaliacaoParecer { get; set; }

    public string? AvaliadoPorNome { get; set; }

    public DateTime? AvaliadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
}

public class PgiaClassificacaoResponse
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public DateOnly DataClassificacao { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public PgiaChecklistDTO Checklist { get; set; } = new();

    public string Resultado { get; set; } = string.Empty;

    // Métrica de acompanhamento da SGDI: só vem preenchida para SGDI, CGTIC e admin
    public int? Pontuacao { get; set; }

    public string? EnquadramentoLegal { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    public string ClassificadoPorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}
