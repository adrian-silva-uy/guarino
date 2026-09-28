# C:\Guarino\fix-ia-duplicates-and-fallback.ps1
Set-StrictMode -Version Latest

$root = "C:\Guarino"
Set-Location $root

# Archivos duplicados a eliminar (si existen)
$dups = @(
    Join-Path $root "GuarinoApp.Infrastructure\Services\IAIService.cs",
    Join-Path $root "GuarinoApp.Infrastructure\Services\BusinessSuggestionDto.cs",
    Join-Path $root "GuarinoApp.Infrastructure\Services\BusinessSuggestionRequest.cs"
)

foreach ($f in $dups) {
    if (Test-Path $f) {
        Remove-Item $f -Force
        Write-Host "Deleted duplicate: $f"
    }
}

# Escribir FallbackAIService corregido (UTF-8 sin BOM)
$fallbackPath = Join-Path $root "GuarinoApp.Infrastructure\Services\FallbackAIService.cs"
$fallbackContent = @'
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services
{
    /// <summary>
    /// Servicio de fallback para IA. Genera sugerencias basadas 100% en datos
    /// y delega a Ollama cuando está disponible.
    /// Usa los DTOs definidos en la capa Application (no define tipos duplicados).
    /// </summary>
    public class FallbackAIService : IAIService
    {
        private readonly OllamaAIService _ollamaService;
        private readonly ILogger<FallbackAIService> _logger;
        private readonly IDashboardDataService _dashboardService;

        public FallbackAIService(OllamaAIService ollamaService, ILogger<FallbackAIService> logger, IDashboardDataService dashboardService)
        {
            _ollamaService = ollamaService;
            _logger = logger;
            _dashboardService = dashboardService;
        }

        public bool IsAvailable => _ollamaService.IsAvailable;

        public async Task<string> ChatAsync(string prompt, List<ChatMessage> history)
        {
            try
            {
                if (_ollamaService.IsAvailable)
                {
                    var response = await _ollamaService.ChatAsync(prompt, history);
                    if (!string.IsNullOrEmpty(response))
                        return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama falló, usando fallback.");
            }

            var request = new BusinessSuggestionRequest
            {
                TotalStock = await _dashboardService.GetTotalStockAsync(DateTime.Now),
                StockByVariety = await _dashboardService.GetStockByVarietyAsync(DateTime.Now),
                PalletsWithoutClient = await _dashboardService.GetPalletsWithoutClientAsync(DateTime.Now),
                GrowthPercent = await _dashboardService.GetProjectedGrowthAsync(DateTime.Now),
                PeriodLabel = $"Actualizado: {DateTime.Now:dd/MM/yyyy}"
            };

            var suggestions = await GetBusinessSuggestionsAsync(request, history);
            if (suggestions != null && suggestions.Any())
                return string.Join("\n", suggestions.Select(s => s.Text));

            return "No pude generar una respuesta en este momento.";
        }

        /// <summary>
        /// Genera sugerencias basadas en datos (fallback determinista).
        /// </summary>
        public async Task<List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(BusinessSuggestionRequest request, List<ChatMessage>? history = null)
        {
            try
            {
                var result = new List<BusinessSuggestionDto>();

                // Determinar topVar cuidadosamente (manejo de nullables)
                KeyValuePair<string,int>? topVarNullable = null;
                if (request.StockByVariety != null && request.StockByVariety.Any())
                {
                    topVarNullable = request.StockByVariety.OrderByDescending(kv => kv.Value).FirstOrDefault();
                }

                var hasTopVar = topVarNullable.HasValue;
                var topVarKey = hasTopVar ? topVarNullable.Value.Key : null;
                var topVarValue = hasTopVar ? topVarNullable.Value.Value : 0;

                // 1) Operaciones: pallets sin cliente
                if (request.PalletsWithoutClient > 0)
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Operaciones: Asignar cliente o priorizar despacho de los {request.PalletsWithoutClient:N0} pallets sin cliente para liberar inventario comercializable y reducir riesgo de inmovilización.",
                        Color = "#ff9800",
                        Icon = "Icons.Material.Filled.AssignmentLate"
                    });
                }
                // 1b) Concentración por variedad
                else if (hasTopVar && topVarValue > Math.Max(100, request.TotalStock / 4))
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Operaciones: Reducir concentración en {topVarKey} ({topVarValue:N0} bins). Priorizar salida por compromisos comerciales o promociones en los próximos 30 días.",
                        Color = "#3d6fb0",
                        Icon = "Icons.Material.Filled.Inventory2"
                    });
                }

                // 2) Planificación según crecimiento proyectado
                if (request.GrowthPercent > 3m)
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Planificación: Preparar capacidad adicional (turnos/espacio de frío) ante crecimiento proyectado de {request.GrowthPercent:0.0}%. Estimar +{(int)Math.Ceiling(request.TotalStock * 0.05)} bins de capacidad adicional.",
                        Color = "#4caf50",
                        Icon = "Icons.Material.Filled.TrendingUp"
                    });
                }
                else
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Planificación: Mantener capacidad actual y monitorizar la evolución mensual (crecimiento {request.GrowthPercent:0.0}%).",
                        Color = "#4caf50",
                        Icon = "Icons.Material.Filled.CheckCircle"
                    });
                }

                // 3) Comercial/logística para top variedades
                if (request.StockByVariety != null && request.StockByVariety.Count > 0)
                {
                    var tops = request.StockByVariety.OrderByDescending(kv => kv.Value).Take(2);
                    var nombres = string.Join(", ", tops.Select(t => $"{t.Key} ({t.Value:N0} bins)"));
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Comercial/Logística: Contactar clientes potenciales y lanzar ofertas puntuales para las variedades principales: {nombres}. Objetivo: reducir 15-25% del exceso en 30-60 días.",
                        Color = "#2196f3",
                        Icon = "Icons.Material.Filled.LocalActivity"
                    });
                }

                if (!result.Any())
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = "No se detectaron acciones prioritarias con los datos actuales.",
                        Color = "#9e9e9e",
                        Icon = "Icons.Material.Filled.Info"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando sugerencias fallback basadas en datos.");
                return new List<BusinessSuggestionDto>
                {
                    new BusinessSuggestionDto
                    {
                        Text = "No se pudieron generar sugerencias en este momento.",
                        Color = "#9e9e9e",
                        Icon = "Icons.Material.Filled.Info"
                    }
                };
            }
        }
    }
}
'@

# Escribir archivo en UTF-8 sin BOM
[System.IO.File]::WriteAllBytes($fallbackPath, [System.Text.Encoding]::UTF8.GetBytes($fallbackContent))
Write-Host "Escrito: $fallbackPath"

# Limpiar bin/obj
Write-Host "Eliminando carpetas bin/obj..."
Get-ChildItem -Path $root -Recurse -Directory -Include bin,obj -Force -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

# Restaurar y compilar
Write-Host "Restaurando y compilando..."
dotnet restore
dotnet build -v minimal

Write-Host "Script finalizado."