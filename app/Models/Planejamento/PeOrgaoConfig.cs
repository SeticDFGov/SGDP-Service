using Models.Pgia;

namespace Models.Planejamento;

/// <summary>
/// Nível de maturidade de um órgão (pgia_orgao), escolhido pelo administrador com
/// justificativa; cada troca fica no pe_modelo_historico (entidade orgao_nivel), que
/// ajuda o Relatório de Maturidade (art. 9º, X, do Decreto nº 48.899/2026). Órgão sem
/// linha usa o nível padrão (o primeiro ativo pela ordem). Tabela pe_orgao_config.
/// </summary>
public class PeOrgaoConfig
{
    // Chave e FK para pgia_orgao
    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long NivelId { get; set; }

    public PeNivel? Nivel { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    public DateTime DefinidoEm { get; set; }

    public string DefinidoPor { get; set; } = string.Empty;
}

/// <summary>
/// Ajuste de um órgão por cima do nível: muda a situação de um passo, seção ou campo só
/// para aquele órgão (item travado nunca desliga). Um por item e órgão. Tabela
/// pe_orgao_ajuste; o alvo não tem FK (pode ser passo, seção ou campo).
/// </summary>
public class PeOrgaoAjuste : IPeAuditavel
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // PeDominios.AlvoAjuste
    public string AlvoTipo { get; set; } = string.Empty;

    public long AlvoId { get; set; }

    // PeDominios.Situacao
    public string Situacao { get; set; } = string.Empty;

    public string? Justificativa { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
