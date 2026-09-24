using System.Text.Json;
using System.Text.Json.Serialization;
using service;

namespace api.Planejamento;

// ── Registros (motor da E3; a E4 usa a mesma forma para o PDTIC) ─────────────

/// <summary>
/// GET .../secoes/{secaoChave}/registros: a seção como o dono a vê (só os campos visíveis,
/// na ordem, com a obrigatoriedade) e os registros, com os valores, os rótulos prontos
/// para exibir e as ligações.
/// </summary>
public class PeRegistrosResponse
{
    public PeRegistroSecaoResponse Secao { get; set; } = new();

    public List<PeRegistroResponse> Registros { get; set; } = new();

    // Quem chama pode incluir, editar, apagar e reordenar (papel e, no PETIC, versão em rascunho)
    public bool PodeEditar { get; set; }
}

public class PeRegistroSecaoResponse
{
    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Ajuda { get; set; }

    // formulario ou tabela
    public string Tipo { get; set; } = string.Empty;

    public string? PrefixoCodigo { get; set; }

    // Mesma forma dos campos da trilha (Obrigatorio já resolvido; Opcoes só as ativas)
    public List<PeTrilhaCampo> Campos { get; set; } = new();
}

public class PeRegistroResponse
{
    public long Id { get; set; }

    // "OE01"; nulo no formulário e na tabela sem prefixo
    public string? Codigo { get; set; }

    public int Ordem { get; set; }

    // Registro do sistema (os princípios do art. 4º): não se edita nem se apaga
    public bool Sistema { get; set; }

    // Chave do campo para o valor, só dos campos visíveis e com valor (ligações vão em Vinculos)
    public Dictionary<string, JsonElement> Dados { get; set; } = new();

    // Chave do campo para o texto pronto para exibir (lista, data, moeda, sim ou não, ligações...)
    public Dictionary<string, string> Rotulos { get; set; } = new();

    // Chave do campo de ligação para os registros ligados (todo campo de ligação visível aparece)
    public Dictionary<string, List<PeVinculoResponse>> Vinculos { get; set; } = new();

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

public class PeVinculoResponse
{
    public long RegistroId { get; set; }

    public string? Codigo { get; set; }

    // O campo principal do registro ligado, como texto (até 300 caracteres)
    public string Resumo { get; set; } = string.Empty;

    // Para ordenar as ligações como a seção de destino ordena os registros
    [JsonIgnore]
    public int Ordem { get; set; }
}

/// <summary>
/// Corpo do POST e do PUT de um registro: { "Dados": { chave: valor }, "Vinculos": { chave:
/// [ids] } }. No PUT, Dados ou Vinculos ausentes (ou nulos) não mudam; presentes, trocam o
/// que o registro tinha nos campos visíveis (chave ausente = vazio). Campos calculados e
/// escondidos são ignorados em Dados.
/// </summary>
public class PeRegistroSalvarDTO
{
    public Dictionary<string, JsonElement>? Dados { get; set; }

    public Dictionary<string, List<long>>? Vinculos { get; set; }

    /// <summary>
    /// Lê o corpo com mensagens em linguagem simples (em vez do 400 genérico do model binding):
    /// Dados precisa ser um objeto; em Vinculos, cada campo recebe uma lista de ids (um id
    /// solto também vale, e nulo vira lista vazia).
    /// </summary>
    public static PeRegistroSalvarDTO Ler(JsonElement corpo)
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie os dados do registro como um objeto JSON.");
        PeCorpo.ConferirTexto(corpo);

        var dto = new PeRegistroSalvarDTO();
        foreach (var propriedade in corpo.EnumerateObject())
        {
            if (string.Equals(propriedade.Name, "Dados", StringComparison.OrdinalIgnoreCase))
            {
                if (propriedade.Value.ValueKind == JsonValueKind.Null) continue;
                if (propriedade.Value.ValueKind != JsonValueKind.Object)
                    throw new ApiException(ErrorCode.PeDadosInvalidos, "\"Dados\" precisa ser um objeto com a chave de cada campo.");
                dto.Dados = new Dictionary<string, JsonElement>();
                foreach (var campo in propriedade.Value.EnumerateObject())
                    dto.Dados[campo.Name] = campo.Value.Clone();
            }
            else if (string.Equals(propriedade.Name, "Vinculos", StringComparison.OrdinalIgnoreCase))
            {
                if (propriedade.Value.ValueKind == JsonValueKind.Null) continue;
                if (propriedade.Value.ValueKind != JsonValueKind.Object)
                    throw new ApiException(ErrorCode.PeDadosInvalidos, "\"Vinculos\" precisa ser um objeto com a chave de cada campo de ligação.");
                dto.Vinculos = new Dictionary<string, List<long>>();
                foreach (var campo in propriedade.Value.EnumerateObject())
                    dto.Vinculos[campo.Name] = Ids(campo.Name, campo.Value);
            }
        }
        return dto;
    }

    private static List<long> Ids(string chave, JsonElement valor)
    {
        var erro = new ApiException(ErrorCode.PeDadosInvalidos,
            $"A ligação \"{chave}\" precisa ser uma lista com os ids dos registros ligados.");
        switch (valor.ValueKind)
        {
            case JsonValueKind.Null:
                return new List<long>();
            case JsonValueKind.Number:
                return valor.TryGetInt64(out var unico) ? new List<long> { unico } : throw erro;
            case JsonValueKind.Array:
                var ids = new List<long>();
                foreach (var item in valor.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt64(out var id)) throw erro;
                    ids.Add(id);
                }
                return ids;
            default:
                throw erro;
        }
    }
}

/// <summary>Item de um catálogo (GET catalogos/{catalogo}): o registro, o código e o texto do campo principal.</summary>
public class PeCatalogoItemResponse
{
    public long Id { get; set; }

    public string? Codigo { get; set; }

    public string Rotulo { get; set; } = string.Empty;
}

// ── Arquivos ─────────────────────────────────────────────────────────────────

public class PeArquivoResponse
{
    public long Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string TipoMime { get; set; } = string.Empty;

    public long Tamanho { get; set; }
}

/// <summary>O arquivo que chegou no multipart (o controller monta; o serviço valida).</summary>
public class PeArquivoUpload
{
    public string? NomeOriginal { get; set; }

    public long Tamanho { get; set; }

    public Stream Conteudo { get; set; } = Stream.Null;
}

/// <summary>O arquivo para baixar: binário, tipo e nome.</summary>
public record PeArquivoDownload(byte[] Conteudo, string TipoMime, string Nome);

/// <summary>Uma planilha pronta: binário, tipo e nome do arquivo.</summary>
public record PePlanilhaArquivo(byte[] Conteudo, string TipoMime, string NomeArquivo);

// ── Leitura de corpos (mensagens em linguagem simples) ───────────────────────

/// <summary>
/// Lê um corpo JSON num DTO sem o 400 genérico do model binding: JSON que não é objeto, ou
/// valor no tipo errado, vira 400 PeDadosInvalidos com uma mensagem simples.
/// </summary>
public static class PeCorpo
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// O corpo é UTF-8 válido. O leitor de JSON só confere os bytes de um texto quando o lê,
    /// e um texto inválido estouraria mais adiante como 500 sem corpo.
    /// </summary>
    public static void ConferirTexto(JsonElement corpo)
    {
        try
        {
            _ = corpo.GetRawText();
        }
        catch (InvalidOperationException)
        {
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Os dados vieram com caracteres que não são UTF-8. Confira e tente de novo.");
        }
    }

    public static T Ler<T>(JsonElement corpo) where T : class
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie os dados como um objeto JSON.");
        ConferirTexto(corpo);
        try
        {
            return corpo.Deserialize<T>(Opcoes)
                ?? throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie os dados como um objeto JSON.");
        }
        catch (JsonException)
        {
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Algum dado veio num formato que não serve. Confira e tente de novo.");
        }
    }
}
