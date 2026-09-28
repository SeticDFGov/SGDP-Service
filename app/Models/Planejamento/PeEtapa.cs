namespace Models.Planejamento;

/// <summary>
/// Etapa da trilha do órgão (as sete do Guia de PDTIC do SISP). Nesta entrega o
/// administrador só muda título, descrição, referência do guia e ordem: não cria nem
/// apaga etapa. Tabela pe_etapa; mapeamento em PeModelConfiguration.
/// </summary>
public class PeEtapa : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    // Estável e única ("preparacao", "diagnostico"...)
    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    // Atividades do guia ("atividades 1.1 a 1.8")
    public string? ReferenciaGuia { get; set; }

    public int Ordem { get; set; }

    // Veio do guia (carregador do modelo inicial)
    public bool Sistema { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PePasso> Passos { get; set; } = new();
}
