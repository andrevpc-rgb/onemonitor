using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneMonitor.Agent.Infrastructure.Sync;

/// <summary>
/// O conversor padrão de TimeOnly do System.Text.Json espera o próprio formato de
/// round-trip dele ("HH:mm:ss.fffffff") — não o que o Postgres/Supabase devolve para uma
/// coluna `time` ("08:00:00", às vezes só "08:00"). Este conversor aceita qualquer formato
/// que TimeOnly.TryParse reconheça, o que cobre os dois.
/// </summary>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time))
        {
            return time;
        }

        throw new JsonException($"Horário inválido: \"{value}\".");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
