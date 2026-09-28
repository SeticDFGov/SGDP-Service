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

        // Se for Local ou Unspecified, precisamos especificar que é timezone de Brasília
        if (brasiliaDateTime.Kind == DateTimeKind.Local || brasiliaDateTime.Kind == DateTimeKind.Unspecified)
        {
            // Marca como Unspecified para poder converter do timezone de Brasília
            brasiliaDateTime = DateTime.SpecifyKind(brasiliaDateTime, DateTimeKind.Unspecified);
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
    /// Adiciona dias úteis a uma data: pula o sábado, o domingo e os feriados
    /// (<see cref="EhFeriado"/>). Até a E8 da Governança Estratégica contava só de segunda a
    /// sexta, e ninguém chamava; o prazo do art. 11 do Decreto nº 48.899/2026 é o primeiro uso.
    /// </summary>
    public static DateTime AdicionarDiasUteis(DateTime data, int diasUteis)
    {
        var resultado = data;
        var diasAdicionados = 0;

        while (diasAdicionados < diasUteis)
        {
            resultado = resultado.AddDays(1);

            if (EhDiaUtil(resultado))
            {
                diasAdicionados++;
            }
        }

        return resultado;
    }

    /// <summary>Dia útil: de segunda a sexta e fora dos feriados (<see cref="EhFeriado"/>).</summary>
    public static bool EhDiaUtil(DateTime data) =>
        data.DayOfWeek != DayOfWeek.Saturday && data.DayOfWeek != DayOfWeek.Sunday && !EhFeriado(data);

    /// <summary>
    /// Feriado em Brasília: os nacionais de data fixa (1º de janeiro, 21 de abril, 1º de maio,
    /// 7 de setembro, 12 de outubro, 2 de novembro, 15 de novembro, 20 de novembro desde 2024
    /// pela Lei nº 14.759/2023, e 25 de dezembro), a Paixão de Cristo (a sexta-feira antes da
    /// Páscoa) e o Dia do Evangélico do DF (30 de novembro, Lei distrital nº 963/1995). O 21 de
    /// abril é também a fundação de Brasília. Os pontos facultativos (Carnaval, Quarta-feira de
    /// Cinzas, Corpus Christi, Dia do Servidor) não entram: dependem do decreto de cada ano.
    /// </summary>
    public static bool EhFeriado(DateTime data)
    {
        var dia = data.Date;
        switch ((dia.Month, dia.Day))
        {
            case (1, 1):
            case (4, 21):
            case (5, 1):
            case (9, 7):
            case (10, 12):
            case (11, 2):
            case (11, 15):
            case (11, 30):
            case (12, 25):
                return true;
            case (11, 20):
                return dia.Year >= 2024;
        }
        return dia == Pascoa(dia.Year).AddDays(-2);
    }

    /// <summary>O domingo de Páscoa do ano (calendário gregoriano, algoritmo de Meeus, Jones e Butcher).</summary>
    public static DateTime Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var diaDoMes = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(ano, mes, diaDoMes);
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
