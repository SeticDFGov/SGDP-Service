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

   public async Task<List<Dictionary<string, object>>> GetEtapaListItemsAsync(int id)
{
    try
    {
        var listItems =  _context.Etapas.Where(d => d.NM_PROJETO.projetoId == id);

        if (listItems == null || !listItems.Any())
        {
            return new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "message", "Projetos não encontradas." } }
            };
        }

        var result = listItems.Select(item => new Dictionary<string, object>
        {
      { "ID", item.EtapaProjetoId },
    { "NM_PROJETO", item.NM_PROJETO },
    { "NM_ETAPA", item.NM_ETAPA ?? "" },
    { "DT_INICIO_PREVISTO", item.DT_INICIO_PREVISTO.Value.ToString("dd/MM/yyyy") ?? DateTime.MinValue.ToString("dd/MM/yyyy") },
    { "DT_TERMINO_PREVISTO", item.DT_TERMINO_PREVISTO.Value.ToString("dd/MM/yyyy") ?? DateTime.MinValue.ToString("dd/MM/yyyy") },
    { "DT_INICIO_REAL", item.DT_INICIO_REAL.Value.ToString("dd/MM/yyyy") ?? DateTime.MinValue.ToString("dd/MM/yyyy") },
    { "DT_TERMINO_REAL", item.DT_TERMINO_REAL.Value.ToString("dd/MM/yyyy") ?? DateTime.MinValue.ToString("dd/MM/yyyy") },
    { "SITUACAO", item.SITUACAO ?? "" },
    { "RESPONSAVEL_ETAPA", item.RESPONSAVEL_ETAPA ?? "" },
    { "PERCENT_TOTAL_ETAPA", item.PERCENT_TOTAL_ETAPA ?? 0 }, // Para tipos numéricos, substitua por 0 se for null
    { "PERCENT_EXEC_ETAPA", item.PERCENT_EXEC_ETAPA ?? 0 },
    { "PERCENT_EXEC_REAL", item.PERCENT_EXEC_REAL },
    { "PERCENT_PLANEJADO", item.PERCENT_PLANEJADO } 
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