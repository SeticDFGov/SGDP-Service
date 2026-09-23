namespace Models.Contratacoes;

/// <summary>
/// Manifestação da SGDI a uma comunicação do TCDF sobre a contratação, no regime de
/// supervisão contínua (Decreto nº 48.899/2026 e IN SGDI nº 1/2026). N por processo:
/// ofício novo do TCDF = manifestação nova; a evolução do desfecho é edição da mesma.
/// Tabela ctr_manifestacao_tcdf.
///
/// O estágio é DERIVADO (CtrManifestacaoService.CalcularEstagio) e nunca gravado.
/// </summary>
public class CtrManifestacaoTcdf
{
    public long Id { get; set; }

    public long ProcessoId { get; set; }

    public CtrProcesso? Processo { get; set; }

    // "Ofício/Comunicação TCDF nº ..."
    public string OficioTcdf { get; set; } = string.Empty;

    public DateOnly DataOficio { get; set; }

    // ── Comunicação do TCDF (pedido de 2026-09-23) ────────────────────────────
    // Os quatro andam juntos (ck_ctr_manifestacao_comunicacao): obrigatórios na
    // manifestação nova e todos nulos nas registradas antes deles.

    // Processo SEI em que o TCDF comunicou a SGDI; é nele que o despacho é juntado,
    // por isso o PDF o traz como "Processo SEI nº" (sem ele, o número do processo)
    public string? ProcessoComunicacaoTcdf { get; set; }

    // Dia em que esse processo chegou à SGDI (a data do ofício continua: o despacho a cita)
    public DateOnly? DataRecebimento { get; set; }

    // CtrDominios.AtoTcdf (Despacho Singular ou Decisão) e o número do ato
    public string? AtoTcdf { get; set; }

    public string? NumeroAtoTcdf { get; set; }

    // CtrDominios.SituacaoPortfolio — inciso I ou II do despacho
    public string SituacaoPortfolio { get; set; } = string.Empty;

    // Esclarecimentos adicionais sobre aquela comunicação (antes "Pendências
    // identificadas pelo TCDF"; coluna renomeada com os dados preservados). Vale em
    // QUALQUER inciso e com qualquer resultado (sem CHECK condicional) e NÃO entra
    // no corpo do despacho.
    public string? EsclarecimentosAdicionais { get; set; }

    // CtrDominios.StatusTcdf — fato do Tribunal (suspensão, revogação do edital),
    // válido em QUALQUER inciso. Tem precedência no estágio derivado e NÃO entra
    // no corpo do despacho (o template oficial do TCDF não tem esse campo).
    public string? StatusTcdf { get; set; }

    // ── Inciso I (comunicada previamente) ─────────────────────────────────────
    public DateOnly? ComunicadaDesde { get; set; }

    // A criticidade (art. 11 da IN) é do PROCESSO (ctr_processo.criticidade):
    // o despacho apenas a reporta. Aqui não há coluna.

    // CtrDominios.ResultadoAnalise
    public string? ResultadoAnalise { get; set; }

    // Ações acumuláveis do bloco "tendo a SGDI:" (só com Riscos significativos)
    public bool RecomendouSuspensao { get; set; }

    public bool ComunicouControleInterno { get; set; }

    // CtrDominios.DesfechoRisco — desfecho exclusivo do bloco de riscos
    public string? DesfechoRisco { get; set; }

    // ── Inciso II (não comunicada previamente) ────────────────────────────────
    // Prazo da notificação para regularizar a comunicação obrigatória (art. 40 da IN)
    public int? PrazoRegularizacaoDias { get; set; }

    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
