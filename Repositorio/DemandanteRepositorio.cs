using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;

namespace Repositorio;

public class DemandanteRepositorio 
{
    public readonly AppDbContext _context;
    public DemandanteRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Dictionary<string, object>>> GetDemandanteListItemsAsync()
{
    try
    {
        var listItems = await _context.AreaDemandantes.ToListAsync();

        if (listItems == null || !listItems.Any())
        {
            return new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "message", "Demandantes não encontradas." } }
            };
        }

        var result = listItems.Select(item => new Dictionary<string, object>
        {
            { "ID", item.AreaDemandanteID },
            { "NM_DEMANDANTE", item.NM_DEMANDANTE },
            {"NM_SIGLA", item.NM_SIGLA}
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

public void CreateDemandante (AreaDemandante demandante)
{
    _context.AreaDemandantes.Add(demandante);
    _context.SaveChangesAsync();
}

public async Task<IResult> DeleteDemandante (int id)
{   
    var item = _context.AreaDemandantes.FirstOrDefault(e => e.AreaDemandanteID == id);

    if(item == null)  return Results.NotFound();

    _context.AreaDemandantes.Remove(item);
    await _context.SaveChangesAsync();

    return Results.Ok();
}

    
}