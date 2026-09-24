using System.Globalization;
using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// A estrutura de um fluxo para o desenho e para a numeração (sem geometria):
/// <list type="bullet">
/// <item>retornos: as ligações que voltam para um passo anterior (arestas de volta de uma busca
/// em profundidade a partir do início, depois das entradas vindas de outro fluxo, seguindo as
/// ligações na ordem da lista); o resto forma um grafo sem ciclos;</item>
/// <item>camadas (colunas, da esquerda para a direita): o caminho mais longo desde a entrada,
/// ignorando os retornos; a entrada vinda de outro fluxo fica logo antes do passo que ela alimenta;</item>
/// <item>faixas (linhas dentro de cada raia): o passo fica na faixa do passo anterior da mesma
/// raia (a mediana, quando junta caminhos); os caminhos que se abrem de um mesmo passo ficam
/// empilhados na mesma coluna, centrados (o do meio segue a linha de quem abriu); conflito vai
/// para a faixa livre mais próxima;</item>
/// <item>número: tarefas e subprocessos, pela ordem de leitura do desenho (coluna, raia de cima
/// para baixo, faixa), com o prefixo do fluxo ("2.9", "2.10", "2.11").</item>
/// </list>
/// Tolera definição inválida (elemento sem raia vai para a primeira; ligação para o que não
/// existe é ignorada): o desenho de uma definição guardada nunca quebra.
/// </summary>
public sealed class PeFluxoAnalise
{
    public required PeFluxoDefinicao Definicao { get; init; }

    // Na ordem de cima para baixo
    public required List<PeFluxoRaia> Raias { get; init; }

    public required Dictionary<string, int> IndiceRaia { get; init; }

    // Na ordem da definição
    public required List<PeFluxoElemento> Elementos { get; init; }

    public required Dictionary<string, PeFluxoElemento> PorId { get; init; }

    public required Dictionary<string, int> IndiceElemento { get; init; }

    // Só as ligações entre elementos que existem (e não de um para ele mesmo)
    public required List<PeFluxoLigacao> Ligacoes { get; init; }

    public required HashSet<PeFluxoLigacao> Retornos { get; init; }

    public required Dictionary<string, List<PeFluxoLigacao>> Saidas { get; init; }

    public required Dictionary<string, List<PeFluxoLigacao>> Entradas { get; init; }

    public required Dictionary<string, int> Camada { get; init; }

    public required Dictionary<string, int> Faixa { get; init; }

    // Ordem de leitura (coluna, raia, faixa): a base da numeração e da descrição
    public required List<PeFluxoElemento> OrdemDeLeitura { get; init; }

    public int Camadas => Camada.Count == 0 ? 0 : Camada.Values.Max() + 1;

    public int RaiaDe(PeFluxoElemento e) => IndiceRaia.TryGetValue(e.RaiaId, out var i) ? i : 0;

    public bool EhRetorno(PeFluxoLigacao l) => Retornos.Contains(l);

    public IEnumerable<PeFluxoLigacao> SaidasDeAvanco(string id) => Saidas[id].Where(l => !Retornos.Contains(l));

    public IEnumerable<PeFluxoLigacao> EntradasDeAvanco(string id) => Entradas[id].Where(l => !Retornos.Contains(l));

    /// <summary>Calcula o número das tarefas e dos subprocessos (os outros ficam nulos).</summary>
    public static void Numerar(PeFluxoDefinicao definicao)
    {
        foreach (var e in definicao.Elementos) e.Numero = null;
        var prefixo = definicao.PrefixoNumeracao;
        if (string.IsNullOrWhiteSpace(prefixo)) return;

        var analise = Analisar(definicao);
        var n = 0;
        foreach (var e in analise.OrdemDeLeitura.Where(e => PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo)))
            e.Numero = $"{prefixo}.{(++n).ToString(CultureInfo.InvariantCulture)}";
    }

    public static PeFluxoAnalise Analisar(PeFluxoDefinicao definicao)
    {
        var raias = (definicao.Raias ?? new List<PeFluxoRaia>()).ToList();
        var indiceRaia = new Dictionary<string, int>();
        for (var i = 0; i < raias.Count; i++) indiceRaia.TryAdd(raias[i].Id ?? string.Empty, i);

        var elementos = new List<PeFluxoElemento>();
        var porId = new Dictionary<string, PeFluxoElemento>();
        foreach (var e in definicao.Elementos ?? new List<PeFluxoElemento>())
        {
            if (string.IsNullOrEmpty(e.Id) || porId.ContainsKey(e.Id)) continue;
            porId[e.Id] = e;
            elementos.Add(e);
        }
        var indice = elementos.Select((e, i) => (e.Id, i)).ToDictionary(x => x.Id, x => x.i);

        var ligacoes = (definicao.Ligacoes ?? new List<PeFluxoLigacao>())
            .Where(l => l.De != null && l.Para != null && l.De != l.Para && porId.ContainsKey(l.De) && porId.ContainsKey(l.Para))
            .ToList();
        var saidas = elementos.ToDictionary(e => e.Id, _ => new List<PeFluxoLigacao>());
        var entradas = elementos.ToDictionary(e => e.Id, _ => new List<PeFluxoLigacao>());
        foreach (var l in ligacoes)
        {
            saidas[l.De].Add(l);
            entradas[l.Para].Add(l);
        }

        var retornos = AcharRetornos(elementos, saidas, entradas);
        var camada = CalcularCamadas(elementos, saidas, entradas, retornos, indice);

        var analise = new PeFluxoAnalise
        {
            Definicao = definicao,
            Raias = raias,
            IndiceRaia = indiceRaia,
            Elementos = elementos,
            PorId = porId,
            IndiceElemento = indice,
            Ligacoes = ligacoes,
            Retornos = retornos,
            Saidas = saidas,
            Entradas = entradas,
            Camada = camada,
            Faixa = new Dictionary<string, int>(),
            OrdemDeLeitura = new List<PeFluxoElemento>()
        };
        analise.DistribuirFaixas();
        analise.OrdemDeLeitura.AddRange(elementos
            .OrderBy(e => camada[e.Id])
            .ThenBy(analise.RaiaDe)
            .ThenBy(e => analise.Faixa[e.Id])
            .ThenBy(e => indice[e.Id]));
        return analise;
    }

    /// <summary>
    /// Os retornos: busca em profundidade a partir do início (depois das outras entradas e, por
    /// último, do que sobrou), seguindo as saídas na ordem das ligações; a ligação para um passo
    /// que ainda está no caminho aberto volta para trás.
    /// </summary>
    private static HashSet<PeFluxoLigacao> AcharRetornos(List<PeFluxoElemento> elementos,
        Dictionary<string, List<PeFluxoLigacao>> saidas, Dictionary<string, List<PeFluxoLigacao>> entradas)
    {
        var raizes = elementos.Where(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio)
            .Concat(elementos.Where(e => e.Tipo != PeDominios.TipoElementoFluxo.Inicio && entradas[e.Id].Count == 0))
            .Concat(elementos)
            .Select(e => e.Id)
            .ToList();
        var estado = elementos.ToDictionary(e => e.Id, _ => 0);
        var retornos = new HashSet<PeFluxoLigacao>();
        foreach (var raiz in raizes)
        {
            if (estado[raiz] != 0) continue;
            var pilha = new Stack<(string Id, int Proxima)>();
            estado[raiz] = 1;
            pilha.Push((raiz, 0));
            while (pilha.Count > 0)
            {
                var (id, proxima) = pilha.Pop();
                var suas = saidas[id];
                if (proxima >= suas.Count)
                {
                    estado[id] = 2;
                    continue;
                }
                pilha.Push((id, proxima + 1));
                var ligacao = suas[proxima];
                switch (estado[ligacao.Para])
                {
                    case 1:
                        retornos.Add(ligacao);
                        break;
                    case 0:
                        estado[ligacao.Para] = 1;
                        pilha.Push((ligacao.Para, 0));
                        break;
                }
            }
        }
        return retornos;
    }

    /// <summary>
    /// A coluna de cada elemento: o caminho mais longo desde uma entrada, sem os retornos. A
    /// entrada que não é o início (a ligação vinda de outro fluxo) fica logo antes do primeiro
    /// passo que ela alimenta.
    /// </summary>
    private static Dictionary<string, int> CalcularCamadas(List<PeFluxoElemento> elementos, Dictionary<string, List<PeFluxoLigacao>> saidas,
        Dictionary<string, List<PeFluxoLigacao>> entradas, HashSet<PeFluxoLigacao> retornos, Dictionary<string, int> indice)
    {
        var grau = elementos.ToDictionary(e => e.Id, e => entradas[e.Id].Count(l => !retornos.Contains(l)));
        var camada = elementos.ToDictionary(e => e.Id, _ => 0);
        var prontos = new SortedSet<(int Indice, string Id)>(elementos.Where(e => grau[e.Id] == 0).Select(e => (indice[e.Id], e.Id)));
        var ordem = new List<string>();
        while (prontos.Count > 0)
        {
            var atual = prontos.Min;
            prontos.Remove(atual);
            ordem.Add(atual.Id);
            foreach (var l in saidas[atual.Id].Where(l => !retornos.Contains(l)))
            {
                camada[l.Para] = Math.Max(camada[l.Para], camada[atual.Id] + 1);
                if (--grau[l.Para] == 0) prontos.Add((indice[l.Para], l.Para));
            }
        }

        // Entradas que não são o início ficam junto do passo que alimentam
        foreach (var e in elementos)
        {
            if (e.Tipo == PeDominios.TipoElementoFluxo.Inicio || entradas[e.Id].Any(l => !retornos.Contains(l))) continue;
            var seguintes = saidas[e.Id].Where(l => !retornos.Contains(l)).Select(l => camada[l.Para]).ToList();
            if (seguintes.Count > 0) camada[e.Id] = Math.Max(0, seguintes.Min() - 1);
        }
        return camada;
    }

    /// <summary>
    /// Distribui as faixas (linhas) de cada raia, coluna por coluna. O passo fica na faixa do
    /// passo anterior da mesma raia (a mediana, quando junta caminhos; sem anterior na raia, a
    /// faixa 0, a linha principal). Os caminhos que se abrem de um passo para a coluna seguinte
    /// da mesma raia ficam empilhados e centrados na faixa de quem abriu, na ordem das ligações
    /// (três caminhos: um acima, um na linha e um abaixo; dois: um na linha e um abaixo).
    /// </summary>
    private void DistribuirFaixas()
    {
        var ocupadas = new HashSet<(int Raia, int Camada, int Faixa)>();
        var reservadas = new Dictionary<string, int>();
        // Coluna por coluna; o início primeiro (fica na linha principal da raia)
        var ordem = Elementos
            .OrderBy(e => Camada[e.Id])
            .ThenBy(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio ? 0 : 1)
            .ThenBy(e => IndiceElemento[e.Id])
            .ToList();

        foreach (var e in ordem)
        {
            var raia = RaiaDe(e);
            var camada = Camada[e.Id];
            int faixa;
            if (reservadas.TryGetValue(e.Id, out var reservada) && !ocupadas.Contains((raia, camada, reservada)))
            {
                faixa = reservada;
            }
            else
            {
                var anteriores = EntradasDeAvanco(e.Id)
                    .Select(l => PorId[l.De])
                    .Where(p => RaiaDe(p) == raia && Faixa.ContainsKey(p.Id))
                    .Select(p => Faixa[p.Id])
                    .OrderBy(f => f)
                    .ToList();
                var preferida = anteriores.Count == 0 ? 0 : anteriores[(anteriores.Count - 1) / 2];
                faixa = MaisProximaLivre(ocupadas, raia, camada, preferida);
            }
            Faixa[e.Id] = faixa;
            ocupadas.Add((raia, camada, faixa));

            // Irmãos: caminhos que se abrem daqui para a coluna seguinte da mesma raia
            var irmaos = SaidasDeAvanco(e.Id)
                .Select(l => PorId[l.Para])
                .Where(s => RaiaDe(s) == raia && Camada[s.Id] == camada + 1 && !Faixa.ContainsKey(s.Id) && !reservadas.ContainsKey(s.Id)
                            && EntradasDeAvanco(s.Id).All(x => x.De == e.Id))
                .Distinct()
                .ToList();
            if (irmaos.Count < 2) continue;
            var primeira = faixa - (irmaos.Count - 1) / 2;
            for (var i = 0; i < irmaos.Count; i++) reservadas[irmaos[i].Id] = primeira + i;
        }
    }

    private static int MaisProximaLivre(HashSet<(int, int, int)> ocupadas, int raia, int camada, int preferida)
    {
        for (var d = 0; ; d++)
        {
            if (!ocupadas.Contains((raia, camada, preferida + d))) return preferida + d;
            if (d > 0 && !ocupadas.Contains((raia, camada, preferida - d))) return preferida - d;
        }
    }
}
