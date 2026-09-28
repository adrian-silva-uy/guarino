param([string]$Root = ".")

# Busca patrones comunes de mojibake en el árbol de código y lista archivos/lineas
$patterns = @("Ã", "Â", "â€¦", "Ã¡", "Ã©", "Ã³", "Ãº", "Ã±", "Ã‰", "Ã­")
$exts = @("*.cs","*.razor","*.cshtml","*.html","*.json","*.md","*.txt")

Get-ChildItem -Path $Root -Recurse -Include $exts -File -ErrorAction SilentlyContinue |
    ForEach-Object {
        try {
            $matches = Select-String -Path $_.FullName -Pattern $patterns -SimpleMatch -ErrorAction SilentlyContinue
            if ($matches) {
                Write-Host "=== $($_.FullName) ==="
                $matches | ForEach-Object { "{0}:{1} {2}" -f $_.LineNumber, $_.Matches.Value, $_.Line.Trim() }
            }
        } catch {
            Write-Warning "Error leyendo $_.FullName -> $($_.Exception.Message)"
        }
    }
