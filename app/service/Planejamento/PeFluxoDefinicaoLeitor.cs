using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// A definição de um fluxo não passou na validação: 400 PeFluxoInvalido com a lista de erros
/// em linguagem simples ({ Code, Message, Erros }).
/// </summary>
public sealed class PeFluxoInvalidoException : ApiException
{
    public IReadOnlyList<string> Erros { get; }

    public PeFluxoInvalidoException(IReadOnlyList<string> erros)
        : base(ErrorCode.PeFluxoInvalido, erros.Count == 1
            ? "O fluxo tem um problema: " + erros[0]
            : $"O fluxo tem {erros.Count} problemas. Confira a lista.")
    {
        Erros = erros;
    }
}

/// <summary>
/// Leitura, conferência e normalização da definição de um fluxo (o jsonb de pe_fluxo_modelo e
/// de pe_fluxo e o corpo dos PUTs). Regras:
/// <list type="bullet">
/// <item>estrutura: Raias, Elementos e Ligacoes em listas; ids curtos (letras, números, hífen e
/// sublinhado) e únicos no fluxo inteiro; raias com nome; elemento com tipo do domínio e raia
/// que existe; tarefa, subprocesso e ligação com outro fluxo com nome; artefatos só na tarefa e
/// no subprocesso; ligação entre elementos que existem, sem ligar um elemento a ele mesmo e sem
/// repetir o par;</item>
/// <item>um início (sem entrada, com saída) e pelo menos um fim (com entrada, sem saída); tarefa
/// e subprocesso com entrada e saída; decisão com entrada e pelo menos duas saídas, todas com
/// rótulo e sem repetir o rótulo; paralelo que abre (duas saídas ou mais) ou fecha (duas entradas
/// ou mais) caminhos; ligação com outro fluxo que só recebe (o fluxo segue em outro) ou só sai
/// (o fluxo vem de outro);</item>
/// <item>nada solto: todo elemento é alcançado a partir do início (ou de uma ligação que vem de
/// outro fluxo) e de todo elemento se chega a um fim (ou a uma ligação que segue em outro fluxo).</item>
/// </list>
/// A saída é a definição normalizada: textos sem espaço sobrando, raias na ordem (Ordem 1, 2,
/// 3...), rótulo vazio como nulo e os números calculados (<see cref="PeFluxoAnalise"/>).
/// <para>
/// Desde a E9, cada problema que torna a definição ilegível (JSON fora do contrato: o que não é
/// objeto, lista ou texto onde devia ser, id que falta, que não serve ou que se repete, tipo que
/// não existe; ou limite estourado: quantidade de raias, passos, ligações e artefatos e tamanho
/// dos textos) vai também para <see cref="Resultado.Ilegiveis"/>. A geometria do editor visual
/// só recusa (400) essas; as outras (bloco sem ligação, decisão com uma saída, ligação para o
/// que não existe, nome em branco) ela desenha e lista em Erros. Os textos são os mesmos.
/// </para>
/// </summary>
public static partial class PeFluxoDefinicaoLeitor
{
    public const int MaximoRaias = 12;
    public const int MaximoElementos = 120;
    public const int MaximoLigacoes = 240;
    public const int MaximoArtefatos = 6;
    public const int MaximoNomeRaia = 120;
    public const int MaximoNome = 200;
    public const int MaximoArtefato = 120;
    public const int MaximoRotulo = 60;
    public const int MaximoId = 40;
    public const int MaximoErros = 25;

    private static readonly JsonSerializerOptions Saida = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly JsonSerializerOptions Entrada = new() { PropertyNameCaseInsensitive = true };

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex IdValido();

    [GeneratedRegex("^[0-9]{1,3}(\\.[0-9]{1,3}){0,2}$")]
    private static partial Regex PrefixoValido();

    [GeneratedRegex("\\s+")]
    private static partial Regex Espacos();

    /// <summary>
    /// A definição lida (mesmo com erros, o que deu para ler) e os erros em linguagem simples.
    /// Ilegiveis: a parte dos erros que impede desenhar a definição (fora do contrato ou além dos
    /// limites); vazia quando dá para desenhar.
    /// </summary>
    public sealed record Resultado(PeFluxoDefinicao? Definicao, List<string> Erros)
    {
        public List<string> Ilegiveis { get; init; } = new();

        public bool Valida => Definicao != null && Erros.Count == 0;

        public bool Legivel => Definicao != null && Ilegiveis.Count == 0;
    }

    /// <summary>Lê e confere; válida, sai numerada.</summary>
    public static Resultado Ler(JsonElement? json)
    {
        var erros = new List<string>();
        var ilegiveis = new List<string>();
        var definicao = Estrutura(json, erros, ilegiveis);
        if (definicao != null && erros.Count == 0) Grafo(definicao, erros);
        if (erros.Count > MaximoErros) erros = Cortar(erros);
        if (ilegiveis.Count > MaximoErros) ilegiveis = Cortar(ilegiveis);
        if (definicao != null && erros.Count == 0) PeFluxoAnalise.Numerar(definicao);
        return new Resultado(definicao, erros) { Ilegiveis = ilegiveis };
    }

    private static List<string> Cortar(List<string> erros) =>
        erros.Take(MaximoErros).Append($"E mais {erros.Count - MaximoErros} problemas.").ToList();

    /// <summary>Lê, confere e numera; com erro, lança <see cref="PeFluxoInvalidoException"/> (400).</summary>
    public static PeFluxoDefinicao LerValida(JsonElement? json)
    {
        var resultado = Ler(json);
        if (!resultado.Valida) throw new PeFluxoInvalidoException(resultado.Erros.Count > 0 ? resultado.Erros : new List<string> { "Envie a definição do fluxo." });
        return resultado.Definicao!;
    }

    /// <summary>
    /// A definição guardada no banco (já conferida quando foi gravada), com os números
    /// calculados de novo (a regra da numeração vale sempre a de hoje).
    /// </summary>
    public static PeFluxoDefinicao DoBanco(string? json)
    {
        PeFluxoDefinicao? definicao = null;
        try
        {
            definicao = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PeFluxoDefinicao>(json, Entrada);
        }
        catch (JsonException)
        {
            definicao = null;
        }
        definicao ??= new PeFluxoDefinicao();
        definicao.Raias ??= new List<PeFluxoRaia>();
        definicao.Elementos ??= new List<PeFluxoElemento>();
        definicao.Ligacoes ??= new List<PeFluxoLigacao>();
        foreach (var e in definicao.Elementos)
        {
            e.Artefatos ??= new List<string>();
            e.Nome ??= string.Empty;
        }
        definicao.Raias = definicao.Raias.OrderBy(r => r.Ordem).ToList();
        PeFluxoAnalise.Numerar(definicao);
        return definicao;
    }

    /// <summary>A definição como vai para o jsonb (e para o hash): PascalCase, com os acentos legíveis.</summary>
    public static string ParaJson(PeFluxoDefinicao definicao) => JsonSerializer.Serialize(definicao, Saida);

    /// <summary>Uma cópia independente (a numeração e a resolução dos nomes não mexem no original).</summary>
    public static PeFluxoDefinicao Copiar(PeFluxoDefinicao definicao) =>
        JsonSerializer.Deserialize<PeFluxoDefinicao>(ParaJson(definicao), Entrada)!;

    /// <summary>Texto sem espaço sobrando (quebras de linha e tabulações viram um espaço).</summary>
    public static string Limpar(string? texto) => Espacos().Replace(texto ?? string.Empty, " ").Trim();

    // ── Estrutura ───────────────────────────────────────────────────────────

    private static PeFluxoDefinicao? Estrutura(JsonElement? json, List<string> erros, List<string> ilegiveis)
    {
        // O problema que impede desenhar: vai para as duas listas
        void Ilegivel(string mensagem)
        {
            erros.Add(mensagem);
            ilegiveis.Add(mensagem);
        }

        if (json is not { ValueKind: JsonValueKind.Object } raiz)
        {
            Ilegivel("Envie a definição do fluxo como um objeto com Raias, Elementos e Ligacoes.");
            return null;
        }

        var definicao = new PeFluxoDefinicao();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        // Prefixo da numeração
        if (Propriedade(raiz, "PrefixoNumeracao") is { } prefixo && prefixo.ValueKind != JsonValueKind.Null)
        {
            var texto = prefixo.ValueKind switch
            {
                JsonValueKind.String => Limpar(prefixo.GetString()),
                JsonValueKind.Number => prefixo.GetRawText(),
                _ => null
            };
            if (texto == null || (texto.Length > 0 && !PrefixoValido().IsMatch(texto)))
                erros.Add("O prefixo da numeração usa só números, como 1 ou 4 (ou fica vazio, sem numeração).");
            else if (texto.Length > 0)
                definicao.PrefixoNumeracao = texto;
        }

        // Raias
        var raias = Lista(raiz, "Raias", "Raias", erros, ilegiveis);
        if (raias != null)
        {
            if (raias.Count == 0) erros.Add("Inclua pelo menos uma raia (quem faz os passos).");
            if (raias.Count > MaximoRaias) Ilegivel($"O fluxo aceita até {MaximoRaias} raias.");
            var posicao = 0;
            var lidas = new List<(PeFluxoRaia Raia, int Ordem, int Posicao)>();
            foreach (var item in raias.Take(MaximoRaias))
            {
                posicao++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    Ilegivel($"A raia {posicao} precisa ser um objeto com Id, Nome e Ordem.");
                    continue;
                }
                var raia = new PeFluxoRaia { Id = Id(item, $"a raia {posicao}", ids, erros, ilegiveis) ?? string.Empty };
                raia.Nome = Limpar(TextoOuNulo(item, "Nome"));
                if (raia.Nome.Length == 0) erros.Add($"Dê um nome à raia {posicao}.");
                else if (raia.Nome.Length > MaximoNomeRaia) Ilegivel($"O nome da raia \"{Curto(raia.Nome)}\" passa de {MaximoNomeRaia} caracteres.");
                var ordem = Propriedade(item, "Ordem") is { ValueKind: JsonValueKind.Number } o && o.TryGetInt32(out var n) ? n : posicao;
                lidas.Add((raia, ordem, posicao));
            }
            var numero = 0;
            foreach (var (raia, _, _) in lidas.OrderBy(x => x.Ordem).ThenBy(x => x.Posicao))
            {
                raia.Ordem = ++numero;
                definicao.Raias.Add(raia);
            }
        }

        // Elementos
        var elementos = Lista(raiz, "Elementos", "Elementos", erros, ilegiveis);
        if (elementos != null)
        {
            if (elementos.Count > MaximoElementos) Ilegivel($"O fluxo aceita até {MaximoElementos} passos.");
            var posicao = 0;
            foreach (var item in elementos.Take(MaximoElementos))
            {
                posicao++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    Ilegivel($"O passo {posicao} precisa ser um objeto com Id, Tipo, RaiaId e Nome.");
                    continue;
                }
                var elemento = new PeFluxoElemento
                {
                    Tipo = Limpar(TextoOuNulo(item, "Tipo")).ToLowerInvariant(),
                    Nome = Limpar(TextoOuNulo(item, "Nome")),
                    RaiaId = Limpar(TextoOuNulo(item, "RaiaId"))
                };
                var quem = Quem(elemento, posicao);
                elemento.Id = Id(item, quem, ids, erros, ilegiveis) ?? string.Empty;

                if (!PeDominios.TipoElementoFluxo.Todos.Contains(elemento.Tipo))
                    Ilegivel(Maiuscula($"{quem}: o tipo \"{Curto(elemento.Tipo)}\" não existe. Use início, fim, ligação com outro fluxo, tarefa, subprocesso, decisão ou paralelo (inicio, fim, ligacao, tarefa, subprocesso, decisao, paralelo)."));
                if (elemento.RaiaId.Length == 0)
                    erros.Add(Maiuscula($"{quem}: escolha a raia."));
                else if (definicao.Raias.All(r => r.Id != elemento.RaiaId))
                    erros.Add(Maiuscula($"{quem}: a raia escolhida não existe."));

                if (elemento.Nome.Length > MaximoNome)
                    Ilegivel(Maiuscula($"{quem}: o nome passa de {MaximoNome} caracteres."));
                else if (elemento.Nome.Length == 0 && (PeDominios.TipoElementoFluxo.EhAtividade(elemento.Tipo) || elemento.Tipo == PeDominios.TipoElementoFluxo.Ligacao))
                    erros.Add(elemento.Tipo == PeDominios.TipoElementoFluxo.Ligacao
                        ? $"Dê um nome à ligação com outro fluxo (o passo {posicao}), dizendo de onde o fluxo vem ou para onde segue."
                        : $"Dê um nome ao passo {posicao} ({NomeDoTipo(elemento.Tipo)}).");

                var artefatos = Propriedade(item, "Artefatos");
                if (artefatos is { ValueKind: not JsonValueKind.Null })
                {
                    if (artefatos.Value.ValueKind != JsonValueKind.Array)
                        Ilegivel(Maiuscula($"{quem}: os artefatos vêm numa lista de nomes."));
                    else
                    {
                        foreach (var a in artefatos.Value.EnumerateArray())
                        {
                            var nome = a.ValueKind == JsonValueKind.String ? Limpar(a.GetString()) : null;
                            if (string.IsNullOrEmpty(nome))
                            {
                                erros.Add(Maiuscula($"{quem}: dê um nome a cada artefato."));
                                continue;
                            }
                            if (nome.Length > MaximoArtefato)
                                Ilegivel(Maiuscula($"{quem}: o artefato \"{Curto(nome)}\" passa de {MaximoArtefato} caracteres."));
                            elemento.Artefatos.Add(nome);
                        }
                        if (elemento.Artefatos.Count > 0 && !PeDominios.TipoElementoFluxo.EhAtividade(elemento.Tipo))
                            erros.Add(Maiuscula($"{quem}: só tarefas e subprocessos têm artefatos."));
                        if (elemento.Artefatos.Count > MaximoArtefatos)
                            Ilegivel(Maiuscula($"{quem}: tem artefatos demais (até {MaximoArtefatos})."));
                    }
                }
                definicao.Elementos.Add(elemento);
            }
        }

        // Ligações
        var ligacoes = Lista(raiz, "Ligacoes", "Ligacoes", erros, ilegiveis);
        if (ligacoes != null)
        {
            if (ligacoes.Count > MaximoLigacoes) Ilegivel($"O fluxo aceita até {MaximoLigacoes} ligações.");
            var porId = definicao.Elementos.Where(e => e.Id.Length > 0).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
            var pares = new HashSet<(string, string)>();
            var posicao = 0;
            foreach (var item in ligacoes.Take(MaximoLigacoes))
            {
                posicao++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    Ilegivel($"A ligação {posicao} precisa ser um objeto com Id, De e Para.");
                    continue;
                }
                var ligacao = new PeFluxoLigacao
                {
                    De = Limpar(TextoOuNulo(item, "De")),
                    Para = Limpar(TextoOuNulo(item, "Para"))
                };
                var rotulo = Limpar(TextoOuNulo(item, "Rotulo"));
                ligacao.Rotulo = rotulo.Length == 0 ? null : rotulo;
                ligacao.Id = Id(item, $"a ligação {posicao}", ids, erros, ilegiveis) ?? string.Empty;

                if (ligacao.De.Length == 0 || ligacao.Para.Length == 0)
                {
                    erros.Add($"A ligação {posicao} precisa dizer de onde sai (De) e para onde vai (Para).");
                }
                else
                {
                    var de = porId.GetValueOrDefault(ligacao.De);
                    var para = porId.GetValueOrDefault(ligacao.Para);
                    if (de == null) erros.Add($"A ligação {posicao} sai de um passo que não existe.");
                    if (para == null) erros.Add($"A ligação {posicao} vai para um passo que não existe.");
                    if (de != null && para != null)
                    {
                        if (ligacao.De == ligacao.Para)
                            erros.Add(Maiuscula($"{Quem(de)}: uma ligação não pode sair e voltar para o mesmo passo."));
                        else if (!pares.Add((ligacao.De, ligacao.Para)))
                            erros.Add($"Há duas ligações {ComDe(Quem(de))} para {Quem(para)}. Deixe só uma.");
                    }
                }
                if (ligacao.Rotulo is { Length: > MaximoRotulo })
                    Ilegivel($"O rótulo \"{Curto(ligacao.Rotulo)}\" da ligação {posicao} passa de {MaximoRotulo} caracteres.");
                definicao.Ligacoes.Add(ligacao);
            }
        }

        return definicao;
    }

    // ── Regras do grafo ─────────────────────────────────────────────────────

    private static void Grafo(PeFluxoDefinicao definicao, List<string> erros)
    {
        var elementos = definicao.Elementos;
        var saidas = elementos.ToDictionary(e => e.Id, _ => new List<PeFluxoLigacao>());
        var entradas = elementos.ToDictionary(e => e.Id, _ => new List<PeFluxoLigacao>());
        foreach (var l in definicao.Ligacoes)
        {
            saidas[l.De].Add(l);
            entradas[l.Para].Add(l);
        }
        var porId = elementos.ToDictionary(e => e.Id);

        var inicios = elementos.Where(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio).ToList();
        if (inicios.Count == 0) erros.Add("O fluxo precisa de um início.");
        if (inicios.Count > 1) erros.Add($"O fluxo tem {inicios.Count} inícios. Deixe só um.");
        if (elementos.All(e => e.Tipo != PeDominios.TipoElementoFluxo.Fim)) erros.Add("O fluxo precisa de pelo menos um fim.");

        foreach (var e in elementos)
        {
            var quem = Maiuscula(Quem(e));
            int ent = entradas[e.Id].Count, sai = saidas[e.Id].Count;
            switch (e.Tipo)
            {
                case PeDominios.TipoElementoFluxo.Inicio:
                    if (ent > 0) erros.Add("O início não recebe ligação: nada vem antes dele.");
                    if (sai == 0)
                    {
                        erros.Add("Ligue o início ao primeiro passo do fluxo.");
                    }
                    break;
                case PeDominios.TipoElementoFluxo.Fim:
                    if (sai > 0) erros.Add($"{quem} não tem saída: é onde o fluxo termina.");
                    if (ent == 0)
                    {
                        erros.Add($"{quem} está solto: ligue o último passo a ele.");
                    }
                    break;
                case PeDominios.TipoElementoFluxo.Ligacao:
                    if (ent > 0 && sai > 0)
                        erros.Add($"{quem} só recebe (o fluxo segue em outro) ou só sai (o fluxo vem de outro), não os dois.");
                    if (ent == 0 && sai == 0)
                    {
                        erros.Add($"{quem} está solta: ligue-a a um passo.");
                    }
                    break;
                case PeDominios.TipoElementoFluxo.Tarefa:
                case PeDominios.TipoElementoFluxo.Subprocesso:
                    if (ent == 0)
                    {
                        var pronome = e.Tipo == PeDominios.TipoElementoFluxo.Subprocesso ? "dele" : "dela";
                        erros.Add($"{quem} não tem de onde vir: diga o que vem antes {pronome}.");
                    }
                    if (sai == 0)
                    {
                        erros.Add($"{quem} não leva a lugar nenhum: diga o que vem depois.");
                    }
                    break;
                case PeDominios.TipoElementoFluxo.Decisao:
                {
                    if (ent == 0)
                    {
                        erros.Add($"{quem} não tem de onde vir: diga o que vem antes dela.");
                    }
                    var suas = saidas[e.Id];
                    if (suas.Count < 2)
                    {
                        erros.Add($"{quem} precisa de pelo menos duas saídas (por exemplo, Sim e Não).");
                        
                    }
                    if (suas.Any(l => l.Rotulo == null))
                        erros.Add($"{quem}: dê um rótulo a cada saída (por exemplo, Sim e Não).");
                    var repetidos = suas.Where(l => l.Rotulo != null)
                        .GroupBy(l => l.Rotulo!.ToLowerInvariant())
                        .Where(g => g.Count() > 1)
                        .Select(g => g.First().Rotulo!)
                        .ToList();
                    foreach (var r in repetidos) erros.Add($"{quem} tem duas saídas com o rótulo \"{r}\".");
                    break;
                }
                case PeDominios.TipoElementoFluxo.Paralelo:
                    if (ent == 0 || sai == 0)
                    {
                        erros.Add($"{quem} precisa de entrada e de saída.");
                    }
                    else if (ent < 2 && sai < 2)
                    {
                        erros.Add($"{quem} precisa abrir caminhos (duas saídas ou mais) ou juntar caminhos (duas entradas ou mais).");
                    }
                    break;
            }
        }
        if (erros.Count > 0) return;

        // Nada solto: alcançado a partir de uma entrada e com um caminho até uma saída
        var entradasDoFluxo = elementos
            .Where(e => e.Tipo == PeDominios.TipoElementoFluxo.Inicio || (e.Tipo == PeDominios.TipoElementoFluxo.Ligacao && entradas[e.Id].Count == 0))
            .Select(e => e.Id)
            .ToList();
        var saidasDoFluxo = elementos
            .Where(e => e.Tipo == PeDominios.TipoElementoFluxo.Fim || (e.Tipo == PeDominios.TipoElementoFluxo.Ligacao && saidas[e.Id].Count == 0))
            .Select(e => e.Id)
            .ToList();
        var alcancados = Alcance(entradasDoFluxo, id => saidas[id].Select(l => l.Para));
        var chegam = Alcance(saidasDoFluxo, id => entradas[id].Select(l => l.De));
        foreach (var e in elementos)
        {
            var (a, o) = Feminino(e.Tipo) ? ("a", "a") : ("o", "o");
            if (!alcancados.Contains(e.Id))
                erros.Add(Maiuscula($"{Quem(e)} não é alcançad{a} a partir do início: ligue-{o} ao caminho do fluxo."));
            else if (!chegam.Contains(e.Id))
                erros.Add(Maiuscula($"{Quem(e)} não leva a nenhum fim: todo caminho precisa terminar num fim."));
        }
    }

    private static HashSet<string> Alcance(IEnumerable<string> origens, Func<string, IEnumerable<string>> vizinhos)
    {
        var vistos = new HashSet<string>(origens);
        var fila = new Queue<string>(vistos);
        while (fila.Count > 0)
            foreach (var v in vizinhos(fila.Dequeue()))
                if (vistos.Add(v)) fila.Enqueue(v);
        return vistos;
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static JsonElement? Propriedade(JsonElement objeto, string nome)
    {
        foreach (var p in objeto.EnumerateObject())
            if (string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static string? TextoOuNulo(JsonElement objeto, string nome) =>
        Propriedade(objeto, nome) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static List<JsonElement>? Lista(JsonElement raiz, string nome, string rotulo, List<string> erros, List<string> ilegiveis)
    {
        List<JsonElement>? Recusar(string mensagem)
        {
            erros.Add(mensagem);
            ilegiveis.Add(mensagem);
            return null;
        }

        var valor = Propriedade(raiz, nome);
        if (valor == null || valor.Value.ValueKind == JsonValueKind.Null)
            return Recusar($"Falta a lista {rotulo} na definição do fluxo.");
        if (valor.Value.ValueKind != JsonValueKind.Array)
            return Recusar($"{rotulo} precisa ser uma lista.");
        return valor.Value.EnumerateArray().ToList();
    }

    /// <summary>O id do item; o que falta, não serve ou se repete deixa a definição ilegível.</summary>
    private static string? Id(JsonElement item, string quem, HashSet<string> ids, List<string> erros, List<string> ilegiveis)
    {
        string? Recusar(string mensagem)
        {
            erros.Add(mensagem);
            ilegiveis.Add(mensagem);
            return null;
        }

        var bruto = Propriedade(item, "Id");
        var id = bruto switch
        {
            { ValueKind: JsonValueKind.String } s => s.GetString()?.Trim(),
            { ValueKind: JsonValueKind.Number } n => n.GetRawText(),
            _ => null
        };
        if (string.IsNullOrEmpty(id))
            return Recusar(Maiuscula($"{quem}: falta o id."));
        if (id.Length > MaximoId || !IdValido().IsMatch(id))
            return Recusar(Maiuscula($"{quem}: o id \"{Curto(id)}\" não serve. Use até {MaximoId} letras, números, hífen ou sublinhado."));
        if (!ids.Add(id))
            return Recusar($"O id \"{id}\" aparece mais de uma vez. Cada raia, passo e ligação precisa de um id próprio.");
        return id;
    }

    /// <summary>Como o elemento aparece nas mensagens: pelo tipo e pelo nome ("a tarefa \"Aprovar o plano\"").</summary>
    public static string Quem(PeFluxoElemento e, int? posicao = null)
    {
        var nome = e.Nome.Length > 0 ? $" \"{Curto(e.Nome)}\"" : (posicao != null ? $" {posicao}" : (e.Id.Length > 0 ? $" {e.Id}" : string.Empty));
        return e.Tipo switch
        {
            PeDominios.TipoElementoFluxo.Inicio => "o início",
            PeDominios.TipoElementoFluxo.Fim => "o fim" + (e.Nome.Length > 0 ? nome : string.Empty),
            PeDominios.TipoElementoFluxo.Ligacao => "a ligação com outro fluxo" + nome,
            PeDominios.TipoElementoFluxo.Tarefa => "a tarefa" + nome,
            PeDominios.TipoElementoFluxo.Subprocesso => "o subprocesso" + nome,
            PeDominios.TipoElementoFluxo.Decisao => "a decisão" + nome,
            PeDominios.TipoElementoFluxo.Paralelo => "o paralelo" + nome,
            _ => "o passo" + nome
        };
    }

    public static string NomeDoTipo(string tipo) => tipo switch
    {
        PeDominios.TipoElementoFluxo.Inicio => "início",
        PeDominios.TipoElementoFluxo.Fim => "fim",
        PeDominios.TipoElementoFluxo.Ligacao => "ligação com outro fluxo",
        PeDominios.TipoElementoFluxo.Tarefa => "tarefa",
        PeDominios.TipoElementoFluxo.Subprocesso => "subprocesso",
        PeDominios.TipoElementoFluxo.Decisao => "decisão",
        PeDominios.TipoElementoFluxo.Paralelo => "paralelo",
        _ => tipo
    };

    /// <summary>"a tarefa" vira "da tarefa"; "o fim", "do fim".</summary>
    private static string ComDe(string quem) =>
        quem.StartsWith("a ", StringComparison.Ordinal) ? "da " + quem[2..]
        : quem.StartsWith("o ", StringComparison.Ordinal) ? "do " + quem[2..]
        : "de " + quem;

    private static bool Feminino(string tipo) =>
        tipo is PeDominios.TipoElementoFluxo.Tarefa or PeDominios.TipoElementoFluxo.Decisao or PeDominios.TipoElementoFluxo.Ligacao;

    private static string Curto(string texto) => texto.Length <= 60 ? texto : texto[..57] + "...";

    private static string Maiuscula(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
