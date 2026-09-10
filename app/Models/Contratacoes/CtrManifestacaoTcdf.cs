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

    // CtrDominios.SituacaoPortfolio — inciso I ou II do despacho
    public string SituacaoPortfolio { get; set; } = string.Empty;

    // ── Inciso I (comunicada previamente) ─────────────────────────────────────
    public DateOnly? ComunicadaDesde { get; set; }

    // CtrDominios.Criticidade (art. 11 da IN)
    public string? Criticidade { get; set; }

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
