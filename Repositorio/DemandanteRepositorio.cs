using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;
using service;

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
            var listItems = await _context.AreaDemandantes.ToListAsync() ?? throw new ApiException(ErrorCode.AreasDemandantesNaoEncontradas);

            var result = listItems.Select(item => new AreaDemandante
            {
                AreaDemandanteID = item.AreaDemandanteID,
                NM_DEMANDANTE = item.NM_DEMANDANTE,
                NM_SIGLA = item.NM_SIGLA
            }).ToList();


            return result;
        }
        catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoBuscarAreasDemandantes);
        }
}

public async Task CreateDemandanteAsync (AreaDemandante demandante)
{
    try
    {
        _context.AreaDemandantes.Add(demandante);
        await _context.SaveChangesAsync();
    }catch (Exception)
    {
        throw new ApiException(ErrorCode.ErroAoCriarAreaDemandante);
    }
    
}

public async Task DeleteDemandanteAsync (int id)
{   
    var item = _context.AreaDemandantes.FirstOrDefault(e => e.AreaDemandanteID == id) ?? throw new ApiException(ErrorCode.AreasDemandantesNaoEncontradas);
    _context.AreaDemandantes.Remove(item);
    await _context.SaveChangesAsync();

}

    
}