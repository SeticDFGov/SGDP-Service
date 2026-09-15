namespace api.Contratacoes;

/// <summary>Par chave/quantidade das contagens do painel.</summary>
public class CtrContagem
{
    public string Chave { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

/// <summary>
/// Tempo médio, em dias, entre cada par de checkpoints. Só entram os processos
/// com as DUAS datas preenchidas; null quando não há amostra.
/// </summary>
public class CtrTemposMedios
{
    public double? SgdiParaSubgd { get; set; }

    public double? SubgdParaUgtic { get; set; }

    public double? UgticParaRetornoGab { get; set; }

    public double? RetornoGabParaOrgao { get; set; }

    public double? ChegadaParaConclusao { get; set; }
}

/// <summary>Indicadores agregados dos processos ativos (GET api/contratacoes/painel).</summary>
public class CtrPainelResponse
{
    public int TotalAtivos { get; set; }

    /// <summary>Corte de dias sem movimento usado nos gargalos.</summary>
    public int LimiteDias { get; set; }

    /// <summary>Ativos com pedido de esclarecimento feito e ainda sem resposta.</summary>
    public int TotalEsclarecimentoPendente { get; set; }

    /// <summary>As 7 situações, mesmo as com zero.</summary>
    public List<CtrContagem> PorSituacao { get; set; } = new();

    /// <summary>As 2 fases (CtrDominios.Fase), mesmo as com zero.</summary>
    public List<CtrContagem> PorFase { get; set; } = new();

    /// <summary>
    /// Os 4 resultados da classificação + "Não classificado", sempre presentes
    /// (quantidade desc; empate pela ordem do domínio).
    /// </summary>
    public List<CtrContagem> PorRiscoClassificado { get; set; } = new();

    /// <summary>
    /// Nível MÁXIMO declarado por processo: Baixo, Médio, Alto, Extremo + "Sem riscos
    /// declarados", sempre presentes (mesma regra de ordem).
    /// </summary>
    public List<CtrContagem> PorNivelRiscoDeclarado { get; set; } = new();

    public List<CtrContagem> PorCategoria { get; set; } = new();

    public List<CtrContagem> PorOrgao { get; set; } = new();

    public CtrTemposMedios TemposMedios { get; set; } = new();

    /// <summary>
    /// Ativos fora de Concluído/Restituído com DiasSemMovimento >= LimiteDias,
    /// do mais parado para o menos, no máximo 50.
    /// </summary>
    public List<CtrProcessoResponse> Gargalos { get; set; } = new();
}
