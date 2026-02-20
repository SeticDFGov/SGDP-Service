using api.Entregavel;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio.Interface;
using service.Interface;

namespace service;

public class EtapaService : IEtapaService
{
    private readonly IEtapaRepositorio _etapaRepositorio;
    private readonly AppDbContext _context;

    public EtapaService(IEtapaRepositorio etapaRepositorio, AppDbContext context)
    {
        _etapaRepositorio = etapaRepositorio;
        _context = context;
    }

    public async Task<List<Etapa>> GetEntregaveisByDemandaAsync(int demandaId)
    {
        return await _etapaRepositorio.GetEntregaveisByDemandaIdAsync(demandaId);
    }

    public async Task<List<Etapa>> GetEntregaveisByCentralITAsync(string areaExecutoraNome)
    {
        return await _etapaRepositorio.GetEntregaveisByAreaExecutoraAsync(areaExecutoraNome);
    }

    public async Task<Etapa> GetByIdAsync(int id)
    {
        return await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);
    }

    public async Task CreateEntregavelAsync(EntregavelCreateDTO dto)
    {
        var demanda = await _etapaRepositorio.GetDemandaByIdAsync(dto.DemandaId)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        var areaExecutora = await _etapaRepositorio.GetAreaExecutoraByIdAsync(dto.AreaExecutoraId)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        var etapa = new Etapa
        {
            NM_PROJETO = demanda,
            NM_ETAPA = dto.NM_ETAPA,
            Responsavel = areaExecutora,
            TIPO_ENTREGA = dto.TIPO_ENTREGA,
            DT_INICIO = DateTimeHelper.ToUtc(dto.DT_INICIO),
            DT_FIM = DateTimeHelper.ToUtc(dto.DT_FIM),
            Descricao = dto.Descricao,
            PERCENT_EXECUTADO = 0
        };

        _etapaRepositorio.Add(etapa);
        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task UpdateEntregavelAsync(int id, EntregavelUpdateDTO dto)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        var areaExecutora = await _etapaRepositorio.GetAreaExecutoraByIdAsync(dto.AreaExecutoraId)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        etapa.NM_ETAPA = dto.NM_ETAPA;
        etapa.Responsavel = areaExecutora;
        etapa.TIPO_ENTREGA = dto.TIPO_ENTREGA;
        etapa.DT_INICIO = DateTimeHelper.ToUtc(dto.DT_INICIO);
        etapa.DT_FIM = DateTimeHelper.ToUtc(dto.DT_FIM);
        etapa.Descricao = dto.Descricao;

        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task UpdatePercentualAsync(int id, EntregavelUpdatePercentDTO dto)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        etapa.PERCENT_EXECUTADO = Math.Clamp(dto.PERCENT_EXECUTADO, 0, 100);
        if (dto.Descricao != null)
            etapa.Descricao = dto.Descricao;

        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task DeleteEntregavelAsync(int id)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        _etapaRepositorio.Remove(etapa);
        await _etapaRepositorio.SaveChangesAsync();
    }
}
