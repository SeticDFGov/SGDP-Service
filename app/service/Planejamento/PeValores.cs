using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Números e datas em português do Brasil ("1.234,56", "24/09/2026"), sem depender dos
/// dados de cultura instalados no servidor (a imagem pode rodar sem ICU).
/// </summary>
public static class PeFormato
{
    public static readonly NumberFormatInfo Br = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = new[] { 3 }
    };

    // Vírgula decimal e nenhum separador de milhar ("1234567,89"): o que a planilha CSV usa
    public static readonly NumberFormatInfo Csv = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = string.Empty
    };

    public static string Numero(decimal valor, int? casas) =>
        casas is int c ? valor.ToString("N" + c, Br) : valor.ToString("#,##0.######", Br);

    public static string Moeda(decimal valor) => "R$ " + valor.ToString("N2", Br);

    public static string Percentual(decimal valor) => valor.ToString("#,##0.##", Br) + "%";

    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Valores dos registros: a validação de cada tipo de campo (com mensagens em linguagem
/// simples), o texto pronto para exibir e os cálculos. Formato guardado no jsonb:
/// <list type="bullet">
/// <item>texto curto e longo: texto (sem espaço nas pontas); texto rico: o JSON do TipTap,
/// conferido e limpo por <see cref="PeTextoRico"/> (E4);</item>
/// <item>número, moeda (duas casas, não negativa) e percentual (0 a 100): número;</item>
/// <item>data: "aaaa-mm-dd"; sim ou não: verdadeiro ou falso;</item>
/// <item>lista: o valor da opção; lista múltipla: lista de valores, na ordem das opções;</item>
/// <item>arquivo: { "ArquivoId", "Nome" }; calculado: número (nível de risco: o valor da opção).</item>
/// </list>
/// Vazio (nulo, texto em branco, lista vazia) não é guardado. Opção desativada só vale se já
/// era o valor guardado (o registro antigo continua gravando).
/// </summary>
public static class PeValores
{
    public const int MaximoTextoCurto = 1000;
    public const int MaximoTextoLongo = 50000;
    public const int MaximoTextoRico = 200_000;
    public const int MaximoResumo = 300;

    private const decimal MaximoNumero = 1_000_000_000_000_000m;
    private const decimal MaximoMoeda = 10_000_000_000_000m;

    /// <summary>
    /// Resultado da validação de um valor: o valor a guardar (nulo = vazio) ou o erro. No
    /// campo de arquivo, o id do arquivo; no texto rico, os ids das imagens (quem chama
    /// confere os arquivos no banco).
    /// </summary>
    public readonly record struct Resultado(JsonNode? Valor, string? Erro, long? ArquivoId = null, IReadOnlyList<long>? Imagens = null)
    {
        public static Resultado Vazio => new(null, null);

        public static Resultado Com(JsonNode valor) => new(valor, null);

        public static Resultado Falha(string mensagem) => new(null, mensagem);
    }

    // ── Validação ───────────────────────────────────────────────────────────

    /// <summary>
    /// Valida e normaliza o valor enviado para um campo (que não é ligação nem calculado).
    /// No campo de arquivo, devolve o id em ArquivoId: quem chama confere o arquivo no banco.
    /// </summary>
    public static Resultado Normalizar(PeCampo campo, IReadOnlyList<PeOpcao> opcoes, JsonElement entrada, JsonNode? atual)
    {
        if (entrada.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return Resultado.Vazio;

        switch (campo.Tipo)
        {
            case PeDominios.TipoCampo.TextoCurto:
            case PeDominios.TipoCampo.TextoLongo:
            {
                if (entrada.ValueKind != JsonValueKind.String) return Resultado.Falha("Escreva um texto.");
                var texto = entrada.GetString()!.Trim();
                if (texto.Length == 0) return Resultado.Vazio;
                var maximo = Inteiro(campo.Config, "max")
                             ?? (campo.Tipo == PeDominios.TipoCampo.TextoCurto ? MaximoTextoCurto : MaximoTextoLongo);
                if (texto.Length > maximo)
                    return Resultado.Falha($"Use no máximo {maximo} caracteres (o texto tem {texto.Length}).");
                return Resultado.Com(JsonValue.Create(texto)!);
            }
            case PeDominios.TipoCampo.TextoRico:
            {
                // JSON do TipTap conferido contra a lista fechada de nós e marcas (E4)
                if (entrada.ValueKind != JsonValueKind.Object)
                    return Resultado.Falha("O texto formatado veio num formato que não serve. Atualize a tela e tente de novo.");
                if (entrada.GetRawText().Length > MaximoTextoRico) return Resultado.Falha("O texto formatado passou do tamanho máximo.");
                var rico = PeTextoRico.Validar(entrada);
                if (rico.Erro != null) return Resultado.Falha(rico.Erro);
                return rico.Documento == null ? Resultado.Vazio : new Resultado(rico.Documento, null, null, rico.Imagens);
            }
            case PeDominios.TipoCampo.Numero:
            {
                if (!LerNumero(entrada, out var valor)) return Resultado.Falha("Informe um número.");
                if (Math.Abs(valor) > MaximoNumero) return Resultado.Falha("O número é grande demais.");
                var casas = Inteiro(campo.Config, "casas");
                if (casas != null && Casas(valor) > casas)
                    return Resultado.Falha(casas == 0 ? "Informe um número inteiro, sem casas decimais." : $"Use no máximo {casas} casas decimais.");
                var minimo = Decimal(campo.Config, "min");
                var maximo = Decimal(campo.Config, "max");
                if (minimo != null && valor < minimo) return Resultado.Falha($"O menor valor aceito é {PeFormato.Numero(minimo.Value, null)}.");
                if (maximo != null && valor > maximo) return Resultado.Falha($"O maior valor aceito é {PeFormato.Numero(maximo.Value, null)}.");
                return Resultado.Com(JsonValue.Create(Normalizado(valor))!);
            }
            case PeDominios.TipoCampo.Moeda:
            {
                if (!LerNumero(entrada, out var valor)) return Resultado.Falha("Informe um valor em reais.");
                if (valor < 0) return Resultado.Falha("Informe um valor igual ou maior que zero.");
                if (valor > MaximoMoeda) return Resultado.Falha("O valor é alto demais.");
                if (Casas(valor) > 2) return Resultado.Falha("Use no máximo duas casas decimais (os centavos).");
                return Resultado.Com(JsonValue.Create(Normalizado(valor))!);
            }
            case PeDominios.TipoCampo.Percentual:
            {
                if (!LerNumero(entrada, out var valor)) return Resultado.Falha("Informe o percentual, de 0 a 100.");
                if (valor < 0 || valor > 100) return Resultado.Falha("O percentual vai de 0 a 100.");
                if (Casas(valor) > 2) return Resultado.Falha("Use no máximo duas casas decimais.");
                return Resultado.Com(JsonValue.Create(Normalizado(valor))!);
            }
            case PeDominios.TipoCampo.Data:
            {
                if (entrada.ValueKind != JsonValueKind.String) return Resultado.Falha("Informe a data no formato aaaa-mm-dd.");
                var texto = entrada.GetString()!.Trim();
                if (texto.Length == 0) return Resultado.Vazio;
                if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    return Resultado.Falha("Informe uma data válida, no formato aaaa-mm-dd.");
                if (data.Year < 1900 || data.Year > 2199) return Resultado.Falha("A data está fora do intervalo aceito.");
                return Resultado.Com(JsonValue.Create(data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))!);
            }
            case PeDominios.TipoCampo.SimNao:
                return entrada.ValueKind switch
                {
                    JsonValueKind.True => Resultado.Com(JsonValue.Create(true)!),
                    JsonValueKind.False => Resultado.Com(JsonValue.Create(false)!),
                    _ => Resultado.Falha("Responda sim ou não.")
                };
            case PeDominios.TipoCampo.Lista:
            {
                var valor = ValorDeOpcao(entrada);
                if (valor == null) return Resultado.Falha("Escolha uma das opções da lista.");
                if (valor.Length == 0) return Resultado.Vazio;
                var opcao = opcoes.FirstOrDefault(o => o.Valor == valor);
                if (opcao == null || (!opcao.Ativa && PeRegistroDados.Texto(atual) != valor))
                    return Resultado.Falha("Escolha uma das opções da lista.");
                return Resultado.Com(JsonValue.Create(valor)!);
            }
            case PeDominios.TipoCampo.ListaMultipla:
            {
                if (entrada.ValueKind != JsonValueKind.Array) return Resultado.Falha("Escolha as opções numa lista.");
                var guardados = PeRegistroDados.Textos(atual);
                var escolhidos = new HashSet<string>();
                foreach (var item in entrada.EnumerateArray())
                {
                    var valor = ValorDeOpcao(item);
                    if (string.IsNullOrEmpty(valor)) return Resultado.Falha("Escolha só opções da lista.");
                    var opcao = opcoes.FirstOrDefault(o => o.Valor == valor);
                    if (opcao == null || (!opcao.Ativa && !guardados.Contains(valor)))
                        return Resultado.Falha("Escolha só opções da lista.");
                    escolhidos.Add(valor);
                }
                if (escolhidos.Count == 0) return Resultado.Vazio;
                // Na ordem das opções, para a exibição e a planilha saírem sempre iguais
                var ordenados = opcoes.Where(o => escolhidos.Contains(o.Valor)).Select(o => (JsonNode)JsonValue.Create(o.Valor)!);
                return Resultado.Com(new JsonArray(ordenados.ToArray()));
            }
            case PeDominios.TipoCampo.Arquivo:
            {
                long? id = entrada.ValueKind switch
                {
                    JsonValueKind.Number when entrada.TryGetInt64(out var n) => n,
                    JsonValueKind.Object => ArquivoIdDaEntrada(entrada),
                    _ => null
                };
                if (id == null) return Resultado.Falha("Envie o arquivo de novo.");
                return new Resultado(null, null, id);
            }
            default:
                // Ligações e calculados não passam por aqui
                return Resultado.Vazio;
        }
    }

    /// <summary>Mensagem do campo obrigatório vazio, conforme o tipo.</summary>
    public static string MensagemObrigatorio(PeCampo campo) => campo.Tipo switch
    {
        PeDominios.TipoCampo.Lista => "Escolha uma opção.",
        PeDominios.TipoCampo.ListaMultipla => "Escolha pelo menos uma opção.",
        PeDominios.TipoCampo.SimNao => "Responda sim ou não.",
        PeDominios.TipoCampo.Data => "Informe a data.",
        PeDominios.TipoCampo.Arquivo => "Envie o arquivo.",
        PeDominios.TipoCampo.LigacaoSecao or PeDominios.TipoCampo.LigacaoCatalogo =>
            Booleano(campo.Config, "multipla") == true ? "Escolha pelo menos um item." : "Escolha um item.",
        _ => "Preencha este campo."
    };

    // ── Texto pronto para exibir ─────────────────────────────────────────────

    /// <summary>
    /// O valor guardado como texto (a lista pelo rótulo da opção, a data em dd/mm/aaaa, o
    /// dinheiro com R$, o sim ou não por extenso). Nulo quando não há valor.
    /// </summary>
    public static string? Rotulo(PeCampo campo, IReadOnlyList<PeOpcao> opcoes, JsonNode? valor)
    {
        if (PeRegistroDados.EhVazio(valor)) return null;

        switch (campo.Tipo)
        {
            case PeDominios.TipoCampo.TextoCurto:
            case PeDominios.TipoCampo.TextoLongo:
                return PeRegistroDados.Texto(valor);
            case PeDominios.TipoCampo.TextoRico:
                var texto = TextoDoRico(valor);
                return texto.Length == 0 ? null : texto;
            case PeDominios.TipoCampo.Numero:
            {
                var numero = PeRegistroDados.Numero(valor);
                if (numero == null) return null;
                var unidade = Texto(campo.Config, "unidade");
                var casas = Inteiro(campo.Config, "casas");
                // Inteiro com máximo de até 9999 (o ano de uma ação ou de uma contratação): sem o
                // ponto de milhar ("2026", não "2.026")
                var formatado = casas == 0 && Decimal(campo.Config, "max") is decimal maximo && maximo <= 9999
                    ? numero.Value.ToString("0", CultureInfo.InvariantCulture)
                    : PeFormato.Numero(numero.Value, casas);
                return unidade == null ? formatado : $"{formatado} {unidade}";
            }
            case PeDominios.TipoCampo.Moeda:
                return PeRegistroDados.Numero(valor) is decimal moeda ? PeFormato.Moeda(moeda) : null;
            case PeDominios.TipoCampo.Percentual:
                return PeRegistroDados.Numero(valor) is decimal percentual ? PeFormato.Percentual(percentual) : null;
            case PeDominios.TipoCampo.Data:
                return DataGuardada(valor) is DateOnly data ? PeFormato.Data(data) : null;
            case PeDominios.TipoCampo.SimNao:
                return valor is JsonValue v && v.TryGetValue<bool>(out var b) ? (b ? "Sim" : "Não") : null;
            case PeDominios.TipoCampo.Lista:
                return RotuloDaOpcao(opcoes, PeRegistroDados.Texto(valor));
            case PeDominios.TipoCampo.ListaMultipla:
                var rotulos = PeRegistroDados.Textos(valor).Select(v2 => RotuloDaOpcao(opcoes, v2)).Where(r => r != null).ToList();
                return rotulos.Count == 0 ? null : string.Join(", ", rotulos);
            case PeDominios.TipoCampo.Arquivo:
                return valor is JsonObject o
                    ? PeRegistroDados.Texto(o.FirstOrDefault(p => string.Equals(p.Key, "Nome", StringComparison.OrdinalIgnoreCase)).Value)
                    : null;
            case PeDominios.TipoCampo.Calculado:
                if (PeConfigCampo.TipoDoCalculo(campo.Config) == PeDominios.Calculo.NivelRisco)
                    return RotuloDaOpcao(opcoes, PeRegistroDados.Texto(valor));
                return PeRegistroDados.Numero(valor) is decimal calculado ? calculado.ToString("#,##0.##", PeFormato.Br) : null;
            default:
                return null;
        }
    }

    /// <summary>Resumo de um registro para as ligações e os catálogos (até 300 caracteres).</summary>
    public static string Resumo(string? texto)
    {
        var limpo = (texto ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return limpo.Length <= MaximoResumo ? limpo : limpo[..(MaximoResumo - 3)].TrimEnd() + "...";
    }

    public static DateOnly? DataGuardada(JsonNode? valor) =>
        PeRegistroDados.Texto(valor) is string texto
        && DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    public static string? RotuloDaOpcao(IReadOnlyList<PeOpcao> opcoes, string? valor) =>
        valor == null ? null : opcoes.FirstOrDefault(o => o.Valor == valor)?.Rotulo ?? valor;

    /// <summary>O texto corrido de um texto rico do TipTap (os nós "text", com espaço entre os blocos).</summary>
    public static string TextoDoRico(JsonNode? valor)
    {
        var sb = new StringBuilder();
        void Percorrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject o:
                    if (PeRegistroDados.Texto(o["text"]) is string t) sb.Append(t);
                    if (o["content"] is JsonArray filhos)
                    {
                        foreach (var filho in filhos) Percorrer(filho);
                        sb.Append(' ');
                    }
                    break;
                case JsonArray a:
                    foreach (var item in a) Percorrer(item);
                    break;
            }
        }
        Percorrer(valor);
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ── Cálculos ────────────────────────────────────────────────────────────

    /// <summary>
    /// O valor de um campo calculado com os valores do registro (nulo quando falta algum). Só
    /// entram os campos que o dono vê: no produto e na soma ponderada, o campo escondido fica
    /// de fora do cálculo; na subtração e no nível de risco, que precisam dos dois, o
    /// resultado fica vazio. Lista entra pelo valor numérico da opção.
    /// </summary>
    public static JsonNode? Calcular(PeCampo calculado, JsonObject dados, IReadOnlyDictionary<string, PeCampo> visiveis)
    {
        var calculo = PeConfigCampo.TipoDoCalculo(calculado.Config);
        var chaves = PeConfigCampo.CamposDoCalculo(calculado.Config);
        if (calculo == null || chaves.Count == 0) return null;

        try
        {
            switch (calculo)
            {
                case PeDominios.Calculo.Produto:
                case PeDominios.Calculo.SomaPonderada:
                {
                    var usados = chaves.Where(visiveis.ContainsKey).ToList();
                    if (usados.Count == 0) return null;
                    var pesos = Pesos(calculado.Config);
                    decimal resultado = calculo == PeDominios.Calculo.Produto ? 1 : 0;
                    foreach (var chave in usados)
                    {
                        if (ValorNumerico(dados[chave]) is not decimal valor) return null;
                        resultado = calculo == PeDominios.Calculo.Produto
                            ? resultado * valor
                            : resultado + valor * pesos.GetValueOrDefault(chave, 1m);
                    }
                    return JsonValue.Create(Normalizado(Math.Round(resultado, 6)));
                }
                case PeDominios.Calculo.Subtracao:
                {
                    if (chaves.Count != 2 || !chaves.All(visiveis.ContainsKey)) return null;
                    if (ValorNumerico(dados[chaves[0]]) is not decimal a || ValorNumerico(dados[chaves[1]]) is not decimal b) return null;
                    return JsonValue.Create(Normalizado(a - b));
                }
                case PeDominios.Calculo.NivelRisco:
                {
                    if (chaves.Count != 2 || !chaves.All(visiveis.ContainsKey)) return null;
                    var linha = PeRegistroDados.Texto(dados[chaves[0]]);
                    var coluna = PeRegistroDados.Texto(dados[chaves[1]]);
                    if (linha == null || coluna == null) return null;
                    if (Parse(calculado.Config)?["matriz"] is not JsonObject matriz || matriz[linha] is not JsonObject celulas)
                        return null;
                    var nivel = PeRegistroDados.Texto(celulas[coluna]);
                    return nivel == null ? null : JsonValue.Create(nivel);
                }
                default:
                    return null;
            }
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static decimal? ValorNumerico(JsonNode? valor)
    {
        if (PeRegistroDados.Numero(valor) is decimal numero) return numero;
        return PeRegistroDados.Texto(valor) is string texto
               && decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var daOpcao)
            ? daOpcao
            : null;
    }

    private static Dictionary<string, decimal> Pesos(string config)
    {
        var saida = new Dictionary<string, decimal>();
        if (Parse(config)?["pesos"] is not JsonObject pesos) return saida;
        foreach (var (chave, valor) in pesos)
            if (PeRegistroDados.Numero(valor) is decimal peso) saida[chave] = peso;
        return saida;
    }

    private static bool LerNumero(JsonElement entrada, out decimal valor)
    {
        valor = 0;
        return entrada.ValueKind == JsonValueKind.Number && entrada.TryGetDecimal(out valor);
    }

    private static string? ValorDeOpcao(JsonElement entrada) => entrada.ValueKind switch
    {
        JsonValueKind.String => entrada.GetString()!.Trim(),
        JsonValueKind.Number => entrada.GetRawText(),
        _ => null
    };

    private static long? ArquivoIdDaEntrada(JsonElement entrada)
    {
        foreach (var propriedade in entrada.EnumerateObject())
        {
            if (!string.Equals(propriedade.Name, "ArquivoId", StringComparison.OrdinalIgnoreCase)) continue;
            return propriedade.Value.ValueKind == JsonValueKind.Number && propriedade.Value.TryGetInt64(out var id) ? id : null;
        }
        return null;
    }

    /// <summary>Quantas casas decimais o número tem, sem contar zeros à direita.</summary>
    public static int Casas(decimal valor) => (decimal.GetBits(Normalizado(valor))[3] >> 16) & 0xFF;

    /// <summary>O número sem zeros à direita (12,50 vira 12,5).</summary>
    public static decimal Normalizado(decimal valor) => valor / 1.000000000000000000000000000000000m;

    private static JsonObject? Parse(string? config)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(config) ? "{}" : config) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static int? Inteiro(string? config, string chave) =>
        PeRegistroDados.Numero(Parse(config)?[chave]) is decimal d ? (int)d : null;

    public static decimal? Decimal(string? config, string chave) => PeRegistroDados.Numero(Parse(config)?[chave]);

    public static string? Texto(string? config, string chave) => PeRegistroDados.Texto(Parse(config)?[chave]);

    public static bool? Booleano(string? config, string chave) =>
        Parse(config)?[chave] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>Tipos aceitos num campo de arquivo ("pdf", "png", "jpg").</summary>
    public static IReadOnlyList<string> TiposDeArquivo(string? config) =>
        Parse(config)?["tipos"] is JsonArray tipos
            ? tipos.Select(PeRegistroDados.Texto).Where(t => t != null).Select(t => t!).ToList()
            : PeDominios.TipoArquivo.Todos;
}
