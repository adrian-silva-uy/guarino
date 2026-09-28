# ================================================================
# SCRIPT - INSTALAR OLLAMA EN EL SERVIDOR
# ================================================================

Write-Host "🚀 Instalando Ollama en el servidor..." -ForegroundColor Cyan

# 1. Descargar e instalar Ollama (Windows)
Write-Host "📦 Descargando e instalando Ollama..." -ForegroundColor Yellow
 = "https://ollama.com/download/OllamaSetup.exe"
 = "C:\Users\ADRIA\AppData\Local\Temp\OllamaSetup.exe"
Invoke-WebRequest -Uri  -OutFile 
Start-Process -FilePath  -Args "/silent" -Wait
Remove-Item  -Force

# 2. Descargar modelo recomendado (phi3:3.8b)
Write-Host "📦 Descargando modelo phi3:3.8b..." -ForegroundColor Yellow
& "ollama" pull phi3:3.8b

# 3. Verificar que funciona
Write-Host "✅ Verificando instalación..." -ForegroundColor Yellow
& "ollama" list

Write-Host ""
Write-Host "✅ OLLAMA INSTALADO CORRECTAMENTE." -ForegroundColor Green
Write-Host ""
Write-Host "📌 Modelo descargado: phi3:3.8b (recomendado para CPU)" -ForegroundColor White
Write-Host "📌 El servicio de Ollama está corriendo en http://localhost:11434" -ForegroundColor White
Write-Host "📌 Para probar: ollama run phi3:3.8b 'Hola, ¿cómo estás?'" -ForegroundColor White
Write-Host ""
Write-Host "⚙️ Para cambiarlo a otro modelo (ej: llama3.2:3b):" -ForegroundColor Yellow
Write-Host "   ollama pull llama3.2:3b" -ForegroundColor White
Write-Host "   Luego actualiza appsettings.json con el nuevo modelo." -ForegroundColor White
