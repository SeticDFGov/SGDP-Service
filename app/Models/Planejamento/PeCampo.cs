namespace Models.Planejamento;

/// <summary>
/// Campo de uma seção. A chave é a chave do valor no jsonb dos registros (E3), por isso
/// é única na seção e não muda nos campos do sistema. O config (jsonb) depende do tipo
/// (<see cref="PeDominios.TipoCampo"/>; regras em service.Planejamento.PeConfigCampo).
/// Campo travado (o tema das ações, que carrega os incisos V, VI e IX) e o campo
/// principal de uma seção travada não desligam em nenhum nível nem em ajuste de órgão.
/// Tabela pe_campo; mapeamento em PeModelConfiguration.
/// </summary>
public class PeCampo : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    public long SecaoId { get; set; }

    public PeSecao? Secao { get; set; }

    // Única na seção; minúsculas, números e sublinhado ("tipo_abrangencia")
    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    // PeDominios.TipoCampo
    public string Tipo { get; set; } = PeDominios.TipoCampo.TextoCurto;

    // jsonb; "{}" quando o tipo não tem configuração
    public string Config { get; set; } = "{}";

    // O campo que descreve o registro (a descrição); um por seção
    public bool Principal { get; set; }

    public bool Travado { get; set; }

    public int Ordem { get; set; }

    public bool NoDocumento { get; set; } = true;

    public bool NaPlanilha { get; set; } = true;

    // PeDominios.Largura (coluna na tabela); nula = a do front
    public string? Largura { get; set; }

    // Situação dos campos das seções fora do PDTIC (a regra fica no serviço: pe_campo
    // não sabe o escopo da seção)
    public string? SituacaoGeral { get; set; }

    public bool Sistema { get; set; }

    public DateTime? ExcluidoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public List<PeCampoNivel> Niveis { get; set; } = new();

    public List<PeOpcao> Opcoes { get; set; } = new();
}

/// <summary>Situação de um campo num nível (pe_campo_nivel). Sem linha = desligado.</summary>
public class PeCampoNivel : IPeSituacaoNivel
{
    public long CampoId { get; set; }

    public PeCampo? Campo { get; set; }

    public long NivelId { get; set; }

    public PeNivel? Nivel { get; set; }

    public string Situacao { get; set; } = PeDominios.Situacao.Desligado;
}

/// <summary>
/// Opção de um campo de lista (e o resultado do nível de risco). O valor é o que fica
/// gravado nos registros: é estável e não muda. Opção do sistema só é desativada; opção
/// travada (os três temas do decreto) nem isso. Tabela pe_opcao; mapeamento em
/// PeModelConfiguration.
/// </summary>
public class PeOpcao : IPeAuditavel, IPeOrdenavel
{
    public long Id { get; set; }

    public long CampoId { get; set; }

    public PeCampo? Campo { get; set; }

    // Único no campo; minúsculas, números e sublinhado ("seguranca", "3")
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Ordem { get; set; }

    public bool Ativa { get; set; } = true;

    // PeDominios.Cor; nula = sem cor
    public string? Cor { get; set; }

    public bool Travada { get; set; }

    public bool Sistema { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
