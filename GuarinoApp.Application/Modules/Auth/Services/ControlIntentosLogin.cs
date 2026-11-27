using System.Collections.Concurrent;

namespace GuarinoApp.Application.Modules.Auth.Services;

/// <summary>
/// Control de intentos de inicio de sesión en memoria: tras varios fallos
/// consecutivos de una combinación usuario/IP, esa combinación queda bloqueada
/// temporalmente. Singleton: el contador se comparte entre todos los circuitos.
/// Los datos viven solo en memoria (se reinician con la app), suficiente para
/// frenar un ataque en curso sin persistir estado de seguridad en la base.
/// </summary>
public sealed class ControlIntentosLogin
{
    /// <summary>Fallos consecutivos antes de bloquear la cuenta.</summary>
    public const int MaximoIntentos = 5;

    /// <summary>Duración del bloqueo temporal, en minutos.</summary>
    public const int BloqueoMinutos = 15;

    /// <summary>Fallos con más de esta antigüedad ya no se acumulan (ventana corta).</summary>
    private const int VentanaMinutos = 30;
    private const int MaximoEstadosEnMemoria = 10_000;

    private sealed record Estado(int Fallos, DateTime? BloqueoHastaUtc, DateTime UltimoFalloUtc);

    private readonly ConcurrentDictionary<string, Estado> _intentos =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// true si la cuenta sigue bloqueada; en tal caso <paramref name="liberacionUtc"/>
    /// indica cuándo vence el bloqueo.
    /// </summary>
    public bool EstaBloqueado(string userName, out DateTime? liberacionUtc, string? clientAddress = null)
    {
        liberacionUtc = null;
        var key = CrearClave(userName, clientAddress);
        if (key == null
            || !_intentos.TryGetValue(key, out var estado)
            || estado.BloqueoHastaUtc is null)
        {
            return false;
        }

        if (estado.BloqueoHastaUtc > DateTime.UtcNow)
        {
            liberacionUtc = estado.BloqueoHastaUtc;
            return true;
        }

        // Bloqueo vencido: se limpia la cuenta y se vuelve a permitir el ingreso.
        _intentos.TryRemove(key, out _);
        return false;
    }

    /// <summary>Registra un intento fallido; devuelve true si la cuenta quedó bloqueada.</summary>
    public bool RegistrarFallo(string userName, string? clientAddress = null)
    {
        var key = CrearClave(userName, clientAddress);
        if (key == null)
        {
            return false;
        }

        var ahora = DateTime.UtcNow;
        if (_intentos.Count >= MaximoEstadosEnMemoria)
        {
            // La clave procede del formulario de login. Eliminar estados vencidos
            // evita que nombres arbitrarios hagan crecer el singleton sin límite.
            foreach (var (usuario, intento) in _intentos)
            {
                if (ahora - intento.UltimoFalloUtc > TimeSpan.FromMinutes(VentanaMinutos + BloqueoMinutos))
                    _intentos.TryRemove(usuario, out _);
            }

            if (_intentos.Count >= MaximoEstadosEnMemoria)
            {
                var masAntiguo = _intentos.OrderBy(par => par.Value.UltimoFalloUtc).FirstOrDefault();
                if (masAntiguo.Key != null)
                    _intentos.TryRemove(masAntiguo.Key, out _);
            }
        }

        var estado = _intentos.AddOrUpdate(
            key,
            _ => new Estado(1, null, ahora),
            (_, actual) =>
            {
                var fallos = (ahora - actual.UltimoFalloUtc).TotalMinutes > VentanaMinutos
                    ? 1
                    : actual.Fallos + 1;
                return fallos >= MaximoIntentos
                    ? new Estado(fallos, ahora.AddMinutes(BloqueoMinutos), ahora)
                    : new Estado(fallos, null, ahora);
            });

        return estado.BloqueoHastaUtc != null;
    }

    /// <summary>Un ingreso exitoso borra los fallos previos de esa cuenta.</summary>
    public void RegistrarExito(string userName, string? clientAddress = null)
    {
        var key = CrearClave(userName, clientAddress);
        if (key != null)
        {
            _intentos.TryRemove(key, out _);
        }
    }

    private static string? CrearClave(string userName, string? clientAddress)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var normalizedName = userName.Trim();
        var address = string.IsNullOrWhiteSpace(clientAddress) ? "unknown" : clientAddress.Trim();
        return $"{normalizedName.Length}:{normalizedName}|{address}";
    }
}
