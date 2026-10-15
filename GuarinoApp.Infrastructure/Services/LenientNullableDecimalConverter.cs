using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Igual que <see cref="LenientStringConverter"/> pero para los KPIs: si el modelo
/// manda los números como texto ("70.841"), se convierten en vez de romper la lectura.
/// </summary>
internal sealed class LenientNullableDecimalConverter : JsonConverter<decimal?>
    {
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
        switch (reader.TokenType)
            {
            case JsonTokenType.Number:
                return reader.GetDecimal();
            case JsonTokenType.String:
                var texto = reader.GetString();
                if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec)
                    || decimal.TryParse(texto, NumberStyles.Any, CultureInfo.CurrentCulture, out dec))
                    return dec;
                return null;
            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean() ? 1m : 0m;
            default:
                return null;
            }
        }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
        }
    }
