using api;
using api.Etapa;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;
using service;
using TimeZoneConverter; // Instale via NuGet: TimeZoneConverter
namespace Repositorio;

public class EtapaRepositorio : IEtapaRepositorio
{
    public readonly AppDbContext _context;
    public EtapaRepositorio(AppDbContext context)
    {
        _context = context;
    }

   public async Task<List<EtapaModel>> GetEtapaListItemsAsync(int projetoId)
{

   var etapas = _context.Etapas
        .Where(e => e.NM_PROJETO.projetoId == projetoId)
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
            }
        )
        .ToList();
    
    return etapas ?? throw new ApiException(ErrorCode.EtapasNaoEncontradas);
    
    
}


public async Task CreateEtapa(EtapaDTO etapa)
{
    Projeto projetocadastro = await _context.Projetos
        .FirstOrDefaultAsync(e => e.projetoId == etapa.NM_PROJETO) ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);


        try
        {
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
        catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoCriarEtapa);
        }
    }
     


public async Task EditEtapa(AfericaoEtapaDTO etapa, int etapaid)
    {
        Etapa etapa_edit = await _context.Etapas.FirstOrDefaultAsync(e => e.EtapaProjetoId == etapaid) ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        TimeZoneInfo brasilia = TZConvert.GetTimeZoneInfo("E. South America Standard Time");
        try
        {
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

        }catch(Exception)
        {
            throw new ApiException(ErrorCode.ErroAoEditarEtapa);
        }
}

public async Task<Etapa> GetById(int id)
{
   Etapa etapa =  _context.Etapas.FirstOrDefault(e => e.EtapaProjetoId == id) ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);
   
   return etapa;
}



}