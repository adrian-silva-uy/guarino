using System.ComponentModel.DataAnnotations;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>
    /// Servicio web de IA en paralelo a la ruta local, en régimen GRATUITO
    /// (sin tarjeta y sin costos para la aplicación). La clave NUNCA se commitea:
    /// en Development va en user-secrets (dotnet user-secrets set "IaWeb:ApiKey" …)
    /// y en producción en la variable de entorno IaWeb__ApiKey.
    /// Sin clave, el servicio queda deshabilitado y todo sigue por lo local.
    /// </summary>
    public class IaWebOptions
        {
        public const string SectionName = "IaWeb";

        /// <summary>Activa el lanzamiento paralelo de la consulta web.</summary>
        public bool Habilitada { get; set; } = true;

        /// <summary>Endpoint compatible con OpenAI (…/v1).</summary>
        [Required]
        public string UrlBase { get; set; } = "https://api.groq.com/openai/v1";

        /// <summary>Modelo a usar en el proveedor.</summary>
        [Required]
        public string Modelo { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";

        /// <summary>Clave de acceso. Vacía = servicio deshabilitado (solo ruta local).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Timeout de cada consulta web (el proveedor responde en segundos).</summary>
        [Range(5, 120)]
        public int TimeoutSegundos { get; set; } = 45;

        /// <summary>Máximo de consultas web simultáneas (cupo por minuto del plan gratuito).</summary>
        [Range(1, 20)]
        public int ConsultasWebSimultaneas { get; set; } = 8;

        /// <summary>
        /// Ventana que se le concede a la ruta local (Ollama) para responder antes
        /// de consumir la respuesta web, que ya está corriendo en paralelo desde
        /// el primer segundo. Solo aplica con la web habilitada; sin ella, la
        /// ruta local conserva su presupuesto histórico completo.
        /// </summary>
        [Range(10, 600)]
        public int EsperaLocalSegundos { get; set; } = 90;
        }
    }
