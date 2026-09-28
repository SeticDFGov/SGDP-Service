using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace service.Planejamento;

/// <summary>
/// Chaves estáveis do modelo, geradas a partir do título quando o administrador não
/// informa uma: sem acento, minúsculas, números e separador (hífen na etapa e no passo,
/// sublinhado no resto). As mesmas regras estão nos CHECKs das tabelas pe_.
/// </summary>
public static partial class PeChaves
{
    public const int MaximoPasso = 100;
    public const int MaximoSecao = 60;
    public const int MaximoCampo = 60;
    public const int MaximoOpcao = 60;
    public const int MaximoNivel = 40;

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex ComSublinhado();

    [GeneratedRegex("^[a-z0-9][a-z0-9_]*$")]
    private static partial Regex DeOpcao();

    [GeneratedRegex("^[a-z][a-z0-9-]*\\.[a-z0-9][a-z0-9-]*$")]
    private static partial Regex DePasso();

    /// <summary>Chave de seção, de campo ou código de nível ("tipo_abrangencia").</summary>
    public static bool ChaveValida(string chave, int maximo) => chave.Length <= maximo && ComSublinhado().IsMatch(chave);

    /// <summary>Valor de opção: pode começar por número ("3").</summary>
    public static bool ValorValido(string valor) => valor.Length <= MaximoOpcao && DeOpcao().IsMatch(valor);

    /// <summary>Chave de passo: "etapa.passo", com hífen.</summary>
    public static bool ChavePassoValida(string chave) => chave.Length <= MaximoPasso && DePasso().IsMatch(chave);

    /// <summary>
    /// Texto sem acento, em minúsculas, com o separador no lugar de tudo que não é letra
    /// ou número, sem separador repetido nem nas pontas, cortado no máximo.
    /// </summary>
    public static string Slug(string texto, char separador, int maximo)
    {
        var semAcento = new StringBuilder();
        foreach (var c in (texto ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) semAcento.Append(c);
        }

        var saida = new StringBuilder();
        foreach (var c in semAcento.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') saida.Append(c);
            else if (saida.Length > 0 && saida[^1] != separador) saida.Append(separador);
        }

        var slug = saida.ToString().Trim(separador);
        if (slug.Length > maximo) slug = slug[..maximo].TrimEnd(separador);
        return slug;
    }

    /// <summary>
    /// Primeira chave livre: a base, depois base_2, base_3... (ou base-2 no passo), sem
    /// passar do máximo.
    /// </summary>
    public static string Livre(string baseChave, char separador, int maximo, Func<string, bool> emUso)
    {
        if (!emUso(baseChave)) return baseChave;
        for (var n = 2; ; n++)
        {
            var sufixo = $"{separador}{n}";
            var raiz = baseChave.Length + sufixo.Length > maximo
                ? baseChave[..(maximo - sufixo.Length)].TrimEnd(separador)
                : baseChave;
            var candidata = raiz + sufixo;
            if (!emUso(candidata)) return candidata;
        }
    }
}
