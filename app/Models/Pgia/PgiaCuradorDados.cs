using app.Models;

namespace Models.Pgia;

/// <summary>
/// Designações de Curador de Dados de Negócio do órgão, no mesmo desenho do
/// Encarregado de Dados: posição (titular ou suplente), ato, processo SEI da
/// comunicação à SGDI, cópia do ato (pgia_documento, FK RESTRICT), vigência e
/// histórico preservado. No máximo um titular e um suplente vigentes por órgão.
/// </summary>
public class PgiaCuradorDados : IPgiaDesignacao
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

    public DateOnly? DataComunicacaoSgdi { get; set; }

    // Cópia do ato de designação ("Ato de designação" em pgia_documento); opcional
    public long? DocumentoId { get; set; }

    public DateOnly? InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
