using api.Demanda;
using demanda_service.Migrations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Iso_Ceiling;
using Models;
using Repositorio.Interface;
using service;
using Sprache;

namespace Repositorio;

public class DemandaRepositorio:IDemandaRepositorio 
{
    public readonly AppDbContext _context;
    public DemandaRepositorio(AppDbContext context)
    {
        _context = context;
    }

 public async Task<List<Demanda>> GetDemandasListItemsAsync()
{
    try
    {
        
        var listItems = await _context.Demandas
            .Include(d => d.CATEGORIA)   
            .Include(d => d.NM_AREA_DEMANDANTE)  
            .ToListAsync() ?? throw new ApiException(ErrorCode.DemandasNaoEncontradas);

        var result = listItems.Select(item => new Demanda
        {
            DemandaId =item.DemandaId ,
            NM_DEMANDA= item.NM_DEMANDA ,
            DT_ABERTURA = item.DT_ABERTURA.HasValue ? item.DT_ABERTURA.Value.ToLocalTime() : (DateTime?)null,
            DT_CONCLUSAO = item.DT_CONCLUSAO.HasValue ? item.DT_CONCLUSAO.Value.ToLocalTime() : (DateTime?)null,
            DT_SOLICITACAO = item.DT_SOLICITACAO.HasValue ? item.DT_SOLICITACAO.Value.ToLocalTime() : (DateTime?)null,
            CATEGORIA = item.CATEGORIA,
            NM_AREA_DEMANDANTE = item.NM_AREA_DEMANDANTE,
            NM_PO_DEMANDANTE = item.NM_PO_DEMANDANTE,
            NM_PO_SUBTDCR = item.NM_PO_SUBTDCR,
            NR_PROCESSO_SEI = item.NR_PROCESSO_SEI,
            PATROCINADOR = item.PATROCINADOR,
            PERIODICO = item.PERIODICO,
            PERIODICIDADE = item.PERIODICIDADE,
            STATUS = item.STATUS,
            UNIDADE = item.UNIDADE
        }).ToList();

        return result;
    }
    catch (Exception)
    {
        throw new ApiException(ErrorCode.ErroAoBuscarDemandas);
    }
}

public async Task CreateDemanda(DemandaDTO demanda)
{
        try
        {
            try
            {
                if (demanda.DT_SOLICITACAO.HasValue)
                    demanda.DT_SOLICITACAO = demanda.DT_SOLICITACAO.Value.ToUniversalTime();

                if (demanda.DT_ABERTURA.HasValue)
                    demanda.DT_ABERTURA = demanda.DT_ABERTURA.Value.ToUniversalTime();

                if (demanda.DT_CONCLUSAO.HasValue)
                    demanda.DT_CONCLUSAO = demanda.DT_CONCLUSAO.Value.ToUniversalTime();
            }
            catch (Exception)
            {
                throw new ApiException(ErrorCode.DataInvalida);
            }
           

            var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Nome == demanda.CATEGORIA) ?? throw new ApiException(ErrorCode.CategoriaNaoEncontrada);

            var demandante = await _context.AreaDemandantes
                .FirstOrDefaultAsync(e => e.NM_DEMANDANTE == demanda.NM_AREA_DEMANDANTE) ?? throw new ApiException(ErrorCode.AreasDemandantesNaoEncontradas);


            Demanda demandaAdd = new Demanda
            {
                NM_DEMANDA = demanda.NM_DEMANDA,
                CATEGORIA = categoria,
                NM_AREA_DEMANDANTE = demandante,
                DT_ABERTURA = demanda.DT_ABERTURA,
                DT_CONCLUSAO = demanda.DT_CONCLUSAO,
                DT_SOLICITACAO = demanda.DT_SOLICITACAO,
                UNIDADE = demanda.UNIDADE,
                PERIODICIDADE = demanda.PERIODICIDADE,
                PERIODICO = demanda.PERIODICO,
                NM_PO_DEMANDANTE = demanda.NM_PO_DEMANDANTE,
                NM_PO_SUBTDCR = demanda.NM_PO_SUBTDCR,
                STATUS = demanda.STATUS,
                NR_PROCESSO_SEI = demanda.NR_PROCESSO_SEI,
                PATROCINADOR = demanda.PATROCINADOR

            };

            _context.Demandas.Add(demandaAdd);
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoCriarDemanda);
        }
}


public async Task DeleteDemanda (int id)
{   
    var item = _context.Demandas.FirstOrDefault(e => e.DemandaId == id) ?? throw new ApiException(ErrorCode.DemandasNaoEncontradas);

    _context.Demandas.Remove(item);
    await _context.SaveChangesAsync();
}
public async Task EditDemanda(DemandaDTO demanda)
{
    var demandaExistente = await _context.Demandas.FirstOrDefaultAsync(e => e.DemandaId == demanda.DemandaId) ?? throw new ApiException(ErrorCode.DemandasNaoEncontradas);
    var categoria = await _context.Categorias.FirstOrDefaultAsync(e => e.Nome == demanda.NM_DEMANDA) ?? throw new ApiException(ErrorCode.CategoriaNaoEncontrada);
    var demandante = await _context.AreaDemandantes.FirstOrDefaultAsync(e => e.NM_DEMANDANTE == demanda.NM_AREA_DEMANDANTE) ?? throw new ApiException(ErrorCode.AreasDemandantesNaoEncontradas);
    
    try
    {
        _context.Demandas.Update(demandaExistente);
        await _context.SaveChangesAsync(); 
    }
    catch (DbUpdateException)
    {
        throw new ApiException(ErrorCode.ErroAoEditarDemanda);
    }
}

public async Task<Demanda?> GetDemandaById(int id)
{
    Demanda? demanda = await _context.Demandas.
    Include(e => e.CATEGORIA)
    .Include(e => e.NM_AREA_DEMANDANTE)
    .FirstOrDefaultAsync(e => e.DemandaId == id);
    return demanda;
}

public async Task<Dictionary<string, int>> GetQuantidadeTipo()
{
    return await _context.Demandas
        .GroupBy(d => d.CATEGORIA.Nome)
        .Select(g => new { Categoria = g.Key, Quantidade = g.Count() })
        .ToDictionaryAsync(g => g.Categoria, g => g.Quantidade);
}

    
}