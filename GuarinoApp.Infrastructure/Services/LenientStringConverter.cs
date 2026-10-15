using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Los modelos pequeños a veces incumplen el esquema: ponen un objeto, número o
/// booleano donde se pide texto (p. ej. "comparativaBenchmark": {"texto": "..."}).
/// Sin esto, un solo token inesperado tumbaba todo el análisis. Convierte al mejor
/// texto disponible en lugar de fallar.
/// </summary>
internal sealed class LenientStringConverter : JsonConverter<string>
    {
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
        switch (reader.TokenType)
            {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                // Se materializa el valor en un JsonElement y se extrae el texto:
                // JsonElement sí expone GetRawText() (Utf8JsonReader no).
                using (var doc = JsonDocument.ParseValue(ref reader))
                    return ExtraerTexto(doc.RootElement);
            default:
                return null;
            }
        }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);

    private static string? ExtraerTexto(JsonElement elemento)
        {
        if (elemento.ValueKind == JsonValueKind.Array)
            {
            var partes = elemento.EnumerateArray()
                .Select(ExtraerTexto)
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var unido = string.Join(" ", partes);
            return string.IsNullOrWhiteSpace(unido) ? elemento.GetRawText() : unido;
            }

        if (elemento.ValueKind == JsonValueKind.Object)
            {
            // Primero claves habituales (inglés/español, mayúsculas indistintas)...
            foreach (var preferida in new[] { "texto", "text", "contenido", "content", "valor", "value", "mensaje", "message", "descripcion", "comparativa", "benchmark" })
                {
                foreach (var propiedad in elemento.EnumerateObject())
                    {
                    if (string.Equals(propiedad.Name, preferida, StringComparison.OrdinalIgnoreCase)
                        && propiedad.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(propiedad.Value.GetString()))
                        return propiedad.Value.GetString();
                    }
                }

            // ...y si no, se concatenan los strings del objeto.
            var textos = elemento.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .Select(p => p.Value.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var concat = string.Join(" ", textos);
            return string.IsNullOrWhiteSpace(concat) ? elemento.GetRawText() : concat;
            }

        return elemento.ValueKind == JsonValueKind.String
            ? elemento.GetString()
            : elemento.GetRawText();
        }
    }
