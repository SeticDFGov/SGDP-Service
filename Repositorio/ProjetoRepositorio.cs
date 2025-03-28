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
    
}