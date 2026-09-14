using System.ComponentModel.DataAnnotations;
using api.Common;

namespace api.Contratacoes;

/// <summary>
/// Filtros da listagem de processos (query string). Herda a paginação comum do SGDP;
/// os limites de Page/PageSize são saneados no service (PagedRequest é compartilhado).
/// </summary>
public class CtrProcessoFiltro : PagedRequest
{
    /// <summary>Busca case-insensitive em número, órgão, sigla e objeto.</summary>
    public string? Filtro { get; set; }

    public string? Categoria { get; set; }

    /// <summary>Situação DERIVADA (CtrDominios.Situacao), traduzida em predicado EF.</summary>
    public string? Situacao { get; set; }

    /// <summary>Fase DERIVADA (CtrDominios.Fase), traduzida em predicado sobre a assinatura.</summary>
    public string? Fase { get; set; }

    /// <summary>CtrDominios.EtapaPlanejamento (DFD/ETP/TR).</summary>
    public string? EtapaPlanejamento { get; set; }

    /// <summary>CtrDominios.Criticidade (art. 11 da IN), agora atributo do processo.</summary>
    public string? Criticidade { get; set; }

    /// <summary>CtrDominios.Origem (Órgão comunicante / TCDF).</summary>
    public string? Origem { get; set; }

    /// <summary>true = só os que aguardam esclarecimento; false = só os sem pendência; null = todos.</summary>
    public bool? EsclarecimentoPendente { get; set; }

    public string? Sigla { get; set; }

    /// <summary>true = só restituídos; false = só não restituídos; null = todos.</summary>
    public bool? Restituidos { get; set; }

    /// <summary>Janela sobre ChegadaSgdi.</summary>
    public DateOnly? De { get; set; }

    public DateOnly? Ate { get; set; }
}

/// <summary>Criação de processo. Mesma forma do update (CtrProcessoForm no front).</summary>
public class CtrProcessoCreateDTO
{
    [StringLength(25)]
    public string NumeroProcesso { get; set; } = string.Empty;

    [StringLength(200)]
    public string OrgaoNome { get; set; } = string.Empty;

    [StringLength(20)]
    public string OrgaoSigla { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ComplementoArea { get; set; }

    public string Objeto { get; set; } = string.Empty;

    [StringLength(60)]
    public string CategoriaObjeto { get; set; } = string.Empty;

    public DateOnly? ChegadaSgdi { get; set; }

    public DateOnly? ChegadaSubgd { get; set; }

    public DateOnly? ChegadaUgtic { get; set; }

    public bool UgticNaoSeAplica { get; set; }

    public DateOnly? RetornoGabSgdi { get; set; }

    public DateOnly? RetornoOrgao { get; set; }

    /// <summary>O processo não é devolvido ao órgão comunicante (etapa pulada).</summary>
    public bool RetornoOrgaoNaoSeAplica { get; set; }

    /// <summary>CtrDominios.EtapaPlanejamento (DFD/ETP/TR); nula = não informada.</summary>
    [StringLength(10)]
    public string? EtapaPlanejamento { get; set; }

    /// <summary>Assinatura do contrato: leva a contratação para a fase de execução.</summary>
    public DateOnly? DataAssinaturaContrato { get; set; }

    /// <summary>CtrDominios.Criticidade (art. 11 da IN).</summary>
    [StringLength(10)]
    public string? Criticidade { get; set; }

    /// <summary>
    /// CtrDominios.Origem; vazia = "Órgão comunicante" na CRIAÇÃO. Na edição, vazia
    /// PRESERVA a origem gravada (o front antigo não manda o campo).
    /// </summary>
    [StringLength(20)]
    public string? Origem { get; set; }

    /// <summary>Data do pedido de esclarecimentos ao órgão comunicante.</summary>
    public DateOnly? EsclarecimentoSolicitadoEm { get; set; }

    /// <summary>O que foi pedido. Obrigatória quando há data do pedido.</summary>
    [StringLength(4000)]
    public string? EsclarecimentoDescricao { get; set; }

    /// <summary>Data da resposta do órgão; enquanto nula, o processo fica sinalizado.</summary>
    public DateOnly? EsclarecimentoRespondidoEm { get; set; }

    public bool Restituido { get; set; }

    public DateOnly? RestituidoEm { get; set; }

    public string? RestituidoMotivo { get; set; }

    public string? Observacao { get; set; }
}

/// <summary>Edição de processo — mesma forma do create.</summary>
public class CtrProcessoUpdateDTO : CtrProcessoCreateDTO
{
}

/// <summary>
/// Edição rápida de um checkpoint (registrar a chegada em cada instância).
/// Data nula limpa o checkpoint; NaoSeAplica só vale para ChegadaUgtic e RetornoOrgao.
/// </summary>
public class CtrCheckpointDTO
{
    /// <summary>CtrDominios.Etapa</summary>
    [StringLength(30)]
    public string Etapa { get; set; } = string.Empty;

    public DateOnly? Data { get; set; }

    public bool? NaoSeAplica { get; set; }
}

public class CtrProcessoResponse
{
    public long Id { get; set; }

    public string NumeroProcesso { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? ComplementoArea { get; set; }

    public string Objeto { get; set; } = string.Empty;

    public string CategoriaObjeto { get; set; } = string.Empty;

    public DateOnly? ChegadaSgdi { get; set; }

    public DateOnly? ChegadaSubgd { get; set; }

    public DateOnly? ChegadaUgtic { get; set; }

    public bool UgticNaoSeAplica { get; set; }

    public DateOnly? RetornoGabSgdi { get; set; }

    public DateOnly? RetornoOrgao { get; set; }

    public bool RetornoOrgaoNaoSeAplica { get; set; }

    public string? EtapaPlanejamento { get; set; }

    public DateOnly? DataAssinaturaContrato { get; set; }

    public string? Criticidade { get; set; }

    public string Origem { get; set; } = string.Empty;

    public DateOnly? EsclarecimentoSolicitadoEm { get; set; }

    public string? EsclarecimentoDescricao { get; set; }

    public DateOnly? EsclarecimentoRespondidoEm { get; set; }

    /// <summary>Derivado: pedido feito e ainda sem resposta. Nunca gravado.</summary>
    public bool EsclarecimentoPendente { get; set; }

    /// <summary>Derivado: dias entre o pedido e hoje; nulo quando não há pendência.</summary>
    public int? DiasEsclarecimentoPendente { get; set; }

    public bool Restituido { get; set; }

    public DateOnly? RestituidoEm { get; set; }

    public string? RestituidoMotivo { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Derivada das datas (CtrProcessoService.CalcularSituacao); nunca gravada.</summary>
    public string Situacao { get; set; } = string.Empty;

    /// <summary>Derivada da assinatura (CtrProcessoService.CalcularFase); nunca gravada.</summary>
    public string Fase { get; set; } = string.Empty;

    /// <summary>Maior data entre os cinco checkpoints e a restituição.</summary>
    public DateOnly? UltimaMovimentacao { get; set; }

    public int DiasSemMovimento { get; set; }

    public int TotalManifestacoes { get; set; }

    /// <summary>Estágio da manifestação mais recente ao TCDF.</summary>
    public string? UltimoEstagioTcdf { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
