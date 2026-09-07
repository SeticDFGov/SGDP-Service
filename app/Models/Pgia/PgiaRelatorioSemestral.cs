namespace Models.Pgia;

/// <summary>
/// Relatório semestral do órgão à SGDI, pelo SEI (art. 32). Os incisos I a V são
/// agregados das demais tabelas no momento da consulta, não persistidos aqui.
/// Tabela pgia_relatorio_semestral.
/// </summary>
public class PgiaRelatorioSemestral
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public short Ano { get; set; }

    // 1 ou 2
    public short Semestre { get; set; }

    public DateOnly? DataEnvio { get; set; }

    public string? ProcessoSei { get; set; }

    public long? DocumentoId { get; set; }

    // D25 (art. 32, § único)
    public string Status { get; set; } = PgiaDominios.StatusRelatorioSemestral.Pendente;

    // Inadimplência no painel analítico da SGDI (art. 32, § único)
    public DateOnly? RegistradoPainelEm { get; set; }

    public DateOnly? ComunicadoControleInternoEm { get; set; }

    /// <summary>
    /// Adaptação registrada: o decreto remete o prazo a norma complementar; adotamos
    /// o último dia do mês seguinte ao fim do semestre (S1 → 31/07; S2 → 31/01 do ano seguinte).
    /// </summary>
    public DateOnly PrazoEnvio { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
