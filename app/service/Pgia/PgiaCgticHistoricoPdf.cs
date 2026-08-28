using api.Pgia;
using demanda_service.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace service.Pgia;

/// <summary>
/// PDF do histórico de decisões do CGTIC (art. 7º), para auditoria e juntada ao
/// processo: contagens, casos submetidos ao comitê com as respectivas
/// deliberações e as demais deliberações do colegiado.
/// </summary>
public static class PgiaCgticHistoricoPdf
{
    static PgiaCgticHistoricoPdf()
    {
        // O Program.cs já define a licença na API; aqui garante o mesmo fora dela
        // (testes e qualquer host que não passe pela inicialização da aplicação).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Gerar(PgiaCgticHistoricoResponse historico)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Histórico de decisões do CGTIC").FontSize(14).SemiBold();
                    col.Item().Text("art. 7º do Decreto nº 48.901/2026").FontSize(9).Italic();
                    col.Item().PaddingTop(4).Text(
                        $"Gerado em {DateTimeHelper.NowBrasilia():dd/MM/yyyy HH:mm} (horário de Brasília)")
                        .FontSize(8);
                    col.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(12);

                    Secao(col, "Contagens");
                    col.Item().Text(TextoDasContagens(historico.Contagens));

                    Secao(col, "Casos submetidos ao comitê");
                    if (historico.Casos.Count == 0)
                        SemDados(col);
                    else
                        foreach (var caso in historico.Casos)
                            Caso(col, caso);

                    Secao(col, "Demais deliberações do comitê");
                    if (historico.OutrasDeliberacoes.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(4);
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(5);
                            });
                            // Também caem aqui deliberações SOBRE sistemas fora da rota do
                            // comitê (ex.: suspensão de Baixo Risco) — o objeto precisa
                            // aparecer no PDF de auditoria, não só no JSON
                            Cabecalho(table, "Data", "Tipo", "Sistema (órgão)", "Resultado", "Ato", "Ementa");
                            foreach (var d in historico.OutrasDeliberacoes)
                            {
                                Celula(table, $"{d.DataDeliberacao:dd/MM/yyyy}");
                                Celula(table, d.Tipo);
                                Celula(table, d.SistemaDenominacao == null
                                    ? "—"
                                    : $"{d.SistemaDenominacao} ({d.OrgaoSigla})");
                                Celula(table, d.Resultado);
                                Celula(table, d.NumeroAto ?? "—");
                                Celula(table, d.Ementa ?? "—");
                            }
                        });
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text(
                            $"Gerado pelo SGDP/PGIA em {DateTimeHelper.NowBrasilia():dd/MM/yyyy HH:mm}")
                            .FontSize(8);
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(8));
                            t.CurrentPageNumber();
                            t.Span(" / ");
                            t.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string TextoDasContagens(PgiaCgticContagens c)
    {
        return $"Total de casos: {c.Total} · Pendentes: {c.Pendentes} · "
            + $"Analisados: {c.Analisadas} · Aprovados: {c.Aprovadas} · Negados: {c.Negadas}";
    }

    /// <summary>Um caso: identificação do sistema e as deliberações que o trataram.</summary>
    private static void Caso(ColumnDescriptor col, PgiaCgticCasoHistorico caso)
    {
        col.Item().Column(bloco =>
        {
            bloco.Item().Text(t =>
            {
                t.Span($"{caso.Denominacao} ").SemiBold();
                t.Span($"({caso.OrgaoSigla} — {caso.OrgaoNome})").FontSize(8);
            });
            bloco.Item().Text(
                $"Situação: {caso.Situacao} · Homologação: {caso.SituacaoHomologacao} · "
                + $"Classificação: {caso.ClassificacaoRiscoAtual ?? "—"} · "
                + $"Entrada na pauta: {(caso.DataEntrada == null ? "—" : caso.DataEntrada.Value.ToString("dd/MM/yyyy"))}")
                .FontSize(8);

            if (caso.Deliberacoes.Count == 0)
            {
                bloco.Item().PaddingTop(2).Text("Sem deliberação registrada.")
                    .Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            bloco.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(4);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(6);
                });
                Cabecalho(table, "Data", "Tipo", "Resultado", "Ato", "Ementa");
                foreach (var d in caso.Deliberacoes)
                {
                    Celula(table, $"{d.DataDeliberacao:dd/MM/yyyy}");
                    Celula(table, d.Tipo);
                    Celula(table, d.Resultado);
                    Celula(table, d.NumeroAto ?? "—");
                    Celula(table, d.Ementa ?? "—");
                }
            });
        });
    }

    private static void Secao(ColumnDescriptor col, string titulo)
    {
        col.Item().Text(titulo).FontSize(11).SemiBold();
    }

    private static void SemDados(ColumnDescriptor col)
    {
        col.Item().Text("Sem registros.").Italic().FontColor(Colors.Grey.Darken1);
    }

    private static void Cabecalho(TableDescriptor table, params string[] titulos)
    {
        table.Header(header =>
        {
            foreach (var titulo in titulos)
            {
                header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(titulo).SemiBold();
            }
        });
    }

    private static void Celula(TableDescriptor table, string texto)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(texto);
    }
}
