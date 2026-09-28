using System.Text;

namespace demanda_service.Helpers;

/// <summary>
/// Escritor de CSV comum aos módulos (saiu do CtrCsv da Supervisão Contínua, que continua
/// escrevendo exatamente os mesmos bytes): UTF-8 COM BOM (o Excel abre sem mojibake),
/// separador ";", CRLF, aspas quando a célula tem ";", aspas ou quebra de linha, e proteção
/// contra fórmula nas células de texto livre (um apóstrofo à frente de =, +, -, @, TAB ou CR,
/// que o leitor da Supervisão Contínua tira de volta na importação).
/// </summary>
public sealed class CsvEscritor
{
    public const string Separador = ";";
    public const string FimDeLinha = "\r\n";

    /// <summary>Caracteres que fazem uma célula virar fórmula no Excel e no Sheets (CSV injection).</summary>
    public static readonly char[] IniciamFormula = { '=', '+', '-', '@', '\t', '\r' };

    private readonly StringBuilder _texto = new();

    /// <summary>Uma linha; TextoLivre = célula digitada por alguém, que recebe a proteção contra fórmula.</summary>
    public CsvEscritor Linha(IEnumerable<(string Valor, bool TextoLivre)> celulas)
    {
        _texto.Append(string.Join(Separador, celulas.Select(c => Escapar(c.Valor, c.TextoLivre)))).Append(FimDeLinha);
        return this;
    }

    /// <summary>Uma linha em que todas as células têm o mesmo tratamento.</summary>
    public CsvEscritor Linha(IEnumerable<string> celulas, bool textoLivre = false) =>
        Linha(celulas.Select(c => (c, textoLivre)));

    /// <summary>O arquivo: BOM explícito (é ele que faz o Excel abrir em UTF-8) e o texto.</summary>
    public byte[] ParaBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_texto.ToString())).ToArray();

    public static string Escapar(string valor, bool textoLivre = false)
    {
        // CSV injection: célula de texto livre começando com =, +, -, @ (ou TAB/CR)
        // vira fórmula ao abrir no Excel/Sheets. O apóstrofo à frente a mantém texto
        // e a importação o remove de volta, para o round-trip ficar fiel.
        if (textoLivre && valor.Length > 0 && IniciamFormula.Contains(valor[0]))
            valor = "'" + valor;

        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r'))
            return "\"" + valor.Replace("\"", "\"\"") + "\"";

        return valor;
    }
}
