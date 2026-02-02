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
       List<Projeto> listItems = await _context.Projetos
           .Where(p => p.Unidade.Nome == unidade)
           .Include(p => p.AREA_DEMANDANTE)
           .Include(p => p.Unidade)
           .Include(p => p.Esteira)
           .Include(p => p.Etapas) // Eager loading das etapas para evitar N+1
           .AsSplitQuery() // Otimiza queries com múltiplos Includes
           .ToListAsync();
        return listItems;
} 
   public async Task<Projeto> GetProjetoById(int id)
{
        Projeto? item = await _context.Projetos
        .Include(e => e.AREA_DEMANDANTE)
        .Include(e => e.Unidade)
        .FirstOrDefaultAsync(e => e.projetoId == id);
        return item;
}
}