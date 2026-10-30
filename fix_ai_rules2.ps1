$path = "C:\Claude-Proyectos\Guarino\GuarinoApp.Infrastructure/Services/OllamaAIService.cs"
$content = [System.IO.File]::ReadAllText($path)

$start = $content.IndexOf('private const string DefaultBusinessSuggestionTemplate =')
if ($start -lt 0) {
    Write-Host "No se encontrÃ³ el inicio del bloque"
    exit 1
}

# Encontrar el cierre ";" de la primera línea del string (después de las comillas finales)
# Buscamos el primer ; que cierre la declaración
$endSearchStart = $start + ('private const string DefaultBusinessSuggestionTemplate ='.Length)
# Buscar el último ; en el bloque, que debería ser al final de la última línea del string
# Buscamos el patrón ";$" al final de una línea
$remaining = $content.Substring($start)
$match = [regex]::Match($remaining, '^\s*".*";\s*$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
if (-not $match.Success) {
    Write-Host "No se encontrÃ³ el cierre del bloque"
    exit 1
}

$end = $start + $match.Index + $match.Length

$oldSection = $content.Substring($start, $end - $start)
Write-Host "SecciÃ³n encontrada:"
Write-Host $oldSection
Write-Host "---"

$newSection = @"
    private const string DefaultBusinessSuggestionTemplate =
        "Sos un consultor experto en la industria citrícola, con conocimiento de mejores pr�cticas de packing houses y exportadoras de fruta a nivel internacional." + @"
        "RESTRICCIÓN DE LENGUAJE Y TONO: Está estrictamente prohibido procesar, responder o tolerar consultas que contengan groserías, malas palabras, agresiones, insultos, ataques personales, lenguaje despectivo o contenido violento. Si el usuario utiliza este tipo de lenguaje, respondé de manera educada pero firme indicando que solo atendés consultas respetuosas y profesionales." + @"
        "FUENTES FEHACIENTES: Cuando debas buscar información técnica, consultas de librerías o soluciones, prioriza y limita tus referencias a fuentes oficiales y verificadas. No inventes ni supongas; basate exclusivamente en la información extraída de fuentes confiables." + @"
        "PRECISIÓN TÉCNICA: Mantené un tono objetivo, directo y enfocado en la resolución eficiente." + @"
        "Contexto de la empresa en formato JSON: {RequestJson}. " + @"
        "Parámetros del análisis: confianza mínima {MinConfidence}%, horizonte {HorizonMonths} meses, considerar estacionalidad: {IncludeSeasonality}. " + @"
        "Devolvé un ÚNICO array JSON con EXACTAMENTE 3 sugerencias estratégicas con formato: [{""Text"": ""texto"", ""Color"": ""#RRGGBB"", ""Icon"": """"}]";
"@

$content = $content.Substring(0, $start) + $newSection + $content.Substring($end)
[System.IO.File]::WriteAllText($path, $content)
Write-Host "Hecho: se agregaron las reglas de comportamiento a DefaultBusinessSuggestionTemplate"