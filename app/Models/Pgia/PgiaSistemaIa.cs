using app.Models;

namespace Models.Pgia;

/// <summary>
/// Inventário de sistemas de IA em uso, em desenvolvimento ou em aquisição
/// (arts. 2º, XI, 8º, II, 9º, I, 14 a 20, 24 e 27). Tabela pgia_sistema_ia;
/// mapeamento em PgiaModelConfiguration.
/// </summary>
public class PgiaSistemaIa
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // Vai ao Registro Público (art. 24, I)
    public string Denominacao { get; set; } = string.Empty;

    // Vai ao Registro Público (art. 24, I)
    public string Finalidade { get; set; } = string.Empty;

    // D06 (art. 37)
    public string OrigemRegistro { get; set; } = string.Empty;

    // Condicional: obrigatória quando a origem é "Outros"
    public string? OrigemRegistroDescricao { get; set; }

    // D07 (arts. 1º e 2º, III)
    public string TipoSistema { get; set; } = string.Empty;

    // D08. IA generativa tem regras próprias (art. 2º, I, II e VI)
    public string Tecnologia { get; set; } = string.Empty;

    // D05 (art. 2º, VIII e XI)
    public string StatusCicloVida { get; set; } = string.Empty;

    // Condicional: se em uso. Em operação antes de 03/07/2026 entra no prazo do art. 35, IV
    public DateOnly? DataImplantacao { get; set; }

    public DateOnly? DataDescontinuacao { get; set; }

    // D09 (arts. 18 a 20)
    public string EscopoDados { get; set; } = string.Empty;

    // Abre natureza_decisoes, efeitos_cidadao e os direitos do art. 23 (arts. 2º, V e 24, III)
    public bool AfetaCidadao { get; set; }

    // Condicional: se afeta cidadão (art. 24, III)
    public string? NaturezaDecisoes { get; set; }

    // Condicional: se afeta cidadão (art. 24, III)
    public string? EfeitosCidadao { get; set; }

    // D01. Desnormalizada da última classificação; o service mantém (arts. 14 e 24, II)
    public string? ClassificacaoRiscoAtual { get; set; }

    // Artigo e inciso, exceto Baixo Risco. Ex.: art. 16, III (arts. 15 a 18)
    public string? EnquadramentoLegal { get; set; }

    // Condicional: Alto Risco (arts. 5º, II, 13, V e 23, II)
    public string? SupervisaoHumanaDescricao { get; set; }

    // Condicional: Risco Moderado (art. 17, § 1º)
    public bool? AvisoInteracaoIa { get; set; }

    // Condicional: se gera conteúdo sintético, quando tecnicamente viável (art. 17, § 2º)
    public bool? IdentificadorAutenticidade { get; set; }

    // D27. Condicional: se trata dado pessoal (art. 19, I)
    public string? BaseLegalLgpd { get; set; }

    // Condicional: minimização (art. 19, II)
    public string? CategoriasDadosPessoais { get; set; }

    // Condicional: vedado reúso para finalidade diversa (art. 19, III)
    public string? FinalidadeTratamentoDados { get; set; }

    // Condicional: padrões da SGDI (art. 19, IV)
    public string? MedidasSeguranca { get; set; }

    // Art. 27
    public bool InteroperavelPadroesSgdi { get; set; }

    // Condicional: se contratado (art. 27)
    public string? JustificativaNaoRedundancia { get; set; }

    public DateOnly? DataAnaliseSgtic { get; set; }

    // Análise de adoção pelo SGTIC; Alto Risco segue ao CGTIC (art. 9º, I)
    public string? ParecerSgtic { get; set; }

    // Designação vigente de Responsável de IA do órgão no momento do cadastro (art. 10)
    public long ResponsavelIaId { get; set; }

    public PgiaResponsavelIa? Responsavel { get; set; }

    // Contratação ou operação
    public string? ProcessoSei { get; set; }

    // Remessa ao Inventário Centralizado (arts. 9º, III, 10, II, 25, III e 35, II)
    public DateOnly? ComunicadoSgdiEm { get; set; }

    // ── Homologação do inventário (fase 2) ────────────────────────────────────

    // Rota e situação da avaliação central: SGDI decide Baixo/Moderado,
    // CGTIC delibera sobre Alto/Excessivo (PgiaDominios.SituacaoHomologacao)
    public string SituacaoHomologacao { get; set; } = PgiaDominios.SituacaoHomologacao.AguardandoSgdi;

    public string? AvaliacaoParecer { get; set; }

    // Quem decidiu: agente da SGDI ou quem registrou a deliberação do CGTIC
    public Guid? AvaliadoPor { get; set; }

    public User? AvaliadoPorUser { get; set; }

    public DateTime? AvaliadoEm { get; set; }

    // Deliberação do CGTIC que decidiu a homologação, quando delegada ao comitê
    public long? DeliberacaoHomologacaoId { get; set; }

    // Registro Público no Portal da Transparência (art. 24)
    public bool PublicadoRegistroPublico { get; set; }

    public DateOnly? DataPublicacaoRegistro { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
