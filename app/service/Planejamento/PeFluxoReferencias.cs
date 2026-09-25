using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Como cada item de um fluxo aparece nas mensagens da validação (F1, achado D11 da revisão
/// complementar): do jeito que a pessoa o vê no desenho, nunca pelo id interno nem pela posição
/// na lista.
/// <list type="bullet">
/// <item>tarefa e subprocesso pelo número do desenho e pelo nome ("a tarefa 2.3 \"Descrever o
/// ambiente\""); sem nome, "a tarefa 2.3, ainda sem nome"; sem número (fluxo sem prefixo), pelo
/// nome;</item>
/// <item>decisão pela pergunta ("a decisão \"Aprovado?\""); os outros pelo tipo e pelo nome;</item>
/// <item>o que não tem nome nem número, pelo vizinho no desenho: "o paralelo sem nome, depois de
/// 2.4" (quem vem antes dele), "antes de 2.5" (sem entrada, quem vem depois) ou, sem ligação
/// nenhuma, a raia ("na raia \"Equipe\""); o início e o fim sem nome só ganham o vizinho quando
/// o fluxo tem mais de um;</item>
/// <item>a raia pelo nome ou, sem nome, pela posição de cima para baixo ("a 2ª raia");</item>
/// <item>a ligação pelas pontas ("a ligação de 2.3 para o fim").</item>
/// </list>
/// Os números são os do desenho (a conta do <see cref="PeFluxoAnalise.Numerar"/>, também na
/// definição com erros), e os nomes passam pelo dicionário: o marcador ("{nomes.comite}") sai como
/// o desenho mostra (o nome do órgão ou, sem ele, o padrão).
/// <para>
/// F2 (observação D-O2 da reconferência): dois itens que sairiam com a mesma descrição (dois fins
/// soltos na mesma raia, dois paralelos sem nome depois da mesma tarefa) ou o item solto que só
/// teria a raia para ser achado, quando a raia tem outro do mesmo tipo (o segundo início ao lado
/// do primeiro), passam a ser citados pela posição entre os do mesmo tipo na raia em que o desenho
/// os põe, na ordem de leitura (da esquerda para a direita e, na mesma coluna, de cima para baixo):
/// "o 2º fim da raia \"Equipe\"", "a decisão \"Aprovado?\", a 2ª da raia \"Equipe\"". Cada um fica
/// com a sua mensagem, então a lista diz quantos são. O vizinho sem nome de um tipo que se repete
/// no fluxo também vai pela raia ("antes do fim da raia \"Comitê\"").
/// </para>
/// </summary>
public sealed class PeFluxoReferencias
{
    private readonly PeFluxoDefinicao _definicao;
    private readonly PeFluxoAnalise _analise;
    private readonly IReadOnlyDictionary<string, string?> _nomes;
    private readonly Dictionary<string, string> _numeros;
    private readonly int _inicios;
    private readonly int _fins;

    // A posição de cada elemento entre os do mesmo tipo na raia em que é desenhado (1, 2...) e quantos são
    private readonly Dictionary<string, (int Posicao, int Total)> _posicoes;

    // Quantos elementos de cada tipo o desenho tem
    private readonly Dictionary<string, int> _porTipo;

    // Os elementos citados pela posição (a descrição de sempre não os distingue)
    private readonly HashSet<string> _pelaPosicao;

    public PeFluxoReferencias(PeFluxoDefinicao definicao, IReadOnlyDictionary<string, string?>? nomes = null)
    {
        _definicao = definicao;
        _nomes = nomes ?? PeFluxoNomes.Mapa(null, null);
        _analise = PeFluxoAnalise.Analisar(definicao);
        _numeros = PeFluxoAnalise.Numeros(_analise, definicao.PrefixoNumeracao);
        _inicios = definicao.Elementos.Count(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio);
        _fins = definicao.Elementos.Count(e => e.Tipo == PeDominios.TipoElementoFluxo.Fim);
        _posicoes = Posicoes(_analise);
        _porTipo = _analise.Elementos.GroupBy(e => e.Tipo ?? string.Empty).ToDictionary(g => g.Key, g => g.Count());
        _pelaPosicao = PelaPosicao();
    }

    // ── Elementos ───────────────────────────────────────────────────────────

    /// <summary>O item antes de ":" ou do ponto final: 'o paralelo sem nome, depois de 2.4'.</summary>
    public string Quem(PeFluxoElemento e)
    {
        var (nucleo, aposto) = Descrever(e);
        return aposto == null ? nucleo : $"{nucleo}, {aposto}";
    }

    /// <summary>O item no meio da frase (antes do verbo): 'o paralelo sem nome, depois de 2.4,'.</summary>
    public string QuemNoMeio(PeFluxoElemento e)
    {
        var (nucleo, aposto) = Descrever(e);
        return aposto == null ? nucleo : $"{nucleo}, {aposto},";
    }

    /// <summary>
    /// O pedido do nome que falta: "Dê um nome à tarefa 2.3." ou, sem número, pelo vizinho; o que
    /// só a posição distingue, pela posição ("Dê um nome à 2ª tarefa da raia \"Equipe\".").
    /// </summary>
    public string PedidoDeNome(PeFluxoElemento e)
    {
        var (artigo, tipo) = Tipo(e.Tipo);
        string alvo;
        if (Numero(e) is string numero)
            alvo = $"{ComA($"{artigo} {tipo}")} {numero}";
        else if (PelaPosicao(e))
            alvo = ComA(Posicional(e).Nucleo);
        else
        {
            var contexto = Contexto(e).Texto;
            var onde = contexto == null ? string.Empty
                : contexto.StartsWith("na ", StringComparison.Ordinal) ? $" {contexto}" : $" que vem {contexto}";
            alvo = ComA($"{artigo} {tipo}") + onde;
        }
        return e.Tipo == PeDominios.TipoElementoFluxo.Ligacao
            ? $"Dê um nome {alvo}, dizendo de onde o fluxo vem ou para onde segue."
            : $"Dê um nome {alvo}.";
    }

    /// <summary>A tarefa, a decisão e a ligação com outro fluxo concordam no feminino ("alcançada", "ligue-a").</summary>
    public static bool Feminino(PeFluxoElemento e) =>
        e.Tipo is PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Decisao or PeDominios.TipoElementoFluxo.Ligacao;

    /// <summary>O número que o desenho mostra (tarefa e subprocesso de fluxo com prefixo), ou nulo.</summary>
    public string? Numero(PeFluxoElemento e) =>
        Analisado(e) && _numeros.TryGetValue(e.Id, out var numero) ? numero : null;

    private (string Nucleo, string? Aposto) Descrever(PeFluxoElemento e)
    {
        if (PelaPosicao(e)) return Posicional(e);
        var (nucleo, aposto, _) = DescreverSemPosicao(e);
        return (nucleo, aposto);
    }

    /// <summary>
    /// A descrição de sempre (D11), sem a posição; SoARaia diz que o aposto é só a raia (o item
    /// sem ligação, que a raia não distingue de outro do mesmo tipo nela).
    /// </summary>
    private (string Nucleo, string? Aposto, bool SoARaia) DescreverSemPosicao(PeFluxoElemento e)
    {
        var (artigo, tipo) = Tipo(e.Tipo);
        var nome = Nome(e.Nome);
        if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo) && Numero(e) is string numero)
            return nome.Length > 0 ? ($"{artigo} {tipo} {numero} \"{Curto(nome)}\"", null, false) : ($"{artigo} {tipo} {numero}", "ainda sem nome", false);
        if (nome.Length > 0) return ($"{artigo} {tipo} \"{Curto(nome)}\"", null, false);
        var (contexto, soARaia) = Contexto(e);
        return e.Tipo switch
        {
            PeDominios.TipoElementoFluxo.Inicio => _inicios > 1 ? ("o início", contexto, soARaia) : ("o início", null, false),
            PeDominios.TipoElementoFluxo.Fim => _fins > 1 ? ("o fim", contexto, soARaia) : ("o fim", null, false),
            PeDominios.TipoElementoFluxo.Decisao => ("a decisão sem pergunta", contexto, soARaia),
            _ => ($"{artigo} {tipo} sem nome", contexto, soARaia)
        };
    }

    /// <summary>
    /// Onde o item sem nome fica no desenho: depois de quem vem antes dele ("depois de 2.4"), antes
    /// de quem vem depois ("antes de 2.5") ou, sem ligação, na raia em que ele é desenhado
    /// (SoARaia).
    /// </summary>
    private (string? Texto, bool SoARaia) Contexto(PeFluxoElemento e)
    {
        if (Analisado(e))
        {
            var entrada = _analise.EntradasDeAvanco(e.Id).FirstOrDefault() ?? _analise.Entradas[e.Id].FirstOrDefault();
            if (entrada != null) return ("depois " + ComDe(Vizinho(_analise.PorId[entrada.De])), false);
            var saida = _analise.SaidasDeAvanco(e.Id).FirstOrDefault() ?? _analise.Saidas[e.Id].FirstOrDefault();
            if (saida != null) return ("antes " + ComDe(Vizinho(_analise.PorId[saida.Para])), false);
        }
        // Sem raia que exista, o desenho põe o item na primeira
        var raia = _definicao.Raias.FirstOrDefault(r => r.Id == e.RaiaId) ?? _definicao.Raias.FirstOrDefault();
        return raia == null ? (null, false) : ("na " + RaiaCurta(raia), true);
    }

    /// <summary>
    /// O vizinho, curto: o número ("2.4") ou o tipo com o nome ("a decisão \"Aprovado?\"", "o
    /// início"). Sem nome, num tipo que se repete no fluxo, pela raia e, quando ela tem outro do
    /// mesmo tipo, pela posição ("o fim da raia \"Comitê\"", "o 2º paralelo da raia \"Equipe\"").
    /// </summary>
    private string Vizinho(PeFluxoElemento e)
    {
        if (Numero(e) is string numero) return numero;
        var (artigo, tipo) = Tipo(e.Tipo);
        var nome = Nome(e.Nome);
        if (nome.Length > 0) return $"{artigo} {tipo} \"{Curto(nome)}\"";
        if (Analisado(e) && _porTipo.GetValueOrDefault(e.Tipo ?? string.Empty) > 1 && RaiaDoDesenho(e) is { } raia)
        {
            var (posicao, total) = _posicoes[e.Id];
            return total > 1
                ? $"{artigo} {Ordinal(e, posicao)} {tipo} da {RaiaCurta(raia)}"
                : $"{artigo} {tipo} da {RaiaCurta(raia)}";
        }
        return e.Tipo switch
        {
            PeDominios.TipoElementoFluxo.Inicio => "o início",
            PeDominios.TipoElementoFluxo.Fim => "o fim",
            PeDominios.TipoElementoFluxo.Decisao => "a decisão sem pergunta",
            _ => $"{artigo} {tipo} sem nome"
        };
    }

    // O elemento entrou na análise (o id é o dele): tem número e vizinhos
    private bool Analisado(PeFluxoElemento e) =>
        e.Id.Length > 0 && _analise.PorId.TryGetValue(e.Id, out var analisado) && ReferenceEquals(analisado, e);

    // ── Posição (F2, D-O2) ──────────────────────────────────────────────────

    /// <summary>
    /// A posição de cada elemento entre os do mesmo tipo na raia em que o desenho o põe, na ordem
    /// de leitura (coluna e, na mesma coluna, a linha de cima primeiro), e quantos são.
    /// </summary>
    private static Dictionary<string, (int Posicao, int Total)> Posicoes(PeFluxoAnalise analise)
    {
        var posicoes = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var grupo in analise.Elementos.GroupBy(e => (e.Tipo ?? string.Empty, analise.RaiaDe(e))))
        {
            var ordem = grupo
                .OrderBy(e => analise.Camada[e.Id])
                .ThenBy(e => analise.Faixa[e.Id])
                .ThenBy(e => analise.IndiceElemento[e.Id])
                .ToList();
            for (var i = 0; i < ordem.Count; i++) posicoes[ordem[i].Id] = (i + 1, ordem.Count);
        }
        return posicoes;
    }

    /// <summary>
    /// Os elementos que a descrição de sempre não distingue: a mesma descrição de outro elemento,
    /// ou só a raia para achá-lo quando ela tem outro do mesmo tipo.
    /// </summary>
    private HashSet<string> PelaPosicao()
    {
        var descricoes = _analise.Elementos
            .Where(e => RaiaDoDesenho(e) != null)
            .Select(e => (e.Id, Descricao: DescreverSemPosicao(e)))
            .ToList();
        var pelaPosicao = descricoes
            .GroupBy(d => d.Descricao.Aposto == null ? d.Descricao.Nucleo : $"{d.Descricao.Nucleo}, {d.Descricao.Aposto}", StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(d => d.Id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (id, descricao) in descricoes)
            if (descricao.SoARaia && _posicoes[id].Total > 1) pelaPosicao.Add(id);
        return pelaPosicao;
    }

    private bool PelaPosicao(PeFluxoElemento e) => Analisado(e) && _pelaPosicao.Contains(e.Id);

    /// <summary>
    /// O item pela posição na raia: "o 2º fim da raia \"Equipe\"", "a 2ª decisão da raia
    /// \"Equipe\", sem pergunta", "a 2ª tarefa da raia \"Equipe\", ainda sem nome"; com nome,
    /// "a decisão \"Aprovado?\", a 2ª da raia \"Equipe\"".
    /// </summary>
    private (string Nucleo, string? Aposto) Posicional(PeFluxoElemento e)
    {
        var (artigo, tipo) = Tipo(e.Tipo);
        var ordinal = Ordinal(e, _posicoes[e.Id].Posicao);
        var raia = $"da {RaiaCurta(RaiaDoDesenho(e)!)}";
        var nome = Nome(e.Nome);
        if (nome.Length > 0) return ($"{artigo} {tipo} \"{Curto(nome)}\"", $"{artigo} {ordinal} {raia}");
        return e.Tipo switch
        {
            PeDominios.TipoElementoFluxo.Decisao => ($"a {ordinal} decisão {raia}", "sem pergunta"),
            PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Subprocesso or PeDominios.TipoElementoFluxo.Ligacao =>
                ($"{artigo} {ordinal} {tipo} {raia}", "ainda sem nome"),
            _ => ($"{artigo} {ordinal} {tipo} {raia}", null)
        };
    }

    // "2º" ou "2ª", pelo gênero do tipo
    private static string Ordinal(PeFluxoElemento e, int posicao) =>
        posicao.ToString(System.Globalization.CultureInfo.InvariantCulture) + (Feminino(e) ? "ª" : "º");

    // A raia em que o desenho põe o elemento (sem raia que exista, a primeira); nula sem raia nenhuma
    private PeFluxoRaia? RaiaDoDesenho(PeFluxoElemento e) =>
        _analise.Raias.Count == 0 ? null : _analise.Raias[_analise.RaiaDe(e)];

    // ── Raias e ligações ────────────────────────────────────────────────────

    /// <summary>A raia: 'a raia "Equipe"' ou, sem nome, 'a 2ª raia' (de cima para baixo).</summary>
    public string Raia(PeFluxoRaia raia) => "a " + RaiaCurta(raia);

    /// <summary>A posição da raia de cima para baixo (1, 2, 3...).</summary>
    public int PosicaoDaRaia(PeFluxoRaia raia) => _definicao.Raias.IndexOf(raia) + 1;

    private string RaiaCurta(PeFluxoRaia raia)
    {
        var nome = Nome(raia.Nome);
        return nome.Length > 0 ? $"raia \"{Curto(nome)}\"" : $"{PosicaoDaRaia(raia)}ª raia";
    }

    /// <summary>A ligação pelas pontas que existem: 'a ligação de 2.3 para o fim', 'a ligação que sai de 2.3'.</summary>
    public string Ligacao(PeFluxoLigacao ligacao)
    {
        var de = Elemento(ligacao.De);
        var para = Elemento(ligacao.Para);
        if (de != null && para != null) return $"a ligação {ComDe(Vizinho(de))} para {Vizinho(para)}";
        if (de != null) return $"a ligação que sai {ComDe(Vizinho(de))}";
        if (para != null) return $"a ligação que chega {ComA(Vizinho(para))}";
        return "uma ligação";
    }

    /// <summary>A ligação sem uma das pontas (De ou Para em branco).</summary>
    public string LigacaoSemPonta(PeFluxoLigacao ligacao)
    {
        if (Elemento(ligacao.De) is { } de) return $"A ligação que sai {ComDe(Vizinho(de))} não diz para onde vai.";
        if (Elemento(ligacao.Para) is { } para) return $"A ligação que chega {ComA(Vizinho(para))} não diz de onde sai.";
        return "Uma ligação não diz de onde sai nem para onde vai.";
    }

    /// <summary>A ligação com uma ponta num passo que não existe (apagado, por exemplo).</summary>
    public string LigacaoParaOQueNaoExiste(PeFluxoLigacao ligacao)
    {
        var de = Elemento(ligacao.De);
        var para = Elemento(ligacao.Para);
        if (de != null) return $"A ligação que sai {ComDe(Vizinho(de))} vai para um passo que não existe: apague essa ligação.";
        if (para != null) return $"A ligação que chega {ComA(Vizinho(para))} vem de um passo que não existe: apague essa ligação.";
        return "Uma ligação liga passos que não existem: apague essa ligação.";
    }

    private PeFluxoElemento? Elemento(string? id) =>
        !string.IsNullOrEmpty(id) && _analise.PorId.TryGetValue(id, out var e) ? e : null;

    // ── Textos ──────────────────────────────────────────────────────────────

    // O nome como o desenho mostra: os marcadores trocados pelo dicionário, sem espaço sobrando
    private string Nome(string? texto) => PeFluxoDefinicaoLeitor.Limpar(PeFluxoDesenho.ResolverNome(texto, _nomes));

    /// <summary>O artigo e o nome do tipo ("a", "tarefa").</summary>
    public static (string Artigo, string Tipo) Tipo(string? tipo) => tipo switch
    {
        PeDominios.TipoElementoFluxo.Inicio => ("o", "início"),
        PeDominios.TipoElementoFluxo.Fim => ("o", "fim"),
        PeDominios.TipoElementoFluxo.Ligacao => ("a", "ligação com outro fluxo"),
        PeDominios.TipoElementoFluxo.Tarefa => ("a", "tarefa"),
        PeDominios.TipoElementoFluxo.Subprocesso => ("o", "subprocesso"),
        PeDominios.TipoElementoFluxo.Decisao => ("a", "decisão"),
        PeDominios.TipoElementoFluxo.Paralelo => ("o", "paralelo"),
        _ => ("o", "passo")
    };

    /// <summary>"a tarefa" vira "da tarefa"; "o fim", "do fim"; "2.4", "de 2.4".</summary>
    public static string ComDe(string texto) =>
        texto.StartsWith("a ", StringComparison.Ordinal) ? "da " + texto[2..]
        : texto.StartsWith("o ", StringComparison.Ordinal) ? "do " + texto[2..]
        : "de " + texto;

    /// <summary>"a tarefa" vira "à tarefa"; "o fim", "ao fim"; "2.4", "a 2.4".</summary>
    public static string ComA(string texto) =>
        texto.StartsWith("a ", StringComparison.Ordinal) ? "à " + texto[2..]
        : texto.StartsWith("o ", StringComparison.Ordinal) ? "ao " + texto[2..]
        : "a " + texto;

    /// <summary>O texto longo cortado em 60 caracteres (com reticências).</summary>
    public static string Curto(string texto) => texto.Length <= 60 ? texto : texto[..57] + "...";

    public static string Maiuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
