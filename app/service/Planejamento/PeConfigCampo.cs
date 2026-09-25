using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>Um campo visto pelo validador do config (irmão na mesma seção).</summary>
public sealed record PeCampoInfo(string Chave, string Tipo, IReadOnlyList<string> Opcoes, IReadOnlyList<string> OpcoesAtivas);

/// <summary>Uma seção vista pelo validador do config (alvo de ligação).</summary>
public sealed record PeSecaoInfo(string Chave, string Escopo, string Tipo);

/// <summary>
/// O que o validador precisa saber em volta do campo: o escopo da seção, os outros
/// campos da mesma seção (não apagados, com os valores das opções), as seções que podem
/// ser alvo de ligação (não apagadas) e as opções do próprio campo (resultado do nível
/// de risco).
/// </summary>
public sealed class PeContextoConfig
{
    public string Escopo { get; init; } = PeDominios.Escopo.Pdtic;

    public string ChaveDoCampo { get; init; } = string.Empty;

    public IReadOnlyList<PeCampoInfo> CamposDaSecao { get; init; } = Array.Empty<PeCampoInfo>();

    public Func<string, PeSecaoInfo?> SecaoPorChave { get; init; } = _ => null;

    public IReadOnlyList<string> OpcoesDoCampo { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Regras do config (jsonb) de cada tipo de campo, do jeito que a E2 fixou:
/// <list type="bullet">
/// <item>texto_curto { max, formato } (max de 1 a 1000; formato, desde a F1, "sei" para o número
/// de um processo SEI ou "url" para um endereço de internet completo) e texto_longo { max } (1 a
/// 50000); data { naoFutura } (desde a F1: verdadeiro recusa data depois de hoje, e só é guardado
/// quando verdadeiro); texto_rico, moeda, percentual, sim_nao, lista e lista_multipla não têm
/// config ({});</item>
/// <item>numero { min, max, casas (0 a 6), unidade };</item>
/// <item>ligacao_secao { secao, multipla }: seção do tipo tabela, do mesmo escopo, não apagada;</item>
/// <item>ligacao_catalogo { catalogo, multipla } (PeDominios.Catalogo);</item>
/// <item>arquivo { tipos (pdf, png, jpg), maxMb (1 a 25) };</item>
/// <item>calculado { calculo, campos, pesos, matriz }: produto (2 ou mais campos),
/// soma_ponderada (pesos maiores que zero para cada campo), subtracao (exatamente 2, o
/// primeiro menos o segundo) e nivel_risco (2 listas e a matriz com uma linha por opção
/// do primeiro e uma coluna por opção do segundo, e o valor de uma opção do próprio
/// campo em cada célula). Entram só campos da mesma seção: número, moeda, percentual ou
/// lista com valores numéricos (nivel_risco: listas). Nada de fórmula livre, e um
/// calculado não usa outro (sem ciclos).</item>
/// </list>
/// As chaves são lidas sem diferenciar maiúsculas; a saída é o JSON normalizado (só as
/// chaves conhecidas, com os padrões preenchidos). Chave que não serve para o tipo volta
/// 400 (PeConfigInvalida); pesos e matriz fora do cálculo deles são descartados.
/// </summary>
public static class PeConfigCampo
{
    public const string Vazio = "{}";

    private static readonly string[] TiposNumericos =
        { PeDominios.TipoCampo.Numero, PeDominios.TipoCampo.Moeda, PeDominios.TipoCampo.Percentual, PeDominios.TipoCampo.Lista };

    public static string Normalizar(string tipo, JsonElement? config, PeContextoConfig ctx)
    {
        var entrada = LerEntrada(config);
        var saida = new JsonObject();

        switch (tipo)
        {
            case PeDominios.TipoCampo.TextoCurto:
                Permitir(entrada, "max", "formato");
                Inteiro(entrada, saida, "max", 1, 1000, "O tamanho máximo do texto curto vai de 1 a 1000 caracteres.");
                if (Texto(entrada, "formato") is string formato)
                {
                    if (!PeDominios.FormatoTexto.Todos.Contains(formato))
                        throw Erro("O formato do texto curto é \"sei\" (número de processo SEI) ou \"url\" (endereço de internet).", "Config.formato");
                    saida["formato"] = formato;
                }
                break;
            case PeDominios.TipoCampo.Data:
                Permitir(entrada, "naoFutura");
                if (Booleano(entrada, "naoFutura") == true) saida["naoFutura"] = true;
                break;
            case PeDominios.TipoCampo.TextoLongo:
                Permitir(entrada, "max");
                Inteiro(entrada, saida, "max", 1, 50000, "O tamanho máximo do texto longo vai de 1 a 50000 caracteres.");
                break;
            case PeDominios.TipoCampo.Numero:
                NumeroConfig(entrada, saida);
                break;
            case PeDominios.TipoCampo.LigacaoSecao:
                LigacaoSecao(entrada, saida, ctx);
                break;
            case PeDominios.TipoCampo.LigacaoCatalogo:
                LigacaoCatalogo(entrada, saida);
                break;
            case PeDominios.TipoCampo.Arquivo:
                Arquivo(entrada, saida);
                break;
            case PeDominios.TipoCampo.Calculado:
                Calculado(entrada, saida, ctx);
                break;
            default:
                if (!PeDominios.TipoCampo.Todos.Contains(tipo))
                    throw Erro($"Tipo de campo inválido: {tipo}.", "Tipo");
                Permitir(entrada);
                break;
        }

        return saida.ToJsonString(PeModeloService.JsonHistorico);
    }

    // ── Leitura do config guardado (quem usa o quê) ─────────────────────────

    /// <summary>Chaves dos campos que entram no cálculo (vazio se não for calculado).</summary>
    public static IReadOnlyList<string> CamposDoCalculo(string configJson)
    {
        var raiz = Parse(configJson);
        if (raiz?["campos"] is not JsonArray campos) return Array.Empty<string>();
        return campos.Select(c => c?.GetValue<string>() ?? string.Empty).Where(c => c.Length > 0).ToList();
    }

    /// <summary>O cálculo guardado (produto, soma_ponderada, nivel_risco, subtracao) ou nulo.</summary>
    public static string? TipoDoCalculo(string configJson) => Parse(configJson)?["calculo"]?.GetValue<string>();

    /// <summary>Chave da seção alvo de um campo de ligação com seção, ou nulo.</summary>
    public static string? SecaoDaLigacao(string configJson) => Parse(configJson)?["secao"]?.GetValue<string>();

    /// <summary>Valores usados na matriz do nível de risco (linhas, colunas e células).</summary>
    public static ISet<string> ValoresDaMatriz(string configJson)
    {
        var valores = new HashSet<string>();
        if (Parse(configJson)?["matriz"] is not JsonObject matriz) return valores;
        foreach (var (linha, colunas) in matriz)
        {
            valores.Add(linha);
            if (colunas is not JsonObject cels) continue;
            foreach (var (coluna, valor) in cels)
            {
                valores.Add(coluna);
                if (valor is JsonValue v && v.TryGetValue<string>(out var s)) valores.Add(s);
            }
        }
        return valores;
    }

    /// <summary>O cálculo precisa de valores numéricos nas opções dos campos que usa.</summary>
    public static bool CalculoNumerico(string? calculo) =>
        calculo is PeDominios.Calculo.Produto or PeDominios.Calculo.SomaPonderada or PeDominios.Calculo.Subtracao;

    public static bool EhNumero(string valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    private static JsonObject? Parse(string configJson)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(configJson) ? Vazio : configJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Tipos ───────────────────────────────────────────────────────────────

    private static void NumeroConfig(Dictionary<string, JsonElement> entrada, JsonObject saida)
    {
        Permitir(entrada, "min", "max", "casas", "unidade");
        var min = Decimal(entrada, "min");
        var max = Decimal(entrada, "max");
        if (min != null && max != null && min > max)
            throw Erro("O mínimo não pode ser maior que o máximo.", "Config.min");
        if (min != null) saida["min"] = min;
        if (max != null) saida["max"] = max;
        Inteiro(entrada, saida, "casas", 0, 6, "As casas decimais vão de 0 a 6.");
        var unidade = Texto(entrada, "unidade");
        if (unidade != null)
        {
            if (unidade.Length > 30) throw Erro("A unidade tem no máximo 30 caracteres.", "Config.unidade");
            saida["unidade"] = unidade;
        }
    }

    private static void LigacaoSecao(Dictionary<string, JsonElement> entrada, JsonObject saida, PeContextoConfig ctx)
    {
        Permitir(entrada, "secao", "multipla");
        var chave = Texto(entrada, "secao")
            ?? throw Erro("Diga a chave da seção que o campo liga (secao).", "Config.secao");
        var alvo = ctx.SecaoPorChave(chave)
            ?? throw Erro($"A seção \"{chave}\" não existe ou foi apagada.", "Config.secao");
        if (alvo.Escopo != ctx.Escopo)
            throw Erro("A ligação só vale entre seções do mesmo escopo (PDTIC com PDTIC, PETIC-DF com PETIC-DF).", "Config.secao");
        if (alvo.Tipo != PeDominios.TipoSecao.Tabela)
            throw Erro($"A seção \"{chave}\" é um formulário: a ligação precisa apontar para uma tabela.", "Config.secao");
        saida["secao"] = alvo.Chave;
        saida["multipla"] = Booleano(entrada, "multipla") ?? false;
    }

    private static void LigacaoCatalogo(Dictionary<string, JsonElement> entrada, JsonObject saida)
    {
        Permitir(entrada, "catalogo", "multipla");
        var catalogo = Texto(entrada, "catalogo")
            ?? throw Erro("Diga o catálogo da ligação: " + string.Join(", ", PeDominios.Catalogo.Todos) + ".", "Config.catalogo");
        if (!PeDominios.Catalogo.Todos.Contains(catalogo))
            throw Erro($"Catálogo inválido: {catalogo}. Use um destes: {string.Join(", ", PeDominios.Catalogo.Todos)}.", "Config.catalogo");
        saida["catalogo"] = catalogo;
        saida["multipla"] = Booleano(entrada, "multipla") ?? false;
    }

    private static void Arquivo(Dictionary<string, JsonElement> entrada, JsonObject saida)
    {
        Permitir(entrada, "tipos", "maxMb");
        var tipos = PeDominios.TipoArquivo.Todos.ToList();
        if (entrada.TryGetValue("tipos", out var t) && t.ValueKind != JsonValueKind.Null)
        {
            if (t.ValueKind != JsonValueKind.Array) throw Erro("Os tipos de arquivo vêm numa lista (tipos).", "Config.tipos");
            tipos = t.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim().ToLowerInvariant() : "")
                .Distinct().ToList();
            if (tipos.Count == 0 || tipos.Any(x => !PeDominios.TipoArquivo.Todos.Contains(x)))
                throw Erro("Os tipos de arquivo aceitos são: " + string.Join(", ", PeDominios.TipoArquivo.Todos) + ".", "Config.tipos");
        }
        saida["tipos"] = new JsonArray(tipos.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
        var maxMb = LerInteiro(entrada, "maxMb", 1, PeDominios.TipoArquivo.MaximoMb,
            $"O tamanho máximo do arquivo vai de 1 a {PeDominios.TipoArquivo.MaximoMb} MB.");
        saida["maxMb"] = maxMb ?? PeDominios.TipoArquivo.MaximoMb;
    }

    private static void Calculado(Dictionary<string, JsonElement> entrada, JsonObject saida, PeContextoConfig ctx)
    {
        Permitir(entrada, "calculo", "campos", "pesos", "matriz");
        var calculo = Texto(entrada, "calculo")
            ?? throw Erro("Diga o cálculo: " + string.Join(", ", PeDominios.Calculo.Todos) + ".", "Config.calculo");
        if (!PeDominios.Calculo.Todos.Contains(calculo))
            throw Erro($"Cálculo inválido: {calculo}. Use um destes: {string.Join(", ", PeDominios.Calculo.Todos)}.", "Config.calculo");

        if (!entrada.TryGetValue("campos", out var c) || c.ValueKind != JsonValueKind.Array)
            throw Erro("Diga os campos que entram no cálculo (campos).", "Config.campos");
        var chaves = c.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()!.Trim() : "").ToList();
        if (chaves.Any(k => k.Length == 0) || chaves.Distinct().Count() != chaves.Count)
            throw Erro("Os campos do cálculo precisam ser chaves diferentes umas das outras.", "Config.campos");

        var campos = new List<PeCampoInfo>();
        foreach (var chave in chaves)
        {
            if (chave == ctx.ChaveDoCampo) throw Erro("O campo calculado não pode entrar no próprio cálculo.", "Config.campos");
            var campo = ctx.CamposDaSecao.FirstOrDefault(x => x.Chave == chave)
                ?? throw Erro($"O campo \"{chave}\" não existe nesta seção (ou foi apagado).", "Config.campos");
            campos.Add(campo);
        }

        switch (calculo)
        {
            case PeDominios.Calculo.Produto when campos.Count < 2:
                throw Erro("O produto precisa de pelo menos dois campos.", "Config.campos");
            case PeDominios.Calculo.Subtracao when campos.Count != 2:
                throw Erro("A subtração usa exatamente dois campos: o primeiro menos o segundo.", "Config.campos");
            case PeDominios.Calculo.SomaPonderada when campos.Count < 1:
                throw Erro("A soma ponderada precisa de pelo menos um campo.", "Config.campos");
            case PeDominios.Calculo.NivelRisco when campos.Count != 2:
                throw Erro("O nível de risco usa exatamente dois campos: probabilidade e impacto.", "Config.campos");
        }

        if (calculo == PeDominios.Calculo.NivelRisco)
        {
            if (campos.Any(x => x.Tipo != PeDominios.TipoCampo.Lista))
                throw Erro("O nível de risco cruza dois campos de lista (probabilidade e impacto).", "Config.campos");
        }
        else
        {
            foreach (var campo in campos)
            {
                if (!TiposNumericos.Contains(campo.Tipo))
                    throw Erro($"O campo \"{campo.Chave}\" não é numérico: entram número, valor em reais, percentual ou lista com valores numéricos.", "Config.campos");
                if (campo.Tipo == PeDominios.TipoCampo.Lista && campo.Opcoes.Any(o => !EhNumero(o)))
                    throw Erro($"As opções do campo \"{campo.Chave}\" precisam ter valores numéricos para entrar no cálculo.", "Config.campos");
            }
        }

        saida["calculo"] = calculo;
        saida["campos"] = new JsonArray(chaves.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray());

        if (calculo == PeDominios.Calculo.SomaPonderada)
            saida["pesos"] = Pesos(entrada, chaves);
        if (calculo == PeDominios.Calculo.NivelRisco)
            saida["matriz"] = Matriz(entrada, campos[0], campos[1], ctx.OpcoesDoCampo);
    }

    private static JsonObject Pesos(Dictionary<string, JsonElement> entrada, List<string> chaves)
    {
        if (!entrada.TryGetValue("pesos", out var p) || p.ValueKind != JsonValueKind.Object)
            throw Erro("A soma ponderada precisa do peso de cada campo (pesos).", "Config.pesos");
        var pesos = p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
        var saida = new JsonObject();
        foreach (var chave in chaves)
        {
            if (!pesos.TryGetValue(chave, out var valor) || valor.ValueKind != JsonValueKind.Number
                || !valor.TryGetDecimal(out var peso) || peso <= 0)
                throw Erro($"Dê um peso maior que zero ao campo \"{chave}\".", "Config.pesos");
            saida[chave] = peso;
        }
        if (pesos.Keys.Any(k => !chaves.Contains(k)))
            throw Erro("Há peso para um campo que não entra no cálculo.", "Config.pesos");
        return saida;
    }

    private static JsonObject Matriz(Dictionary<string, JsonElement> entrada, PeCampoInfo linhas, PeCampoInfo colunas,
        IReadOnlyList<string> resultados)
    {
        if (!entrada.TryGetValue("matriz", out var m) || m.ValueKind != JsonValueKind.Object)
            throw Erro("O nível de risco precisa da matriz (probabilidade nas linhas, impacto nas colunas).", "Config.matriz");

        var saida = new JsonObject();
        var linhasInformadas = m.EnumerateObject().ToList();
        foreach (var linha in linhasInformadas)
        {
            if (!linhas.Opcoes.Contains(linha.Name))
                throw Erro($"A matriz tem a linha \"{linha.Name}\", que não é opção do campo \"{linhas.Chave}\".", "Config.matriz");
            if (linha.Value.ValueKind != JsonValueKind.Object)
                throw Erro($"A linha \"{linha.Name}\" da matriz precisa dizer o nível de cada impacto.", "Config.matriz");

            var celulas = new JsonObject();
            foreach (var celula in linha.Value.EnumerateObject())
            {
                if (!colunas.Opcoes.Contains(celula.Name))
                    throw Erro($"A matriz tem a coluna \"{celula.Name}\", que não é opção do campo \"{colunas.Chave}\".", "Config.matriz");
                var nivel = celula.Value.ValueKind == JsonValueKind.String ? celula.Value.GetString()!.Trim() : "";
                if (nivel.Length == 0 || (resultados.Count > 0 && !resultados.Contains(nivel)))
                    throw Erro($"A célula \"{linha.Name}\" x \"{celula.Name}\" precisa ser uma das opções do nível de risco.", "Config.matriz");
                celulas[celula.Name] = nivel;
            }
            saida[linha.Name] = celulas;
        }

        // Completa: toda opção ativa de um lado cruza com toda opção ativa do outro
        foreach (var linha in linhas.OpcoesAtivas)
        {
            if (saida[linha] is not JsonObject celulas)
                throw Erro($"Falta a linha \"{linha}\" na matriz do nível de risco.", "Config.matriz");
            foreach (var coluna in colunas.OpcoesAtivas)
                if (celulas[coluna] == null)
                    throw Erro($"Falta a célula \"{linha}\" x \"{coluna}\" na matriz do nível de risco.", "Config.matriz");
        }
        return saida;
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static Dictionary<string, JsonElement> LerEntrada(JsonElement? config)
    {
        var entrada = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (config == null || config.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return entrada;
        if (config.Value.ValueKind != JsonValueKind.Object) throw Erro("O config precisa ser um objeto JSON.", "Config");
        foreach (var p in config.Value.EnumerateObject())
        {
            // Chave com nulo não diz nada: conta como ausente (o formulário do front manda
            // todas as chaves do tipo PeConfigCampo, as que não usa com nulo)
            if (p.Value.ValueKind == JsonValueKind.Null) continue;
            if (!entrada.TryAdd(p.Name, p.Value)) throw Erro($"O config repete a chave \"{p.Name}\".", "Config");
        }
        return entrada;
    }

    private static void Permitir(Dictionary<string, JsonElement> entrada, params string[] chaves)
    {
        var sobrando = entrada.Keys.Where(k => !chaves.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        if (sobrando.Count == 0) return;
        throw Erro(chaves.Length == 0
            ? "Este tipo de campo não tem configuração."
            : $"O config não aceita \"{string.Join("\", \"", sobrando)}\" neste tipo de campo. Aceita: {string.Join(", ", chaves)}.", "Config");
    }

    private static string? Texto(Dictionary<string, JsonElement> entrada, string chave)
    {
        if (!entrada.TryGetValue(chave, out var e) || e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind != JsonValueKind.String) throw Erro($"\"{chave}\" precisa ser um texto.", $"Config.{chave}");
        var texto = e.GetString()!.Trim();
        return texto.Length == 0 ? null : texto;
    }

    private static bool? Booleano(Dictionary<string, JsonElement> entrada, string chave)
    {
        if (!entrada.TryGetValue(chave, out var e) || e.ValueKind == JsonValueKind.Null) return null;
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Erro($"\"{chave}\" precisa ser verdadeiro ou falso.", $"Config.{chave}")
        };
    }

    private static decimal? Decimal(Dictionary<string, JsonElement> entrada, string chave)
    {
        if (!entrada.TryGetValue(chave, out var e) || e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetDecimal(out var valor))
            throw Erro($"\"{chave}\" precisa ser um número.", $"Config.{chave}");
        return valor;
    }

    private static int? LerInteiro(Dictionary<string, JsonElement> entrada, string chave, int min, int max, string mensagem)
    {
        if (!entrada.TryGetValue(chave, out var e) || e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetInt32(out var valor) || valor < min || valor > max)
            throw Erro(mensagem, $"Config.{chave}");
        return valor;
    }

    private static void Inteiro(Dictionary<string, JsonElement> entrada, JsonObject saida, string chave, int min, int max,
        string mensagem)
    {
        var valor = LerInteiro(entrada, chave, min, max, mensagem);
        if (valor != null) saida[chave] = valor;
    }

    /// <summary>
    /// O erro do config com o campo da tela a que ele se refere (F1, achado A14): "Config.max",
    /// "Config.secao" e assim por diante ("Config" quando é o config inteiro; "Tipo" no tipo). A
    /// resposta 400 leva Campos com essa chave, e a mensagem continua em Message.
    /// </summary>
    private static ApiException Erro(string mensagem, string campo) =>
        new PeValidacaoException(new Dictionary<string, string> { [campo] = mensagem }, mensagem, ErrorCode.PeConfigInvalida);
}
