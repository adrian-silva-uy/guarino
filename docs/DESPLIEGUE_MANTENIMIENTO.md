# 🚀 Guía de Despliegue y Mantenimiento - GuarinoApp

## Índice
1. [Requisitos Previos](#requisitos-previos)
2. [Despliegue en Desarrollo](#despliegue-en-desarrollo)
3. [Despliegue en Producción](#despliegue-en-producción)
4. [Configuración por Entorno](#configuración-por-entorno)
5. [Migraciones de BD](#migraciones-de-bd)
6. [Monitoreo y Mantenimiento](#monitoreo-y-mantenimiento)
7. [Troubleshooting](#troubleshooting)
8. [Rollback](#rollback)

---

## Requisitos Previos

### Hardware (Servidor)
- **CPU**: 4+ cores
- **RAM**: 8+ GB
- **Almacenamiento**: 100+ GB (incluir BD)
- **Ancho de banda**: 10+ Mbps

### Software
- **.NET 8 SDK** (desarrollo) / Runtime (producción)
- **SQL Server 2019+** (o SQL Azure)
- **Windows Server 2019+** / IIS 10+
- **Git** (para control de versiones)

### Credenciales Necesarias
- Acceso a repositorio GitHub
- Acceso a SQL Server
- Cuenta de email SMTP (para notificaciones)
- API keys para servicios externos:
  - OpenMeteo (gratis, no requiere key)
  - Ollama (si está hosteado localmente)

---

## Despliegue en Desarrollo

### 1. Clonar Repositorio

```bash
# Clonar proyecto
git clone https://github.com/adrian-silva-uy/guarino.git
cd guarino

# Verificar rama
git status
# En rama: master
```

### 2. Restaurar Dependencias

```bash
# Restaurar NuGet packages
dotnet restore

# Verificar compilación
dotnet build
# Build success ✅
```

### 3. Configurar Base de Datos

```bash
# Navegar a proyecto Infrastructure
cd GuarinoApp.Infrastructure

# Crear/actualizar BD (LocalDB)
dotnet ef database update

# Verificar migraciones
dotnet ef migrations list
# Output: 001_Initial, 002_AddAuditLog, ...
```

### 4. Configurar appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=GuarinoApp_Dev;Trusted_Connection=true"
  },
  "ClimateRiskSettings": {
    "Enabled": true,
    "CheckIntervalMinutes": 60,
    "TemperatureThresholdCelsius": 35,
    "HumidityThresholdPercent": 30
  },
  "OllamaOptions": {
    "Url": "http://localhost:11434",
    "TimeoutSeconds": 300,
    "Model": "llama2"
  },
  "EmailOptions": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "test@gmail.com",
    "Username": "test@gmail.com",
    "Password": "app_password_here"
  }
}
```

### 5. Ejecutar Aplicación

```bash
# En directorio raíz
cd GuarinoApp.Web
dotnet run

# Salida esperada:
# info: GuarinoApp.Web.Program[0]
#     Application started. Press Ctrl+C to exit.
# info: GuarinoApp.Web.Program[0]
#     Hosting environment: Development
# info: GuarinoApp.Web.Program[0]
#     Content root path: C:\...\GuarinoApp.Web
```

**Acceder**: http://localhost:5000

---

## Despliegue en Producción

### Arquitectura de Despliegue

```mermaid
graph TB
    Users["👥 Usuarios"]
    LoadBalancer["⚙️ Load Balancer"]

    subgraph WebServers["🖥️ Web Servers (IIS)"]
        IIS1["IIS-01<br/>GuarinoApp v1.0"]
        IIS2["IIS-02<br/>GuarinoApp v1.0"]
        IIS3["IIS-03<br/>GuarinoApp v1.0"]
    end

    subgraph Database["💾 SQL Server"]
        Primary["Primary DB<br/>guarino_prod"]
        Secondary["Secondary DB<br/>(Replica)"]
    end

    subgraph External["🌐 Servicios Externos"]
        OpenMeteo["OpenMeteo API"]
        Ollama["Ollama Server<br/>192.168.x.x:11434"]
        Email["SMTP Server"]
    end

    Monitoring["📊 Monitoring<br/>Application Insights"]
    Backup["💿 Backups Automatizados"]

    Users -->|HTTPS| LoadBalancer
    LoadBalancer --> IIS1
    LoadBalancer --> IIS2
    LoadBalancer --> IIS3

    IIS1 --> Primary
    IIS2 --> Primary
    IIS3 --> Primary

    Primary -.->|Sincronización| Secondary

    IIS1 --> OpenMeteo
    IIS1 --> Ollama
    IIS1 --> Email

    Primary --> Monitoring
    IIS1 --> Monitoring

    Primary --> Backup

    style Users fill:#e3f2fd
    style LoadBalancer fill:#fff9c4
    style WebServers fill:#c8e6c9
    style Database fill:#ffccbc
    style External fill:#e1f5fe
    style Monitoring fill:#f8bbd0
    style Backup fill:#d1c4e9
```

### Paso 1: Preparación del Servidor

```bash
# 1. Instalar .NET 8 Runtime
# Descargar desde https://dotnet.microsoft.com/download/dotnet

# 2. Verificar instalación
dotnet --version
# Output: 8.0.x

# 3. Instalar IIS (en Windows Server)
dism /online /enable-feature /featurename:IIS-WebServer /all

# 4. Instalar módulo ASP.NET Core Hosting
# Descargar y ejecutar: dotnet-hosting-8.x.x-win.exe

# 5. Reiniciar servidor
shutdown /r /t 0
```

### Paso 2: Preparar Base de Datos

```bash
# En servidor SQL Server:

# 1. Crear BD
CREATE DATABASE GuarinoApp_Prod;
GO

# 2. Crear usuario de aplicación
CREATE LOGIN GuarinoApp WITH PASSWORD = 'StrongPassword123!';
GO

CREATE USER GuarinoApp FOR LOGIN GuarinoApp;
GO

# 3. Asignar permisos
ALTER ROLE db_owner ADD MEMBER GuarinoApp;
GO

# 4. Habilitar cifrado
ALTER DATABASE GuarinoApp_Prod SET ENCRYPTION ON;
GO

# 5. Configurar backups
-- Backup automático cada día a las 2 AM
BACKUP DATABASE GuarinoApp_Prod 
TO DISK = 'C:\Backups\GuarinoApp_Prod_Daily.bak'
WITH COMPRESSION;
GO
```

### Paso 3: Compilar Release

```bash
# En máquina de desarrollo

# Limpiar y compilar
dotnet clean
dotnet publish -c Release -o ./publish

# Salida:
# C:\...\publish\
# ├── GuarinoApp.Web.exe
# ├── appsettings.json
# ├── web.config
# └── (más archivos)
```

### Paso 4: Desplegar en IIS

```bash
# 1. Crear sitio en IIS Manager

# PowerShell (como Admin):
New-IISSite -Name "GuarinoApp" `
    -BindingInformation "*:443:guarino.company.com:https" `
    -PhysicalPath "C:\inetpub\guarino" `
    -ApplicationPool ".NET AppPool v4.0"

# 2. Copiar archivos publicados
Copy-Item -Path ".\publish\*" -Destination "C:\inetpub\guarino" -Recurse

# 3. Configurar permisos
icacls "C:\inetpub\guarino" /grant:r "IIS AppPool\GuarinoApp:(OI)(CI)F"

# 4. Instalar certificado SSL
# (Usar Let's Encrypt o certificado interno)
```

### Paso 5: Configurar appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Error"
    },
    "ApplicationInsights": {
      "Enabled": true,
      "InstrumentationKey": "YOUR_KEY_HERE"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver.internal;Database=GuarinoApp_Prod;User=GuarinoApp;Password=StrongPassword123!;Encrypt=true;TrustServerCertificate=false"
  },
  "ClimateRiskSettings": {
    "Enabled": true,
    "CheckIntervalMinutes": 60
  },
  "OllamaOptions": {
    "Url": "http://192.168.1.100:11434",
    "TimeoutSeconds": 300,
    "Model": "llama2"
  },
  "EmailOptions": {
    "SmtpServer": "smtp.office365.com",
    "SmtpPort": 587,
    "FromEmail": "noreply@guarino.com.uy",
    "Username": "noreply@guarino.com.uy",
    "Password": "SecurePasswordHere"
  },
  "RateLimiting": {
    "Enabled": true,
    "PermitLimit": 100,
    "WindowMinutes": 1
  }
}
```

### Paso 6: Migraciones de BD

```bash
# En servidor de BD:

# Opción A: Usar dotnet ef (si .NET 8 instalado)
dotnet ef database update --configuration Release

# Opción B: Generar script SQL (Recomendado)
dotnet ef migrations script -o migrate_prod.sql

# Luego ejecutar SQL script en SQL Management Studio
```

### Paso 7: Verificar Despliegue

```bash
# 1. Acceder a aplicación
# https://guarino.company.com

# 2. Verificar logs de IIS
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\*.log" -Tail 20

# 3. Verificar conectividad BD
# En IIS Manager > Aplicación > Advanced Settings > Test Connection

# 4. Probar servicios externos
# Abrir navegador > Consola de desarrollador > Network
# Verificar que OpenMeteo responde
```

---

## Configuración por Entorno

### Archivo appsettings.json Base

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### Variables de Entorno (Recomendado)

```bash
# En servidor (Windows CMD):
setx ASPNETCORE_ENVIRONMENT Production
setx ConnectionStrings__DefaultConnection "Server=...;Password=..."
setx OllamaOptions__Url "http://localhost:11434"
setx EmailOptions__Password "app_password"

# En servidor (PowerShell):
[Environment]::SetEnvironmentVariable(
    "ASPNETCORE_ENVIRONMENT", 
    "Production", 
    "Machine"
)
```

### Secret Manager (Desarrollo)

```bash
# Inicializar secrets
dotnet user-secrets init

# Establecer secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Password=..."
dotnet user-secrets set "EmailOptions:Password" "app_password"

# Verificar
dotnet user-secrets list
```

### Azure Key Vault (Recomendado para Producción)

```csharp
// Program.cs
var keyVaultUrl = new Uri(builder.Configuration["KeyVault:Url"]);
var credential = new DefaultAzureCredential();
builder.Configuration.AddAzureKeyVault(keyVaultUrl, credential);
```

---

## Migraciones de BD

### Crear Nueva Migración

```bash
# 1. Realizar cambios en entidades (Domain)
// Ejemplo: Agregar propiedad a AppUser
public class AppUser
{
    // ...
    public DateTime? LastPasswordChange { get; set; }  // NUEVA
}

# 2. Crear migración
dotnet ef migrations add AddLastPasswordChangeToAppUser

# 3. Revisar archivo generado
# GuarinoApp.Infrastructure/Migrations/xxx_AddLastPasswordChangeToAppUser.cs

# 4. Aplicar localmente
dotnet ef database update

# 5. Generar script SQL
dotnet ef migrations script -o add_password_change.sql

# 6. En producción: Ejecutar script en SQL Management Studio
```

### Migración Segura en Producción

```sql
-- 1. BACKUP PREVIO
BACKUP DATABASE GuarinoApp_Prod 
TO DISK = 'C:\Backups\GuarinoApp_Prod_BeforeMigration.bak';
GO

-- 2. Ejecutar migración dentro de transacción
BEGIN TRANSACTION;

-- Comando de migración EF o SQL directo
ALTER TABLE AppUsers
ADD LastPasswordChange DATETIME NULL;

COMMIT TRANSACTION;
-- Si hay error: ROLLBACK TRANSACTION;

-- 3. Verificar
SELECT TOP 1 * FROM AppUsers;
```

### Rollback de Migración

```bash
# Local: Revertir última migración
dotnet ef migrations remove

# Producción: Restaurar BD desde backup
# En SQL Management Studio:
# Restore Database > Select Backup > Proceed
```

---

## Monitoreo y Mantenimiento

### Monitoreo de Rendimiento

```csharp
// Program.cs - Agregar Application Insights
builder.Services.AddApplicationInsightsTelemetry();
```

**Métricas a Monitorear**:
- 📊 Tiempo de respuesta promedio
- 🔴 Tasa de errores (> 1%)
- 💾 Uso de memoria (> 80%)
- 🌐 Conexiones activas a BD
- 📈 Solicitudes por segundo

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(new Uri("https://api.open-meteo.com/v1/forecast"))
    .AddCheck("Ollama", 
        async () => 
        {
            var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode 
                ? HealthCheckResult.Healthy() 
                : HealthCheckResult.Unhealthy();
        });

app.MapHealthChecks("/health");
```

**Acceder**: https://guarino.company.com/health

### Logs Centralizados

```csharp
// Configurar Serilog (recomendado)
builder.Host.UseSerilog((context, config) =>
{
    config
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.File("logs/guarino-.txt", 
            rollingInterval: RollingInterval.Day)
        .WriteTo.ApplicationInsights(new TelemetryConverter());
});
```

### Mantenimiento Preventivo

| Tarea | Frecuencia | Responsable |
|-------|-----------|-------------|
| Backup BD | Diario | DevOps |
| Logs limpieza | Semanal | DevOps |
| Parches OS | Mensual | IT |
| Renovación certificados | Anual | IT |
| Auditoría de seguridad | Trimestral | Security |
| Load testing | Semestral | QA |

---

## Troubleshooting

### Problema: BD no accesible

```
Error: A network-related or instance-specific error 
occurred while establishing a connection to SQL Server.
```

**Diagnóstico**:
```powershell
# Verificar conectividad
Test-NetConnection -ComputerName sqlserver.internal -Port 1433

# Verificar connection string
$connStr = "Server=sqlserver.internal;Database=GuarinoApp_Prod;"
sqlcmd -S sqlserver.internal -U GuarinoApp

# Verificar permisos
SELECT * FROM fn_my_permissions (NULL, 'DATABASE');
```

**Solución**:
1. Verificar SQL Server está online
2. Verificar credenciales en appsettings
3. Verificar firewall permite puerto 1433
4. Verificar usuario tiene permisos

---

### Problema: Aplicación lenta

```
Tiempo de respuesta > 3 segundos
```

**Diagnóstico**:
```csharp
// Habilitar logging detallado
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
});

// Revisar logs para queries lentas
// Buscar: "Executing DbCommand with timeout"
```

**Soluciones**:
1. Optimizar queries (índices, proyecciones)
2. Habilitar caching
3. Aumentar connection pool
4. Revisar recursos del servidor

---

### Problema: Alto uso de memoria

```
Memory > 80% de límite
```

**Diagnóstico**:
```powershell
# Ver procesos
Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 5

# Ver memoria por IIS app pool
Get-IISAppPool | Select-Object -Property Name, Recycling

# Memory dump
$proc = Get-Process "w3wp"
# Crear dump con Windows Task Manager o WinDbg
```

**Soluciones**:
1. Reducir cache size
2. Configurar AppPool recycling
3. Revisar memory leaks en código
4. Aumentar RAM del servidor

---

### Problema: Error de autenticación

```
401 Unauthorized - Windows Auth falla
```

**Diagnóstico**:
```powershell
# Verificar Kerberos
setspn -L DOMAIN\usuario

# Verificar IIS
Test-IISAppPoolIdentity -AppPoolName "GuarinoApp"

# Verificar credential
whoami /priv
```

**Soluciones**:
1. Configurar SPN (Service Principal Name)
2. Reiniciar app pool
3. Verificar usuario está en grupo AD

---

## Rollback

### Rollback de Aplicación

```bash
# 1. Mantener versión anterior en C:\inetpub\guarino_backup\

# 2. Si deployment falla, copiar versión anterior
Copy-Item -Path "C:\inetpub\guarino_backup\*" `
    -Destination "C:\inetpub\guarino" -Recurse -Force

# 3. Reiniciar IIS
Restart-WebAppPool -Name "GuarinoApp"

# 4. Verificar aplicación
# Acceder a https://guarino.company.com
```

### Rollback de BD

```sql
-- Restaurar desde backup
RESTORE DATABASE GuarinoApp_Prod
FROM DISK = 'C:\Backups\GuarinoApp_Prod_Daily.bak'
WITH REPLACE;
GO

-- Verificar integridad
DBCC CHECKDB (GuarinoApp_Prod);
GO
```

### Procedimiento de Rollback Completo

```bash
#!/bin/bash
# Script: rollback.sh

set -e

echo "🔄 Iniciando rollback..."

# 1. Parar aplicación
iisreset /stop

# 2. Restaurar código
cp -r /backup/guarino_v1.0/* /inetpub/guarino/

# 3. Restaurar BD
sqlcmd -S sqlserver -U GuarinoApp <<EOF
  RESTORE DATABASE GuarinoApp_Prod
  FROM DISK = '/backup/GuarinoApp_Prod.bak'
  WITH REPLACE
EOF

# 4. Iniciar aplicación
iisreset /start

echo "✅ Rollback completado"
echo "📧 Notificando al equipo..."
```

---

## Checklist de Despliegue

### Pre-Despliegue
- [ ] Código revisado y aprobado (PR)
- [ ] Tests unitarios pasados
- [ ] Build en Release mode exitoso
- [ ] Migraciones de BD revisadas
- [ ] Secrets configurados en servidor
- [ ] Backup de BD creado
- [ ] Plan de rollback documentado

### Despliegue
- [ ] Aplicación copiada a servidor
- [ ] Permisos de carpeta configurados
- [ ] Migraciones de BD ejecutadas
- [ ] Sitio IIS iniciado
- [ ] Verificación de health checks
- [ ] Acceso a aplicación confirmado

### Post-Despliegue
- [ ] Monitoreo de logs activo
- [ ] Tests de humo en producción
- [ ] Notificación a usuarios
- [ ] Documentación actualizada
- [ ] Métricas de rendimiento revisadas
- [ ] Ambiente preparado para próximo despliegue

---

**Última actualización**: 2024-01-XX  
**Responsable**: DevOps Team  
**Versión**: 1.0
