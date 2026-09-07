using api.Pgia;
using demanda_service.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace service.Pgia;

/// <summary>
/// PDF do relatório semestral do órgão à SGDI (art. 32), para juntada ao processo SEI.
/// As seções I a V espelham os incisos do artigo.
/// </summary>
public static class PgiaRelatorioPdf
{
    static PgiaRelatorioPdf()
    {
        // O Program.cs já define a licença na API; aqui garante o mesmo fora dela
        // (testes e qualquer host que não passe pela inicialização da aplicação).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Gerar(PgiaRelatorioSemestralResponse relatorio)
    {
        var conteudo = relatorio.Conteudo ?? new PgiaRelatorioConteudoResponse();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Relatório Semestral de IA — {relatorio.OrgaoSigla} — "
                        + $"{relatorio.Ano}/{relatorio.Semestre}º semestre")
                        .FontSize(14).SemiBold();
                    col.Item().Text("art. 32 do Decreto nº 48.901/2026").FontSize(9).Italic();
                    col.Item().PaddingTop(4).Text(TextoDoEnvio(relatorio)).FontSize(8);
                    col.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(12);

                    Secao(col, "I — Sistemas de IA em uso, em desenvolvimento ou em aquisição");
                    if (conteudo.Sistemas.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                            });
                            Cabecalho(table, "Denominação", "Fase do ciclo de vida", "Classificação", "Homologação");
                            foreach (var s in conteudo.Sistemas)
                            {
                                Celula(table, s.Denominacao);
                                Celula(table, s.StatusCicloVida);
                                Celula(table, s.ClassificacaoRiscoAtual ?? "—");
                                Celula(table, s.SituacaoHomologacao);
                            }
                        });

                    Secao(col, "II — Incidentes comunicados e medidas adotadas");
                    if (conteudo.Incidentes.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(4);
                            });
                            Cabecalho(table, "Sistema", "Hipótese", "Comunicação", "Apuração", "Medidas adotadas");
                            foreach (var i in conteudo.Incidentes)
                            {
                                Celula(table, i.SistemaDenominacao);
                                Celula(table, i.Hipotese ?? "—");
                                // Instantes do banco são UTC: o leitor vê Brasília
                                Celula(table, DateTimeHelper.ToBrasilia(i.DataComunicacaoSgdi)?.ToString("dd/MM/yyyy") ?? "—");
                                Celula(table, i.StatusApuracao);
                                Celula(table, i.MedidasAdotadas ?? "—");
                            }
                        });

                    Secao(col, "III — Indicadores dos sistemas de Alto Risco");
                    if (conteudo.IndicadoresAltoRisco.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                            });
                            Cabecalho(table, "Sistema", "Indicador", "Categoria", "Valor", "Período");
                            foreach (var i in conteudo.IndicadoresAltoRisco)
                            {
                                Celula(table, i.SistemaDenominacao);
                                Celula(table, i.Nome);
                                Celula(table, i.Categoria);
                                Celula(table, $"{i.Valor:0.##}{(string.IsNullOrWhiteSpace(i.Unidade) ? "" : " " + i.Unidade)}");
                                Celula(table, $"{i.PeriodoInicio:dd/MM/yyyy} a {i.PeriodoFim:dd/MM/yyyy}");
                            }
                        });

                    Secao(col, "IV — Capacitação realizada (trilhas ProCapIA/DF)");
                    if (conteudo.Capacitacoes.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            Cabecalho(table, "Agente", "Trilha", "Situação", "Conclusão");
                            foreach (var c in conteudo.Capacitacoes)
                            {
                                Celula(table, c.AgenteNome);
                                Celula(table, c.Trilha);
                                Celula(table, c.Status);
                                Celula(table, c.DataConclusao?.ToString("dd/MM/yyyy") ?? "—");
                            }
                        });

                    Secao(col, "V — Atualizações do inventário no período");
                    if (conteudo.AtualizacoesInventario.Count == 0)
                        SemDados(col);
                    else
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(5);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                            });
                            Cabecalho(table, "Denominação", "Fase do ciclo de vida", "Classificação", "Homologação");
                            foreach (var s in conteudo.AtualizacoesInventario)
                            {
                                Celula(table, s.Denominacao);
                                Celula(table, s.StatusCicloVida);
                                Celula(table, s.ClassificacaoRiscoAtual ?? "—");
                                Celula(table, s.SituacaoHomologacao);
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

    private static string TextoDoEnvio(PgiaRelatorioSemestralResponse r)
    {
        var envio = r.DataEnvio == null
            ? "não enviado"
            : $"enviado em {r.DataEnvio:dd/MM/yyyy}";
        var sei = string.IsNullOrWhiteSpace(r.ProcessoSei) ? "—" : r.ProcessoSei;
        return $"Situação: {r.Status} ({envio}) · Prazo: {r.PrazoEnvio:dd/MM/yyyy} · Processo SEI: {sei}";
    }

    private static void Secao(ColumnDescriptor col, string titulo)
    {
        col.Item().Text(titulo).FontSize(11).SemiBold();
    }

    private static void SemDados(ColumnDescriptor col)
    {
        col.Item().Text("Sem registros no período.").Italic().FontColor(Colors.Grey.Darken1);
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
