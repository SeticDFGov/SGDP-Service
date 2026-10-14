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

    /// <summary>Chegada à SGDI até o retorno ao órgão (análise concluída).</summary>
    public double? ChegadaParaConclusao { get; set; }

    /// <summary>Chegada à SGDI até a assinatura do contrato (processo concluído).</summary>
    public double? ChegadaParaAssinatura { get; set; }
}

/// <summary>
/// Bloco dos registros históricos (contratações anteriores ao regime) do painel interno:
/// fora das estatísticas da supervisão, que medem o regime atual.
/// </summary>
public class CtrPainelHistoricos
{
    public int Total { get; set; }

    /// <summary>Quantidade desc; empate pela chave.</summary>
    public List<CtrContagem> PorCategoria { get; set; } = new();

    /// <summary>Pela sigla do órgão; quantidade desc, empate pela chave.</summary>
    public List<CtrContagem> PorOrgao { get; set; } = new();
}

/// <summary>
/// Indicadores agregados dos processos ativos (GET api/contratacoes/painel). Desde 2026-10-13
/// as estatísticas da supervisão contam só as origens da supervisão (Órgão comunicante e TCDF);
/// os registros históricos ficam no bloco <see cref="Historicos"/>.
/// </summary>
public class CtrPainelResponse
{
    /// <summary>Ativos da supervisão (sem os registros históricos).</summary>
    public int TotalAtivos { get; set; }

    /// <summary>Corte de dias sem movimento usado nos gargalos.</summary>
    public int LimiteDias { get; set; }

    /// <summary>Ativos com pedido de esclarecimento feito e ainda sem resposta.</summary>
    public int TotalEsclarecimentoPendente { get; set; }

    /// <summary>As 11 situações, mesmo as com zero.</summary>
    public List<CtrContagem> PorSituacao { get; set; } = new();

    /// <summary>Ativos da supervisão com contrato assinado (situação "Contrato assinado").</summary>
    public int TotalConcluidos { get; set; }

    /// <summary>
    /// Relação dos de contrato assinado: os de assinatura mais recente primeiro, no máximo 50,
    /// com os mesmos dados da lista (sem os registros históricos).
    /// </summary>
    public List<CtrProcessoResponse> Concluidos { get; set; } = new();

    /// <summary>
    /// Nível MÁXIMO dos riscos da contratação por processo: Baixo, Médio, Alto, Extremo +
    /// "Sem riscos declarados", sempre presentes (quantidade desc; empate pela ordem do
    /// domínio).
    /// </summary>
    public List<CtrContagem> PorNivelRiscoDeclarado { get; set; } = new();

    public List<CtrContagem> PorCategoria { get; set; } = new();

    /// <summary>
    /// Nova contratação, Alteração de contratação vigente e "Não informado", sempre
    /// presentes (quantidade desc; empate pela ordem do domínio). Só no painel interno.
    /// </summary>
    public List<CtrContagem> PorTipoContratacao { get; set; } = new();

    public List<CtrContagem> PorOrgao { get; set; } = new();

    public CtrTemposMedios TemposMedios { get; set; } = new();

    /// <summary>
    /// Ativos da supervisão fora das situações terminais (Contrato assinado, Restituído ao
    /// órgão, Nota informativa enviada e Análise concluída) com DiasSemMovimento >= LimiteDias,
    /// do mais parado para o menos, no máximo 50.
    /// </summary>
    public List<CtrProcessoResponse> Gargalos { get; set; } = new();

    /// <summary>Registros históricos (origem "Anterior ao regime"): total, por categoria e por órgão.</summary>
    public CtrPainelHistoricos Historicos { get; set; } = new();
}
