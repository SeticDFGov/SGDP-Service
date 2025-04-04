using api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;

namespace Repositorio;

public class EtapaRepositorio 
{
    public readonly AppDbContext _context;
    public EtapaRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<Etapa>> GetEtapaListItemsAsync(int id)
{
   
        List<Etapa> listItems =  _context.Etapas.Where(d => d.NM_PROJETO.projetoId == id).ToList();
        return listItems;
    
    
}

public void CreateEtapa (Etapa etapa, int projeto)
{
    Projeto projetocadastro = _context.Projetos.FirstOrDefault(e => e.projetoId == projeto);

    etapa.NM_PROJETO = projetocadastro;


    _context.Etapas.Add(etapa);
    _context.SaveChangesAsync();
}

public async Task EditEtapa (AfericaoEtapaDTO etapa, int etapaid)
{
    Etapa etapa_edit = await _context.Etapas.FirstOrDefaultAsync(e => e.EtapaProjetoId == etapaid);

    if (etapa_edit == null)
    {
        throw new KeyNotFoundException("Etapa não encontrada.");
    }

    etapa_edit.DT_INICIO_REAL = etapa.DT_INICIO_REAL.Value.ToUniversalTime();
    etapa_edit.DT_TERMINO_REAL = etapa.DT_TERMINO_REAL.Value.ToUniversalTime();
    etapa_edit.ANALISE = etapa.ANALISE;
    etapa_edit.PERCENT_EXEC_ETAPA = etapa.PERCENT_EXEC_ETAPA;

   await  _context.SaveChangesAsync();
}

public async Task<Etapa> GetById(int id)
{
   Etapa etapa =  _context.Etapas.FirstOrDefault(e => e.EtapaProjetoId == id);
   
   return etapa;
}
    
}