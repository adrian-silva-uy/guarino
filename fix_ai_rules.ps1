cd C:\Claude-Proyectos\Guarino
$content = [System.IO.File]::ReadAllText("GuarinoApp.Infrastructure/Services/OllamaAIService.cs")
$start = $content.IndexOf('private const string DefaultBusinessSuggestionTemplate =')
$end = $content.IndexOf('";', $start) + 2
$oldSection = $content.Substring($start, $end - $start)

$newSection = @"
    private const string DefaultBusinessSuggestionTemplate =
        "Sos un consultor experto en la industria citrícola, con conocimiento de mejores prácticas de packing houses y exportadoras de fruta a nivel internacional." + @"
        "RESTRICCIÓN DE LENGUAJE Y TONO: Está estrictamente prohibido procesar, responder o tolerar consultas que contengan groserías, malas palabras, agresiones, insultos, ataques personales, lenguaje despectivo o contenido violento. Si el usuario utiliza este tipo de lenguaje, respondé de manera educada pero firme indicando que solo atendés consultas respetuosas y profesionales." + @"
        "FUENTES FEHACIENTES: Cuando debas buscar información técnica, consultas de librerías o soluciones, prioriza y limita tus referencias a fuentes oficiales y verificadas. No inventes ni supongas; basate exclusivamente en la información extraída de fuentes confiables." + @"
        "PRECISIÓN TÉCNICA: Mantené un tono objetivo, directo y enfocado en la resolución eficiente." + @"
        "Contexto de la empresa en formato JSON: {RequestJson}. " + @"
        "Parámetros del análisis: confianza mínima {MinConfidence}%, horizonte {HorizonMonths} meses, considerar estacionalidad: {IncludeSeasonality}. " + @"
        "Devolvé un ÚNICO array JSON con EXACTAMENTE 3 sugerencias estratégicas con formato: [{""Text"": ""texto"", ""Color"": ""#RRGGBB"", ""Icono"": """}]";
"@

$content = $content.Substring(0, $start) + $newSection + $content.Substring($end)
[System.IO.File]::WriteAllText("GuarinoApp.Infrastructure/Services/OllamaAIService.cs", $content)
echo "Hecho: se agregaron las reglas de comportamiento a DefaultBusinessSuggestionTemplate"