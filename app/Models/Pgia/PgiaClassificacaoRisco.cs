using app.Models;

namespace Models.Pgia;

/// <summary>
/// Histórico de classificações de risco com o checklist dos arts. 15 a 17
/// (arts. 7º, III e 14 a 18). O resultado vigente fica desnormalizado em
/// PgiaSistemaIa.ClassificacaoRiscoAtual. Tabela pgia_classificacao_risco.
/// </summary>
public class PgiaClassificacaoRisco
{
    public long Id { get; set; }

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public DateOnly DataClassificacao { get; set; }

    // D31 (arts. 14, 16, § 2º e 37)
    public string Motivo { get; set; } = string.Empty;

    // jsonb: {"q15":[...],"q16":[...],"q17":[...]} por inciso marcado
    public string RespostasChecklist { get; set; } = string.Empty;

    // D01 (arts. 14 a 18)
    public string Resultado { get; set; } = string.Empty;

    // Soma dos pesos dos incisos marcados (PgiaQuesitos); metrificação da SGDI,
    // não exibida ao órgão que preenche
    public int Pontuacao { get; set; }

    // Artigo e inciso; obrigatório fora do Baixo Risco
    public string? EnquadramentoLegal { get; set; }

    // No Alto Risco, base da proposta fundamentada da SGDI ao CGTIC (art. 7º, III)
    public string Justificativa { get; set; } = string.Empty;

    // Agente que classificou (Users do SGDP)
    public Guid ClassificadoPor { get; set; }

    public User? ClassificadoPorUser { get; set; }

    // Condicional: Alto Risco é classificado pelo CGTIC (art. 7º, III)
    public long? DeliberacaoCgticId { get; set; }

    // Grupo "Outros" do questionário: riscos declarados pelo órgão (matriz da CGDF).
    // Complementares — não entram no cálculo do resultado nem na pontuação.
    public List<PgiaRiscoOutro> OutrosRiscos { get; set; } = new();

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
