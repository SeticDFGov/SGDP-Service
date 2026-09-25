using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Config dos blocos do modelo do documento (pe_doc_bloco.config), conferido e normalizado
/// (chaves em PascalCase; chave desconhecida ou do tipo errado = 400 PeDocConfigInvalida):
/// <list type="bullet">
/// <item>texto: { "Texto": JSON do TipTap } (a lista fechada do PeTextoRico; sem letra = sem texto);</item>
/// <item>tabela_secao: { "Secao": chave da seção do PDTIC (ou "pgia_sistemas", o inventário de IA
/// do PGIA), "Colunas": [chaves] ou nulo (os campos visíveis marcados "no documento"),
/// "Filtro": { "Campo", "Valor", "Excluir" } opcional };</item>
/// <item>lista_tema: { "Tema": valor de uma opção do campo acoes.tema };</item>
/// <item>fluxo: { "Fluxo": chave do fluxo } (desenhado a partir da E6);</item>
/// <item>matriz_swot e quebra_pagina: sem configuração;</item>
/// <item>desde a E7 (rodada B), os blocos do acompanhamento, sem configuração: acoes_por_situacao
/// (as ações do ciclo em grupos pela situação), metas_por_resultado (as metas pelo resultado),
/// riscos_ocorridos (os riscos que ocorreram no ciclo ou na vigência) e medicoes (as medições do
/// ciclo com o valor de referência);</item>
/// <item>em todos, menos a quebra de página: "PaginaDeitada": true (página deitada no PDF).</item>
/// </list>
/// O Filtro guarda só as linhas cujo campo tem o Valor (lista múltipla: contém); com
/// "Excluir": true, tira essas linhas e guarda as outras (inclusive as sem valor). É o que
/// separa as necessidades priorizadas (capítulo 9) das não priorizadas (anexo).
/// </summary>
public static partial class PeDocConfig
{
    /// <summary>A seção "virtual" com os sistemas de IA do inventário do PGIA do órgão (só leitura).</summary>
    public const string SecaoPgia = "pgia_sistemas";

    /// <summary>As colunas do inventário do PGIA, na ordem.</summary>
    public static readonly IReadOnlyList<(string Chave, string Rotulo)> ColunasPgia = new[]
    {
        ("nome", "Sistema"),
        ("finalidade", "Finalidade"),
        ("classificacao", "Classificação de risco"),
        ("base", "Base legal da classificação"),
        ("situacao", "Situação")
    };

    public const int MaximoTexto = PeValores.MaximoTextoRico;

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex ChaveDeFluxo();

    /// <summary>O que o config precisa saber do modelo em volta.</summary>
    public sealed class Contexto
    {
        // Campos (chave e tipo) de uma seção do PDTIC que não foi apagada; nulo quando a seção não existe
        public required Func<string, IReadOnlyList<(string Chave, string Tipo)>?> CamposDaSecao { get; init; }

        // Os valores das opções do campo acoes.tema
        public required Func<IReadOnlyList<string>> Temas { get; init; }
    }

    /// <summary>O config normalizado (JSON) e as imagens do texto (quem grava confere os arquivos).</summary>
    public sealed record Resultado(string Json, IReadOnlyList<long> Imagens);

    private static readonly IReadOnlyDictionary<string, string[]> Chaves = new Dictionary<string, string[]>
    {
        [PeDominios.TipoBloco.Texto] = new[] { "Texto", "PaginaDeitada" },
        [PeDominios.TipoBloco.TabelaSecao] = new[] { "Secao", "Colunas", "Filtro", "PaginaDeitada" },
        [PeDominios.TipoBloco.ListaTema] = new[] { "Tema", "PaginaDeitada" },
        [PeDominios.TipoBloco.MatrizSwot] = new[] { "PaginaDeitada" },
        [PeDominios.TipoBloco.Fluxo] = new[] { "Fluxo", "PaginaDeitada" },
        // PaginaDeitada é aceito (o editor manda em todo bloco), mas a quebra de página não guarda
        [PeDominios.TipoBloco.QuebraPagina] = new[] { "PaginaDeitada" },
        // Os blocos do acompanhamento (E7, rodada B): os dados vêm dos ciclos do documento
        [PeDominios.TipoBloco.AcoesPorSituacao] = new[] { "PaginaDeitada" },
        [PeDominios.TipoBloco.MetasPorResultado] = new[] { "PaginaDeitada" },
        [PeDominios.TipoBloco.RiscosOcorridos] = new[] { "PaginaDeitada" },
        [PeDominios.TipoBloco.Medicoes] = new[] { "PaginaDeitada" }
    };

    public static Resultado Normalizar(string tipo, JsonElement? entrada, Contexto ctx)
    {
        if (!Chaves.TryGetValue(tipo, out var aceitas))
            throw Erro($"Tipo de bloco inválido. Use {string.Join(", ", PeDominios.TipoBloco.Todos)}.");

        var valores = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (entrada is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } config)
        {
            if (config.ValueKind != JsonValueKind.Object) throw Erro("O config do bloco precisa ser um objeto JSON.");
            foreach (var p in config.EnumerateObject())
            {
                var nome = aceitas.FirstOrDefault(a => string.Equals(a, p.Name, StringComparison.OrdinalIgnoreCase));
                if (nome == null)
                    throw Erro(tipo == PeDominios.TipoBloco.QuebraPagina
                        ? "A quebra de página não tem configuração."
                        : $"O config do bloco não aceita \"{p.Name}\". Use: {string.Join(", ", aceitas)}.");
                // Nulo conta como ausente
                if (p.Value.ValueKind != JsonValueKind.Null) valores[nome] = p.Value;
            }
        }

        var saida = new JsonObject();
        IReadOnlyList<long> imagens = Array.Empty<long>();
        switch (tipo)
        {
            case PeDominios.TipoBloco.Texto:
                if (valores.TryGetValue("Texto", out var texto))
                {
                    if (texto.ValueKind != JsonValueKind.Object) throw Erro("O texto do bloco precisa ser o JSON do editor.");
                    if (texto.GetRawText().Length > MaximoTexto) throw Erro("O texto do bloco passou do tamanho máximo.");
                    var rico = PeTextoRico.Validar(texto);
                    if (rico.Erro != null) throw new ApiException(ErrorCode.PeDocConfigInvalida, rico.Erro);
                    if (rico.Documento != null)
                    {
                        saida["Texto"] = rico.Documento;
                        imagens = rico.Imagens;
                    }
                }
                break;
            case PeDominios.TipoBloco.TabelaSecao:
                Tabela(valores, saida, ctx);
                break;
            case PeDominios.TipoBloco.ListaTema:
            {
                var tema = Texto(valores, "Tema") ?? throw Erro("Escolha o tema das ações (Tema).");
                if (!ctx.Temas().Contains(tema))
                    throw Erro($"O tema \"{tema}\" não é uma opção do campo Tema das ações.");
                saida["Tema"] = tema;
                break;
            }
            case PeDominios.TipoBloco.Fluxo:
            {
                var fluxo = Texto(valores, "Fluxo") ?? throw Erro("Diga a chave do fluxo (Fluxo).");
                if (fluxo.Length > 60 || !ChaveDeFluxo().IsMatch(fluxo))
                    throw Erro("A chave do fluxo usa só letras minúsculas sem acento, números e sublinhado.");
                saida["Fluxo"] = fluxo;
                break;
            }
        }

        if (valores.TryGetValue("PaginaDeitada", out var deitada))
        {
            if (deitada.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw Erro("PaginaDeitada é verdadeiro ou falso.");
            if (deitada.ValueKind == JsonValueKind.True && tipo != PeDominios.TipoBloco.QuebraPagina) saida["PaginaDeitada"] = true;
        }

        return new Resultado(saida.ToJsonString(PeModeloService.JsonHistorico), imagens);
    }

    private static void Tabela(Dictionary<string, JsonElement> valores, JsonObject saida, Contexto ctx)
    {
        var secao = Texto(valores, "Secao") ?? throw Erro("Escolha a seção da tabela (Secao).");
        IReadOnlyList<(string Chave, string Tipo)> campos;
        if (secao == SecaoPgia)
        {
            campos = ColunasPgia.Select(c => (c.Chave, PeDominios.TipoCampo.TextoCurto)).ToList();
        }
        else
        {
            campos = ctx.CamposDaSecao(secao)
                     ?? throw Erro($"A seção \"{secao}\" não existe no PDTIC (ou foi apagada).");
        }
        saida["Secao"] = secao;

        if (valores.TryGetValue("Colunas", out var colunas))
        {
            if (colunas.ValueKind != JsonValueKind.Array) throw Erro("As colunas vêm numa lista com as chaves dos campos (Colunas).");
            var lista = new List<string>();
            foreach (var item in colunas.EnumerateArray())
            {
                var chave = item.ValueKind == JsonValueKind.String ? item.GetString()!.Trim() : null;
                if (string.IsNullOrEmpty(chave) || campos.All(c => c.Chave != chave))
                    throw Erro($"A coluna \"{(item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())}\" não é um campo da seção.");
                if (lista.Contains(chave)) throw Erro($"A coluna \"{chave}\" está repetida.");
                lista.Add(chave);
            }
            if (lista.Count == 0) throw Erro("Escolha pelo menos uma coluna, ou deixe Colunas nulo para usar todas.");
            saida["Colunas"] = new JsonArray(lista.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray());
        }

        if (valores.TryGetValue("Filtro", out var filtro))
        {
            if (secao == SecaoPgia) throw Erro("O inventário do PGIA não tem filtro.");
            if (filtro.ValueKind != JsonValueKind.Object) throw Erro("O filtro é um objeto { Campo, Valor, Excluir }.");
            string? campo = null;
            JsonElement? valor = null;
            var excluir = false;
            foreach (var p in filtro.EnumerateObject())
            {
                if (string.Equals(p.Name, "Campo", StringComparison.OrdinalIgnoreCase))
                    campo = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()!.Trim() : null;
                else if (string.Equals(p.Name, "Valor", StringComparison.OrdinalIgnoreCase))
                    valor = p.Value;
                else if (string.Equals(p.Name, "Excluir", StringComparison.OrdinalIgnoreCase))
                {
                    if (p.Value.ValueKind == JsonValueKind.True) excluir = true;
                    else if (p.Value.ValueKind is not (JsonValueKind.False or JsonValueKind.Null)) throw Erro("Excluir é verdadeiro ou falso.");
                }
                else
                    throw Erro($"O filtro não aceita \"{p.Name}\". Use Campo, Valor e Excluir.");
            }
            var doCampo = campos.FirstOrDefault(c => c.Chave == campo);
            if (campo == null || doCampo.Chave == null) throw Erro("O campo do filtro precisa ser um campo da seção.");
            if (doCampo.Tipo is PeDominios.TipoCampo.LigacaoSecao or PeDominios.TipoCampo.LigacaoCatalogo
                or PeDominios.TipoCampo.Arquivo or PeDominios.TipoCampo.TextoRico)
                throw Erro("O filtro não serve para campo de ligação, de arquivo ou de texto formatado.");
            if (valor is not { ValueKind: JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False })
                throw Erro("O valor do filtro é um texto, um número ou verdadeiro ou falso.");
            saida["Filtro"] = new JsonObject
            {
                ["Campo"] = campo,
                ["Valor"] = JsonNode.Parse(valor.Value.GetRawText()),
                ["Excluir"] = excluir
            };
        }
    }

    // ── Leitura do config guardado ──────────────────────────────────────────

    /// <summary>O config guardado como objeto (vazio quando não é um objeto JSON).</summary>
    public static JsonObject Ler(string? config) => PeRegistroDados.Ler(config);

    public static bool PaginaDeitada(JsonObject config) => config["PaginaDeitada"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static JsonNode? TextoDoBloco(JsonObject config) => config["Texto"] is JsonObject texto ? texto : null;

    public static string? Secao(JsonObject config) => PeRegistroDados.Texto(config["Secao"]);

    /// <summary>As colunas escolhidas, ou nulo (todas as visíveis marcadas "no documento").</summary>
    public static List<string>? Colunas(JsonObject config) =>
        config["Colunas"] is JsonArray lista ? PeRegistroDados.Textos(lista) : null;

    public static string? Tema(JsonObject config) => PeRegistroDados.Texto(config["Tema"]);

    public static string? Fluxo(JsonObject config) => PeRegistroDados.Texto(config["Fluxo"]);

    public sealed record FiltroDaTabela(string Campo, JsonNode? Valor, bool Excluir);

    public static FiltroDaTabela? Filtro(JsonObject config) =>
        config["Filtro"] is JsonObject f && PeRegistroDados.Texto(f["Campo"]) is string campo
            ? new FiltroDaTabela(campo, f["Valor"], f["Excluir"] is JsonValue e && e.TryGetValue<bool>(out var x) && x)
            : null;

    /// <summary>
    /// O valor guardado do registro bate com o valor do filtro: texto e lista pelo texto, lista
    /// múltipla quando contém, número pelo número, sim ou não pelo booleano.
    /// </summary>
    public static bool Bate(JsonNode? guardado, JsonNode? filtro)
    {
        if (guardado == null || filtro == null) return false;
        if (guardado is JsonArray lista) return lista.Any(item => Bate(item, filtro));
        if (guardado is not JsonValue g || filtro is not JsonValue f) return false;
        if (g.TryGetValue<bool>(out var gb)) return f.TryGetValue<bool>(out var fb) && gb == fb;
        if (PeRegistroDados.Numero(g) is decimal gn && PeRegistroDados.Numero(f) is decimal fn) return gn == fn;
        var gt = PeRegistroDados.Texto(g);
        var ft = PeRegistroDados.Texto(f) ?? (PeRegistroDados.Numero(f) is decimal n ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
        return gt != null && ft != null && gt == ft;
    }

    private static string? Texto(Dictionary<string, JsonElement> valores, string chave)
    {
        if (!valores.TryGetValue(chave, out var valor)) return null;
        if (valor.ValueKind != JsonValueKind.String) throw Erro($"{chave} é um texto.");
        var texto = valor.GetString()!.Trim();
        return texto.Length == 0 ? null : texto;
    }

    private static ApiException Erro(string mensagem) => new(ErrorCode.PeDocConfigInvalida, mensagem);
}
