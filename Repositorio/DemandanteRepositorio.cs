using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;

namespace Repositorio;

public class DemandanteRepositorio : IDemandanteRepositorio
{
    public readonly AppDbContext _context;
    public DemandanteRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<AreaDemandante>> GetDemandanteListItemsAsync()
{
    try
    {
        var listItems = await _context.AreaDemandantes.ToListAsync();

        if (listItems == null || !listItems.Any())
        {
            return new List<AreaDemandante> {
                new AreaDemandante { 
                    AreaDemandanteID = 0, 
                    NM_DEMANDANTE = "Demandantes não encontrados.", 
                    NM_SIGLA = "N/A"
                 }
            };
        }

        var result = listItems.Select(item => new AreaDemandante
        {
            AreaDemandanteID = item.AreaDemandanteID,
            NM_DEMANDANTE = item.NM_DEMANDANTE,
            NM_SIGLA = item.NM_SIGLA
        }).ToList();
        

        return result;
    }
    catch (Exception ex)
    {
        return new List<AreaDemandante> {
            new AreaDemandante { }
        };
    }
}

public async Task CreateDemandante (AreaDemandante demandante)
{
    _context.AreaDemandantes.Add(demandante);
    _context.SaveChangesAsync();
}

public async Task DeleteDemandante (int id)
{   
    var item = _context.AreaDemandantes.FirstOrDefault(e => e.AreaDemandanteID == id) ?? throw new Exception("Demandante não encontrado.");
        _context.AreaDemandantes.Remove(item);
    await _context.SaveChangesAsync();

}

    
}