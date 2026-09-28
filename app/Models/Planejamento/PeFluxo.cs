namespace Models.Planejamento;

/// <summary>
/// Fluxo do guia como modelo (E6): as figuras 4 a 22 do Guia de PDTIC do SISP, semeadas pelo
/// carregador (versão 4) e editadas pelo administrador do módulo. A definição (jsonb) tem as
/// raias, os elementos e as ligações (contrato em api.Planejamento.PeFluxoDefinicao); o
/// desenho sai dela sozinho (service.Planejamento.PeFluxoDesenho). A chave é única e estável
/// ("preparacao"): o bloco de fluxo do documento aponta para ela. Tabela pe_fluxo_modelo;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PeFluxoModelo : IPeAuditavel
{
    public long Id { get; set; }

    // Única ("preparacao", "planejamento_acompanhamento")
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    // "Figura 6"; nulo no fluxo que não vem do guia
    public string? FiguraGuia { get; set; }

    public int Ordem { get; set; }

    // jsonb: { "PrefixoNumeracao", "Raias", "Elementos", "Ligacoes" }, já conferida e numerada
    public string Definicao { get; set; } = "{}";

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// A cópia de um fluxo do guia que o órgão adaptou no PDTIC dele (passo da metodologia). Só
/// existe quando o órgão muda alguma coisa: sem ela, vale o modelo na hora (decisão 14). O
/// modelo_hash é o do modelo (nome e definição) quando o órgão gravou: se o modelo mudar
/// depois, a tela mostra "o modelo mudou". Uma por PDTIC e modelo. Tabela pe_fluxo.
/// </summary>
public class PeFluxo : IPeAuditavel
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public long ModeloId { get; set; }

    public PeFluxoModelo? Modelo { get; set; }

    public string Nome { get; set; } = string.Empty;

    // jsonb, na mesma forma do modelo
    public string Definicao { get; set; } = "{}";

    // SHA-256 do modelo (nome e definição, JSON canônico) quando o órgão gravou
    public string ModeloHash { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
