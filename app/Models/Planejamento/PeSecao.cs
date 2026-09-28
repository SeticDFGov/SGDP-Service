namespace Models.Planejamento;

/// <summary>
/// Seção: bloco de dados de um passo (formulário com um registro ou tabela com vários).
/// As seções do PDTIC (escopo pdtic) pertencem a um passo e têm situação por nível; as
/// do PETIC-DF e do catálogo do DF (escopos petic e df, semeadas a partir da E3) não têm
/// passo nem nível: usam a situação geral. Seção travada (um dos nove conteúdos do
/// art. 12, § 2º) não desliga, e o campo principal dela também não. Tabela pe_secao;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PeSecao : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    // Obrigatório no escopo pdtic, nulo nos outros (CHECK)
    public long? PassoId { get; set; }

    public PePasso? Passo { get; set; }

    // PeDominios.Escopo
    public string Escopo { get; set; } = PeDominios.Escopo.Pdtic;

    // Única ("necessidades"); é a chave usada nas rotas e nos nomes das planilhas
    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    // PeDominios.TipoSecao
    public string Tipo { get; set; } = PeDominios.TipoSecao.Formulario;

    // Prefixo do código dos registros da tabela (N, M, A, R): N01, M03, A12
    public string? PrefixoCodigo { get; set; }

    public int Ordem { get; set; }

    public bool NoDocumento { get; set; } = true;

    public bool NaPlanilha { get; set; } = true;

    public bool Travada { get; set; }

    // Incisos do art. 12, § 2º, que a seção cobre; nulo fora deles
    public string? IncisoDecreto { get; set; }

    // Situação das seções fora do PDTIC (escopos petic e df); nula no escopo pdtic (CHECK)
    public string? SituacaoGeral { get; set; }

    public bool Sistema { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PeSecaoNivel> Niveis { get; set; } = new();

    public List<PeCampo> Campos { get; set; } = new();
}

/// <summary>Situação de uma seção num nível (pe_secao_nivel). Sem linha = desligado.</summary>
public class PeSecaoNivel : IPeSituacaoNivel
{
    public long SecaoId { get; set; }

    public PeSecao? Secao { get; set; }

    public long NivelId { get; set; }

    public PeNivel? Nivel { get; set; }

    public string Situacao { get; set; } = PeDominios.Situacao.Desligado;
}
