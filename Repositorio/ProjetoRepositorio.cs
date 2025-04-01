
using api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;

namespace Repositorio;

public class ProjetoRepositorio 
{
    public readonly AppDbContext _context;
    public ProjetoRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Dictionary<string, object>>> GetProjetoListItemsAsync()
{
    try
    {
        var listItems = await _context.Projetos.ToListAsync();

        if (listItems == null || !listItems.Any())
        {
            return new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "message", "Projetos não encontradas." } }
            };
        }

        var result = listItems.Select(item => new Dictionary<string, object>
        {
            { "ID", item.projetoId },
            { "NM_PROJETO", item.NM_PROJETO },
            {"GERENTE_PROJETO", item.GERENTE_PROJETO},
            {"SITUACAO", item.SITUACAO},
            {"UNIDADE", item.UNIDADE},
            {"NR_PROCESSO_SEI", item.NR_PROCESSO_SEI},
            {"NM_ARE_DEMANDANTE", item.NM_AREA_DEMANDANTE},
            {"ANO", item.ANO},
            {"TEMPLATE", item.TEMPLATE},
            {"PROFISCOII", item.PROFISCOII},
            {"PDTIC24/27", item.PDTIC2427},
            {"PTD24/27", item.PTD2427}
        }).ToList();

        return result;
    }
    catch (Exception ex)
    {
        return new List<Dictionary<string, object>> {
            new Dictionary<string, object> { {  "details", ex.Message } }
        };
    }
}

public void CreateProjeto (Projeto projeto)
{
    _context.Projetos.Add(projeto);
    _context.SaveChangesAsync();
}

   public async Task<Dictionary<string, object>> GetProjetoById(int id)
{
    try
    {
         Projeto item = await _context.Projetos.FirstOrDefaultAsync(e => e.projetoId == id);
        var result = new Dictionary<string, object>
        {
            { "ID", item.projetoId },
            { "NM_PROJETO", item.NM_PROJETO },
            {"GERENTE_PROJETO", item.GERENTE_PROJETO},
            {"SITUACAO", item.SITUACAO},
            {"UNIDADE", item.UNIDADE},
            {"NR_PROCESSO_SEI", item.NR_PROCESSO_SEI},
            {"NM_ARE_DEMANDANTE", item.NM_AREA_DEMANDANTE},
            {"ANO", item.ANO},
            {"TEMPLATE", item.TEMPLATE},
            {"PROFISCOII", item.PROFISCOII},
            {"PDTIC24/27", item.PDTIC2427},
            {"PTD24/27", item.PTD2427}
        };
 

        return result;
    }
    catch (Exception ex)
    {
        return 
            new Dictionary<string, object> { {  "details", ex.Message } };
    }
}
public async Task CreateProjetoByTemplate(Projeto projeto)
{
    using (var transaction = await _context.Database.BeginTransactionAsync())
    {
        try
        {
            Console.WriteLine("Iniciando transação...");
            
          
            _context.Projetos.Add(projeto);
            await _context.SaveChangesAsync();  
            Console.WriteLine($"Projeto adicionado com sucesso. Projeto ID: {projeto.projetoId}");

          
            List<Template> templates = await _context.Templates
                .Where(c => c.NM_TEMPLATE == projeto.TEMPLATE)
                .ToListAsync();
            Console.WriteLine($"Templates encontrados: {templates.Count}");

        
            if (templates.Count == 0)
            {
                throw new Exception("Nenhum template encontrado para o projeto.");
            }

            // Garantir que o projeto está rastreado pelo EF antes de criar as etapas
            _context.Attach(projeto);
            Console.WriteLine("Projeto anexado ao contexto.");

            // Criar e adicionar as etapas
            foreach (Template template in templates)
            {
                var etapa = new Etapa
                {
                    NM_ETAPA = template.NM_ETAPA,
                    NM_PROJETO = projeto,  // Garantindo a relação correta
                    PERCENT_TOTAL_ETAPA = template.PERCENT_TOTAL
                };

                // Debug: Verificar o conteúdo de cada etapa
                Console.WriteLine($"Adicionando etapa: {etapa.NM_ETAPA} (Percentual: {etapa.PERCENT_TOTAL_ETAPA})");
                
                _context.Etapas.Add(etapa);
            }

            // Salva as etapas no banco
            await _context.SaveChangesAsync(); 
            Console.WriteLine("Etapas salvas com sucesso.");

            // Confirma a transação
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

public async Task AnaliseProjeto(ProjetoAnaliseDTO analise)
{
    Projeto projeto = _context.Projetos.FirstOrDefault(c => c.projetoId == analise.NM_PROJETO);
    
    ProjetoAnalise nova_analise = new ProjetoAnalise
{
    NM_PROJETO = projeto,
    ANALISE = analise.ANALISE,
    ENTRAVE = analise.ENTRAVE,
};

    
    

    _context.Analises.Add(nova_analise);
    _context.SaveChangesAsync();
    
}

public async Task<ProjetoAnalise> GetLastAnaliseProjeto(int projetoid)
{
    ProjetoAnalise projeto = _context.Analises.Where(c => c.NM_PROJETO.projetoId == projetoid).ToList().LastOrDefault();
    return projeto ; 
}

    
}