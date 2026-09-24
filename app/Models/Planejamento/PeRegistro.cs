namespace Models.Planejamento;

/// <summary>
/// Um registro de uma seção: a linha de uma tabela ou o único registro de um formulário.
/// Os valores ficam em jsonb (chave do campo para valor), validados pelo modelo a cada
/// gravação (service.Planejamento.PeRegistroService); as ligações ficam em pe_vinculo.
/// Dono: a versão do PETIC-DF (petic_id) ou nenhum (catálogo do DF, escopo df); a E4
/// acrescenta o PDTIC. O código (N01, OE03) sai do prefixo da seção e da sequência dentro
/// do dono (pe_registro_sequencia) e nunca é reaproveitado; formulário e tabela sem
/// prefixo não têm código. Registro do sistema (os princípios do art. 4º) não se edita
/// nem se apaga. Tabela pe_registro; mapeamento em PeModelConfiguration.
/// </summary>
public class PeRegistro : IPeAuditavel
{
    public long Id { get; set; }

    public long SecaoId { get; set; }

    public PeSecao? Secao { get; set; }

    // Nulo no catálogo do DF
    public long? PeticId { get; set; }

    public PePetic? Petic { get; set; }

    // Prefixo da seção + sequência ("OE01"); nulo no formulário e na tabela sem prefixo
    public string? Codigo { get; set; }

    public int Ordem { get; set; }

    // jsonb: { "chave do campo": valor }, com os calculados já calculados
    public string Dados { get; set; } = "{}";

    public bool Sistema { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// Ligação de um registro com outro por um campo de ligação (com outra seção do mesmo
/// dono ou com um catálogo). Apagar a origem apaga a ligação; o destino ligado não pode
/// ser apagado. Tabela pe_vinculo.
/// </summary>
public class PeVinculo
{
    public long Id { get; set; }

    public long RegistroOrigemId { get; set; }

    public PeRegistro? RegistroOrigem { get; set; }

    public long CampoId { get; set; }

    public PeCampo? Campo { get; set; }

    public long RegistroDestinoId { get; set; }

    public PeRegistro? RegistroDestino { get; set; }
}

/// <summary>
/// O último número de código dado numa seção para um dono ("df", "petic:12"; a E4 usa
/// "pdtic:ID"). É o que impede reaproveitar o código de um registro apagado, e o token de
/// concorrência em Ultimo faz duas inclusões ao mesmo tempo não darem o mesmo código (a
/// segunda recebe 409 e tenta de novo). Formulário também passa por aqui: a primeira
/// gravação cria a linha, e duas primeiras gravações simultâneas não viram dois registros.
/// Tabela pe_registro_sequencia.
/// </summary>
public class PeRegistroSequencia
{
    public long SecaoId { get; set; }

    public PeSecao? Secao { get; set; }

    public string Dono { get; set; } = PeDominios.DonoRegistro.Df;

    public int Ultimo { get; set; }
}

/// <summary>
/// Arquivo enviado ao módulo (atos, atas, anexos de um campo de arquivo), com o binário no
/// próprio banco, como os anexos do PGIA. Os metadados ficam nesta classe; o binário, em
/// <see cref="PeArquivoConteudo"/>, que divide a mesma tabela (table splitting): consultar
/// os metadados nunca traz o binário junto. O dono é o registro cujo campo aponta para o
/// arquivo; sem dono, só quem enviou vê. Tabela pe_arquivo.
/// </summary>
public class PeArquivo : IPeAuditavel
{
    public long Id { get; set; }

    // Nome original saneado (sem caminho, até 200 caracteres)
    public string Nome { get; set; } = string.Empty;

    // Do mapa de extensões aceitas, nunca o que o navegador mandou
    public string TipoMime { get; set; } = string.Empty;

    // Bytes
    public long Tamanho { get; set; }

    // SHA-256 do conteúdo, em hexadecimal minúsculo
    public string Hash { get; set; } = string.Empty;

    // PeDominios.DonoArquivo; nulo = ainda sem dono
    public string? DonoTipo { get; set; }

    public long? DonoId { get; set; }

    // Enviado em e por
    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>O binário de um <see cref="PeArquivo"/> (mesma linha de pe_arquivo).</summary>
public class PeArquivoConteudo
{
    public long Id { get; set; }

    public PeArquivo? Arquivo { get; set; }

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}
