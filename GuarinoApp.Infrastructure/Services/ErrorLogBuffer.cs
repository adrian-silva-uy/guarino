using System.Collections.Concurrent;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>Registro capturado del log para el reporte al administrador.</summary>
public sealed class ErrorLogEntry
    {
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Categoria { get; init; } = "";
    public string Mensaje { get; init; } = "";
    public string? Excepcion { get; init; }
    }

/// <summary>
/// Buffer en memoria (ring) de errores y avisos importantes. El proveedor de
/// logging lo alimenta y el notificador del administrador lo drena cada
/// intervalo para mandarlo por correo. Está acotado (300 entradas) para que
/// un pico de errores nunca consuma memoria sin límite.
/// </summary>
public sealed class ErrorLogBuffer
    {
    private const int Maximo = 300;
    private readonly ConcurrentQueue<ErrorLogEntry> _items = new();

    public int Cantidad => _items.Count;

    public void Agregar(string categoria, string mensaje, string? excepcion)
        {
        _items.Enqueue(new ErrorLogEntry
            {
            Timestamp = DateTime.Now,
            Categoria = categoria,
            Mensaje = mensaje,
            Excepcion = excepcion
            });

        while (_items.Count > Maximo && _items.TryDequeue(out _)) { }
        }

    /// <summary>Devuelve todo lo acumulado y vacía el buffer.</summary>
    public List<ErrorLogEntry> Vaciar()
        {
        var lista = new List<ErrorLogEntry>();
        while (_items.TryDequeue(out var entrada))
            lista.Add(entrada);
        return lista;
        }

    /// <summary>Reintenta entradas cuyo envío por correo falló: las devuelve al
    /// frente del buffer para que salgan en el próximo intento (sin perderlas).</summary>
    public void Reencolar(IReadOnlyList<ErrorLogEntry> fallidas)
        {
        var restantes = Vaciar();

        foreach (var entrada in fallidas)
            _items.Enqueue(entrada);
        foreach (var entrada in restantes)
            _items.Enqueue(entrada);

        while (_items.Count > Maximo && _items.TryDequeue(out _)) { }
        }
    }
