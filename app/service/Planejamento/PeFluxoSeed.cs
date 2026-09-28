using System.Text.Json;
using api.Planejamento;

namespace service.Planejamento;

// ── Formato dos fluxos do guia (Models/Planejamento/Seed/fluxos-inicial.json) ──

/// <summary>
/// Os fluxos do guia em JSON (E6), na ordem em que aparecem: chave, nome, a figura do guia e a
/// definição no mesmo contrato da API (PascalCase), conferida pela mesma validação
/// (<see cref="PeFluxoDefinicaoLeitor"/>) e numerada pelo servidor.
/// </summary>
public sealed class PeSeedFluxos
{
    public List<PeSeedFluxo> Fluxos { get; set; } = new();
}

public sealed class PeSeedFluxo
{
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? FiguraGuia { get; set; }

    public JsonElement Definicao { get; set; }
}

/// <summary>Leitura e conferência dos fluxos do guia (antes de gravar qualquer coisa).</summary>
public static class PeFluxoSeed
{
    public const string Recurso = "Planejamento.fluxos-inicial.json";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Os fluxos do guia embutidos na aplicação.</summary>
    public static PeSeedFluxos Ler()
    {
        using var stream = typeof(PeFluxoSeed).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} não encontrado no assembly.");
        using var leitor = new StreamReader(stream);
        return Ler(leitor.ReadToEnd());
    }

    public static PeSeedFluxos Ler(string json) =>
        JsonSerializer.Deserialize<PeSeedFluxos>(json, Opcoes) ?? throw new InvalidOperationException("Fluxos do guia vazios.");

    /// <summary>
    /// Confere chaves (únicas e no formato), nomes e figuras, e cada definição pela validação da
    /// API; devolve as definições normalizadas e numeradas, na ordem do JSON.
    /// </summary>
    public static List<(PeSeedFluxo Seed, PeFluxoDefinicao Definicao)> Validar(PeSeedFluxos seed)
    {
        void Falha(string mensagem) => throw new InvalidOperationException("Fluxos do guia inválidos: " + mensagem);

        var chaves = new HashSet<string>();
        var saida = new List<(PeSeedFluxo, PeFluxoDefinicao)>();
        foreach (var fluxo in seed.Fluxos)
        {
            if (!chaves.Add(fluxo.Chave) || !PeChaves.ChaveValida(fluxo.Chave, 60))
                Falha($"chave \"{fluxo.Chave}\" repetida ou fora do formato.");
            if (string.IsNullOrWhiteSpace(fluxo.Nome) || fluxo.Nome.Length > 200) Falha($"nome do fluxo \"{fluxo.Chave}\".");
            if (fluxo.FiguraGuia is { Length: > 40 }) Falha($"figura do fluxo \"{fluxo.Chave}\".");
            var resultado = PeFluxoDefinicaoLeitor.Ler(fluxo.Definicao);
            if (!resultado.Valida)
                Falha($"o fluxo \"{fluxo.Chave}\": {string.Join(" ", resultado.Erros)}");
            saida.Add((fluxo, resultado.Definicao!));
        }
        return saida;
    }
}
