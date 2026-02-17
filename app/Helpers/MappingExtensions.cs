using api.Etapa;
using api.Projeto;
using demanda_service.Helpers;
using Models;

namespace demanda_service.Helpers;

/// <summary>
/// Extensions para mapeamento de entidades para DTOs de resposta
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Mapeia Projeto para ProjetoResponseDTO
    /// </summary>
    public static ProjetoResponseDTO ToResponseDTO(this Projeto projeto)
    {
        return new ProjetoResponseDTO
        {
            ProjetoId = projeto.projetoId,
            NM_PROJETO = projeto.NM_PROJETO,
            GERENTE_PROJETO = projeto.GERENTE_PROJETO,
            SITUACAO = projeto.SITUACAO,
            NR_PROCESSO_SEI = projeto.NR_PROCESSO_SEI,
            ANO = projeto.ANO,
            TEMPLATE = projeto.TEMPLATE,
            PROFISCOII = projeto.PROFISCOII,
            PDTIC2427 = projeto.PDTIC2427,
            PTD2427 = projeto.PTD2427,
            ValorEstimado = projeto.valorEstimado,
            DT_INICIO = DateTimeHelper.ToBrasilia(projeto.DT_INICIO),
            DT_TERMINO = DateTimeHelper.ToBrasilia(projeto.DT_TERMINO),

            // Relacionamentos simplificados
            UnidadeNome = projeto.Unidade?.Nome,
            UnidadeId = projeto.Unidade?.id,
            EsteiraNome = projeto.Esteira?.Nome,
            EsteiraId = projeto.Esteira?.EsteiraId,
            AreaDemandanteNome = projeto.AREA_DEMANDANTE?.NM_DEMANDANTE,
            AreaDemandanteSigla = projeto.AREA_DEMANDANTE?.NM_SIGLA,
            AreaDemandanteId = projeto.AREA_DEMANDANTE?.AreaDemandanteID,

            // Estatísticas (se etapas estiverem carregadas)
            TotalEtapas = projeto.Etapas?.Count,
            EtapasConcluidas = projeto.Etapas?.Count(e => e.SITUACAO == "Concluido"),
            PercentualConclusao = projeto.Etapas?.Any() == true
                ? projeto.Etapas.Average(e => e.PERCENT_EXEC_ETAPA)
                : null
        };
    }

    /// <summary>
    /// Mapeia Etapa para EtapaResponseDTO
    /// </summary>
    /// <summary>
    /// Mapeia Etapa para EtapaResponseDTO
    /// Nota: Após refatoração, datas e percentuais são calculados das atividades
    /// </summary>
    public static EtapaResponseDTO ToResponseDTO(this Etapa etapa)
    {
        return new EtapaResponseDTO
        {
            EtapaProjetoId = etapa.EtapaProjetoId,
            NM_ETAPA = etapa.NM_ETAPA,
            RESPONSAVEL_ETAPA = etapa.RESPONSAVEL_ETAPA,
            ANALISE = etapa.ANALISE,
            SITUACAO = etapa.SITUACAO,

            // Datas convertidas para Brasília (agora calculadas das atividades)
            DT_INICIO_PREVISTO = DateTimeHelper.ToBrasilia(etapa.DT_INICIO_PREVISTO),
            DT_TERMINO_PREVISTO = DateTimeHelper.ToBrasilia(etapa.DT_TERMINO_PREVISTO),
            DT_INICIO_REAL = DateTimeHelper.ToBrasilia(etapa.DT_INICIO_REAL),
            DT_TERMINO_REAL = DateTimeHelper.ToBrasilia(etapa.DT_TERMINO_REAL),

            // Percentuais (agora calculados das atividades)
            PERCENT_TOTAL_ETAPA = 100, // Sempre 100 (representa o peso total da etapa)
            PERCENT_EXEC_ETAPA = etapa.PERCENT_EXEC_ETAPA,
            PERCENT_EXEC_REAL = etapa.PERCENT_EXEC_ETAPA, // Mesmo valor após refatoração
            PERCENT_PLANEJADO = etapa.PERCENT_PLANEJADO,

            // Outros
            DIAS_PREVISTOS = etapa.DIAS_PREVISTOS,
            Order = etapa.Order,

            // Projeto simplificado
            ProjetoId = etapa.NM_PROJETO.projetoId,
            ProjetoNome = etapa.NM_PROJETO.NM_PROJETO
        };
    }

    /// <summary>
    /// Mapeia lista de Projetos para lista de DTOs
    /// </summary>
    public static List<ProjetoResponseDTO> ToResponseDTOList(this IEnumerable<Projeto> projetos)
    {
        return projetos.Select(p => p.ToResponseDTO()).ToList();
    }

    /// <summary>
    /// Mapeia lista de Etapas para lista de DTOs
    /// </summary>
    public static List<EtapaResponseDTO> ToResponseDTOList(this IEnumerable<Etapa> etapas)
    {
        return etapas.Select(e => e.ToResponseDTO()).ToList();
    }
}
