namespace api.Planejamento;

// ── PETIC-DF (versões) ───────────────────────────────────────────────────────

/// <summary>Uma versão do PETIC-DF, com a última deliberação do CGTIC sobre ela (ou nulo).</summary>
public class PePeticResponse
{
    public long Id { get; set; }

    public string Versao { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public DateOnly? VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    // rascunho, em_deliberacao, aprovado (a vigente) ou substituido
    public string Situacao { get; set; } = string.Empty;

    public DateTime? AprovadoEm { get; set; }

    public long? AnteriorId { get; set; }

    public PeDeliberacaoResponse? Deliberacao { get; set; }
}

/// <summary>
/// POST api/planejamento/petic: cria o rascunho. Sem CopiarDaVigente (ou com true), copia os
/// registros, os códigos e as ligações da vigente, quando há uma. Datas em "aaaa-mm-dd"
/// (vazio = sem data).
/// </summary>
public class PePeticCriarDTO
{
    public string? Titulo { get; set; }

    public string? VigenciaInicio { get; set; }

    public string? VigenciaFim { get; set; }

    public bool? CopiarDaVigente { get; set; }
}

/// <summary>PUT api/planejamento/petic/{id} (só rascunho): campo ausente não muda; nulo limpa a data.</summary>
public class PePeticAtualizarDTO : PeCorpoParcial
{
    public string? Titulo { get; set; }

    public string? VigenciaInicio { get; set; }

    public string? VigenciaFim { get; set; }
}

// ── Deliberações do CGTIC ────────────────────────────────────────────────────

public class PeDeliberacaoResponse
{
    public long Id { get; set; }

    // petic ou pdtic (E7)
    public string ObjetoTipo { get; set; } = string.Empty;

    public long ObjetoId { get; set; }

    public string VersaoObjeto { get; set; } = string.Empty;

    // "PETIC-DF 1.0" ou "PDTIC SES 1.0"
    public string Titulo { get; set; } = string.Empty;

    // Sigla do órgão do PDTIC (nulo no PETIC-DF)
    public string? OrgaoSigla { get; set; }

    // A mais que o contrato: o nome do órgão do PDTIC (nulo no PETIC-DF)
    public string? OrgaoNome { get; set; }

    // O PDF enviado (PDTIC): baixa em GET pdtic/{PdticId}/documento/versoes/{Numero}/arquivo; nulo no PETIC-DF
    public PeDeliberacaoDocumentoResponse? Documento { get; set; }

    public DateTime EnviadoEm { get; set; }

    public string EnviadoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string EnviadoPorNome { get; set; } = string.Empty;

    // aguardando, aprovado ou devolvido
    public string Situacao { get; set; } = string.Empty;

    public DateTime? DecididoEm { get; set; }

    public string? DecididoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o DecididoPor
    public string? DecididoPorNome { get; set; }

    public string? AtoTipo { get; set; }

    public string? AtoNumero { get; set; }

    public DateOnly? AtoData { get; set; }

    public string? Sei { get; set; }

    public string? Observacao { get; set; }

    // F1 (C40): a deliberação nasceu do registro de um PDTIC aprovado fora do sistema (não houve
    // envio: EnviadoEm é o dia do registro, e DecididoEm, o dia do ato do CGTIC, ao meio-dia)
    public bool RegistradaForaDoSistema { get; set; }
}

/// <summary>
/// Uma pendência do envio do PETIC-DF ao CGTIC (F1, achado A06 da revisão final): a seção e o que
/// falta nela. Na vigência (que não é seção), SecaoChave vem nula e SecaoTitulo "Título e vigência".
/// </summary>
public class PePeticPendenciaResponse
{
    public string? SecaoChave { get; set; }

    public string SecaoTitulo { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// A prévia do envio do PETIC-DF (GET petic/{id}/envio, F1): se a versão pode ir ao CGTIC agora,
/// o que falta (a mesma lista do 400 do POST petic/{id}/enviar) e, quando não pode, o porquê.
/// </summary>
public class PePeticEnvioResponse
{
    public bool PodeEnviar { get; set; }

    public List<PePeticPendenciaResponse> Pendencias { get; set; } = new();

    public string? Motivo { get; set; }
}

/// <summary>A versão do documento enviada ao CGTIC com a deliberação do PDTIC (E7).</summary>
public class PeDeliberacaoDocumentoResponse
{
    public long PdticId { get; set; }

    public int Numero { get; set; }
}

/// <summary>
/// Filtros da fila de deliberações (GET deliberacoes). O parâmetro da action se chama
/// "consulta" (armadilha do model binding). Situação ou objeto fora do domínio devolve
/// lista vazia.
/// </summary>
public class PeDeliberacoesConsulta
{
    public string? Situacao { get; set; }

    public string? ObjetoTipo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// POST deliberacoes/{id}/decidir: "aprovado" exige AtoNumero e AtoData; "devolvido" exige
/// Observacao. Data em "aaaa-mm-dd"; SEI no formato 00000-00000000/0000-00.
/// </summary>
public class PeDecidirDTO
{
    public string? Decisao { get; set; }

    public string? AtoTipo { get; set; }

    public string? AtoNumero { get; set; }

    public string? AtoData { get; set; }

    public string? Sei { get; set; }

    public string? Observacao { get; set; }
}
