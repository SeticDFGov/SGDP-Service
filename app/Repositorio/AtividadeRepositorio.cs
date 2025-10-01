using api.Atividade;
using api.Projeto;
using demanda_service.Repositorio.Interface;
using Microsoft.EntityFrameworkCore;
using Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AtividadeDTO = api.Atividade.AtividadeDTO;

namespace Repositorio;

public class AtividadeRepositorio:IAtividadeRepositorio
{
    public readonly AppDbContext _context;

    public AtividadeRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO)
    {
        Projeto projeto =  _context.Projetos.FirstOrDefault(c => c.projetoId == inicioatividadeDTO.NM_PROJETO);
        Report novo_report = new Report
        {
            descricao = inicioatividadeDTO.descricao,
            Data_criacao = DateTime.UtcNow,
            Data_fim = inicioatividadeDTO.data_fim,
            NM_PROJETO = projeto,
            fase = inicioatividadeDTO.fase,
        };
        
        _context.Reports.Add(novo_report);
        await _context.SaveChangesAsync();
    }

    public async Task InserirAtividade(AtividadeDTO atividadeDTO, int reportId)
    {
        Report report = await _context.Reports.FirstOrDefaultAsync(c => c.ReportId == reportId);
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        Atividade nova = new Atividade
        {
            titulo = atividadeDTO.titulo,
            situacao = situacao.proximo,
            categoria = atividadeDTO.categoria,
            descricao = atividadeDTO.descricao,
            data_termino = atividadeDTO.data_fim = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified), brasilia),

            Report = report,
        };
        _context.Atividades.Add(nova);
        await _context.SaveChangesAsync();
    }

    public async Task AlterarAtividade(AtividadeDTO atividadeDTO, int atividadeId)
    {
      
      Atividade atividade = await _context.Atividades.FirstOrDefaultAsync(c => c.AtividadeId == atividadeId);
      var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
      atividade.situacao = atividadeDTO.situacao;
        atividade.categoria = atividadeDTO.categoria;
        atividade.descricao = atividadeDTO.descricao;
        atividade.data_termino = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified), brasilia);

        await _context.SaveChangesAsync();
    }

    public async Task RemoverAtividade(int atividadeId)
    {
        Atividade remove = await _context.Atividades.FirstOrDefaultAsync(c => c.AtividadeId == atividadeId);
        _context.Atividades.Remove(remove);
        await _context.SaveChangesAsync();
    }

   
 
    public async Task<List<Atividade>> VisualizarAtividades(int reportId)
    {
        return await _context.Atividades.Where(c => c.Report.ReportId == reportId).ToListAsync();
    }
    
    public byte[] GerarReportPDF(Guid exportId)
    {
        List<AtividadeExport> atividades = _context.AtividadeExport
            .Include(a => a.Export)
            .Where(a => a.Export.Id == exportId)
            .ToList(); 
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Status Report").FontSize(20).Bold().AlignCenter();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("titulo").Bold();
                        header.Cell().Text("situacao").Bold();
                        header.Cell().Text("categoria").Bold();
                        header.Cell().Text("decrição").Bold();
                    });

                    foreach (var atividade in atividades)
                    {
                        table.Cell().Text(atividade.titulo);
                        table.Cell().Text(atividade.situacao);
                        table.Cell().Text(atividade.categoria);
                        table.Cell().Text(atividade.descricao);
                    }
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

    public async Task GerarStatusReportExport( int projetoId)
    {
        Report report = await _context.Reports
            .Include(r => r.NM_PROJETO)
            .FirstOrDefaultAsync(e => e.NM_PROJETO.projetoId == projetoId);

        Projeto projeto = report.NM_PROJETO; 
        List<Atividade> atividades = await _context.Atividades
            .Where(c => c.Report.NM_PROJETO.projetoId == projetoId)
            .ToListAsync();
        List<AtividadeExport> atividadesExport = new List<AtividadeExport>();
        foreach (var atividade in atividades )
        {
            atividadesExport.Add(
            new AtividadeExport
            {
                titulo    = atividade.titulo,
                descricao = atividade.descricao,
                categoria = atividade.categoria,
                data_termino = atividade.data_termino,
                situacao = atividade.situacao
            }
            );
        }
        Export novo_export = new Export
        {
            NM_PROJETO = projeto,
            descricao = report.descricao,
            Data_fim = report.Data_fim,
            Data_criacao = DateTime.UtcNow,
            AtividadeExport = atividadesExport,
            fase = report.fase,
        };
        _context.Exports.Add(novo_export);
        await  _context.SaveChangesAsync();
    }
}
