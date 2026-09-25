namespace Models.Planejamento;

/// <summary>
/// Configurações gerais do módulo, uma por chave, com o valor em jsonb: a versão do
/// modelo inicial já carregada (seed_modelo_versao), a periodicidade padrão do
/// monitoramento (periodicidade_monitoramento_padrao) e, desde a E7 (rodada B), os dias de
/// prazo para fechar um ciclo depois do fim dele (prazo_fechamento_ciclo_dias, 15) e quantos
/// dias antes do fim da vigência a etapa 7 fica disponível (dias_avaliacao_final, 90). Tabela
/// pe_configuracao.
/// </summary>
public class PeConfiguracao : IPeAuditavel
{
    public const string ChaveVersaoModelo = "seed_modelo_versao";
    public const string ChavePeriodicidadeMonitoramento = "periodicidade_monitoramento_padrao";
    public const string ChavePrazoFechamentoCiclo = "prazo_fechamento_ciclo_dias";
    public const string ChaveDiasAvaliacaoFinal = "dias_avaliacao_final";

    // Os valores quando a configuração não existe (antes de o carregador trazer a versão 6)
    public const int PrazoFechamentoCicloPadrao = 15;
    public const int DiasAvaliacaoFinalPadrao = 90;

    public string Chave { get; set; } = string.Empty;

    // jsonb
    public string Valor { get; set; } = "null";

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// O que mudou no modelo: cada criação, alteração, mudança de situação, de ordem,
/// exclusão ou remoção vira uma linha, com o antes e o depois em jsonb, quando e quem.
/// Também guarda o nível e os ajustes de cada órgão (entidade_id = id do órgão). Tabela
/// pe_modelo_historico.
/// </summary>
public class PeModeloHistorico
{
    public long Id { get; set; }

    // PeDominios.EntidadeHistorico
    public string Entidade { get; set; } = string.Empty;

    public long EntidadeId { get; set; }

    // PeDominios.AcaoHistorico
    public string Acao { get; set; } = string.Empty;

    // jsonb; nulo na criação (antes) e na remoção (depois)
    public string? Antes { get; set; }

    public string? Depois { get; set; }

    public DateTime AlteradoEm { get; set; }

    public string AlteradoPor { get; set; } = string.Empty;
}
