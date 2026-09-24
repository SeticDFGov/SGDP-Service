using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace service.Planejamento;

/// <summary>Como a coluna guarda e mostra o valor.</summary>
public enum PeXlsxTipo
{
    Texto,
    // Texto que quebra linha (texto longo e texto rico)
    TextoLongo,
    // Código do registro (OE01)
    Codigo,
    Numero,
    Moeda,
    Percentual,
    Data
}

public sealed class PeXlsxColuna
{
    public required string Titulo { get; init; }

    public PeXlsxTipo Tipo { get; init; } = PeXlsxTipo.Texto;

    // Casas decimais do número (nulo = formato geral)
    public int? Casas { get; init; }

    // Largura em caracteres
    public double Largura { get; init; } = 20;

    // Valores aceitos (validação de dados com lista); nulo = sem validação
    public IReadOnlyList<string>? Lista { get; init; }

    // Texto de ajuda da coluna (vai para a aba Leia-me)
    public string? Ajuda { get; init; }
}

/// <summary>Uma aba: nome, colunas e linhas (texto, decimal, DateOnly ou nulo em cada célula).</summary>
public sealed class PeXlsxAba
{
    public required string Nome { get; init; }

    public required List<PeXlsxColuna> Colunas { get; init; }

    public List<object?[]> Linhas { get; init; } = new();
}

/// <summary>
/// Gera o XLSX com o Open XML SDK (MIT): cada aba com o cabeçalho destacado e fixo (painel
/// congelado na linha 1), filtro no cabeçalho, largura das colunas, datas e valores como
/// números com formato, listas com validação de dados (os valores ficam numa aba oculta
/// "Listas", sem o limite de 255 caracteres da lista escrita na própria validação) e a aba
/// Leia-me. Texto vai como texto (inline string): nada vira fórmula ao abrir.
/// </summary>
public static class PeXlsx
{
    // Estilos (índices em CellFormats)
    private const uint EstiloPadrao = 0;
    private const uint EstiloCabecalho = 1;
    private const uint EstiloData = 2;
    private const uint EstiloMoeda = 3;
    private const uint EstiloPercentual = 4;
    private const uint EstiloInteiro = 5;
    private const uint EstiloDecimalBase = 5; // + casas (1 a 6) = 6 a 11
    private const uint EstiloTextoLongo = 12;
    private const uint EstiloNegrito = 13;
    private const uint EstiloTitulo = 14;

    public const string NomeLeiaMe = "Leia-me";
    public const string NomeListas = "Listas";
    private const int LinhasComValidacao = 10000;
    private const int MaximoTextoCelula = 32767;

    /// <summary>
    /// O arquivo: as abas de dados, na ordem; depois a Leia-me (itens de cabeçalho e a ajuda de
    /// cada coluna) e, se alguma coluna tem lista, a aba oculta Listas.
    /// </summary>
    public static byte[] Gerar(IReadOnlyList<PeXlsxAba> abas, IReadOnlyList<(string Rotulo, string Valor)> leiaMe)
    {
        using var memoria = new MemoryStream();
        using (var documento = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var livro = documento.AddWorkbookPart();
            livro.Workbook = new Workbook();
            var estilos = livro.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = Estilos();
            estilos.Stylesheet.Save();

            var nomes = NomesUnicos(abas.Select(a => a.Nome).Append(NomeLeiaMe).Append(NomeListas).ToList());
            var planilhas = new Sheets();
            var nomesDefinidos = new DefinedNames();

            // Listas: uma coluna por coluna de dados que tem lista
            var listas = new List<(string Cabecalho, IReadOnlyList<string> Valores)>();
            var referenciaDaLista = new Dictionary<(int Aba, int Coluna), string>();
            for (var a = 0; a < abas.Count; a++)
                for (var c = 0; c < abas[a].Colunas.Count; c++)
                {
                    var valores = abas[a].Colunas[c].Lista;
                    if (valores == null || valores.Count == 0) continue;
                    var letra = Letra(listas.Count);
                    referenciaDaLista[(a, c)] = $"'{nomes[abas.Count + 1]}'!${letra}$2:${letra}${valores.Count + 1}";
                    listas.Add(($"{nomes[a]} · {abas[a].Colunas[c].Titulo}", valores));
                }

            uint idPlanilha = 1;
            for (var a = 0; a < abas.Count; a++)
            {
                var parte = livro.AddNewPart<WorksheetPart>();
                parte.Worksheet = AbaDeDados(abas[a], a, referenciaDaLista, primeira: a == 0);
                parte.Worksheet.Save();
                planilhas.Append(new Sheet { Id = livro.GetIdOfPart(parte), SheetId = idPlanilha++, Name = nomes[a] });

                // Nome do filtro, como o Excel grava
                var ultima = Letra(Math.Max(abas[a].Colunas.Count, 1) - 1);
                nomesDefinidos.Append(new DefinedName($"'{nomes[a].Replace("'", "''")}'!$A$1:${ultima}${abas[a].Linhas.Count + 1}")
                {
                    Name = "_xlnm._FilterDatabase",
                    LocalSheetId = (uint)a,
                    Hidden = true
                });
            }

            var parteLeiaMe = livro.AddNewPart<WorksheetPart>();
            parteLeiaMe.Worksheet = AbaLeiaMe(abas, nomes, leiaMe);
            parteLeiaMe.Worksheet.Save();
            planilhas.Append(new Sheet { Id = livro.GetIdOfPart(parteLeiaMe), SheetId = idPlanilha++, Name = nomes[abas.Count] });

            if (listas.Count > 0)
            {
                var parteListas = livro.AddNewPart<WorksheetPart>();
                parteListas.Worksheet = AbaListas(listas);
                parteListas.Worksheet.Save();
                planilhas.Append(new Sheet
                {
                    Id = livro.GetIdOfPart(parteListas),
                    SheetId = idPlanilha,
                    Name = nomes[abas.Count + 1],
                    State = SheetStateValues.Hidden
                });
            }

            livro.Workbook.Append(new BookViews(new WorkbookView { ActiveTab = 0 }));
            livro.Workbook.Append(planilhas);
            if (nomesDefinidos.HasChildren) livro.Workbook.Append(nomesDefinidos);
            livro.Workbook.Save();
        }
        return memoria.ToArray();
    }

    // ── Abas ────────────────────────────────────────────────────────────────

    private static Worksheet AbaDeDados(PeXlsxAba aba, int indice, IReadOnlyDictionary<(int, int), string> listas, bool primeira)
    {
        var dados = new SheetData();
        var cabecalho = new Row { RowIndex = 1 };
        for (var c = 0; c < aba.Colunas.Count; c++)
            cabecalho.Append(CelulaTexto(Ref(c, 1), aba.Colunas[c].Titulo, EstiloCabecalho));
        dados.Append(cabecalho);

        for (var l = 0; l < aba.Linhas.Count; l++)
        {
            var numeroLinha = (uint)(l + 2);
            var linha = new Row { RowIndex = numeroLinha };
            for (var c = 0; c < aba.Colunas.Count; c++)
            {
                var valor = c < aba.Linhas[l].Length ? aba.Linhas[l][c] : null;
                var celula = Celula(Ref(c, numeroLinha), aba.Colunas[c], valor);
                if (celula != null) linha.Append(celula);
            }
            dados.Append(linha);
        }

        var planilha = new Worksheet();
        planilha.Append(new SheetViews(new SheetView(
            new Pane
            {
                VerticalSplit = 1D,
                TopLeftCell = "A2",
                ActivePane = PaneValues.BottomLeft,
                State = PaneStateValues.Frozen
            },
            new Selection
            {
                Pane = PaneValues.BottomLeft,
                ActiveCell = "A2",
                SequenceOfReferences = new ListValue<StringValue> { InnerText = "A2" }
            })
        {
            WorkbookViewId = 0U,
            TabSelected = primeira
        }));

        if (aba.Colunas.Count > 0)
        {
            var colunas = new Columns();
            for (var c = 0; c < aba.Colunas.Count; c++)
                colunas.Append(new Column
                {
                    Min = (uint)(c + 1),
                    Max = (uint)(c + 1),
                    Width = aba.Colunas[c].Largura,
                    CustomWidth = true
                });
            planilha.Append(colunas);
        }

        planilha.Append(dados);

        if (aba.Colunas.Count > 0)
            planilha.Append(new AutoFilter { Reference = $"A1:{Letra(aba.Colunas.Count - 1)}{aba.Linhas.Count + 1}" });

        var validacoes = new DataValidations();
        for (var c = 0; c < aba.Colunas.Count; c++)
        {
            if (!listas.TryGetValue((indice, c), out var referencia)) continue;
            var letra = Letra(c);
            validacoes.Append(new DataValidation(new Formula1(referencia))
            {
                Type = DataValidationValues.List,
                AllowBlank = true,
                ShowErrorMessage = true,
                ErrorTitle = "Valor fora da lista",
                Error = "Escolha um dos valores da lista.",
                SequenceOfReferences = new ListValue<StringValue> { InnerText = $"{letra}2:{letra}{LinhasComValidacao}" }
            });
        }
        if (validacoes.HasChildren)
        {
            validacoes.Count = (uint)validacoes.ChildElements.Count;
            planilha.Append(validacoes);
        }

        return planilha;
    }

    private static Worksheet AbaLeiaMe(IReadOnlyList<PeXlsxAba> abas, IReadOnlyList<string> nomes,
        IReadOnlyList<(string Rotulo, string Valor)> itens)
    {
        var dados = new SheetData();
        uint linha = 1;

        var titulo = new Row { RowIndex = linha };
        titulo.Append(CelulaTexto(Ref(0, linha), "Leia-me", EstiloTitulo));
        dados.Append(titulo);
        linha += 2;

        foreach (var (rotulo, valor) in itens)
        {
            var row = new Row { RowIndex = linha };
            row.Append(CelulaTexto(Ref(0, linha), rotulo, EstiloNegrito));
            row.Append(CelulaTexto(Ref(1, linha), valor, EstiloTextoLongo));
            dados.Append(row);
            linha++;
        }
        linha++;

        var cabecalho = new Row { RowIndex = linha };
        cabecalho.Append(CelulaTexto(Ref(0, linha), "Aba", EstiloCabecalho));
        cabecalho.Append(CelulaTexto(Ref(1, linha), "Coluna", EstiloCabecalho));
        cabecalho.Append(CelulaTexto(Ref(2, linha), "O que a coluna traz", EstiloCabecalho));
        dados.Append(cabecalho);
        linha++;

        for (var a = 0; a < abas.Count; a++)
            foreach (var coluna in abas[a].Colunas)
            {
                var row = new Row { RowIndex = linha };
                row.Append(CelulaTexto(Ref(0, linha), nomes[a], EstiloPadrao));
                row.Append(CelulaTexto(Ref(1, linha), coluna.Titulo, EstiloPadrao));
                row.Append(CelulaTexto(Ref(2, linha), string.IsNullOrWhiteSpace(coluna.Ajuda) ? "-" : coluna.Ajuda!, EstiloTextoLongo));
                dados.Append(row);
                linha++;
            }

        var planilha = new Worksheet();
        planilha.Append(new Columns(
            new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true },
            new Column { Min = 2, Max = 2, Width = 40, CustomWidth = true },
            new Column { Min = 3, Max = 3, Width = 90, CustomWidth = true }));
        planilha.Append(dados);
        return planilha;
    }

    private static Worksheet AbaListas(IReadOnlyList<(string Cabecalho, IReadOnlyList<string> Valores)> listas)
    {
        var dados = new SheetData();
        var maior = listas.Max(l => l.Valores.Count);
        for (uint linha = 1; linha <= maior + 1; linha++)
        {
            var row = new Row { RowIndex = linha };
            for (var c = 0; c < listas.Count; c++)
            {
                var texto = linha == 1
                    ? listas[c].Cabecalho
                    : linha - 2 < listas[c].Valores.Count ? listas[c].Valores[(int)linha - 2] : null;
                if (texto != null) row.Append(CelulaTexto(Ref(c, linha), texto, linha == 1 ? EstiloCabecalho : EstiloPadrao));
            }
            dados.Append(row);
        }
        var planilha = new Worksheet();
        planilha.Append(dados);
        return planilha;
    }

    // ── Células ─────────────────────────────────────────────────────────────

    private static Cell? Celula(string referencia, PeXlsxColuna coluna, object? valor)
    {
        switch (valor)
        {
            case null:
                return null;
            case string texto:
                return texto.Length == 0 ? null : CelulaTexto(referencia, texto,
                    coluna.Tipo == PeXlsxTipo.TextoLongo ? EstiloTextoLongo : EstiloPadrao);
            case DateOnly data:
                return CelulaNumero(referencia, data.ToDateTime(TimeOnly.MinValue).ToOADate().ToString("R", CultureInfo.InvariantCulture), EstiloData);
            case decimal numero:
                var estilo = coluna.Tipo switch
                {
                    PeXlsxTipo.Moeda => EstiloMoeda,
                    PeXlsxTipo.Percentual => EstiloPercentual,
                    PeXlsxTipo.Numero when coluna.Casas == 0 => EstiloInteiro,
                    PeXlsxTipo.Numero when coluna.Casas is >= 1 and <= 6 => EstiloDecimalBase + (uint)coluna.Casas.Value,
                    _ => EstiloPadrao
                };
                return CelulaNumero(referencia, numero.ToString(CultureInfo.InvariantCulture), estilo);
            default:
                return CelulaTexto(referencia, Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty, EstiloPadrao);
        }
    }

    private static Cell CelulaTexto(string referencia, string texto, uint estilo) => new()
    {
        CellReference = referencia,
        DataType = CellValues.InlineString,
        StyleIndex = estilo,
        InlineString = new InlineString(new Text(Limpo(texto)) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static Cell CelulaNumero(string referencia, string numero, uint estilo) => new()
    {
        CellReference = referencia,
        StyleIndex = estilo,
        CellValue = new CellValue(numero)
    };

    /// <summary>Sem os caracteres que o XML não aceita e no limite de uma célula do Excel.</summary>
    private static string Limpo(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
                {
                    sb.Append(c).Append(texto[i + 1]);
                    i++;
                }
                continue;
            }
            if (char.IsLowSurrogate(c)) continue;
            if (c < 0x20 && c is not ('\t' or '\n' or '\r')) continue;
            if (c is '￾' or '￿') continue;
            sb.Append(c);
        }
        var limpo = sb.ToString();
        return limpo.Length <= MaximoTextoCelula ? limpo : limpo[..MaximoTextoCelula];
    }

    // ── Estilos ─────────────────────────────────────────────────────────────

    private static Stylesheet Estilos()
    {
        var formatos = new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "dd/mm/yyyy" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "\"R$\" #,##0.00" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "0.00\"%\"" },
            new NumberingFormat { NumberFormatId = 167, FormatCode = "#,##0" });
        for (var casas = 1; casas <= 6; casas++)
            formatos.Append(new NumberingFormat { NumberFormatId = (uint)(167 + casas), FormatCode = "#,##0." + new string('0', casas) });
        formatos.Count = (uint)formatos.ChildElements.Count;

        var fontes = new Fonts(
            new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 14 }, new FontName { Val = "Calibri" }))
        { Count = 3 };

        var preenchimentos = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFD9E1F2" }, new BackgroundColor { Indexed = 64U })
            {
                PatternType = PatternValues.Solid
            }))
        { Count = 3 };

        var bordas = new Borders(new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()))
        { Count = 1 };

        var formatosCelula = new CellFormats(
            Xf(),                                                                                               // 0 padrão
            Xf(fonte: 1, preenchimento: 2, alinhamento: new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center }), // 1 cabeçalho
            Xf(formato: 164),                                                                                   // 2 data
            Xf(formato: 165),                                                                                   // 3 moeda
            Xf(formato: 166),                                                                                   // 4 percentual
            Xf(formato: 167));                                                                                  // 5 inteiro
        for (var casas = 1; casas <= 6; casas++) formatosCelula.Append(Xf(formato: (uint)(167 + casas)));      // 6 a 11
        formatosCelula.Append(Xf(alinhamento: new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top })); // 12 texto longo
        formatosCelula.Append(Xf(fonte: 1));                                                                   // 13 negrito
        formatosCelula.Append(Xf(fonte: 2));                                                                   // 14 título
        formatosCelula.Count = (uint)formatosCelula.ChildElements.Count;

        return new Stylesheet(formatos, fontes, preenchimentos, bordas,
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
            formatosCelula,
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
    }

    private static CellFormat Xf(uint formato = 0, uint fonte = 0, uint preenchimento = 0, Alignment? alinhamento = null)
    {
        var xf = new CellFormat { NumberFormatId = formato, FontId = fonte, FillId = preenchimento, BorderId = 0, FormatId = 0 };
        if (formato != 0) xf.ApplyNumberFormat = true;
        if (fonte != 0) xf.ApplyFont = true;
        if (preenchimento != 0) xf.ApplyFill = true;
        if (alinhamento != null)
        {
            xf.Append(alinhamento);
            xf.ApplyAlignment = true;
        }
        return xf;
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>Letra da coluna (0 = A, 25 = Z, 26 = AA).</summary>
    public static string Letra(int indice)
    {
        var letras = string.Empty;
        for (var n = indice + 1; n > 0; n = (n - 1) / 26)
            letras = (char)('A' + (n - 1) % 26) + letras;
        return letras;
    }

    private static string Ref(int coluna, uint linha) => Letra(coluna) + linha.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Nomes de aba válidos no Excel: sem : \ / ? * [ ], até 31 caracteres, sem apóstrofo nas
    /// pontas e sem repetir (ignorando maiúsculas).
    /// </summary>
    public static List<string> NomesUnicos(IReadOnlyList<string> nomes)
    {
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var saida = new List<string>();
        foreach (var original in nomes)
        {
            var limpo = new string(original.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']') && !char.IsControl(c)).ToArray())
                .Trim().Trim('\'').Trim();
            if (limpo.Length == 0 || string.Equals(limpo, "History", StringComparison.OrdinalIgnoreCase)) limpo = "Aba";
            if (limpo.Length > 31) limpo = limpo[..31].TrimEnd();

            var nome = limpo;
            for (var n = 2; !usados.Add(nome); n++)
            {
                var sufixo = $" ({n})";
                nome = (limpo.Length + sufixo.Length > 31 ? limpo[..(31 - sufixo.Length)].TrimEnd() : limpo) + sufixo;
            }
            saida.Add(nome);
        }
        return saida;
    }
}
