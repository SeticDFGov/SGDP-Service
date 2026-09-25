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
/// <item>capa como a da prévia (F1, achados C25 e B09): a faixa escura com a marca monocromática
/// do GDF, "GOVERNO DO DISTRITO FEDERAL" e o nome do órgão; no meio, o logotipo do órgão (quando
/// houver), o título, a sigla do documento com a do órgão ("PDTIC · SES", "RA · SES", "RR · SES"),
/// no RA o ciclo com o período ("Ciclo" só no de monitoramento) e o texto da capa (vigência e
/// versão, editáveis pelo órgão); no pé, "Brasília, ano";</item>
/// <item>folha de rosto, histórico de versões e sumário, cada um na sua página; o sumário com
/// os números de página e os links para os capítulos;</item>
/// <item>capítulos com o número pela posição (6, 6.1); texto rico pelo conversor único
/// (<see cref="PeTextoRicoPdf"/>); marcador sem valor sai em branco;</item>
/// <item>tabelas de dados com o cabeçalho repetido em cada página; formulários como rótulo e
/// valor; ações do tema; matriz SWOT em quatro quadrantes; fluxo em SVG (a partir da E6); os
/// blocos do acompanhamento em grupos de tabelas e o aviso do capítulo em branco (E7, rodada B,
/// nos relatórios RA e RR, que usam o mesmo PDF);</item>
/// <item>bloco com página deitada abre páginas A4 deitadas só para ele (desde a F1, só quando
/// tem dado: a tabela vazia fica na página em pé, junto do capítulo, achado C26); a quebra de
/// página começa uma página nova; os anexos começam numa página nova;</item>
/// <item>colunas com a largura mínima da maior palavra delas, do cabeçalho e das células (a data,
/// o código, o número e o valor inteiros, e nenhuma palavra quebrada no meio: achado C06, F1 e
/// F2), e o resto pelo peso do texto;</item>
/// <item>a legenda da tabela sai com o cabeçalho das colunas e a primeira linha, e a tabela curta
/// (até um terço da página) não se divide entre páginas (F2, achado B-N1);</item>
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

    // Largura útil da página (pontos), pelas margens de cada orientação
    public const float LarguraUtilEmPe = 595.28f - (2.5f + 2f) * PontosPorCentimetro;
    public const float LarguraUtilDeitada = 841.89f - 2 * 1.8f * PontosPorCentimetro;
    private const float PontosPorCentimetro = 28.3465f;

    // Espaço interno e borda de uma célula (4 de cada lado e a borda de 0,5)
    private const float SobraDaCelula = 9f;

    // A largura mínima não passa disto (a palavra maior, como um endereço de internet, quebra)
    private const float MaximoDaLarguraMinima = 96f;

    // A entrelinha do texto (a da página)
    private const float Entrelinha = 1.3f;

    // A altura útil da página (pontos): sem as margens e sem o rodapé (linha, espaço e texto)
    private const float AlturaDoRodape = 0.5f + 4f + 8f * Entrelinha;
    public const float AlturaUtilEmPe = 841.89f - (2.2f + 1.6f) * PontosPorCentimetro - AlturaDoRodape;
    public const float AlturaUtilDeitada = 595.28f - 2 * 1.6f * PontosPorCentimetro - AlturaDoRodape;

    // A tabela curta (com a legenda, até um terço da altura útil) não se divide entre páginas
    private const float FracaoDaTabelaCurta = 1f / 3f;

    // A marca monocromática do GDF (a mesma da prévia no front), embutida na aplicação
    private const string RecursoDaMarca = "Planejamento.marca-gdf.png";
    private static readonly Lazy<byte[]?> Marca = new(() =>
    {
        using var stream = typeof(PeDocumentoPdf).Assembly.GetManifestResourceStream(RecursoDaMarca);
        if (stream == null) return null;
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    });

    // A faixa da capa: o fundo escuro e o texto claro (os tons da prévia)
    private static readonly Color FundoDaFaixa = Color.FromHex("#0F172A");
    private static readonly Color TextoDaFaixa = Color.FromHex("#CBD5E1");

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

        // O ciclo do RA (a capa mostra o ciclo e o período); nulo no PDTIC e no RR
        public PeCiclo? Ciclo { get; init; }
    }

    public sealed record LinhaHistorico(string Data, string Versao, string Descricao, string Autor);

    public sealed record Resultado(byte[] Pdf, int Paginas, Roteiro Roteiro);

    // ── Roteiro ─────────────────────────────────────────────────────────────

    public enum TipoPeca { Capa, Quebra, Titulo, TituloSimples, Bloco, Historico, Sumario, Aviso }

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
                    var tituloDeitado = primeiro >= 0 && deitados[primeiro];
                    pecas.Add(new Peca(TipoPeca.Titulo, tituloDeitado, capitulo, Secao: secao));
                    sumario.Add(new ItemSumario(secao, capitulo.Numero, capitulo.Titulo, capitulo.Nivel));
                    // O capítulo em branco no relatório (E7, rodada B): o aviso logo abaixo do título
                    if (!string.IsNullOrWhiteSpace(capitulo.Aviso)) pecas.Add(new Peca(TipoPeca.Aviso, tituloDeitado, capitulo));
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
            : new Peca(TipoPeca.Bloco, Deitado(bloco), capitulo, bloco);

    /// <summary>
    /// O bloco vai para a página deitada: marcado para ela e com dado. A tabela (ou os grupos do
    /// acompanhamento) sem nenhuma linha fica em pé, junto do capítulo, em vez de ocupar uma página
    /// deitada inteira só com "Nenhum item" (F1, achado C26).
    /// </summary>
    public static bool Deitado(PeDocBlocoResponse bloco) => bloco.PaginaDeitada && !SemDados(bloco);

    private static bool SemDados(PeDocBlocoResponse bloco) =>
        bloco.Tipo switch
        {
            PeDominios.TipoBloco.TabelaSecao => bloco.Tabela == null || bloco.Tabela.Vazia || bloco.Tabela.Linhas.Count == 0,
            _ when bloco.Grupos != null => bloco.Grupos.All(g => g.Tabela.Vazia || g.Tabela.Linhas.Count == 0),
            _ => false
        };

    /// <summary>
    /// A orientação de cada bloco do capítulo: a do próprio bloco; o texto que vem antes de um
    /// bloco de dados deitado vai junto com ele (a introdução fica na mesma página da tabela
    /// larga, sem deixar uma página em pé quase vazia).
    /// </summary>
    private static List<bool> Orientacoes(IReadOnlyList<PeDocBlocoResponse> blocos)
    {
        var saida = blocos.Select(Deitado).ToList();
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
                if (!saida[i] && seguinte == true) saida[i] = true;
                continue;
            }
            if (Aparece(bloco)) seguinte = saida[i];
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
            Title = $"{SiglaDoDocumento(entrada.Documento.DocTipo)} {entrada.Documento.OrgaoSigla} · {entrada.Documento.Titulo}",
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
    /// Onde termina o começo de um capítulo (o que não se separa do título): os títulos, os avisos
    /// e os textos seguidos, até o primeiro bloco que não é texto (inclusive); para na quebra de
    /// página.
    /// </summary>
    public static int FimDoComeco(IReadOnlyList<Peca> pecas, int titulo)
    {
        var fim = titulo;
        while (fim + 1 < pecas.Count)
        {
            var seguinte = pecas[fim + 1];
            if (seguinte.Tipo is TipoPeca.Titulo or TipoPeca.Aviso)
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

    // O nó raiz da árvore de páginas diz o total (/Count); os nós do meio dizem menos
    [GeneratedRegex(@"/Type\s*/Pages\b[^>]*?/Count\s+(\d+)|/Count\s+(\d+)[^>]*?/Type\s*/Pages\b")]
    private static partial Regex ArvoreDePaginas();

    [GeneratedRegex(@"/Type\s*/ObjStm\b")]
    private static partial Regex FluxoDeObjetos();

    // Teto do que se descomprime ao contar as páginas de um PDF enviado (proteção contra arquivo-bomba)
    private const int LimiteDescomprimido = 32 * 1024 * 1024;

    /// <summary>
    /// Páginas de um PDF qualquer (o do PDTIC aprovado fora do sistema, E7), sem biblioteca de
    /// leitura: o maior /Count da árvore de páginas, procurado também nos fluxos de objetos
    /// comprimidos (PDF 1.5 em diante); senão, a contagem dos objetos de página; no mínimo 1.
    /// </summary>
    public static int ContarPaginasDoArquivo(byte[] pdf)
    {
        var textos = new List<string> { System.Text.Encoding.Latin1.GetString(pdf) };
        if (!ArvoreDePaginas().IsMatch(textos[0])) textos.AddRange(FluxosDeObjetos(pdf, textos[0]));

        var total = textos
            .SelectMany(t => ArvoreDePaginas().Matches(t))
            .Select(m => int.TryParse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        if (total > 0) return total;
        return Math.Max(1, textos.Sum(t => PaginaDoPdf().Matches(t).Count));
    }

    /// <summary>O conteúdo descomprimido dos fluxos de objetos (/Type /ObjStm, FlateDecode), onde o PDF 1.5 guarda a árvore de páginas.</summary>
    private static IEnumerable<string> FluxosDeObjetos(byte[] pdf, string texto)
    {
        var usado = 0;
        foreach (Match m in FluxoDeObjetos().Matches(texto))
        {
            var inicio = texto.IndexOf("stream", m.Index, StringComparison.Ordinal);
            if (inicio < 0) yield break;
            inicio += "stream".Length;
            if (inicio < texto.Length && texto[inicio] == '\r') inicio++;
            if (inicio < texto.Length && texto[inicio] == '\n') inicio++;
            var fim = texto.IndexOf("endstream", inicio, StringComparison.Ordinal);
            if (fim <= inicio) continue;

            string? conteudo = null;
            try
            {
                using var entrada = new MemoryStream(pdf, inicio, fim - inicio);
                using var zlib = new System.IO.Compression.ZLibStream(entrada, System.IO.Compression.CompressionMode.Decompress);
                using var saida = new MemoryStream();
                var buffer = new byte[81920];
                int lidos;
                while ((lidos = zlib.Read(buffer, 0, buffer.Length)) > 0)
                {
                    usado += lidos;
                    if (usado > LimiteDescomprimido) break;
                    saida.Write(buffer, 0, lidos);
                }
                conteudo = System.Text.Encoding.Latin1.GetString(saida.ToArray());
            }
            catch (InvalidDataException)
            {
                // Fluxo que não é FlateDecode (ou corrompido): fica de fora da contagem
            }
            if (conteudo != null) yield return conteudo;
            if (usado > LimiteDescomprimido) yield break;
        }
    }

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
                Bloco(col.Item(), peca.Bloco!, ctx, peca.Deitada);
                break;
            case TipoPeca.Aviso:
                col.Item().Text(peca.Capitulo!.Aviso!).FontSize(9.5f).Italic().FontColor(PeTextoRicoPdf.CorSuave);
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

    /// <summary>
    /// A capa, na ordem da prévia (F1, C25 e B09): a faixa escura com a marca do GDF, o governo e o
    /// órgão; no meio, o logotipo do órgão, o título, "PDTIC · SES" (ou "RA · SES", "RR · SES"),
    /// no RA o ciclo com o período e o texto da capa.
    /// </summary>
    private static void Capa(IContainer container, Contexto ctx, PeDocCapituloResponse capitulo)
    {
        var documento = ctx.Entrada.Documento;
        container.Column(col =>
        {
            col.Item().Background(FundoDaFaixa).PaddingVertical(14).PaddingHorizontal(18).Row(faixa =>
            {
                if (Marca.Value is byte[] marca)
                {
                    faixa.ConstantItem(92).Height(55).AlignMiddle().Image(marca).FitArea();
                    faixa.ConstantItem(16);
                }
                faixa.RelativeItem().AlignMiddle().Column(textos =>
                {
                    textos.Item().Text(Governo).FontSize(10.5f).SemiBold().LetterSpacing(0.06f).FontColor(Branco);
                    textos.Item().PaddingTop(2).Text(documento.OrgaoNome).FontSize(10.5f).FontColor(TextoDaFaixa);
                });
            });

            col.Item().ExtendVertical().AlignMiddle().Column(meio =>
            {
                if (ctx.Logotipo is PeImagemPdf logo)
                {
                    var largura = Math.Min(180f, 80f * logo.Largura / Math.Max(1, logo.Altura));
                    meio.Item().AlignCenter().MaxWidth(largura).Image(logo.Imagem).FitWidth();
                    meio.Item().Height(18);
                }
                meio.Item().AlignCenter().Text(documento.Titulo).FontSize(26).Bold().FontColor(PeTextoRicoPdf.CorTitulo).AlignCenter();
                meio.Item().PaddingTop(8).AlignCenter().Text($"{SiglaDoDocumento(documento.DocTipo)} · {documento.OrgaoSigla}")
                    .FontSize(15).SemiBold().FontColor(PeTextoRicoPdf.CorSuave);
                if (LinhaDoCiclo(documento, ctx.Entrada.Ciclo) is string ciclo)
                    meio.Item().PaddingTop(6).AlignCenter().Text(ciclo).FontSize(13).SemiBold().FontColor(PeTextoRicoPdf.CorTexto).AlignCenter();
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

    /// <summary>A sigla do documento na capa: PDTIC, RA (relatório de acompanhamento) ou RR (relatório de resultados).</summary>
    public static string SiglaDoDocumento(string? tipo) => tipo switch
    {
        PeDominios.TipoDocumento.Ra => "RA",
        PeDominios.TipoDocumento.Rr => "RR",
        _ => "PDTIC"
    };

    /// <summary>
    /// A linha do ciclo na capa do RA: "Ciclo 2027 · 1º trimestre (01/01/2027 a 31/03/2027)" no
    /// ciclo de monitoramento; na avaliação intermediária, o nome dela ("Avaliação da frente C
    /// (01/09/2026 a 25/09/2026)", ou "desde" enquanto aberta), sem a palavra "Ciclo". Nula fora do RA.
    /// </summary>
    public static string? LinhaDoCiclo(PeDocumentoResponse documento, PeCiclo? ciclo)
    {
        if (documento.DocTipo != PeDominios.TipoDocumento.Ra) return null;
        var rotulo = ciclo?.Rotulo ?? documento.CicloRotulo;
        if (string.IsNullOrWhiteSpace(rotulo)) return null;
        var nome = ciclo?.Tipo == PeDominios.TipoCiclo.Avaliacao ? rotulo.Trim() : $"Ciclo {rotulo.Trim()}";
        if (ciclo == null) return nome;
        var periodo = ciclo.Fim is DateOnly fim
            ? $"{PeFormato.Data(ciclo.Inicio)} a {PeFormato.Data(fim)}"
            : $"desde {PeFormato.Data(ciclo.Inicio)}";
        return $"{nome} ({periodo})";
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

    private static void Bloco(IContainer container, PeDocBlocoResponse bloco, Contexto ctx, bool deitada)
    {
        var largura = deitada ? LarguraUtilDeitada : LarguraUtilEmPe;
        switch (bloco.Tipo)
        {
            case PeDominios.TipoBloco.Texto:
            {
                var texto = TextoDoPdf(bloco, ctx);
                if (PeTextoRicoPdf.TemConteudo(texto)) PeTextoRicoPdf.Desenhar(container, texto, ctx.Texto());
                break;
            }
            case PeDominios.TipoBloco.TabelaSecao when bloco.Tabela != null:
                TabelaDeDados(container, bloco.Tabela, ctx, largura);
                break;
            case PeDominios.TipoBloco.ListaTema when bloco.Lista != null:
                ListaDoTema(container, bloco.Lista, largura);
                break;
            case PeDominios.TipoBloco.MatrizSwot when bloco.Swot != null:
                Swot(container, bloco.Swot);
                break;
            case PeDominios.TipoBloco.Fluxo when bloco.Fluxo?.Svg != null:
                Fluxo(container, bloco);
                break;
            case var tipo when bloco.Grupos != null && PeDominios.TipoBloco.DoAcompanhamento.Contains(tipo):
                GruposDoAcompanhamento(container, bloco.Grupos, ctx, largura);
                break;
        }
    }

    /// <summary>
    /// Os blocos do acompanhamento (E7, rodada B): cada grupo com o título, a frase que o explica
    /// (quando há) e a tabela; o grupo sem linha diz que não há nenhum item.
    /// </summary>
    private static void GruposDoAcompanhamento(IContainer container, IReadOnlyList<PeDocGrupoResponse> grupos, Contexto ctx, float largura)
    {
        container.Column(col =>
        {
            col.Spacing(10);
            foreach (var grupo in grupos)
                col.Item().Element(c => TabelaDeDados(c, grupo.Tabela, ctx, largura, grupo.Titulo, grupo.Texto, "Nenhum item neste grupo."));
        });
    }

    /// <summary>
    /// O texto do bloco para o PDF: os marcadores trocados, o sem valor em branco e o que sobra
    /// dele limpo (parênteses vazios, linhas vazias ou só com pontuação, rótulos sem valor).
    /// </summary>
    public static JsonNode? TextoDoPdf(PeDocBlocoResponse bloco, IReadOnlyDictionary<string, string?> marcadores)
    {
        if (bloco.TextoBruto is not JsonElement bruto || bruto.ValueKind != JsonValueKind.Object) return null;
        return PeDocMarcadores.ResolverParaPdf(JsonNode.Parse(bruto.GetRawText()), marcadores);
    }

    private static JsonNode? TextoDoPdf(PeDocBlocoResponse bloco, Contexto ctx) => TextoDoPdf(bloco, ctx.Entrada.Marcadores);

    private static void Legenda(ColumnDescriptor col, string texto) =>
        col.Item().Text(texto).FontSize(9.5f).Bold().FontColor(PeTextoRicoPdf.CorTitulo);

    private static void Vazio(ColumnDescriptor col, string texto) =>
        col.Item().Text(texto).FontSize(9).Italic().FontColor(PeTextoRicoPdf.CorSuave);

    // ── Tabelas de dados ────────────────────────────────────────────────────

    // Metade da linha mais baixa de uma tabela (uma linha de texto e o espaço interno da célula).
    // Somada à altura estimada da legenda e do cabeçalho, fica entre o cabeçalho sem nenhuma linha
    // e o cabeçalho com a primeira (a linha não se divide entre páginas, então não há meio-termo)
    private const float MeiaLinha = (FonteTabela * Entrelinha + 8f) / 2;

    /// <summary>
    /// A tabela de dados (ou o formulário) com a legenda e a frase que a explica. A legenda não fica
    /// sozinha no pé da página (F2, achado B-N1): a tabela curta (até um terço da página) vai
    /// inteira para a página seguinte quando não cabe no resto desta, e a longa só começa numa
    /// página em que cabem a legenda, o cabeçalho e a primeira linha (<see cref="ComecoDaTabela"/>).
    /// </summary>
    private static void TabelaDeDados(IContainer container, PeDocTabelaResponse tabela, Contexto ctx, float largura, string? legenda = null,
        string? explicacao = null, string vazia = "Nenhum item registrado.")
    {
        var titulo = legenda ?? tabela.SecaoTitulo;
        container = TabelaCurta(tabela, largura, titulo, explicacao)
            ? container.PreventPageBreak()
            : container.EnsureSpace(ComecoDaTabela(tabela, largura, titulo, explicacao));
        container.Column(col =>
        {
            col.Spacing(4);
            Legenda(col, titulo);
            if (!string.IsNullOrWhiteSpace(explicacao))
                col.Item().Text(explicacao).FontSize(9).FontColor(PeTextoRicoPdf.CorSuave);
            if (tabela.SecaoTipo == PeDominios.TipoSecao.Formulario)
            {
                Formulario(col, tabela, ctx);
                return;
            }
            if (tabela.Vazia || tabela.Colunas.Count == 0)
            {
                Vazio(col, vazia);
                return;
            }

            var comCodigo = tabela.Linhas.Any(l => l.Codigo != null);
            var larguraDoCodigo = comCodigo ? LarguraDaColunaDoCodigo(tabela) : 0f;
            var larguras = Larguras(tabela, largura - larguraDoCodigo);
            var total = (uint)(tabela.Colunas.Count + (comCodigo ? 1 : 0));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    if (comCodigo) c.ConstantColumn(larguraDoCodigo);
                    foreach (var coluna in larguras)
                        if (coluna.Constante) c.ConstantColumn(coluna.Valor);
                        else c.RelativeColumn(coluna.Valor);
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
                        if (comCodigo) Celula(row.ConstantItem(larguraDoCodigo), linha.Codigo ?? "-");
                        for (var i = 0; i < tabela.Colunas.Count; i++)
                        {
                            var coluna = tabela.Colunas[i];
                            var item = larguras[i].Constante ? row.ConstantItem(larguras[i].Valor) : row.RelativeItem(larguras[i].Valor);
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

    // Proporção das colunas do rótulo e do valor no formulário
    private const float RotuloDoFormulario = 1.1f;
    private const float ValorDoFormulario = 2.6f;

    // O rótulo de um texto formatado do formulário (o espaço acima, a linha de 9 e o espaço abaixo)
    private const float RotuloDoTexto = 2f + 9f * Entrelinha + 4f;

    // O começo de um texto que não fica sozinho no pé da página: duas linhas do corpo
    private const float DuasLinhas = 2 * Fonte * Entrelinha;

    /// <summary>
    /// Formulário: os campos preenchidos como rótulo e valor; o texto formatado (o diagnóstico,
    /// com organograma) sai na largura da página, abaixo do rótulo, que não fica sozinho no pé da
    /// página (vai com as duas primeiras linhas do texto, F2).
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
        if (TextoUnico(tabela, linha) is { } unico)
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
                        row.RelativeItem(RotuloDoFormulario).Border(0.5f).BorderColor(PeTextoRicoPdf.CorBorda).Background(FundoRotulo).Padding(4)
                            .Text(rotulo).FontSize(FonteTabela).SemiBold();
                        Celula(row.RelativeItem(ValorDoFormulario), valor);
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
                col.Item().EnsureSpace(RotuloDoTexto + DuasLinhas).Column(c =>
                {
                    c.Spacing(4);
                    c.Item().PaddingTop(2).Text(coluna.Rotulo).FontSize(9).SemiBold().FontColor(PeTextoRicoPdf.CorSuave);
                    c.Item().Element(x => PeTextoRicoPdf.Desenhar(x, no, ctx.Texto()));
                });
                continue;
            }
            var valor = linha.Celulas.GetValueOrDefault(coluna.Chave);
            if (string.IsNullOrWhiteSpace(valor) || valor == "-") continue;
            simples.Add((coluna.Rotulo, valor));
        }
        FecharSimples();
        if (!desenhou) Vazio(col, "Não preenchido.");
    }

    /// <summary>O formulário com um só campo preenchido, e ele é um texto formatado: o texto dele (senão, nulo).</summary>
    private static JsonElement? TextoUnico(PeDocTabelaResponse tabela, PeDocLinhaResponse linha)
    {
        var preenchidas = tabela.Colunas.Where(c => linha.Ricos != null && linha.Ricos.TryGetValue(c.Chave, out var r)
                ? PeTextoRicoPdf.TemConteudo(JsonNode.Parse(r.GetRawText()))
                : !string.IsNullOrWhiteSpace(linha.Celulas.GetValueOrDefault(c.Chave)) && linha.Celulas.GetValueOrDefault(c.Chave) != "-")
            .ToList();
        return preenchidas.Count == 1 && linha.Ricos != null && linha.Ricos.TryGetValue(preenchidas[0].Chave, out var unico) ? unico : null;
    }

    /// <summary>A largura de uma coluna da tabela: fixa (pontos) ou relativa (o peso).</summary>
    public sealed record LarguraDaColuna(bool Constante, float Valor);

    /// <summary>
    /// As larguras das colunas de dados (F1, achado C06; F2): toda coluna tem uma largura mínima
    /// em que a maior palavra dela cabe inteira (<see cref="LarguraMinima"/>: a do cabeçalho e a de
    /// cada célula; a data, o código, o número e o valor em reais são uma palavra só). A coluna
    /// que pelo peso ficaria mais estreita que a mínima fica fixa nela, e as outras dividem o resto
    /// pelo peso. Se as mínimas somadas não cabem na largura útil (colunas demais), cada coluna
    /// fica com a parte proporcional à sua mínima.
    /// </summary>
    public static List<LarguraDaColuna> Larguras(PeDocTabelaResponse tabela, float larguraUtil)
    {
        var pesos = tabela.Colunas.Select(c => Peso(tabela, c)).ToList();
        var minimas = tabela.Colunas.Select(c => LarguraMinima(tabela, c)).ToList();
        if (minimas.Sum() > larguraUtil)
            return minimas.Select(m => new LarguraDaColuna(false, Math.Max(m, 1f))).ToList();

        var fixas = new bool[pesos.Count];
        bool mudou;
        do
        {
            mudou = false;
            var resto = larguraUtil - minimas.Where((_, i) => fixas[i]).Sum();
            var somaDosPesos = pesos.Where((_, i) => !fixas[i]).Sum();
            if (somaDosPesos <= 0) break;
            for (var i = 0; i < pesos.Count; i++)
            {
                if (fixas[i] || minimas[i] <= 0) continue;
                if (pesos[i] / somaDosPesos * resto >= minimas[i]) continue;
                fixas[i] = true;
                mudou = true;
            }
        } while (mudou);

        if (fixas.All(f => f))
            return minimas.Select(m => new LarguraDaColuna(false, Math.Max(m, 1f))).ToList();
        return pesos.Select((p, i) => fixas[i] ? new LarguraDaColuna(true, minimas[i]) : new LarguraDaColuna(false, p)).ToList();
    }

    /// <summary>
    /// A largura mínima da coluna (F2, achado C06): a maior palavra dela inteira, a do cabeçalho
    /// (em negrito) e a de cada célula (o texto formatado pelas palavras dele), mais o espaço
    /// interno da célula, até o teto de <see cref="MaximoDaLarguraMinima"/> pontos. Palavra é o que
    /// fica entre espaços: a data, o código, o número e o "R$" com o valor (espaço sem quebra) são
    /// uma palavra só. Zero na coluna sem palavra nenhuma.
    /// </summary>
    public static float LarguraMinima(PeDocTabelaResponse tabela, PeDocColunaResponse coluna)
    {
        var cabecalho = Palavras(coluna.Rotulo).Select(p => LarguraDoTexto(p, negrito: true));
        var celulas = tabela.Linhas.SelectMany(l => Palavras(TextoDaCelula(l, coluna))).Select(p => LarguraDoTexto(p, negrito: false));
        var maior = cabecalho.Concat(celulas).DefaultIfEmpty(0).Max();
        return maior <= 0 ? 0 : (float)Math.Ceiling(Math.Min(MaximoDaLarguraMinima, maior + SobraDaCelula));
    }

    // As palavras de um texto (o que fica entre espaços e quebras de linha; o espaço sem quebra não separa)
    private static IEnumerable<string> Palavras(string texto) =>
        texto.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>O texto da célula como o PDF o mostra ("-" quando vazia; o texto formatado, pelas palavras dele).</summary>
    private static string TextoDaCelula(PeDocLinhaResponse linha, PeDocColunaResponse coluna) =>
        linha.Ricos != null && linha.Ricos.TryGetValue(coluna.Chave, out var rico)
            ? TextoDoRico(rico)
            : SemQuebra(linha.Celulas.GetValueOrDefault(coluna.Chave) ?? "-");

    /// <summary>O texto de um texto formatado (TipTap), um bloco por linha, sem as marcas.</summary>
    private static string TextoDoRico(JsonElement documento)
    {
        var texto = new System.Text.StringBuilder();
        void Visitar(JsonElement no)
        {
            if (no.ValueKind != JsonValueKind.Object) return;
            if (no.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) texto.Append(t.GetString());
            if (no.TryGetProperty("type", out var tipo) && tipo.ValueKind == JsonValueKind.String && tipo.GetString() == "hardBreak") texto.Append('\n');
            if (no.TryGetProperty("content", out var filhos) && filhos.ValueKind == JsonValueKind.Array)
                foreach (var filho in filhos.EnumerateArray())
                {
                    Visitar(filho);
                    // Depois de um bloco (parágrafo, título, item), a linha seguinte
                    if (filho.ValueKind == JsonValueKind.Object && filho.TryGetProperty("content", out _) && texto.Length > 0 && texto[^1] != '\n')
                        texto.Append('\n');
                }
        }
        Visitar(documento);
        return texto.ToString().Trim();
    }

    // A altura de uma imagem de texto formatado, na conta da altura (a máxima do PDF: a conta erra para mais)
    private const float AlturaDaImagem = 480f;

    /// <summary>Quantas imagens o texto formatado tem (a altura delas não sai do texto).</summary>
    private static int ImagensDoRico(JsonElement no)
    {
        if (no.ValueKind != JsonValueKind.Object) return 0;
        var imagens = no.TryGetProperty("type", out var tipo) && tipo.ValueKind == JsonValueKind.String && tipo.GetString() == "image" ? 1 : 0;
        if (no.TryGetProperty("content", out var filhos) && filhos.ValueKind == JsonValueKind.Array)
            foreach (var filho in filhos.EnumerateArray()) imagens += ImagensDoRico(filho);
        return imagens;
    }

    /// <summary>A coluna do código: o maior código (e o cabeçalho "Código") inteiro, no mínimo a largura de sempre.</summary>
    private static float LarguraDaColunaDoCodigo(PeDocTabelaResponse tabela)
    {
        var maior = tabela.Linhas.Select(l => l.Codigo ?? "-").Append("Código")
            .Max(c => LarguraDoTexto(c, negrito: c == "Código"));
        return (float)Math.Max(LarguraDoCodigo, Math.Ceiling(maior + SobraDaCelula));
    }

    /// <summary>A largura do texto na fonte das tabelas (a Lato do PDF; sem os arquivos dela, a estimada).</summary>
    private static double LarguraDoTexto(string texto, bool negrito) => LarguraDoTexto(texto, FonteTabela, negrito);

    public static double LarguraDoTexto(string texto, float tamanho, bool negrito)
    {
        var fonte = negrito ? PeFluxoFonte.Negrito : PeFluxoFonte.Regular;
        return fonte?.Largura(texto, tamanho) ?? PeFluxoFonte.LarguraEstimada(texto, tamanho, negrito);
    }

    // ── Altura estimada (F2, achado B-N1) ───────────────────────────────────

    /// <summary>
    /// A tabela (ou o formulário) é curta quando, com a legenda, cabe em um terço da altura útil
    /// da página (em pé ou deitada, pela largura): não se divide entre páginas.
    /// </summary>
    public static bool TabelaCurta(PeDocTabelaResponse tabela, float largura, string legenda, string? explicacao) =>
        AlturaEstimada(tabela, largura, legenda, explicacao) <= AlturaUtil(largura) * FracaoDaTabelaCurta;

    private static float AlturaUtil(float largura) => largura > LarguraUtilEmPe + 1 ? AlturaUtilDeitada : AlturaUtilEmPe;

    /// <summary>
    /// A altura estimada da tabela (pontos), com a legenda e a explicação: as linhas de cada célula
    /// pela largura da coluna e pelas medidas da Lato, quebrando nas palavras como o PDF.
    /// </summary>
    public static float AlturaEstimada(PeDocTabelaResponse tabela, float largura, string legenda, string? explicacao)
    {
        var altura = AlturaDaCabeca(largura, legenda, explicacao) + 4f;
        if (tabela.SecaoTipo == PeDominios.TipoSecao.Formulario) return altura + AlturaDoFormulario(tabela, largura);
        if (tabela.Vazia || tabela.Colunas.Count == 0) return altura + 9f * Entrelinha;

        var (larguraDoCodigo, colunas, cabecalho) = ColunasEOCabecalho(tabela, largura);
        altura += cabecalho;
        foreach (var linha in tabela.Linhas)
        {
            var celulas = tabela.Colunas.Select((c, i) => (TextoDaCelula(linha, c), colunas[i])).ToList();
            if (larguraDoCodigo > 0) celulas.Insert(0, (linha.Codigo ?? "-", larguraDoCodigo));
            altura += AlturaDaLinha(celulas, negrito: false);
            // A imagem de um texto formatado na célula: a altura dela não sai do texto
            if (linha.Ricos != null) altura += linha.Ricos.Values.Sum(ImagensDoRico) * AlturaDaImagem;
        }
        return altura;
    }

    /// <summary>
    /// O mínimo para a tabela começar numa página (F2, achado B-N1): a legenda, a explicação e o
    /// cabeçalho estimados e metade da linha mais baixa. Com menos que isso no resto da página, a
    /// tabela começa na seguinte: a legenda não fica sozinha no pé da página, nem com o cabeçalho
    /// sem nenhuma linha (a linha não se divide entre páginas). No formulário, a legenda e metade
    /// do primeiro par de rótulo e valor ou, quando ele começa por um texto formatado, o rótulo e
    /// as duas primeiras linhas do texto.
    /// </summary>
    public static float ComecoDaTabela(PeDocTabelaResponse tabela, float largura, string legenda, string? explicacao)
    {
        var cabeca = AlturaDaCabeca(largura, legenda, explicacao) + 4f;
        if (tabela.SecaoTipo == PeDominios.TipoSecao.Formulario)
        {
            var linha = tabela.Linhas.FirstOrDefault();
            if (linha == null) return cabeca + 9f * Entrelinha;
            if (TextoUnico(tabela, linha) != null) return cabeca + DuasLinhas;
            foreach (var coluna in tabela.Colunas)
            {
                if (linha.Ricos != null && linha.Ricos.TryGetValue(coluna.Chave, out var rico))
                {
                    if (PeTextoRicoPdf.TemConteudo(JsonNode.Parse(rico.GetRawText()))) return cabeca + RotuloDoTexto + DuasLinhas;
                    continue;
                }
                var valor = linha.Celulas.GetValueOrDefault(coluna.Chave);
                if (!string.IsNullOrWhiteSpace(valor) && valor != "-") return cabeca + MeiaLinha;
            }
            return cabeca + 9f * Entrelinha;
        }
        if (tabela.Vazia || tabela.Colunas.Count == 0) return cabeca + 9f * Entrelinha;
        return cabeca + ColunasEOCabecalho(tabela, largura).Cabecalho + MeiaLinha;
    }

    /// <summary>As larguras das colunas em pontos (a do código à parte, zero sem código) e a altura do cabeçalho.</summary>
    private static (float LarguraDoCodigo, List<float> Colunas, float Cabecalho) ColunasEOCabecalho(PeDocTabelaResponse tabela, float largura)
    {
        var comCodigo = tabela.Linhas.Any(l => l.Codigo != null);
        var larguraDoCodigo = comCodigo ? LarguraDaColunaDoCodigo(tabela) : 0f;
        var colunas = EmPontos(Larguras(tabela, largura - larguraDoCodigo), largura - larguraDoCodigo);
        var cabecalho = tabela.Colunas.Select((c, i) => (c.Rotulo, colunas[i])).ToList();
        if (comCodigo) cabecalho.Insert(0, ("Código", larguraDoCodigo));
        return (larguraDoCodigo, colunas, AlturaDaLinha(cabecalho, negrito: true));
    }

    /// <summary>A legenda (9,5 em negrito) e a explicação (9), na largura toda.</summary>
    private static float AlturaDaCabeca(float largura, string legenda, string? explicacao)
    {
        var altura = LinhasDoTexto(legenda, largura, 9.5f, negrito: true) * 9.5f * Entrelinha;
        if (!string.IsNullOrWhiteSpace(explicacao))
            altura += 4f + LinhasDoTexto(explicacao, largura, 9f, negrito: false) * 9f * Entrelinha;
        return altura;
    }

    /// <summary>Uma linha da tabela: a célula mais alta (as linhas do texto e o espaço interno).</summary>
    private static float AlturaDaLinha(IEnumerable<(string Texto, float Largura)> celulas, bool negrito) =>
        celulas.Select(c => LinhasDoTexto(c.Texto, c.Largura - SobraDaCelula, FonteTabela, negrito)).DefaultIfEmpty(1).Max()
        * FonteTabela * Entrelinha + 8f;

    /// <summary>O formulário: os pares de rótulo e valor e os textos formatados (rótulo e texto na largura toda).</summary>
    private static float AlturaDoFormulario(PeDocTabelaResponse tabela, float largura)
    {
        var linha = tabela.Linhas.FirstOrDefault();
        if (linha == null) return 9f * Entrelinha;
        var rotulo = largura * RotuloDoFormulario / (RotuloDoFormulario + ValorDoFormulario);
        var valor = largura * ValorDoFormulario / (RotuloDoFormulario + ValorDoFormulario);
        var altura = 0f;
        foreach (var coluna in tabela.Colunas)
        {
            if (linha.Ricos != null && linha.Ricos.TryGetValue(coluna.Chave, out var rico))
            {
                var texto = TextoDoRico(rico);
                var imagens = ImagensDoRico(rico);
                if (texto.Length == 0 && imagens == 0) continue;
                altura += 4f + RotuloDoTexto + AlturaDoTextoCorrido(texto, largura) + imagens * AlturaDaImagem;
                continue;
            }
            var celula = linha.Celulas.GetValueOrDefault(coluna.Chave);
            if (string.IsNullOrWhiteSpace(celula) || celula == "-") continue;
            // O par: a mais alta das duas células (o rótulo em negrito)
            altura += Math.Max(AlturaDaLinha(new[] { (coluna.Rotulo, rotulo) }, negrito: true),
                AlturaDaLinha(new[] { (SemQuebra(celula), valor) }, negrito: false));
        }
        return altura > 0 ? altura : 9f * Entrelinha;
    }

    /// <summary>O texto corrido do corpo (10,5), um parágrafo por linha do texto, com o espaço entre eles.</summary>
    private static float AlturaDoTextoCorrido(string texto, float largura) =>
        texto.Split('\n').Sum(p => LinhasDoTexto(p, largura, Fonte, negrito: false) * Fonte * Entrelinha + Fonte * 0.5f);

    /// <summary>
    /// Quantas linhas o texto ocupa na largura: quebra nas palavras (a palavra que não cabe numa
    /// linha inteira quebra no meio, como no PDF) e em cada quebra de linha do texto.
    /// </summary>
    public static int LinhasDoTexto(string texto, float largura, float tamanho, bool negrito)
    {
        largura = Math.Max(largura, 1f);
        var espaco = LarguraDoTexto(" ", tamanho, negrito);
        var linhas = 0;
        foreach (var paragrafo in texto.Split('\n'))
        {
            linhas++;
            var atual = 0.0;
            foreach (var palavra in paragrafo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var medida = LarguraDoTexto(palavra, tamanho, negrito);
                if (atual > 0 && atual + espaco + medida > largura)
                {
                    linhas++;
                    atual = 0;
                }
                atual += (atual > 0 ? espaco : 0) + medida;
                while (atual > largura)
                {
                    linhas++;
                    atual -= largura;
                }
            }
        }
        return Math.Max(1, linhas);
    }

    /// <summary>As larguras em pontos: a fixa como está e a relativa pela parte dela no que sobra.</summary>
    private static List<float> EmPontos(IReadOnlyList<LarguraDaColuna> larguras, float disponivel)
    {
        var fixas = larguras.Where(l => l.Constante).Sum(l => l.Valor);
        var relativas = larguras.Where(l => !l.Constante).Sum(l => l.Valor);
        return larguras.Select(l => l.Constante ? l.Valor : relativas <= 0 ? 0 : (disponivel - fixas) * l.Valor / relativas).ToList();
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

    // As colunas da lista de ações do tema: o código, a ação e a situação
    private const float AcaoDoTema = 4f;
    private const float SituacaoDoTema = 1.4f;

    /// <summary>
    /// As ações de um tema do decreto. Como nas tabelas de dados (F2, achado B-N1), a legenda sai
    /// com o cabeçalho e a primeira ação, e a lista curta não se divide entre páginas.
    /// </summary>
    private static void ListaDoTema(IContainer container, PeDocListaResponse lista, float largura)
    {
        var legenda = $"Ações do tema {lista.Tema}";
        var acao = (largura - LarguraDoCodigo) * AcaoDoTema / (AcaoDoTema + SituacaoDoTema);
        var situacao = largura - LarguraDoCodigo - acao;
        var comeco = AlturaDaCabeca(largura, legenda, null) + 4f
                     + AlturaDaLinha(new[] { ("Código", LarguraDoCodigo), ("Ação", acao), ("Situação", situacao) }, negrito: true);
        var altura = comeco + lista.Itens.Sum(item => AlturaDaLinha(new[]
        {
            (item.Codigo ?? "-", LarguraDoCodigo),
            (string.IsNullOrWhiteSpace(item.Texto) ? "-" : item.Texto, acao),
            (item.Situacao ?? "-", situacao)
        }, negrito: false));
        container = lista.Itens.Count == 0 || altura <= AlturaUtil(largura) * FracaoDaTabelaCurta
            ? container.PreventPageBreak()
            : container.EnsureSpace(comeco + MeiaLinha);

        container.Column(col =>
        {
            col.Spacing(4);
            Legenda(col, legenda);
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
                    c.RelativeColumn(AcaoDoTema);
                    c.RelativeColumn(SituacaoDoTema);
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
                        Celula(row.RelativeItem(AcaoDoTema), string.IsNullOrWhiteSpace(item.Texto) ? "-" : item.Texto);
                        Celula(row.RelativeItem(SituacaoDoTema), item.Situacao ?? "-");
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
