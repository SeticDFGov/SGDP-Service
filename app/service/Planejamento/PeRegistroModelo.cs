using System.Text.Json;
using System.Text.Json.Nodes;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Dono de um conjunto de registros: o catálogo do DF (sem id), uma versão do PETIC-DF ou o
/// PDTIC de um órgão (E4). O escopo da seção precisa ser o do dono (df com df, petic com
/// petic, pdtic com pdtic). No PDTIC, a seção e os campos aparecem pela trilha do órgão
/// (nível e ajustes); nos outros, pela situação geral.
/// </summary>
public sealed record PeDono(string Tipo, long? PeticId, long? PdticId = null)
{
    public static readonly PeDono Df = new(PeDominios.DonoRegistro.Df, null);

    public static PeDono DoPetic(long peticId) => new(PeDominios.DonoRegistro.Petic, peticId);

    public static PeDono DoPdtic(long pdticId) => new(PeDominios.DonoRegistro.Pdtic, null, pdticId);

    public bool EhPdtic => Tipo == PeDominios.DonoRegistro.Pdtic;

    /// <summary>Escopo das seções deste dono (pe_secao.escopo).</summary>
    public string Escopo => Tipo switch
    {
        PeDominios.DonoRegistro.Petic => PeDominios.Escopo.Petic,
        PeDominios.DonoRegistro.Pdtic => PeDominios.Escopo.Pdtic,
        _ => PeDominios.Escopo.Df
    };

    /// <summary>Chave da sequência dos códigos (pe_registro_sequencia.dono): "df", "petic:12" ou "pdtic:7".</summary>
    public string Chave => (PeticId ?? PdticId) is long id ? $"{Tipo}:{id}" : Tipo;
}

/// <summary>Um campo que o dono vê, com a obrigatoriedade já resolvida.</summary>
public sealed record PeCampoVisivel(PeCampo Campo, bool Obrigatorio);

/// <summary>
/// Uma seção como o dono a vê: todos os campos (inclusive os apagados e os desligados, que
/// guardam valor), os visíveis na ordem, com a obrigatoriedade, e as opções de cada campo
/// (ativas e inativas, na ordem).
/// </summary>
public sealed class PeSecaoDoDono
{
    public required PeSecao Secao { get; init; }

    public required List<PeCampo> Campos { get; init; }

    public required List<PeCampoVisivel> Visiveis { get; init; }

    public required Dictionary<long, List<PeOpcao>> Opcoes { get; init; }

    // A seção é obrigatória para o dono (situação geral fora do PDTIC; no PDTIC, a da trilha do órgão)
    public bool Obrigatoria { get; init; }

    // Só no PDTIC, desde a E7 (rodada B): os registros são de um ciclo (monitoramento ou avaliacao), ou nulo
    public string? PorCiclo { get; init; }

    public IReadOnlyList<PeOpcao> OpcoesDe(PeCampo campo) =>
        Opcoes.TryGetValue(campo.Id, out var lista) ? lista : Array.Empty<PeOpcao>();

    public bool EhFormulario => Secao.Tipo == PeDominios.TipoSecao.Formulario;

    /// <summary>
    /// O campo que descreve o registro (o Resumo das ligações e o Rótulo dos catálogos): o
    /// principal; sem principal (seção criada pelo administrador), o primeiro texto, senão o
    /// primeiro campo que não é ligação.
    /// </summary>
    public PeCampo? CampoDoResumo()
    {
        var ativos = Campos.Where(c => c.ExcluidoEm == null).ToList();
        return ativos.FirstOrDefault(c => c.Principal)
            ?? ativos.FirstOrDefault(c => c.Tipo is PeDominios.TipoCampo.TextoCurto or PeDominios.TipoCampo.TextoLongo)
            ?? ativos.FirstOrDefault(c => !PeRegistroDados.EhLigacao(c));
    }
}

/// <summary>
/// Erro de validação de um registro: 400 PeRegistroInvalido com { Code, Message, Campos },
/// uma mensagem em linguagem simples por chave de campo. Desde a E7, também a publicação sem
/// a data ou o endereço (PePublicacaoIncompleta) e o registro do PDTIC aprovado fora do
/// sistema (PeRegistroExternoInvalido), com o código próprio.
/// </summary>
public sealed class PeValidacaoException : ApiException
{
    public IReadOnlyDictionary<string, string> Campos { get; }

    public PeValidacaoException(IReadOnlyDictionary<string, string> campos, string? mensagem = null,
        ErrorCode codigo = ErrorCode.PeRegistroInvalido)
        : base(codigo, mensagem ?? MensagemPadrao(campos))
    {
        Campos = campos;
    }

    private static string MensagemPadrao(IReadOnlyDictionary<string, string> campos) =>
        campos.Count == 1 ? "Confira o campo destacado." : "Confira os campos destacados.";
}

/// <summary>
/// Envio ao CGTIC com pendências (E7): 400 PePdticComPendencias com { Code, Message,
/// Pendencias }, a mesma lista da prévia (GET pdtic/{id}/envio). Na rodada B, também o
/// fechamento de um ciclo com pendências (PeCicloComPendencias), com a mesma forma.
/// </summary>
public sealed class PePendenciasException : ApiException
{
    public IReadOnlyList<api.Planejamento.PePendenciaResponse> Pendencias { get; }

    public PePendenciasException(IReadOnlyList<api.Planejamento.PePendenciaResponse> pendencias)
        : base(ErrorCode.PePdticComPendencias, pendencias.Count == 1
            ? "Falta um passo para enviar o PDTIC ao CGTIC. Confira a lista."
            : $"Faltam {pendencias.Count} passos para enviar o PDTIC ao CGTIC. Confira a lista.")
    {
        Pendencias = pendencias;
    }

    public PePendenciasException(ErrorCode codigo, string mensagem, IReadOnlyList<api.Planejamento.PePendenciaResponse> pendencias)
        : base(codigo, mensagem)
    {
        Pendencias = pendencias;
    }
}

/// <summary>
/// Envio do PETIC-DF ao CGTIC com pendências (F1, achado A06): 400 PePeticIncompleto com { Code,
/// Message, Pendencias }, a mesma lista da prévia (GET petic/{id}/envio), cada item com a seção.
/// </summary>
public sealed class PePeticPendenciasException : ApiException
{
    public IReadOnlyList<api.Planejamento.PePeticPendenciaResponse> Pendencias { get; }

    public PePeticPendenciasException(IReadOnlyList<api.Planejamento.PePeticPendenciaResponse> pendencias)
        : base(ErrorCode.PePeticIncompleto, pendencias.Count == 1
            ? "Falta uma coisa para enviar o PETIC-DF ao CGTIC. Confira a lista."
            : $"Faltam {pendencias.Count} coisas para enviar o PETIC-DF ao CGTIC. Confira a lista.")
    {
        Pendencias = pendencias;
    }
}

/// <summary>Leitura dos valores guardados no jsonb dos registros.</summary>
public static class PeRegistroDados
{
    /// <summary>O objeto guardado (vazio quando o texto não é um objeto JSON).</summary>
    public static JsonObject Ler(string? dados)
    {
        if (string.IsNullOrWhiteSpace(dados)) return new JsonObject();
        try
        {
            return JsonNode.Parse(dados) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>O registro guarda algum valor (não nulo) nesta chave.</summary>
    public static bool TemValor(string? dados, string chave) => Ler(dados)[chave] is { } valor && !EhVazio(valor);

    /// <summary>Valor vazio: nulo, texto em branco, lista vazia ou objeto vazio.</summary>
    public static bool EhVazio(JsonNode? valor) => valor switch
    {
        null => true,
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s),
        JsonArray a => a.Count == 0,
        JsonObject o => o.Count == 0,
        _ => false
    };

    /// <summary>Texto guardado (valor de lista, por exemplo) ou nulo.</summary>
    public static string? Texto(JsonNode? valor) =>
        valor is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Os textos de uma lista guardada (lista múltipla).</summary>
    public static List<string> Textos(JsonNode? valor) =>
        valor is JsonArray a ? a.Select(Texto).Where(s => s != null).Select(s => s!).ToList() : new List<string>();

    /// <summary>Número guardado (número, moeda, percentual, calculado) ou nulo.</summary>
    public static decimal? Numero(JsonNode? valor)
    {
        if (valor is not JsonValue v) return null;
        if (v.TryGetValue<decimal>(out var d)) return d;
        if (v.TryGetValue<JsonElement>(out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var de)) return de;
        if (v.TryGetValue<double>(out var dbl)) return (decimal)dbl;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<int>(out var i)) return i;
        return null;
    }

    /// <summary>Id do arquivo guardado num campo de arquivo ({ "ArquivoId", "Nome" }).</summary>
    public static long? ArquivoId(JsonNode? valor)
    {
        if (valor is not JsonObject o) return null;
        foreach (var (chave, v) in o)
        {
            if (!string.Equals(chave, "ArquivoId", StringComparison.OrdinalIgnoreCase) || v is not JsonValue jv) continue;
            if (jv.TryGetValue<long>(out var id)) return id;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<JsonElement>(out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var el)) return el;
        }
        return null;
    }

    public static bool EhLigacao(PeCampo campo) =>
        campo.Tipo is PeDominios.TipoCampo.LigacaoSecao or PeDominios.TipoCampo.LigacaoCatalogo;

    /// <summary>
    /// Ligação com o catálogo dos sistemas de IA do PGIA (E4): os ids ficam no jsonb do
    /// registro (lista de números), porque o destino não é um registro (pe_vinculo liga só
    /// registros). Na resposta e no corpo, continua em Vinculos, como as outras ligações.
    /// </summary>
    public static bool EhLigacaoPgia(PeCampo campo) =>
        campo.Tipo == PeDominios.TipoCampo.LigacaoCatalogo && PeDominios.Catalogo.DoPgia(PeValores.Texto(campo.Config, "catalogo"));

    /// <summary>Ligação gravada em pe_vinculo (com seção ou com catálogo feito de registros).</summary>
    public static bool EhLigacaoPorVinculo(PeCampo campo) => EhLigacao(campo) && !EhLigacaoPgia(campo);

    /// <summary>Os ids guardados numa lista do jsonb (ligação com o PGIA), sem repetir, na ordem.</summary>
    public static List<long> Ids(JsonNode? valor)
    {
        var ids = new List<long>();
        if (valor is not JsonArray lista) return ids;
        foreach (var item in lista)
        {
            if (item is not JsonValue v) continue;
            long? id = v.TryGetValue<long>(out var l) ? l
                : v.TryGetValue<int>(out var i) ? i
                : v.TryGetValue<JsonElement>(out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var el) ? el
                : null;
            if (id != null && !ids.Contains(id.Value)) ids.Add(id.Value);
        }
        return ids;
    }
}
