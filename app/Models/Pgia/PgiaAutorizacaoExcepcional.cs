namespace Models.Pgia;

/// <summary>
/// Autorizações excepcionais (arts. 18, § 2º e 21). Tabela pgia_autorizacao_excepcional.
/// O vínculo com contrato chega na fase 4.
/// </summary>
public class PgiaAutorizacaoExcepcional
{
    public long Id { get; set; }

    // D18
    public string Tipo { get; set; } = string.Empty;

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long? PlataformaId { get; set; }

    public PgiaPlataformaIaGenerativa? Plataforma { get; set; }

    // Condicional: autorização cujo objeto é um contrato de IA (art. 21)
    public long? ContratoId { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    // Condicional: art. 18, § 2º exige avaliação prévia de riscos e garantias verificadas
    public long? AvaliacaoRiscosDocId { get; set; }

    // D34
    public string AutorizadaPor { get; set; } = string.Empty;

    // Condicional: obrigatória quando envolve treinamento (art. 21)
    public long? DeliberacaoCgticId { get; set; }

    public DateOnly DataAutorizacao { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
