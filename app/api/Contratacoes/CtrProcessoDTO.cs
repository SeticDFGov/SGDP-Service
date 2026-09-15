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

    /// <summary>
    /// Um dos quatro resultados (PgiaDominios.ResultadoRisco) ou "Não classificado".
    /// Fora do domínio, lista vazia.
    /// </summary>
    public string? RiscoClassificado { get; set; }

    /// <summary>
    /// Nível MÁXIMO entre os riscos declarados (Baixo/Médio/Alto/Extremo) ou "Sem riscos
    /// declarados" — predicado EF sobre os pares da matriz. Fora do domínio, lista vazia.
    /// </summary>
    public string? NivelRiscoDeclarado { get; set; }

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

    /// <summary>
    /// Classificação de riscos (arts. 15 a 17 + riscos declarados). Na CRIAÇÃO, nula =
    /// processo não classificado. Na EDIÇÃO, nula PRESERVA a gravada (ausência nunca
    /// apaga); enviada, é validada por inteiro, recalculada e substitui a anterior,
    /// inclusive a lista de riscos declarados. Importação e checkpoint não a tocam.
    /// </summary>
    public CtrClassificacaoRiscoDTO? ClassificacaoRisco { get; set; }
}

/// <summary>Edição de processo — mesma forma do create, mais o pedido de remoção da classificação.</summary>
public class CtrProcessoUpdateDTO : CtrProcessoCreateDTO
{
    /// <summary>
    /// true REMOVE a classificação gravada (checklist, resultado, enquadramento, pontuação,
    /// auditoria e riscos declarados). Enviar junto de ClassificacaoRisco é recusado.
    /// </summary>
    public bool LimparClassificacaoRisco { get; set; }
}

/// <summary>
/// Classificação de riscos enviada no cadastro/edição do processo (CtrClassificacaoRiscoForm
/// no front): o questionário do PGIA — cada grupo com incisos marcados XOR "Nenhuma das
/// alternativas acima" — e os riscos declarados pela matriz completa da CGDF.
/// </summary>
public class CtrClassificacaoRiscoDTO
{
    /// <summary>Incisos do art. 15 (PgiaDominios.ChecklistIncisos.Art15).</summary>
    public List<string> Q15 { get; set; } = new();

    /// <summary>Incisos do art. 16.</summary>
    public List<string> Q16 { get; set; } = new();

    /// <summary>Incisos do art. 17.</summary>
    public List<string> Q17 { get; set; } = new();

    public bool Q15Nenhuma { get; set; }

    public bool Q16Nenhuma { get; set; }

    public bool Q17Nenhuma { get; set; }

    /// <summary>Obrigatório (ao menos um) quando os três grupos estão em "nenhuma". Até 20.</summary>
    [MaxLength(20)]
    public List<CtrRiscoDeclaradoDTO> RiscosDeclarados { get; set; } = new();
}

/// <summary>Risco declarado da contratação (CtrRiscoDeclaradoForm no front).</summary>
public class CtrRiscoDeclaradoDTO
{
    [StringLength(4000)]
    public string DescricaoRisco { get; set; } = string.Empty;

    [StringLength(4000)]
    public string AcaoMitigacao { get; set; } = string.Empty;

    [StringLength(200)]
    public string ResponsavelNome { get; set; } = string.Empty;

    /// <summary>Validado pela regra do PGIA e gravado com trim e em minúsculas.</summary>
    [StringLength(200)]
    public string ResponsavelEmail { get; set; } = string.Empty;

    /// <summary>Escala COMPLETA da CGDF: Improvável, Raro, Possível, Provável, Quase certo.</summary>
    [StringLength(15)]
    public string Probabilidade { get; set; } = string.Empty;

    /// <summary>Escala COMPLETA da CGDF: Desprezível, Menor, Moderada, Maior, Catastrófica.</summary>
    [StringLength(15)]
    public string Consequencia { get; set; } = string.Empty;
}

public class CtrRiscoDeclaradoResponse : CtrRiscoDeclaradoDTO
{
    public long Id { get; set; }

    /// <summary>Derivado da célula da matriz da CGDF (Baixo/Médio/Alto/Extremo); nunca gravado.</summary>
    public string Nivel { get; set; } = string.Empty;
}

/// <summary>Classificação de riscos gravada no processo (só nas leituras de UM processo).</summary>
public class CtrClassificacaoRiscoResponse
{
    public List<string> Q15 { get; set; } = new();

    public List<string> Q16 { get; set; } = new();

    public List<string> Q17 { get; set; } = new();

    public bool Q15Nenhuma { get; set; }

    public bool Q16Nenhuma { get; set; }

    public bool Q17Nenhuma { get; set; }

    /// <summary>PgiaDominios.ResultadoRisco, recalculado no servidor.</summary>
    public string RiscoClassificado { get; set; } = string.Empty;

    /// <summary>"art. 16, II"; nulo em Baixo Risco.</summary>
    public string? EnquadramentoRisco { get; set; }

    public int PontuacaoRisco { get; set; }

    public DateTime ClassificadoEm { get; set; }

    public string? ClassificadoPor { get; set; }

    public List<CtrRiscoDeclaradoResponse> RiscosDeclarados { get; set; } = new();
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

    /// <summary>Resultado da classificação de riscos; nulo = não classificado. Vem em todas as leituras.</summary>
    public string? RiscoClassificado { get; set; }

    /// <summary>
    /// Maior nível entre os riscos declarados (derivado da matriz); nulo quando não há
    /// nenhum. Vem em todas as leituras — na lista, resolvido EM LOTE.
    /// </summary>
    public string? NivelMaximoRiscoDeclarado { get; set; }

    /// <summary>
    /// Classificação completa: SÓ nas leituras e escritas de UM processo (GET {id}, POST,
    /// PUT e checkpoint); nula na lista, no painel e quando o processo não é classificado.
    /// </summary>
    public CtrClassificacaoRiscoResponse? ClassificacaoRisco { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
