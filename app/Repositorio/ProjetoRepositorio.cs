using api.Projeto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;
using service;

namespace Repositorio;

public class ProjetoRepositorio : IProjetoRepositorio 
{
    public readonly AppDbContext _context;
    public ProjetoRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Projeto>> GetProjetoListItemsAsync(string unidade)
{
        List<Projeto> listItems = await _context.Projetos.Where(p => p.Unidade.Nome == unidade)
            .Include(e => e.AREA_DEMANDANTE)
            .Include(e => e.Unidade)
            .Include(e=> e.Esteira)
            .ToListAsync() ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);
        return listItems;
}

public async Task CreateProjeto (Projeto projeto)
{
    _context.Projetos.Add(projeto);
    _context.SaveChangesAsync();
}

   public async Task<Projeto> GetProjetoById(int id)
{
        Projeto? item = await _context.Projetos
        .Include(e => e.AREA_DEMANDANTE)
        .Include(e => e.Unidade)
        .FirstOrDefaultAsync(e => e.projetoId == id);
        return item;
}
public async Task CreateProjetoByTemplate(Projeto projeto)
{
    using (var transaction = await _context.Database.BeginTransactionAsync())
    {
        try
        {            
          
            _context.Projetos.Add(projeto);
            await _context.SaveChangesAsync();  
                     
            List<Template> templates = await _context.Templates
                .Where(c => c.NM_TEMPLATE == projeto.TEMPLATE)
                .ToListAsync();
           
            Console.WriteLine(templates.Count);
                if (templates.Count == 0)
                {
                    await transaction.CommitAsync();
                    return;
                }
            _context.Attach(projeto);
            Console.WriteLine("Projeto anexado ao contexto.");

            foreach (Template template in templates)
            {
                var etapa = new Etapa
                {
                    NM_ETAPA = template.NM_ETAPA,
                    NM_PROJETO = projeto, 
                    PERCENT_TOTAL_ETAPA = template.PERCENT_TOTAL,
                    DIAS_PREVISTOS = template.DIAS_PREVISTOS,
                    Order = template.ORDER
                };

               
                
                _context.Etapas.Add(etapa);
            }

            await _context.SaveChangesAsync(); 

            await transaction.CommitAsync(); 
            Console.WriteLine("Transação confirmada com sucesso.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); // Reverte em caso de erro
            Console.WriteLine($"Erro: {ex.Message}");
            throw new Exception("Erro ao salvar o projeto e as etapas: " + ex.Message, ex);
        }
    }
}

public async Task AnaliseProjeto(ReportDTO analise)
{
    Projeto projeto = _context.Projetos.FirstOrDefault(c => c.projetoId == analise.NM_PROJETO);
    
    Report novo_report = new Report
{
    NM_PROJETO = projeto,
    descricao = analise.descricao,
    fase = analise.fase,
    Data_criacao = DateTime.UtcNow,
    Data_fim = analise.data_fim,
};
    foreach (var atividade in analise.atividades)
    {
        var nova_atividade = new Atividade
        {
            descricao = atividade.descricao,
            categoria = atividade.categoria,
            data_termino = atividade.data_fim,
            situacao = atividade.situacao,
        };
        novo_report.Atividades.Add(nova_atividade);
    }

    _context.Analises.Add(novo_report);
    await _context.SaveChangesAsync();
    
}
public async Task<List<Report>> GetListReport(int projetoId)
{
    List<Report> report = await _context.Analises.Include(p=>p.Atividades).Where(p=>p.NM_PROJETO.projetoId==projetoId).ToListAsync();
    return report; 
}

    
}