using System.Globalization;
using System.Text.Json;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Planilhas do PETIC-DF e do catálogo do DF (plano, seção 11). Colunas: o código (tabela
/// com prefixo), depois os campos visíveis e marcados "na planilha", na ordem do modelo,
/// com os rótulos configurados; ligações mostram os códigos dos registros ligados.
/// <list type="bullet">
/// <item>CSV: pelo escritor comum (UTF-8 com BOM, ";", CRLF, proteção contra fórmula no
/// texto digitado); números com vírgula e sem milhar, datas em dd/mm/aaaa, listas pelo rótulo;</item>
/// <item>XLSX (Open XML SDK): uma aba por seção, cabeçalho destacado e fixo, filtro, datas e
/// valores como números com formato, listas e sim ou não com validação, e a aba Leia-me
/// (dono, versão, data da extração e a ajuda de cada coluna).</item>
/// </list>
/// Nome do arquivo: PETIC-DF_versao_secao_aaaa-mm-dd (a completa leva "completa" no lugar da
/// seção) e DF_secao_aaaa-mm-dd.
/// </summary>
public class PePlanilhaService : IPePlanilhaService
{
    public const string MimeCsv = "text/csv; charset=utf-8";
    public const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string Csv = "csv";
    private const string Xlsx = "xlsx";

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;

    public PePlanilhaService(AppDbContext context, IPeRegistroService registros)
    {
        _context = context;
        _registros = registros;
    }

    public async Task<PePlanilhaArquivo> PeticSecaoAsync(long peticId, string secaoChave, string? formato, PeUserContext ctx)
    {
        var tipo = Formato(formato, completa: false);
        var secao = await _registros.ExportarSecaoAsync(PeDono.DoPetic(peticId), secaoChave, ctx);
        var petic = await _context.PePetics.AsNoTracking().FirstAsync(p => p.Id == peticId);
        var nome = $"PETIC-DF_{petic.Versao}_{secao.Modelo.Secao.Chave}_{Hoje()}.{tipo}";
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsv(secao), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { Aba(secao) }, LeiaMeDoPetic(petic)), MimeXlsx, nome);
    }

    public async Task<PePlanilhaArquivo> PeticCompletaAsync(long peticId, string? formato, PeUserContext ctx)
    {
        Formato(formato, completa: true);
        var secoes = await _registros.ExportarSecoesAsync(PeDono.DoPetic(peticId), ctx);
        var petic = await _context.PePetics.AsNoTracking().FirstAsync(p => p.Id == peticId);
        var nome = $"PETIC-DF_{petic.Versao}_completa_{Hoje()}.{Xlsx}";
        return new PePlanilhaArquivo(PeXlsx.Gerar(secoes.Select(Aba).ToList(), LeiaMeDoPetic(petic)), MimeXlsx, nome);
    }

    public async Task<PePlanilhaArquivo> DfSecaoAsync(string secaoChave, string? formato, PeUserContext ctx)
    {
        var tipo = Formato(formato, completa: false);
        var secao = await _registros.ExportarSecaoAsync(PeDono.Df, secaoChave, ctx);
        var nome = $"DF_{secao.Modelo.Secao.Chave}_{Hoje()}.{tipo}";
        var leiaMe = new List<(string, string)>
        {
            ("Dono", "Catálogo do DF (princípios e diretrizes do ciclo)"),
            ("Seção", secao.Modelo.Secao.Titulo),
            ("Extraído em", Agora())
        };
        leiaMe.AddRange(ComoLer());
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsv(secao), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { Aba(secao) }, leiaMe), MimeXlsx, nome);
    }

    // ── CSV ─────────────────────────────────────────────────────────────────

    /// <summary>O CSV de uma seção: cabeçalho com os rótulos e uma linha por registro.</summary>
    public static byte[] GerarCsv(PeSecaoExportada secao)
    {
        var csv = new CsvEscritor();
        var comCodigo = TemCodigo(secao);

        var cabecalho = new List<(string, bool)>();
        if (comCodigo) cabecalho.Add(("Código", false));
        cabecalho.AddRange(secao.Colunas.Select(c => (c.Campo.Rotulo, true)));
        csv.Linha(cabecalho);

        foreach (var registro in secao.Registros)
        {
            var linha = new List<(string, bool)>();
            if (comCodigo) linha.Add((registro.Codigo ?? string.Empty, false));
            foreach (var coluna in secao.Colunas)
            {
                var (valor, textoLivre) = TextoCsv(coluna.Campo, registro);
                linha.Add((valor, textoLivre));
            }
            csv.Linha(linha);
        }
        return csv.ParaBytes();
    }

    private static (string Valor, bool TextoLivre) TextoCsv(PeCampo campo, PeRegistroResponse registro)
    {
        var rotulo = registro.Rotulos.GetValueOrDefault(campo.Chave) ?? string.Empty;
        switch (TipoDaColuna(campo))
        {
            case PeXlsxTipo.Numero:
            case PeXlsxTipo.Moeda:
            case PeXlsxTipo.Percentual:
                var numero = Numero(registro, campo.Chave);
                if (numero == null) return (string.Empty, false);
                var casas = campo.Tipo == PeDominios.TipoCampo.Numero ? PeValores.Inteiro(campo.Config, "casas") : null;
                var formato = campo.Tipo == PeDominios.TipoCampo.Moeda ? "0.00"
                    : casas is > 0 ? "0." + new string('0', casas.Value)
                    : casas == 0 ? "0"
                    : "0.######";
                return (numero.Value.ToString(formato, PeFormato.Csv), false);
            case PeXlsxTipo.Data:
                return (rotulo, false);
            default:
                // Ligações mostram códigos (dados pelo sistema); o resto veio do teclado de alguém
                return (rotulo, !PeRegistroDados.EhLigacao(campo));
        }
    }

    // ── XLSX ────────────────────────────────────────────────────────────────

    private static PeXlsxAba Aba(PeSecaoExportada secao)
    {
        var colunas = new List<PeXlsxColuna>();
        var comCodigo = TemCodigo(secao);
        if (comCodigo)
            colunas.Add(new PeXlsxColuna
            {
                Titulo = "Código",
                Tipo = PeXlsxTipo.Codigo,
                Largura = 10,
                Ajuda = "Código do registro, dado pelo sistema. Não se repete, nem depois de apagado."
            });

        foreach (var coluna in secao.Colunas)
        {
            var campo = coluna.Campo;
            var tipo = TipoDaColuna(campo);
            colunas.Add(new PeXlsxColuna
            {
                Titulo = campo.Rotulo,
                Tipo = tipo,
                Casas = campo.Tipo == PeDominios.TipoCampo.Numero ? PeValores.Inteiro(campo.Config, "casas") : null,
                Largura = Largura(campo, tipo),
                Lista = campo.Tipo switch
                {
                    PeDominios.TipoCampo.Lista => secao.Modelo.OpcoesDe(campo).Where(o => o.Ativa).Select(o => o.Rotulo).ToList(),
                    PeDominios.TipoCampo.SimNao => new[] { "Sim", "Não" },
                    _ => null
                },
                Ajuda = AjudaDaColuna(campo)
            });
        }

        var linhas = secao.Registros.Select(registro =>
        {
            var celulas = new List<object?>();
            if (comCodigo) celulas.Add(registro.Codigo);
            foreach (var coluna in secao.Colunas)
            {
                var campo = coluna.Campo;
                celulas.Add(TipoDaColuna(campo) switch
                {
                    PeXlsxTipo.Numero or PeXlsxTipo.Moeda or PeXlsxTipo.Percentual => Numero(registro, campo.Chave),
                    PeXlsxTipo.Data => Data(registro, campo.Chave),
                    _ => registro.Rotulos.GetValueOrDefault(campo.Chave)
                });
            }
            return celulas.ToArray();
        }).ToList();

        return new PeXlsxAba { Nome = secao.Modelo.Secao.Titulo, Colunas = colunas, Linhas = linhas };
    }

    private static PeXlsxTipo TipoDaColuna(PeCampo campo) => campo.Tipo switch
    {
        PeDominios.TipoCampo.Numero => PeXlsxTipo.Numero,
        PeDominios.TipoCampo.Moeda => PeXlsxTipo.Moeda,
        PeDominios.TipoCampo.Percentual => PeXlsxTipo.Percentual,
        PeDominios.TipoCampo.Data => PeXlsxTipo.Data,
        PeDominios.TipoCampo.TextoLongo or PeDominios.TipoCampo.TextoRico => PeXlsxTipo.TextoLongo,
        PeDominios.TipoCampo.Calculado when PeConfigCampo.TipoDoCalculo(campo.Config) != PeDominios.Calculo.NivelRisco => PeXlsxTipo.Numero,
        _ => PeXlsxTipo.Texto
    };

    private static double Largura(PeCampo campo, PeXlsxTipo tipo) => campo.Largura switch
    {
        PeDominios.Largura.Estreita => 12,
        PeDominios.Largura.Media => 24,
        PeDominios.Largura.Larga => 50,
        _ => tipo switch
        {
            PeXlsxTipo.TextoLongo => 50,
            PeXlsxTipo.Data => 12,
            PeXlsxTipo.Numero or PeXlsxTipo.Percentual => 14,
            PeXlsxTipo.Moeda => 18,
            _ => 24
        }
    };

    private static string? AjudaDaColuna(PeCampo campo)
    {
        var ajuda = campo.Ajuda?.Trim();
        return PeRegistroDados.EhLigacao(campo)
            ? string.IsNullOrEmpty(ajuda) ? "Os códigos dos registros ligados." : $"{ajuda} A coluna mostra os códigos dos registros ligados."
            : ajuda;
    }

    private static IReadOnlyList<(string, string)> LeiaMeDoPetic(PePetic petic)
    {
        var itens = new List<(string, string)>
        {
            ("Dono", "PETIC-DF (Plano Estratégico de Tecnologia da Informação e Comunicação do Distrito Federal)"),
            ("Versão", $"{petic.Versao} ({Situacao(petic.Situacao)})"),
            ("Título", petic.Titulo),
            ("Vigência", petic.VigenciaInicio != null && petic.VigenciaFim != null
                ? $"{PeFormato.Data(petic.VigenciaInicio.Value)} a {PeFormato.Data(petic.VigenciaFim.Value)}"
                : "-"),
            ("Extraído em", Agora())
        };
        itens.AddRange(ComoLer());
        return itens;
    }

    private static IEnumerable<(string, string)> ComoLer() => new[]
    {
        ("Como ler", "Cada aba é uma seção. A coluna Código identifica cada registro (por exemplo, OE01), e as colunas de "
                     + "ligação mostram os códigos dos registros ligados, para cruzar as abas. Datas e valores estão como números.")
    };

    private static string Situacao(string situacao) => situacao switch
    {
        PeDominios.SituacaoPetic.Rascunho => "rascunho",
        PeDominios.SituacaoPetic.EmDeliberacao => "em deliberação no CGTIC",
        PeDominios.SituacaoPetic.Aprovado => "aprovada, vigente",
        PeDominios.SituacaoPetic.Substituido => "substituída",
        _ => situacao
    };

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static bool TemCodigo(PeSecaoExportada secao) =>
        !secao.Modelo.EhFormulario && !string.IsNullOrEmpty(secao.Modelo.Secao.PrefixoCodigo);

    private static decimal? Numero(PeRegistroResponse registro, string chave) =>
        registro.Dados.TryGetValue(chave, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)
            ? numero
            : null;

    private static DateOnly? Data(PeRegistroResponse registro, string chave) =>
        registro.Dados.TryGetValue(chave, out var valor) && valor.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(valor.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    /// <summary>csv ou xlsx (padrão: xlsx); a completa só sai em xlsx.</summary>
    private static string Formato(string? formato, bool completa)
    {
        var texto = string.IsNullOrWhiteSpace(formato) ? Xlsx : formato.Trim().ToLowerInvariant();
        if (texto is not (Csv or Xlsx))
            throw new ApiException(ErrorCode.PePlanilhaInvalida, "Escolha o formato da planilha: csv ou xlsx.");
        if (completa && texto == Csv)
            throw new ApiException(ErrorCode.PePlanilhaInvalida, "A planilha completa, com uma aba por seção, só sai em xlsx.");
        return texto;
    }

    private static string Hoje() => DateTimeHelper.TodayBrasilia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Agora() =>
        DateTimeHelper.NowBrasilia().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (horário de Brasília)";
}
