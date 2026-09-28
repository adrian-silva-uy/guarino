param([string]$Root = ".")

# Convierte archivos de texto a UTF-8 (sin BOM).
# Intenta detectar mojibake: si la decodificación UTF-8 contiene patrones como "Ã¡",
# intenta decodificar como Windows-1252 y reescribir como UTF-8.
# Crea copia de seguridad .bak antes de sobrescribir.

$exts = @("*.cs","*.razor","*.cshtml","*.html","*.json","*.md","*.txt")
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$latin1 = [System.Text.Encoding]::GetEncoding(1252)
$utf8 = [System.Text.Encoding]::UTF8

Get-ChildItem -Path $Root -Recurse -Include $exts -File -ErrorAction SilentlyContinue |
    ForEach-Object {
        $path = $_.FullName
        try {
            $bytes = [System.IO.File]::ReadAllBytes($path)

            # Decodificar como UTF-8
            $textUtf8 = $utf8.GetString($bytes)
            $hasMojibake = $textUtf8 -match "Ã|Â|â€¦|Ã¡|Ã©|Ã³|Ãº|Ã±|Ã‰|Ã­"

            if ($hasMojibake) {
                # Intentar decodificar como Windows-1252 (latin1)
                $textLatin = $latin1.GetString($bytes)

                # Si la versión latin1 no contiene mojibake evidente, usarla
                if (-not ($textLatin -match "Ã|Â|â€¦")) {
                    # Backup
                    Copy-Item -Path $path -Destination ($path + ".bak") -Force
                    [System.IO.File]::WriteAllText($path, $textLatin, $utf8NoBom)
                    Write-Host "Converted (latin1 -> UTF8): $path"
                    continue
                }
            }

            # Si ya era UTF-8 válido o no detectamos mojibake
            # Reescribe en UTF-8 sin BOM para normalizar el encoding
            $detectedText = $textUtf8
            Copy-Item -Path $path -Destination ($path + ".bak") -Force
            [System.IO.File]::WriteAllText($path, $detectedText, $utf8NoBom)
            Write-Host "Normalized to UTF8: $path"
        } catch {
            Write-Warning "Failed to process: $path -> $($_.Exception.Message)"
        }
    }

Write-Host "Conversion completa. Los archivos originales se han copiado con la extensión .bak"
