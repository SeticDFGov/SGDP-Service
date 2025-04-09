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
    .ToListAsync(); 

    decimal? somaPercentExecReal = etapas.Sum(e => e.PERCENT_EXEC_REAL ?? 0);
    decimal? somaPercentExecPlan = etapas.Sum(e => e.PERCENT_PLANEJADO);

        
        PercentualEtapaDTO response = new PercentualEtapaDTO {
            PERCENT_PLANEJADO = somaPercentExecPlan,
            PERCENT_EXECUTADO = somaPercentExecReal,
        };

        return response ;
    }
    
 public async Task<SituacaoProjetoDTO> GetSituacao()
{
    var projetos = await _context.Projetos.ToListAsync();

    int concluidos = 0;
    int atrasados = 0;
    int emAndamento = 0;
    int naoIniciados = 0;

    foreach (var projeto in projetos)
    {
        var etapas = await _context.Etapas
            .Where(e => e.NM_PROJETO.projetoId == projeto.projetoId)
            .ToListAsync();

        if (etapas.Count == 0)
        {
            naoIniciados++;
            continue;
        }

        if (etapas.Any(e => e.SITUACAO == "Atrasado"))
        {
            atrasados++;
        }
        else if (etapas.Any(e => e.SITUACAO == "Em Andamento"))
        {
            emAndamento++;
        }
        else if (etapas.All(e => e.SITUACAO == "Concluido"))
        {
            concluidos++;
        }
        else
        {
            naoIniciados++;
        }
    }

    return new SituacaoProjetoDTO
    {
        Atrasado = atrasados,
        EmAndamento = emAndamento,
        Concluido = concluidos,
        NaoIniciado = naoIniciados
    };
}



}
