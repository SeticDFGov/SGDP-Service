using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace test.planejamento;

/// <summary>
/// O texto de cada página de um PDF gerado pelo QuestPDF, só para os testes (F2): cada linha que o
/// PDF escreve (um bloco BT ... ET, com as letras pelo ToUnicode das fontes), página por página.
/// Serve para conferir em que página cada texto saiu (a legenda junto da tabela, achado B-N1) e que
/// uma palavra não quebrou no meio (achado C06). Lê só o que o QuestPDF escreve: objetos soltos,
/// fluxos FlateDecode, fontes Type0 com ToUnicode e texto em hexadecimal (Tj e TJ).
/// </summary>
public static partial class PeTextoDoPdf
{
    [GeneratedRegex(@"(\d+)\s+0\s+obj\b")]
    private static partial Regex InicioDoObjeto();

    [GeneratedRegex(@"/Type\s*/Catalog\b")]
    private static partial Regex Catalogo();

    [GeneratedRegex(@"/Pages\s+(\d+)\s+0\s+R")]
    private static partial Regex ArvoreDePaginas();

    [GeneratedRegex(@"/Kids\s*\[([^\]]*)\]")]
    private static partial Regex Filhos();

    [GeneratedRegex(@"(\d+)\s+0\s+R")]
    private static partial Regex Referencia();

    [GeneratedRegex(@"/Contents\s*(\[[^\]]*\]|\d+\s+0\s+R)")]
    private static partial Regex Conteudos();

    [GeneratedRegex(@"/Font\s*<<(.*?)>>", RegexOptions.Singleline)]
    private static partial Regex Fontes();

    [GeneratedRegex(@"/([A-Za-z0-9_.+-]+)\s+(\d+)\s+0\s+R")]
    private static partial Regex FonteDaPagina();

    [GeneratedRegex(@"/Resources\s+(\d+)\s+0\s+R")]
    private static partial Regex RecursosIndiretos();

    [GeneratedRegex(@"/ToUnicode\s+(\d+)\s+0\s+R")]
    private static partial Regex ParaUnicode();

    [GeneratedRegex(@"\bBT\b(.*?)\bET\b", RegexOptions.Singleline)]
    private static partial Regex BlocoDeTexto();

    [GeneratedRegex(@"/([A-Za-z0-9_.+-]+)\s+[-\d.]+\s+Tf|<([0-9A-Fa-f\s]*)>\s*Tj|\[((?:[^\]]|\\\])*)\]\s*TJ", RegexOptions.Singleline)]
    private static partial Regex OperacaoDeTexto();

    [GeneratedRegex(@"<([0-9A-Fa-f\s]*)>")]
    private static partial Regex Hexadecimal();

    [GeneratedRegex(@"beginbfchar(.*?)endbfchar", RegexOptions.Singleline)]
    private static partial Regex BlocoBfChar();

    [GeneratedRegex(@"beginbfrange(.*?)endbfrange", RegexOptions.Singleline)]
    private static partial Regex BlocoBfRange();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")]
    private static partial Regex ParBfChar();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(<[0-9A-Fa-f]+>|\[[^\]]*\])")]
    private static partial Regex FaixaBfRange();

    private sealed record Objeto(string Dicionario, byte[]? Fluxo);

    /// <summary>As linhas de texto de cada página, na ordem das páginas e na ordem em que o PDF as escreve.</summary>
    public static List<List<string>> Paginas(byte[] pdf)
    {
        var objetos = Objetos(pdf);
        var catalogo = objetos.Values.First(o => Catalogo().IsMatch(o.Dicionario)).Dicionario;
        var raiz = int.Parse(ArvoreDePaginas().Match(catalogo).Groups[1].Value, CultureInfo.InvariantCulture);
        var paginas = new List<int>();
        void Visitar(int numero)
        {
            var dicionario = objetos[numero].Dicionario;
            var filhos = Filhos().Match(dicionario);
            if (Regex.IsMatch(dicionario, @"/Type\s*/Pages\b") && filhos.Success)
            {
                foreach (Match f in Referencia().Matches(filhos.Groups[1].Value))
                    Visitar(int.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture));
                return;
            }
            paginas.Add(numero);
        }
        Visitar(raiz);

        var mapas = new Dictionary<int, Dictionary<string, string>>();
        return paginas.Select(p => Linhas(objetos, objetos[p].Dicionario, mapas)).ToList();
    }

    /// <summary>A página (a partir de 1) da primeira linha que contém o texto, ou 0.</summary>
    public static int PaginaCom(List<List<string>> paginas, string texto)
    {
        for (var i = 0; i < paginas.Count; i++)
            if (paginas[i].Any(l => l.Contains(texto, StringComparison.Ordinal)))
                return i + 1;
        return 0;
    }

    private static List<string> Linhas(Dictionary<int, Objeto> objetos, string pagina, Dictionary<int, Dictionary<string, string>> mapas)
    {
        var recursos = pagina;
        if (RecursosIndiretos().Match(pagina) is { Success: true } indiretos)
            recursos = objetos[int.Parse(indiretos.Groups[1].Value, CultureInfo.InvariantCulture)].Dicionario;
        var fontes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (Fontes().Match(recursos) is { Success: true } lista)
            foreach (Match f in FonteDaPagina().Matches(lista.Groups[1].Value))
            {
                var numero = int.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture);
                if (!mapas.TryGetValue(numero, out var mapa))
                {
                    mapa = ParaUnicode().Match(objetos[numero].Dicionario) is { Success: true } cmap
                        ? Mapa(Encoding.Latin1.GetString(objetos[int.Parse(cmap.Groups[1].Value, CultureInfo.InvariantCulture)].Fluxo ?? Array.Empty<byte>()))
                        : new Dictionary<string, string>();
                    mapas[numero] = mapa;
                }
                fontes[f.Groups[1].Value] = mapa;
            }

        var conteudo = new StringBuilder();
        var referencias = Conteudos().Match(pagina);
        if (referencias.Success)
            foreach (Match r in Referencia().Matches(referencias.Groups[1].Value))
                conteudo.Append(Encoding.Latin1.GetString(objetos[int.Parse(r.Groups[1].Value, CultureInfo.InvariantCulture)].Fluxo ?? Array.Empty<byte>())).Append('\n');

        var linhas = new List<string>();
        foreach (Match bloco in BlocoDeTexto().Matches(conteudo.ToString()))
        {
            Dictionary<string, string>? atual = null;
            var linha = new StringBuilder();
            foreach (Match op in OperacaoDeTexto().Matches(bloco.Groups[1].Value))
            {
                if (op.Groups[1].Success)
                {
                    atual = fontes.GetValueOrDefault(op.Groups[1].Value);
                    continue;
                }
                var hexas = op.Groups[2].Success
                    ? new[] { op.Groups[2].Value }
                    : Hexadecimal().Matches(op.Groups[3].Value).Select(m => m.Groups[1].Value).ToArray();
                foreach (var hexa in hexas) linha.Append(Decodificar(hexa, atual));
            }
            if (linha.Length > 0) linhas.Add(linha.ToString());
        }
        return linhas;
    }

    // Os códigos de dois bytes (Identity-H) pelo mapa ToUnicode da fonte
    private static string Decodificar(string hexa, Dictionary<string, string>? mapa)
    {
        var limpo = Regex.Replace(hexa, @"\s", string.Empty).ToUpperInvariant();
        var saida = new StringBuilder();
        for (var i = 0; i + 4 <= limpo.Length; i += 4)
        {
            var codigo = limpo.Substring(i, 4);
            saida.Append(mapa != null && mapa.TryGetValue(codigo, out var letra) ? letra : "?");
        }
        return saida.ToString();
    }

    private static Dictionary<string, string> Mapa(string cmap)
    {
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match bloco in BlocoBfChar().Matches(cmap))
            foreach (Match par in ParBfChar().Matches(bloco.Groups[1].Value))
                mapa[par.Groups[1].Value.ToUpperInvariant().PadLeft(4, '0')] = Utf16(par.Groups[2].Value);
        foreach (Match bloco in BlocoBfRange().Matches(cmap))
            foreach (Match faixa in FaixaBfRange().Matches(bloco.Groups[1].Value))
            {
                var inicio = Convert.ToInt32(faixa.Groups[1].Value, 16);
                var fim = Convert.ToInt32(faixa.Groups[2].Value, 16);
                var destino = faixa.Groups[3].Value;
                if (destino.StartsWith('['))
                {
                    var lista = Hexadecimal().Matches(destino).Select(m => m.Groups[1].Value).ToList();
                    for (var c = inicio; c <= fim && c - inicio < lista.Count; c++)
                        mapa[c.ToString("X4", CultureInfo.InvariantCulture)] = Utf16(lista[c - inicio]);
                    continue;
                }
                var primeiro = Convert.ToInt32(destino.Trim('<', '>'), 16);
                for (var c = inicio; c <= fim; c++)
                    mapa[c.ToString("X4", CultureInfo.InvariantCulture)] = char.ConvertFromUtf32(primeiro + (c - inicio));
            }
        return mapa;
    }

    private static string Utf16(string hexa)
    {
        var bytes = Convert.FromHexString(hexa.Length % 2 == 0 ? hexa : "0" + hexa);
        return Encoding.BigEndianUnicode.GetString(bytes);
    }

    // Os objetos do PDF: o dicionário (o texto antes de "stream") e o fluxo, já descomprimido
    private static Dictionary<int, Objeto> Objetos(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        var objetos = new Dictionary<int, Objeto>();
        // O que parece um "N 0 obj" dentro de um objeto já lido (bytes de um fluxo) não conta
        var lido = 0;
        foreach (Match inicio in InicioDoObjeto().Matches(texto))
        {
            if (inicio.Index < lido) continue;
            var numero = int.Parse(inicio.Groups[1].Value, CultureInfo.InvariantCulture);
            var comeco = inicio.Index + inicio.Length;
            var fim = texto.IndexOf("endobj", comeco, StringComparison.Ordinal);
            if (fim < 0) continue;
            var marca = texto.IndexOf("stream", comeco, StringComparison.Ordinal);
            if (marca < 0 || marca > fim)
            {
                objetos[numero] = new Objeto(texto[comeco..fim], null);
                lido = fim;
                continue;
            }

            // O fluxo: o tamanho pelo /Length (os bytes dele podem ter qualquer coisa)
            var dicionario = texto[comeco..marca];
            var dados = marca + "stream".Length;
            if (texto[dados] == '\r') dados++;
            if (texto[dados] == '\n') dados++;
            var tamanho = Regex.Match(dicionario, @"/Length\s+(\d+)\b(?!\s+\d+\s+R)");
            var fimDosDados = tamanho.Success
                ? dados + int.Parse(tamanho.Groups[1].Value, CultureInfo.InvariantCulture)
                : texto.IndexOf("endstream", dados, StringComparison.Ordinal);
            var bruto = pdf.AsSpan(dados, fimDosDados - dados).ToArray();
            objetos[numero] = new Objeto(dicionario, dicionario.Contains("/FlateDecode", StringComparison.Ordinal) ? Descomprimir(bruto) : bruto);
            lido = texto.IndexOf("endobj", fimDosDados, StringComparison.Ordinal);
            if (lido < 0) break;
        }
        return objetos;
    }

    private static byte[] Descomprimir(byte[] dados)
    {
        using var entrada = new MemoryStream(dados);
        using var zlib = new ZLibStream(entrada, CompressionMode.Decompress);
        using var saida = new MemoryStream();
        zlib.CopyTo(saida);
        return saida.ToArray();
    }
}
