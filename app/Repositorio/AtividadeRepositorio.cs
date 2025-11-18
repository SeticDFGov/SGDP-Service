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

    if (!atividades.Any())
    {
        return new byte[0]; 
    }

    var export = _context.Exports.Include(a=>a.NM_PROJETO).FirstOrDefault(a=>a.Id == exportId);
    var projeto = export.NM_PROJETO;
    
     

    // <<< NOTA IMPORTANTE >>>
    // Substitua 'dadosDoProjeto.NomeProjeto', 'dadosDoProjeto.Fase', etc.,
    // pelas propriedades REAIS do seu modelo 'Export'.

    var doc = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Margin(30);

            page.Header().Stack(stack =>
            {
                // Substitua pela sua propriedade
                stack.Item().Text($"Report de Projeto: {projeto.NM_PROJETO}") 
                    .FontSize(20).Bold().AlignCenter();
                stack.Item()
                    .Text($"De: {projeto.DT_INICIO:dd/MM/yyyy} a {projeto.DT_TERMINO:dd/MM/yyyy}")
                    .AlignCenter(); 
                stack.Item().PaddingTop(10); 
            });
            
            page.Content().Column(column =>
            {
                // --- INÍCIO DA TABELA DE DADOS DO PROJETO ---
                // <<< CORRIGIDO: Border(1) e BorderColor(Colors.Grey.Medium) aplicados >>>
                column.Item().Border(1).BorderColor(Colors.Grey.Medium).Table(projectTable =>
                {
                    projectTable.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150); 
                        columns.RelativeColumn();    
                        columns.ConstantColumn(110); 
                        columns.RelativeColumn();    
                    });

                    // --- Linha 1: Nome do Projeto ---
                    // <<< CORRIGIDO: Estilos aplicados diretamente e 'Lighten4' e 'Lighten1' corrigidos >>>
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Nome do Projeto:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NM_PROJETO ?? ""); 

                    // --- Linha 2: Processo de Contratação / Pagamento ---
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Processo de Contratação:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NR_PROCESSO_SEI ?? "N/A"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft() // Era ValueCellStyle
                        .Text("Processo de Pagamento"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(projeto.NR_PROCESSO_SEI ?? "N/A"); 

                    // --- Linha 3: Data Início/Fim ---
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Data de início - fim do projeto:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text( "N/A"); 
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text("(documento SEI)");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text("N/A");
                                 // --- Linha 6: Fase ---
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(5).AlignLeft()
                        .Text("Fase:");
                    projectTable.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignLeft()
                        .Text(export.fase ?? "Execução");
                });
                // --- FIM DA NOVA TABELA ---

                column.Item().PaddingTop(20); 

                // --- INÍCIO DA TABELA DE ATIVIDADES (SEU CÓDIGO ORIGINAL) ---
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

                    foreach (var atividade in atividades)
                    {
                        // <<< CORRIGIDO: Estilos aplicados diretamente e 'Lighten2' corrigido >>>
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.titulo);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.situacao);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.categoria);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(atividade.descricao);
                    }
                });
                // --- FIM DA TABELA DE ATIVIDADES ---
            });

            // Footer original
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
