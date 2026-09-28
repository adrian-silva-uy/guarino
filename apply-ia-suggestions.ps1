# apply-ia-suggestions.ps1
# Crea/actualiza archivos necesarios para las sugerencias IA estructuradas,
# modifica Dashboard.razor y compila el proyecto.
# Guarda en UTF-8 sin BOM.

Set-StrictMode -Version Latest

# UTF8 sin BOM encoder
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-FileNoBom([string]$path, [string]$content) {
    $dir = [System.IO.Path]::GetDirectoryName($path)
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($path, $utf8NoBom.GetBytes($content))
    Write-Host "Wrote $path"
}

# Ficheros a crear / reemplazar (rutas relativas desde C:\Guarino)
$files = @{
'GuarinoApp.Application\Common\Dtos\BusinessSuggestionDto.cs' = @'
namespace GuarinoApp.Application.Common.Dtos
{
    /// <summary>
    /// Sugerencia de negocio devuelta por el servicio de IA (estructura simple).
    /// </summary>
    public class BusinessSuggestionDto
    {
        public string? Text { get; set; }
        public string? Color { get; set; }   // Hex o nombre CSS
        public string? Icon { get; set; }    // Nombre icono Material (opcional)
    }

    public class BusinessSuggestionRequest
    {
        public int TotalStock { get; set; }
        public System.Collections.Generic.Dictionary<string,int>? StockByVariety { get; set; }
        public int PalletsWithoutClient { get; set; }
        public decimal GrowthPercent { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
    }
}
'@

'GuarinoApp.Application\Common\Interfaces\IAIService.cs' = @'
using GuarinoApp.Application.Common.Dtos;

namespace GuarinoApp.Application.Common.Interfaces;
public interface IAIService
{
    Task<string> ChatAsync(string prompt, System.Collections.Generic.List<ChatMessage> history);
    bool IsAvailable { get; }

    /// <summary>
    /// Devuelve sugerencias de negocio estructuradas basadas en los datos suministrados.
    /// </summary>
    Task<System.Collections.Generic.List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(BusinessSuggestionRequest request, System.Collections.Generic.List<ChatMessage>? history = null);
}

public class ChatMessage
{
    public string? Text { get; set; }
    public bool IsUser { get; set; }
    public System.DateTime Timestamp { get; set; }
}
'@

'GuarinoApp.Infrastructure\Services\FallbackAIService.cs' = @'
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Infrastructure.Services
{
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

        public async Task<string> ChatAsync(string prompt, System.Collections.Generic.List<ChatMessage> history)
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
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Ollama falló, usando fallback.");
            }

            var request = new BusinessSuggestionRequest
            {
                TotalStock = await _dashboardService.GetTotalStockAsync(System.DateTime.Now),
                StockByVariety = await _dashboardService.GetStockByVarietyAsync(System.DateTime.Now),
                PalletsWithoutClient = await _dashboardService.GetPalletsWithoutClientAsync(System.DateTime.Now),
                GrowthPercent = await _dashboardService.GetProjectedGrowthAsync(System.DateTime.Now),
                PeriodLabel = $"Actualizado: {System.DateTime.Now:dd/MM/yyyy}"
            };

            var suggestions = await GetBusinessSuggestionsAsync(request, history);
            if (suggestions.Any())
                return string.Join("\n", suggestions.Select(s => s.Text));

            return "No pude generar una respuesta en este momento.";
        }

        public async Task<System.Collections.Generic.List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(BusinessSuggestionRequest request, System.Collections.Generic.List<ChatMessage>? history = null)
        {
            try
            {
                var result = new System.Collections.Generic.List<BusinessSuggestionDto>();

                var topVar = request.StockByVariety?.OrderByDescending(kv => kv.Value).FirstOrDefault();

                if (request.PalletsWithoutClient > 0)
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Operaciones: Asignar cliente o priorizar despacho de los {request.PalletsWithoutClient:N0} pallets sin cliente para liberar inventario comercializable y reducir riesgo de inmovilización.",
                        Color = "#ff9800",
                        Icon = "Icons.Material.Filled.AssignmentLate"
                    });
                }
                else if (topVar.Key != null && topVar.Value > System.Math.Max(100, request.TotalStock / 4))
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Operaciones: Reducir concentración en {topVar.Key} ({topVar.Value:N0} bins). Priorizar salida por compromisos comerciales o promociones en los próximos 30 días.",
                        Color = "#3d6fb0",
                        Icon = "Icons.Material.Filled.Inventory2"
                    });
                }

                if (request.GrowthPercent > 3)
                {
                    result.Add(new BusinessSuggestionDto
                    {
                        Text = $"Planificación: Preparar capacidad adicional (turnos/espacio de frío) ante crecimiento proyectado de {request.GrowthPercent:0.0}%. Estimar +{System.Math.Ceiling(request.TotalStock * 0.05)} bins de capacidad adicional.",
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
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error generando sugerencias fallback basadas en datos.");
                return new System.Collections.Generic.List<BusinessSuggestionDto>
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

'GuarinoApp.Infrastructure\Services\OllamaAIService.cs' = @'
using GuarinoApp.Application.Common.Dtos;
using GuarinoApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace GuarinoApp.Infrastructure.Services;

public class OllamaAIService : IAIService
{
    private readonly ILogger<OllamaAIService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDashboardDataService _dashboardService;

    private readonly string _url;
    private readonly string _model;

    private bool _isAvailable;

    public OllamaAIService(
        IConfiguration configuration,
        ILogger<OllamaAIService> logger,
        IHttpClientFactory httpClientFactory,
        IDashboardDataService dashboardService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dashboardService = dashboardService;

        _url = configuration[""Ollama:Url""] ?? ""http://localhost:11434"";
        _model = configuration[""Ollama:Model""] ?? ""qwen2.5:1.5b"";

        _ = Task.Run(CheckAvailabilityAsync);
    }

    public bool IsAvailable => _isAvailable;

    private async Task CheckAvailabilityAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($""{_url}/api/tags"");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(""Ollama no responde en {Url}"", _url);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement.GetProperty(""models"");

            var existe = models
                .EnumerateArray()
                .Any(x => x.GetProperty(""name"").GetString()?.Equals(_model, System.StringComparison.OrdinalIgnoreCase) == true);

            if (!existe)
            {
                _logger.LogWarning(""Modelo {Model} no encontrado"", _model);
                return;
            }

            _isAvailable = true;
            _logger.LogInformation(""Ollama disponible. Modelo {Model}"", _model);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, ""Error verificando Ollama"");
        }
    }

    public async Task<string> ChatAsync(string prompt, System.Collections.Generic.List<ChatMessage> history)
    {
        if (!IsAvailable) return string.Empty;

        try
        {
            var fecha = System.DateTime.Now;
            var totalStock = await _dashboardService.GetTotalStockAsync(fecha);
            var stockVariedad = await _dashboardService.GetStockByVarietyAsync(fecha);

            var system =
$"""
Eres un asistente experto en empacado y logística hortofrutícola.

Datos actuales:
Stock total:
{totalStock} bins

Variedades:
{string.Join(", ", stockVariedad.Keys)}

Responde de forma profesional, con recomendaciones prácticas.
""";

            var messages = new System.Collections.Generic.List<object> { new { role = "system", content = system } };
            foreach (var item in history.TakeLast(10))
            {
                messages.Add(new { role = item.IsUser ? "user" : "assistant", content = item.Text ?? "" });
            }
            messages.Add(new { role = "user", content = prompt });

            var request = new { model = _model, messages, stream = false };
            var client = _httpClientFactory.CreateClient();
            client.Timeout = System.TimeSpan.FromSeconds(60);

            var response = await client.PostAsJsonAsync($""{_url}/api/chat"", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(""Ollama error: {Error}"", error);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            return result?.Message?.Content ?? string.Empty;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, ""Error ejecutando chat Ollama"");
            return string.Empty;
        }
    }

    public async Task<System.Collections.Generic.List<BusinessSuggestionDto>> GetBusinessSuggestionsAsync(BusinessSuggestionRequest request, System.Collections.Generic.List<ChatMessage>? history = null)
    {
        var result = new System.Collections.Generic.List<BusinessSuggestionDto>();
        if (!IsAvailable) return result;

        try
        {
            var system = $"""
Eres un analista operativo senior para una empacadora. Tus decisiones deben basarse únicamente en los datos proporcionados.
Devuelve EXACTAMENTE un JSON que sea un arreglo de 3 objetos con las propiedades:
- text: texto de la sugerencia (acción + justificación + impacto estimado)
- color: color hex o nombre (opcional)
- icon: nombre de icono Material (opcional)

Datos (JSON):
{JsonSerializer.Serialize(request)}

Responde solo con el JSON.
""";

            var messages = new System.Collections.Generic.List<object> { new { role = "system", content = system } };
            if (history != null)
            {
                foreach (var item in history.TakeLast(10))
                    messages.Add(new { role = item.IsUser ? "user" : "assistant", content = item.Text ?? "" });
            }
            messages.Add(new { role = "user", content = "Genera 3 sugerencias en JSON según las instrucciones." });

            var requestObj = new { model = _model, messages, stream = false };
            var client = _httpClientFactory.CreateClient();
            client.Timeout = System.TimeSpan.FromSeconds(60);

            var response = await client.PostAsJsonAsync($""{_url}/api/chat"", requestObj);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(""Ollama error obteniendo sugerencias: {Status}"", response.StatusCode);
                return result;
            }

            var content = await response.Content.ReadAsStringAsync();

            try
            {
                var idx = content.IndexOf('[');
                if (idx -ge 0)
                {
                    var json = content.Substring(idx);
                    var suggestions = JsonSerializer.Deserialize<System.Collections.Generic.List<BusinessSuggestionDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (suggestions != null && suggestions.Any())
                        return suggestions;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, ""No se pudo parsear JSON directo de la respuesta de Ollama."");
            }

            try
            {
                var lines = content.Split([char]10, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries).Where(l => l.Length > 10).Take(3).ToArray();
                foreach (var line in lines)
                    result.Add(new BusinessSuggestionDto { Text = line, Color = "#9c27b0", Icon = "Icons.Material.Filled.Lightbulb" });
            }
            catch { }

            return result;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, ""Error pidiendo sugerencias a Ollama"");
            return new System.Collections.Generic.List<BusinessSuggestionDto>();
        }
    }
}

public class OllamaChatResponse { public OllamaMessage? Message { get; set; } }
public class OllamaMessage { public string? Content { get; set; } }
'@
}

# Escribir archivos
$root = Get-Location
foreach ($rel in $files.Keys) {
    $full = Join-Path $root $rel
    Write-FileNoBom $full $files[$rel]
}

# Modificar Dashboard.razor: insertar @using y reemplazar método RefreshSuggestionsAsync si existe
$dashboardPath = Join-Path $root "GuarinoApp.Web\Pages\Dashboard.razor"
if (Test-Path $dashboardPath) {
    $content = Get-Content $dashboardPath -Raw

    if ($content -notmatch '@using GuarinoApp.Application.Common.Dtos') {
        # insertar después de la primera línea @using o al principio si no hay
        $content = $content -replace '(^@using [^\r\n]*\r?\n)', "`$1@using GuarinoApp.Application.Common.Dtos`n"
    }

    $startToken = 'private async Task RefreshSuggestionsAsync('
    $endToken = '    // --- Exportaciones ---'
    $startIdx = $content.IndexOf($startToken)
    $endIdx = $content.IndexOf($endToken, $startIdx)
    if ($startIdx -ge 0 -and $endIdx -gt $startIdx) {
        $prefix = $content.Substring(0, $startIdx)
        $suffix = $content.Substring($endIdx)
        $newMethod = @'
private async Task RefreshSuggestionsAsync(
        int totalStock,
        int palletsSinCliente,
        decimal crecimiento,
        string mesTexto,
        IReadOnlyDictionary<string, int> stockPorVariedad,
        IReadOnlyList<MonthlyVolcado> volcadosMensuales,
        IReadOnlyList<GuarinoApp.Application.Modules.Proyeccion.Dtos.ZafraDto> proyeccionesZafra)
    {
        var request = new BusinessSuggestionRequest
        {
            TotalStock = totalStock,
            StockByVariety = stockPorVariedad.ToDictionary(kv => kv.Key, kv => kv.Value),
            PalletsWithoutClient = palletsSinCliente,
            GrowthPercent = crecimiento,
            PeriodLabel = mesTexto
        };

        System.Collections.Generic.List<BusinessSuggestionDto> suggestions = new();
        try
        {
            suggestions = await AiService.GetBusinessSuggestionsAsync(request);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning(ex, "Error al solicitar sugerencias IA estructuradas para periodo {Periodo}", mesTexto);
            suggestions = new System.Collections.Generic.List<BusinessSuggestionDto>();
        }

        if (suggestions != null && suggestions.Count > 0)
        {
            _iaSuggestions = suggestions.Select(s => new IASuggestionSimple
            {
                Text = s.Text,
                Color = s.Color ?? "#9c27b0",
                Icon = s.Icon ?? Icons.Material.Filled.Lightbulb
            }).ToList();

            return;
        }

        var ultimoVolcado = volcadosMensuales.LastOrDefault()?.Kgs ?? 0;
        var variedadPrincipal = stockPorVariedad.OrderByDescending(x => x.Value).FirstOrDefault();

        _iaSuggestions = new System.Collections.Generic.List<IASuggestionSimple>
        {
            new()
            {
                Text = palletsSinCliente > 0
                    ? $""Prioridad alta: asignar cliente a {palletsSinCliente:N0} pallets pendientes para liberar inventario comercializable y evitar inmovilización de stock.""
                    : ""Mantener la asignación de clientes al ingreso: no hay pallets pendientes y el inventario está listo para su gestión comercial."",
                Color = palletsSinCliente > 0 ? ""#ff9800"" : ""#4caf50"",
                Icon = palletsSinCliente > 0 ? Icons.Material.Filled.AssignmentLate : Icons.Material.Filled.CheckCircle
            },
            new()
            {
                Text = variedadPrincipal.Key is not null
                    ? $""Planificar rotación y compromisos comerciales para {variedadPrincipal.Key}, la variedad con mayor stock ({variedadPrincipal.Value:N0} bins), para reducir concentración y mejorar la salida.""
                    : ""Registrar el detalle de stock por variedad para habilitar recomendaciones de rotación más precisas."",
                Color = ""#3d6fb0"",
                Icon = Icons.Material.Filled.Inventory2
            },
            new()
            {
                Text = $""Revisar capacidad operativa frente a un crecimiento proyectado de {crecimiento:0.0}% y {ultimoVolcado:N0} kg volcados en el último mes."",
                Color = ""#4caf50"",
                Icon = Icons.Material.Filled.TrendingUp
            }
        };
    }
'@
        $newContent = $prefix + $newMethod + $suffix
        Set-Content -Path $dashboardPath -Value $newContent -Encoding UTF8
        Write-Host "Dashboard.razor modificado."
    }
    else {
        Write-Host "No se encontró el método RefreshSuggestionsAsync en Dashboard.razor; edita manualmente." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Dashboard.razor no encontrado en la ruta esperada: $dashboardPath" -ForegroundColor Red
}

# Limpieza bin/obj
Write-Host "Eliminando carpetas bin/obj..."
Get-ChildItem -Path . -Recurse -Directory -Include bin,obj -Force -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

# Restaurar y compilar
Write-Host "Restaurando y compilando..."
dotnet restore
dotnet build -v minimal

Write-Host "Script finalizado."