namespace api.Contratacoes;

/// <summary>
/// Painel público da Supervisão Contínua das Contratações (GET api/contratacoes/publico/painel,
/// anônimo; página /transparencia-contratacoes do front, que outros sites podem incorporar).
///
/// Todos os processos ativos de supervisão contínua (em análise e com contrato assinado; as
/// comunicações do TCDF ficam de fora), e só os campos abertos: órgão, objeto, tipo (a categoria
/// do objeto) e as duas datas. O valor estimado NÃO sai (pedido de 2026-10-02: o sigiloso é o
/// valor, não a contratação). Os filtros, as contagens, o gráfico e a seleção são feitos no
/// navegador, sobre esta lista.
/// </summary>
public class CtrPainelPublicoResponse
{
    /// <summary>Da chegada à SGDI mais recente para a mais antiga (sem a data, no fim).</summary>
    public List<CtrContratacaoPublicaResponse> Contratacoes { get; set; } = new();

    /// <summary>Última alteração entre as contratações publicadas; nulo quando não há nenhuma.</summary>
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>
/// Uma contratação no painel público. Não acrescente campo interno do processo (valor,
/// número SEI, trâmite, criticidade, riscos, manifestações, observação ou quem registrou):
/// o <c>CtrPainelPublicoTest</c> confere a lista exata de propriedades.
/// </summary>
public class CtrContratacaoPublicaResponse
{
    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Objeto { get; set; } = string.Empty;

    /// <summary>O "tipo da contratação" do painel: a categoria do objeto do processo.</summary>
    public string CategoriaObjeto { get; set; } = string.Empty;

    public DateOnly? ChegadaSgdi { get; set; }

    /// <summary>Nulo enquanto o contrato não foi assinado (processo em análise).</summary>
    public DateOnly? DataAssinaturaContrato { get; set; }
}
