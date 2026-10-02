namespace GuarinoApp.Application.Common.Interfaces
    {
    /// <summary>
    /// Cliente del SERVICIO WEB de IA (consulta armada hacia un proveedor externo
    /// del régimen gratuito — sin costo para la aplicación). Se lanza EN PARALELO
    /// con la ruta local (Ollama): la ruta local tiene prioridad y la respuesta
    /// web solo se consume cuando la local no responde.
    /// Sin clave configurada, Habilitada=false y todo vuelve a la ruta local.
    /// </summary>
    public interface IIaWebClient
        {
        bool Habilitada { get; }

        /// <summary>
        /// Envía los mensajes ya armados al servicio web y devuelve el texto de la
        /// respuesta, o null si no está habilitado, falló o agotó su límite de uso.
        /// Nunca lanza excepción: el llamador siempre puede apoyarse en lo local.
        /// </summary>
        Task<string?> EnviarAsync(
            IReadOnlyList<object> messages,
            bool modoJson,
            CancellationToken cancellationToken = default);
        }
    }
