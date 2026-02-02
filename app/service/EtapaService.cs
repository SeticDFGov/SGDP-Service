using api.Etapa;
using api.Projeto;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio;
using Repositorio.Interface;
using service.Interface;

namespace service;

/// <summary>
/// Serviço de Etapa - contém lógica de negócio e validações
/// </summary>
public class EtapaService : IEtapaService
{
    private readonly IEtapaRepositorio _etapaRepositorio;
    private readonly AppDbContext _context;

    public EtapaService(IEtapaRepositorio etapaRepositorio, AppDbContext context)
    {
        _etapaRepositorio = etapaRepositorio;
        _context = context;
    }

    public async Task<PercentualEtapaDTO> GetPercentEtapas(int projetoid)
    {
        var etapas = await _context.Etapas
            .Where(e => e.NM_PROJETO.projetoId == projetoid)
            .ToListAsync();

        decimal somaPercentExecReal = etapas.Sum(e => e.PERCENT_EXEC_REAL ?? 0);
        decimal somaPercentExecPlan = etapas.Sum(e => e.PERCENT_PLANEJADO);

        var datasInicio = etapas.Where(e => e.DT_INICIO_PREVISTO.HasValue).Select(e => e.DT_INICIO_PREVISTO.Value);
        var datasTermino = etapas.Where(e => e.DT_TERMINO_PREVISTO.HasValue).Select(e => e.DT_TERMINO_PREVISTO.Value);

        if (!datasInicio.Any() || !datasTermino.Any())
        {
            return new PercentualEtapaDTO
            {
                PERCENT_PLANEJADO = 0,
                PERCENT_EXECUTADO = somaPercentExecReal
            };
        }

        var inicioMin = datasInicio.Min();
        var terminoMax = datasTermino.Max();
        var dataAtual = DateTime.UtcNow;
        var diffDays = (dataAtual - inicioMin).Days;
        var diffDaysTermino = 0;
        if (terminoMax < dataAtual)
            diffDaysTermino = (terminoMax - inicioMin).Days;
        else
            diffDaysTermino = (dataAtual - inicioMin).Days;

        if (diffDays == 0) diffDays = 1;



        return new PercentualEtapaDTO
        {
            PERCENT_PLANEJADO = somaPercentExecPlan,
            PERCENT_EXECUTADO = somaPercentExecReal
        };
    }
    /// <summary>
    /// Edita uma etapa existente (aferição)
    /// </summary>
    public async Task EditEtapa(AfericaoEtapaDTO etapa, int etapaid)
    {
        var etapaEdit = await _etapaRepositorio.GetByIdAsync(etapaid)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        try
        {
            // Conversões de timezone usando DateTimeHelper
            if (etapa.DT_INICIO_REAL.HasValue)
            {
                etapaEdit.DT_INICIO_REAL = DateTimeHelper.ToUtc(etapa.DT_INICIO_REAL.Value);
            }

            if (etapa.DT_TERMINO_REAL.HasValue)
            {
                etapaEdit.DT_TERMINO_REAL = DateTimeHelper.ToUtc(etapa.DT_TERMINO_REAL.Value);
            }

            etapaEdit.ANALISE = etapa.ANALISE;
            etapaEdit.PERCENT_EXEC_ETAPA = etapa.PERCENT_EXEC_ETAPA;

            await _etapaRepositorio.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoEditarEtapa);
        }
    }

    /// <summary>
    /// Cria uma nova etapa
    /// </summary>
    public async Task CreateEtapa(EtapaDTO etapa)
    {
        var projetoCadastro = await _etapaRepositorio.GetProjetoByIdAsync(etapa.NM_PROJETO)
            ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);

        try
        {
            // Conversões de timezone usando DateTimeHelper
            var etapaCadastro = new Etapa
            {
                NM_ETAPA = etapa.NM_ETAPA,
                DT_INICIO_PREVISTO = DateTimeHelper.ToUtc(etapa.DT_INICIO_PREVISTO),
                DT_TERMINO_PREVISTO = DateTimeHelper.ToUtc(etapa.DT_TERMINO_PREVISTO),
                PERCENT_TOTAL_ETAPA = etapa.PERCENT_TOTAL_ETAPA,
                RESPONSAVEL_ETAPA = etapa.RESPONSAVEL_ETAPA,
                NM_PROJETO = projetoCadastro
            };

            _etapaRepositorio.Add(etapaCadastro);
            await _etapaRepositorio.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw new ApiException(ErrorCode.ErroAoCriarEtapa);
        }
    }
    /// <summary>
    /// Obtém todas as etapas de um projeto
    /// </summary>
    public async Task<List<Etapa>> GetEtapaListItemsAsync(int projetoId)
    {
        var etapas = await _etapaRepositorio.GetEtapasByProjetoIdAsync(projetoId);

        if (etapas == null || etapas.Count == 0)
            return new List<Etapa>();

        return etapas;
    }

    /// <summary>
    /// Obtém uma etapa por ID
    /// </summary>
    public async Task<Etapa> GetById(int id)
    {
        var etapa = await _etapaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        return etapa;
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
                continue;
            }

            if (etapas.Any(e => e.SITUACAO == "atrasado para inicio" || e.SITUACAO == "atrasado para conclusão"))
            {
                atrasados++;
            }
            else if (etapas.Any(e => e.SITUACAO == "Em andamento"))
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

    public async Task<TagsDTO> GetTags()
    {
        var pdtic2427 = await _context.Projetos.Where(p => p.PDTIC2427 == true).ToListAsync();
        var ptd2427 = await _context.Projetos.Where(p => p.PTD2427 == true).ToListAsync();
        var profiscoII = await _context.Projetos.Where(p => p.PROFISCOII == true).ToListAsync();

        return new TagsDTO
        {
            PDTIC2427 = pdtic2427.Count,
            PTD2427 = ptd2427.Count,
            PROFISCOII = profiscoII.Count
        };
    }


    public async Task IniciarEtapa(int id, DateTime dtInicioPrevisto)
    {
        Etapa etapa = await GetById(id);

        if (etapa == null)
            throw new KeyNotFoundException("Etapa não encontrada.");

        // Usando DateTimeHelper centralizado
        DateTime dtUtc = DateTimeHelper.ToUtc(dtInicioPrevisto);

        etapa.DT_INICIO_PREVISTO = dtUtc;
        etapa.DT_TERMINO_PREVISTO = dtUtc.AddDays(etapa.DIAS_PREVISTOS);

        await _context.SaveChangesAsync();
    }



}
