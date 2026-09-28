# ================================================================
# SCRIPT - PUBLICAR APLICACIÓN EN IIS (EJECUTAR CUANDO ESTÉ LISTA)
# ================================================================

Write-Host "🚀 Publicando aplicación..." -ForegroundColor Cyan

# 1. Publicar en carpeta
dotnet publish C:\Guarino\GuarinoApp.Web -c Release -o C:\Publish\GuarinoApp

# 2. Copiar a IIS (ajusta la ruta)
# xcopy /E /Y C:\Publish\GuarinoApp C:\inetpub\wwwroot\GuarinoApp

# 3. Configurar appsettings.Production.json con la conexión real
Write-Host "⚠️ No olvides reemplazar la cadena de conexión en appsettings.Production.json" -ForegroundColor Yellow

Write-Host "✅ Aplicación publicada." -ForegroundColor Green
