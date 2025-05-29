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

   public async Task<List<EtapaModel>> GetEtapaListItemsAsync(int id)
{

   var etapas = _context.Etapas
        .Where(e => e.NM_PROJETO.projetoId == id)
        .Join(
            _context.Templates,
            etapa => etapa.NM_ETAPA,
            
            template => template.NM_ETAPA,
            (etapa, template) => new EtapaModel
            {
                EtapaProjetoId = etapa.EtapaProjetoId,
                NM_ETAPA = etapa.NM_ETAPA,
                RESPONSAVEL_ETAPA = etapa.RESPONSAVEL_ETAPA,
                ANALISE = etapa.ANALISE,
                PERCENT_TOTAL_ETAPA = etapa.PERCENT_TOTAL_ETAPA,
                PERCENT_EXEC_ETAPA = etapa.PERCENT_EXEC_ETAPA,
                DT_INICIO_PREVISTO = etapa.DT_INICIO_PREVISTO,
                DT_TERMINO_PREVISTO = etapa.DT_TERMINO_PREVISTO,
                DT_INICIO_REAL = etapa.DT_INICIO_REAL,
                DT_TERMINO_REAL = etapa.DT_TERMINO_REAL,
                Order = template.ORDER
                // os campos calculados serão populados automaticamente pelas propriedades
            }
        )
        .ToList();

    return etapas;
    
    
}


public async Task CreateEtapa(EtapaDTO etapa)
{
    Projeto projetocadastro = await _context.Projetos
        .FirstOrDefaultAsync(e => e.projetoId == etapa.NM_PROJETO);

    if (projetocadastro == null)
    {
        throw new KeyNotFoundException("Projeto não encontrado.");
    }

    TimeZoneInfo brasilia = TZConvert.GetTimeZoneInfo("E. South America Standard Time");

    Etapa etapaCadastro = new Etapa
    {
        NM_ETAPA = etapa.NM_ETAPA,
        DT_INICIO_PREVISTO = etapa.DT_INICIO_PREVISTO.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(etapa.DT_INICIO_PREVISTO.Value, DateTimeKind.Unspecified), brasilia)
            : (DateTime?)null,
        DT_TERMINO_PREVISTO = etapa.DT_TERMINO_PREVISTO.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(etapa.DT_TERMINO_PREVISTO.Value, DateTimeKind.Unspecified), brasilia)
            : (DateTime?)null,
        PERCENT_TOTAL_ETAPA = etapa.PERCENT_TOTAL_ETAPA,
        RESPONSAVEL_ETAPA = etapa.RESPONSAVEL_ETAPA,
        NM_PROJETO = projetocadastro
    };

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