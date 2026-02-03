using api.Atividade;
using demanda_service.Repositorio.Interface;
using Microsoft.EntityFrameworkCore;
using Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Repositorio;

/// <summary>
/// Repositório para acesso a dados de Atividade
/// Após refatoração: Atividades estão vinculadas a Etapas, não a Projetos
/// </summary>
public class AtividadeRepositorio : IAtividadeRepositorio
{
    private readonly AppDbContext _context;

    public AtividadeRepositorio(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Busca atividades por ID da etapa (data access only)
    /// </summary>
    public async Task<List<Atividade>> GetAtividadesByEtapaIdAsync(int etapaId)
    {
        return await _context.Atividades
            .Where(a => a.EtapaProjetoId == etapaId)
            .OrderBy(a => a.Order)
            .ToListAsync();
    }

    /// <summary>
    /// Busca atividades por ID do projeto (através das etapas)
    /// </summary>
    public async Task<List<Atividade>> GetAtividadesByProjetoIdAsync(int projetoId)
    {
        return await _context.Atividades
            .Include(a => a.Etapa)
            .Where(a => a.Etapa.NM_PROJETO.projetoId == projetoId)
            .OrderBy(a => a.Etapa.Order)
            .ThenBy(a => a.Order)
            .ToListAsync();
    }

    /// <summary>
    /// Busca atividade por ID (data access only)
    /// </summary>
    public async Task<Atividade?> GetByIdAsync(int id)
    {
        return await _context.Atividades
            .Include(a => a.Etapa)
            .FirstOrDefaultAsync(a => a.AtividadeId == id);
    }

    /// <summary>
    /// Busca etapa por ID (data access only)
    /// </summary>
    public async Task<Etapa?> GetEtapaByIdAsync(int etapaId)
    {
        return await _context.Etapas
            .FirstOrDefaultAsync(e => e.EtapaProjetoId == etapaId);
    }

    /// <summary>
    /// Adiciona uma atividade ao contexto
    /// </summary>
    public void Add(Atividade atividade)
    {
        _context.Atividades.Add(atividade);
    }

    /// <summary>
    /// Remove uma atividade do contexto
    /// </summary>
    public void Remove(Atividade atividade)
    {
        _context.Atividades.Remove(atividade);
    }

    /// <summary>
    /// Salva as mudanças no banco de dados
    /// </summary>
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // ==== MÉTODOS LEGADOS (Reports - manter temporariamente para compatibilidade) ====

    /// <summary>
    /// LEGADO: Iniciar report (manter para compatibilidade com Reports antigos)
    /// </summary>
    public async Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO)
    {
        var projeto = await _context.Projetos
            .FirstOrDefaultAsync(c => c.projetoId == inicioatividadeDTO.NM_PROJETO);

        if (projeto == null)
            throw new Exception("Projeto não encontrado");

        // Busca atividades através das etapas
        var atividades = await GetAtividadesByProjetoIdAsync(inicioatividadeDTO.NM_PROJETO);

        var novoReport = new Report
        {
            descricao = inicioatividadeDTO.descricao,
            Data_criacao = DateTime.UtcNow,
            Data_fim = inicioatividadeDTO.data_fim,
            NM_PROJETO = projeto,
            Atividades = atividades,
            fase = inicioatividadeDTO.fase,
        };

        _context.Reports.Add(novoReport);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// LEGADO: Remover atividade (manter para compatibilidade)
    /// </summary>
    public async Task RemoverAtividade(int atividadeId)
    {
        var atividade = await GetByIdAsync(atividadeId);
        if (atividade != null)
        {
            Remove(atividade);
            await SaveChangesAsync();
        }
    }

    /// <summary>
    /// LEGADO: Visualizar atividades (agora busca por projeto através das etapas)
    /// </summary>
    public async Task<List<Atividade>> VisualizarAtividades(int projetoId)
    {
        return await GetAtividadesByProjetoIdAsync(projetoId);
    }

    /// <summary>
    /// LEGADO: Inserir atividade (compatibilidade com Reports antigos)
    /// NOTA: Esta versão usa a estrutura antiga. Use AtividadeService para novas implementações.
    /// </summary>
    public async Task InserirAtividade(AtividadeDTO atividadeDTO, int projetoId)
    {
        var projeto = await _context.Projetos.FirstOrDefaultAsync(c => c.projetoId == projetoId);
        if (projeto == null)
            throw new Exception("Projeto não encontrado");

        // Timezone helper (compatibilidade)
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

        var novaAtividade = new Atividade
        {
            Titulo = atividadeDTO.titulo,
            Categoria = atividadeDTO.categoria,
            Descricao = atividadeDTO.descricao,
            DT_TERMINO_PREVISTO = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified),
                brasilia
            ),
            DT_INICIO_PREVISTO = DateTime.UtcNow, // Default
            // EtapaProjetoId precisa ser configurado após refatoração completa
        };

        _context.Atividades.Add(novaAtividade);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// LEGADO: Alterar atividade (compatibilidade com Reports antigos)
    /// </summary>
    public async Task AlterarAtividade(AtividadeDTO atividadeDTO, int atividadeId)
    {
        var atividade = await GetByIdAsync(atividadeId);
        if (atividade == null)
            throw new Exception("Atividade não encontrada");

        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

        // Atualiza apenas campos editáveis
        atividade.Categoria = atividadeDTO.categoria;
        atividade.Descricao = atividadeDTO.descricao;
        atividade.DT_TERMINO_REAL = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified),
            brasilia
        );

        await SaveChangesAsync();
    }

    /// <summary>
    /// LEGADO: Gerar PDF do Report (compatibilidade com Reports antigos)
    /// </summary>
    public byte[] GerarReportPDF(int reportId)
    {
        var report = _context.Reports
            .Include(r => r.Atividades)
            .Include(r => r.NM_PROJETO)
            .FirstOrDefault(r => r.ReportId == reportId);

        if (report == null)
            throw new Exception("Report não encontrado");

        var projeto = report.NM_PROJETO;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header().Stack(stack =>
                {
                    stack.Item().Text($"Status Report de Projeto No {projeto.projetoId}")
                        .FontSize(20).Bold().AlignCenter();
                    stack.Item().Text($"De: XX a XX").AlignCenter();
                    stack.Item().PaddingTop(10);
                });

                page.Content().Column(column =>
                {
                    column.Item().Border(1).BorderColor(Colors.Grey.Medium).Table(projectTable =>
                    {
                        projectTable.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(150);
                            columns.RelativeColumn();
                            columns.ConstantColumn(110);
                            columns.RelativeColumn();
                        });

                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                            .Text("Nome do Projeto:");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text(projeto.NM_PROJETO ?? "");

                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                            .Text("Processo de Contratação:");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text(projeto.NR_PROCESSO_SEI ?? "N/A");

                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text("Processo de Pagamento");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text(projeto.NR_PROCESSO_SEI ?? "N/A");

                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                            .Text("Data de início - fim do projeto:");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text("N/A");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text("(documento SEI)");

                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                            .Text("Fase:");
                        projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1)
                            .Padding(5).AlignLeft().Text(report.fase ?? "Execução");
                    });

                    column.Item().PaddingTop(20);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(5)
                                .Background(Colors.Grey.Lighten2).Text("Título");
                            header.Cell().BorderBottom(1).Padding(5)
                                .Background(Colors.Grey.Lighten2).Text("Situação");
                            header.Cell().BorderBottom(1).Padding(5)
                                .Background(Colors.Grey.Lighten2).Text("Categoria");
                            header.Cell().BorderBottom(1).Padding(5)
                                .Background(Colors.Grey.Lighten2).Text("Descrição");
                        });

                        foreach (var atividade in report.Atividades)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(5).Text(atividade.Titulo ?? "");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(5).Text(atividade.SITUACAO);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(5).Text(atividade.Categoria ?? "");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(5).Text(atividade.Descricao ?? "");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }
}
