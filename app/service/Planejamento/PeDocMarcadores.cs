using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Marcadores dos textos do documento: escritos como texto no JSON do TipTap
/// ("{orgao.nome}") e trocados pelos valores do órgão na prévia e no PDF. Só as chaves
/// conhecidas são marcadores; o resto entre chaves fica como está. Marcador sem valor:
/// continua escrito na prévia (o front destaca) e sai em branco no PDF, com o que sobra dele
/// limpo (<see cref="ResolverParaPdf"/>).
/// <list type="bullet">
/// <item>órgão: {orgao.nome} e {orgao.sigla} (a do dicionário de nomes; sem ela, a do cadastro);</item>
/// <item>vigência (passo 1.1): {vigencia.inicio} e {vigencia.fim}, em dd/mm/aaaa;</item>
/// <item>dicionário de nomes (passo 1.2): {nomes.comite}, {nomes.equipe},
/// {nomes.equipe_acompanhamento}, {nomes.autoridade}, {nomes.autoridade_cargo},
/// {nomes.unidade_tic} e {nomes.chave} de todo campo de texto do dicionário, inclusive os que
/// o administrador criar;</item>
/// <item>{pdtic.versao} e {hoje} (no PDF, a data em que foi gerado);</item>
/// <item>aprovação e publicação (E7): {aprovacao.sgtic.data} e {aprovacao.sgtic.ato} (a
/// seção da aprovação do SGTIC, com a decisão "aprovado"), {aprovacao.cgtic.data} e
/// {aprovacao.cgtic.ato} (a deliberação aprovada do CGTIC) e {publicacao.data} e
/// {publicacao.endereco} (a seção da publicação);</item>
/// <item>ciclo (E7, rodada B): {ciclo.rotulo}, {ciclo.inicio} e {ciclo.fim}, do ciclo do
/// relatório de acompanhamento (RA); no PDTIC e no RR, sem valor.</item>
/// </list>
/// </summary>
public static partial class PeDocMarcadores
{
    public sealed record Marcador(string Chave, string Descricao, string Exemplo);

    /// <summary>Os marcadores fixos, na ordem em que o editor do modelo mostra.</summary>
    public static readonly IReadOnlyList<Marcador> Fixos = new[]
    {
        new Marcador("orgao.nome", "Nome do órgão", "Secretaria de Estado de Saúde"),
        new Marcador("orgao.sigla", "Sigla do órgão (a do dicionário de nomes; sem ela, a do cadastro do órgão)", "SES"),
        new Marcador("vigencia.inicio", "Início da vigência do PDTIC (passo 1.1)", "01/01/2026"),
        new Marcador("vigencia.fim", "Fim da vigência do PDTIC (passo 1.1)", "31/12/2029"),
        new Marcador("nomes.comite", "Nome do comitê interno de TIC, o SGTIC (dicionário de nomes)", "Subcomitê Gestor de TIC"),
        new Marcador("nomes.equipe", "Nome da equipe de elaboração (dicionário de nomes)", "Equipe de Elaboração do PDTIC"),
        new Marcador("nomes.equipe_acompanhamento", "Nome da equipe de acompanhamento (dicionário de nomes)", "Equipe de Acompanhamento do PDTIC"),
        new Marcador("nomes.autoridade", "Nome da autoridade máxima do órgão (dicionário de nomes)", "Maria da Silva"),
        new Marcador("nomes.autoridade_cargo", "Cargo da autoridade máxima (dicionário de nomes)", "Secretária de Estado de Saúde"),
        new Marcador("nomes.unidade_tic", "Nome da unidade de TIC (dicionário de nomes)", "Subsecretaria de Tecnologia da Informação"),
        new Marcador("pdtic.versao", "Versão do PDTIC", "1.0"),
        new Marcador("hoje", "Data de hoje (no PDF, a data em que ele foi gerado)", "24/09/2026"),
        new Marcador("aprovacao.sgtic.data", "Data da aprovação do PDTIC pelo comitê interno de TIC, o SGTIC (passo do envio)", "10/03/2027"),
        new Marcador("aprovacao.sgtic.ato", "Ato da aprovação pelo SGTIC: tipo e número (passo do envio)", "Ata de reunião nº 3/2027"),
        new Marcador("aprovacao.cgtic.data", "Data do ato do CGTIC que aprovou o PDTIC", "20/04/2027"),
        new Marcador("aprovacao.cgtic.ato", "Ato do CGTIC que aprovou o PDTIC: tipo e número", "Resolução nº 12/2027"),
        new Marcador("publicacao.data", "Data da publicação do PDTIC (passo da publicação)", "30/04/2027"),
        new Marcador("publicacao.endereco", "Endereço da íntegra do PDTIC na internet (passo da publicação)", "https://www.saude.df.gov.br/pdtic"),
        new Marcador("ciclo.rotulo", "Nome do ciclo do relatório de acompanhamento (só no RA)", "2027 · 1º trimestre"),
        new Marcador("ciclo.inicio", "Início do ciclo do relatório de acompanhamento (só no RA)", "01/01/2027"),
        new Marcador("ciclo.fim", "Fim do ciclo do relatório de acompanhamento (só no RA; na avaliação aberta, sem valor)", "31/03/2027")
    };

    /// <summary>Os marcadores do ciclo (E7, rodada B): com valor só no relatório de acompanhamento (RA) de um ciclo.</summary>
    public static readonly string[] DoCiclo = { "ciclo.rotulo", "ciclo.inicio", "ciclo.fim" };

    /// <summary>Os marcadores de aprovação e de publicação (E7), sem valor até cada momento do caminho.</summary>
    public static readonly string[] DaAprovacao =
    {
        "aprovacao.sgtic.data", "aprovacao.sgtic.ato", "aprovacao.cgtic.data", "aprovacao.cgtic.ato", "publicacao.data", "publicacao.endereco"
    };

    /// <summary>Marcadores do dicionário com nome curto: a chave do marcador e o campo da seção nomes.</summary>
    public static readonly IReadOnlyDictionary<string, string> Apelidos = new Dictionary<string, string>
    {
        ["nomes.equipe"] = PeDominios.DicionarioNomes.Equipe,
        ["nomes.autoridade"] = PeDominios.DicionarioNomes.AutoridadeNome
    };

    // Campos do dicionário que já têm marcador fixo com outro nome (não entram de novo na lista)
    private static readonly HashSet<string> CobertosPorFixos = new(StringComparer.Ordinal)
    {
        PeDominios.DicionarioNomes.Equipe, PeDominios.DicionarioNomes.AutoridadeNome, PeDominios.DicionarioNomes.SiglaOrgao
    };

    [GeneratedRegex(@"\{([a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)*)\}")]
    private static partial Regex Padrao();

    /// <summary>
    /// A lista para o editor do modelo: os fixos e um por campo de texto do dicionário de nomes
    /// que ainda não tem marcador fixo (os que o administrador criou).
    /// </summary>
    public static List<Marcador> Lista(IEnumerable<PeCampo> camposDoDicionario)
    {
        var lista = Fixos.ToList();
        foreach (var campo in camposDoDicionario.Where(c => c.ExcluidoEm == null && EhDeTexto(c)).OrderBy(c => c.Ordem).ThenBy(c => c.Id))
        {
            var chave = "nomes." + campo.Chave;
            if (CobertosPorFixos.Contains(campo.Chave) || lista.Any(m => m.Chave == chave)) continue;
            lista.Add(new Marcador(chave, $"{campo.Rotulo} (dicionário de nomes)", campo.Rotulo));
        }
        return lista;
    }

    /// <summary>Campo do dicionário que vira marcador: o que tem texto para mostrar (não arquivo nem ligação).</summary>
    public static bool EhDeTexto(PeCampo campo) =>
        campo.Tipo is not (PeDominios.TipoCampo.Arquivo or PeDominios.TipoCampo.LigacaoSecao or PeDominios.TipoCampo.LigacaoCatalogo);

    /// <summary>As chaves de marcador que aparecem no texto (só as conhecidas), sem repetir, na ordem.</summary>
    public static List<string> Encontrados(JsonNode? documento, IReadOnlyDictionary<string, string?> valores)
    {
        var saida = new List<string>();
        foreach (var texto in Textos(documento))
            foreach (Match m in Padrao().Matches(texto))
            {
                var chave = m.Groups[1].Value;
                if (valores.ContainsKey(chave) && !saida.Contains(chave)) saida.Add(chave);
            }
        return saida;
    }

    /// <summary>Os marcadores do texto que não têm valor para o órgão.</summary>
    public static List<string> SemValor(JsonNode? documento, IReadOnlyDictionary<string, string?> valores) =>
        Encontrados(documento, valores).Where(c => string.IsNullOrWhiteSpace(valores[c])).ToList();

    /// <summary>
    /// Uma cópia do documento com os marcadores trocados. Marcador sem valor: fica escrito
    /// (prévia) ou sai em branco (PDF, emBranco). Texto que fica vazio sai do documento.
    /// </summary>
    public static JsonNode? Resolver(JsonNode? documento, IReadOnlyDictionary<string, string?> valores, bool emBranco)
    {
        if (documento == null) return null;
        var copia = documento.DeepClone();
        Trocar(copia, valores, emBranco);
        return copia;
    }

    /// <summary>Troca os marcadores de um texto simples (títulos, rodapé).</summary>
    public static string ResolverTexto(string texto, IReadOnlyDictionary<string, string?> valores, bool emBranco) =>
        Padrao().Replace(texto, m =>
        {
            var chave = m.Groups[1].Value;
            if (!valores.TryGetValue(chave, out var valor)) return m.Value;
            return string.IsNullOrWhiteSpace(valor) ? (emBranco ? string.Empty : m.Value) : valor;
        });

    // ── PDF: o que sobra dos marcadores sem valor ──────────────────────────

    [GeneratedRegex(@"\s*\(\s*\)")]
    private static partial Regex ParentesesVazios();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex EspacosSeguidos();

    // O fim de uma frase: ponto, exclamação ou interrogação seguido de espaço e de letra
    // maiúscula (ou de um marcador), ou no fim da linha ("art. 4º" e "48.900" não terminam frase)
    [GeneratedRegex(@"[.!?](?=\s+(?:\p{Lu}|\{)|\s*$)")]
    private static partial Regex FimDeFrase();

    // Linha curta que dependia só de marcadores (todos sem valor): até estas palavras, sai inteira
    private const int PalavrasDaLinhaCurta = 4;

    private enum Estado { Mantido, Removido, EmBranco, Rotulo }

    /// <summary>
    /// O texto para o PDF: os marcadores trocados, os sem valor em branco e o que sobra deles
    /// limpo, para o documento não sair com "()" nem com rótulos sem valor (a prévia continua
    /// mostrando o marcador, destacado). Só mexe no que tinha marcador:
    /// <list type="bullet">
    /// <item>tira os parênteses que ficaram vazios, com o espaço antes ("equipe ()" vira
    /// "equipe"), e os espaços repetidos;</item>
    /// <item>tira a frase inteira com marcador sem valor fora de parênteses ("O PDTIC vale de
    /// {inicio} a {fim}." sai e fica o resto do parágrafo), porque a frase não se sustenta sem
    /// ele;</item>
    /// <item>tira a linha (parágrafo, título, item de lista ou linha separada por quebra) que
    /// ficou vazia ou só com pontuação (":" de "{cargo}: {nome}"), a que terminou em ":" sem o
    /// valor que vinha depois ("Publicação: {data}") e a linha curta (até quatro palavras) cujos
    /// marcadores ficaram todos sem valor ("Vigência: de {inicio} a {fim}");</item>
    /// <item>tira o rótulo em negrito (parágrafo só em negrito, sem marcador) quando tudo o que
    /// vinha logo abaixo dele, até a próxima linha em branco, saiu;</item>
    /// <item>juntam-se as linhas em branco que ficaram seguidas, e as das pontas saem;</item>
    /// <item>nas tabelas, só troca e tira os parênteses vazios (a célula fica).</item>
    /// </list>
    /// </summary>
    public static JsonNode? ResolverParaPdf(JsonNode? documento, IReadOnlyDictionary<string, string?> valores)
    {
        if (documento == null) return null;
        var copia = documento.DeepClone();
        if (copia is JsonObject raiz) LimparBlocos(raiz, valores);
        return copia;
    }

    /// <summary>Limpa os blocos de um contêiner (o documento, um item de lista). Devolve se tirou algum bloco.</summary>
    private static bool LimparBlocos(JsonObject container, IReadOnlyDictionary<string, string?> valores)
    {
        if (container["content"] is not JsonArray filhos) return false;
        var itens = new List<(JsonNode? No, Estado Estado)>();
        foreach (var filho in filhos.ToList())
        {
            if (filho is not JsonObject no)
            {
                itens.Add((filho, Estado.Mantido));
                continue;
            }
            switch (PeRegistroDados.Texto(no["type"]))
            {
                case "paragraph":
                case "heading":
                    itens.Add((no, LimparParagrafo(no, valores)));
                    break;
                case "bulletList":
                case "orderedList":
                    itens.Add((no, LimparLista(no, valores)));
                    break;
                case "table":
                    TrocarEmLinha(no, valores);
                    itens.Add((no, Estado.Mantido));
                    break;
                default:
                    itens.Add((no, Estado.Mantido));
                    break;
            }
        }

        // O rótulo em negrito sai quando tudo o que vinha logo abaixo dele saiu
        for (var i = 0; i < itens.Count; i++)
        {
            if (itens[i].Estado != Estado.Rotulo) continue;
            var grupo = itens.Skip(i + 1).TakeWhile(x => (x.Estado is Estado.Mantido or Estado.Removido) && EhLinha(x.No)).ToList();
            if (grupo.Count > 0 && grupo.All(x => x.Estado == Estado.Removido)) itens[i] = (itens[i].No, Estado.Removido);
        }

        if (itens.All(x => x.Estado != Estado.Removido)) return false;

        // As linhas em branco que ficaram seguidas viram uma; as das pontas saem
        var ficam = itens.Where(x => x.Estado != Estado.Removido).ToList();
        var saida = new List<JsonNode?>();
        for (var i = 0; i < ficam.Count; i++)
        {
            if (ficam[i].Estado == Estado.EmBranco
                && (saida.Count == 0 || i == ficam.Count - 1 || (i > 0 && ficam[i - 1].Estado == Estado.EmBranco)))
                continue;
            saida.Add(ficam[i].No);
        }
        while (saida.Count > 0 && saida[^1] is JsonObject ultimo && PeRegistroDados.Texto(ultimo["type"]) == "paragraph" && !PeTextoRico.TemConteudo(ultimo))
            saida.RemoveAt(saida.Count - 1);

        filhos.Clear();
        foreach (var no in saida) filhos.Add(no);
        return true;
    }

    private static bool EhLinha(JsonNode? no) =>
        no is JsonObject o && PeRegistroDados.Texto(o["type"]) is "paragraph" or "heading";

    /// <summary>A lista sem os itens que ficaram vazios; a lista vazia sai.</summary>
    private static Estado LimparLista(JsonObject lista, IReadOnlyDictionary<string, string?> valores)
    {
        if (lista["content"] is not JsonArray itens) return Estado.Mantido;
        var tirou = false;
        foreach (var item in itens.OfType<JsonObject>().ToList())
        {
            if (LimparBlocos(item, valores) && !PeTextoRico.TemConteudo(item))
            {
                itens.Remove(item);
                tirou = true;
            }
        }
        return tirou && itens.Count == 0 ? Estado.Removido : Estado.Mantido;
    }

    /// <summary>
    /// Um parágrafo (ou título): sem marcador, fica como está (em branco, rótulo em negrito ou
    /// texto); com marcador, troca, limpa e diz se ele (ou alguma linha dele) sai.
    /// </summary>
    private static Estado LimparParagrafo(JsonObject paragrafo, IReadOnlyDictionary<string, string?> valores)
    {
        if (paragrafo["content"] is not JsonArray conteudo || conteudo.Count == 0) return Estado.EmBranco;
        var nos = conteudo.OfType<JsonObject>().ToList();
        if (!nos.Any(n => TemMarcador(n, valores)))
        {
            if (!PeTextoRico.TemConteudo(paragrafo)) return Estado.EmBranco;
            return EhRotulo(paragrafo, nos) ? Estado.Rotulo : Estado.Mantido;
        }
        var original = TextoVisivel(nos).TrimEnd();

        // As linhas do parágrafo (separadas pela quebra de linha), cada uma limpa à parte
        var linhas = new List<List<JsonObject>> { new() };
        foreach (var no in nos)
        {
            if (PeRegistroDados.Texto(no["type"]) == "hardBreak") linhas.Add(new List<JsonObject>());
            else linhas[^1].Add(no);
        }
        var ficam = new List<List<JsonObject>>();
        foreach (var inteira in linhas)
        {
            var linha = TirarFrasesSemValor(inteira, valores);
            if (linha.Count == 0 && inteira.Count > 0) continue;
            var comMarcador = linha.Where(n => TemMarcador(n, valores)).ToList();
            if (comMarcador.Count == 0)
            {
                ficam.Add(linha);
                continue;
            }
            var todosSemValor = comMarcador.All(n => MarcadoresDe(n, valores).All(c => string.IsNullOrWhiteSpace(valores[c])));
            var literais = linha.Sum(n => Palavras(Padrao().Replace(PeRegistroDados.Texto(n["text"]) ?? string.Empty, " ")));
            var limpa = LimparLinha(linha, valores);
            var texto = TextoVisivel(limpa).Trim();
            if (texto.Length == 0 || !texto.Any(char.IsLetterOrDigit)) continue;
            if (todosSemValor && literais <= PalavrasDaLinhaCurta) continue;
            ficam.Add(limpa);
        }
        if (ficam.Count == 0) return Estado.Removido;

        conteudo.Clear();
        for (var i = 0; i < ficam.Count; i++)
        {
            if (i > 0) conteudo.Add(new JsonObject { ["type"] = "hardBreak" });
            foreach (var no in ficam[i]) conteudo.Add(no);
        }
        // O rótulo que ficou sem o valor que vinha depois dele ("Publicação: {data}")
        var final = TextoVisivel(conteudo.OfType<JsonObject>()).Trim();
        if (final.EndsWith(':') && !original.EndsWith(':')) return Estado.Removido;
        return Estado.Mantido;
    }

    /// <summary>
    /// Tira da linha cada frase que tem marcador sem valor fora de parênteses (o de dentro de
    /// parênteses fica para a regra dos parênteses vazios). A frase vai do fim da anterior até o
    /// próximo fim de frase (ou até o fim da linha), com o espaço depois dela; a linha que sobra
    /// fica sem espaço nas pontas. Sem frase a tirar, devolve a própria linha.
    /// </summary>
    private static List<JsonObject> TirarFrasesSemValor(List<JsonObject> linha, IReadOnlyDictionary<string, string?> valores)
    {
        var textos = linha.Select(n => PeRegistroDados.Texto(n["type"]) == "text" ? PeRegistroDados.Texto(n["text"]) ?? string.Empty : string.Empty).ToList();
        var bruto = string.Concat(textos);
        var fins = FimDeFrase().Matches(bruto).Select(m => m.Index).ToList();

        var trechos = new List<(int Inicio, int Fim)>();
        foreach (Match marcador in Padrao().Matches(bruto))
        {
            var chave = marcador.Groups[1].Value;
            if (!valores.TryGetValue(chave, out var valor) || !string.IsNullOrWhiteSpace(valor)) continue;
            if (DentroDeParenteses(bruto, marcador.Index, marcador.Length)) continue;

            var anterior = fins.LastOrDefault(f => f < marcador.Index, -1);
            var inicio = anterior + 1;
            while (inicio < marcador.Index && char.IsWhiteSpace(bruto[inicio])) inicio++;
            var seguinte = fins.FirstOrDefault(f => f >= marcador.Index + marcador.Length, -1);
            var fim = seguinte < 0 ? bruto.Length : seguinte + 1;
            while (fim < bruto.Length && char.IsWhiteSpace(bruto[fim])) fim++;
            trechos.Add((inicio, fim));
        }
        if (trechos.Count == 0) return linha;

        var saida = new List<JsonObject>();
        var posicao = 0;
        for (var i = 0; i < linha.Count; i++)
        {
            var no = linha[i];
            var texto = textos[i];
            var inicioDoNo = posicao;
            posicao += texto.Length;
            if (PeRegistroDados.Texto(no["type"]) != "text")
            {
                saida.Add(no);
                continue;
            }
            var sobra = new StringBuilder();
            for (var c = 0; c < texto.Length; c++)
            {
                var absoluta = inicioDoNo + c;
                if (!trechos.Any(t => absoluta >= t.Inicio && absoluta < t.Fim)) sobra.Append(texto[c]);
            }
            if (sobra.Length == 0) continue;
            no["text"] = sobra.ToString();
            saida.Add(no);
        }
        if (saida.FirstOrDefault(n => n["text"] != null) is { } primeiro) Aparar(primeiro, inicio: true);
        if (saida.LastOrDefault(n => n["text"] != null) is { } ultimo) Aparar(ultimo, inicio: false);
        return saida.Where(n => n["text"] == null || (PeRegistroDados.Texto(n["text"])?.Length ?? 0) > 0).ToList();
    }

    /// <summary>O trecho está entre parênteses: o parêntese mais perto antes dele abre e o mais perto depois fecha.</summary>
    private static bool DentroDeParenteses(string texto, int inicio, int tamanho)
    {
        var antes = inicio > 0 ? texto.LastIndexOfAny(new[] { '(', ')' }, inicio - 1) : -1;
        var depois = texto.IndexOfAny(new[] { '(', ')' }, inicio + tamanho);
        return antes >= 0 && texto[antes] == '(' && depois >= 0 && texto[depois] == ')';
    }

    /// <summary>Troca os marcadores da linha (sem valor = em branco), tira os parênteses vazios e os espaços que sobraram.</summary>
    private static List<JsonObject> LimparLinha(List<JsonObject> linha, IReadOnlyDictionary<string, string?> valores)
    {
        var saida = new List<JsonObject>();
        foreach (var no in linha)
        {
            if (PeRegistroDados.Texto(no["type"]) != "text" || PeRegistroDados.Texto(no["text"]) is not string texto)
            {
                saida.Add(no);
                continue;
            }
            var novo = EspacosSeguidos().Replace(ParentesesVazios().Replace(ResolverTexto(texto, valores, emBranco: true), string.Empty), " ");
            if (novo.Length == 0) continue;
            no["text"] = novo;
            saida.Add(no);
        }
        // Sem espaço sobrando nas pontas da linha
        if (saida.FirstOrDefault(n => n["text"] != null) is { } primeiro) Aparar(primeiro, inicio: true);
        if (saida.LastOrDefault(n => n["text"] != null) is { } ultimo) Aparar(ultimo, inicio: false);
        return saida.Where(n => n["text"] == null || (PeRegistroDados.Texto(n["text"])?.Length ?? 0) > 0).ToList();
    }

    private static void Aparar(JsonObject no, bool inicio)
    {
        var texto = PeRegistroDados.Texto(no["text"]) ?? string.Empty;
        no["text"] = inicio ? texto.TrimStart() : texto.TrimEnd();
    }

    /// <summary>Nas tabelas (e no que não é bloco de linha): troca, tira os parênteses vazios e os espaços repetidos, sem tirar células.</summary>
    private static void TrocarEmLinha(JsonNode? no, IReadOnlyDictionary<string, string?> valores)
    {
        if (no is not JsonObject objeto) return;
        if (PeRegistroDados.Texto(objeto["type"]) == "text" && PeRegistroDados.Texto(objeto["text"]) is string texto && TemMarcador(objeto, valores))
            objeto["text"] = EspacosSeguidos().Replace(ParentesesVazios().Replace(ResolverTexto(texto, valores, emBranco: true), string.Empty), " ");
        if (objeto["content"] is JsonArray filhos)
        {
            foreach (var filho in filhos) TrocarEmLinha(filho, valores);
            // Texto que ficou vazio sai (o TipTap não guarda texto vazio)
            foreach (var vazio in filhos.OfType<JsonObject>()
                         .Where(f => PeRegistroDados.Texto(f["type"]) == "text" && string.IsNullOrEmpty(PeRegistroDados.Texto(f["text"])))
                         .ToList())
                filhos.Remove(vazio);
            if (filhos.Count == 0) objeto.Remove("content");
        }
    }

    /// <summary>O parágrafo é um rótulo: texto só em negrito, sem marcador e sem quebra de linha.</summary>
    private static bool EhRotulo(JsonObject paragrafo, IReadOnlyList<JsonObject> nos) =>
        PeRegistroDados.Texto(paragrafo["type"]) == "paragraph"
        && nos.Count > 0
        && nos.All(n => PeRegistroDados.Texto(n["type"]) == "text"
                        && n["marks"] is JsonArray marcas
                        && marcas.OfType<JsonObject>().Any(m => PeRegistroDados.Texto(m["type"]) == "bold"))
        && nos.Any(n => !string.IsNullOrWhiteSpace(PeRegistroDados.Texto(n["text"])));

    private static bool TemMarcador(JsonObject no, IReadOnlyDictionary<string, string?> valores) =>
        MarcadoresDe(no, valores).Count > 0;

    private static List<string> MarcadoresDe(JsonObject no, IReadOnlyDictionary<string, string?> valores) =>
        PeRegistroDados.Texto(no["type"]) == "text" && PeRegistroDados.Texto(no["text"]) is string texto
            ? Padrao().Matches(texto).Select(m => m.Groups[1].Value).Where(valores.ContainsKey).ToList()
            : new List<string>();

    private static string TextoVisivel(IEnumerable<JsonObject> nos) =>
        string.Concat(nos.Select(n => PeRegistroDados.Texto(n["type"]) == "hardBreak" ? "\n" : PeRegistroDados.Texto(n["text"]) ?? string.Empty));

    private static int Palavras(string texto) =>
        texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(p => p.Any(char.IsLetterOrDigit));

    private static void Trocar(JsonNode? no, IReadOnlyDictionary<string, string?> valores, bool emBranco)
    {
        if (no is not JsonObject objeto || objeto["content"] is not JsonArray filhos) return;
        for (var i = filhos.Count - 1; i >= 0; i--)
        {
            if (filhos[i] is not JsonObject filho) continue;
            if (PeRegistroDados.Texto(filho["type"]) == "text" && PeRegistroDados.Texto(filho["text"]) is string texto)
            {
                var novo = ResolverTexto(texto, valores, emBranco);
                if (novo.Length == 0) filhos.RemoveAt(i);
                else filho["text"] = novo;
                continue;
            }
            Trocar(filho, valores, emBranco);
        }
        // Parágrafo que ficou sem texto fica sem conteúdo (o TipTap não guarda lista vazia)
        if (filhos.Count == 0) objeto.Remove("content");
    }

    private static IEnumerable<string> Textos(JsonNode? no)
    {
        if (no is not JsonObject objeto) yield break;
        if (PeRegistroDados.Texto(objeto["type"]) == "text" && PeRegistroDados.Texto(objeto["text"]) is string texto)
            yield return texto;
        if (objeto["content"] is JsonArray filhos)
            foreach (var filho in filhos)
                foreach (var t in Textos(filho))
                    yield return t;
    }

    // ── Comparação de textos ────────────────────────────────────────────────

    /// <summary>
    /// O JSON canônico (chaves em ordem, sem espaços): o jsonb do PostgreSQL reordena as chaves,
    /// então a comparação e o hash usam esta forma.
    /// </summary>
    public static string Canonico(JsonNode? no)
    {
        var sb = new StringBuilder();
        Escrever(sb, no);
        return sb.ToString();
    }

    private static void Escrever(StringBuilder sb, JsonNode? no)
    {
        switch (no)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject o:
                sb.Append('{');
                var primeiro = true;
                foreach (var (chave, valor) in o.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!primeiro) sb.Append(',');
                    primeiro = false;
                    sb.Append(JsonSerializer.Serialize(chave)).Append(':');
                    Escrever(sb, valor);
                }
                sb.Append('}');
                break;
            case JsonArray a:
                sb.Append('[');
                for (var i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Escrever(sb, a[i]);
                }
                sb.Append(']');
                break;
            default:
                sb.Append(no.ToJsonString());
                break;
        }
    }

    /// <summary>SHA-256 do JSON canônico, em hexadecimal minúsculo (vazio = hash do "null").</summary>
    public static string Hash(JsonNode? no) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonico(no)))).ToLowerInvariant();
}
