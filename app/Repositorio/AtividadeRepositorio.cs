using api.Atividade;
using api.Projeto;
using demanda_service.Repositorio.Interface;
using Microsoft.EntityFrameworkCore;
using Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AtividadeDTO = api.Atividade.AtividadeDTO;
using QuestPDF.Fluent; 
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

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
        List<Atividade> atividades  = _context.Atividades.Where(a => a.NM_PROJETO == inicioatividadeDTO.NM_PROJETO)
            .ToList();
        
        Report novo_report = new Report
        {
            descricao = inicioatividadeDTO.descricao,
            Data_criacao = DateTime.UtcNow,
            Data_fim = inicioatividadeDTO.data_fim,
            NM_PROJETO = projeto,
            Atividades = atividades,
            fase = inicioatividadeDTO.fase,
        };
        
        _context.Reports.Add(novo_report);
        await _context.SaveChangesAsync();
    }
    
    public async Task InserirAtividade(AtividadeDTO atividadeDTO, int projetoId)
    {
        Projeto projeto = await _context.Projetos.FirstOrDefaultAsync(c => c.projetoId == projetoId);
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        Atividade nova = new Atividade
        {
            titulo = atividadeDTO.titulo,
            situacao = situacao.proximo,
            categoria = atividadeDTO.categoria,
            descricao = atividadeDTO.descricao,
            data_termino = atividadeDTO.data_fim = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified), brasilia),

            NM_PROJETO = projeto.projetoId,
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

   
 
    public async Task<List<Atividade>> VisualizarAtividades(int projetoId)
    {
        return await _context.Atividades.Where(c => c.NM_PROJETO == projetoId).ToListAsync();
    }
    
 public byte[] GerarReportPDF(int reportId)
 { 
     Report report = _context.Reports.Include(r=>r.Atividades).Include(r => r.NM_PROJETO).FirstOrDefault(r => r.ReportId == reportId);
    var projeto = report.NM_PROJETO;
    
    var doc = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Margin(30);

            page.Header().Stack(stack =>
            {
                // Substitua pela sua propriedade
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

                   
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Nome do Projeto:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NM_PROJETO ?? ""); 

               
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Processo de Contratação:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NR_PROCESSO_SEI ?? "N/A"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft() // Era ValueCellStyle
                        .Text("Processo de Pagamento"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NR_PROCESSO_SEI ?? "N/A"); 

                 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Data de início - fim do projeto:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text( "N/A"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text("(documento SEI)");

                                 // --- Linha 6: Fase ---
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Fase:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(report.fase ?? "Execução");
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
                        // <<< CORRIGIDO: Estilos aplicados diretamente e 'Lighten2' corrigido >>>
                        header.Cell().BorderBottom(1).Padding(5).Background(Colors.Grey.Lighten2).Text("Título");
                        header.Cell().BorderBottom(1).Padding(5).Background(Colors.Grey.Lighten2).Text("Situação");
                        header.Cell().BorderBottom(1).Padding(5).Background(Colors.Grey.Lighten2).Text("Categoria");
                        header.Cell().BorderBottom(1).Padding(5).Background(Colors.Grey.Lighten2).Text("Descrição");
                    });

                    foreach (var atividade in report.Atividades)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.titulo);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.situacao);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.categoria);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.descricao);
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

    public async Task GerarStatusReportExport( int projetoId)
    {
        /*Report report = await _context.Reports
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
        await  _context.SaveChangesAsync();*/
    }
}
