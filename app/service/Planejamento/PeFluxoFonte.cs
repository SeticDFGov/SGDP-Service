using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace service.Planejamento;

/// <summary>
/// A fonte do desenho dos fluxos: a Lato (regular e negrito) que o QuestPDF já traz na pasta
/// LatoFont da aplicação (licença OFL). O QuestPDF desenha SVG, mas não desenha o elemento
/// &lt;text&gt; do SVG (no Linux não há fonte para ele); por isso o texto do desenho vira contorno:
/// cada letra é um &lt;path&gt; definido uma vez (&lt;defs&gt;) e reusado com &lt;use xlink:href&gt;, e o
/// mesmo SVG sai igual no navegador e no PDF. As larguras vêm da própria fonte, então a quebra
/// das linhas é exata. Leitura mínima do TrueType: cmap (formatos 4 e 12), hmtx, loca e glyf
/// (com os glifos compostos das letras acentuadas); o kerning não é usado.
/// Sem os arquivos da fonte, <see cref="Carregar"/> devolve nulo e o desenho usa &lt;text&gt; com
/// larguras estimadas (o navegador desenha; o PDF sai sem o texto).
/// </summary>
public sealed class PeFluxoFonte
{
    public const string ArquivoRegular = "Lato-Regular.ttf";
    public const string ArquivoNegrito = "Lato-Bold.ttf";

    private static readonly Lazy<(PeFluxoFonte? Regular, PeFluxoFonte? Negrito)> Padrao = new(() =>
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "LatoFont");
        return (Carregar(Path.Combine(pasta, ArquivoRegular)), Carregar(Path.Combine(pasta, ArquivoNegrito)));
    });

    /// <summary>A Lato regular da aplicação (nula sem o arquivo).</summary>
    public static PeFluxoFonte? Regular => Padrao.Value.Regular;

    /// <summary>A Lato em negrito (sem ela, vale a regular).</summary>
    public static PeFluxoFonte? Negrito => Padrao.Value.Negrito ?? Padrao.Value.Regular;

    private readonly byte[] _dados;
    private readonly Dictionary<string, (int Inicio, int Tamanho)> _tabelas = new();
    private readonly Dictionary<int, int> _mapa = new();
    private readonly ConcurrentDictionary<int, string> _caminhos = new();

    public int UnidadesPorEm { get; private set; }

    public int Ascendente { get; private set; }

    public int Descendente { get; private set; }

    private int _glifos;
    private int _metricas;
    private bool _locaLongo;

    private PeFluxoFonte(byte[] dados)
    {
        _dados = dados;
    }

    /// <summary>Abre um arquivo TrueType; nulo quando não existe ou não dá para ler.</summary>
    public static PeFluxoFonte? Carregar(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? Ler(File.ReadAllBytes(caminho)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or IndexOutOfRangeException or ArgumentException)
        {
            return null;
        }
    }

    public static PeFluxoFonte Ler(byte[] dados)
    {
        var fonte = new PeFluxoFonte(dados);
        var tabelas = fonte.U16(4);
        for (var i = 0; i < tabelas; i++)
        {
            var registro = 12 + i * 16;
            var nome = Encoding.ASCII.GetString(dados, registro, 4);
            fonte._tabelas[nome] = ((int)fonte.U32(registro + 8), (int)fonte.U32(registro + 12));
        }
        foreach (var obrigatoria in new[] { "head", "hhea", "hmtx", "maxp", "cmap", "loca", "glyf" })
            if (!fonte._tabelas.ContainsKey(obrigatoria)) throw new InvalidDataException($"Fonte sem a tabela {obrigatoria}.");

        var head = fonte._tabelas["head"].Inicio;
        fonte.UnidadesPorEm = fonte.U16(head + 18);
        fonte._locaLongo = fonte.S16(head + 50) != 0;
        fonte._glifos = fonte.U16(fonte._tabelas["maxp"].Inicio + 4);
        var hhea = fonte._tabelas["hhea"].Inicio;
        fonte.Ascendente = fonte.S16(hhea + 4);
        fonte.Descendente = fonte.S16(hhea + 6);
        fonte._metricas = fonte.U16(hhea + 34);
        fonte.LerMapa();
        if (fonte.UnidadesPorEm <= 0 || fonte._mapa.Count == 0) throw new InvalidDataException("Fonte sem mapa de caracteres.");
        return fonte;
    }

    // ── Medidas ─────────────────────────────────────────────────────────────

    /// <summary>O glifo do caractere (0, o "não existe", quando a fonte não tem).</summary>
    public int Glifo(char caractere) => _mapa.TryGetValue(caractere, out var g) ? g : 0;

    /// <summary>O avanço do glifo, em unidades da fonte.</summary>
    public int Avanco(int glifo)
    {
        if (glifo < 0 || glifo >= _glifos) return 0;
        var hmtx = _tabelas["hmtx"].Inicio;
        return U16(hmtx + Math.Min(glifo, _metricas - 1) * 4);
    }

    /// <summary>A largura do texto num tamanho (em unidades do desenho).</summary>
    public double Largura(string texto, double tamanho)
    {
        long soma = 0;
        foreach (var c in texto) soma += Avanco(Glifo(c));
        return soma * tamanho / UnidadesPorEm;
    }

    // ── Contornos ───────────────────────────────────────────────────────────

    /// <summary>
    /// O contorno do glifo como dados de &lt;path&gt;, em unidades da fonte com o y para baixo
    /// (a origem na linha de base), com comandos relativos para ficar curto. Vazio no espaço.
    /// </summary>
    public string Caminho(int glifo) => _caminhos.GetOrAdd(glifo, g =>
    {
        var contornos = new List<List<(double X, double Y, bool NaCurva)>>();
        Contornos(g, new double[] { 1, 0, 0, 1, 0, 0 }, contornos, 0);
        return DadosDoCaminho(contornos);
    });

    private static string DadosDoCaminho(List<List<(double X, double Y, bool NaCurva)>> contornos)
    {
        var sb = new StringBuilder();
        var cx = 0L;
        var cy = 0L;
        void Ponto(char comando, (long X, long Y) p)
        {
            sb.Append(comando);
            Numero(sb, p.X - cx);
            Numero(sb, p.Y - cy, true);
            cx = p.X;
            cy = p.Y;
        }
        void Curva((long X, long Y) controle, (long X, long Y) fim)
        {
            sb.Append('q');
            Numero(sb, controle.X - cx);
            Numero(sb, controle.Y - cy, true);
            Numero(sb, fim.X - cx, true);
            Numero(sb, fim.Y - cy, true);
            cx = fim.X;
            cy = fim.Y;
        }
        static (long X, long Y) Arredondar(double x, double y) => ((long)Math.Round(x), (long)Math.Round(-y));

        foreach (var contorno in contornos)
        {
            var n = contorno.Count;
            if (n < 2) continue;
            var primeiro = contorno.FindIndex(p => p.NaCurva);
            (double X, double Y) inicio;
            if (primeiro < 0)
            {
                // Só pontos de controle: começa no meio dos dois primeiros
                inicio = ((contorno[0].X + contorno[1].X) / 2, (contorno[0].Y + contorno[1].Y) / 2);
                primeiro = 0;
            }
            else
            {
                inicio = (contorno[primeiro].X, contorno[primeiro].Y);
            }

            // O "m" de cada contorno depois do primeiro é relativo ao começo do contorno anterior
            // (o ponto corrente depois do "z")
            Ponto(sb.Length == 0 ? 'M' : 'm', Arredondar(inicio.X, inicio.Y));
            (double X, double Y)? controle = null;
            for (var k = 1; k <= n; k++)
            {
                var p = contorno[(primeiro + k) % n];
                if (p.NaCurva)
                {
                    if (controle is { } c) Curva(Arredondar(c.X, c.Y), Arredondar(p.X, p.Y));
                    else Ponto('l', Arredondar(p.X, p.Y));
                    controle = null;
                }
                else
                {
                    if (controle is { } c) Curva(Arredondar(c.X, c.Y), Arredondar((c.X + p.X) / 2, (c.Y + p.Y) / 2));
                    controle = (p.X, p.Y);
                }
            }
            if (controle is { } ultimo) Curva(Arredondar(ultimo.X, ultimo.Y), Arredondar(inicio.X, inicio.Y));
            sb.Append('z');
        }
        return sb.ToString();
    }

    private static void Numero(StringBuilder sb, long valor, bool separar = false)
    {
        // Sinal negativo já separa; positivo depois de outro número precisa de espaço
        if (separar && valor >= 0) sb.Append(' ');
        sb.Append(valor.ToString(CultureInfo.InvariantCulture));
    }

    private (int Inicio, int Tamanho) Local(int glifo)
    {
        if (glifo < 0 || glifo >= _glifos) return (0, 0);
        var loca = _tabelas["loca"].Inicio;
        var glyf = _tabelas["glyf"].Inicio;
        int a, b;
        if (_locaLongo)
        {
            a = (int)U32(loca + glifo * 4);
            b = (int)U32(loca + (glifo + 1) * 4);
        }
        else
        {
            a = U16(loca + glifo * 2) * 2;
            b = U16(loca + (glifo + 1) * 2) * 2;
        }
        return (glyf + a, b - a);
    }

    /// <summary>Os contornos do glifo (pontos na curva e de controle), com a transformação dos compostos.</summary>
    private void Contornos(int glifo, double[] m, List<List<(double X, double Y, bool NaCurva)>> saida, int profundidade)
    {
        var (o, tamanho) = Local(glifo);
        if (tamanho <= 0 || profundidade > 8) return;
        var contornos = S16(o);
        if (contornos >= 0)
        {
            var fins = new int[contornos];
            for (var i = 0; i < contornos; i++) fins[i] = U16(o + 10 + i * 2);
            var pontos = contornos == 0 ? 0 : fins[contornos - 1] + 1;
            var p = o + 10 + contornos * 2;
            p += 2 + U16(p);
            var flags = new byte[pontos];
            for (var i = 0; i < pontos;)
            {
                var f = _dados[p++];
                flags[i++] = f;
                if ((f & 8) == 0) continue;
                var repetir = _dados[p++];
                for (var r = 0; r < repetir && i < pontos; r++) flags[i++] = f;
            }
            var xs = new int[pontos];
            var ys = new int[pontos];
            var v = 0;
            for (var i = 0; i < pontos; i++)
            {
                var f = flags[i];
                if ((f & 2) != 0) v += (f & 16) != 0 ? _dados[p++] : -_dados[p++];
                else if ((f & 16) == 0)
                {
                    v += S16(p);
                    p += 2;
                }
                xs[i] = v;
            }
            v = 0;
            for (var i = 0; i < pontos; i++)
            {
                var f = flags[i];
                if ((f & 4) != 0) v += (f & 32) != 0 ? _dados[p++] : -_dados[p++];
                else if ((f & 32) == 0)
                {
                    v += S16(p);
                    p += 2;
                }
                ys[i] = v;
            }
            var inicio = 0;
            foreach (var fim in fins)
            {
                var contorno = new List<(double, double, bool)>();
                for (var i = inicio; i <= fim && i < pontos; i++)
                    contorno.Add((m[0] * xs[i] + m[2] * ys[i] + m[4], m[1] * xs[i] + m[3] * ys[i] + m[5], (flags[i] & 1) != 0));
                saida.Add(contorno);
                inicio = fim + 1;
            }
            return;
        }

        // Composto: cada componente com o deslocamento e a escala dele
        var q = o + 10;
        while (true)
        {
            var flags = U16(q);
            var componente = U16(q + 2);
            q += 4;
            double dx, dy;
            if ((flags & 1) != 0)
            {
                dx = S16(q);
                dy = S16(q + 2);
                q += 4;
            }
            else
            {
                dx = (sbyte)_dados[q];
                dy = (sbyte)_dados[q + 1];
                q += 2;
            }
            double a = 1, b = 0, c = 0, d = 1;
            if ((flags & 8) != 0)
            {
                a = d = F2Dot14(q);
                q += 2;
            }
            else if ((flags & 0x40) != 0)
            {
                a = F2Dot14(q);
                d = F2Dot14(q + 2);
                q += 4;
            }
            else if ((flags & 0x80) != 0)
            {
                a = F2Dot14(q);
                b = F2Dot14(q + 2);
                c = F2Dot14(q + 4);
                d = F2Dot14(q + 6);
                q += 8;
            }
            var composta = new[]
            {
                m[0] * a + m[2] * b, m[1] * a + m[3] * b,
                m[0] * c + m[2] * d, m[1] * c + m[3] * d,
                m[0] * dx + m[2] * dy + m[4], m[1] * dx + m[3] * dy + m[5]
            };
            Contornos(componente, composta, saida, profundidade + 1);
            if ((flags & 0x20) == 0) break;
        }
    }

    private void LerMapa()
    {
        var cmap = _tabelas["cmap"].Inicio;
        var subtabelas = U16(cmap + 2);
        int escolhida = -1, formato = 0;
        for (var i = 0; i < subtabelas; i++)
        {
            var plataforma = U16(cmap + 4 + i * 8);
            var codificacao = U16(cmap + 6 + i * 8);
            var inicio = cmap + (int)U32(cmap + 8 + i * 8);
            var f = U16(inicio);
            var unicode = plataforma == 0 || (plataforma == 3 && codificacao is 1 or 10);
            if (!unicode) continue;
            if (f == 12)
            {
                escolhida = inicio;
                formato = 12;
            }
            else if (f == 4 && formato != 12)
            {
                escolhida = inicio;
                formato = 4;
            }
        }
        if (formato == 12)
        {
            var grupos = (int)U32(escolhida + 12);
            for (var g = 0; g < grupos; g++)
            {
                var o = escolhida + 16 + g * 12;
                var ini = (int)U32(o);
                var fim = (int)Math.Min(U32(o + 4), 0xFFFF);
                var glifo = (int)U32(o + 8);
                for (var cp = ini; cp <= fim; cp++) _mapa[cp] = glifo + (cp - ini);
            }
            return;
        }
        if (formato != 4) return;
        var segmentos = U16(escolhida + 6) / 2;
        var fins = escolhida + 14;
        var inicios = fins + segmentos * 2 + 2;
        var deltas = inicios + segmentos * 2;
        var deslocamentos = deltas + segmentos * 2;
        for (var s = 0; s < segmentos; s++)
        {
            int fim = U16(fins + s * 2), ini = U16(inicios + s * 2), delta = S16(deltas + s * 2), desl = U16(deslocamentos + s * 2);
            for (var cp = ini; cp <= fim && cp != 0xFFFF; cp++)
            {
                int glifo;
                if (desl == 0) glifo = (cp + delta) & 0xFFFF;
                else
                {
                    glifo = U16(deslocamentos + s * 2 + desl + (cp - ini) * 2);
                    if (glifo != 0) glifo = (glifo + delta) & 0xFFFF;
                }
                _mapa[cp] = glifo;
            }
        }
    }

    private int U16(int o) => (_dados[o] << 8) | _dados[o + 1];

    private int S16(int o) => (short)U16(o);

    private uint U32(int o) => (uint)((_dados[o] << 24) | (_dados[o + 1] << 16) | (_dados[o + 2] << 8) | _dados[o + 3]);

    private double F2Dot14(int o) => S16(o) / 16384.0;

    // ── Sem a fonte ─────────────────────────────────────────────────────────

    /// <summary>Largura estimada (sem os arquivos da fonte): larguras médias da Lato por grupo de letra.</summary>
    public static double LarguraEstimada(string texto, double tamanho, bool negrito)
    {
        double soma = 0;
        foreach (var c in texto)
        {
            soma += c switch
            {
                ' ' => 0.26,
                'i' or 'l' or 'j' or 'í' or 'ì' or '.' or ',' or ':' or ';' or '!' or '|' or '\'' => 0.25,
                'f' or 't' or 'r' or '(' or ')' or '[' or ']' or '-' => 0.36,
                'm' or 'w' => 0.82,
                'M' or 'W' => 0.9,
                _ when char.IsUpper(c) => 0.66,
                _ when char.IsDigit(c) => 0.58,
                _ => 0.52
            };
        }
        return soma * tamanho * (negrito ? 1.04 : 1.0);
    }
}
