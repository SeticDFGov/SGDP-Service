using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using api.Planejamento;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace service.Planejamento;

/// <summary>
/// O PDF do documento do PDTIC (QuestPDF, licença Community como os outros PDFs do projeto),
/// a partir da estrutura resolvida (<see cref="PeDocumentoResponse"/>). Layout limpo e sóbrio:
/// <list type="bullet">
/// <item>capa com a identificação do Governo do Distrito Federal, o nome do órgão, o logotipo
/// (quando houver), o título e o texto da capa (vigência e versão, editáveis pelo órgão);</item>
/// <item>folha de rosto, histórico de versões e sumário, cada um na sua página; o sumário com
/// os números de página e os links para os capítulos;</item>
/// <item>capítulos com o número pela posição (6, 6.1); texto rico pelo conversor único
/// (<see cref="PeTextoRicoPdf"/>); marcador sem valor sai em branco;</item>
/// <item>tabelas de dados com o cabeçalho repetido em cada página; formulários como rótulo e
/// valor; ações do tema; matriz SWOT em quatro quadrantes; fluxo em SVG (a partir da E6);</item>
/// <item>bloco com página deitada abre páginas A4 deitadas só para ele; a quebra de página
/// começa uma página nova; os anexos começam numa página nova;</item>
/// <item>rodapé com o órgão, a versão e "Página N de M" (menos na capa).</item>
/// </list>
/// O roteiro (<see cref="Montar"/>) diz o que vai em cada grupo de páginas e o que entra no
/// sumário, e é o que os testes conferem sem abrir o PDF.
/// </summary>
public static partial class PeDocumentoPdf
{
    static PeDocumentoPdf()
    {
        // O Program.cs já define a licença na API; aqui garante o mesmo fora dela (testes)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public const float Fonte = 10.5f;
    private const float FonteTabela = 8.5f;

    /// <summary>Espaço mínimo para começar um capítulo (título, introdução e o começo da tabela).</summary>
    private const float EspacoDoComeco = 180f;

    private const float LarguraDoCodigo = 44f;
    private const string Governo = "GOVERNO DO DISTRITO FEDERAL";

    private static readonly Color Branco = Color.FromHex("#FFFFFF");
    private static readonly Color FundoRotulo = Color.FromHex("#F3F5F8");

    // Quadrantes da SWOT: fundo e cor do título
    private static readonly (Color Fundo, Color Cor) Forcas = (Color.FromHex("#EEF6F0"), Color.FromHex("#2E6B3F"));
    private static readonly (Color Fundo, Color Cor) Fraquezas = (Color.FromHex("#FBEFEC"), Color.FromHex("#9A3B2A"));
    private static readonly (Color Fundo, Color Cor) Oportunidades = (Color.FromHex("#EDF3FA"), Color.FromHex("#27548A"));
    private static readonly (Color Fundo, Color Cor) Ameacas = (Color.FromHex("#FDF4E8"), Color.FromHex("#8A5A12"));

    /// <summary>O que o PDF precisa além da estrutura resolvida.</summary>
    public sealed class Entrada
    {
        public required PeDocumentoResponse Documento { get; init; }

        // Os valores dos marcadores (o marcador sem valor sai em branco)
        public IReadOnlyDictionary<string, string?> Marcadores { get; init; } = new Dictionary<string, string?>();

        // Imagens dos textos (do modelo, do órgão e dos registros), pelo id do arquivo
        public IReadOnlyDictionary<long, byte[]> Imagens { get; init; } = new Dictionary<long, byte[]>();

        public byte[]? Logotipo { get; init; }

        public IReadOnlyList<LinhaHistorico> Historico { get; init; } = Array.Empty<LinhaHistorico>();

        // "SES · PDTIC versão 1.0 · minuta nº 3"
        public string Rodape { get; init; } = string.Empty;

        // Horário de Brasília
        public DateTime GeradoEm { get; init; }
    }

    public sealed record LinhaHistorico(string Data, string Versao, string Descricao, string Autor);

    public sealed record Resultado(byte[] Pdf, int Paginas, Roteiro Roteiro);

    // ── Roteiro ─────────────────────────────────────────────────────────────

    public enum TipoPeca { Capa, Quebra, Titulo, TituloSimples, Bloco, Historico, Sumario }

    /// <summary>Um pedaço do documento: título de capítulo, bloco, quebra ou página especial.</summary>
    public sealed record Peca(TipoPeca Tipo, bool Deitada, PeDocCapituloResponse? Capitulo = null, PeDocBlocoResponse? Bloco = null,
        string? Secao = null);

    /// <summary>Uma entrada do sumário: a seção do PDF (destino do link), o número e o título.</summary>
    public sealed record ItemSumario(string Secao, string? Numero, string Titulo, int Nivel);

    /// <summary>Páginas seguidas com a mesma orientação (a capa é um grupo só dela, sem rodapé).</summary>
    public sealed record Grupo(bool Deitada, bool Capa, IReadOnlyList<Peca> Pecas);

    public sealed class Roteiro
    {
        public List<Grupo> Grupos { get; init; } = new();

        public List<ItemSumario> Sumario { get; init; } = new();
    }

    /// <summary>
    /// Monta o roteiro: os capítulos visíveis (o oculto não entra), cada peça com a orientação
    /// da página, as quebras (as páginas especiais, o primeiro capítulo depois delas e os
    /// anexos começam numa página nova) e o sumário (capítulos e subcapítulos, fora as páginas
    /// especiais). O título do capítulo vai com a orientação do primeiro bloco que aparece.
    /// </summary>
    public static Roteiro Montar(PeDocumentoResponse documento)
    {
        var pecas = new List<Peca>();
        var sumario = new List<ItemSumario>();
        var anteriorEspecial = false;

        foreach (var capitulo in documento.Capitulos.Where(c => !c.Oculto))
        {
            var chave = capitulo.Chave;
            var blocos = capitulo.Blocos.OrderBy(b => b.Ordem).ToList();
            if (chave == PeDominios.CapituloEspecial.Capa)
            {
                pecas.Add(new Peca(TipoPeca.Capa, false, capitulo));
                anteriorEspecial = true;
                continue;
            }

            var especial = PeDominios.CapituloEspecial.EhPreTextual(chave);
            if (especial || anteriorEspecial || chave == PeDominios.CapituloEspecial.Anexos)
                pecas.Add(new Peca(TipoPeca.Quebra, false));
            anteriorEspecial = especial;

            switch (chave)
            {
                case PeDominios.CapituloEspecial.FolhaRosto:
                    foreach (var bloco in blocos) pecas.Add(PecaDoBloco(capitulo, bloco));
                    break;
                case PeDominios.CapituloEspecial.Historico:
                    pecas.Add(new Peca(TipoPeca.TituloSimples, false, capitulo));
                    pecas.Add(new Peca(TipoPeca.Historico, false, capitulo));
                    break;
                case PeDominios.CapituloEspecial.Sumario:
                    pecas.Add(new Peca(TipoPeca.TituloSimples, false, capitulo));
                    pecas.Add(new Peca(TipoPeca.Sumario, false, capitulo));
                    break;
                default:
                {
                    var secao = NomeDaSecao(capitulo);
                    var deitados = Orientacoes(blocos);
                    var primeiro = blocos.FindIndex(Aparece);
                    pecas.Add(new Peca(TipoPeca.Titulo, primeiro >= 0 && deitados[primeiro], capitulo, Secao: secao));
                    sumario.Add(new ItemSumario(secao, capitulo.Numero, capitulo.Titulo, capitulo.Nivel));
                    for (var i = 0; i < blocos.Count; i++)
                    {
                        var peca = PecaDoBloco(capitulo, blocos[i]);
                        pecas.Add(peca.Tipo == TipoPeca.Bloco ? peca with { Deitada = deitados[i] } : peca);
                    }
                    break;
                }
            }
        }

        // A quebra vai com a orientação da peça seguinte; no começo do grupo ela não faz falta
        for (var i = pecas.Count - 1; i >= 0; i--)
        {
            if (pecas[i].Tipo != TipoPeca.Quebra) continue;
            var seguinte = pecas.Skip(i + 1).FirstOrDefault(p => p.Tipo != TipoPeca.Quebra);
            pecas[i] = pecas[i] with { Deitada = seguinte?.Deitada ?? false };
        }

        var grupos = new List<Grupo>();
        List<Peca>? atual = null;
        var deitado = false;
        foreach (var peca in pecas)
        {
            if (peca.Tipo == TipoPeca.Capa)
            {
                grupos.Add(new Grupo(false, true, new[] { peca }));
                atual = null;
                continue;
            }
            if (atual == null || deitado != peca.Deitada)
            {
                atual = new List<Peca>();
                deitado = peca.Deitada;
                grupos.Add(new Grupo(deitado, false, atual));
            }
            if (peca.Tipo == TipoPeca.Quebra && (atual.Count == 0 || atual[^1].Tipo == TipoPeca.Quebra)) continue;
            atual.Add(peca);
        }
        foreach (var grupo in grupos.Where(g => !g.Capa))
        {
            var lista = (List<Peca>)grupo.Pecas;
            while (lista.Count > 0 && lista[^1].Tipo == TipoPeca.Quebra) lista.RemoveAt(lista.Count - 1);
        }

        return new Roteiro { Grupos = grupos.Where(g => g.Pecas.Count > 0).ToList(), Sumario = sumario };
    }

    private static Peca PecaDoBloco(PeDocCapituloResponse capitulo, PeDocBlocoResponse bloco) =>
        bloco.Tipo == PeDominios.TipoBloco.QuebraPagina
            ? new Peca(TipoPeca.Quebra, false)
            : new Peca(TipoPeca.Bloco, bloco.PaginaDeitada, capitulo, bloco);

    /// <summary>
    /// A orientação de cada bloco do capítulo: a do próprio bloco; o texto que vem antes de um
    /// bloco de dados deitado vai junto com ele (a introdução fica na mesma página da tabela
    /// larga, sem deixar uma página em pé quase vazia).
    /// </summary>
    private static List<bool> Orientacoes(IReadOnlyList<PeDocBlocoResponse> blocos)
    {
        var saida = blocos.Select(b => b.PaginaDeitada).ToList();
        bool? seguinte = null;
        for (var i = blocos.Count - 1; i >= 0; i--)
        {
            var bloco = blocos[i];
            if (bloco.Tipo == PeDominios.TipoBloco.QuebraPagina)
            {
                seguinte = null;
                continue;
            }
            if (bloco.Tipo == PeDominios.TipoBloco.Texto)
            {
                if (!bloco.PaginaDeitada && seguinte == true) saida[i] = true;
                continue;
            }
            if (Aparece(bloco)) seguinte = bloco.PaginaDeitada;
        }
        return saida;
    }

    /// <summary>O bloco desenha alguma coisa no PDF (o fluxo sem desenho e a quebra, não).</summary>
    private static bool Aparece(PeDocBlocoResponse bloco) =>
        bloco.Tipo switch
        {
            PeDominios.TipoBloco.QuebraPagina => false,
            PeDominios.TipoBloco.Fluxo => bloco.Fluxo?.Svg != null,
            _ => true
        };

    public static string NomeDaSecao(PeDocCapituloResponse capitulo) => $"cap-{capitulo.Id}";

    // ── Geração ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gera o PDF. Erro do QuestPDF (conteúdo que não cabe na página, por exemplo) sobe para
    /// quem chama, que responde 409 com a mensagem.
    /// </summary>
    public static Resultado Gerar(Entrada entrada)
    {
        var roteiro = Montar(entrada.Documento);
        var imagens = new Dictionary<long, PeImagemPdf?>();
        PeImagemPdf? Imagem(long id)
        {
            if (!imagens.TryGetValue(id, out var imagem))
                imagens[id] = imagem = PeImagemPdf.Abrir(entrada.Imagens.GetValueOrDefault(id));
            return imagem;
        }
        var logotipo = PeImagemPdf.Abrir(entrada.Logotipo);
        var paginas = 0;
        var ctx = new Contexto(entrada, roteiro, Imagem, logotipo);

        var documento = Document.Create(container =>
        {
            foreach (var grupo in roteiro.Grupos)
            {
                container.Page(page =>
                {
                    page.Size(grupo.Deitada ? PageSizes.A4.Landscape() : PageSizes.A4);
                    if (grupo.Deitada)
                    {
                        page.MarginVertical(1.6f, Unit.Centimetre);
                        page.MarginHorizontal(1.8f, Unit.Centimetre);
                    }
                    else
                    {
                        page.MarginTop(2.2f, Unit.Centimetre);
                        page.MarginBottom(1.6f, Unit.Centimetre);
                        page.MarginLeft(2.5f, Unit.Centimetre);
                        page.MarginRight(2f, Unit.Centimetre);
                    }
                    page.PageColor(Branco);
                    page.DefaultTextStyle(s => s.FontSize(Fonte).FontColor(PeTextoRicoPdf.CorTexto).LineHeight(1.3f));

                    if (grupo.Capa)
                    {
                        page.Content().Element(c => Capa(c, ctx, grupo.Pecas[0].Capitulo!));
                        page.Footer().AlignCenter().Text($"Brasília, {entrada.GeradoEm.Year.ToString(CultureInfo.InvariantCulture)}")
                            .FontSize(11).FontColor(PeTextoRicoPdf.CorSuave);
                        return;
                    }

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(PeTextoRicoPdf.CorBorda);
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text(entrada.Rodape).FontSize(8).FontColor(PeTextoRicoPdf.CorSuave);
                            row.AutoItem().Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(8).FontColor(PeTextoRicoPdf.CorSuave));
                                t.Span("Página ");
                                t.CurrentPageNumber();
                                t.Span(" de ");
                                t.TotalPages().Format(n =>
                                {
                                    if (n is int total && total > paginas) paginas = total;
                                    return n?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                                });
                            });
                        });
                    });

                    page.Content().PaddingTop(2).Column(col =>
                    {
                        col.Spacing(8);
                        var pecas = grupo.Pecas;
                        for (var i = 0; i < pecas.Count; i++)
                        {
                            if (pecas[i].Tipo != TipoPeca.Titulo)
                            {
                                Desenhar(col, pecas[i], ctx);
                                continue;
                            }
                            // O título não fica sozinho no pé da página: ele, o texto que vem logo
                            // depois e o começo do primeiro bloco de dados vão juntos; sem espaço
                            // para começar, o conjunto vai para a página seguinte
                            var fim = FimDoComeco(pecas, i);
                            var comeco = pecas.Skip(i).Take(fim - i + 1).ToList();
                            col.Item().EnsureSpace(EspacoDoComeco).Column(c =>
                            {
                                c.Spacing(8);
                                foreach (var peca in comeco) Desenhar(c, peca, ctx);
                            });
                            i = fim;
                        }
                    });
                });
            }
        }).WithMetadata(new DocumentMetadata
        {
            Title = $"PDTIC {entrada.Documento.OrgaoSigla} · {entrada.Documento.Titulo}",
            Author = entrada.Documento.OrgaoNome,
            Subject = $"{entrada.Documento.Titulo}, versão {entrada.Documento.Versao}",
            Creator = "SGDP · Governança Estratégica",
            Producer = "SGDP",
            Language = "pt-BR",
            CreationDate = new DateTimeOffset(DateTime.SpecifyKind(entrada.GeradoEm, DateTimeKind.Unspecified), TimeSpan.FromHours(-3)),
            ModifiedDate = new DateTimeOffset(DateTime.SpecifyKind(entrada.GeradoEm, DateTimeKind.Unspecified), TimeSpan.FromHours(-3))
        });

        var pdf = documento.GeneratePdf();
        return new Resultado(pdf, paginas > 0 ? paginas : ContarPaginas(pdf), roteiro);
    }

    /// <summary>
    /// Onde termina o começo de um capítulo (o que não se separa do título): os títulos e os
    /// textos seguidos, até o primeiro bloco que não é texto (inclusive); para na quebra de página.
    /// </summary>
    public static int FimDoComeco(IReadOnlyList<Peca> pecas, int titulo)
    {
        var fim = titulo;
        while (fim + 1 < pecas.Count)
        {
            var seguinte = pecas[fim + 1];
            if (seguinte.Tipo == TipoPeca.Titulo)
            {
                fim++;
                continue;
            }
            if (seguinte.Tipo != TipoPeca.Bloco) break;
            fim++;
            if (seguinte.Bloco!.Tipo != PeDominios.TipoBloco.Texto) break;
        }
        return fim;
    }

    /// <summary>Páginas do PDF pelo objeto de cada página (reserva, se o rodapé não contou).</summary>
    public static int ContarPaginas(byte[] pdf) =>
        PaginaDoPdf().Matches(System.Text.Encoding.Latin1.GetString(pdf)).Count;

    [GeneratedRegex(@"/Type\s*/Page[^s]")]
    private static partial Regex PaginaDoPdf();

    private sealed record Contexto(Entrada Entrada, Roteiro Roteiro, Func<long, PeImagemPdf?> Imagem, PeImagemPdf? Logotipo)
    {
        public PeTextoRicoPdf.Opcoes Texto(float fonte = Fonte, bool centralizar = false) => new()
        {
            Fonte = fonte,
            Imagem = Imagem,
            Centralizar = centralizar,
            AlturaMaximaImagem = 480f
        };
    }

    // ── Peças ───────────────────────────────────────────────────────────────

    private static void Desenhar(ColumnDescriptor col, Peca peca, Contexto ctx)
    {
        switch (peca.Tipo)
        {
            case TipoPeca.Quebra:
                col.Item().PageBreak();
                break;
            case TipoPeca.Titulo:
                Titulo(col.Item(), peca.Capitulo!, peca.Secao!);
                break;
            case TipoPeca.TituloSimples:
                col.Item().PaddingBottom(4).Text(peca.Capitulo!.Titulo).FontSize(16).Bold().FontColor(PeTextoRicoPdf.CorTitulo);
                break;
            case TipoPeca.Historico:
                Historico(col.Item(), ctx.Entrada.Historico);
                break;
            case TipoPeca.Sumario:
                Sumario(col.Item(), ctx.Roteiro.Sumario);
                break;
            case TipoPeca.Bloco:
                Bloco(col.Item(), peca.Bloco!, ctx);
                break;
        }
    }

    private static void Titulo(IContainer container, PeDocCapituloResponse capitulo, string secao)
    {
        var nivel1 = capitulo.Nivel <= 1;
        var tamanho = nivel1 ? 15f : 12.5f;
        container.PaddingTop(nivel1 ? 10 : 4).Section(secao).Column(col =>
        {
            col.Item().Row(row =>
            {
                if (capitulo.Numero != null)
                    row.ConstantItem(nivel1 ? 30 : 38).Text(capitulo.Numero).FontSize(tamanho).Bold().FontColor(PeTextoRicoPdf.CorTitulo);
                row.RelativeItem().Text(capitulo.Titulo).FontSize(tamanho).Bold().FontColor(PeTextoRicoPdf.CorTitulo);
            });
            if (nivel1) col.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(PeTextoRicoPdf.CorTitulo);
        });
    }

    private static void Capa(IContainer container, Contexto ctx, PeDocCapituloResponse capitulo)
    {
        var documento = ctx.Entrada.Documento;
        container.Column(col =>
        {
            col.Item().AlignCenter().Column(topo =>
            {
                if (ctx.Logotipo is PeImagemPdf logo)
                {
                    var largura = Math.Min(180f, 80f * logo.Largura / Math.Max(1, logo.Altura));
                    topo.Item().AlignCenter().MaxWidth(largura).Image(logo.Imagem).FitWidth();
                    topo.Item().Height(14);
                }
                topo.Item().AlignCenter().Text(Governo).FontSize(11).SemiBold().LetterSpacing(0.08f).FontColor(PeTextoRicoPdf.CorTitulo);
                topo.Item().PaddingTop(4).AlignCenter().Text(documento.OrgaoNome).FontSize(13).FontColor(PeTextoRicoPdf.CorTexto);
            });

            col.Item().ExtendVertical().AlignMiddle().Column(meio =>
            {
                meio.Item().AlignCenter().Text(documento.Titulo).FontSize(26).Bold().FontColor(PeTextoRicoPdf.CorTitulo).AlignCenter();
                meio.Item().PaddingTop(8).AlignCenter().Text($"PDTIC {documento.OrgaoSigla}").FontSize(15).FontColor(PeTextoRicoPdf.CorSuave);
                meio.Item().PaddingTop(12).AlignCenter().Width(90).LineHorizontal(1.5f).LineColor(PeTextoRicoPdf.CorTitulo);
                foreach (var bloco in capitulo.Blocos.OrderBy(b => b.Ordem).Where(b => b.Tipo == PeDominios.TipoBloco.Texto))
                {
                    var texto = TextoDoPdf(bloco, ctx);
                    if (!PeTextoRicoPdf.TemConteudo(texto)) continue;
                    meio.Item().PaddingTop(18).Element(c => PeTextoRicoPdf.Desenhar(c, texto, ctx.Texto(12f, centralizar: true)));
                }
            });
        });
    }

    private static void Sumario(IContainer container, IReadOnlyList<ItemSumario> itens)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            foreach (var item in itens)
            {
                var nivel1 = item.Nivel <= 1;
                col.Item().PaddingTop(nivel1 ? 4 : 0).PaddingLeft(nivel1 ? 0 : 20).SectionLink(item.Secao).Row(row =>
                {
                    if (item.Numero != null)
                        row.ConstantItem(nivel1 ? 26 : 34).Text(item.Numero).FontSize(nivel1 ? 10.5f : 10f).SemiBold();
                    var titulo = row.RelativeItem().Text(item.Titulo).FontSize(nivel1 ? 10.5f : 10f);
                    if (nivel1) titulo.SemiBold();
                    row.ConstantItem(34).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(nivel1 ? 10.5f : 10f));
                        t.BeginPageNumberOfSection(item.Secao);
                    });
                });
            }
        });
    }

    private static void Historico(IContainer container, IReadOnlyList<LinhaHistorico> linhas)
    {
        if (linhas.Count == 0)
        {
            container.Text("Nenhuma versão registrada.").FontSize(9.5f).Italic().FontColor(PeTextoRicoPdf.CorSuave);
            return;
        }
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(3f);
                c.RelativeColumn(2.2f);
            });
            t.Header(h =>
            {
                foreach (var titulo in new[] { "Data", "Versão", "Descrição", "Autor" }) CelulaDeCabecalho(h.Cell(), titulo);
            });
            foreach (var linha in linhas)
            {
                Celula(t.Cell(), linha.Data);
                Celula(t.Cell(), linha.Versao);
                Celula(t.Cell(), linha.Descricao);
                Celula(t.Cell(), linha.Autor);
            }
        });
    }

    // ── Blocos ──────────────────────────────────────────────────────────────

    private static void Bloco(IContainer container, PeDocBlocoResponse bloco, Contexto ctx)
    {
        switch (bloco.Tipo)
        {
            case PeDominios.TipoBloco.Texto:
            {
                var texto = TextoDoPdf(bloco, ctx);
                if (PeTextoRicoPdf.TemConteudo(texto)) PeTextoRicoPdf.Desenhar(container, texto, ctx.Texto());
                break;
            }
            case PeDominios.TipoBloco.TabelaSecao when bloco.Tabela != null:
                TabelaDeDados(container, bloco.Tabela, ctx);
                break;
            case PeDominios.TipoBloco.ListaTema when bloco.Lista != null:
                ListaDoTema(container, bloco.Lista);
                break;
            case PeDominios.TipoBloco.MatrizSwot when bloco.Swot != null:
                Swot(container, bloco.Swot);
                break;
            case PeDominios.TipoBloco.Fluxo when bloco.Fluxo?.Svg != null:
                Fluxo(container, bloco);
                break;
        }
    }

    /// <summary>O texto do bloco para o PDF: os marcadores trocados e o sem valor em branco.</summary>
    private static JsonNode? TextoDoPdf(PeDocBlocoResponse bloco, Contexto ctx)
    {
        if (bloco.TextoBruto is not JsonElement bruto || bruto.ValueKind != JsonValueKind.Object) return null;
        return PeDocMarcadores.Resolver(JsonNode.Parse(bruto.GetRawText()), ctx.Entrada.Marcadores, emBranco: true);
    }

    private static void Legenda(ColumnDescriptor col, string texto) =>
        col.Item().Text(texto).FontSize(9.5f).Bold().FontColor(PeTextoRicoPdf.CorTitulo);

    private static void Vazio(ColumnDescriptor col, string texto) =>
        col.Item().Text(texto).FontSize(9).Italic().FontColor(PeTextoRicoPdf.CorSuave);

    private static void TabelaDeDados(IContainer container, PeDocTabelaResponse tabela, Contexto ctx)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            Legenda(col, tabela.SecaoTitulo);
            if (tabela.SecaoTipo == PeDominios.TipoSecao.Formulario)
            {
                Formulario(col, tabela, ctx);
                return;
            }
            if (tabela.Vazia || tabela.Colunas.Count == 0)
            {
                Vazio(col, "Nenhum item registrado.");
                return;
            }

            var comCodigo = tabela.Linhas.Any(l => l.Codigo != null);
            var pesos = tabela.Colunas.Select(c => Peso(tabela, c)).ToList();
            var total = (uint)(tabela.Colunas.Count + (comCodigo ? 1 : 0));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    if (comCodigo) c.ConstantColumn(LarguraDoCodigo);
                    foreach (var peso in pesos) c.RelativeColumn(peso);
                });
                t.Header(h =>
                {
                    if (comCodigo) CelulaDeCabecalho(h.Cell(), "Código");
                    foreach (var coluna in tabela.Colunas) CelulaDeCabecalho(h.Cell(), coluna.Rotulo);
                });
                foreach (var linha in tabela.Linhas)
                {
                    // A linha não se divide entre páginas (a não ser que não caiba numa página inteira)
                    LinhaInteira(t, total, row =>
                    {
                        if (comCodigo) Celula(row.ConstantItem(LarguraDoCodigo), linha.Codigo ?? "-");
                        for (var i = 0; i < tabela.Colunas.Count; i++)
                        {
                            var coluna = tabela.Colunas[i];
                            var item = row.RelativeItem(pesos[i]);
                            if (linha.Ricos != null && linha.Ricos.TryGetValue(coluna.Chave, out var rico))
                            {
                                var no = JsonNode.Parse(rico.GetRawText());
                                item.Border(0.5f).BorderColor(PeTextoRicoPdf.CorBorda).Padding(4)
                                    .Element(c => PeTextoRicoPdf.Desenhar(c, no, ctx.Texto(FonteTabela)));
                                continue;
                            }
                            Celula(item, linha.Celulas.GetValueOrDefault(coluna.Chave) ?? "-");
                        }
                    });
                }
            });
        });
    }

    /// <summary>
    /// Formulário: os campos preenchidos como rótulo e valor; o texto formatado (o diagnóstico,
    /// com organograma) sai na largura da página, abaixo do rótulo.
    /// </summary>
    private static void Formulario(ColumnDescriptor col, PeDocTabelaResponse tabela, Contexto ctx)
    {
        var linha = tabela.Linhas.FirstOrDefault();
        if (linha == null)
        {
            Vazio(col, "Não preenchido.");
            return;
        }

        // Só um texto formatado preenchido (o diagnóstico): sai sem o rótulo, logo abaixo da legenda
        var preenchidas = tabela.Colunas.Where(c => linha.Ricos != null && linha.Ricos.TryGetValue(c.Chave, out var r)
                ? PeTextoRicoPdf.TemConteudo(JsonNode.Parse(r.GetRawText()))
                : !string.IsNullOrWhiteSpace(linha.Celulas.GetValueOrDefault(c.Chave)) && linha.Celulas.GetValueOrDefault(c.Chave) != "-")
            .ToList();
        if (preenchidas.Count == 1 && linha.Ricos != null && linha.Ricos.TryGetValue(preenchidas[0].Chave, out var unico))
        {
            var no = JsonNode.Parse(unico.GetRawText());
            col.Item().Element(c => PeTextoRicoPdf.Desenhar(c, no, ctx.Texto()));
            return;
        }

        var desenhou = false;
        var simples = new List<(string Rotulo, string Valor)>();
        void FecharSimples()
        {
            if (simples.Count == 0) return;
            var pares = simples.ToList();
            simples.Clear();
            desenhou = true;
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => c.RelativeColumn());
                foreach (var (rotulo, valor) in pares)
                    LinhaInteira(t, 1, row =>
                    {
                        row.RelativeItem(1.1f).Border(0.5f).BorderColor(PeTextoRicoPdf.CorBorda).Background(FundoRotulo).Padding(4)
                            .Text(rotulo).FontSize(FonteTabela).SemiBold();
                        Celula(row.RelativeItem(2.6f), valor);
                    });
            });
        }

        foreach (var coluna in tabela.Colunas)
        {
            if (linha.Ricos != null && linha.Ricos.TryGetValue(coluna.Chave, out var rico))
            {
                FecharSimples();
                var no = JsonNode.Parse(rico.GetRawText());
                if (!PeTextoRicoPdf.TemConteudo(no)) continue;
                desenhou = true;
                col.Item().PaddingTop(2).Text(coluna.Rotulo).FontSize(9).SemiBold().FontColor(PeTextoRicoPdf.CorSuave);
                col.Item().Element(c => PeTextoRicoPdf.Desenhar(c, no, ctx.Texto()));
                continue;
            }
            var valor = linha.Celulas.GetValueOrDefault(coluna.Chave);
            if (string.IsNullOrWhiteSpace(valor) || valor == "-") continue;
            simples.Add((coluna.Rotulo, valor));
        }
        FecharSimples();
        if (!desenhou) Vazio(col, "Não preenchido.");
    }

    /// <summary>
    /// Peso da coluna: a maior palavra (do cabeçalho e das células) cabe inteira, e o texto
    /// longo ganha mais espaço, sem tomar a tabela toda (cresce pela raiz do tamanho médio).
    /// </summary>
    public static float Peso(PeDocTabelaResponse tabela, PeDocColunaResponse coluna)
    {
        var textos = tabela.Linhas.Select(l => SemQuebra(l.Celulas.GetValueOrDefault(coluna.Chave) ?? string.Empty)).ToList();
        var media = textos.Count == 0 ? 0 : textos.Average(t => Math.Min(t.Length, 160));
        var palavra = textos.Append(coluna.Rotulo)
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(p => Math.Min(p.Length, 18))
            .DefaultIfEmpty(4)
            .Max();
        return (float)Math.Max(palavra + 2, Math.Sqrt(media) * 4);
    }

    private static void ListaDoTema(IContainer container, PeDocListaResponse lista)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            Legenda(col, $"Ações do tema {lista.Tema}");
            if (lista.Itens.Count == 0)
            {
                Vazio(col, "Nenhuma ação deste tema no plano.");
                if (!string.IsNullOrWhiteSpace(lista.Justificativa))
                    col.Item().Text(t =>
                    {
                        t.Span("Justificativa do órgão: ").SemiBold();
                        t.Span(lista.Justificativa);
                    });
                return;
            }
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(LarguraDoCodigo);
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.4f);
                });
                t.Header(h =>
                {
                    CelulaDeCabecalho(h.Cell(), "Código");
                    CelulaDeCabecalho(h.Cell(), "Ação");
                    CelulaDeCabecalho(h.Cell(), "Situação");
                });
                foreach (var item in lista.Itens)
                    LinhaInteira(t, 3, row =>
                    {
                        Celula(row.ConstantItem(LarguraDoCodigo), item.Codigo ?? "-");
                        Celula(row.RelativeItem(4), string.IsNullOrWhiteSpace(item.Texto) ? "-" : item.Texto);
                        Celula(row.RelativeItem(1.4f), item.Situacao ?? "-");
                    });
            });
        });
    }

    private static void Swot(IContainer container, PeDocSwotResponse swot)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });
            t.Cell().Row(1).Column(1).ColumnSpan(2).PaddingBottom(2)
                .Text("Ambiente interno").FontSize(8.5f).SemiBold().FontColor(PeTextoRicoPdf.CorSuave);
            Quadrante(t.Cell().Row(2).Column(1), "Forças", swot.Forcas, Forcas);
            Quadrante(t.Cell().Row(2).Column(2), "Fraquezas", swot.Fraquezas, Fraquezas);
            t.Cell().Row(3).Column(1).ColumnSpan(2).PaddingTop(6).PaddingBottom(2)
                .Text("Ambiente externo").FontSize(8.5f).SemiBold().FontColor(PeTextoRicoPdf.CorSuave);
            Quadrante(t.Cell().Row(4).Column(1), "Oportunidades", swot.Oportunidades, Oportunidades);
            Quadrante(t.Cell().Row(4).Column(2), "Ameaças", swot.Ameacas, Ameacas);
        });
    }

    private static void Quadrante(IContainer container, string titulo, IReadOnlyList<string> itens, (Color Fundo, Color Cor) cores)
    {
        container.Padding(3).Background(cores.Fundo).Border(0.5f).BorderColor(cores.Cor).Padding(8).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text(titulo).FontSize(10).Bold().FontColor(cores.Cor);
            if (itens.Count == 0)
            {
                col.Item().Text("Nenhum item.").FontSize(9).Italic().FontColor(PeTextoRicoPdf.CorSuave);
                return;
            }
            foreach (var item in itens)
                col.Item().Row(row =>
                {
                    row.ConstantItem(10).Text("•").FontSize(9);
                    row.RelativeItem().Text(item).FontSize(9);
                });
        });
    }

    // Área do fluxo na página (pontos): a largura útil e uma altura que deixa a legenda e o rodapé
    private const float FluxoLarguraEmPe = 465f;
    private const float FluxoAlturaEmPe = 600f;
    private const float FluxoLarguraDeitada = 735f;
    private const float FluxoAlturaDeitada = 420f;

    // O desenho não cresce além deste tamanho (texto de 10,5 do desenho sai com cerca de 9 pontos)
    private const float FluxoEscalaMaxima = 0.85f;

    // A altura da legenda (até duas linhas) e do espaço até o desenho
    private const float FluxoFolgaDaLegenda = 30f;

    /// <summary>
    /// O fluxo em SVG (E6): o texto do desenho já vem em contornos, então sai igual ao da prévia.
    /// A escala cabe na largura e na altura da página (deitada, quando o fluxo é largo) sem
    /// passar da escala máxima.
    /// </summary>
    private static void Fluxo(IContainer container, PeDocBlocoResponse bloco)
    {
        var fluxo = bloco.Fluxo!;
        SvgImage? svg;
        try
        {
            svg = SvgImage.FromText(fluxo.Svg!);
        }
        catch (Exception)
        {
            return;
        }
        var largura = FluxoEscalaMaxima * 600f;
        var altura = 200f;
        if (TamanhoDoSvg(fluxo.Svg!) is (float l, float a))
        {
            var escala = Math.Min(FluxoEscalaMaxima, Math.Min(
                (bloco.PaginaDeitada ? FluxoLarguraDeitada : FluxoLarguraEmPe) / l,
                (bloco.PaginaDeitada ? FluxoAlturaDeitada : FluxoAlturaEmPe) / a));
            largura = l * escala;
            altura = a * escala;
        }
        // A legenda e o desenho juntos: pela escala, o bloco inteiro cabe numa página, então,
        // quando não cabe no resto desta, vai todo para a seguinte (a legenda não fica sozinha)
        container.EnsureSpace(altura + FluxoFolgaDaLegenda).Column(col =>
        {
            col.Spacing(4);
            Legenda(col, fluxo.Nome);
            col.Item().AlignCenter().Width(largura).Svg(svg).FitWidth();
        });
    }

    /// <summary>Largura e altura do SVG, pelo viewBox (ou nulo).</summary>
    public static (float Largura, float Altura)? TamanhoDoSvg(string svg)
    {
        var m = ViewBox().Match(svg);
        if (!m.Success) return null;
        var partes = m.Groups[1].Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 4
               && float.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var largura)
               && float.TryParse(partes[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var altura)
               && largura > 0 && altura > 0
            ? (largura, altura)
            : null;
    }

    [GeneratedRegex("viewBox\\s*=\\s*\"([^\"]+)\"")]
    private static partial Regex ViewBox();

    // ── Células ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Uma linha da tabela como uma célula só, com as colunas dentro (as mesmas larguras do
    /// cabeçalho): a linha inteira passa para a página seguinte em vez de se dividir, e só se
    /// divide quando não cabe numa página inteira. O cabeçalho da tabela se repete em cada página.
    /// </summary>
    private static void LinhaInteira(TableDescriptor tabela, uint colunas, Action<RowDescriptor> desenhar) =>
        tabela.Cell().ColumnSpan(colunas).PreventPageBreak().Row(desenhar);

    private static void CelulaDeCabecalho(IContainer celula, string texto) =>
        celula.Border(0.5f).BorderColor(PeTextoRicoPdf.CorBorda).Background(PeTextoRicoPdf.CorFundoCabecalho).Padding(4)
            .Text(texto).FontSize(FonteTabela).SemiBold().FontColor(PeTextoRicoPdf.CorTitulo);

    private static void Celula(IContainer celula, string texto) =>
        celula.Border(0.5f).BorderColor(PeTextoRicoPdf.CorBorda).Padding(4).Text(SemQuebra(texto)).FontSize(FonteTabela);

    /// <summary>O "R$" não se separa do valor na quebra de linha (espaço sem quebra).</summary>
    public static string SemQuebra(string texto) => texto.Replace("R$ ", "R$" + EspacoSemQuebra, StringComparison.Ordinal);

    private const char EspacoSemQuebra = (char)0xA0;
}
