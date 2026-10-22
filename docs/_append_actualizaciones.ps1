# Construye y anexa la seccion 'Actualizaciones recientes (Oct 2026)' a todos los .md de docs/
# usando [char] para acentos (evita mojibake en PowerShell 5.1).
$ErrorActionPreference = 'Stop'
$docs = 'C:\Claude-Proyectos\Guarino\docs'

# Acentos via [char] (ASCII-safe en el .ps1)
$a = [char]0xE1  # a
$e = [char]0xE9  # e
$i = [char]0xED  # i
$o = [char]0xF3  # o
$u = [char]0xFA  # u
$n = [char]0xF1  # n
$em = [char]0x2014  # em dash
$en = [char]0x2013  # en dash
$arrow = [char]0x2192  # right arrow

$sec = @"
---

## Actualizaciones recientes (Oct 2026)

Cambios aplicados al c${o}digo y reflejados en la documentaci${o}n:

- **Filtro de m${o}dulos por rol (UI)**: ``NavMenu.razor`` y ``Index.razor`` muestran los m${o}dulos bloqueados con candado y tooltip "Requiere: X $em tu rol actual: Y" en lugar de ocultarlos. Componentes nuevos: ``Shared/NavLinkSeguro.razor`` y ``Shared/EnlaceSeguro.razor``. Matriz de acceso por reflexi${o}n en ``Services/MapaAccesosRutas.cs`` (lee ``[Authorize]`` de las p${a}ginas $arrow ruta$arrow roles). Helper ``Services/RolUsuario.cs`` resuelve el rol actual con ``RoleNames``. Registrado como singleton en ``Program.cs``.
- **RoleNames como fuente ${u}nica**: constantes ``Admin``/``Gerencia``/``Contador``/``Operador`` + grupos ``Todos``/``Financieros``/``Administracion`` + cadenas ``*Autorizacion`` para ``[Authorize]``. Cero literales de rol en ``.razor``.
- **Paginaci${o}n fija de 25 filas** en las 30 grillas (``RowsPerPage="25"`` + ``MudTablePager PageSizeOptions="new int[] { 25 }"``). Auditor${i}a usa paginaci${o}n server-side (``ServerData`` + ``state.Page/PageSize``).
- **Filtro IA por rol**: ``ConsultaIArestringida`` (log) + matriz de roles por tool MCP.
- **Servidor MCP** (``/mcp``) con ``WithHttpTransport()`` + middleware Bearer propio (``McpAuthMiddleware``); tools solo lectura; auditor${i}a ``McpCall``.
- **Cero datos inventados**: ``GetDummyData``/``GenerarZafrasDummy``/fallbacks eliminados; ``EsDummy`` siempre ``false``; "En nuestro poder" = sin cliente y Estado != Bajado/Despachado.
- **Autenticaci${o}n**: Negotiate + Data Protection tokens (``GuarinoApp.Auth.v1``, 12 h, localStorage ``auth_token``); lockout 5 fallos/15 min.
- **Grillas sin paginador** reemplazado por paginaci${o}n de 25 (instrucci${o}n vigente).
- **Build**: 0 errores / 0 advertencias. UI en espa${n}ol.
- **Diagramas Mermaid**: 40 bloques en ``docs/diagramas.md`` y otros archivos (arquitectura, flujos, seguridad, servicios). Renderizan en GitHub, VS Code (con extensi${o}n), Obsidian.
- **Wiki de usuario**: ``docs/WIKI_USUARIO.md`` con manual funcional por m${o}dulo y rol.
- **Auditor${i}a exhaustiva**: ``docs/auditoria-exhaustiva.md`` (OWASP + UX + deuda t${e}cnica).
- **Documentaci${o}n inversa**: ``docs/documentacion-inversa.md`` (arquitectura, m${o}dulos, decisiones, trampas).
"@

# Escribir helper con UTF-8 (sin BOM)
$helper = Join-Path $docs '_actualizaciones.md'
[System.IO.File]::WriteAllText($helper, $sec, [System.Text.UTF8Encoding]::new($false))

# Verificar
$b = [System.IO.File]::ReadAllBytes($helper)
$txt = [System.Text.Encoding]::UTF8.GetString($b)
Write-Host "P${a}gina present: $($txt.Contains("P${a}gina"))"
Write-Host "mojibake check: $($txt.Contains('estÃ¡n'))"

# Anexar bytes del helper a cada .md (excepto el helper mismo)
$helperBytes = [System.IO.File]::ReadAllBytes($helper)
$files = Get-ChildItem $docs -Filter *.md -Recurse | Where-Object { $_.Name -ne '_actualizaciones.md' }
$updated = 0
foreach ($f in $files) {
  $content = [System.IO.File]::ReadAllText($f.FullName)
  if ($content -notmatch 'Actualizaciones recientes \(Oct 2026\)') {
    # append con salto de linea
    $prefix = "`r`n"
    $prefixBytes = [System.Text.Encoding]::UTF8.GetBytes($prefix)
    $all = New-Object byte[] ($prefixBytes.Length + $helperBytes.Length)
    [Array]::Copy($prefixBytes, 0, $all, 0, $prefixBytes.Length)
    [Array]::Copy($helperBytes, 0, $all, $prefixBytes.Length, $helperBytes.Length)
    $stream = [System.IO.File]::Open($f.FullName, [System.IO.FileMode]::Append)
    $stream.Write($all, 0, $all.Length)
    $stream.Close()
    $updated++
  }
}
Write-Host "archivos actualizados: $updated de $($files.Count)"