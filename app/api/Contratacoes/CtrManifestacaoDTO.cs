using System.ComponentModel.DataAnnotations;
using api.Common;

namespace api.Contratacoes;

/// <summary>Filtros da lista geral de manifestações ao TCDF.</summary>
public class CtrManifestacaoFiltro : PagedRequest
{
    /// <summary>Estágio DERIVADO (CtrDominios.Estagio), traduzido em predicado EF.</summary>
    public string? Estagio { get; set; }

    /// <summary>Busca case-insensitive em número do processo, sigla e ofício.</summary>
    public string? Filtro { get; set; }
}

/// <summary>
/// Manifestação da SGDI ao TCDF, espelhando o despacho padrão (inciso I ou II).
/// Campos de bloco inativo devem chegar nulos — o service recusa, não anula.
/// </summary>
public class CtrManifestacaoCreateDTO
{
    [StringLength(60)]
    public string OficioTcdf { get; set; } = string.Empty;

    public DateOnly DataOficio { get; set; }

    /// <summary>CtrDominios.SituacaoPortfolio</summary>
    [StringLength(30)]
    public string SituacaoPortfolio { get; set; } = string.Empty;

    public DateOnly? ComunicadaDesde { get; set; }

    /// <summary>CtrDominios.Criticidade</summary>
    [StringLength(10)]
    public string? Criticidade { get; set; }

    /// <summary>CtrDominios.ResultadoAnalise</summary>
    [StringLength(30)]
    public string? ResultadoAnalise { get; set; }

    public bool RecomendouSuspensao { get; set; }

    public bool ComunicouControleInterno { get; set; }

    /// <summary>CtrDominios.DesfechoRisco</summary>
    [StringLength(25)]
    public string? DesfechoRisco { get; set; }

    public int? PrazoRegularizacaoDias { get; set; }

    public string? Observacao { get; set; }
}

/// <summary>Edição da manifestação — mesma forma do create.</summary>
public class CtrManifestacaoUpdateDTO : CtrManifestacaoCreateDTO
{
}

public class CtrManifestacaoResponse
{
    public long Id { get; set; }

    public long ProcessoId { get; set; }

    public string NumeroProcesso { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    public string OficioTcdf { get; set; } = string.Empty;

    public DateOnly DataOficio { get; set; }

    public string SituacaoPortfolio { get; set; } = string.Empty;

    public DateOnly? ComunicadaDesde { get; set; }

    public string? Criticidade { get; set; }

    public string? ResultadoAnalise { get; set; }

    public bool RecomendouSuspensao { get; set; }

    public bool ComunicouControleInterno { get; set; }

    public string? DesfechoRisco { get; set; }

    public int? PrazoRegularizacaoDias { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Derivado (CtrManifestacaoService.CalcularEstagio); nunca gravado.</summary>
    public string Estagio { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
