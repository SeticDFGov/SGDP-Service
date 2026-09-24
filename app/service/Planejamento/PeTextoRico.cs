using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace service.Planejamento;

/// <summary>
/// Texto rico dos campos texto_rico (E4): o JSON do editor TipTap
/// ({ "type": "doc", "content": [...] }), conferido contra uma lista fechada e guardado já
/// limpo (plano, seção 9: nada de HTML cru).
/// <list type="bullet">
/// <item>nós: doc, paragraph, heading (level 3 ou 4), bulletList, orderedList, listItem,
/// table, tableRow, tableHeader, tableCell, image, hardBreak e text, cada um só onde o
/// editor o põe (item de lista dentro de lista, célula dentro de linha e assim por diante);</item>
/// <item>marcas: bold, italic, underline e link (href http ou https, endereço completo);</item>
/// <item>imagem: src "api/planejamento/arquivos/{id}" (com ou sem a barra no começo, sem
/// servidor): o arquivo precisa ser uma imagem do próprio módulo, e quem grava confere o
/// arquivo no banco (PeRegistroService);</item>
/// <item>atributos: só os conhecidos de cada nó e marca, com valor válido (título e link
/// com valor errado recusam; os demais, como alinhamento ou classe, são descartados).</item>
/// </list>
/// Qualquer outro nó ou marca (tachado, código, citação, linha horizontal...) é recusado
/// com a mensagem do campo. Texto sem nenhuma letra nem imagem conta como vazio.
/// </summary>
public static partial class PeTextoRico
{
    // Níveis de nó (o leitor de JSON do ASP.NET para em 64 níveis de JSON, cerca de 30 de nó)
    public const int ProfundidadeMaxima = 25;
    public const int NosMaximos = 50_000;
    public const int MaximoLink = 2000;
    public const int MaximoAtributoTexto = 500;

    [GeneratedRegex(@"^/?api/planejamento/arquivos/([0-9]{1,18})$")]
    private static partial Regex SrcDeArquivo();

    /// <summary>O documento limpo (nulo quando vazio), o erro (nulo quando válido) e as imagens usadas.</summary>
    public sealed record Resultado(JsonObject? Documento, string? Erro, IReadOnlyList<long> Imagens);

    private const string FormatoRuim = "O texto formatado veio num formato que não serve. Atualize a tela e tente de novo.";
    private const string ListaAceita = "Use parágrafos, títulos, listas, tabelas, imagens enviadas, negrito, itálico, sublinhado e links.";

    private static readonly string[] Blocos = { "paragraph", "heading", "bulletList", "orderedList", "table", "image" };
    private static readonly string[] Inline = { "text", "hardBreak", "image" };

    // Filhos aceitos em cada nó (vazio = folha)
    private static readonly IReadOnlyDictionary<string, string[]> Filhos = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["doc"] = Blocos,
        ["paragraph"] = Inline,
        ["heading"] = new[] { "text", "hardBreak" },
        ["bulletList"] = new[] { "listItem" },
        ["orderedList"] = new[] { "listItem" },
        ["listItem"] = Blocos,
        ["table"] = new[] { "tableRow" },
        ["tableRow"] = new[] { "tableHeader", "tableCell" },
        ["tableHeader"] = Blocos,
        ["tableCell"] = Blocos,
        ["text"] = Array.Empty<string>(),
        ["hardBreak"] = Array.Empty<string>(),
        ["image"] = Array.Empty<string>()
    };

    // Nós que precisam de pelo menos um filho (senão o editor monta um documento inválido)
    private static readonly HashSet<string> ExigemFilhos =
        new(StringComparer.Ordinal) { "bulletList", "orderedList", "listItem", "table", "tableRow", "tableHeader", "tableCell" };

    private static readonly HashSet<string> Marcas = new(StringComparer.Ordinal) { "bold", "italic", "underline", "link" };

    private static readonly HashSet<string> TiposDeListaNumerada = new(StringComparer.Ordinal) { "1", "a", "A", "i", "I" };

    private static readonly HashSet<string> RelAceitos = new(StringComparer.Ordinal) { "noopener", "noreferrer", "nofollow" };

    // Nomes em português dos recursos do editor que ficam de fora (para a mensagem)
    private static readonly IReadOnlyDictionary<string, string> NomesDeFora = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["strike"] = "tachado",
        ["code"] = "código",
        ["codeBlock"] = "bloco de código",
        ["blockquote"] = "citação",
        ["horizontalRule"] = "linha horizontal",
        ["taskList"] = "lista de tarefas",
        ["taskItem"] = "lista de tarefas",
        ["highlight"] = "marca-texto",
        ["textStyle"] = "estilo de texto",
        ["subscript"] = "subscrito",
        ["superscript"] = "sobrescrito",
        ["youtube"] = "vídeo",
        ["mention"] = "menção",
        ["details"] = "bloco recolhível",
        ["mathematics"] = "fórmula"
    };

    private sealed class Estado
    {
        public int Nos;
        public bool TemConteudo;
        public readonly List<long> Imagens = new();
    }

    private sealed class Invalido : Exception
    {
        public Invalido(string mensagem) : base(mensagem) { }
    }

    /// <summary>Confere e limpa o JSON do editor.</summary>
    public static Resultado Validar(JsonElement entrada)
    {
        if (entrada.ValueKind != JsonValueKind.Object) return Falha(FormatoRuim);
        try
        {
            var estado = new Estado();
            var tipo = TipoDe(entrada);
            if (tipo != "doc") return Falha(FormatoRuim);
            var documento = No(entrada, "doc", 0, estado);
            return estado.TemConteudo
                ? new Resultado(documento, null, estado.Imagens)
                : new Resultado(null, null, Array.Empty<long>());
        }
        catch (Invalido ex)
        {
            return Falha(ex.Message);
        }
    }

    /// <summary>O documento guardado tem pelo menos uma letra ou uma imagem.</summary>
    public static bool TemConteudo(JsonNode? valor)
    {
        switch (valor)
        {
            case JsonObject o:
                if (PeRegistroDados.Texto(o["type"]) == "image") return true;
                if (PeRegistroDados.Texto(o["text"]) is string texto && !string.IsNullOrWhiteSpace(texto)) return true;
                return o["content"] is JsonArray filhos && filhos.Any(TemConteudo);
            case JsonArray a:
                return a.Any(TemConteudo);
            default:
                return false;
        }
    }

    /// <summary>Os ids dos arquivos das imagens de um documento guardado.</summary>
    public static List<long> ImagensDe(JsonNode? valor)
    {
        var ids = new List<long>();
        void Percorrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject o:
                    if (PeRegistroDados.Texto(o["type"]) == "image"
                        && o["attrs"] is JsonObject attrs && PeRegistroDados.Texto(attrs["src"]) is string src
                        && IdDaImagem(src) is long id && !ids.Contains(id))
                        ids.Add(id);
                    if (o["content"] is JsonArray filhos)
                        foreach (var filho in filhos) Percorrer(filho);
                    break;
            }
        }
        Percorrer(valor);
        return ids;
    }

    private static Resultado Falha(string mensagem) => new(null, mensagem, Array.Empty<long>());

    private static string? TipoDe(JsonElement no) =>
        no.ValueKind == JsonValueKind.Object && no.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;

    private static JsonObject No(JsonElement no, string tipo, int profundidade, Estado estado)
    {
        if (profundidade > ProfundidadeMaxima)
            throw new Invalido("O texto formatado tem níveis demais (listas ou tabelas uma dentro da outra). Simplifique e tente de novo.");
        if (++estado.Nos > NosMaximos) throw new Invalido("O texto formatado passou do tamanho máximo.");

        var saida = new JsonObject { ["type"] = tipo };

        // Atributos: só os conhecidos, com valor válido
        var attrs = Atributos(tipo, no.TryGetProperty("attrs", out var a) ? a : default, estado);
        if (attrs.Count > 0) saida["attrs"] = attrs;

        if (tipo == "text")
        {
            if (!no.TryGetProperty("text", out var t) || t.ValueKind != JsonValueKind.String) throw new Invalido(FormatoRuim);
            var texto = t.GetString()!;
            saida["text"] = texto;
            if (!string.IsNullOrWhiteSpace(texto)) estado.TemConteudo = true;
        }
        if (tipo == "image") estado.TemConteudo = true;

        // Marcas: só nos nós de linha (texto, quebra de linha e imagem na linha)
        if (Inline.Contains(tipo) && no.TryGetProperty("marks", out var marcas) && marcas.ValueKind != JsonValueKind.Null)
        {
            var lista = MarcasDe(marcas);
            if (lista.Count > 0) saida["marks"] = lista;
        }

        // Filhos
        var aceitos = Filhos[tipo];
        if (no.TryGetProperty("content", out var conteudo) && conteudo.ValueKind != JsonValueKind.Null)
        {
            if (conteudo.ValueKind != JsonValueKind.Array) throw new Invalido(FormatoRuim);
            if (aceitos.Length == 0)
            {
                if (conteudo.GetArrayLength() > 0) throw new Invalido(FormatoRuim);
            }
            else
            {
                var filhos = new JsonArray();
                foreach (var filho in conteudo.EnumerateArray())
                {
                    var tipoFilho = TipoDe(filho) ?? throw new Invalido(FormatoRuim);
                    if (!Filhos.ContainsKey(tipoFilho)) throw new Invalido(ForaDaLista(tipoFilho));
                    if (!aceitos.Contains(tipoFilho)) throw new Invalido(FormatoRuim);
                    // Texto vazio não existe no editor: sai sem erro
                    if (tipoFilho == "text" && filho.TryGetProperty("text", out var tf) && tf.ValueKind == JsonValueKind.String
                        && tf.GetString()!.Length == 0)
                        continue;
                    filhos.Add(No(filho, tipoFilho, profundidade + 1, estado));
                }
                if (filhos.Count > 0) saida["content"] = filhos;
            }
        }
        if (ExigemFilhos.Contains(tipo) && saida["content"] is not JsonArray { Count: > 0 }) throw new Invalido(FormatoRuim);
        return saida;
    }

    private static JsonObject Atributos(string tipo, JsonElement attrs, Estado estado)
    {
        var saida = new JsonObject();
        var temAttrs = attrs.ValueKind == JsonValueKind.Object;
        JsonElement Attr(string nome) => temAttrs && attrs.TryGetProperty(nome, out var v) ? v : default;

        switch (tipo)
        {
            case "heading":
            {
                var nivel = Inteiro(Attr("level"));
                if (nivel is not (3 or 4)) throw new Invalido("Use só títulos de nível 3 ou 4.");
                saida["level"] = nivel.Value;
                break;
            }
            case "orderedList":
            {
                if (Inteiro(Attr("start")) is int inicio && inicio is >= 1 and <= 1_000_000) saida["start"] = inicio;
                var tipoLista = Attr("type");
                if (tipoLista.ValueKind == JsonValueKind.String && TiposDeListaNumerada.Contains(tipoLista.GetString()!))
                    saida["type"] = tipoLista.GetString();
                break;
            }
            case "tableCell":
            case "tableHeader":
            {
                var colspan = Inteiro(Attr("colspan"));
                if (colspan is >= 1 and <= 1000) saida["colspan"] = colspan.Value;
                if (Inteiro(Attr("rowspan")) is int rowspan && rowspan is >= 1 and <= 1000) saida["rowspan"] = rowspan;
                var larguras = Attr("colwidth");
                if (larguras.ValueKind == JsonValueKind.Array)
                {
                    var lista = larguras.EnumerateArray().Select(Inteiro).ToList();
                    if (lista.Count > 0 && lista.Count <= 1000 && lista.All(l => l is >= 0 and <= 100_000))
                        saida["colwidth"] = new JsonArray(lista.Select(l => (JsonNode)JsonValue.Create(l!.Value)).ToArray());
                }
                break;
            }
            case "image":
            {
                var src = Attr("src");
                if (src.ValueKind != JsonValueKind.String || IdDaImagem(src.GetString()!) is not long id)
                    throw new Invalido("A imagem precisa ser enviada pelo botão de imagem do editor (arquivo do próprio sistema).");
                saida["src"] = $"api/planejamento/arquivos/{id.ToString(CultureInfo.InvariantCulture)}";
                if (!estado.Imagens.Contains(id)) estado.Imagens.Add(id);
                foreach (var nome in new[] { "alt", "title" })
                {
                    var valor = Attr(nome);
                    if (valor.ValueKind == JsonValueKind.String && valor.GetString()!.Length <= MaximoAtributoTexto)
                        saida[nome] = valor.GetString();
                }
                foreach (var nome in new[] { "width", "height" })
                    if (Inteiro(Attr(nome)) is int medida && medida is >= 1 and <= 100_000)
                        saida[nome] = medida;
                break;
            }
            // Os outros nós não têm atributo que o módulo use (alinhamento, classe e afins saem)
        }
        return saida;
    }

    private static JsonArray MarcasDe(JsonElement marcas)
    {
        if (marcas.ValueKind != JsonValueKind.Array) throw new Invalido(FormatoRuim);
        var saida = new JsonArray();
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marca in marcas.EnumerateArray())
        {
            var tipo = TipoDe(marca) ?? throw new Invalido(FormatoRuim);
            if (!Marcas.Contains(tipo)) throw new Invalido(ForaDaLista(tipo));
            if (!vistas.Add(tipo)) continue;

            var limpa = new JsonObject { ["type"] = tipo };
            if (tipo == "link")
            {
                var attrs = marca.TryGetProperty("attrs", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
                var href = attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty("href", out var h) && h.ValueKind == JsonValueKind.String
                    ? h.GetString()!.Trim()
                    : null;
                if (!LinkValido(href))
                    throw new Invalido(string.IsNullOrEmpty(href)
                        ? "Todo link precisa de um endereço que comece com http:// ou https://."
                        : $"O link \"{Curto(href!)}\" precisa ser um endereço completo que comece com http:// ou https://.");
                var attrsLimpos = new JsonObject { ["href"] = href };
                if (attrs.TryGetProperty("target", out var alvo) && alvo.ValueKind == JsonValueKind.String && alvo.GetString() == "_blank")
                    attrsLimpos["target"] = "_blank";
                if (attrs.TryGetProperty("rel", out var rel) && rel.ValueKind == JsonValueKind.String)
                {
                    var partes = rel.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length > 0 && partes.All(RelAceitos.Contains)) attrsLimpos["rel"] = string.Join(' ', partes);
                }
                limpa["attrs"] = attrsLimpos;
            }
            saida.Add(limpa);
        }
        return saida;
    }

    /// <summary>Endereço completo com http ou https e servidor (sem javascript:, data: e afins).</summary>
    public static bool LinkValido(string? href) =>
        !string.IsNullOrWhiteSpace(href) && href.Length <= MaximoLink
        && Uri.TryCreate(href, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host)
        && !href.Any(char.IsControl);

    /// <summary>O id do arquivo de uma imagem ("api/planejamento/arquivos/12"), ou nulo.</summary>
    public static long? IdDaImagem(string src)
    {
        var m = SrcDeArquivo().Match(src.Trim());
        return m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    private static int? Inteiro(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var n) ? n : null;

    private static string ForaDaLista(string tipo) =>
        $"O texto formatado usa um recurso que não é aceito aqui ({(NomesDeFora.TryGetValue(tipo, out var nome) ? nome : Curto(tipo))}). {ListaAceita}";

    private static string Curto(string texto) => texto.Length <= 60 ? texto : texto[..57] + "...";
}
