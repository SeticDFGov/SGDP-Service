using api.Etapa;
using api.Projeto;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio;
using Repositorio.Interface;
using service.Interface;
using TimeZoneConverter; 

namespace service;

public class EtapaService: IEtapaService
{
    
    public readonly AppDbContext _context;
    public EtapaService(  AppDbContext context)
    {
        _context = context;
        
    }

 public async Task<PercentualEtapaDTO> GetPercentEtapas(int projetoid)
{
    var etapas = await _context.Etapas
        .Where(e => e.NM_PROJETO.projetoId == projetoid)
        .ToListAsync();

    decimal somaPercentExecReal = etapas.Sum(e => e.PERCENT_EXEC_REAL ?? 0);
    decimal somaPercentExecPlan = etapas.Sum(e => e.PERCENT_PLANEJADO );

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
    if(terminoMax < dataAtual)
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
  public async Task<List<Etapa>> GetEtapaListItemsAsync(int projetoId)
{
var etapas = await _context.Etapas
        .Where(e => e.NM_PROJETO.projetoId == projetoId) 
        .ToListAsync();

    if (etapas == null || etapas.Count == 0)
        throw new ApiException(ErrorCode.EtapasNaoEncontradas);
   
    return etapas;
    
    
}



public async Task<Etapa> GetById(int id)
{
    Etapa etapa =  _context.Etapas.FirstOrDefault(e => e.EtapaProjetoId == id) ?? throw new ApiException(ErrorCode.EtapaNaoEncontrada);
   
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

    return new TagsDTO {
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
    DateTime dtUtc;

    if (dtInicioPrevisto.Kind == DateTimeKind.Utc)
    {
        dtUtc = dtInicioPrevisto;
    }
    else
    {
        TimeZoneInfo brasilia = TZConvert.GetTimeZoneInfo("E. South America Standard Time");
        dtUtc = TimeZoneInfo.ConvertTimeToUtc(dtInicioPrevisto, brasilia);
    }

    etapa.DT_INICIO_PREVISTO = dtUtc;
    etapa.DT_TERMINO_PREVISTO = dtUtc.AddDays(etapa.DIAS_PREVISTOS);

    await _context.SaveChangesAsync();
}



}
