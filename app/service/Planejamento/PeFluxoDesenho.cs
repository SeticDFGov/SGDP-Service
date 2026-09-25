using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O desenho automático de um fluxo no padrão BPMN das figuras do guia (E6), com uma fonte só
/// desde a E9: o layout abaixo produz a geometria (<see cref="Geometria"/>, a mesma que o
/// editor visual recebe) e o SVG da prévia e do PDF é escrito a partir dela
/// (<see cref="PeFluxoSvg"/>), então a tela e o PDF não divergem:
/// <list type="bullet">
/// <item>raias em faixas horizontais, com o nome na vertical à esquerda (e, com duas raias ou
/// mais, o nome do fluxo numa faixa a mais, como o "pool" do guia);</item>
/// <item>colunas da esquerda para a direita pela ordem das ligações (<see cref="PeFluxoAnalise"/>);
/// caminhos paralelos empilhados na mesma coluna;</item>
/// <item>tarefa em retângulo arredondado com o número em negrito acima do nome (o subprocesso
/// com a marca "+" embaixo), início em círculo verde, fim em círculo vermelho de borda grossa,
/// ligação com outro fluxo em círculo duplo com a seta, decisão em losango com "X" e paralelo
/// em losango com "+";</item>
/// <item>ligações ortogonais com seta, sem atravessar caixas (a rota mais simples que não bate
/// em nada); o rótulo das saídas da decisão perto da saída; os retornos descem, correm por baixo
/// da raia e sobem até o passo de destino;</item>
/// <item>artefatos como documento com a ponta dobrada embaixo da tarefa, ligados por linha
/// pontilhada, com o nome embaixo;</item>
/// <item>texto quebrado em linhas pela largura real da fonte, sem sair da caixa, e desenhado
/// como contorno (<see cref="PeFluxoFonte"/>): o mesmo SVG sai igual no navegador e no PDF;</item>
/// <item>definição incompleta (durante a edição): tudo é posicionado; o elemento sem nenhuma
/// ligação vai para o fim da raia dele (<see cref="PeFluxoAnalise.Soltos"/>) e a ligação para
/// o que não existe fica de fora.</item>
/// </list>
/// Os marcadores do dicionário de nomes ("{nomes.comite}") são trocados antes de medir. O SVG
/// traz &lt;title&gt; e &lt;desc&gt; (a descrição em lista numerada) para leitores de tela.
/// </summary>
public sealed class PeFluxoDesenho
{
    // ── Medidas (unidades do SVG) ─────────────────────────────────────────────

    public const double Margem = 8;
    public const double LarguraTarefa = 112;
    private const double PadTarefaX = 6;
    private const double PadTarefaY = 7;
    public const double FonteTarefa = 10.5;
    private const double LinhaTarefa = 13;
    private const double AlturaMinimaTarefa = 56;
    private const int MaximoLinhasTarefa = 7;
    private const double MarcaSubprocesso = 13;
    public const double RaioEvento = 15;
    public const double MeiaPorta = 20;
    private const double FonteRotulo = 9.5;
    private const double LinhaRotulo = 11.5;
    private const double LarguraRotuloEvento = 108;
    private const double LarguraRotuloPorta = 118;
    private const double FonteArtefato = 9;
    private const double LinhaArtefato = 11;
    private const double DocLargura = 22;
    private const double DocAltura = 28;
    private const double ConectorArtefato = 16;
    private const double GapColuna = 30;
    private const double InicioConteudo = 18;
    private const double FimConteudo = 24;
    private const double GapFaixa = 22;
    private const double PadRaiaTopo = 20;
    private const double PadRaiaBase = 18;
    private const double EspacoCanal = 13;
    private const double FonteRaia = 11.5;
    private const double LinhaRaia = 14.5;
    private const double FontePool = 12.5;
    private const double LinhaPool = 16;
    private const double AlturaMinimaRaia = 90;
    private const double AlturaMaiuscula = 0.72;

    // Cores dos textos e (fundo e borda) da tarefa por prefixo, como as cores dos subprocessos
    // no guia; as outras cores do desenho são estilo do SVG (PeFluxoSvg)
    private const string CorTexto = "#1F2937";
    private const string CorRotuloLigacao = "#111827";

    private static readonly Dictionary<string, (string Fundo, string Borda)> Paleta = new()
    {
        ["1"] = ("#FFF4CC", "#A8861B"),
        ["2"] = ("#E4F1D9", "#557F36"),
        ["3"] = ("#DCE9F7", "#3A6A9E"),
        ["4"] = ("#E7E9EE", "#5B6677"),
        ["5"] = ("#ECE5F6", "#65499A"),
        ["6"] = ("#FCE6D6", "#B25E27"),
        ["7"] = ("#EFE3DB", "#77503E")
    };

    private static readonly (string Fundo, string Borda) PaletaPadrao = ("#E2ECF7", "#3E6C99");

    // ── Resultado ───────────────────────────────────────────────────────────

    public double Largura { get; private set; }

    public double Altura { get; private set; }

    /// <summary>
    /// A geometria do desenho (E9): o que o editor visual recebe e de onde o SVG sai. Erros fica
    /// vazio aqui (quem lê a definição preenche).
    /// </summary>
    public PeFluxoGeometria Geometria { get; private set; } = new();

    private string? _svg;

    /// <summary>O SVG, escrito a partir da <see cref="Geometria"/> (só quando é pedido).</summary>
    public string Svg => _svg ??= PeFluxoSvg.Escrever(Geometria, _regular, _negrito);

    /// <summary>A descrição do fluxo em lista numerada (texto alternativo do desenho).</summary>
    public List<string> Descricao { get; } = new();

    public string Titulo { get; private set; } = string.Empty;

    public List<Caixa> Caixas { get; } = new();

    public List<Linha> Linhas { get; } = new();

    public List<Artefato> Artefatos { get; } = new();

    public List<RaiaDesenhada> Raias { get; } = new();

    public List<Texto> Textos { get; } = new();

    public readonly record struct Ponto(double X, double Y);

    public readonly record struct Retangulo(double X, double Y, double L, double A)
    {
        public double Direita => X + L;
        public double Base => Y + A;
        public double Cx => X + L / 2;
        public double Cy => Y + A / 2;

        public bool Sobrepoe(Retangulo o, double folga = 0) =>
            X < o.Direita - folga && o.X < Direita - folga && Y < o.Base - folga && o.Y < Base - folga;

        public bool Contem(Retangulo o, double folga = 0) =>
            o.X >= X - folga && o.Direita <= Direita + folga && o.Y >= Y - folga && o.Base <= Base + folga;

        public Retangulo Inflar(double d) => new(X - d, Y - d, L + 2 * d, A + 2 * d);
    }

    public sealed class Caixa
    {
        public required PeFluxoElemento Elemento { get; init; }
        public required Retangulo Area { get; init; }
        public required int Raia { get; init; }
        public required int Camada { get; init; }
        public required int Faixa { get; init; }
        public double Cx => Area.Cx;
        public double Cy => Area.Cy;
    }

    public sealed class Linha
    {
        public required PeFluxoLigacao Ligacao { get; init; }
        public required bool Retorno { get; init; }
        public required List<Ponto> Pontos { get; init; }
        // O retângulo branco atrás do rótulo e onde o texto começa (x à esquerda, y na linha de base)
        public Retangulo? Rotulo { get; set; }
        public Ponto? RotuloEm { get; set; }
        public int Colisoes { get; set; }
        // O que a rota escolhida atravessa (vazio no desenho bom)
        public List<string> Atingidos { get; init; } = new();
    }

    public sealed class Artefato
    {
        public required string DonoId { get; init; }
        public required int Indice { get; init; }
        public required string Nome { get; init; }
        public required IReadOnlyList<string> Linhas { get; init; }
        public required Retangulo Documento { get; init; }
        public required Retangulo Rotulo { get; init; }
        public required List<Ponto> Conector { get; init; }
    }

    public sealed class RaiaDesenhada
    {
        public required PeFluxoRaia Raia { get; init; }
        public required Retangulo Area { get; init; }
        public required Retangulo Faixa { get; init; }
    }

    public sealed class Texto
    {
        public required string Conteudo { get; init; }
        // Início da linha (esquerda; na vertical, o pé) e a linha de base
        public required double X { get; init; }
        public required double Y { get; init; }
        public required double Tamanho { get; init; }
        public required bool Negrito { get; init; }
        public required double Largura { get; init; }
        public bool Vertical { get; init; }
        public string Cor { get; init; } = CorTexto;
        // A área que o texto ocupa (para conferir que não sai da caixa)
        public required Retangulo Area { get; init; }
        // O dono (id do elemento, "raia:ID", "pool", "artefato:ID:n", "ligacao:ID")
        public required string Dono { get; init; }
    }

    // ── Estado da montagem ──────────────────────────────────────────────────

    private enum Porta { Esquerda, Direita, Cima, Baixo, DireitaBaixo, EsquerdaBaixo }

    private enum PosicaoNome { Nenhum, Acima, Direita, Abaixo }

    private sealed class Medidas
    {
        public List<string> Numero = new();
        public List<string> Nome = new();
        public List<(string Nome, List<string> Linhas, double Largura)> Artefatos = new();
        public double LarguraArtefatos;
        public double AlturaArtefatos;
        public PosicaoNome PosicaoNome = PosicaoNome.Nenhum;
        public double LarguraNome;
        public double Cima;
        public double Baixo;
        public double Pegada;
        public double Altura;
        public double LarguraCaixa;
    }

    private PeFluxoAnalise _a = null!;
    private readonly Dictionary<string, Medidas> _m = new();
    private readonly Dictionary<string, Caixa> _caixa = new();
    private readonly Dictionary<PeFluxoLigacao, Porta> _saida = new();
    private readonly Dictionary<PeFluxoLigacao, Porta> _entrada = new();
    private readonly Dictionary<PeFluxoLigacao, int> _raiaDoCanal = new();
    private readonly Dictionary<PeFluxoLigacao, int> _indiceCanal = new();
    private readonly List<(Retangulo Area, string Nome)> _obstaculos = new();
    private readonly List<(Ponto A, Ponto B, PeFluxoLigacao Dona)> _segmentos = new();
    private double[] _colEsquerda = Array.Empty<double>();
    private double[] _colLargura = Array.Empty<double>();
    private double[] _gap = Array.Empty<double>();
    private readonly List<double> _corredores = new();
    // O esforço da busca das rotas (cada rota gerada e cada conferência com um obstáculo ou
    // com um trecho já desenhado): passado o limite, a busca fica econômica (Economico)
    private long _esforco;

    /// <summary>
    /// O limite do esforço da busca das rotas. Os fluxos do guia gastam uma fração pequena dele
    /// (há teste); só um desenho enorme, perto dos limites da definição (120 passos e 240
    /// ligações, com ligações que atravessam dezenas de colunas), chega nele. Daí em diante, cada
    /// ligação só tenta o vão e o corredor mais perto de cada ponta, sem pesar a sobreposição
    /// com as outras linhas: o desenho sai em tempo curto (a geometria é pedida a cada mudança
    /// do editor), talvez com uma linha passando por uma caixa ou por cima de outra. É uma
    /// contagem, não um relógio: a mesma definição sai sempre igual.
    /// </summary>
    public const long EsforcoMaximo = 4_000_000;

    // O custo de gerar e simplificar uma rota, em conferências
    private const long EsforcoPorRota = 50;

    private bool Economico => _esforco > EsforcoMaximo;

    /// <summary>O esforço que a busca das rotas gastou neste desenho (para conferir a folga até o limite).</summary>
    public long EsforcoDasRotas => _esforco;
    private readonly Dictionary<int, double> _canalBase = new();
    private double _alturaTarefa;
    private double _larguraFaixaRaia;
    private double _larguraFaixaPool;
    private (string Fundo, string Borda) _cor;
    private readonly PeFluxoFonte? _regular;
    private readonly PeFluxoFonte? _negrito;

    private PeFluxoDesenho(PeFluxoFonte? regular, PeFluxoFonte? negrito)
    {
        _regular = regular;
        _negrito = negrito ?? regular;
    }

    /// <summary>
    /// Desenha a definição (não é alterada). Os marcadores dos nomes são trocados pelos valores
    /// dados (chave sem as chaves: "nomes.comite"); marcador sem valor fica como está. O título
    /// vai para a faixa do fluxo (com duas raias ou mais) e para o &lt;title&gt; do SVG.
    /// </summary>
    public static PeFluxoDesenho Desenhar(PeFluxoDefinicao definicao, string? titulo, IReadOnlyDictionary<string, string?> nomes,
        bool usarFonte = true)
    {
        var desenho = usarFonte
            ? new PeFluxoDesenho(PeFluxoFonte.Regular, PeFluxoFonte.Negrito)
            : new PeFluxoDesenho(null, null);
        desenho.Montar(definicao, titulo, nomes);
        return desenho;
    }

    /// <summary>Troca os marcadores conhecidos de um texto (o sem valor fica escrito).</summary>
    public static string ResolverNome(string? texto, IReadOnlyDictionary<string, string?> nomes) =>
        PeDocMarcadores.ResolverTexto(texto ?? string.Empty, nomes, emBranco: false);

    private void Montar(PeFluxoDefinicao original, string? titulo, IReadOnlyDictionary<string, string?> nomes)
    {
        // Cópia com os nomes resolvidos e os números de hoje
        var definicao = PeFluxoDefinicaoLeitor.Copiar(original);
        // A definição lida com erros (na edição) pode ter buracos: nada nulo daqui em diante
        definicao.Raias ??= new List<PeFluxoRaia>();
        definicao.Elementos ??= new List<PeFluxoElemento>();
        definicao.Ligacoes ??= new List<PeFluxoLigacao>();
        definicao.Raias.RemoveAll(r => r is null);
        definicao.Elementos.RemoveAll(e => e is null);
        definicao.Ligacoes.RemoveAll(l => l is null);
        foreach (var r in definicao.Raias) r.Id ??= string.Empty;
        foreach (var e in definicao.Elementos)
        {
            e.Id ??= string.Empty;
            e.Tipo ??= string.Empty;
            e.RaiaId ??= string.Empty;
        }
        var nomesOriginais = definicao.Raias.Select(r => r.Nome ?? string.Empty).ToList();
        foreach (var r in definicao.Raias) r.Nome = PeFluxoDefinicaoLeitor.Limpar(ResolverNome(r.Nome, nomes));
        foreach (var e in definicao.Elementos)
        {
            e.Nome = PeFluxoDefinicaoLeitor.Limpar(ResolverNome(e.Nome, nomes));
            e.Artefatos = (e.Artefatos ?? new List<string>()).Select(a => PeFluxoDefinicaoLeitor.Limpar(ResolverNome(a, nomes))).Where(a => a.Length > 0).ToList();
        }
        foreach (var l in definicao.Ligacoes)
            l.Rotulo = string.IsNullOrWhiteSpace(l.Rotulo) ? null : PeFluxoDefinicaoLeitor.Limpar(ResolverNome(l.Rotulo, nomes));
        if (definicao.Raias.Count == 0) definicao.Raias.Add(new PeFluxoRaia { Id = "_", Nome = string.Empty, Ordem = 1 });
        PeFluxoAnalise.Numerar(definicao);

        Titulo = PeFluxoDefinicaoLeitor.Limpar(ResolverNome(titulo, nomes));
        _a = PeFluxoAnalise.Analisar(definicao);
        _cor = Paleta.TryGetValue((definicao.PrefixoNumeracao ?? string.Empty).Split('.')[0], out var cor) ? cor : PaletaPadrao;

        Medir();
        DefinirPortas();
        RaiasEFaixas();
        Colunas();
        Elementos();
        Rotear();
        RotulosDasLigacoes();
        Faixas();
        Descrever();
        Geometria = MontarGeometria(nomesOriginais);
    }

    // ── Texto ───────────────────────────────────────────────────────────────

    private double LarguraDe(string texto, double tamanho, bool negrito)
    {
        var fonte = negrito ? _negrito : _regular;
        return fonte != null ? fonte.Largura(texto, tamanho) : PeFluxoFonte.LarguraEstimada(texto, tamanho, negrito);
    }

    /// <summary>
    /// Quebra o texto em linhas que cabem na largura (palavra maior que a linha é cortada nas
    /// letras). Passou do máximo de linhas: a última termina em reticências.
    /// </summary>
    private List<string> Quebrar(string texto, double tamanho, bool negrito, double largura, int maximo)
    {
        var linhas = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return linhas;
        var atual = string.Empty;
        var fila = new LinkedList<string>(Palavras(texto));
        while (fila.Count > 0)
        {
            var palavra = fila.First!.Value;
            fila.RemoveFirst();
            var tentativa = atual.Length == 0 ? palavra : atual + " " + palavra;
            if (LarguraDe(tentativa, tamanho, negrito) <= largura)
            {
                atual = tentativa;
                continue;
            }
            // O trecho entre colchetes ou parênteses que não cabe nem numa linha inteira volta a ser palavras
            if (palavra.Contains(' ') && LarguraDe(palavra, tamanho, negrito) > largura)
            {
                var partes = palavra.Split(' ');
                for (var i = partes.Length - 1; i >= 0; i--) fila.AddFirst(partes[i]);
                continue;
            }
            if (atual.Length > 0) linhas.Add(atual);
            atual = palavra;
            // Palavra que sozinha não cabe: corta nas letras
            while (LarguraDe(atual, tamanho, negrito) > largura && atual.Length > 1)
            {
                var corte = atual.Length - 1;
                while (corte > 1 && LarguraDe(atual[..corte], tamanho, negrito) > largura) corte--;
                linhas.Add(atual[..corte]);
                atual = atual[corte..];
            }
        }
        if (atual.Length > 0) linhas.Add(atual);
        if (linhas.Count <= maximo) return linhas;

        var cortadas = linhas.Take(maximo).ToList();
        var ultima = cortadas[^1];
        while (ultima.Length > 1 && LarguraDe(ultima + "…", tamanho, negrito) > largura) ultima = ultima[..^1].TrimEnd();
        cortadas[^1] = ultima + "…";
        return cortadas;
    }

    /// <summary>
    /// As palavras do texto, com o trecho entre colchetes ou parênteses junto ("[situação
    /// atual]", "(parcial)"): ele só quebra no meio quando não cabe numa linha.
    /// </summary>
    private static List<string> Palavras(string texto)
    {
        var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var saida = new List<string>();
        for (var i = 0; i < partes.Length; i++)
        {
            var parte = partes[i];
            var fecha = parte[0] == '[' ? ']' : parte[0] == '(' ? ')' : (char?)null;
            if (fecha == null || parte.TrimEnd('.', ',', ';', ':', '?', '!').EndsWith(fecha.Value))
            {
                saida.Add(parte);
                continue;
            }
            var j = i;
            var grupo = parte;
            while (j + 1 < partes.Length && !partes[j].TrimEnd('.', ',', ';', ':', '?', '!').EndsWith(fecha.Value))
            {
                j++;
                grupo += " " + partes[j];
            }
            saida.Add(grupo);
            i = j;
        }
        return saida;
    }

    private double LarguraMaxima(IEnumerable<string> linhas, double tamanho, bool negrito) =>
        linhas.Select(l => LarguraDe(l, tamanho, negrito)).DefaultIfEmpty(0).Max();

    /// <summary>Uma linha de texto: começa em x (na horizontal) com a linha de base em y.</summary>
    private void Escrever(string conteudo, double x, double y, double tamanho, bool negrito, string dono, string cor = CorTexto)
    {
        var largura = LarguraDe(conteudo, tamanho, negrito);
        Textos.Add(new Texto
        {
            Conteudo = conteudo,
            X = x,
            Y = y,
            Tamanho = tamanho,
            Negrito = negrito,
            Largura = largura,
            Cor = cor,
            Area = new Retangulo(x, y - tamanho * 0.78, largura, tamanho),
            Dono = dono
        });
    }

    /// <summary>Linhas centradas em cx, com o bloco começando (topo) em y.</summary>
    private void Bloco(IReadOnlyList<string> linhas, double cx, double topo, double tamanho, double altura, bool negrito, string dono,
        string cor = CorTexto)
    {
        for (var i = 0; i < linhas.Count; i++)
        {
            var largura = LarguraDe(linhas[i], tamanho, negrito);
            Escrever(linhas[i], cx - largura / 2, topo + i * altura + (altura + tamanho * AlturaMaiuscula) / 2, tamanho, negrito, dono, cor);
        }
    }

    /// <summary>Linhas na vertical (de baixo para cima), centradas no ponto (cx, cy).</summary>
    private void BlocoVertical(IReadOnlyList<string> linhas, double cx, double cy, double tamanho, double altura, string dono)
    {
        var inicio = cx - linhas.Count * altura / 2;
        for (var i = 0; i < linhas.Count; i++)
        {
            var largura = LarguraDe(linhas[i], tamanho, true);
            var baseX = inicio + i * altura + (altura + tamanho * AlturaMaiuscula) / 2;
            Textos.Add(new Texto
            {
                Conteudo = linhas[i],
                X = baseX,
                Y = cy + largura / 2,
                Tamanho = tamanho,
                Negrito = true,
                Largura = largura,
                Vertical = true,
                Area = new Retangulo(baseX - tamanho * 0.78, cy - largura / 2, tamanho, largura),
                Dono = dono
            });
        }
    }

    // ── 1. Medidas ──────────────────────────────────────────────────────────

    private void Medir()
    {
        var larguraTexto = LarguraTarefa - 2 * PadTarefaX;
        var maiorTarefa = AlturaMinimaTarefa;
        foreach (var e in _a.Elementos)
        {
            var m = new Medidas();
            _m[e.Id] = m;
            switch (e.Tipo)
            {
                case PeDominios.TipoElementoFluxo.Tarefa:
                case PeDominios.TipoElementoFluxo.Subprocesso:
                {
                    if (e.Numero != null) m.Numero.Add(e.Numero);
                    m.Nome = Quebrar(e.Nome, FonteTarefa, false, larguraTexto, MaximoLinhasTarefa);
                    var altura = 2 * PadTarefaY + (m.Numero.Count + Math.Max(1, m.Nome.Count)) * LinhaTarefa
                                 + (e.Tipo == PeDominios.TipoElementoFluxo.Subprocesso ? MarcaSubprocesso : 0);
                    maiorTarefa = Math.Max(maiorTarefa, altura);

                    var k = e.Artefatos.Count;
                    if (k > 0)
                    {
                        var maximoSlot = k == 1 ? LarguraTarefa + 10 : 80;
                        foreach (var nome in e.Artefatos)
                        {
                            var natural = LarguraDe(nome, FonteArtefato, false);
                            var largura = Math.Max(DocLargura + 12, Math.Min(maximoSlot, natural + 4));
                            var linhas = Quebrar(nome, FonteArtefato, false, largura - 2, 4);
                            m.Artefatos.Add((nome, linhas, largura));
                        }
                        m.LarguraArtefatos = m.Artefatos.Sum(x => x.Largura) + (k - 1) * 6;
                        m.AlturaArtefatos = ConectorArtefato + DocAltura + 3 + m.Artefatos.Max(x => x.Linhas.Count) * LinhaArtefato;
                    }
                    break;
                }
                case PeDominios.TipoElementoFluxo.Decisao:
                case PeDominios.TipoElementoFluxo.Paralelo:
                    if (e.Nome.Length > 0) m.Nome = Quebrar(e.Nome, FonteRotulo, false, LarguraRotuloPorta, 4);
                    m.LarguraNome = LarguraMaxima(m.Nome, FonteRotulo, false);
                    break;
                default:
                    if (e.Nome.Length > 0) m.Nome = Quebrar(e.Nome, FonteRotulo, false, LarguraRotuloEvento, 4);
                    m.LarguraNome = LarguraMaxima(m.Nome, FonteRotulo, false);
                    break;
            }
        }
        _alturaTarefa = Math.Ceiling(maiorTarefa);
    }

    // ── 2. Portas (de onde sai e por onde entra cada ligação) ────────────────

    /// <summary>-1: o outro está acima; 1: abaixo; 0: na mesma linha (raia e faixa).</summary>
    private int Relativo(PeFluxoElemento de, PeFluxoElemento outro)
    {
        int ra = _a.RaiaDe(de), rb = _a.RaiaDe(outro);
        if (rb != ra) return rb < ra ? -1 : 1;
        int fa = _a.Faixa[de.Id], fb = _a.Faixa[outro.Id];
        return fb == fa ? 0 : (fb < fa ? -1 : 1);
    }

    private void DefinirPortas()
    {
        foreach (var l in _a.Ligacoes)
        {
            var de = _a.PorId[l.De];
            var para = _a.PorId[l.Para];
            if (_a.EhRetorno(l))
            {
                _saida[l] = PeDominios.TipoElementoFluxo.EhAtividade(de.Tipo) && de.Artefatos.Count > 0 ? Porta.DireitaBaixo : Porta.Baixo;
                _entrada[l] = PeDominios.TipoElementoFluxo.EhAtividade(para.Tipo) && para.Artefatos.Count > 0 ? Porta.EsquerdaBaixo : Porta.Baixo;
                continue;
            }
            var rel = Relativo(de, para);
            _saida[l] = PeDominios.TipoElementoFluxo.EhPorta(de.Tipo)
                ? rel < 0 ? Porta.Cima : rel > 0 ? Porta.Baixo : Porta.Direita
                : Porta.Direita;
            // O losango recebe por cima ou por baixo só quem vem da mesma raia (a junção dos
            // caminhos paralelos); quem vem de outra raia entra pela ponta da esquerda
            var mesmaRaia = _a.RaiaDe(de) == _a.RaiaDe(para);
            _entrada[l] = PeDominios.TipoElementoFluxo.EhPorta(para.Tipo) && mesmaRaia
                ? rel > 0 ? Porta.Cima : rel < 0 ? Porta.Baixo : Porta.Esquerda
                : Porta.Esquerda;
        }

        // O retorno que sai de um losango fica com a ponta de baixo; a saída de avanço que
        // desceria por ela sai pela direita
        foreach (var e in _a.Elementos.Where(e => PeDominios.TipoElementoFluxo.EhPorta(e.Tipo)))
        {
            if (!_a.Saidas[e.Id].Any(_a.EhRetorno)) continue;
            foreach (var l in _a.SaidasDeAvanco(e.Id).Where(l => _saida[l] == Porta.Baixo).ToList()) _saida[l] = Porta.Direita;
        }

        // Onde vai o nome do losango: acima, à direita ou abaixo, na primeira ponta livre
        foreach (var e in _a.Elementos.Where(e => PeDominios.TipoElementoFluxo.EhPorta(e.Tipo) && _m[e.Id].Nome.Count > 0))
        {
            var usadas = _a.Saidas[e.Id].Select(l => _saida[l]).Concat(_a.Entradas[e.Id].Select(l => _entrada[l])).ToHashSet();
            var m = _m[e.Id];
            m.PosicaoNome = !usadas.Contains(Porta.Cima) ? PosicaoNome.Acima
                : !usadas.Contains(Porta.Direita) ? PosicaoNome.Direita
                : !usadas.Contains(Porta.Baixo) ? PosicaoNome.Abaixo
                : PosicaoNome.Acima;
        }

        // Extensões de cada elemento acima e abaixo do centro, e a largura que ocupa na coluna
        foreach (var e in _a.Elementos)
        {
            var m = _m[e.Id];
            var alturaNome = m.Nome.Count * LinhaRotulo;
            switch (e.Tipo)
            {
                case PeDominios.TipoElementoFluxo.Tarefa:
                case PeDominios.TipoElementoFluxo.Subprocesso:
                    m.Altura = _alturaTarefa;
                    m.LarguraCaixa = LarguraTarefa;
                    m.Cima = _alturaTarefa / 2;
                    m.Baixo = _alturaTarefa / 2 + m.AlturaArtefatos;
                    m.Pegada = Math.Max(LarguraTarefa, m.LarguraArtefatos);
                    break;
                case PeDominios.TipoElementoFluxo.Decisao:
                case PeDominios.TipoElementoFluxo.Paralelo:
                    m.Altura = 2 * MeiaPorta;
                    m.LarguraCaixa = 2 * MeiaPorta;
                    m.Cima = MeiaPorta + (m.PosicaoNome == PosicaoNome.Acima ? 6 + alturaNome : 0);
                    m.Baixo = MeiaPorta + (m.PosicaoNome == PosicaoNome.Abaixo ? 6 + alturaNome : 0);
                    if (m.PosicaoNome == PosicaoNome.Direita)
                    {
                        m.Cima = Math.Max(m.Cima, alturaNome / 2 + 2);
                        m.Baixo = Math.Max(m.Baixo, alturaNome / 2 + 2);
                    }
                    m.Pegada = m.PosicaoNome is PosicaoNome.Acima or PosicaoNome.Abaixo ? Math.Max(2 * MeiaPorta, m.LarguraNome) : 2 * MeiaPorta;
                    break;
                default:
                    m.Altura = 2 * RaioEvento;
                    m.LarguraCaixa = 2 * RaioEvento;
                    m.Cima = RaioEvento;
                    m.Baixo = RaioEvento + (m.Nome.Count > 0 ? 5 + alturaNome : 0);
                    m.Pegada = Math.Max(2 * RaioEvento, m.LarguraNome);
                    break;
            }
        }
    }

    // ── 3. Colunas ──────────────────────────────────────────────────────────

    private void Colunas()
    {
        var n = Math.Max(1, _a.Camadas);
        _colLargura = new double[n];
        _gap = Enumerable.Repeat(GapColuna, n).ToArray();
        foreach (var e in _a.Elementos)
            _colLargura[_a.Camada[e.Id]] = Math.Max(_colLargura[_a.Camada[e.Id]], _m[e.Id].Pegada);

        // O espaço depois da coluna cabe o nome do losango à direita e o rótulo da saída pela direita
        foreach (var e in _a.Elementos)
        {
            var k = _a.Camada[e.Id];
            var m = _m[e.Id];
            var meia = m.LarguraCaixa / 2;
            var sobra = (_colLargura[k] / 2) - meia;
            if (m.PosicaoNome == PosicaoNome.Direita)
                _gap[k] = Math.Max(_gap[k], 6 + m.LarguraNome + 12 - sobra);
            foreach (var l in _a.Saidas[e.Id].Where(l => l.Rotulo != null && _saida[l] == Porta.Direita))
                _gap[k] = Math.Max(_gap[k], 5 + LarguraDe(l.Rotulo!, FonteRotulo, false) + 16 - sobra);
        }

        // Depois das faixas da esquerda (a do fluxo, com duas raias ou mais, e a das raias)
        _colEsquerda = new double[n];
        var x = Margem + _larguraFaixaPool + _larguraFaixaRaia + InicioConteudo;
        for (var k = 0; k < n; k++)
        {
            _colEsquerda[k] = x;
            x += _colLargura[k] + _gap[k];
        }
    }

    private double Centro(int camada) => _colEsquerda[camada] + _colLargura[camada] / 2;

    private double GapX(int camada) => _colEsquerda[camada] + _colLargura[camada] + _gap[camada] / 2;

    // ── 4. Raias e faixas (as linhas de cada raia) ───────────────────────────

    private readonly Dictionary<(int Raia, int Faixa), double> _centroFaixa = new();
    private readonly List<(double Topo, double Base, double BaseConteudo)> _raiaY = new();
    private readonly List<List<string>> _linhasRaia = new();
    private List<string> _linhasPool = new();

    /// <summary>
    /// A altura de cada raia (as faixas de linhas, uma embaixo da outra, e os canais dos
    /// retornos embaixo), com o nome da raia na vertical cabendo em até três linhas, e a
    /// largura das faixas da esquerda.
    /// </summary>
    private void RaiasEFaixas()
    {
        // Canais dos retornos: na raia mais baixa entre a de saída e a de chegada; o retorno
        // mais curto fica mais perto do conteúdo
        var retornos = _a.Ligacoes.Where(_a.EhRetorno).ToList();
        foreach (var l in retornos)
            _raiaDoCanal[l] = Math.Max(_a.RaiaDe(_a.PorId[l.De]), _a.RaiaDe(_a.PorId[l.Para]));
        foreach (var grupo in retornos.GroupBy(l => _raiaDoCanal[l]))
        {
            var i = 0;
            foreach (var l in grupo.OrderBy(l => Math.Abs(_a.Camada[l.De] - _a.Camada[l.Para])).ThenBy(l => _a.Camada[l.Para]))
                _indiceCanal[l] = i++;
        }

        var y = Margem;
        for (var r = 0; r < _a.Raias.Count; r++)
        {
            var daRaia = _a.Elementos.Where(e => _a.RaiaDe(e) == r).ToList();
            var faixas = daRaia.Select(e => _a.Faixa[e.Id]).Distinct().OrderBy(f => f).ToList();
            if (faixas.Count == 0) faixas.Add(0);
            var topo = y;

            // Quanto cada linha ocupa acima e abaixo do centro, coluna por coluna (nomes,
            // artefatos), e a metade da forma (sem eles)
            var cima = new Dictionary<(int Faixa, int Camada), double>();
            var baixo = new Dictionary<(int Faixa, int Camada), double>();
            var metade = faixas.ToDictionary(f => f, _ => 0.0);
            foreach (var e in daRaia)
            {
                var chave = (_a.Faixa[e.Id], _a.Camada[e.Id]);
                cima[chave] = Math.Max(cima.GetValueOrDefault(chave), _m[e.Id].Cima);
                baixo[chave] = Math.Max(baixo.GetValueOrDefault(chave), _m[e.Id].Baixo);
                metade[chave.Item1] = Math.Max(metade[chave.Item1], _m[e.Id].Altura / 2);
            }
            foreach (var f in faixas.Where(f => metade[f] == 0)) metade[f] = _alturaTarefa / 2;

            // Uma linha logo abaixo da outra: a distância só precisa separar o que está na mesma
            // coluna (o artefato de uma tarefa não empurra a linha de baixo onde não há nada embaixo)
            var centros = new Dictionary<int, double>();
            var primeira = faixas[0];
            centros[primeira] = topo + PadRaiaTopo
                + Math.Max(metade[primeira], cima.Where(x => x.Key.Faixa == primeira).Select(x => x.Value).DefaultIfEmpty(0).Max());
            for (var j = 1; j < faixas.Count; j++)
            {
                var fj = faixas[j];
                var centro = centros[faixas[j - 1]] + metade[faixas[j - 1]] + GapFaixa + metade[fj];
                for (var i = 0; i < j; i++)
                {
                    var fi = faixas[i];
                    foreach (var (chave, abaixo) in baixo.Where(x => x.Key.Faixa == fi))
                        if (cima.TryGetValue((fj, chave.Camada), out var acima))
                            centro = Math.Max(centro, centros[fi] + abaixo + GapFaixa + acima);
                }
                centros[fj] = centro;
            }
            var corredores = new List<double> { topo + 9 };
            for (var j = 1; j < faixas.Count; j++)
                corredores.Add((centros[faixas[j - 1]] + metade[faixas[j - 1]] + centros[faixas[j]] - metade[faixas[j]]) / 2);
            foreach (var f in faixas) _centroFaixa[(r, f)] = centros[f];

            var baseConteudo = daRaia.Count == 0
                ? topo + PadRaiaTopo + _alturaTarefa
                : daRaia.Max(e => centros[_a.Faixa[e.Id]] + _m[e.Id].Baixo);
            corredores.Add(baseConteudo + 7);
            var canais = retornos.Count(l => _raiaDoCanal[l] == r);
            var canalBase = baseConteudo + 12;
            var natural = (canais > 0 ? canalBase + (canais - 1) * EspacoCanal + 8 : baseConteudo) + PadRaiaBase - topo;

            // O nome da raia na vertical: de preferência em até duas linhas (a raia cresce um
            // pouco se precisar); no máximo três
            var altura = Math.Max(AlturaMinimaRaia, natural);
            var nome = _a.Raias[r].Nome;
            if (Quebrar(nome, FonteRaia, true, altura - 16, 99).Count > 2)
            {
                var paraDuas = altura;
                while (paraDuas < altura + 80 && Quebrar(nome, FonteRaia, true, paraDuas - 16, 99).Count > 2) paraDuas += 8;
                if (Quebrar(nome, FonteRaia, true, paraDuas - 16, 99).Count <= 2) altura = paraDuas;
            }
            for (var tentativa = 0; Quebrar(nome, FonteRaia, true, altura - 16, 99).Count > 3 && tentativa < 400; tentativa++) altura += 12;
            _linhasRaia.Add(Quebrar(nome, FonteRaia, true, altura - 16, 3));

            // Raia mais alta que o conteúdo: o conteúdo fica no meio
            var desloca = (altura - natural) / 2;
            foreach (var f in faixas) _centroFaixa[(r, f)] += desloca;
            _corredores.AddRange(corredores.Select(c => c + desloca));
            _canalBase[r] = canalBase + desloca;
            _raiaY.Add((topo, topo + altura, baseConteudo + desloca));
            y = topo + altura;
        }
        Altura = y + Margem;

        // Larguras das faixas da esquerda
        _larguraFaixaRaia = Math.Max(28, 12 + _linhasRaia.Select(l => l.Count).DefaultIfEmpty(1).Max() * LinhaRaia);
        if (_a.Raias.Count > 1 && Titulo.Length > 0)
        {
            _linhasPool = Quebrar(Titulo, FontePool, true, Altura - 2 * Margem - 16, 2);
            _larguraFaixaPool = Math.Max(30, 12 + _linhasPool.Count * LinhaPool);
        }
    }

    // ── 5. Elementos ─────────────────────────────────────────────────────────

    private void Elementos()
    {
        foreach (var e in _a.Elementos)
        {
            var r = _a.RaiaDe(e);
            var k = _a.Camada[e.Id];
            var f = _a.Faixa[e.Id];
            var m = _m[e.Id];
            var cx = Centro(k);
            var cy = _centroFaixa[(r, f)];
            var area = new Retangulo(cx - m.LarguraCaixa / 2, cy - m.Altura / 2, m.LarguraCaixa, m.Altura);
            var caixa = new Caixa { Elemento = e, Area = area, Raia = r, Camada = k, Faixa = f };
            Caixas.Add(caixa);
            _caixa[e.Id] = caixa;
            _obstaculos.Add((area, e.Id));

            switch (e.Tipo)
            {
                case PeDominios.TipoElementoFluxo.Tarefa:
                case PeDominios.TipoElementoFluxo.Subprocesso:
                {
                    var linhas = m.Numero.Count + Math.Max(1, m.Nome.Count);
                    var marca = e.Tipo == PeDominios.TipoElementoFluxo.Subprocesso ? MarcaSubprocesso : 0;
                    var topo = area.Y + (area.A - marca - linhas * LinhaTarefa) / 2;
                    if (m.Numero.Count > 0) Bloco(m.Numero, cx, topo, FonteTarefa, LinhaTarefa, true, e.Id);
                    Bloco(m.Nome, cx, topo + m.Numero.Count * LinhaTarefa, FonteTarefa, LinhaTarefa, false, e.Id);
                    ArtefatosDe(e, caixa, m);
                    break;
                }
                case PeDominios.TipoElementoFluxo.Decisao:
                case PeDominios.TipoElementoFluxo.Paralelo:
                    if (m.Nome.Count == 0) break;
                    var alturaNome = m.Nome.Count * LinhaRotulo;
                    switch (m.PosicaoNome)
                    {
                        case PosicaoNome.Direita:
                        {
                            var x0 = cx + MeiaPorta + 6;
                            for (var i = 0; i < m.Nome.Count; i++)
                                Escrever(m.Nome[i], x0, cy - alturaNome / 2 + i * LinhaRotulo + (LinhaRotulo + FonteRotulo * AlturaMaiuscula) / 2,
                                    FonteRotulo, false, e.Id);
                            break;
                        }
                        case PosicaoNome.Abaixo:
                            Bloco(m.Nome, cx, cy + MeiaPorta + 4, FonteRotulo, LinhaRotulo, false, e.Id);
                            break;
                        default:
                            Bloco(m.Nome, cx, cy - MeiaPorta - 4 - alturaNome, FonteRotulo, LinhaRotulo, false, e.Id);
                            break;
                    }
                    break;
                default:
                    if (m.Nome.Count > 0) Bloco(m.Nome, cx, cy + RaioEvento + 3, FonteRotulo, LinhaRotulo, false, e.Id);
                    break;
            }
        }
        // Os nomes dos losangos e dos eventos também são obstáculos das ligações
        foreach (var t in Textos) _obstaculos.Add((t.Area.Inflar(1), $"texto {t.Dono}: {t.Conteudo}"));
    }

    private void ArtefatosDe(PeFluxoElemento e, Caixa caixa, Medidas m)
    {
        if (m.Artefatos.Count == 0) return;
        var x = caixa.Cx - m.LarguraArtefatos / 2;
        var topoDoc = caixa.Area.Base + ConectorArtefato;
        for (var i = 0; i < m.Artefatos.Count; i++)
        {
            var (nome, linhas, largura) = m.Artefatos[i];
            var dx = x + largura / 2;
            var doc = new Retangulo(dx - DocLargura / 2, topoDoc, DocLargura, DocAltura);
            var saida = Math.Clamp(dx, caixa.Area.X + 10, caixa.Area.Direita - 10);
            // No subprocesso, a linha sai ao lado da marca "+" (no meio, embaixo)
            if (e.Tipo == PeDominios.TipoElementoFluxo.Subprocesso && Math.Abs(saida - caixa.Cx) < 12) saida = caixa.Cx + 14;
            var conector = new List<Ponto>
            {
                new(saida, caixa.Area.Base),
                new(saida, caixa.Area.Base + ConectorArtefato / 2),
                new(dx, caixa.Area.Base + ConectorArtefato / 2),
                new(dx, topoDoc - 1)
            };
            var alturaRotulo = linhas.Count * LinhaArtefato;
            var rotulo = new Retangulo(x, doc.Base + 3, largura, alturaRotulo);
            Bloco(linhas, dx, doc.Base + 3, FonteArtefato, LinhaArtefato, false, $"artefato:{e.Id}:{i}");
            Artefatos.Add(new Artefato { DonoId = e.Id, Indice = i, Nome = nome, Linhas = linhas, Documento = doc, Rotulo = rotulo, Conector = conector });
            _obstaculos.Add((doc.Inflar(2), $"documento {e.Id}:{i}"));
            _obstaculos.Add((new Retangulo(x, caixa.Area.Base + 2, largura, doc.Base - caixa.Area.Base), $"conector {e.Id}:{i}"));
            x += largura + 6;
        }
    }

    // ── 6. Rotas das ligações ────────────────────────────────────────────────

    private Ponto PontoDa(Caixa c, Porta porta) => porta switch
    {
        Porta.Esquerda => new Ponto(c.Area.X, c.Cy),
        Porta.Direita => new Ponto(c.Area.Direita, c.Cy),
        Porta.Cima => new Ponto(c.Cx, c.Area.Y),
        Porta.Baixo => new Ponto(c.Cx, c.Area.Base),
        Porta.DireitaBaixo => new Ponto(c.Area.Direita, c.Cy + c.Area.A / 4),
        Porta.EsquerdaBaixo => new Ponto(c.Area.X, c.Cy + c.Area.A / 4),
        _ => new Ponto(c.Cx, c.Cy)
    };

    private void Rotear()
    {
        // Avanço primeiro (da esquerda para a direita), depois os retornos
        var avanco = _a.Ligacoes.Where(l => !_a.EhRetorno(l))
            .OrderBy(l => _a.Camada[l.De]).ThenBy(l => _a.Camada[l.Para]).ThenBy(l => _a.Ligacoes.IndexOf(l))
            .ToList();
        foreach (var l in avanco) Registrar(l, false, RotaDeAvanco(l));
        foreach (var l in _a.Ligacoes.Where(_a.EhRetorno)) Registrar(l, true, RotaDeRetorno(l));
    }

    private void Registrar(PeFluxoLigacao l, bool retorno, (List<Ponto> Pontos, List<string> Atingidos) rota)
    {
        var pontos = Simplificar(rota.Pontos);
        Linhas.Add(new Linha { Ligacao = l, Retorno = retorno, Pontos = pontos, Colisoes = rota.Atingidos.Count, Atingidos = rota.Atingidos });
        for (var i = 0; i + 1 < pontos.Count; i++) _segmentos.Add((pontos[i], pontos[i + 1], l));
    }

    private static List<Ponto> Simplificar(List<Ponto> pontos)
    {
        var saida = new List<Ponto>();
        foreach (var p in pontos)
        {
            if (saida.Count > 0 && Math.Abs(saida[^1].X - p.X) < 0.01 && Math.Abs(saida[^1].Y - p.Y) < 0.01) continue;
            if (saida.Count >= 2)
            {
                var a = saida[^2];
                var b = saida[^1];
                var colinear = (Math.Abs(a.X - b.X) < 0.01 && Math.Abs(b.X - p.X) < 0.01) || (Math.Abs(a.Y - b.Y) < 0.01 && Math.Abs(b.Y - p.Y) < 0.01);
                if (colinear) saida.RemoveAt(saida.Count - 1);
            }
            saida.Add(p);
        }
        return saida;
    }

    private (List<Ponto> Pontos, List<string> Atingidos) RotaDeAvanco(PeFluxoLigacao l)
    {
        var de = _caixa[l.De];
        var para = _caixa[l.Para];
        var ps = _saida[l];
        var pe = _entrada[l];
        var a = PontoDa(de, ps);
        var b = PontoDa(para, pe);
        var gaps = Enumerable.Range(de.Camada, Math.Max(0, para.Camada - de.Camada)).Select(GapX).ToList();
        if (gaps.Count == 0) gaps.Add((a.X + b.X) / 2);
        IReadOnlyList<double> corredores = _corredores;
        if (Economico)
        {
            // Desenho grande demais (o esforço passou do limite): só o vão perto de quem sai e o
            // perto de quem chega, e o corredor mais perto de cada ponta
            gaps = PertoDasPontas(gaps, 1);
            corredores = PertoDe(_corredores, a.Y, b.Y, 1);
        }
        var saiNaVertical = ps is Porta.Cima or Porta.Baixo;
        var entraNaVertical = pe is Porta.Cima or Porta.Baixo;
        return Melhor(Candidatos(), l);

        // As rotas na ordem de preferência (feitas uma a uma: a busca para no limite do esforço)
        IEnumerable<List<Ponto>> Candidatos()
        {
            if (!saiNaVertical && !entraNaVertical)
            {
                if (Math.Abs(a.Y - b.Y) < 0.5) yield return new List<Ponto> { a, b };
                foreach (var gx in gaps.AsEnumerable().Reverse())
                    yield return new List<Ponto> { a, new(gx, a.Y), new(gx, b.Y), b };
                foreach (var g1 in gaps)
                    foreach (var g2 in gaps.Where(g => g > g1))
                        foreach (var yc in corredores)
                            yield return new List<Ponto> { a, new(g1, a.Y), new(g1, yc), new(g2, yc), new(g2, b.Y), b };
            }
            else if (saiNaVertical && !entraNaVertical)
            {
                yield return new List<Ponto> { a, new(a.X, b.Y), b };
                foreach (var gx in gaps.AsEnumerable().Reverse())
                    foreach (var yc in corredores.Where(y => ps == Porta.Cima ? y < a.Y : y > a.Y))
                        yield return new List<Ponto> { a, new(a.X, yc), new(gx, yc), new(gx, b.Y), b };
            }
            else if (!saiNaVertical && entraNaVertical)
            {
                yield return new List<Ponto> { a, new(b.X, a.Y), b };
                foreach (var gx in gaps)
                    foreach (var yc in corredores.Where(y => pe == Porta.Cima ? y < b.Y : y > b.Y))
                        yield return new List<Ponto> { a, new(gx, a.Y), new(gx, yc), new(b.X, yc), b };
            }
            else
            {
                var meio = (a.Y + b.Y) / 2;
                yield return new List<Ponto> { a, new(a.X, meio), new(b.X, meio), b };
                foreach (var yc in corredores)
                    yield return new List<Ponto> { a, new(a.X, yc), new(b.X, yc), b };
            }
        }
    }

    /// <summary>Os primeiros e os últimos vãos (na ordem): perto de quem sai e de quem chega.</summary>
    private static List<double> PertoDasPontas(List<double> gaps, int quantos) =>
        gaps.Count <= 2 * quantos ? gaps : gaps.Take(quantos).Concat(gaps.Skip(gaps.Count - quantos)).ToList();

    /// <summary>Os corredores mais perto de cada uma das duas alturas, na ordem original.</summary>
    private static List<double> PertoDe(List<double> corredores, double y1, double y2, int quantos)
    {
        var escolhidos = corredores.OrderBy(c => Math.Abs(c - y1)).Take(quantos)
            .Concat(corredores.OrderBy(c => Math.Abs(c - y2)).Take(quantos))
            .ToHashSet();
        return corredores.Where(escolhidos.Contains).Distinct().ToList();
    }

    private (List<Ponto> Pontos, List<string> Atingidos) RotaDeRetorno(PeFluxoLigacao l)
    {
        var de = _caixa[l.De];
        var para = _caixa[l.Para];
        var ps = _saida[l];
        var pe = _entrada[l];
        var a = PontoDa(de, ps);
        var b = PontoDa(para, pe);
        var raia = _raiaDoCanal[l];
        var canal = _canalBase[raia] + _indiceCanal[l] * EspacoCanal;
        var candidatos = new List<List<Ponto>>();

        // Saídas possíveis até o canal: direto para baixo, ou pelo vão à direita da coluna
        var descidas = new List<List<Ponto>>();
        if (ps == Porta.Baixo) descidas.Add(new List<Ponto> { a, new(a.X, canal) });
        var gxDe = GapX(de.Camada);
        var abaixo = ps == Porta.Baixo ? a.Y + 8 : a.Y;
        descidas.Add(new List<Ponto> { a, new(a.X, abaixo), new(gxDe, abaixo), new(gxDe, canal) });

        // Chegadas: subindo direto até a ponta de baixo, ou por um vão à esquerda (colado na
        // caixa, antes dos artefatos mais largos que ela, ou no meio do vão entre as colunas)
        var subidas = new List<List<Ponto>>();
        if (pe == Porta.Baixo) subidas.Add(new List<Ponto> { new(b.X, canal), b });
        var chegadaEsquerda = pe == Porta.EsquerdaBaixo ? b : PontoDa(para, PeDominios.TipoElementoFluxo.EhAtividade(para.Elemento.Tipo) ? Porta.EsquerdaBaixo : Porta.Esquerda);
        var limite = para.Camada > 0 ? _colEsquerda[para.Camada - 1] + _colLargura[para.Camada - 1] + 4 : Margem + _larguraFaixaPool + _larguraFaixaRaia + 4;
        var grupo = _m[para.Elemento.Id].LarguraArtefatos > para.Area.L ? para.Cx - _m[para.Elemento.Id].LarguraArtefatos / 2 : para.Area.X;
        var xs = new List<double> { para.Area.X - 10, grupo - 8 };
        if (para.Camada > 0) xs.AddRange(new[] { GapX(para.Camada - 1) - 6, GapX(para.Camada - 1), GapX(para.Camada - 1) + 6 });
        foreach (var x in xs.Where(x => x > limite && x < para.Area.X - 3).Distinct())
            subidas.Add(new List<Ponto> { new(x, canal), new(x, chegadaEsquerda.Y), chegadaEsquerda });
        if (subidas.Count == 0) subidas.Add(new List<Ponto> { new(para.Area.X - 10, canal), new(para.Area.X - 10, chegadaEsquerda.Y), chegadaEsquerda });

        foreach (var d in descidas)
            foreach (var s in subidas)
                candidatos.Add(d.Concat(s).ToList());
        return Melhor(candidatos, l);
    }

    /// <summary>
    /// A rota com menos batidas em caixas, depois com menos sobreposição, curvas e comprimento.
    /// A rota cujo custo sem olhar os obstáculos (curvas e comprimento) já não ganha da melhor
    /// nem é conferida (o resultado é o mesmo: o custo inteiro só pode ser maior). Passado o
    /// limite do esforço, fica a melhor achada até ali, e as ligações seguintes não pesam a
    /// sobreposição com as outras linhas.
    /// </summary>
    private (List<Ponto> Pontos, List<string> Atingidos) Melhor(IEnumerable<List<Ponto>> candidatos, PeFluxoLigacao l)
    {
        var de = _caixa[l.De].Area;
        var para = _caixa[l.Para].Area;
        List<Ponto>? melhor = null;
        List<Ponto>? primeiro = null;
        var menor = double.MaxValue;
        var atingidosDaMelhor = new List<string>();
        var normal = !Economico;
        var segmentos = normal ? _segmentos : new List<(Ponto A, Ponto B, PeFluxoLigacao Dona)>();
        foreach (var c in candidatos)
        {
            // O esforço passou do limite no meio desta ligação: fica a melhor achada até aqui
            if (normal && melhor != null && Economico) break;
            primeiro ??= c;
            _esforco += EsforcoPorRota;
            var pontos = Simplificar(c);
            double comprimento = 0;
            for (var i = 0; i + 1 < pontos.Count; i++)
                comprimento += Math.Abs(pontos[i].X - pontos[i + 1].X) + Math.Abs(pontos[i].Y - pontos[i + 1].Y);
            if ((pontos.Count - 2) * 30 + comprimento * 0.05 >= menor) continue;

            var atingidos = new List<string>();
            double sobreposicao = 0, cruzamentos = 0;
            for (var i = 0; i + 1 < pontos.Count; i++)
            {
                var p = pontos[i];
                var q = pontos[i + 1];
                foreach (var (o, nome) in _obstaculos)
                {
                    if (o.Equals(de) || o.Equals(para)) continue;
                    if (Bate(p, q, o)) atingidos.Add(nome);
                }
                // Atravessar a própria caixa de origem ou de destino (fora das pontas) também conta
                if (i > 0 && Bate(p, q, de.Inflar(-2))) atingidos.Add("origem");
                if (i + 2 < pontos.Count && Bate(p, q, para.Inflar(-2))) atingidos.Add("destino");
                foreach (var (s1, s2, dona) in segmentos)
                {
                    if (dona.Para == l.Para || dona.De == l.De) continue;
                    sobreposicao += Sobreposicao(p, q, s1, s2);
                    if (Cruza(p, q, s1, s2)) cruzamentos++;
                }
            }
            _esforco += (long)(pontos.Count - 1) * (_obstaculos.Count + segmentos.Count);
            var custo = atingidos.Count * 10000 + sobreposicao * 40 + cruzamentos * 6 + (pontos.Count - 2) * 30 + comprimento * 0.05;
            if (custo < menor)
            {
                menor = custo;
                melhor = pontos;
                atingidosDaMelhor = atingidos;
            }
        }
        return (melhor ?? primeiro ?? new List<Ponto>(), atingidosDaMelhor);
    }

    private static bool Bate(Ponto p, Ponto q, Retangulo r)
    {
        const double folga = 0.5;
        if (Math.Abs(p.Y - q.Y) < 0.01)
        {
            var (x1, x2) = (Math.Min(p.X, q.X), Math.Max(p.X, q.X));
            return p.Y > r.Y + folga && p.Y < r.Base - folga && x2 > r.X + folga && x1 < r.Direita - folga;
        }
        var (y1, y2) = (Math.Min(p.Y, q.Y), Math.Max(p.Y, q.Y));
        return p.X > r.X + folga && p.X < r.Direita - folga && y2 > r.Y + folga && y1 < r.Base - folga;
    }

    private static double Sobreposicao(Ponto p, Ponto q, Ponto s1, Ponto s2)
    {
        var horizontal = Math.Abs(p.Y - q.Y) < 0.01;
        var outroHorizontal = Math.Abs(s1.Y - s2.Y) < 0.01;
        if (horizontal != outroHorizontal) return 0;
        if (horizontal)
        {
            if (Math.Abs(p.Y - s1.Y) > 1) return 0;
            return Math.Max(0, Math.Min(Math.Max(p.X, q.X), Math.Max(s1.X, s2.X)) - Math.Max(Math.Min(p.X, q.X), Math.Min(s1.X, s2.X)));
        }
        if (Math.Abs(p.X - s1.X) > 1) return 0;
        return Math.Max(0, Math.Min(Math.Max(p.Y, q.Y), Math.Max(s1.Y, s2.Y)) - Math.Max(Math.Min(p.Y, q.Y), Math.Min(s1.Y, s2.Y)));
    }

    private static bool Cruza(Ponto p, Ponto q, Ponto s1, Ponto s2)
    {
        var horizontal = Math.Abs(p.Y - q.Y) < 0.01;
        var outroHorizontal = Math.Abs(s1.Y - s2.Y) < 0.01;
        if (horizontal == outroHorizontal) return false;
        var (h1, h2, v1, v2) = horizontal ? (p, q, s1, s2) : (s1, s2, p, q);
        return v1.X > Math.Min(h1.X, h2.X) + 0.5 && v1.X < Math.Max(h1.X, h2.X) - 0.5
               && h1.Y > Math.Min(v1.Y, v2.Y) + 0.5 && h1.Y < Math.Max(v1.Y, v2.Y) - 0.5;
    }

    // ── 7. Rótulos das saídas ────────────────────────────────────────────────

    private void RotulosDasLigacoes()
    {
        var ocupados = new List<Retangulo>();
        foreach (var linha in Linhas.Where(x => x.Ligacao.Rotulo != null))
        {
            var texto = linha.Ligacao.Rotulo!;
            var largura = LarguraDe(texto, FonteRotulo, false);
            var p = linha.Pontos[0];
            var q = linha.Pontos.Count > 1 ? linha.Pontos[1] : p;
            double x, baseY;
            if (Math.Abs(p.Y - q.Y) < 0.01)
            {
                // Saída na horizontal: acima da linha, logo depois da ponta
                x = q.X >= p.X ? p.X + 5 : p.X - 5 - largura;
                baseY = p.Y - 4;
            }
            else if (q.Y > p.Y)
            {
                x = p.X + 5;
                baseY = p.Y + 12;
            }
            else
            {
                x = p.X + 5;
                baseY = p.Y - 6;
            }
            var area = new Retangulo(x - 1.5, baseY - FonteRotulo * 0.82, largura + 3, FonteRotulo + 1.5);
            if (ocupados.Any(o => o.Sobrepoe(area)))
            {
                // Já há rótulo aí: vai para o outro lado da linha
                x = p.X - 5 - largura;
                area = new Retangulo(x - 1.5, area.Y, largura + 3, area.A);
            }
            ocupados.Add(area);
            linha.Rotulo = area;
            linha.RotuloEm = new Ponto(x, baseY);
            Escrever(texto, x, baseY, FonteRotulo, false, $"ligacao:{linha.Ligacao.Id}", CorRotuloLigacao);
        }
    }

    // ── 8. Faixas das raias e do fluxo ───────────────────────────────────────

    private void Faixas()
    {
        var direita = Math.Max(
            Caixas.Select(c => c.Area.Direita).DefaultIfEmpty(0).Max(),
            Math.Max(Textos.Select(t => t.Area.Direita).DefaultIfEmpty(0).Max(), Linhas.SelectMany(l => l.Pontos).Select(p => p.X).DefaultIfEmpty(0).Max()));
        direita = Math.Max(direita, Artefatos.Select(a => a.Rotulo.Direita).DefaultIfEmpty(0).Max());
        Largura = Math.Ceiling(direita + FimConteudo + Margem);

        // Faixa do fluxo (o "pool" do guia), com duas raias ou mais
        var xRaia = Margem;
        var alturaTotal = Altura - 2 * Margem;
        if (_larguraFaixaPool > 0)
        {
            BlocoVertical(_linhasPool, Margem + _larguraFaixaPool / 2, Margem + alturaTotal / 2, FontePool, LinhaPool, "pool");
            xRaia += _larguraFaixaPool;
        }

        // Faixas das raias: o nome na vertical, centrado
        for (var r = 0; r < _a.Raias.Count; r++)
        {
            var (topo, baseRaia, _) = _raiaY[r];
            BlocoVertical(_linhasRaia[r], xRaia + _larguraFaixaRaia / 2, (topo + baseRaia) / 2, FonteRaia, LinhaRaia, $"raia:{_a.Raias[r].Id}");
            Raias.Add(new RaiaDesenhada
            {
                Raia = _a.Raias[r],
                Area = new Retangulo(xRaia, topo, Largura - Margem - xRaia, baseRaia - topo),
                Faixa = new Retangulo(xRaia, topo, _larguraFaixaRaia, baseRaia - topo)
            });
        }
    }

    // ── 9. Descrição (texto alternativo) ─────────────────────────────────────

    private string Referencia(PeFluxoElemento e) => e.Tipo switch
    {
        PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Subprocesso =>
            e.Numero != null ? $"{e.Numero} {e.Nome}" : e.Nome,
        PeDominios.TipoElementoFluxo.Fim => "o fim",
        PeDominios.TipoElementoFluxo.Inicio => "o início",
        PeDominios.TipoElementoFluxo.Ligacao => $"o fluxo \"{e.Nome}\"",
        PeDominios.TipoElementoFluxo.Decisao => e.Nome.Length > 0 ? $"a decisão \"{e.Nome}\"" : "a decisão",
        _ => "a junção dos caminhos"
    };

    private void Descrever()
    {
        var raias = _a.Raias.Select(r => r.Nome).Where(n => n.Length > 0).ToList();
        var i = 0;
        foreach (var e in _a.OrdemDeLeitura)
        {
            var raia = _a.Raias.Count > 0 ? _a.Raias[_a.RaiaDe(e)].Nome : string.Empty;
            var quem = raia.Length > 0 ? $" ({raia})" : string.Empty;
            var saidas = _a.Saidas[e.Id];
            string texto = e.Tipo switch
            {
                PeDominios.TipoElementoFluxo.Inicio => $"Início{quem}.",
                PeDominios.TipoElementoFluxo.Fim => $"Fim{quem}.",
                PeDominios.TipoElementoFluxo.Ligacao => _a.Entradas[e.Id].Count > 0
                    ? $"Segue no fluxo \"{e.Nome}\"{quem}."
                    : $"Vem do fluxo \"{e.Nome}\"{quem}.",
                PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Subprocesso =>
                    $"{Referencia(e)}{quem}" + (e.Artefatos.Count > 0 ? $"; gera {string.Join(", ", e.Artefatos)}" : string.Empty) + ".",
                PeDominios.TipoElementoFluxo.Decisao =>
                    $"Decisão{(e.Nome.Length > 0 ? $" \"{e.Nome}\"" : string.Empty)}{quem}: "
                    + string.Join("; ", saidas.Select(l => $"{l.Rotulo ?? "sem rótulo"}, {(_a.EhRetorno(l) ? "volta para" : "segue para")} {Referencia(_a.PorId[l.Para])}")) + ".",
                _ => saidas.Count > 1
                    ? $"Os caminhos se abrem e seguem ao mesmo tempo para {string.Join(", ", saidas.Select(l => Referencia(_a.PorId[l.Para])))}{quem}."
                    : $"Os caminhos se juntam{quem}."
            };
            if (e.Tipo is not PeDominios.TipoElementoFluxo.Decisao
                && saidas.Any(_a.EhRetorno))
                texto = texto.TrimEnd('.') + "; " + string.Join("; ", saidas.Where(_a.EhRetorno).Select(l => $"volta para {Referencia(_a.PorId[l.Para])}")) + ".";
            Descricao.Add($"{++i}. {texto}");
        }
        if (raias.Count > 0) Descricao.Insert(0, $"Raias: {string.Join(", ", raias)}.");
    }

    // ── 10. Geometria (o que o editor visual recebe e de onde o SVG sai) ──────

    private static PeFluxoGeometriaPonto PontoDaGeometria(Ponto p) => new() { X = p.X, Y = p.Y };

    private static PeFluxoGeometriaArea AreaDaGeometria(Retangulo r) => new() { X = r.X, Y = r.Y, Largura = r.L, Altura = r.A };

    /// <summary>Onde fica o nome do elemento (nulo sem nome).</summary>
    private static string? PosicaoDoNome(PeFluxoElemento e, Medidas m)
    {
        if (m.Nome.Count == 0) return null;
        if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo)) return "dentro";
        if (!PeDominios.TipoElementoFluxo.EhPorta(e.Tipo)) return "abaixo";
        return m.PosicaoNome switch
        {
            PosicaoNome.Direita => "direita",
            PosicaoNome.Abaixo => "abaixo",
            _ => "acima"
        };
    }

    /// <summary>
    /// A geometria do desenho: as mesmas medidas (sem arredondar) que o SVG usa, na ordem em que
    /// ele desenha. O SVG (<see cref="PeFluxoSvg"/>) lê só daqui.
    /// </summary>
    private PeFluxoGeometria MontarGeometria(IReadOnlyList<string> nomesOriginais)
    {
        var g = new PeFluxoGeometria
        {
            Largura = Largura,
            Altura = Altura,
            Titulo = Titulo,
            Cor = new PeFluxoGeometriaCor { Fundo = _cor.Fundo, Borda = _cor.Borda },
            Descricao = Descricao.ToList()
        };

        // A faixa do fluxo ocupa a altura toda, dentro da margem
        if (_larguraFaixaPool > 0)
        {
            var baseTotal = Altura - Margem;
            g.FaixaTitulo = new PeFluxoGeometriaFaixa
            {
                X = Margem,
                Y = Margem,
                Largura = _larguraFaixaPool,
                Altura = baseTotal - Margem,
                Linhas = _linhasPool.ToList()
            };
        }

        for (var r = 0; r < Raias.Count; r++)
        {
            var raia = Raias[r];
            g.Raias.Add(new PeFluxoGeometriaRaia
            {
                Id = raia.Raia.Id,
                Nome = raia.Raia.Nome,
                NomeOriginal = r < nomesOriginais.Count ? nomesOriginais[r] : string.Empty,
                Y = raia.Area.Y,
                Altura = raia.Area.A,
                LarguraCabecalho = raia.Faixa.L,
                X = raia.Area.X,
                Largura = raia.Area.L,
                Linhas = _linhasRaia[r].ToList()
            });
        }

        foreach (var c in Caixas)
        {
            var e = c.Elemento;
            var m = _m[e.Id];
            g.Elementos.Add(new PeFluxoGeometriaElemento
            {
                Id = e.Id,
                Tipo = e.Tipo,
                RaiaId = _a.Raias[c.Raia].Id,
                Camada = c.Camada,
                X = c.Area.X,
                Y = c.Area.Y,
                Largura = c.Area.L,
                Altura = c.Area.A,
                Numero = e.Numero,
                Nome = e.Nome,
                Linhas = m.Nome.ToList(),
                Solto = _a.Soltos.Contains(e.Id),
                Faixa = c.Faixa,
                PosicaoNome = PosicaoDoNome(e, m)
            });
        }

        foreach (var l in Linhas)
        {
            g.Ligacoes.Add(new PeFluxoGeometriaLigacao
            {
                Id = l.Ligacao.Id,
                De = l.Ligacao.De,
                Para = l.Ligacao.Para,
                Rotulo = l.Ligacao.Rotulo,
                Retorno = l.Retorno,
                Pontos = l.Pontos.Select(PontoDaGeometria).ToList(),
                RotuloX = l.RotuloEm?.X,
                RotuloY = l.RotuloEm?.Y,
                RotuloArea = l.Rotulo is { } area ? AreaDaGeometria(area) : null
            });
        }

        foreach (var a in Artefatos)
        {
            g.Artefatos.Add(new PeFluxoGeometriaArtefato
            {
                ElementoId = a.DonoId,
                Indice = a.Indice,
                Nome = a.Nome,
                X = a.Documento.X,
                Y = a.Documento.Y,
                Largura = a.Documento.L,
                Altura = a.Documento.A,
                Linhas = a.Linhas.ToList(),
                Rotulo = AreaDaGeometria(a.Rotulo),
                Conector = a.Conector.Select(PontoDaGeometria).ToList()
            });
        }

        foreach (var t in Textos)
        {
            g.Textos.Add(new PeFluxoGeometriaTexto
            {
                Texto = t.Conteudo,
                X = t.X,
                Y = t.Y,
                Tamanho = t.Tamanho,
                Negrito = t.Negrito,
                Vertical = t.Vertical,
                Cor = t.Cor,
                Largura = t.Largura,
                Dono = t.Dono
            });
        }

        // Os números na ordem de leitura do desenho
        foreach (var e in _a.OrdemDeLeitura.Where(e => e.Numero != null)) g.Numeros[e.Id] = e.Numero!;
        return g;
    }
}
