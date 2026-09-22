namespace Models.Contratacoes;

/// <summary>
/// Processo SEI de contratação de TIC em análise pela SGDI (uma linha da planilha
/// legada "Análises de contratações"). Tabela ctr_processo.
///
/// A situação do processo é DERIVADA das datas (CtrProcessoService.CalcularSituacao)
/// e nunca é gravada aqui.
/// </summary>
public class CtrProcesso
{
    public long Id { get; set; }

    // Formato SEI 00000-00000000/AAAA-DD; único entre os ativos
    public string NumeroProcesso { get; set; } = string.Empty;

    // Órgão comunicante em texto livre: o universo aqui é maior que o dos
    // órgãos aderentes ao PGIA, então não há FK para pgia_orgao.
    public string OrgaoNome { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? ComplementoArea { get; set; }

    public string Objeto { get; set; } = string.Empty;

    // CtrDominios.CategoriaObjeto
    public string CategoriaObjeto { get; set; } = string.Empty;

    // Os cinco checkpoints do trâmite (SGDI -> SUBGD -> UGTIC -> Gab SGDI -> órgão)
    public DateOnly? ChegadaSgdi { get; set; }

    public DateOnly? ChegadaSubgd { get; set; }

    public DateOnly? ChegadaUgtic { get; set; }

    // A célula "-" da planilha: etapa pulada de propósito
    public bool UgticNaoSeAplica { get; set; }

    public DateOnly? RetornoGabSgdi { get; set; }

    public DateOnly? RetornoOrgao { get; set; }

    // Nem todo processo volta ao órgão comunicante: mesma semântica do "-" da UGTIC.
    // Só conclui o processo junto do retorno ao Gab SGDI (CalcularSituacao).
    public bool RetornoOrgaoNaoSeAplica { get; set; }

    // CtrDominios.EtapaPlanejamento — onde o planejamento está (ou parou). Continua
    // registrada depois da assinatura do contrato.
    public string? EtapaPlanejamento { get; set; }

    // Sexta e última data do trâmite (desde 2026-09-22): assinado o contrato, o processo
    // está Concluído (CalcularSituacao), sai da lista e vai para a relação de concluídos
    // do painel. Fora da cronologia dos outros checkpoints: o TCDF também analisa
    // contrato já assinado. Só não pode ser futura.
    public DateOnly? DataAssinaturaContrato { get; set; }

    // CtrDominios.Criticidade (art. 11 da IN SGDI nº 1/2026): atributo da
    // CONTRATAÇÃO, não da resposta ao TCDF — o despacho apenas a reporta. Com as
    // respostas aos critérios (abaixo) é DERIVADA delas (CtrCriticidade.Calcular) e
    // regravada a cada validação; sem respostas, é a registrada antes da regra automática.
    public string? Criticidade { get; set; }

    // Respostas aos critérios do art. 11, § 3º, da IN (CtrDominios.CriterioCriticidade),
    // em jsonb {"I":"Sim","II":"Alto",...}. Nulo = critérios ainda não avaliados.
    public string? CriteriosCriticidade { get; set; }

    // CtrDominios.Origem: nem toda contratação chega pelo órgão comunicante —
    // há as que nascem de análise do próprio TCDF sobre o contrato.
    public string Origem { get; set; } = CtrDominios.Origem.OrgaoComunicante;

    // ── Dados da contratação (pedido de 2026-09-22) ───────────────────────────
    // Vêm do formulário do processo, que manda os três sempre (no PUT, nulo limpa). O
    // checkpoint não os toca; a importação só quando o arquivo traz a coluna.

    // Em reais: numeric(18,2), nunca negativo (CtrProcessoService.ValidarProcesso)
    public decimal? ValorEstimado { get; set; }

    // CtrDominios.HospedagemCetic; nulo = não informado
    public string? HospedagemCetic { get; set; }

    // Usa a rede GDFNet; nulo = não informado
    public bool? UsaGdfnet { get; set; }

    // ── Pedido de esclarecimentos ao órgão comunicante ────────────────────────
    // Sinal PARALELO ao trâmite: não é etapa e NÃO muda a situação derivada — só
    // marca que o processo está aguardando retorno do órgão.
    public DateOnly? EsclarecimentoSolicitadoEm { get; set; }

    public string? EsclarecimentoDescricao { get; set; }

    public DateOnly? EsclarecimentoRespondidoEm { get; set; }

    // ── LEGADO: questionário dos arts. 15 a 17 do Decreto nº 48.901/2026 ─────
    // Sem uso desde 2026-09-21: a classificação de riscos do processo passou a ser só a
    // lista de riscos da contratação (RiscosDeclarados). Estas seis colunas continuam no
    // banco, sem migration (processos classificados antes guardam o questionário do PGIA
    // aqui), nada as lê e elas são zeradas juntas quando os riscos do processo são
    // alterados ou limpos. O CHECK ck_ctr_processo_classificacao (tudo ou nada) segue
    // valendo, por isso RiscoClassificadoEm/Por não servem mais para registrar quem
    // mexeu nos riscos: isso sai de CtrRiscoDeclarado.CriadoEm/CriadoPor. Remover numa
    // migration própria.

    // jsonb no formato de pgia_classificacao_risco.respostas_checklist
    public string? ChecklistRisco { get; set; }

    // PgiaDominios.ResultadoRisco
    public string? RiscoClassificado { get; set; }

    public string? EnquadramentoRisco { get; set; }

    public int? PontuacaoRisco { get; set; }

    public DateTime? RiscoClassificadoEm { get; set; }

    public string? RiscoClassificadoPor { get; set; }

    // ── Riscos da contratação ─────────────────────────────────────────────────
    // A classificação de riscos do processo: cada risco na matriz COMPLETA da CGDF.
    // Opcional (importados e os do cadastro rápido do TCDF nascem sem riscos); estado
    // atual, sem histórico; independente da Criticidade.
    public ICollection<CtrRiscoDeclarado> RiscosDeclarados { get; set; } = new List<CtrRiscoDeclarado>();

    // Restituição ao órgão: conceito de primeira classe (hoje vive na Observação)
    public bool Restituido { get; set; }

    public DateOnly? RestituidoEm { get; set; }

    public string? RestituidoMotivo { get; set; }

    public string? Observacao { get; set; }

    // Soft delete: some das listas, a linha permanece auditável
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public ICollection<CtrManifestacaoTcdf> Manifestacoes { get; set; } = new List<CtrManifestacaoTcdf>();
}
