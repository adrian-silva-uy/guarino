# Generar SVGs placeholder para secciones sin imagen y actualizar markdown
$ErrorActionPreference = 'Stop'
$imgDir = "C:\Claude-Proyectos\Guarino\GuarinoApp.Web\wwwroot\wiki\img"
$mdFile = "C:\Claude-Proyectos\Guarino\docs\WIKI_USUARIO.md"

# Secciones que NO tienen imagen y necesitan una
$seccionesSinImagen = @(
    "1.2 Roles y permisos",
    "2.1 Menú lateral",
    "2.4 Filtrar datos",
    "3.4 Stock en Playa",
    "3.5 Fruta a Volcar",
    "3.7 Registro de Pesadas",
    "3.8 Gestión de Empaque",
    "3.9 Pallets por Contramarca / Definitivos / Sin Clientes",
    "3.12 Proyección de Volcados / Zafra Siguiente",
    "3.13 Reportes",
    "3.14 Logística y Recursos",
    "3.15 Contactos",
    "3.17 Contaduría",
    "3.18 Gerencia",
    "4. Preguntas frecuentes"
)

# Crear SVG para cada sección
foreach ($seccion in $seccionesSinImagen) {
    $nombreArchivo = $seccion -replace '[^a-zA-Z0-9]', '-' -replace '--+', '-' -replace '^-|-$', ''
    $nombreArchivo = $nombreArchivo.ToLower() + ".svg"
    $rutaArchivo = Join-Path $imgDir $nombreArchivo
    
    $svg = @"
<svg width="600" height="300" xmlns="http://www.w3.org/2000/svg">
  <rect width="100%" height="100%" fill="#f5f5f5"/>
  <rect x="50" y="50" width="500" height="200" fill="#e3f2fd" stroke="#1565c0" stroke-width="2" rx="8"/>
  <text x="300" y="140" font-family="Arial, sans-serif" font-size="24" fill="#1565c0" text-anchor="middle" font-weight="bold">$seccion</text>
  <text x="300" y="180" font-family="Arial, sans-serif" font-size="16" fill="#666666" text-anchor="middle">Imagen próximamente</text>
</svg>
"@
    [System.IO.File]::WriteAllText($rutaArchivo, $svg)
    Write-Host "Creado: $nombreArchivo"
}

# Ahora agregar las imágenes al markdown
$mdContent = [System.IO.File]::ReadAllText($mdFile)

foreach ($seccion in $seccionesSinImagen) {
    $nombreArchivo = $seccion -replace '[^a-zA-Z0-9]', '-' -replace '--+', '-' -replace '^-|-$', ''
    $nombreArchivo = $nombreArchivo.ToLower() + ".svg"
    
    # Buscar la línea del encabezado y agregar la imagen después
    $pattern = "^### $([regex]::Escape($seccion))$"
    $reemplazo = "### $seccion`n`n![$seccion](img/$nombreArchivo)`n"
    $mdContent = [regex]::Replace($mdContent, $pattern, $reemplazo)
}

[System.IO.File]::WriteAllText($mdFile, $mdContent)
Write-Host "Markdown actualizado con $($seccionesSinImagen.Count) imágenes nuevas"