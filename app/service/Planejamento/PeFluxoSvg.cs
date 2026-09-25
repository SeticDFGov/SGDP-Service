using System.Globalization;
using System.Security;
using System.Text;
using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O SVG de um fluxo desenhado a partir da geometria do desenho automático (E9): tudo o que o
/// SVG mostra (raias, formas, ligações, setas, artefatos e textos) sai de
/// <see cref="PeFluxoGeometria"/>, a mesma que o editor visual recebe. Assim a tela e o PDF
/// nunca divergem: só existe uma função de layout (<see cref="PeFluxoDesenho"/>), e este
/// escritor não calcula posição nenhuma, só lê as da geometria e aplica o estilo (cores,
/// espessuras, setas e os glifos da fonte).
/// <para>
/// O texto sai em contorno (os glifos da Lato em &lt;defs&gt;, reusados com &lt;use&gt;), como na
/// E6: o SVG do QuestPDF no Linux não desenha &lt;text&gt;. Sem a fonte, sai &lt;text&gt; comum.
/// </para>
/// </summary>
public static class PeFluxoSvg
{
    private const string CorLinha = "#374151";
    private const string CorBorda = "#6B7280";
    private const string CorFaixaPool = "#ECEEF2";
    private const string CorFaixaRaia = "#F5F6F8";
    private const double DocDobra = 7;

    private static string N(double v) => Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Esc(string texto) => SecurityElement.Escape(texto) ?? string.Empty;

    /// <summary>
    /// Escreve o SVG da geometria. Com a fonte, o texto vira contorno; sem ela (nula), sai
    /// &lt;text&gt; comum. O negrito nulo usa a regular.
    /// </summary>
    public static string Escrever(PeFluxoGeometria g, PeFluxoFonte? regular, PeFluxoFonte? negrito)
    {
        negrito ??= regular;
        var sb = new StringBuilder();
        var titulo = g.Titulo.Length > 0 ? g.Titulo : "Fluxo";
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"")
            .Append($" viewBox=\"0 0 {N(g.Largura)} {N(g.Altura)}\" width=\"{N(g.Largura)}\" height=\"{N(g.Altura)}\"")
            .Append($" role=\"img\" aria-label=\"{Esc(titulo)}\">");
        sb.Append("<title>").Append(Esc(titulo)).Append("</title>");
        sb.Append("<desc>").Append(Esc(string.Join(" ", g.Descricao))).Append("</desc>");

        // Os glifos usados (uma vez cada)
        var usados = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (regular != null)
            foreach (var t in g.Textos)
            {
                var fonte = t.Negrito ? negrito! : regular;
                var prefixo = t.Negrito ? "b" : "r";
                foreach (var c in t.Texto)
                {
                    var glifo = fonte.Glifo(c);
                    var id = $"pe-g{prefixo}{glifo}";
                    if (usados.ContainsKey(id)) continue;
                    usados[id] = fonte.Caminho(glifo);
                }
            }
        if (usados.Count > 0)
        {
            sb.Append("<defs>");
            foreach (var (id, d) in usados.Where(x => x.Value.Length > 0)) sb.Append($"<path id=\"{id}\" d=\"{d}\"/>");
            sb.Append("</defs>");
        }

        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{N(g.Largura)}\" height=\"{N(g.Altura)}\" fill=\"#FFFFFF\"/>");

        // Raias e faixas (a moldura fica na margem do desenho)
        var topo = PeFluxoDesenho.Margem;
        var baseTotal = g.Altura - PeFluxoDesenho.Margem;
        var esquerda = PeFluxoDesenho.Margem;
        var direita = g.Largura - PeFluxoDesenho.Margem;
        sb.Append("<g stroke=\"").Append(CorBorda).Append("\" stroke-width=\"1\">");
        if (g.FaixaTitulo is { } faixa)
            sb.Append($"<rect x=\"{N(faixa.X)}\" y=\"{N(faixa.Y)}\" width=\"{N(faixa.Largura)}\" height=\"{N(faixa.Altura)}\" fill=\"{CorFaixaPool}\"/>");
        foreach (var r in g.Raias)
        {
            sb.Append($"<rect x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.LarguraCabecalho)}\" height=\"{N(r.Altura)}\" fill=\"{CorFaixaRaia}\"/>");
            sb.Append($"<rect x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.Largura)}\" height=\"{N(r.Altura)}\" fill=\"none\"/>");
        }
        sb.Append($"<rect x=\"{N(esquerda)}\" y=\"{N(topo)}\" width=\"{N(direita - esquerda)}\" height=\"{N(baseTotal - topo)}\" fill=\"none\" stroke-width=\"1.2\"/>");
        sb.Append("</g>");

        // Ligações (linhas e setas)
        sb.Append($"<g fill=\"none\" stroke=\"{CorLinha}\" stroke-width=\"1.3\" stroke-linejoin=\"round\">");
        foreach (var l in g.Ligacoes)
        {
            if (l.Pontos.Count < 2) continue;
            var d = new StringBuilder();
            d.Append('M').Append(N(l.Pontos[0].X)).Append(' ').Append(N(l.Pontos[0].Y));
            for (var i = 1; i < l.Pontos.Count; i++)
            {
                // A linha para dentro da seta (a ponta fica fina)
                double x = l.Pontos[i].X, y = l.Pontos[i].Y;
                if (i == l.Pontos.Count - 1)
                {
                    var anterior = l.Pontos[i - 1];
                    var dx = x - anterior.X;
                    var dy = y - anterior.Y;
                    var tamanho = Math.Sqrt(dx * dx + dy * dy);
                    if (tamanho > 5)
                    {
                        x -= dx / tamanho * 4;
                        y -= dy / tamanho * 4;
                    }
                }
                d.Append('L').Append(N(x)).Append(' ').Append(N(y));
            }
            sb.Append($"<path d=\"{d}\"/>");
        }
        sb.Append("</g>");
        sb.Append($"<g fill=\"{CorLinha}\">");
        foreach (var l in g.Ligacoes.Where(l => l.Pontos.Count >= 2)) sb.Append(Seta(l.Pontos[^2], l.Pontos[^1], 7.5, 3.6));
        sb.Append("</g>");

        // Artefatos: conector pontilhado e documento com a ponta dobrada
        if (g.Artefatos.Count > 0)
        {
            sb.Append($"<g fill=\"none\" stroke=\"{CorBorda}\" stroke-width=\"1.1\" stroke-dasharray=\"1.2 2.6\" stroke-linecap=\"round\">");
            foreach (var a in g.Artefatos)
                sb.Append("<path d=\"M").Append(string.Join(" L", a.Conector.Select(p => $"{N(p.X)} {N(p.Y)}"))).Append("\"/>");
            sb.Append("</g>");
            sb.Append($"<g fill=\"none\" stroke=\"{CorBorda}\" stroke-width=\"1.1\">");
            foreach (var a in g.Artefatos)
            {
                var p = a.Conector[^1];
                sb.Append($"<path d=\"M{N(p.X - 3.2)} {N(p.Y - 4.5)}L{N(p.X)} {N(p.Y)}L{N(p.X + 3.2)} {N(p.Y - 4.5)}\"/>");
            }
            sb.Append("</g>");
            foreach (var a in g.Artefatos)
            {
                var d = new PeFluxoDesenho.Retangulo(a.X, a.Y, a.Largura, a.Altura);
                sb.Append($"<path d=\"M{N(d.X)} {N(d.Y)}L{N(d.Direita - DocDobra)} {N(d.Y)}L{N(d.Direita)} {N(d.Y + DocDobra)}L{N(d.Direita)} {N(d.Base)}L{N(d.X)} {N(d.Base)}Z\" fill=\"#F3F4F6\" stroke=\"{CorBorda}\" stroke-width=\"1\"/>");
                sb.Append($"<path d=\"M{N(d.Direita - DocDobra)} {N(d.Y)}L{N(d.Direita - DocDobra)} {N(d.Y + DocDobra)}L{N(d.Direita)} {N(d.Y + DocDobra)}\" fill=\"#E5E7EB\" stroke=\"{CorBorda}\" stroke-width=\"1\"/>");
            }
        }

        // Elementos (a ligação com outro fluxo que recebe ligação é a de saída: a seta cheia)
        var recebem = g.Ligacoes.Select(l => l.Para).ToHashSet(StringComparer.Ordinal);
        foreach (var e in g.Elementos) sb.Append(Forma(e, g.Cor, recebem.Contains(e.Id)));

        // Fundo branco dos rótulos das ligações (a linha passa por baixo)
        foreach (var l in g.Ligacoes.Where(l => l.RotuloArea != null))
        {
            var r = l.RotuloArea!;
            sb.Append($"<rect x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.Largura)}\" height=\"{N(r.Altura)}\" fill=\"#FFFFFF\" fill-opacity=\"0.9\"/>");
        }

        // Textos
        foreach (var t in g.Textos) sb.Append(Texto(t, t.Negrito ? negrito : regular));

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Seta(PeFluxoGeometriaPonto de, PeFluxoGeometriaPonto para, double comprimento, double meia)
    {
        var dx = para.X - de.X;
        var dy = para.Y - de.Y;
        var tamanho = Math.Sqrt(dx * dx + dy * dy);
        if (tamanho < 0.01) return string.Empty;
        var ux = dx / tamanho;
        var uy = dy / tamanho;
        var bx = para.X - ux * comprimento;
        var by = para.Y - uy * comprimento;
        return $"<path d=\"M{N(para.X)} {N(para.Y)}L{N(bx - uy * meia)} {N(by + ux * meia)}L{N(bx + uy * meia)} {N(by - ux * meia)}Z\"/>";
    }

    private static string Forma(PeFluxoGeometriaElemento e, PeFluxoGeometriaCor cor, bool recebeLigacao)
    {
        var a = new PeFluxoDesenho.Retangulo(e.X, e.Y, e.Largura, e.Altura);
        switch (e.Tipo)
        {
            case PeDominios.TipoElementoFluxo.Tarefa:
            case PeDominios.TipoElementoFluxo.Subprocesso:
            {
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"{N(a.X)}\" y=\"{N(a.Y)}\" width=\"{N(a.L)}\" height=\"{N(a.A)}\" rx=\"9\" ry=\"9\" fill=\"{cor.Fundo}\" stroke=\"{cor.Borda}\" stroke-width=\"1.4\"/>");
                if (e.Tipo == PeDominios.TipoElementoFluxo.Subprocesso)
                {
                    var q = 11.0;
                    var x = a.Cx - q / 2;
                    var y = a.Base - q - 3;
                    sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(q)}\" height=\"{N(q)}\" fill=\"#FFFFFF\" stroke=\"{cor.Borda}\" stroke-width=\"1.1\"/>");
                    sb.Append($"<path d=\"M{N(a.Cx)} {N(y + 2.4)}L{N(a.Cx)} {N(y + q - 2.4)}M{N(x + 2.4)} {N(y + q / 2)}L{N(x + q - 2.4)} {N(y + q / 2)}\" stroke=\"{cor.Borda}\" stroke-width=\"1.5\"/>");
                }
                return sb.ToString();
            }
            case PeDominios.TipoElementoFluxo.Inicio:
                return $"<circle cx=\"{N(a.Cx)}\" cy=\"{N(a.Cy)}\" r=\"{N(a.L / 2)}\" fill=\"#DCF3D2\" stroke=\"#3C9A32\" stroke-width=\"2\"/>";
            case PeDominios.TipoElementoFluxo.Fim:
                return $"<circle cx=\"{N(a.Cx)}\" cy=\"{N(a.Cy)}\" r=\"{N(a.L / 2 - 1)}\" fill=\"#F9D6D4\" stroke=\"#B42318\" stroke-width=\"3.4\"/>";
            case PeDominios.TipoElementoFluxo.Ligacao:
            {
                // Evento de enlace: círculo duplo com a seta (cheia quando o fluxo segue em outro)
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{N(a.Cx)}\" cy=\"{N(a.Cy)}\" r=\"{N(a.L / 2)}\" fill=\"#FFFBEA\" stroke=\"#8A6D1D\" stroke-width=\"1.3\"/>");
                sb.Append($"<circle cx=\"{N(a.Cx)}\" cy=\"{N(a.Cy)}\" r=\"{N(a.L / 2 - 3)}\" fill=\"none\" stroke=\"#8A6D1D\" stroke-width=\"1.1\"/>");
                double x = a.Cx, y = a.Cy;
                sb.Append($"<path d=\"M{N(x - 6)} {N(y - 2.6)}L{N(x + 0.5)} {N(y - 2.6)}L{N(x + 0.5)} {N(y - 6)}L{N(x + 6.5)} {N(y)}L{N(x + 0.5)} {N(y + 6)}L{N(x + 0.5)} {N(y + 2.6)}L{N(x - 6)} {N(y + 2.6)}Z\"")
                    .Append(recebeLigacao ? " fill=\"#8A6D1D\" stroke=\"none\"/>" : " fill=\"#FFFFFF\" stroke=\"#8A6D1D\" stroke-width=\"1.1\"/>");
                return sb.ToString();
            }
            default:
            {
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M{N(a.Cx)} {N(a.Y)}L{N(a.Direita)} {N(a.Cy)}L{N(a.Cx)} {N(a.Base)}L{N(a.X)} {N(a.Cy)}Z\" fill=\"#FFF6C7\" stroke=\"#9A7B12\" stroke-width=\"1.4\"/>");
                var m = 7.5;
                sb.Append(e.Tipo == PeDominios.TipoElementoFluxo.Paralelo
                    ? $"<path d=\"M{N(a.Cx)} {N(a.Cy - m - 1)}L{N(a.Cx)} {N(a.Cy + m + 1)}M{N(a.Cx - m - 1)} {N(a.Cy)}L{N(a.Cx + m + 1)} {N(a.Cy)}\" stroke=\"#7A5F0C\" stroke-width=\"2.8\" fill=\"none\"/>"
                    : $"<path d=\"M{N(a.Cx - m)} {N(a.Cy - m)}L{N(a.Cx + m)} {N(a.Cy + m)}M{N(a.Cx + m)} {N(a.Cy - m)}L{N(a.Cx - m)} {N(a.Cy + m)}\" stroke=\"#7A5F0C\" stroke-width=\"2.6\" fill=\"none\"/>");
                return sb.ToString();
            }
        }
    }

    private static string Texto(PeFluxoGeometriaTexto t, PeFluxoFonte? fonte)
    {
        if (fonte == null)
        {
            // Sem a fonte: texto comum (o navegador desenha)
            var peso = t.Negrito ? " font-weight=\"bold\"" : string.Empty;
            var transformacao = t.Vertical ? $" transform=\"rotate(-90 {N(t.X)} {N(t.Y)})\"" : string.Empty;
            return $"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-family=\"Lato, 'Helvetica Neue', Arial, sans-serif\" font-size=\"{N(t.Tamanho)}\"{peso} fill=\"{t.Cor}\"{transformacao}>{Esc(t.Texto)}</text>";
        }
        var escala = (t.Tamanho / fonte.UnidadesPorEm).ToString("0.######", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append($"<g fill=\"{t.Cor}\" transform=\"translate({N(t.X)} {N(t.Y)})")
            .Append(t.Vertical ? " rotate(-90)" : string.Empty)
            .Append($" scale({escala})\">");
        var prefixo = t.Negrito ? "b" : "r";
        long avanco = 0;
        foreach (var c in t.Texto)
        {
            var glifo = fonte.Glifo(c);
            if (fonte.Caminho(glifo).Length > 0)
            {
                sb.Append($"<use xlink:href=\"#pe-g{prefixo}{glifo}\"");
                if (avanco != 0) sb.Append($" x=\"{avanco.ToString(CultureInfo.InvariantCulture)}\"");
                sb.Append("/>");
            }
            avanco += fonte.Avanco(glifo);
        }
        sb.Append("</g>");
        return sb.ToString();
    }
}
