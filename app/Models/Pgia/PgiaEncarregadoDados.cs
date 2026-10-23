using app.Models;

namespace Models.Pgia;

/// <summary>
/// Designações de Encarregado de Dados / DPO (arts. 2º, X, 11 e 35, I; LGPD art. 41).
/// Comunicação à SGDI até 02/08/2026. No máximo um titular e um suplente vigentes por
/// órgão (índice único parcial por órgão e posição); o histórico fica preservado.
/// </summary>
public class PgiaEncarregadoDados : IPgiaDesignacao
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // Pessoa designada (Users do SGDP)
    public Guid AgenteId { get; set; }

    public User? Agente { get; set; }

    // Titular ou suplente (PgiaDominios.PosicaoDesignacao)
    public string Posicao { get; set; } = PgiaDominios.PosicaoDesignacao.Titular;

    // Importado da planilha da SGDI com o ato a completar (só a carga SQL liga)
    public bool AtoPendente { get; set; }

    public string? AtoTipo { get; set; }

    public string? AtoNumero { get; set; }

    public DateOnly? AtoData { get; set; }

    public string? ProcessoSeiComunicacao { get; set; }

    // Até 02/08/2026 (art. 35, I)
    public DateOnly? DataComunicacaoSgdi { get; set; }

    // Cópia do ato de designação anexada (pgia_documento); opcional
    public long? DocumentoId { get; set; }

    public DateOnly? InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
