# Elimina duplicados en Infrastructure que causan CS0433 y reconstruye
Set-StrictMode -Version Latest
$root = "C:\Guarino"
Set-Location $root

# Archivos duplicados a eliminar (si existen)
$filesToRemove = @(
    "GuarinoApp.Infrastructure\Services\IAIService.cs",
    "GuarinoApp.Infrastructure\Services\BusinessSuggestionDto.cs",
    "GuarinoApp.Infrastructure\Services\BusinessSuggestionRequest.cs"
)

foreach ($rel in $filesToRemove) {
    $full = Join-Path $root $rel
    if (Test-Path $full) {
        Remove-Item $full -Force
        Write-Host "Deleted: $full"
    } else {
        Write-Host "Not found (ok): $full"
    }
}

# Limpieza bin/obj
Write-Host "Eliminando carpetas bin/obj..."
Get-ChildItem -Path $root -Recurse -Directory -Include bin,obj -Force -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

# Restaurar y compilar
Write-Host "Restaurando..."
dotnet restore

Write-Host "Compilando..."
dotnet build -v minimal