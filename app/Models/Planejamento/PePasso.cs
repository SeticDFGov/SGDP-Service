namespace Models.Planejamento;

/// <summary>
/// Passo da trilha do órgão, numerado pela posição entre os passos visíveis (N.M). O
/// tipo diz qual tela o front usa (<see cref="PeDominios.TipoPasso"/>). Passo travado
/// (um dos nove conteúdos do art. 12, § 2º, do Decreto nº 48.900/2026) não fica
/// desligado em nenhum nível nem em ajuste de órgão. Passo do sistema (veio do guia ou
/// do decreto) pode ser renomeado e mudar de situação, mas não muda de tipo nem de
/// chave e não é apagado; o criado pelo administrador é apagado de forma lógica
/// (ExcluidoEm), e os dados ficam guardados. Tabela pe_passo; mapeamento em
/// PeModelConfiguration.
/// </summary>
public class PePasso : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    public long EtapaId { get; set; }

    public PeEtapa? Etapa { get; set; }

    // Única ("preparacao.abrangencia")
    public string Chave { get; set; } = string.Empty;

    // No imperativo
    public string Titulo { get; set; } = string.Empty;

    // Uma ou duas frases simples
    public string OQueFazer { get; set; } = string.Empty;

    // No formato das dicas: "art. 12, § 2º, I" (Decreto nº 48.900/2026) ou
    // "art. 7º, V, do Decreto nº 48.899/2026"
    public string? BaseLegal { get; set; }

    // Atividade do guia ("1.1", "2.8 a 2.11", "Anexo X, item 19")
    public string? ReferenciaGuia { get; set; }

    // PeDominios.TipoPasso
    public string Tipo { get; set; } = PeDominios.TipoPasso.Dados;

    // Incisos do art. 12, § 2º, que o passo cobre ("I", "V,VI,IX"); nulo fora deles
    public string? IncisoDecreto { get; set; }

    public bool Travado { get; set; }

    // O órgão pode marcar "não se aplica" (só quando o passo é opcional no nível dele; E4)
    public bool AceitaNaoSeAplica { get; set; }

    public int Ordem { get; set; }

    public bool Sistema { get; set; }

    // Exclusão lógica (só passo criado pelo administrador)
    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PePassoNivel> Niveis { get; set; } = new();

    public List<PeSecao> Secoes { get; set; } = new();
}

/// <summary>Situação de um passo num nível (pe_passo_nivel). Sem linha = desligado.</summary>
public class PePassoNivel : IPeSituacaoNivel
{
    public long PassoId { get; set; }

    public PePasso? Passo { get; set; }

    public long NivelId { get; set; }

    public PeNivel? Nivel { get; set; }

    // PeDominios.Situacao
    public string Situacao { get; set; } = PeDominios.Situacao.Desligado;
}
