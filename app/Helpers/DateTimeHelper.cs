using TimeZoneConverter;

namespace demanda_service.Helpers;

/// <summary>
/// Helper centralizado para conversões de timezone e manipulação de datas
/// </summary>
public static class DateTimeHelper
{
    // Timezone do Brasil (Brasília - GMT-3)
    private static readonly TimeZoneInfo BrasiliaTimeZone =
        TZConvert.GetTimeZoneInfo("E. South America Standard Time");

    /// <summary>
    /// Converte DateTime UTC para horário de Brasília
    /// </summary>
    /// <param name="utcDateTime">Data/hora em UTC</param>
    /// <returns>Data/hora no horário de Brasília</returns>
    public static DateTime ToBrasilia(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            // Se não for UTC, assume que já está em UTC e converte
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BrasiliaTimeZone);
    }

    /// <summary>
    /// Converte DateTime do horário de Brasília para UTC
    /// </summary>
    /// <param name="brasiliaDateTime">Data/hora no horário de Brasília</param>
    /// <returns>Data/hora em UTC</returns>
    public static DateTime ToUtc(DateTime brasiliaDateTime)
    {
        // Se já for UTC, retorna direto
        if (brasiliaDateTime.Kind == DateTimeKind.Utc)
        {
            return brasiliaDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(brasiliaDateTime, BrasiliaTimeZone);
    }

    /// <summary>
    /// Retorna a data/hora atual de Brasília
    /// </summary>
    public static DateTime NowBrasilia()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone);
    }

    /// <summary>
    /// Retorna apenas a data atual de Brasília (sem hora)
    /// </summary>
    public static DateTime TodayBrasilia()
    {
        return NowBrasilia().Date;
    }

    /// <summary>
    /// Converte DateTime nullable UTC para Brasília
    /// </summary>
    public static DateTime? ToBrasilia(DateTime? utcDateTime)
    {
        if (utcDateTime == null)
            return null;

        return ToBrasilia(utcDateTime.Value);
    }

    /// <summary>
    /// Converte DateTime nullable de Brasília para UTC
    /// </summary>
    public static DateTime? ToUtc(DateTime? brasiliaDateTime)
    {
        if (brasiliaDateTime == null)
            return null;

        return ToUtc(brasiliaDateTime.Value);
    }

    /// <summary>
    /// Calcula diferença em dias entre duas datas (ignora hora)
    /// </summary>
    public static int DiferencaEmDias(DateTime dataInicio, DateTime dataFim)
    {
        return (dataFim.Date - dataInicio.Date).Days;
    }

    /// <summary>
    /// Adiciona dias úteis (segunda a sexta) a uma data
    /// </summary>
    public static DateTime AdicionarDiasUteis(DateTime data, int diasUteis)
    {
        var resultado = data;
        var diasAdicionados = 0;

        while (diasAdicionados < diasUteis)
        {
            resultado = resultado.AddDays(1);

            // Ignora sábado (6) e domingo (0)
            if (resultado.DayOfWeek != DayOfWeek.Saturday &&
                resultado.DayOfWeek != DayOfWeek.Sunday)
            {
                diasAdicionados++;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Verifica se uma data está no passado (comparado com hoje em Brasília)
    /// </summary>
    public static bool EstaNoPassado(DateTime data)
    {
        return data.Date < TodayBrasilia();
    }

    /// <summary>
    /// Verifica se uma data está no futuro (comparado com hoje em Brasília)
    /// </summary>
    public static bool EstaNoFuturo(DateTime data)
    {
        return data.Date > TodayBrasilia();
    }

    /// <summary>
    /// Formata data no padrão brasileiro (dd/MM/yyyy)
    /// </summary>
    public static string FormatarDataBrasileira(DateTime data)
    {
        return data.ToString("dd/MM/yyyy");
    }

    /// <summary>
    /// Formata data e hora no padrão brasileiro (dd/MM/yyyy HH:mm)
    /// </summary>
    public static string FormatarDataHoraBrasileira(DateTime dataHora)
    {
        return dataHora.ToString("dd/MM/yyyy HH:mm");
    }

    /// <summary>
    /// Formata data nullable no padrão brasileiro
    /// </summary>
    public static string? FormatarDataBrasileira(DateTime? data)
    {
        return data?.ToString("dd/MM/yyyy");
    }
}
