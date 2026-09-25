using Models.Pgia;

namespace Models.Planejamento;

/// <summary>
/// Um registro de inadimplência de um órgão (E8; art. 7º, § 3º, e art. 11 do Decreto nº
/// 48.899/2026). Nasce com a notificação da SGDI ao titular do órgão (a obrigação pendente, o
/// prazo descumprido, a data, o documento e o SEI), com o prazo de 5 dias úteis para regularizar
/// ou justificar (DateTimeHelper.AdicionarDiasUteis). Depois: a justificativa aceita encerra
/// (justificado); vencido o prazo sem regularização nem justificativa aceita, a SGDI registra a
/// inadimplência com o motivo e a nota de motivação (inadimplente); o saneamento, de notificado
/// ou de inadimplente, guarda a data e tira a marca do painel (art. 11, § 2º). A marca vigente é
/// a notificada ou a inadimplente. Tabela pe_inadimplencia; mapeamento em PeModelConfiguration.
/// </summary>
public class PeInadimplencia : IPeAuditavel
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // A comunicação pendente ("Comunicar à SGDI o PDTIC aprovado pelo comitê interno (art. 7º, V, do Decreto nº 48.899/2026)")
    public string Obrigacao { get; set; } = string.Empty;

    // O prazo que o órgão descumpriu, como a notificação diz
    public string PrazoDescumprido { get; set; } = string.Empty;

    public DateOnly NotificadoEm { get; set; }

    // O documento da notificação ("Ofício nº 12/2027-SGDI")
    public string Documento { get; set; } = string.Empty;

    // Processo SEI (00000-00000000/0000-00), opcional
    public string? Sei { get; set; }

    // O último dia para regularizar: 5 dias úteis depois da notificação
    public DateOnly Prazo { get; set; }

    // PeDominios.SituacaoInadimplencia; token de concorrência (duas pessoas decidindo ao mesmo tempo)
    public string Situacao { get; set; } = PeDominios.SituacaoInadimplencia.Notificado;

    // A justificativa aceita (situação justificado)
    public string? Justificativa { get; set; }

    // PeDominios.MotivoInadimplencia, no registro da inadimplência
    public string? Motivo { get; set; }

    // A nota de motivação que acompanha a comunicação ao controle interno (art. 11, § 1º)
    public string? NotaMotivacao { get; set; }

    // Quando a SGDI comunicou ao órgão de controle interno (art. 7º, § 3º), se já comunicou
    public DateOnly? ComunicadoControleEm { get; set; }

    // O registro da inadimplência (quem e quando)
    public DateTime? RegistradoEm { get; set; }

    public string? RegistradoPor { get; set; }

    // A data da regularização (art. 11, § 2º)
    public DateOnly? SaneadoEm { get; set; }

    // Observação do saneamento
    public string? Observacao { get; set; }

    // A notificação (quem registrou e quando)
    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    // A última mudança (justificativa, registro ou saneamento)
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
