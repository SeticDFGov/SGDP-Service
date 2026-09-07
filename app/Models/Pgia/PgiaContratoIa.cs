namespace Models.Pgia;

/// <summary>
/// Contratos de aquisição de sistemas de IA e requisitos obrigatórios
/// (arts. 21, 25, 26 e 27). Tabela pgia_contrato_ia.
/// Divergência registrada: a coluna espelho sistema_ia.contrato_id do schema não
/// é criada — o vínculo vive só aqui, em sistema_ia_id, e a circularidade do
/// schema seria redundante no nosso fluxo.
/// </summary>
public class PgiaContratoIa
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public string NumeroContrato { get; set; } = string.Empty;

    public string ProcessoSei { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    public string FornecedorNome { get; set; } = string.Empty;

    // D32
    public string Status { get; set; } = "Vigente";

    // Condicional: Alto Risco e Risco Moderado (art. 25, I, a e II, a)
    public bool? PrevistoPdtic { get; set; }

    // Condicional: Risco Moderado, homologação técnica prévia (art. 25, II, b e c)
    public long? HomologacaoSgdiDocId { get; set; }

    // Obrigatória em todo contrato de IA (arts. 21 e 25, § 1º, e)
    public bool ClausulaVedacaoTreinamento { get; set; }

    // Exceção: autorização formal aprovada pelo CGTIC (art. 21)
    public long? AutorizacaoTreinamentoId { get; set; }

    public bool ReqExplicabilidade { get; set; }     // art. 25, § 1º, a

    public bool ReqAuditabilidade { get; set; }      // art. 25, § 1º, b

    public bool ReqPortabilidade { get; set; }       // art. 25, § 1º, c

    public bool ReqSemAprisionamento { get; set; }   // art. 25, § 1º, d

    public bool ReqAcessibilidade { get; set; }      // art. 25, § 1º, f

    // Condicional: Alto Risco (art. 25, § 2º)
    public bool? ClausulaAuditoriaIndependente { get; set; }

    public decimal? SlaDesempenho { get; set; }

    public decimal? SlaAcuracia { get; set; }

    public decimal? SlaEquidade { get; set; }

    public decimal? SlaDisponibilidade { get; set; }

    // Condicional: Alto Risco, ANS com penalidades (art. 25, § 2º)
    public string? SlaPenalidades { get; set; }

    // Condicional: após o Guia publicado, observância obrigatória (art. 26, § 1º)
    public bool? ConformeGuiaContratacoes { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
