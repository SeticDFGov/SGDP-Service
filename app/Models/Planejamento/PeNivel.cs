namespace Models.Planejamento;

/// <summary>
/// Nível de maturidade (Básico, Intermediário, Avançado; o administrador cria outros).
/// O nível do órgão diz quais passos, seções e campos ele preenche. Nível em uso não é
/// apagado: é desativado, e o órgão que está nele continua nele até o administrador
/// trocar. Tabela pe_nivel; mapeamento em PeModelConfiguration.
/// </summary>
public class PeNivel : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    // Estável: não muda depois de criado (o carregador do modelo inicial acha o nível por ele)
    public string Codigo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public int Ordem { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
