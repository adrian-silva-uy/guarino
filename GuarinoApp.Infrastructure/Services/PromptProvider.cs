using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace GuarinoApp.Infrastructure.Services
    {
    /// <summary>
    /// Única fuente de verdad para cargar plantillas de prompts desde disco.
    /// Usa IHostEnvironment.ContentRootPath (la raíz del proyecto/publicación) en vez
    /// de AppContext.BaseDirectory (carpeta de salida del build, que en Debug suele
    /// ser distinta de ContentRootPath). Así todos los servicios que leen prompts
    /// —incluido OllamaAIService— buscan siempre en la misma carpeta física:
    /// {ContentRootPath}/Resources/Prompts.
    /// </summary>
    public sealed class PromptProvider : IPromptProvider
        {
        private readonly string _promptsPath;

        public PromptProvider(IHostEnvironment hostEnvironment)
            {
            _promptsPath = Path.Combine(hostEnvironment.ContentRootPath, "Resources", "Prompts");
            }

        public Task<IEnumerable<string>> GetAvailablePromptsAsync()
            {
            if (!Directory.Exists(_promptsPath))
                return Task.FromResult(Enumerable.Empty<string>());

            var keys = Directory.EnumerateFiles(_promptsPath)
                                .Select(f => Path.GetFileNameWithoutExtension(f));
            return Task.FromResult(keys);
            }

        public async Task<string?> GetPromptAsync(string key)
            {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            if (!Directory.Exists(_promptsPath))
                return null;

            // Buscar por nombre sin extensión o por nombre exacto (case-insensitive)
            var file = Directory.EnumerateFiles(_promptsPath)
                                .FirstOrDefault(f =>
                                    string.Equals(Path.GetFileNameWithoutExtension(f), key, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(Path.GetFileName(f), key, StringComparison.OrdinalIgnoreCase));

            if (file is null)
                return null;

            return await File.ReadAllTextAsync(file).ConfigureAwait(false);
            }
        }
    }
