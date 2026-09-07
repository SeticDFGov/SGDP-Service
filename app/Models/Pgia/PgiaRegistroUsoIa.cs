using app.Models;

namespace Models.Pgia;

/// <summary>
/// Registro do uso de IA em produtos, documentos e atividades (art. 13, IV e V).
/// Tabela pgia_registro_uso_ia; exatamente uma fonte por registro (sistema OU plataforma).
/// </summary>
public class PgiaRegistroUsoIa
{
    public long Id { get; set; }

    public Guid AgenteId { get; set; }

    public User? Agente { get; set; }

    /// <summary>
    /// Adaptação: o schema resolve o órgão pelo agente_publico. Aqui a coluna é
    /// persistida para o escopo por órgão e para os relatórios (arts. 32 e 33)
    /// não dependerem da lotação atual do agente.
    /// </summary>
    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long? SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    public long? PlataformaId { get; set; }

    public PgiaPlataformaIaGenerativa? Plataforma { get; set; }

    // Registro explícito que assegura rastreabilidade e supervisão (art. 13, IV)
    public string ProdutoRef { get; set; } = string.Empty;

    public string? ProcessoSei { get; set; }

    public DateOnly DataUso { get; set; }

    // Declaração de revisão crítica antes de divulgar, decidir ou usar (art. 13, V)
    public bool RevisaoHumanaConfirmada { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
