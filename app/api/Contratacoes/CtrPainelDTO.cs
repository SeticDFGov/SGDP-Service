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

    /// <summary>As 7 situações, mesmo as com zero.</summary>
    public List<CtrContagem> PorSituacao { get; set; } = new();

    public List<CtrContagem> PorCategoria { get; set; } = new();

    public List<CtrContagem> PorOrgao { get; set; } = new();

    public CtrTemposMedios TemposMedios { get; set; } = new();

    /// <summary>
    /// Ativos fora de Concluído/Restituído com DiasSemMovimento >= LimiteDias,
    /// do mais parado para o menos, no máximo 50.
    /// </summary>
    public List<CtrProcessoResponse> Gargalos { get; set; } = new();
}
