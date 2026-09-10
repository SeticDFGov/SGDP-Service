namespace api.Contratacoes;

/// <summary>Ações possíveis de uma linha da planilha importada.</summary>
public static class CtrImportacaoAcao
{
    public const string Criar = "Criar";
    public const string Atualizar = "Atualizar";
    public const string Rejeitar = "Rejeitar";
}

/// <summary>
/// Uma linha da planilha, como o parser a interpretou. Linha = número físico da
/// linha no arquivo (o que o usuário vê no Excel).
/// </summary>
public class CtrImportacaoLinha
{
    public int Linha { get; set; }

    public string NumeroProcesso { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string CategoriaObjeto { get; set; } = string.Empty;

    /// <summary>Situação derivada dos dados lidos (vazia nas linhas rejeitadas).</summary>
    public string Situacao { get; set; } = string.Empty;

    /// <summary>CtrImportacaoAcao</summary>
    public string Acao { get; set; } = string.Empty;

    /// <summary>Motivo da rejeição.</summary>
    public string? Motivo { get; set; }

    /// <summary>Dados interpretados (null quando a linha é rejeitada).</summary>
    public CtrProcessoCreateDTO? Dados { get; set; }
}

/// <summary>Prévia da importação — interpreta o arquivo sem gravar nada.</summary>
public class CtrImportacaoPrevia
{
    public int TotalLinhas { get; set; }

    public List<CtrImportacaoLinha> Linhas { get; set; } = new();
}

/// <summary>Resultado da importação efetiva (um único SaveChanges).</summary>
public class CtrImportacaoRelatorio
{
    public int Criados { get; set; }

    public int Atualizados { get; set; }

    public int Rejeitados { get; set; }

    public List<CtrImportacaoLinha> Linhas { get; set; } = new();
}
