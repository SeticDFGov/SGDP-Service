using demanda_service.Helpers;
using Models;

namespace demanda_service.Helpers;

public static class MappingExtensions
{
    public static object ToDemandaResponse(this Demanda demanda)
    {
        return new
        {
            demanda.demandaId,
            demanda.NM_PROJETO,
            demanda.NR_PROCESSO_SEI,
            demanda.SITUACAO,
            AREA_DEMANDANTE = demanda.AREA_DEMANDANTE,
            Esteira = demanda.Esteira,
            TotalEntregaveis = demanda.Entregaveis?.Count,
            EntregaveisConcluidos = demanda.Entregaveis?.Count(e => e.SITUACAO == "Concluído")
        };
    }

    public static object ToEntregavelResponse(this Etapa etapa)
    {
        return new
        {
            etapa.EtapaProjetoId,
            etapa.NM_ETAPA,
            etapa.TIPO_ENTREGA,
            etapa.PERCENT_EXECUTADO,
            etapa.SITUACAO,
            etapa.Descricao,
            DT_INICIO = DateTimeHelper.ToBrasilia(etapa.DT_INICIO),
            DT_FIM = DateTimeHelper.ToBrasilia(etapa.DT_FIM),
            Responsavel = etapa.Responsavel,
            DemandaId = etapa.NM_PROJETO?.demandaId,
            DemandaNome = etapa.NM_PROJETO?.NM_PROJETO
        };
    }
}
