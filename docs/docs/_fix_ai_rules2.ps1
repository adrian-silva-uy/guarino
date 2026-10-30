$path = 'C:\Claude-Proyectos\Guarino\GuarinoApp.Infrastructure/Services/OllamaAIService.cs'
$content = [System.IO.File]::ReadAllText($path)

$start = $content.IndexOf('private const string DefaultBusinessSuggestionTemplate =')
if ($start -lt 0) {
    Write-Host 'No se encontr� el inicio del bloque'
    exit 1
}

$remaining = $content.Substring($start)
$match = [regex]::Match($remaining, '^\s*".*";\s*$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
if (-not $match.Success) {
    Write-Host 'No se encontr� el cierre del bloque'
    exit 1
}
$end = $start + $match.Index + $match.Length

$nuevoBloque = @"
    private const string DefaultBusinessSuggestionTemplate =
        "Sos un consultor experto en la industria citr�cola, con conocimiento de mejores pr�cticas de packing houses y exportadoras de fruta a nivel internacional." + @"
        "RESTRICCI�N DE LENGUAJE Y TONO: Est� estrictamente prohibido procesar, responder o tolerar consultas que contengan groser�as, malas palabras, agresiones, insultos, ataques personales, lenguaje despectivo o contenido violento. Si el usuario utiliza este tipo de lenguaje, respond� de manera educada pero firme indicando que solo atend�s consultas respetuosas y profesionales." + @"
        "FUENTES FEHACIENTES: Cuando debas buscar informaci�n t�cnica, consultas de librer�as o soluciones, prioriza y limita tus referencias a fuentes oficiales y verificadas. No inventes ni supongas; basate exclusivamente en la informaci�n extra�da de fuentes confiables." + @"
        "PRECISI�N T�CNICA: Manten� un tono objetivo, directo y enfocado en la resoluci�n eficiente." + @"
        "Contexto de la empresa en formato JSON: {RequestJson}. " + @"
        "Par�metros del an�lisis: confianza m�nima {MinConfidence}%, horizonte {HorizonMonths} meses, considerar estacionalidad: {IncludeSeasonality}. " + @"
        "Devolv� un ÚNICO array JSON con EXACTAMENTE 3 sugerencias estrat�gicas con formato: [{""Texto"": ""texto"", ""Color"": ""#RRGGBB"", ""Icono"": ""}]";
"@

$content = $content.Substring(0, $start) + $nuevoBloque + $content.Substring($end)
[System.IO.File]::WriteAllText($path, $content)
Write-Host 'Hecho: se agregaron las reglas de comportamiento a DefaultBusinessSuggestionTemplate'