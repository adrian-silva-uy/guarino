using System.Collections.Concurrent;

namespace GuarinoApp.Application.Modules.Auth.Services;

/// <summary>
/// Control de intentos de inicio de sesión en memoria: tras varios fallos
/// consecutivos de un mismo usuario, su cuenta queda bloqueada temporalmente.
/// Cierra el hueco de fuerza bruta contra /login (el handler no tenía límite
/// de intentos). Singleton: el contador se comparte entre todos los circuitos.
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

    private sealed record Estado(int Fallos, DateTime? BloqueoHastaUtc, DateTime UltimoFalloUtc);

    private readonly ConcurrentDictionary<string, Estado> _intentos =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// true si la cuenta sigue bloqueada; en tal caso <paramref name="liberacionUtc"/>
    /// indica cuándo vence el bloqueo.
    /// </summary>
    public bool EstaBloqueado(string userName, out DateTime? liberacionUtc)
    {
        liberacionUtc = null;
        if (string.IsNullOrWhiteSpace(userName)
            || !_intentos.TryGetValue(userName, out var estado)
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
        _intentos.TryRemove(userName, out _);
        return false;
    }

    /// <summary>Registra un intento fallido; devuelve true si la cuenta quedó bloqueada.</summary>
    public bool RegistrarFallo(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        var ahora = DateTime.UtcNow;
        var estado = _intentos.AddOrUpdate(
            userName,
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
    public void RegistrarExito(string userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            _intentos.TryRemove(userName, out _);
        }
    }
}
