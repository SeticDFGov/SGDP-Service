using api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using TimeZoneConverter; // Instale via NuGet: TimeZoneConverter
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

public async Task CreateEtapa (EtapaDTO etapa)
{
    Projeto projetocadastro = await _context.Projetos.FirstOrDefaultAsync(e => e.projetoId == etapa.NM_PROJETO);

    Etapa etapaCadastro = new Etapa();
    etapaCadastro.NM_ETAPA = etapa.NM_ETAPA;
    etapaCadastro.DT_INICIO_PREVISTO = etapa.DT_INICIO_PREVISTO.Value.ToUniversalTime();
    etapaCadastro.DT_TERMINO_PREVISTO = etapa.DT_TERMINO_PREVISTO.Value.ToUniversalTime();
    etapaCadastro.PERCENT_TOTAL_ETAPA = etapa.PERCENT_TOTAL_ETAPA;
    etapaCadastro.RESPONSAVEL_ETAPA = etapa.RESPONSAVEL_ETAPA;
    etapaCadastro.NM_PROJETO = projetocadastro;
    


    _context.Etapas.Add(etapaCadastro);
    await _context.SaveChangesAsync();
}


public async Task EditEtapa (AfericaoEtapaDTO etapa, int etapaid)
{
    Etapa etapa_edit = await _context.Etapas.FirstOrDefaultAsync(e => e.EtapaProjetoId == etapaid);

    if (etapa_edit == null)
    {
        throw new KeyNotFoundException("Etapa não encontrada.");
    }

    TimeZoneInfo brasilia = TZConvert.GetTimeZoneInfo("E. South America Standard Time");

    if (etapa.DT_INICIO_REAL.HasValue)
    {
        DateTime dtInicio = DateTime.SpecifyKind(etapa.DT_INICIO_REAL.Value, DateTimeKind.Unspecified);
        etapa_edit.DT_INICIO_REAL = TimeZoneInfo.ConvertTimeToUtc(dtInicio, brasilia);
    }

    if (etapa.DT_TERMINO_REAL.HasValue)
    {
        DateTime dtTermino = DateTime.SpecifyKind(etapa.DT_TERMINO_REAL.Value, DateTimeKind.Unspecified);
        etapa_edit.DT_TERMINO_REAL = TimeZoneInfo.ConvertTimeToUtc(dtTermino, brasilia);
    }

    etapa_edit.ANALISE = etapa.ANALISE;
    etapa_edit.PERCENT_EXEC_ETAPA = etapa.PERCENT_EXEC_ETAPA;

    await _context.SaveChangesAsync();
}


public async Task<Etapa> GetById(int id)
{
   Etapa etapa =  _context.Etapas.FirstOrDefault(e => e.EtapaProjetoId == id);
   
   return etapa;
}



}