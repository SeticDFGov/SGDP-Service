using api.Common;
using api.Demanda;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio.Interface;
using service;
using service.Interface;

namespace demanda_service.service;

public class DemandaService : IDemandaService
{
    private readonly IDemandaRepositorio _demandaRepositorio;
    private readonly AppDbContext _context;

    public DemandaService(IDemandaRepositorio demandaRepositorio, AppDbContext context)
    {
        _demandaRepositorio = demandaRepositorio;
        _context = context;
    }

    public async Task<List<Demanda>> GetDemandasAsync()
    {
        return await _demandaRepositorio.GetDemandasAsync();
    }

    public async Task<Demanda> GetDemandaByIdAsync(int id)
    {
        return await _demandaRepositorio.GetDemandaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);
    }

    public async Task CreateDemandaAsync(DemandaCreateDTO dto)
    {
        var esteira = await _context.Esteiras.FindAsync(dto.EsteiraId)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        var demandante = await _context.AreaDemandantes.FindAsync(dto.NM_AREA_DEMANDANTE)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        var demanda = new Demanda
        {
            NM_PROJETO = dto.NM_PROJETO,
            NR_PROCESSO_SEI = dto.NR_PROCESSO_SEI,
            AREA_DEMANDANTE = demandante,
            Esteira = esteira
        };

        await _demandaRepositorio.AddAsync(demanda);
    }

    public IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome)
    {
        var query = _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .AsSplitQuery()
            .AsQueryable();

        return perfil switch
        {
            "admin" => query,
            "gestor" => query,
            "centralit" => query,
            "parceiro" => query.Where(d => d.AREA_DEMANDANTE != null && d.AREA_DEMANDANTE.NM_DEMANDANTE == unidadeNome),
            _ => query.Where(d => d.AREA_DEMANDANTE != null && d.AREA_DEMANDANTE.NM_DEMANDANTE == unidadeNome)
        };
    }
}
