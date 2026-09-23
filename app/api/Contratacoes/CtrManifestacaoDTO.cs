using System.ComponentModel.DataAnnotations;
using api.Common;

namespace api.Contratacoes;

/// <summary>Filtros da lista geral de manifestações ao TCDF.</summary>
public class CtrManifestacaoFiltro : PagedRequest
{
    /// <summary>Estágio DERIVADO (CtrDominios.Estagio), traduzido em predicado EF.</summary>
    public string? Estagio { get; set; }

    /// <summary>
    /// Busca case-insensitive em número do processo, processo de comunicação do TCDF,
    /// sigla e ofício.
    /// </summary>
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

    /// <summary>
    /// Nº do processo SEI de comunicação do TCDF à SGDI (formato SEI). Os quatro dados da
    /// comunicação (este, <see cref="DataRecebimento"/>, <see cref="AtoTcdf"/> e
    /// <see cref="NumeroAtoTcdf"/>) são obrigatórios na criação; na edição de manifestação
    /// registrada antes deles, vêm os quatro ou nenhum.
    /// </summary>
    [StringLength(25)]
    public string? ProcessoComunicacaoTcdf { get; set; }

    /// <summary>Dia em que o processo de comunicação chegou à SGDI (não antes do ofício).</summary>
    public DateOnly? DataRecebimento { get; set; }

    /// <summary>CtrDominios.AtoTcdf: Despacho Singular ou Decisão.</summary>
    [StringLength(20)]
    public string? AtoTcdf { get; set; }

    /// <summary>Número do despacho singular ou da decisão (ex.: 1234/2026).</summary>
    [StringLength(60)]
    public string? NumeroAtoTcdf { get; set; }

    /// <summary>CtrDominios.SituacaoPortfolio</summary>
    [StringLength(30)]
    public string SituacaoPortfolio { get; set; } = string.Empty;

    /// <summary>
    /// Esclarecimentos Adicionais (antes "Pendências identificadas pelo TCDF"). Registro
    /// de acompanhamento: vale em qualquer inciso e NÃO entra no corpo do despacho (o
    /// template oficial não tem esse campo). Vazio vira nulo.
    /// </summary>
    [StringLength(4000)]
    public string? EsclarecimentosAdicionais { get; set; }

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

/// <summary>
/// Comunicação nova do TCDF (POST api/contratacoes/manifestacao): a manifestação e o
/// processo da contratação numa gravação só. Vem exatamente um dos dois: o processo já
/// cadastrado (<see cref="ProcessoId"/>, obrigatório no inciso I, que reporta a
/// criticidade dele) ou os dados de uma contratação que o módulo ainda não tem
/// (<see cref="Contratacao"/>), que viram um processo de origem TCDF com o número do
/// processo de comunicação.
/// </summary>
public class CtrComunicacaoTcdfCreateDTO : CtrManifestacaoCreateDTO
{
    public long? ProcessoId { get; set; }

    public CtrContratacaoTcdfDTO? Contratacao { get; set; }
}

/// <summary>A contratação auditada, como o TCDF a comunicou (sem os critérios de criticidade).</summary>
public class CtrContratacaoTcdfDTO
{
    [StringLength(200)]
    public string OrgaoNome { get; set; } = string.Empty;

    [StringLength(20)]
    public string OrgaoSigla { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    /// <summary>CtrDominios.CategoriaObjeto</summary>
    [StringLength(60)]
    public string CategoriaObjeto { get; set; } = string.Empty;

    /// <summary>Em reais; nulo = não informado.</summary>
    public decimal? ValorEstimado { get; set; }
}

public class CtrManifestacaoResponse
{
    public long Id { get; set; }

    public long ProcessoId { get; set; }

    public string NumeroProcesso { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    /// <summary>Espelhos do processo, para o resumo da contratação na tela.</summary>
    public string CategoriaObjeto { get; set; } = string.Empty;

    public decimal? ValorEstimado { get; set; }

    public string Origem { get; set; } = string.Empty;

    public string OficioTcdf { get; set; } = string.Empty;

    public DateOnly DataOficio { get; set; }

    /// <summary>Nulo (os quatro) nas manifestações registradas antes destes campos.</summary>
    public string? ProcessoComunicacaoTcdf { get; set; }

    public DateOnly? DataRecebimento { get; set; }

    public string? AtoTcdf { get; set; }

    public string? NumeroAtoTcdf { get; set; }

    public string SituacaoPortfolio { get; set; } = string.Empty;

    public string? EsclarecimentosAdicionais { get; set; }

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
