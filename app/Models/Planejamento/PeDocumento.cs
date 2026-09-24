namespace Models.Planejamento;

/// <summary>
/// Modelo de documento da SGDI (E5): o documento do PDTIC (tipo pdtic; a E7 acrescenta os
/// relatórios de acompanhamento e de resultados). Um modelo ativo por tipo (índice único
/// parcial). O administrador do módulo edita os capítulos e os blocos; o órgão recebe a cópia
/// (pe_doc_orgao e pe_doc_orgao_bloco), que só existe no que ele mudou. Tabela pe_doc_modelo;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PeDocModelo : IPeAuditavel
{
    public long Id { get; set; }

    // PeDominios.TipoDocumento
    public string Tipo { get; set; } = PeDominios.TipoDocumento.Pdtic;

    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PeDocCapitulo> Capitulos { get; set; } = new();
}

/// <summary>
/// Capítulo do modelo (um nível de subcapítulo, por pai_id). A chave é única no modelo. Os
/// nove conteúdos do art. 12, § 2º, do Decreto nº 48.900/2026 são travados: o órgão não os
/// esconde e o administrador não os apaga nem os deixa opcionais. Capítulo com passo_chave só
/// aparece para o órgão quando aquele passo está na trilha dele (nível e ajustes). Capítulo do
/// sistema (veio do carregador) não é apagado; o criado pelo administrador é apagado de forma
/// lógica, e a cópia do órgão fica guardada. Tabela pe_doc_capitulo.
/// </summary>
public class PeDocCapitulo : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    public long ModeloId { get; set; }

    public PeDocModelo? Modelo { get; set; }

    // Nulo no capítulo; o id do capítulo no subcapítulo
    public long? PaiId { get; set; }

    public PeDocCapitulo? Pai { get; set; }

    // Única no modelo ("diagnostico", "capa")
    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    // Numerado pela posição entre os visíveis (6, 6.1); a capa, o sumário e os anexos não são
    public bool Numerado { get; set; } = true;

    public int Ordem { get; set; }

    // O órgão não esconde
    public bool Obrigatorio { get; set; }

    // Um dos nove conteúdos do art. 12, § 2º (sempre obrigatório)
    public bool Travado { get; set; }

    // "I" a "IX"; nulo fora dos nove
    public string? IncisoDecreto { get; set; }

    // Chave do passo que alimenta o capítulo; passo fora da trilha do órgão esconde o capítulo
    public string? PassoChave { get; set; }

    public bool Sistema { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PeDocBloco> Blocos { get; set; } = new();
}

/// <summary>
/// Bloco de um capítulo: texto rico (o texto padrão em JSON do TipTap, que o órgão pode
/// editar), tabela de uma seção, lista das ações de um tema, matriz SWOT, fluxo ou quebra de
/// página. O config (jsonb) depende do tipo (service.Planejamento.PeDocConfig). Tabela
/// pe_doc_bloco.
/// </summary>
public class PeDocBloco : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    public long CapituloId { get; set; }

    public PeDocCapitulo? Capitulo { get; set; }

    public int Ordem { get; set; }

    // PeDominios.TipoBloco
    public string Tipo { get; set; } = PeDominios.TipoBloco.Texto;

    // jsonb: { "Texto": {TipTap} }, { "Secao", "Colunas", "Filtro" }, { "Tema" }, { "Fluxo" }; em todos, "PaginaDeitada"
    public string Config { get; set; } = "{}";

    public bool Sistema { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// O que o órgão mudou num capítulo do PDTIC dele: esconder (só capítulo opcional) e o título
/// próprio. Uma linha por PDTIC e capítulo. Tabela pe_doc_orgao.
/// </summary>
public class PeDocOrgao : IPeAuditavel
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public long CapituloId { get; set; }

    public PeDocCapitulo? Capitulo { get; set; }

    public bool Oculto { get; set; }

    // Nulo = o título do modelo
    public string? TituloProprio { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// Texto que o órgão escreveu num bloco de texto (JSON do TipTap, já conferido). Só existe
/// quando o órgão edita: sem esta linha, o bloco segue o texto do modelo na hora (decisão 14).
/// O modelo_hash é o do texto do modelo quando o órgão gravou: se o modelo mudar depois, a
/// prévia mostra "o modelo mudou". Tabela pe_doc_orgao_bloco.
/// </summary>
public class PeDocOrgaoBloco
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public long BlocoId { get; set; }

    public PeDocBloco? Bloco { get; set; }

    // jsonb
    public string Texto { get; set; } = "{}";

    // SHA-256 do texto do modelo (JSON canônico) quando o órgão gravou
    public string ModeloHash { get; set; } = string.Empty;

    public DateTime EditadoEm { get; set; }

    public string EditadoPor { get; set; } = string.Empty;
}

/// <summary>
/// Uma versão gerada do documento em PDF: número sequencial no PDTIC, situação (minuta na
/// E5; enviada, aprovada e publicada na E7), o arquivo (pe_arquivo, dono o PDTIC), o hash, as
/// páginas, quando e por quem. Tabela pe_doc_versao.
/// </summary>
public class PeDocVersao
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public int Numero { get; set; }

    // PeDominios.SituacaoVersaoDoc
    public string Situacao { get; set; } = PeDominios.SituacaoVersaoDoc.Minuta;

    public long ArquivoId { get; set; }

    public PeArquivo? Arquivo { get; set; }

    // SHA-256 do PDF
    public string Hash { get; set; } = string.Empty;

    public int Paginas { get; set; }

    public DateTime GeradoEm { get; set; }

    public string GeradoPor { get; set; } = string.Empty;
}
