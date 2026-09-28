using GuarinoApp.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace GuarinoApp.Web.Validation
    {
    public class ValidateOllamaOptions : IValidateOptions<OllamaOptions>
        {
        public ValidateOptionsResult Validate(string? name, OllamaOptions options)
            {
            if (!options.Enabled)
                return ValidateOptionsResult.Success;

            if (string.IsNullOrWhiteSpace(options.Url))
                return ValidateOptionsResult.Fail("Ollama:Url es obligatoria si Ollama:Enabled = true.");

            if (string.IsNullOrWhiteSpace(options.Model))
                return ValidateOptionsResult.Fail("Ollama:Model es obligatoria si Ollama:Enabled = true.");

            return ValidateOptionsResult.Success;
            }
        }
    }
