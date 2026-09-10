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
/// Data nula limpa o checkpoint; NaoSeAplica só vale para ChegadaUgtic.
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

    public bool Restituido { get; set; }

    public DateOnly? RestituidoEm { get; set; }

    public string? RestituidoMotivo { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Derivada das datas (CtrProcessoService.CalcularSituacao); nunca gravada.</summary>
    public string Situacao { get; set; } = string.Empty;

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
