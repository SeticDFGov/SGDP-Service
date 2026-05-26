using System.Text.Json;
using System.Text.Json.Serialization;

namespace demanda_service.Helpers;

/// <summary>
/// Conversor JSON que automaticamente converte DateTime de UTC para horário de Brasília na serialização
/// e de Brasília para UTC na desserialização
/// </summary>
public class BrasiliaDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTimeString = reader.GetString();
        if (string.IsNullOrEmpty(dateTimeString))
            return DateTime.MinValue;

        var dateTime = DateTime.Parse(dateTimeString);

        // Se já vier com informação de timezone (Z ou offset), usa como está
        // Caso contrário, assume que é horário de Brasília e converte para UTC
        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime;

        return DateTimeHelper.ToUtc(dateTime);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Converte de UTC para Brasília antes de serializar
        var brasilia = DateTimeHelper.ToBrasilia(value);

        // Serializa no formato ISO 8601 sem timezone info (apenas data e hora local de Brasília)
        // Isso evita confusão no frontend
        writer.WriteStringValue(brasilia.ToString("yyyy-MM-ddTHH:mm:ss"));
    }
}

/// <summary>
/// Conversor JSON para DateTime nullable
/// </summary>
public class BrasiliaDateTimeNullableConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTimeString = reader.GetString();
        if (string.IsNullOrEmpty(dateTimeString))
            return null;

        var dateTime = DateTime.Parse(dateTimeString);

        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime;

        return DateTimeHelper.ToUtc(dateTime);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        var brasilia = DateTimeHelper.ToBrasilia(value.Value);
        writer.WriteStringValue(brasilia.ToString("yyyy-MM-ddTHH:mm:ss"));
    }
}
