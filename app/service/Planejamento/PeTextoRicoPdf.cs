using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json.Nodes;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace service.Planejamento;

/// <summary>
/// Conversor único do texto rico (o JSON do TipTap, na lista fechada do
/// <see cref="PeTextoRico"/>) para o QuestPDF. Serve ao documento do PDTIC (textos do modelo e
/// do órgão, texto rico dos registros) e aos próximos documentos do mesmo motor.
/// <list type="bullet">
/// <item>parágrafo (vazio vira linha em branco), títulos de nível 3 e 4, quebra de linha;</item>
/// <item>negrito, itálico, sublinhado e link (endereço http ou https, clicável no PDF);</item>
/// <item>listas com marcadores e numeradas (início e tipo 1, a, A, i, I), uma dentro da outra;</item>
/// <item>tabela com cabeçalho (as primeiras linhas só de células de cabeçalho repetem em cada
/// página), mesclagem de colunas e de linhas e a largura das colunas do editor;</item>
/// <item>imagem do próprio módulo (api/planejamento/arquivos/{id}), no tamanho do editor, sem
/// passar da largura nem da altura da página; imagem que falta ou não abre vira um aviso.</item>
/// </list>
/// Nó ou marca fora da lista é ignorado (o texto já foi conferido ao gravar).
/// </summary>
public static class PeTextoRicoPdf
{
    public static readonly Color CorTexto = Color.FromHex("#1F2328");
    public static readonly Color CorTitulo = Color.FromHex("#1B365D");
    public static readonly Color CorSuave = Color.FromHex("#5B6570");
    public static readonly Color CorLink = Color.FromHex("#1A56A6");
    public static readonly Color CorBorda = Color.FromHex("#C3CCD5");
    public static readonly Color CorFundoCabecalho = Color.FromHex("#E8EDF2");

    // Pixel do editor (CSS, 96 por polegada) em ponto do PDF (72 por polegada)
    public const float PontosPorPixel = 0.75f;

    /// <summary>Como desenhar: o tamanho da letra, a altura máxima das imagens e de onde vêm as imagens.</summary>
    public sealed class Opcoes
    {
        public float Fonte { get; init; } = 10.5f;

        public float AlturaMaximaImagem { get; init; } = 500f;

        // Parágrafos e títulos centralizados (a capa)
        public bool Centralizar { get; init; }

        // A imagem já aberta pelo id do arquivo (nula quando falta ou não abre)
        public Func<long, PeImagemPdf?> Imagem { get; init; } = _ => null;
    }

    private sealed record Contexto(Opcoes Opcoes, float Fonte, bool Negrito);

    /// <summary>Desenha o documento do TipTap ({ "type": "doc", "content": [...] }) no container.</summary>
    public static void Desenhar(IContainer container, JsonNode? documento, Opcoes opcoes)
    {
        var ctx = new Contexto(opcoes, opcoes.Fonte, false);
        container.Column(col =>
        {
            col.Spacing(opcoes.Fonte * 0.5f);
            Blocos(col, Conteudo(documento), ctx, 0);
        });
    }

    /// <summary>O documento tem algo para desenhar (texto com letra ou imagem).</summary>
    public static bool TemConteudo(JsonNode? documento) => PeTextoRico.TemConteudo(documento);

    // ── Blocos ──────────────────────────────────────────────────────────────

    private static void Blocos(ColumnDescriptor col, JsonArray? nos, Contexto ctx, int nivelLista)
    {
        if (nos == null) return;
        var lista = nos.OfType<JsonObject>().ToList();
        for (var i = 0; i < lista.Count; i++)
        {
            var no = lista[i];
            if (Tipo(no) == "heading" && i + 1 < lista.Count)
            {
                // O título não fica sozinho no pé da página: vai junto com o começo do que vem depois
                var seguinte = lista[i + 1];
                col.Item().EnsureSpace(ctx.Fonte * EspacoDepoisDoTitulo).Column(junto =>
                {
                    junto.Spacing(ctx.Opcoes.Fonte * 0.5f);
                    Bloco(junto, no, ctx, nivelLista);
                    Bloco(junto, seguinte, ctx, nivelLista);
                });
                i++;
                continue;
            }
            Bloco(col, no, ctx, nivelLista);
        }
    }

    // Em linhas de texto: o título e pelo menos umas quatro linhas do que vem depois
    private const float EspacoDepoisDoTitulo = 7f;

    private static void Bloco(ColumnDescriptor col, JsonObject no, Contexto ctx, int nivelLista)
    {
        switch (Tipo(no))
        {
            case "paragraph":
                Paragrafo(col, no, ctx);
                break;
            case "heading":
                Titulo(col.Item(), no, ctx);
                break;
            case "bulletList":
                Lista(col.Item(), no, ctx, nivelLista, numerada: false);
                break;
            case "orderedList":
                Lista(col.Item(), no, ctx, nivelLista, numerada: true);
                break;
            case "table":
                Tabela(col.Item(), no, ctx);
                break;
            case "image":
                Imagem(col.Item(), no, ctx);
                break;
        }
    }

    /// <summary>Parágrafo: o texto em linha; a imagem no meio do parágrafo sai entre dois trechos.</summary>
    private static void Paragrafo(ColumnDescriptor col, JsonObject no, Contexto ctx)
    {
        var filhos = Conteudo(no)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        if (!filhos.Any(f => Tipo(f) == "image" || (Tipo(f) == "text" && !string.IsNullOrEmpty(Texto(f)))))
        {
            // Parágrafo vazio (ou só quebras de linha): a linha em branco que a pessoa deixou
            col.Item().Height(ctx.Fonte * 0.9f);
            return;
        }

        var trecho = new List<JsonObject>();
        void Fechar()
        {
            if (trecho.Count == 0) return;
            var nos = trecho.ToList();
            trecho.Clear();
            col.Item().Text(t =>
            {
                Linha(t, nos, ctx);
                if (ctx.Opcoes.Centralizar) t.AlignCenter();
            });
        }

        foreach (var filho in filhos)
        {
            if (Tipo(filho) == "image")
            {
                Fechar();
                Imagem(col.Item(), filho, ctx);
            }
            else
            {
                trecho.Add(filho);
            }
        }
        Fechar();
    }

    private static void Titulo(IContainer container, JsonObject no, Contexto ctx)
    {
        // Menores que os títulos dos capítulos (15 e 12,5): o título do texto fica dentro do capítulo
        var nivel = Inteiro(Atributo(no, "level")) ?? 3;
        var tamanho = nivel == 3 ? ctx.Fonte + 1f : ctx.Fonte;
        var nos = Conteudo(no)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        container.PaddingTop(ctx.Fonte * 0.4f).Text(t =>
        {
            Linha(t, nos, ctx with { Fonte = tamanho, Negrito = true }, CorTitulo, 1.25f);
            if (ctx.Opcoes.Centralizar) t.AlignCenter();
        });
    }

    // ── Texto em linha ──────────────────────────────────────────────────────

    private static void Linha(TextDescriptor t, IReadOnlyList<JsonObject> nos, Contexto ctx, Color? cor = null, float alturaDaLinha = 1.3f)
    {
        t.DefaultTextStyle(s =>
        {
            var estilo = s.FontSize(ctx.Fonte).LineHeight(alturaDaLinha).FontColor(cor ?? CorTexto);
            return ctx.Negrito ? estilo.Bold() : estilo;
        });

        foreach (var no in nos)
        {
            switch (Tipo(no))
            {
                case "hardBreak":
                    t.Span("\n");
                    break;
                case "text":
                {
                    var texto = Texto(no);
                    if (string.IsNullOrEmpty(texto)) break;
                    var marcas = Marcas(no);
                    var span = marcas.Link != null && PeTextoRico.LinkValido(marcas.Link)
                        ? t.Hyperlink(texto, marcas.Link).FontColor(CorLink).Underline()
                        : t.Span(texto);
                    if (marcas.Negrito) span.Bold();
                    if (marcas.Italico) span.Italic();
                    if (marcas.Sublinhado) span.Underline();
                    break;
                }
            }
        }
    }

    private sealed record MarcasDoTexto(bool Negrito, bool Italico, bool Sublinhado, string? Link);

    private static MarcasDoTexto Marcas(JsonObject no)
    {
        bool negrito = false, italico = false, sublinhado = false;
        string? link = null;
        if (no["marks"] is JsonArray marcas)
        {
            foreach (var marca in marcas.OfType<JsonObject>())
            {
                switch (Tipo(marca))
                {
                    case "bold":
                        negrito = true;
                        break;
                    case "italic":
                        italico = true;
                        break;
                    case "underline":
                        sublinhado = true;
                        break;
                    case "link":
                        link = Texto((marca["attrs"] as JsonObject)?["href"])?.Trim();
                        break;
                }
            }
        }
        return new MarcasDoTexto(negrito, italico, sublinhado, link);
    }

    // ── Listas ──────────────────────────────────────────────────────────────

    private static void Lista(IContainer container, JsonObject no, Contexto ctx, int nivel, bool numerada)
    {
        var itens = Conteudo(no)?.OfType<JsonObject>().Where(i => Tipo(i) == "listItem").ToList() ?? new List<JsonObject>();
        if (itens.Count == 0) return;
        var inicio = numerada ? Math.Max(1, Inteiro(Atributo(no, "start")) ?? 1) : 1;
        var estilo = numerada ? Texto(Atributo(no, "type")) ?? "1" : null;

        container.Column(col =>
        {
            col.Spacing(ctx.Fonte * 0.3f);
            for (var i = 0; i < itens.Count; i++)
            {
                var item = itens[i];
                var marcador = numerada ? Numero(inicio + i, estilo!) + "." : "•";
                col.Item().Row(row =>
                {
                    row.ConstantItem(numerada ? ctx.Fonte * 2.2f : ctx.Fonte * 1.4f)
                        .Text(marcador).FontSize(ctx.Fonte).FontColor(CorTexto);
                    row.RelativeItem().Column(c =>
                    {
                        c.Spacing(ctx.Fonte * 0.3f);
                        Blocos(c, Conteudo(item), ctx, nivel + 1);
                    });
                });
            }
        });
    }

    /// <summary>O número do item no estilo da lista: 1, a, A, i ou I.</summary>
    public static string Numero(int numero, string estilo) => estilo switch
    {
        "a" => Letras(numero),
        "A" => Letras(numero).ToUpperInvariant(),
        "i" => Romano(numero).ToLowerInvariant(),
        "I" => Romano(numero),
        _ => numero.ToString(CultureInfo.InvariantCulture)
    };

    private static string Letras(int numero)
    {
        var saida = string.Empty;
        for (var n = numero; n > 0; n = (n - 1) / 26)
            saida = (char)('a' + (n - 1) % 26) + saida;
        return saida;
    }

    private static string Romano(int numero)
    {
        if (numero is < 1 or > 3999) return numero.ToString(CultureInfo.InvariantCulture);
        var valores = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        var simbolos = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        var saida = new System.Text.StringBuilder();
        for (var i = 0; i < valores.Length; i++)
            while (numero >= valores[i])
            {
                saida.Append(simbolos[i]);
                numero -= valores[i];
            }
        return saida.ToString();
    }

    // ── Tabelas ─────────────────────────────────────────────────────────────

    /// <summary>Uma célula posicionada na grade da tabela (linha e coluna a partir de zero).</summary>
    public sealed record CelulaPosicionada(int Linha, int Coluna, int Linhas, int Colunas, bool Cabecalho, JsonObject No);

    /// <summary>A grade da tabela do TipTap: as células no lugar, as colunas, a largura de cada uma e as linhas de cabeçalho.</summary>
    public sealed record Grade(IReadOnlyList<CelulaPosicionada> Celulas, int Colunas, int LinhasTotal, int LinhasCabecalho, IReadOnlyList<float> Larguras);

    /// <summary>
    /// Posiciona as células como o navegador faz: da esquerda para a direita, pulando o que
    /// uma célula mesclada de uma linha acima já ocupa. A mesclagem que passa do fim da tabela
    /// é cortada. Cabeçalho: as primeiras linhas só com células de cabeçalho (sem mesclagem
    /// que entre no corpo), deixando pelo menos uma linha no corpo.
    /// </summary>
    public static Grade Posicionar(JsonObject tabela)
    {
        var linhas = Conteudo(tabela)?.OfType<JsonObject>().Where(r => Tipo(r) == "tableRow").ToList() ?? new List<JsonObject>();
        var celulas = new List<CelulaPosicionada>();
        var ocupadas = new HashSet<(int, int)>();
        var larguras = new Dictionary<int, float>();

        for (var r = 0; r < linhas.Count; r++)
        {
            var c = 0;
            foreach (var celula in Conteudo(linhas[r])?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            {
                var tipo = Tipo(celula);
                if (tipo is not ("tableCell" or "tableHeader")) continue;
                while (ocupadas.Contains((r, c))) c++;
                var colunas = Math.Clamp(Inteiro(Atributo(celula, "colspan")) ?? 1, 1, 1000);
                var mescladas = Math.Clamp(Inteiro(Atributo(celula, "rowspan")) ?? 1, 1, linhas.Count - r);
                celulas.Add(new CelulaPosicionada(r, c, mescladas, colunas, tipo == "tableHeader", celula));
                for (var dr = 0; dr < mescladas; dr++)
                    for (var dc = 0; dc < colunas; dc++)
                        ocupadas.Add((r + dr, c + dc));

                // Largura do editor (uma por coluna da célula), quando existe
                if (Atributo(celula, "colwidth") is JsonArray lista && lista.Count == colunas)
                    for (var j = 0; j < colunas; j++)
                        if (Inteiro(lista[j]) is int px && px > 0 && !larguras.ContainsKey(c + j))
                            larguras[c + j] = px;
                c += colunas;
            }
        }

        var total = celulas.Count == 0 ? 0 : celulas.Max(x => x.Coluna + x.Colunas);
        var media = larguras.Count == 0 ? 1f : larguras.Values.Average();
        var pesos = Enumerable.Range(0, total).Select(i => larguras.TryGetValue(i, out var px) ? px : media).ToList();

        var cabecalho = 0;
        while (cabecalho < linhas.Count - 1)
        {
            var daLinha = celulas.Where(x => x.Linha == cabecalho).ToList();
            if (daLinha.Count == 0 || daLinha.Any(x => !x.Cabecalho)) break;
            cabecalho++;
        }
        // Mesclagem do cabeçalho que entra no corpo: sem cabeçalho repetido
        if (celulas.Any(x => x.Linha < cabecalho && x.Linha + x.Linhas > cabecalho)) cabecalho = 0;

        return new Grade(celulas, total, linhas.Count, cabecalho, pesos);
    }

    private static void Tabela(IContainer container, JsonObject no, Contexto ctx)
    {
        var grade = Posicionar(no);
        if (grade.Colunas == 0) return;
        var fonte = Math.Max(7f, ctx.Fonte - 1f);

        container.Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                foreach (var peso in grade.Larguras) cd.RelativeColumn(peso);
            });
            if (grade.LinhasCabecalho > 0)
                t.Header(h =>
                {
                    foreach (var celula in grade.Celulas.Where(x => x.Linha < grade.LinhasCabecalho))
                        Celula(h.Cell(), celula, celula.Linha, ctx with { Fonte = fonte });
                });
            foreach (var celula in grade.Celulas.Where(x => x.Linha >= grade.LinhasCabecalho))
                Celula(t.Cell(), celula, celula.Linha - grade.LinhasCabecalho, ctx with { Fonte = fonte });
        });
    }

    private static void Celula(ITableCellContainer celula, CelulaPosicionada posicao, int linha, Contexto ctx)
    {
        var caixa = celula
            .Row((uint)linha + 1).Column((uint)posicao.Coluna + 1)
            .RowSpan((uint)posicao.Linhas).ColumnSpan((uint)posicao.Colunas)
            .Border(0.5f).BorderColor(CorBorda);
        if (posicao.Cabecalho) caixa = caixa.Background(CorFundoCabecalho);
        caixa.Padding(4).Column(col =>
        {
            col.Spacing(ctx.Fonte * 0.3f);
            Blocos(col, Conteudo(posicao.No), ctx with { Negrito = posicao.Cabecalho }, 0);
        });
    }

    // ── Imagens ─────────────────────────────────────────────────────────────

    private static void Imagem(IContainer container, JsonObject no, Contexto ctx)
    {
        var src = Texto(Atributo(no, "src"));
        var alt = Texto(Atributo(no, "alt"));
        var id = src == null ? null : PeTextoRico.IdDaImagem(src);
        var imagem = id == null ? null : ctx.Opcoes.Imagem(id.Value);
        if (imagem == null)
        {
            container.Text(string.IsNullOrWhiteSpace(alt) ? "[Imagem indisponível]" : $"[Imagem indisponível: {alt.Trim()}]")
                .FontSize(ctx.Fonte - 1).Italic().FontColor(CorSuave);
            return;
        }

        // A largura do editor (ou a natural), sem passar da altura máxima nem da largura da página
        var largura = (Inteiro(Atributo(no, "width")) is int px && px > 0 ? px : imagem.Largura) * PontosPorPixel;
        largura = Math.Min(largura, ctx.Opcoes.AlturaMaximaImagem * imagem.Largura / Math.Max(1, imagem.Altura));
        container.AlignCenter().MaxWidth(Math.Max(8f, largura)).Image(imagem.Imagem).FitWidth();
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static JsonArray? Conteudo(JsonNode? no) => (no as JsonObject)?["content"] as JsonArray;

    private static string? Tipo(JsonObject no) => PeRegistroDados.Texto(no["type"]);

    private static string? Texto(JsonObject no) => PeRegistroDados.Texto(no["text"]);

    private static string? Texto(JsonNode? valor) => PeRegistroDados.Texto(valor);

    private static JsonNode? Atributo(JsonObject no, string nome) => (no["attrs"] as JsonObject)?[nome];

    private static int? Inteiro(JsonNode? valor) =>
        PeRegistroDados.Numero(valor) is decimal d && d >= int.MinValue && d <= int.MaxValue ? (int)d : null;
}

/// <summary>
/// Imagem pronta para o PDF: a imagem aberta pelo QuestPDF e o tamanho em pixels, lido do
/// cabeçalho do PNG ou do JPEG (o QuestPDF não expõe o tamanho). Arquivo que não abre (só o
/// começo de um PNG, por exemplo) fica de fora.
/// </summary>
public sealed class PeImagemPdf
{
    public required Image Imagem { get; init; }

    public int Largura { get; init; }

    public int Altura { get; init; }

    /// <summary>A imagem, ou nulo quando o conteúdo não é um PNG ou JPEG que abre.</summary>
    public static PeImagemPdf? Abrir(byte[]? conteudo)
    {
        if (conteudo == null || conteudo.Length == 0) return null;
        var tamanho = Tamanho(conteudo);
        if (tamanho == null) return null;
        try
        {
            return new PeImagemPdf { Imagem = Image.FromBinaryData(conteudo), Largura = tamanho.Value.Largura, Altura = tamanho.Value.Altura };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Largura e altura em pixels pelo cabeçalho do PNG (IHDR) ou do JPEG (marcador SOF).</summary>
    public static (int Largura, int Altura)? Tamanho(byte[] b)
    {
        // PNG: assinatura de 8 bytes, depois o IHDR com largura e altura
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
        {
            var largura = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16, 4));
            var altura = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20, 4));
            return largura > 0 && altura > 0 ? (largura, altura) : null;
        }

        // JPEG: percorre os marcadores até o SOF (C0 a CF, menos C4, C8 e CC)
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                i++;
                continue;
            }
            var marcador = b[i + 1];
            if (marcador == 0xFF)
            {
                i++;
                continue;
            }
            if (marcador is 0xD8 or 0x01 || marcador is >= 0xD0 and <= 0xD7)
            {
                i += 2;
                continue;
            }
            var comprimento = (b[i + 2] << 8) | b[i + 3];
            if (marcador is >= 0xC0 and <= 0xCF && marcador is not (0xC4 or 0xC8 or 0xCC))
            {
                var altura = (b[i + 5] << 8) | b[i + 6];
                var largura = (b[i + 7] << 8) | b[i + 8];
                return largura > 0 && altura > 0 ? (largura, altura) : null;
            }
            if (comprimento < 2) return null;
            i += 2 + comprimento;
        }
        return null;
    }
}
