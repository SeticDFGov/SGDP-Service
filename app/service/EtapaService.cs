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

    public async Task<Etapa> GetByIdAsync(int id)
    {
        return await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);
    }

    public async Task CreateEntregavelAsync(EntregavelCreateDTO dto, string userEmail)
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
            // JsonConverter já faz a conversão de Brasília para UTC automaticamente
            DT_INICIO = dto.DT_INICIO,
            DT_FIM = dto.DT_FIM,
            Descricao = dto.Descricao,
            PERCENT_EXECUTADO = 0,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = userEmail
        };

        _etapaRepositorio.Add(etapa);
        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task UpdateEntregavelAsync(int id, EntregavelUpdateDTO dto, string userEmail)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        var areaExecutora = await _etapaRepositorio.GetAreaExecutoraByIdAsync(dto.AreaExecutoraId)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        etapa.NM_ETAPA = dto.NM_ETAPA;
        etapa.Responsavel = areaExecutora;
        etapa.TIPO_ENTREGA = dto.TIPO_ENTREGA;
        // JsonConverter já faz a conversão de Brasília para UTC automaticamente
        etapa.DT_INICIO = dto.DT_INICIO;
        etapa.DT_FIM = dto.DT_FIM;
        etapa.Descricao = dto.Descricao;
        etapa.AlteradoEm = DateTime.UtcNow;
        etapa.AlteradoPor = userEmail;

        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task UpdateEntregavelBasicoAsync(int id, EntregavelUpdateBasicoDTO dto, string userEmail)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        etapa.NM_ETAPA = dto.NM_ETAPA;
        etapa.TIPO_ENTREGA = dto.TIPO_ENTREGA;
        etapa.AlteradoEm = DateTime.UtcNow;
        etapa.AlteradoPor = userEmail;

        await _etapaRepositorio.SaveChangesAsync();
    }

    public async Task UpdatePercentualAsync(int id, EntregavelUpdatePercentDTO dto, string userEmail)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        etapa.PERCENT_EXECUTADO = Math.Clamp(dto.PERCENT_EXECUTADO, 0, 100);
        if (dto.Descricao != null)
            etapa.Descricao = dto.Descricao;
        etapa.AlteradoEm = DateTime.UtcNow;
        etapa.AlteradoPor = userEmail;

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
