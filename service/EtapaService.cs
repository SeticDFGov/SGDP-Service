using api;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio;

namespace service;

public class EtapaService
{
    public readonly EtapaRepositorio _etapaRepositorio;
    public readonly AppDbContext _context;
    public EtapaService( EtapaRepositorio etapaRepositorio, AppDbContext context)
    {
        _context = context;
        _etapaRepositorio = etapaRepositorio;
    }

    public async Task<PercentualEtapaDTO> GetPercentEtapas (int projetoid)
    {
        var etapas = await _context.Etapas
    .Where(e => e.NM_PROJETO.projetoId == projetoid)
    .ToListAsync(); // Carrega os dados na memória

    decimal? somaPercentExecReal = etapas.Sum(e => e.PERCENT_EXEC_REAL);
    decimal? somaPercentExecPlan = etapas.Sum(e => e.PERCENT_PLANEJADO);

        
        PercentualEtapaDTO response = new PercentualEtapaDTO {
            PERCENT_PLANEJADO = somaPercentExecPlan,
            PERCENT_EXECUTADO = somaPercentExecReal,
        };

        return response ;
    }


}
