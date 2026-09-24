namespace Models.Planejamento;

/// <summary>
/// Uma versão do PETIC-DF (art. 11 do Decreto nº 48.900/2026): nasce em rascunho (vazia
/// ou copiada da vigente, com os registros e os códigos), vai ao CGTIC (em deliberação)
/// e volta aprovada ou devolvida (rascunho de novo). Aprovar uma versão marca a anterior
/// como substituída: só uma vigente. Os dados de cada seção são registros (pe_registro
/// com petic_id). A versão ("1.0", "2.0") é dada pelo servidor. Tabela pe_petic;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PePetic : IPeAuditavel
{
    public long Id { get; set; }

    // "N.0", única; a primeira é "1.0"
    public string Versao { get; set; } = "1.0";

    public string Titulo { get; set; } = string.Empty;

    public DateOnly? VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    // PeDominios.SituacaoPetic; token de concorrência (duas pessoas mexendo ao mesmo tempo)
    public string Situacao { get; set; } = PeDominios.SituacaoPetic.Rascunho;

    // A versão vigente quando esta nasceu (a que ela vai substituir)
    public long? AnteriorId { get; set; }

    public PePetic? Anterior { get; set; }

    // Quando o CGTIC aprovou (fica também depois de substituída)
    public DateTime? AprovadoEm { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    // Última mudança na versão ou num registro dela
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// Envio de um PETIC-DF ou (desde a E7) de um PDTIC ao CGTIC e a decisão que a Secretaria
/// Executiva registra: aprovado (com o ato e a data) ou devolvido (com a observação).
/// Uma por envio; decidida não muda. O objeto não tem FK (pode ser PETIC ou PDTIC); no PDTIC,
/// doc_versao_id aponta o PDF enviado. Tabela pe_deliberacao; mapeamento em PeModelConfiguration.
/// </summary>
public class PeDeliberacao : IPeAuditavel
{
    public long Id { get; set; }

    // PeDominios.ObjetoDeliberacao
    public string ObjetoTipo { get; set; } = PeDominios.ObjetoDeliberacao.Petic;

    public long ObjetoId { get; set; }

    // A versão do objeto quando foi enviado ("1.0")
    public string VersaoObjeto { get; set; } = string.Empty;

    public DateTime EnviadoEm { get; set; }

    public string EnviadoPor { get; set; } = string.Empty;

    // PeDominios.SituacaoDeliberacao; token de concorrência (duas decisões ao mesmo tempo)
    public string Situacao { get; set; } = PeDominios.SituacaoDeliberacao.Aguardando;

    public DateTime? DecididoEm { get; set; }

    public string? DecididoPor { get; set; }

    // Tipo do ato (resolução, deliberação, ata...); texto livre
    public string? AtoTipo { get; set; }

    public string? AtoNumero { get; set; }

    public DateOnly? AtoData { get; set; }

    // Processo SEI (00000-00000000/0000-00)
    public string? Sei { get; set; }

    // Obrigatória na devolução: o que ajustar
    public string? Observacao { get; set; }

    // PDTIC (E7): a versão do documento enviada (o PDF que a Secretaria baixa); nulo no PETIC-DF
    public long? DocVersaoId { get; set; }

    public PeDocVersao? DocVersao { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
