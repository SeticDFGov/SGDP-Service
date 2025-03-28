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
            { "NM_ETAPA", item.NM_ETAPA },
            { "DT_INICIO_PREVISTO", item.DT_INICIO_PREVISTO },
            { "DT_TERMINO_PREVISTO", item.DT_TERMINO_PREVISTO },
            { "DT_INICIO_REAL", item.DT_INICIO_REAL },
            { "DT_TERMINO_REAL", item.DT_TERMINO_REAL },
            { "SITUACAO", item.SITUACAO },
            { "RESPONSAVEL_ETAPA", item.RESPONSAVEL_ETAPA },
            { "ANALISE", item.ANALISE },
            { "PERCENT_TOTAL_ETAPA", item.PERCENT_TOTAL_ETAPA },
            { "PERCENT_EXEC_ETAPA", item.PERCENT_EXEC_ETAPA },
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


    
}