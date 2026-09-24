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
/// continua escrito na prévia (o front destaca, com o link para o passo 1.2) e sai em branco
/// no PDF. Os de aprovação chegam na E7.
/// <list type="bullet">
/// <item>órgão: {orgao.nome} e {orgao.sigla} (a do dicionário de nomes; sem ela, a do cadastro);</item>
/// <item>vigência (passo 1.1): {vigencia.inicio} e {vigencia.fim}, em dd/mm/aaaa;</item>
/// <item>dicionário de nomes (passo 1.2): {nomes.comite}, {nomes.equipe},
/// {nomes.equipe_acompanhamento}, {nomes.autoridade}, {nomes.autoridade_cargo},
/// {nomes.unidade_tic} e {nomes.chave} de todo campo de texto do dicionário, inclusive os que
/// o administrador criar;</item>
/// <item>{pdtic.versao} e {hoje} (no PDF, a data em que foi gerado).</item>
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
        new Marcador("hoje", "Data de hoje (no PDF, a data em que ele foi gerado)", "24/09/2026")
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
