using GuarinoApp.Application.Common;
using Microsoft.Extensions.Configuration;

namespace GuarinoApp.Infrastructure.Extensions
    {
    public static class BusinessSuggestionRequestExtensions
        {
        /// <summary>
        /// Valida y normaliza el request: aplica rangos, y si los valores no son válidos
        /// utiliza defaults configurables en "IA:Defaults". No modifica el DTO original.
        /// </summary>
        public static BusinessSuggestionRequest Normalize(this BusinessSuggestionRequest req, IConfiguration config)
            {
            if (req is null) throw new ArgumentNullException(nameof(req));

            // Lectura de defaults desde configuración (pueden venir de appsettings / env / key vault)
            var minDefault = config.GetValue<int?>("IA:Defaults:MinConfidence") ?? 70;
            var horizonDefault = config.GetValue<int?>("IA:Defaults:HorizonMonths") ?? 6;
            var seasonDefault = config.GetValue<bool?>("IA:Defaults:IncludeSeasonality") ?? true;

            // Aplicar validación/rangos (mejores prácticas: no confiar en valores del cliente)
            var min = (req.MinConfidence >= 0 && req.MinConfidence <= 100) ? req.MinConfidence : minDefault;
            var horizon = (req.HorizonMonths >= 1 && req.HorizonMonths <= 36) ? req.HorizonMonths : horizonDefault;
            var season = req.IncludeSeasonality; // bool siempre es válido; config sólo sirve como fallback si se necesita override

            return new BusinessSuggestionRequest
                {
                TotalStock = req.TotalStock,
                StockByVariety = req.StockByVariety,
                PalletsWithoutClient = req.PalletsWithoutClient,
                GrowthPercent = req.GrowthPercent,
                PeriodLabel = req.PeriodLabel,
                MinConfidence = min,
                HorizonMonths = horizon,
                IncludeSeasonality = season,
                ProyeccionZafra = req.ProyeccionZafra
                };
            }
        }
    }
