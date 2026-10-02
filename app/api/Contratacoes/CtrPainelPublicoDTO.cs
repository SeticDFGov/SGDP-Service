namespace api.Contratacoes;

/// <summary>
/// Painel público da Supervisão Contínua das Contratações (GET api/contratacoes/publico/painel,
/// anônimo; página /transparencia-contratacoes do front, que outros sites podem incorporar).
///
/// Só as contratações com contrato assinado dos processos de supervisão contínua, e só os
/// campos abertos: órgão, objeto, tipo (a categoria do objeto), as duas datas e o valor
/// estimado. Os filtros, os totais e a seleção são feitos no navegador, sobre esta lista.
/// </summary>
public class CtrPainelPublicoResponse
{
    /// <summary>Do contrato assinado mais recente para o mais antigo.</summary>
    public List<CtrContratacaoPublicaResponse> Contratacoes { get; set; } = new();

    /// <summary>Última alteração entre as contratações publicadas; nulo quando não há nenhuma.</summary>
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>
/// Uma contratação no painel público. Não acrescente campo interno do processo (número SEI,
/// trâmite, criticidade, riscos, manifestações, observação ou quem registrou): o
/// <c>CtrPainelPublicoTest</c> confere a lista exata de propriedades.
/// </summary>
public class CtrContratacaoPublicaResponse
{
    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    /// <summary>O "tipo da contratação" do painel: a categoria do objeto do processo.</summary>
    public string CategoriaObjeto { get; set; } = string.Empty;

    public DateOnly? ChegadaSgdi { get; set; }

    public DateOnly DataAssinaturaContrato { get; set; }

    /// <summary>Valor estimado do processo; nulo quando não foi informado.</summary>
    public decimal? ValorEstimado { get; set; }
}
