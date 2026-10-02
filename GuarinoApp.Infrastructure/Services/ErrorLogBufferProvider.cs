using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// ILoggerProvider global que copia al <see cref="ErrorLogBuffer"/> los
/// errores (y los avisos críticos de los servicios de IA) para que el
/// notificador del administrador los envíe por correo. No altera la consola
/// ni los demás providers: solo observa.
/// </summary>
public sealed class ErrorLogBufferProvider : ILoggerProvider
    {
    // Avisos de IA que sí importan notificar (ruta local, servicio web, modelo).
    private static readonly string[] CategoriasDeIa =
        {
        "GuarinoApp.Infrastructure.Services.OllamaAIService",
        "GuarinoApp.Infrastructure.Services.IaWebClient",
        "GuarinoApp.Infrastructure.Services.OllamaStartupService"
        };

    // Excluidas: el propio EmailService (un fallo de envío NO debe disparar
    // otro correo — sería un ciclo) y el notificador (sus fallos se manejan
    // con reintentos internos, no con más correos).
    private static readonly string[] CategoriasExcluidas =
        {
        "GuarinoApp.Infrastructure.Services.EmailService",
        "GuarinoApp.Infrastructure.Services.NotificadorAdminService"
        };

    private readonly ErrorLogBuffer _buffer;

    public ErrorLogBufferProvider(ErrorLogBuffer buffer)
        {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

    public ILogger CreateLogger(string categoryName) => new LoggerDeBuffer(_buffer, categoryName);

    public void Dispose() { }

    private sealed class LoggerDeBuffer : ILogger
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
            (logLevel == LogLevel.Warning && Array.Exists(CategoriasDeIa, c => _categoria.StartsWith(c, StringComparison.Ordinal)));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            {
            if (!IsEnabled(logLevel))
                return;

            if (Array.Exists(CategoriasExcluidas, c => _categoria.StartsWith(c, StringComparison.Ordinal)))
                return;

            _buffer.Agregar(_categoria, $"[{logLevel}] {formatter(state, exception)}", exception?.ToString());
            }
        }
    }
