# GenerarResumenClases.ps1
# Escanea el proyecto actual y genera un resumen de clases/interfaces/servicios

Write-Host "Iniciando escaneo de proyecto GuarinoApp..." -ForegroundColor Cyan

# Directorios a escanear (ajusta si tu estructura es diferente)
$Directories = @(
    "GuarinoApp.Domain",
    "GuarinoApp.Application",
    "GuarinoApp.Infrastructure",
    "GuarinoApp.Web"
)

# Resultado acumulado
$result = @{}

foreach ($dir in $Directories) {
    if (Test-Path $dir) {
        Write-Host "Escaneando: $dir" -ForegroundColor Yellow
        
        # Buscar archivos .cs en el directorio y subdirectorios
        $files = Get-ChildItem -Path $dir -Filter "*.cs" -Recurse -File
        
        $classes = @()
        $interfaces = @()
        $services = @()
        
        foreach ($file in $files) {
            try {
                $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                if ($content -eq $null) { continue }
                
                # Detectar Clases (clase MyClass)
                $classMatches = [regex]::Matches($content, 'public\s+(?:abstract\s+)?class\s+(\w+)')
                foreach ($match in $classMatches) {
                    $classes += $match.Groups[1].Value
                }
                
                # Detectar Interfaces (interface IMyInterface)
                $interfaceMatches = [regex]::Matches($content, 'public\s+interface\s+(I\w+)')
                foreach ($match in $interfaceMatches) {
                    $interfaces += $match.Groups[1].Value
                }
                
                # Detectar Servicios comunes (class MyService, class MyService : IService)
                # Busca patrones comunes de servicios en Application e Infrastructure
                $serviceMatches = [regex]::Matches($content, 'public\s+(?:abstract\s+)?class\s+(\w+)(?:\s*:\s*I\w+|\s*\{\s*public\s+\w+\s+\w+\s*\{)')
                foreach ($match in $serviceMatches) {
                    $serviceName = $match.Groups[1].Value
                    # Evitar duplicados y nombres genéricos no deseados
                    if ($serviceName -notmatch "Dto|Command|Query|Request|Response|Validator|Handler|Entity|ViewModel|Model|Page|Component" -and 
                        $serviceName -notmatch "^I") {
                        $services += $serviceName
                    }
                }
                
            } catch {
                # Ignorar errores de lectura
            }
        }
        
        $result[$dir] = @{
            Classes = $classes | Sort-Object -Unique
            Interfaces = $interfaces | Sort-Object -Unique
            Services = $services | Sort-Object -Unique
        }
    } else {
        Write-Host "⚠️  Carpeta no encontrada: $dir" -ForegroundColor Red
    }
}

# Generar salida formateada
Write-Host "`n--- RESUMEN GENERADO PARA CLAUDE ---" -ForegroundColor Green

$output = "# Contexto Actual de Clases - GuarinoApp" + "`n"

foreach ($dir in $result.Keys) {
    $layerData = $result[$dir]
    $output += "`n## $dir" + "`n"
    
    if ($layerData.Classes) {
        $output += "### Clases" + "`n"
        $layerData.Classes | ForEach-Object { $output += "- `$_" + "`n" }
    }
    
    if ($layerData.Interfaces) {
        $output += "`n### Interfaces" + "`n"
        $layerData.Interfaces | ForEach-Object { $output += "- `$_" + "`n" }
    }
    
    if ($layerData.Services) {
        $output += "`n### Servicios" + "`n"
        $layerData.Services | ForEach-Object { $output += "- `$_" + "`n" }
    }
}

$output += "`n**Nota:** Estas clases, interfaces y servicios existen en el proyecto. No solicitar su código a menos que se indique lo contrario."

Write-Host $output -ForegroundColor White

# Opcional: Guardar en un archivo de texto
$fileOutput = "ResumenClases_GuarinoApp.txt"
try {
    $output | Set-Content -Path $fileOutput -Encoding UTF8
    Write-Host "`n✅ Resumen guardado en: $fileOutput" -ForegroundColor Green
} catch {
    Write-Host "⚠️ No se pudo guardar el archivo." -ForegroundColor Red
}