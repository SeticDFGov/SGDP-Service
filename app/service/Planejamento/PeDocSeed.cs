using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Models.Planejamento;

namespace service.Planejamento;

// ── Formato do modelo inicial do documento (Models/Planejamento/Seed/documento-inicial.json) ──

/// <summary>
/// O modelo do documento da SGDI em JSON (E5): um modelo por tipo, com os capítulos (e os
/// subcapítulos) na ordem do documento e os blocos de cada um. O texto padrão vem em linhas de
/// uma marcação simples (<see cref="PeDocTextoSimples"/>) que o carregador converte no JSON do
/// TipTap; os blocos de dados trazem a seção, as colunas, o filtro, o tema ou o fluxo.
/// </summary>
public sealed class PeSeedDocumentos
{
    public List<PeSeedDocModelo> Modelos { get; set; } = new();
}

public sealed class PeSeedDocModelo
{
    public string Tipo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public List<PeSeedDocCapitulo> Capitulos { get; set; } = new();
}

public sealed class PeSeedDocCapitulo
{
    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public bool Numerado { get; set; } = true;

    public bool Obrigatorio { get; set; }

    public bool Travado { get; set; }

    public string? Inciso { get; set; }

    // Chave do passo que alimenta o capítulo
    public string? Passo { get; set; }

    public List<PeSeedDocBloco> Blocos { get; set; } = new();

    public List<PeSeedDocCapitulo> Subcapitulos { get; set; } = new();
}

public sealed class PeSeedDocBloco
{
    public string Tipo { get; set; } = PeDominios.TipoBloco.Texto;

    // Linhas da marcação simples (texto)
    public List<string>? Texto { get; set; }

    public string? Secao { get; set; }

    public List<string>? Colunas { get; set; }

    public JsonElement? Filtro { get; set; }

    public string? Tema { get; set; }

    public string? Fluxo { get; set; }

    public bool PaginaDeitada { get; set; }

    // Desde a versão 7 (F1), só no bloco de texto: os textos (linhas da marcação simples) que o
    // carregador gravou antes neste bloco. No capítulo que já existe, o bloco da mesma posição
    // que ainda tem um deles passa ao texto de hoje
    public List<List<string>>? Antes { get; set; }

    /// <summary>O config do bloco antes de conferir (chaves como o PeDocConfig lê).</summary>
    public JsonElement Config()
    {
        var config = new JsonObject();
        if (Texto != null) config["Texto"] = PeDocTextoSimples.ParaTipTap(Texto);
        if (Secao != null) config["Secao"] = Secao;
        if (Colunas != null) config["Colunas"] = new JsonArray(Colunas.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray());
        if (Filtro is { ValueKind: JsonValueKind.Object } filtro) config["Filtro"] = JsonNode.Parse(filtro.GetRawText());
        if (Tema != null) config["Tema"] = Tema;
        if (Fluxo != null) config["Fluxo"] = Fluxo;
        if (PaginaDeitada) config["PaginaDeitada"] = true;
        return JsonSerializer.SerializeToElement(config);
    }
}

/// <summary>Leitura e conferência do modelo inicial do documento (antes de gravar qualquer coisa).</summary>
public static class PeDocSeed
{
    public const string Recurso = "Planejamento.documento-inicial.json";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>O modelo do documento embutido na aplicação.</summary>
    public static PeSeedDocumentos Ler()
    {
        using var stream = typeof(PeDocSeed).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} não encontrado no assembly.");
        using var leitor = new StreamReader(stream);
        return Ler(leitor.ReadToEnd());
    }

    public static PeSeedDocumentos Ler(string json) =>
        JsonSerializer.Deserialize<PeSeedDocumentos>(json, Opcoes) ?? throw new InvalidOperationException("Modelo do documento vazio.");

    /// <summary>
    /// Confere a estrutura: tipo de documento, chaves únicas no modelo e no formato, um nível
    /// de subcapítulo, os capítulos especiais no primeiro nível, travado com inciso, passo que
    /// existe e bloco do tipo certo com o que ele precisa. O config de cada bloco é conferido na
    /// carga (PeDocConfig), com o modelo de seções em volta.
    /// </summary>
    public static void Validar(PeSeedDocumentos seed, ISet<string> passos)
    {
        void Falha(string mensagem) => throw new InvalidOperationException("Modelo inicial do documento inválido: " + mensagem);

        var tipos = new HashSet<string>();
        foreach (var modelo in seed.Modelos)
        {
            if (!PeDominios.TipoDocumento.Todos.Contains(modelo.Tipo) || !tipos.Add(modelo.Tipo)) Falha($"tipo \"{modelo.Tipo}\" repetido ou fora do domínio.");
            if (string.IsNullOrWhiteSpace(modelo.Nome)) Falha($"modelo \"{modelo.Tipo}\" sem nome.");

            var chaves = new HashSet<string>();
            void Capitulo(PeSeedDocCapitulo capitulo, bool subcapitulo)
            {
                if (!chaves.Add(capitulo.Chave) || !PeChaves.ChaveValida(capitulo.Chave, PeChaves.MaximoSecao))
                    Falha($"capítulo \"{capitulo.Chave}\" repetido ou fora do formato.");
                if (string.IsNullOrWhiteSpace(capitulo.Titulo) || capitulo.Titulo.Length > 200) Falha($"título do capítulo \"{capitulo.Chave}\".");
                if (capitulo.Inciso != null && !PeDominios.Inciso.EhValido(capitulo.Inciso)) Falha($"inciso do capítulo \"{capitulo.Chave}\".");
                if (capitulo.Travado && capitulo.Inciso == null) Falha($"o capítulo travado \"{capitulo.Chave}\" precisa do inciso.");
                if (capitulo.Passo != null && !passos.Contains(capitulo.Passo)) Falha($"o passo \"{capitulo.Passo}\" do capítulo \"{capitulo.Chave}\" não existe.");
                if (subcapitulo && (capitulo.Subcapitulos.Count > 0 || capitulo.Travado))
                    Falha($"o subcapítulo \"{capitulo.Chave}\" não tem subcapítulos nem é travado.");
                if (subcapitulo && (PeDominios.CapituloEspecial.EhPreTextual(capitulo.Chave) || capitulo.Chave == PeDominios.CapituloEspecial.Anexos))
                    Falha($"o capítulo especial \"{capitulo.Chave}\" fica no primeiro nível.");

                foreach (var bloco in capitulo.Blocos)
                {
                    var falta = bloco.Tipo switch
                    {
                        PeDominios.TipoBloco.Texto => bloco.Texto == null,
                        PeDominios.TipoBloco.TabelaSecao => bloco.Secao == null,
                        PeDominios.TipoBloco.ListaTema => bloco.Tema == null,
                        PeDominios.TipoBloco.Fluxo => bloco.Fluxo == null,
                        PeDominios.TipoBloco.MatrizSwot or PeDominios.TipoBloco.QuebraPagina => false,
                        _ when PeDominios.TipoBloco.DoAcompanhamento.Contains(bloco.Tipo) => false,
                        _ => true
                    };
                    if (falta) Falha($"bloco \"{bloco.Tipo}\" do capítulo \"{capitulo.Chave}\" fora do domínio ou sem o que precisa.");
                }
                foreach (var sub in capitulo.Subcapitulos) Capitulo(sub, true);
            }

            foreach (var capitulo in modelo.Capitulos) Capitulo(capitulo, false);
        }
    }
}

/// <summary>
/// Marcação simples dos textos padrão do modelo inicial, convertida no JSON do TipTap (uma
/// linha por parágrafo, item ou linha de tabela):
/// <list type="bullet">
/// <item>"### título" e "#### título": títulos de nível 3 e 4;</item>
/// <item>"- item": item de lista com marcadores; "1. item": item de lista numerada;</item>
/// <item>"| a | b |": linha de tabela (a primeira linha é o cabeçalho);</item>
/// <item>linha vazia: parágrafo em branco; o resto: parágrafo;</item>
/// <item>"**texto**" no meio da linha: negrito.</item>
/// </list>
/// </summary>
public static partial class PeDocTextoSimples
{
    [GeneratedRegex(@"^(\d+)\. (.*)$")]
    private static partial Regex ItemNumerado();

    public static JsonObject ParaTipTap(IEnumerable<string> linhas)
    {
        var conteudo = new JsonArray();
        JsonArray? itens = null;
        string? tipoLista = null;
        JsonArray? linhasDaTabela = null;

        void FecharLista()
        {
            itens = null;
            tipoLista = null;
        }

        foreach (var bruta in linhas)
        {
            var linha = (bruta ?? string.Empty).TrimEnd();

            if (linha.StartsWith("- ", StringComparison.Ordinal))
            {
                linhasDaTabela = null;
                if (tipoLista != "bulletList")
                {
                    itens = new JsonArray();
                    conteudo.Add(new JsonObject { ["type"] = "bulletList", ["content"] = itens });
                    tipoLista = "bulletList";
                }
                itens!.Add(Item(linha[2..]));
                continue;
            }

            var numerado = ItemNumerado().Match(linha);
            if (numerado.Success)
            {
                linhasDaTabela = null;
                if (tipoLista != "orderedList")
                {
                    itens = new JsonArray();
                    var lista = new JsonObject { ["type"] = "orderedList", ["content"] = itens };
                    var inicio = int.Parse(numerado.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (inicio != 1) lista["attrs"] = new JsonObject { ["start"] = inicio };
                    conteudo.Add(lista);
                    tipoLista = "orderedList";
                }
                itens!.Add(Item(numerado.Groups[2].Value));
                continue;
            }

            if (linha.StartsWith('|'))
            {
                FecharLista();
                var cabecalho = linhasDaTabela == null;
                if (linhasDaTabela == null)
                {
                    linhasDaTabela = new JsonArray();
                    conteudo.Add(new JsonObject { ["type"] = "table", ["content"] = linhasDaTabela });
                }
                var celulas = linha.Trim().Trim('|').Split('|').Select(c => c.Trim());
                linhasDaTabela.Add(new JsonObject
                {
                    ["type"] = "tableRow",
                    ["content"] = new JsonArray(celulas.Select(c => (JsonNode)new JsonObject
                    {
                        ["type"] = cabecalho ? "tableHeader" : "tableCell",
                        ["content"] = new JsonArray(Paragrafo(c))
                    }).ToArray())
                });
                continue;
            }

            FecharLista();
            linhasDaTabela = null;
            if (linha.StartsWith("#### ", StringComparison.Ordinal)) conteudo.Add(Titulo(4, linha[5..]));
            else if (linha.StartsWith("### ", StringComparison.Ordinal)) conteudo.Add(Titulo(3, linha[4..]));
            else conteudo.Add(Paragrafo(linha));
        }

        return new JsonObject { ["type"] = "doc", ["content"] = conteudo };
    }

    private static JsonObject Item(string texto) =>
        new() { ["type"] = "listItem", ["content"] = new JsonArray(Paragrafo(texto)) };

    private static JsonObject Titulo(int nivel, string texto)
    {
        var no = new JsonObject { ["type"] = "heading", ["attrs"] = new JsonObject { ["level"] = nivel } };
        var linha = EmLinha(texto);
        if (linha.Count > 0) no["content"] = linha;
        return no;
    }

    private static JsonObject Paragrafo(string texto)
    {
        var no = new JsonObject { ["type"] = "paragraph" };
        var linha = EmLinha(texto);
        if (linha.Count > 0) no["content"] = linha;
        return no;
    }

    /// <summary>O texto da linha, com os trechos entre ** em negrito.</summary>
    private static JsonArray EmLinha(string texto)
    {
        var saida = new JsonArray();
        var partes = texto.Split("**");
        for (var i = 0; i < partes.Length; i++)
        {
            if (partes[i].Length == 0) continue;
            var no = new JsonObject { ["type"] = "text", ["text"] = partes[i] };
            if (i % 2 == 1) no["marks"] = new JsonArray(new JsonObject { ["type"] = "bold" });
            saida.Add(no);
        }
        return saida;
    }
}
