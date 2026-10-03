using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Caché TTL de resultados IA por clave de solicitud, compartida por todos los
/// circuitos (singleton). En días pico (~20 usuarios simultáneos) varias cargas
/// de página disparan el MISMO prompt (p. ej. 20 dashboards con la misma data):
/// sin caché cada carga quema cupo del plan gratuito (8.000 tokens/min) y un
/// turno de la compuerta local; con caché, la primera carga paga la consulta y
/// las siguientes reciben la respuesta al instante.
///
/// El prompt YA incluye los datos vigentes de la base, así que un acierto solo
/// se produce cuando la data de entrada es idéntica — la respuesta sigue siendo
/// la correcta. El TTL (10 min) acota la memoria y el caso de consultas
/// abiertas repetidas. Solo se guardan respuestas IA reales: los respaldos
/// deterministas NO se cachean, para que un pico no deje respuestas "sin IA"
/// congeladas durante el TTL.
/// </summary>
public class AiResultCache
    {
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const int MaxEntradas = 256;

    private readonly ConcurrentDictionary<string, Entrada> _datos = new();

    private sealed class Entrada
        {
        public DateTime Expira { get; init; }
        public object? Valor { get; init; }
        }

    /// <summary>Clave estable y compacta a partir de la operación y sus argumentos.</summary>
    public static string Clave(string operacion, params object?[] partes)
        {
        string json;
        try
            {
            json = JsonSerializer.Serialize(partes);
            }
        catch
            {
            // Si algún argumento no es serializable, la clave degrada a la
            // operación: se comporta como sin caché en vez de fallar.
            return operacion + ":sin-clave";
            }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(operacion + "|" + json));
        return operacion + ":" + Convert.ToHexString(hash.AsSpan(0, 12));
        }

    public bool TryGet<T>(string clave, out T? valor)
        {
        valor = default;
        if (!_datos.TryGetValue(clave, out var entrada))
            return false;
        if (entrada.Expira < DateTime.UtcNow)
            {
            _datos.TryRemove(clave, out _);
            return false;
            }
        if (entrada.Valor is T tipado)
            {
            valor = tipado;
            return true;
            }
        return false;
        }

    public void Set<T>(string clave, T valor)
        {
        if (valor == null)
            return;

        if (_datos.Count >= MaxEntradas)
            {
            var ahora = DateTime.UtcNow;
            foreach (var k in _datos
                .Where(kv => kv.Value.Expira < ahora)
                .Select(kv => kv.Key)
                .Take(32)
                .ToList())
                _datos.TryRemove(k, out _);

            while (_datos.Count >= MaxEntradas)
                {
                var masVieja = _datos
                    .OrderBy(kv => kv.Value.Expira)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();
                if (masVieja == null)
                    break;
                _datos.TryRemove(masVieja, out _);
                }
            }

        _datos[clave] = new Entrada { Expira = DateTime.UtcNow.Add(Ttl), Valor = valor };
        }
    }
