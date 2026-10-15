using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>Logger que observa y copia al <see cref="ErrorLogBuffer"/> solo lo
/// notificable (errores y avisos críticos de IA), sin alterar la consola.</summary>
internal sealed class LoggerDeBuffer : ILogger
    {
    private readonly ErrorLogBuffer _buffer;
    private readonly string _categoria;

    public LoggerDeBuffer(ErrorLogBuffer buffer, string categoria)
        {
        _buffer = buffer;
        _categoria = categoria;
        }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Error ||
        (logLevel == LogLevel.Warning && Array.Exists(ErrorLogBufferProvider.CategoriasDeIa, c => _categoria.StartsWith(c, StringComparison.Ordinal)));

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        {
        if (!IsEnabled(logLevel))
            return;

        if (Array.Exists(ErrorLogBufferProvider.CategoriasExcluidas, c => _categoria.StartsWith(c, StringComparison.Ordinal)))
            return;

        _buffer.Agregar(_categoria, $"[{logLevel}] {formatter(state, exception)}", exception?.ToString());
        }
    }
