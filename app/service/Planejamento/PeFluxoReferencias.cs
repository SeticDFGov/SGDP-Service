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
/// </summary>
public sealed class PeFluxoReferencias
{
    private readonly PeFluxoDefinicao _definicao;
    private readonly PeFluxoAnalise _analise;
    private readonly IReadOnlyDictionary<string, string?> _nomes;
    private readonly Dictionary<string, string> _numeros;
    private readonly int _inicios;
    private readonly int _fins;

    public PeFluxoReferencias(PeFluxoDefinicao definicao, IReadOnlyDictionary<string, string?>? nomes = null)
    {
        _definicao = definicao;
        _nomes = nomes ?? PeFluxoNomes.Mapa(null, null);
        _analise = PeFluxoAnalise.Analisar(definicao);
        _numeros = PeFluxoAnalise.Numeros(_analise, definicao.PrefixoNumeracao);
        _inicios = definicao.Elementos.Count(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio);
        _fins = definicao.Elementos.Count(e => e.Tipo == PeDominios.TipoElementoFluxo.Fim);
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

    /// <summary>O pedido do nome que falta: "Dê um nome à tarefa 2.3." ou, sem número, pelo vizinho.</summary>
    public string PedidoDeNome(PeFluxoElemento e)
    {
        var (artigo, tipo) = Tipo(e.Tipo);
        var alvo = ComA($"{artigo} {tipo}");
        var onde = Numero(e) is string numero
            ? $" {numero}"
            : Contexto(e) is string contexto
                ? contexto.StartsWith("na ", StringComparison.Ordinal) ? $" {contexto}" : $" que vem {contexto}"
                : string.Empty;
        return e.Tipo == PeDominios.TipoElementoFluxo.Ligacao
            ? $"Dê um nome {alvo}{onde}, dizendo de onde o fluxo vem ou para onde segue."
            : $"Dê um nome {alvo}{onde}.";
    }

    /// <summary>A tarefa, a decisão e a ligação com outro fluxo concordam no feminino ("alcançada", "ligue-a").</summary>
    public static bool Feminino(PeFluxoElemento e) =>
        e.Tipo is PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Decisao or PeDominios.TipoElementoFluxo.Ligacao;

    /// <summary>O número que o desenho mostra (tarefa e subprocesso de fluxo com prefixo), ou nulo.</summary>
    public string? Numero(PeFluxoElemento e) =>
        Analisado(e) && _numeros.TryGetValue(e.Id, out var numero) ? numero : null;

    private (string Nucleo, string? Aposto) Descrever(PeFluxoElemento e)
    {
        var (artigo, tipo) = Tipo(e.Tipo);
        var nome = Nome(e.Nome);
        if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo) && Numero(e) is string numero)
            return nome.Length > 0 ? ($"{artigo} {tipo} {numero} \"{Curto(nome)}\"", null) : ($"{artigo} {tipo} {numero}", "ainda sem nome");
        if (nome.Length > 0) return ($"{artigo} {tipo} \"{Curto(nome)}\"", null);
        return e.Tipo switch
        {
            PeDominios.TipoElementoFluxo.Inicio => ("o início", _inicios > 1 ? Contexto(e) : null),
            PeDominios.TipoElementoFluxo.Fim => ("o fim", _fins > 1 ? Contexto(e) : null),
            PeDominios.TipoElementoFluxo.Decisao => ("a decisão sem pergunta", Contexto(e)),
            _ => ($"{artigo} {tipo} sem nome", Contexto(e))
        };
    }

    /// <summary>
    /// Onde o item sem nome fica no desenho: depois de quem vem antes dele ("depois de 2.4"), antes
    /// de quem vem depois ("antes de 2.5") ou, sem ligação, na raia em que ele é desenhado.
    /// </summary>
    private string? Contexto(PeFluxoElemento e)
    {
        if (Analisado(e))
        {
            var entrada = _analise.EntradasDeAvanco(e.Id).FirstOrDefault() ?? _analise.Entradas[e.Id].FirstOrDefault();
            if (entrada != null) return "depois " + ComDe(Vizinho(_analise.PorId[entrada.De]));
            var saida = _analise.SaidasDeAvanco(e.Id).FirstOrDefault() ?? _analise.Saidas[e.Id].FirstOrDefault();
            if (saida != null) return "antes " + ComDe(Vizinho(_analise.PorId[saida.Para]));
        }
        // Sem raia que exista, o desenho põe o item na primeira
        var raia = _definicao.Raias.FirstOrDefault(r => r.Id == e.RaiaId) ?? _definicao.Raias.FirstOrDefault();
        return raia == null ? null : "na " + RaiaCurta(raia);
    }

    /// <summary>O vizinho, curto: o número ("2.4") ou o tipo com o nome ("a decisão \"Aprovado?\"", "o início").</summary>
    private string Vizinho(PeFluxoElemento e)
    {
        if (Numero(e) is string numero) return numero;
        var (artigo, tipo) = Tipo(e.Tipo);
        var nome = Nome(e.Nome);
        if (nome.Length > 0) return $"{artigo} {tipo} \"{Curto(nome)}\"";
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
