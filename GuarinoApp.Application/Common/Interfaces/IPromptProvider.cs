namespace GuarinoApp.Application.Common.Interfaces
    {
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPromptProvider
        {
        /// <summary>
        /// Obtiene el contenido del prompt identificado por <paramref name="key"/>.
        /// La implementación busca archivos en "Resources/Prompts" dentro del output del build.
        /// </summary>
        Task<string?> GetPromptAsync(string key);

        /// <summary>
        /// Lista las claves de prompts disponibles (nombre de fichero sin extensión).
        /// </summary>
        Task<IEnumerable<string>> GetAvailablePromptsAsync();
        }
    }
