using System.Text.Json;
using System.Text.Json.Serialization;
using service;

namespace api.Planejamento;

// ── GET api/planejamento/modelo ──────────────────────────────────────────────

/// <summary>
/// O catálogo inteiro do modelo (GET modelo): níveis, etapas com passos, seções,
/// campos e opções, e as seções fora do PDTIC (PETIC-DF e catálogo do DF). Os itens
/// apagados só vêm com ?incluirExcluidos=true (administrador).
/// </summary>
public class PeModeloResponse
{
    public List<PeNivelResponse> Niveis { get; set; } = new();

    public List<PeEtapaResponse> Etapas { get; set; } = new();

    // Escopos petic e df: sem Niveis (dicionário vazio) e com SituacaoGeral
    public List<PeSecaoResponse> SecoesForaDoPdtic { get; set; } = new();
}

public class PeNivelResponse
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public int Ordem { get; set; }

    public bool Ativo { get; set; }

    // Órgãos com este nível escolhido (nível em uso não é apagado, é desativado)
    public int Orgaos { get; set; }
}

public class PeEtapaResponse
{
    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public string? ReferenciaGuia { get; set; }

    public int Ordem { get; set; }

    public bool Sistema { get; set; }

    public List<PePassoResponse> Passos { get; set; } = new();
}

public class PePassoResponse
{
    public long Id { get; set; }

    public long EtapaId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string OQueFazer { get; set; } = string.Empty;

    public string? BaseLegal { get; set; }

    public string? ReferenciaGuia { get; set; }

    public string Tipo { get; set; } = string.Empty;

    // "I" a "IX", ou vários separados por vírgula ("V,VI,IX")
    public string? IncisoDecreto { get; set; }

    public bool Travado { get; set; }

    public bool AceitaNaoSeAplica { get; set; }

    public int Ordem { get; set; }

    public bool Sistema { get; set; }

    public bool Excluido { get; set; }

    // Id do nível (string no JSON) para a situação; todos os níveis aparecem
    public Dictionary<string, string> Niveis { get; set; } = new();

    public List<PeSecaoResponse> Secoes { get; set; } = new();
}

public class PeSecaoResponse
{
    public long Id { get; set; }

    // Nulo nas seções fora do PDTIC
    public long? PassoId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Escopo { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string? PrefixoCodigo { get; set; }

    public int Ordem { get; set; }

    public bool NoDocumento { get; set; }

    public bool NaPlanilha { get; set; }

    public bool Travada { get; set; }

    public string? IncisoDecreto { get; set; }

    public bool Sistema { get; set; }

    public bool Excluido { get; set; }

    // Só fora do PDTIC (escopos petic e df)
    public string? SituacaoGeral { get; set; }

    public Dictionary<string, string> Niveis { get; set; } = new();

    public List<PeCampoResponse> Campos { get; set; } = new();
}

public class PeCampoResponse
{
    public long Id { get; set; }

    public long SecaoId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    public string Tipo { get; set; } = string.Empty;

    // Objeto JSON, conforme o tipo
    public JsonElement Config { get; set; }

    public bool Principal { get; set; }

    // Travado (o tema das ações) ou principal de seção travada: não desliga
    public bool Travado { get; set; }

    public int Ordem { get; set; }

    public bool NoDocumento { get; set; }

    public bool NaPlanilha { get; set; }

    public string? Largura { get; set; }

    public bool Sistema { get; set; }

    public bool Excluido { get; set; }

    public string? SituacaoGeral { get; set; }

    public Dictionary<string, string> Niveis { get; set; } = new();

    public List<PeOpcaoResponse> Opcoes { get; set; } = new();
}

public class PeOpcaoResponse
{
    public long Id { get; set; }

    public long CampoId { get; set; }

    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Ordem { get; set; }

    public bool Ativa { get; set; }

    public string? Cor { get; set; }

    // Os três temas do decreto: não são desativados nem apagados
    public bool Travada { get; set; }

    public bool Sistema { get; set; }
}

// ── GET api/planejamento/modelo/trilha ───────────────────────────────────────

/// <summary>
/// A trilha resolvida de um órgão: só os itens visíveis (situação diferente de
/// desligado), já na ordem e numerados pela posição (etapa N, passo N.M).
/// </summary>
public class PeTrilhaResponse
{
    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public long NivelId { get; set; }

    public string NivelNome { get; set; } = string.Empty;

    // O órgão não tem nível escolhido: vale o primeiro nível ativo pela ordem
    public bool NivelPadrao { get; set; }

    // Falso quando o nível do órgão foi desativado (o órgão continua nele até a troca)
    public bool NivelAtivo { get; set; } = true;

    public List<PeTrilhaEtapa> Etapas { get; set; } = new();
}

public class PeTrilhaEtapa
{
    public int Numero { get; set; }

    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    public string? ReferenciaGuia { get; set; }

    public List<PeTrilhaPasso> Passos { get; set; } = new();
}

public class PeTrilhaPasso
{
    // "N.M" pela posição entre os passos visíveis
    public string Numero { get; set; } = string.Empty;

    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string OQueFazer { get; set; } = string.Empty;

    public string? BaseLegal { get; set; }

    public string? ReferenciaGuia { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string? IncisoDecreto { get; set; }

    public bool Travado { get; set; }

    public bool AceitaNaoSeAplica { get; set; }

    // obrigatorio ou opcional
    public string Situacao { get; set; } = string.Empty;

    public bool AjustadoParaOrgao { get; set; }

    public List<PeTrilhaSecao> Secoes { get; set; } = new();
}

public class PeTrilhaSecao
{
    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string? PrefixoCodigo { get; set; }

    // obrigatorio ou opcional
    public string Situacao { get; set; } = string.Empty;

    public bool Travada { get; set; }

    public List<PeTrilhaCampo> Campos { get; set; } = new();
}

public class PeTrilhaCampo
{
    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public JsonElement Config { get; set; }

    public bool Obrigatorio { get; set; }

    public bool Principal { get; set; }

    public string? Largura { get; set; }

    // Só as opções ativas, na ordem
    public List<PeTrilhaOpcao> Opcoes { get; set; } = new();
}

public class PeTrilhaOpcao
{
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public string? Cor { get; set; }
}

// ── Escrita do modelo (administrador) ────────────────────────────────────────

/// <summary>
/// Corpo dos PUT de um item: só os campos editáveis. Campo ausente do JSON não muda
/// nada; campo presente com nulo (ou texto vazio) limpa o que é opcional e é recusado
/// no que é obrigatório (título, rótulo, nome). O controller lê o JSON e marca o que
/// veio em <see cref="Informados"/>; quem monta o objeto em C# (testes) deixa nulo, e
/// aí todos contam como informados.
/// </summary>
public abstract class PeCorpoParcial
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    [JsonIgnore]
    public HashSet<string>? Informados { get; set; }

    public bool Informou(string propriedade) => Informados == null || Informados.Contains(propriedade);

    public static T Ler<T>(JsonElement corpo) where T : PeCorpoParcial
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie os dados como um objeto JSON.");

        T? dto;
        try
        {
            dto = corpo.Deserialize<T>(Opcoes);
        }
        catch (JsonException)
        {
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Algum dado veio num formato que não serve. Confira e tente de novo.");
        }
        if (dto == null)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie os dados como um objeto JSON.");

        var nomes = typeof(T).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        dto.Informados = corpo.EnumerateObject()
            .Select(p => nomes.TryGetValue(p.Name, out var nome) ? nome : null)
            .Where(n => n != null)
            .Select(n => n!)
            .ToHashSet();
        return dto;
    }
}

public class PeNivelCriarDTO
{
    public string? Nome { get; set; }

    public string? Descricao { get; set; }

    // Opcional: sem ele o servidor gera a partir do nome
    public string? Codigo { get; set; }

    // Opcional: copia a situação de cada passo, seção e campo deste nível
    public long? CopiarDe { get; set; }
}

public class PeNivelAtualizarDTO : PeCorpoParcial
{
    public string? Nome { get; set; }

    public string? Descricao { get; set; }

    // false desativa (o último nível ativo não pode ser desativado)
    public bool? Ativo { get; set; }
}

/// <summary>Nova ordem de um grupo de itens: todos os ids do grupo, na ordem desejada.</summary>
public class PeOrdemDTO
{
    public List<long>? Ids { get; set; }
}

public class PeEtapaAtualizarDTO : PeCorpoParcial
{
    public string? Titulo { get; set; }

    public string? Descricao { get; set; }

    public string? ReferenciaGuia { get; set; }

    // Posição da etapa (1 = primeira); as outras são renumeradas
    public int? Ordem { get; set; }
}

public class PePassoCriarDTO
{
    public long EtapaId { get; set; }

    public string? Titulo { get; set; }

    public string? OQueFazer { get; set; }

    public string? BaseLegal { get; set; }

    public string? ReferenciaGuia { get; set; }

    public bool? AceitaNaoSeAplica { get; set; }
}

public class PePassoAtualizarDTO : PeCorpoParcial
{
    public string? Titulo { get; set; }

    public string? OQueFazer { get; set; }

    public string? BaseLegal { get; set; }

    public string? ReferenciaGuia { get; set; }

    public bool? AceitaNaoSeAplica { get; set; }

    // Só passo criado pelo administrador muda de chave; o tipo não muda em nenhum
    public string? Chave { get; set; }

    public string? Tipo { get; set; }
}

/// <summary>
/// Situação em cada nível ({ "Niveis": { "3": "obrigatorio" } }; os níveis ausentes não
/// mudam) ou, nas seções e campos fora do PDTIC, a situação geral.
/// </summary>
public class PeSituacoesDTO
{
    public Dictionary<string, string?>? Niveis { get; set; }

    public string? SituacaoGeral { get; set; }
}

public class PeSecaoCriarDTO
{
    // Seção do PDTIC: o passo. Fora do PDTIC: nulo, com o Escopo (petic ou df)
    public long? PassoId { get; set; }

    public string? Escopo { get; set; }

    public string? Titulo { get; set; }

    public string? Ajuda { get; set; }

    public string? Tipo { get; set; }

    public string? PrefixoCodigo { get; set; }

    // Opcional: sem ela o servidor gera a partir do título
    public string? Chave { get; set; }
}

public class PeSecaoAtualizarDTO : PeCorpoParcial
{
    public string? Titulo { get; set; }

    public string? Ajuda { get; set; }

    public string? Tipo { get; set; }

    public string? PrefixoCodigo { get; set; }

    public bool? NoDocumento { get; set; }

    public bool? NaPlanilha { get; set; }

    public string? Chave { get; set; }
}

public class PeCampoCriarDTO
{
    public long SecaoId { get; set; }

    // Opcional: sem ela o servidor gera a partir do rótulo
    public string? Chave { get; set; }

    public string? Rotulo { get; set; }

    public string? Ajuda { get; set; }

    public string? Tipo { get; set; }

    public JsonElement? Config { get; set; }

    public string? Largura { get; set; }
}

public class PeCampoAtualizarDTO : PeCorpoParcial
{
    public string? Rotulo { get; set; }

    public string? Ajuda { get; set; }

    public string? Tipo { get; set; }

    public JsonElement? Config { get; set; }

    public string? Largura { get; set; }

    public bool? NoDocumento { get; set; }

    public bool? NaPlanilha { get; set; }

    public string? Chave { get; set; }
}

public class PeOpcaoCriarDTO
{
    // Opcional: sem ele o servidor gera a partir do rótulo
    public string? Valor { get; set; }

    public string? Rotulo { get; set; }

    public string? Cor { get; set; }
}

public class PeOpcaoAtualizarDTO : PeCorpoParcial
{
    public string? Rotulo { get; set; }

    public string? Cor { get; set; }

    public bool? Ativa { get; set; }
}

// ── Histórico do modelo ──────────────────────────────────────────────────────

/// <summary>
/// Filtros do histórico do modelo (GET modelo/historico). O parâmetro da action se chama
/// "consulta", nunca o nome de uma propriedade (armadilha do model binding).
/// </summary>
public class PeHistoricoConsulta
{
    // PeDominios.EntidadeHistorico; vazio = todas
    public string? Entidade { get; set; }

    public long? EntidadeId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class PeHistoricoResponse
{
    public long Id { get; set; }

    public string Entidade { get; set; } = string.Empty;

    public long EntidadeId { get; set; }

    public string Acao { get; set; } = string.Empty;

    // Objetos JSON; nulos na criação (Antes) e na remoção (Depois)
    public JsonElement? Antes { get; set; }

    public JsonElement? Depois { get; set; }

    public DateTime AlteradoEm { get; set; }

    public string AlteradoPor { get; set; } = string.Empty;
}

// ── Órgãos: nível e ajustes ──────────────────────────────────────────────────

/// <summary>Filtros da lista de órgãos (parte da sigla ou do nome).</summary>
public class PeOrgaosConsulta
{
    public string? Filtro { get; set; }
}

public class PeOrgaoNivelResponse
{
    public long OrgaoId { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    // Nulo só quando não há nível ativo algum (modelo ainda não carregado)
    public long? NivelId { get; set; }

    public string? NivelNome { get; set; }

    // Sem nível escolhido: vale o primeiro nível ativo pela ordem
    public bool NivelPadrao { get; set; }

    // Falso quando o nível escolhido foi desativado depois
    public bool NivelAtivo { get; set; } = true;

    // Quantos ajustes o órgão tem por cima do nível
    public int Ajustes { get; set; }
}

public class PeOrgaoNivelDTO
{
    public long? NivelId { get; set; }

    // Obrigatória
    public string? Justificativa { get; set; }
}

public class PeOrgaoNivelHistoricoResponse
{
    public long? NivelAnteriorId { get; set; }

    public string? NivelAnterior { get; set; }

    // O nível anterior era o padrão (o órgão ainda não tinha nível escolhido)
    public bool NivelAnteriorPadrao { get; set; }

    public long? NivelNovoId { get; set; }

    public string? NivelNovo { get; set; }

    public string? Justificativa { get; set; }

    public DateTime DefinidoEm { get; set; }

    public string DefinidoPor { get; set; } = string.Empty;
}

/// <summary>Um ajuste do órgão, como o PUT recebe (Situacao nula remove o ajuste).</summary>
public class PeOrgaoAjusteDTO
{
    public string? AlvoTipo { get; set; }

    public long AlvoId { get; set; }

    public string? Situacao { get; set; }

    public string? Justificativa { get; set; }
}

public class PeOrgaoAjusteResponse
{
    public string AlvoTipo { get; set; } = string.Empty;

    public long AlvoId { get; set; }

    public string Situacao { get; set; } = string.Empty;

    public string? Justificativa { get; set; }

    // Título do passo ou da seção, rótulo do campo
    public string? AlvoTitulo { get; set; }

    // O item foi apagado depois do ajuste (o ajuste fica guardado, sem efeito)
    public bool AlvoExcluido { get; set; }

    public DateTime AlteradoEm { get; set; }

    public string AlteradoPor { get; set; } = string.Empty;
}
