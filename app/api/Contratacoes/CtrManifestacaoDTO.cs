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

    /// <summary>
    /// O que o TCDF apontou nesta comunicação. Vale em qualquer inciso e NÃO entra
    /// no corpo do despacho (o template oficial não tem esse campo).
    /// </summary>
    [StringLength(4000)]
    public string? PendenciasTcdf { get; set; }

    /// <summary>
    /// CtrDominios.StatusTcdf (opcional, vale nos dois incisos). Registro de
    /// acompanhamento: não altera o texto do despacho.
    /// </summary>
    [StringLength(30)]
    public string? StatusTcdf { get; set; }

    public DateOnly? ComunicadaDesde { get; set; }

    // A criticidade NÃO entra aqui: é do processo (CtrProcessoCreateDTO.Criticidade)

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

    public string? PendenciasTcdf { get; set; }

    public string? StatusTcdf { get; set; }

    public DateOnly? ComunicadaDesde { get; set; }

    /// <summary>
    /// Espelho SOMENTE LEITURA da criticidade do processo (art. 11 da IN): o front
    /// mostra e o PDF imprime, mas o dado nasce e é editado no cadastro do processo.
    /// </summary>
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
