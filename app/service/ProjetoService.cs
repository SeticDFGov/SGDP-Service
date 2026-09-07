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

    public async Task CreateDemandaAsync(DemandaCreateDTO dto, string userEmail)
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
            Esteira = esteira,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = userEmail
        };

        await _demandaRepositorio.AddAsync(demanda);
    }

    public async Task UpdateDemandaAsync(int id, DemandaUpdateDTO dto, string userEmail)
    {
        var demanda = await _context.Demandas.FindAsync(id)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        var esteira = await _context.Esteiras.FindAsync(dto.EsteiraId)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        var demandante = await _context.AreaDemandantes.FindAsync(dto.NM_AREA_DEMANDANTE)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        demanda.NM_PROJETO = dto.NM_PROJETO;
        demanda.NR_PROCESSO_SEI = dto.NR_PROCESSO_SEI;
        demanda.AREA_DEMANDANTE = demandante;
        demanda.Esteira = esteira;

        await _context.SaveChangesAsync();
    }

    public IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome)
    {
        return _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .AsSplitQuery()
            .AsQueryable();
    }
}
