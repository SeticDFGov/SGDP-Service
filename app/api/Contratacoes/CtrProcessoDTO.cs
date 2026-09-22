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

    /// <summary>CtrDominios.EtapaPlanejamento (DFD/ETP/TR).</summary>
    public string? EtapaPlanejamento { get; set; }

    /// <summary>CtrDominios.Criticidade (art. 11 da IN), agora atributo do processo.</summary>
    public string? Criticidade { get; set; }

    /// <summary>CtrDominios.Origem (Órgão comunicante / TCDF).</summary>
    public string? Origem { get; set; }

    /// <summary>true = só os que aguardam esclarecimento; false = só os sem pendência; null = todos.</summary>
    public bool? EsclarecimentoPendente { get; set; }

    /// <summary>
    /// Nível MÁXIMO entre os riscos da contratação (Baixo/Médio/Alto/Extremo) ou "Sem riscos
    /// declarados": predicado EF sobre os pares da matriz. Fora do domínio, lista vazia.
    /// </summary>
    public string? NivelRiscoDeclarado { get; set; }

    public string? Sigla { get; set; }

    /// <summary>true = só restituídos; false = só não restituídos; null = todos.</summary>
    public bool? Restituidos { get; set; }

    /// <summary>
    /// true traz também os Concluídos (contrato assinado), que por padrão ficam fora da lista
    /// e do export. O filtro Situacao = "Concluído" também os traz.
    /// </summary>
    public bool IncluirConcluidos { get; set; }

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

    /// <summary>
    /// Assinatura do contrato: a data final do trâmite. Preenchida, o processo fica Concluído
    /// (sai da lista e vai para a relação de concluídos do painel). Não pode ser futura.
    /// </summary>
    public DateOnly? DataAssinaturaContrato { get; set; }

    /// <summary>
    /// CtrDominios.Criticidade (art. 11 da IN). Com <see cref="CriteriosCriticidade"/> no
    /// corpo é DERIVADA das respostas e o valor enviado é ignorado. Sem respostas vale o
    /// que vier (planilha antiga, processo classificado antes da regra automática).
    /// </summary>
    [StringLength(10)]
    public string? Criticidade { get; set; }

    /// <summary>
    /// Respostas aos critérios do art. 11, § 3º, da IN: chave = código do critério
    /// (CtrDominios.CriterioCriticidade.Todos), valor = resposta (Sim/Não ou Nenhum/Baixo/
    /// Médio/Alto). Critério ausente recebe a resposta padrão. Na EDIÇÃO, nula PRESERVA as
    /// respostas gravadas (e a criticidade calculada delas).
    /// </summary>
    public Dictionary<string, string>? CriteriosCriticidade { get; set; }

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
    /// Riscos da contratação. Na CRIAÇÃO, nula (ou com a lista vazia) = processo sem
    /// riscos. Na EDIÇÃO, nula PRESERVA os gravados (ausência nunca apaga); enviada, é
    /// validada por inteiro e a lista SUBSTITUI a anterior (vazia tira todos). Importação
    /// e checkpoint não a tocam.
    /// </summary>
    public CtrClassificacaoRiscoDTO? ClassificacaoRisco { get; set; }
}

/// <summary>Edição de processo: mesma forma do create, mais o pedido de remoção dos riscos.</summary>
public class CtrProcessoUpdateDTO : CtrProcessoCreateDTO
{
    /// <summary>
    /// true REMOVE os riscos da contratação gravados (o mesmo que enviar a lista vazia).
    /// Enviar junto de ClassificacaoRisco é recusado.
    /// </summary>
    public bool LimparClassificacaoRisco { get; set; }
}

/// <summary>
/// Classificação de riscos enviada no cadastro/edição do processo (CtrClassificacaoRiscoForm
/// no front): a lista de riscos da contratação, cada um na matriz completa da CGDF. Desde
/// 2026-09-21 não há mais o questionário dos arts. 15 a 17 do PGIA; campos antigos que um
/// cliente ainda mande (Q15, Q16Nenhuma...) são ignorados pelo desserializador.
/// </summary>
public class CtrClassificacaoRiscoDTO
{
    /// <summary>Lista completa dos riscos da contratação, até 20; vazia = nenhum.</summary>
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

/// <summary>
/// Riscos da contratação gravados no processo (só nas leituras de UM processo); nula
/// quando o processo não tem nenhum.
/// </summary>
public class CtrClassificacaoRiscoResponse
{
    /// <summary>Quando a lista atual foi gravada (o risco mais recente).</summary>
    public DateTime ClassificadoEm { get; set; }

    /// <summary>E-mail de quem gravou a lista atual.</summary>
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

    /// <summary>Respostas aos critérios do art. 11, § 3º, da IN; nulas quando ainda não avaliados.</summary>
    public Dictionary<string, string>? CriteriosCriticidade { get; set; }

    /// <summary>Derivado: soma dos pontos das respostas (CtrCriticidade); nulo sem respostas.</summary>
    public int? PontosCriticidade { get; set; }

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

    /// <summary>
    /// Derivada das datas (CtrProcessoService.CalcularSituacao); nunca gravada. Concluído =
    /// contrato assinado.
    /// </summary>
    public string Situacao { get; set; } = string.Empty;

    /// <summary>Maior data entre os cinco checkpoints e a restituição.</summary>
    public DateOnly? UltimaMovimentacao { get; set; }

    public int DiasSemMovimento { get; set; }

    public int TotalManifestacoes { get; set; }

    /// <summary>Estágio da manifestação mais recente ao TCDF.</summary>
    public string? UltimoEstagioTcdf { get; set; }

    /// <summary>
    /// Maior nível entre os riscos da contratação (derivado da matriz); nulo quando não há
    /// nenhum. Vem em todas as leituras; na lista, resolvido EM LOTE.
    /// </summary>
    public string? NivelMaximoRiscoDeclarado { get; set; }

    /// <summary>
    /// Riscos da contratação completos: SÓ nas leituras e escritas de UM processo (GET {id},
    /// POST, PUT e checkpoint); nula na lista, no painel e quando o processo não tem riscos.
    /// </summary>
    public CtrClassificacaoRiscoResponse? ClassificacaoRisco { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
