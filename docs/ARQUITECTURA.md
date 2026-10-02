# 📋 Arquitectura GuarinoApp.Web - Wiki Completa

## Índice
1. [Descripción General](#descripción-general)
2. [Arquitectura de Capas](#arquitectura-de-capas)
3. [Componentes Blazor](#componentes-blazor)
4. [Flujo de Datos](#flujo-de-datos)
5. [Seguridad](#seguridad)
6. [Servicios y Utilidades](#servicios-y-utilidades)
7. [Base de Datos](#base-de-datos)
8. [Auditoría](#auditoría)
9. [Despliegue](#despliegue)

---

## Descripción General

**GuarinoApp** es una aplicación web Blazor Server (.NET 8) para gestión de operaciones citríolas. Implementa una arquitectura **Clean Architecture** con separación clara de capas y responsabilidades.

### Stack Tecnológico
- **Framework**: ASP.NET Core 8 + Blazor Server
- **BD**: SQL Server (Entity Framework Core 8)
- **Autenticación**: Windows Authentication (Negotiate)
- **UI**: MudBlazor + Bootstrap
- **Comunicación**: MediatR (CQRS)
- **Servicios Externos**: OpenMeteo (clima), Ollama (IA)

### Características Principales
- 📊 Dashboards ejecutivos con KPIs
- 🤖 Asistente IA integrado (Ollama)
- 🌦️ Monitoreo climático y análisis de riesgos
- 📦 Gestión de stock y pallets
- 💰 Módulo de contaduría y liquidaciones
- 👥 Control de usuarios y auditoría
- 📄 Exportación a Excel y PDF

---

## Arquitectura de Capas

```mermaid
graph TB
    UI["🎨 Presentación<br/>(GuarinoApp.Web)<br/>Blazor Server, MudBlazor"]

    APP["🔧 Aplicación<br/>(GuarinoApp.Application)<br/>MediatR, CQRS, Validaciones"]

    DOMAIN["📦 Dominio<br/>(GuarinoApp.Domain)<br/>Entidades, Reglas de Negocio"]

    INFRA["💾 Infraestructura<br/>(GuarinoApp.Infrastructure)<br/>EF Core, Servicios Externos"]

    UI -->|Mediator| APP
    APP -->|Usa| DOMAIN
    APP -->|Implementa| INFRA
    INFRA -->|Accede| DB[(SQL Server)]
    INFRA -->|Consume| EXT["🌐 Externos<br/>OpenMeteo, Ollama"]

    style UI fill:#e1f5ff
    style APP fill:#f3e5f5
    style DOMAIN fill:#e8f5e9
    style INFRA fill:#fff3e0
    style DB fill:#fce4ec
    style EXT fill:#ede7f6
```

### Descripción de Capas

| Capa | Responsabilidad | Proyecto |
|------|-----------------|----------|
| **Presentación** | Componentes Blazor, UI, enrutamiento | `GuarinoApp.Web` |
| **Aplicación** | Lógica de negocio, CQRS, validaciones | `GuarinoApp.Application` |
| **Dominio** | Entidades, interfaces, reglas core | `GuarinoApp.Domain` |
| **Infraestructura** | BD, servicios externos, implementaciones | `GuarinoApp.Infrastructure` |

---

## Componentes Blazor

### Jerarquía de Componentes

```mermaid
graph TD
    App["<b>App.razor</b><br/>CascadingAuthenticationState<br/>AuthorizeRouteView"]

    Layout["<b>MainLayout.razor</b><br/>MudLayout<br/>Drawer + AppBar"]

    NavMenu["<b>NavMenu.razor</b><br/>Menú contextual por roles"]

    Pages["<b>Pages/</b><br/>30+ componentes de página"]

    Shared["<b>Shared Components</b><br/>Reutilizables"]

    App --> Layout
    Layout --> NavMenu
    Layout --> Pages
    Pages --> Shared

    Shared --> |"CurrentUserDisplay.razor"| SD1["Muestra usuario actual"]
    Shared --> |"ExportToolbar.razor"| SD2["Botones Excel/PDF"]
    Shared --> |"InteractiveChart.razor"| SD3["Gráficos"]
    Shared --> |"SimpleDialog.razor"| SD4["Diálogos genéricos"]
    Shared --> |"SendEmailDialog.razor"| SD5["Envío de emails"]
    Shared --> |"SurveyPrompt.razor"| SD6["Feedback"]
    Shared --> |"DataSourceFooter.razor"| SD7["Info de datos"]

    style App fill:#bbdefb
    style Layout fill:#c8e6c9
    style Pages fill:#ffe0b2
    style Shared fill:#f0f4c3
```

### Páginas Principales por Módulo

#### 🏠 Inicio
- `Index.razor` - Página de bienvenida
- `Dashboard.razor` - Dashboard ejecutivo (KPIs, gráficos)

#### 📦 Operación Diaria (Todos los roles)
- `StockGeneral.razor` - Stock general de producto
- `StockPlaya.razor` - Stock en playa
- `FrutaVolcar.razor` - Fruta pendiente de volcado
- `PalletsSinCliente.razor` - Pallets sin asignación
- `LotesVolcados.razor` - Histórico de lotes
- `RegistroPesadas.razor` - Registro de balanzas
- `Empaque.razor` - Gestión de empaque

#### 🤖 Inteligencia Artificial
- `ProyeccionVolcados.razor` - Proyección de volcados (MLP)
- `ProyeccionZafra.razor` - Análisis estratégico de zafra siguiente
- `IAChat.razor` - Chat con asistente IA (Ollama)

#### 📊 Reportes y Análisis
- `ResumenMensual.razor` - Resumen mensual
- `ResumenLiquidaciones.razor` - Liquidaciones de operarios
- `LiquidacionesQuinta.razor` - Liquidaciones de quinta
- `GraficaProcesos.razor` - Visualización de procesos
- `PalletsContramarca.razor` - Análisis por contramarca
- `PalletsDefinitivos.razor` - Pallets finales (exportación)
- `GestionRiesgosClimaticos.razor` - Riesgos climáticos

#### 🚛 Logística y Recursos
- `Transportistas.razor` - Gestión de transportistas
- `CamionesZorras.razor` - Vehículos
- `Lugares.razor` - Orígenes y destinos
- `Basculas.razor` - Básculas de pesaje

#### 👥 Administración
- `Usuarios.razor` - Gestión de usuarios
- `Auditoria.razor` - Logs de auditoría
- `Contaduria.razor` - Módulo contable

---

## Flujo de Datos

### Ciclo CQRS Principal

```mermaid
sequenceDiagram
    participant U as Usuario
    participant B as Componente Blazor
    participant M as MediatR
    participant H as Query/CommandHandler
    participant R as Repository
    participant DB as SQL Server

    U->>B: Interacción (clic, filtro)
    B->>M: Send(Query/Command)
    M->>H: Ejecuta Handler
    H->>R: GetData/Modificar
    R->>DB: SQL
    DB->>R: Datos
    R->>H: DTO
    H->>M: Resultado
    M->>B: IAsyncEnumerable/Objeto
    B->>U: Renderiza UI
```

### Ejemplo: Obtener Pallets

```csharp
// 1. Componente Blazor dispara
await Mediator.Send(new GetPalletsQuery { 
    Skip = 0, 
    Take = 10 
});

// 2. MediatR enruta a GetPalletsQueryHandler
// 3. Handler consulta IApplicationDbContext
// 4. Repository devuelve IAsyncEnumerable<PalletDto>
// 5. Componente renderiza DataGrid
```

### Inyección de Dependencias

```mermaid
graph LR
    DI["Contenedor DI<br/>(Program.cs)"]

    DI --> A1["AddServerSideBlazor()"]
    DI --> A2["AddApplication()"]
    DI --> A3["AddInfrastructure()"]
    DI --> A4["AddMudServices()"]
    DI --> A5["AddBlazoredLocalStorage()"]
    DI --> A6["AddMemoryCache()"]

    A2 --> M1["MediatR handlers"]
    A2 --> M2["Validadores"]
    A3 --> I1["IApplicationDbContext"]
    A3 --> I2["IClimateService"]
    A3 --> I3["IEmailService"]
    A3 --> I4["IAIService"]
    A3 --> I5["HostedServices"]

    style DI fill:#ffccbc
    style A1 fill:#fff9c4
    style A2 fill:#c8e6c9
    style A3 fill:#b3e5fc
```

---

## Seguridad

### Modelo de Autenticación

```mermaid
graph TB
    subgraph Auth["🔐 Autenticación"]
        WA["Windows Auth<br/>(Negotiate)"]
        CSS["CascadingAuthenticationState"]
        ARV["AuthorizeRouteView"]
    end

    subgraph Authz["✅ Autorización"]
        Roles["Roles: Admin, Gerencia,<br/>Contador, Operador"]
        AA["@attribute [Authorize]"]
        AV["AuthorizeView"]
    end

    subgraph User["👤 Usuario"]
        US["ICurrentUserService"]
        AuthState["AuthenticationState"]
    end

    WA --> CSS
    CSS --> ARV
    Roles --> AA
    Roles --> AV
    AA --> ARV
    AV --> ARV
    CSS --> AuthState
    AuthState --> US

    style Auth fill:#ffebee
    style Authz fill:#e8f5e9
    style User fill:#e3f2fd
```

### Jerarquía de Roles

| Rol | Permisos |
|-----|----------|
| **Admin** | Acceso total. Gestión de usuarios, auditoría. |
| **Gerencia** | Dashboard ejecutivo, análisis, reportes, algunos datos administrativos. |
| **Contador** | Contaduría, liquidaciones, proyecciones. |
| **Operador** | Datos operativos diarios, stocks. |

### Protecciones de Seguridad

#### 1. **Control de Acceso por Rol**
```razor
@attribute [Authorize(Roles = "Admin, Gerencia")]
<AuthorizeView Roles="Admin, Gerencia">
    <Authorized>
        <!-- Contenido protegido -->
    </Authorized>
    <NotAuthorized>
        <MudAlert Severity="Severity.Warning">
            No tenés permiso para acceder a esta sección.
        </MudAlert>
    </NotAuthorized>
</AuthorizeView>
```

#### 2. **Sanitización de Entrada**
```csharp
// GuarinoApp.Application/Common/Security/InputSanitizer.cs
public class InputSanitizer
{
    public static string Sanitize(string input)
    {
        // Remove HTML tags, scripts, etc.
    }
}

// Aplicado automáticamente vía InputSanitizationBehavior
```

#### 3. **Rate Limiting**
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});
```

#### 4. **Validación de Datos**
```csharp
// FluentValidation + MediatR behaviors
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
```

#### 5. **HTTPS Forzado**
```csharp
// Program.cs
app.UseHsts();
app.UseHttpsRedirection();
```

---

## Servicios y Utilidades

### Servicios de Dominio

```mermaid
graph TB
    ICS["IClimateService<br/>Interface"]
    OCS["OpenMeteoClimateService<br/>Implementación"]
    CRS["IClimateRiskService<br/>Interface"]
    CRSI["ClimateRiskService<br/>Implementación"]

    CM["ClimateMonitoringHostedService<br/>Escaneo periódico"]

    ICS --> OCS
    OCS --> |"Llama"| OM["OpenMeteo API"]

    CRS --> CRSI
    CRSI --> |"Usa"| ICS

    CM --> |"Ejecuta"| CRSI

    style ICS fill:#c8e6c9
    style OCS fill:#a5d6a7
    style CRS fill:#c8e6c9
    style CRSI fill:#a5d6a7
    style CM fill:#fff9c4
```

### Servicios de Aplicación

| Servicio | Responsabilidad |
|----------|-----------------|
| `IExportService` | Exportación a Excel (.xlsx) y PDF |
| `IEmailService` | Envío de correos electrónicos |
| `IAIService` | Integración con Ollama para análisis |
| `IDashboardDataService` | Cálculos y KPIs para dashboards |
| `IAuthorizationService` | Validación de permisos de usuario |
| `ICurrentUserService` | Información del usuario autenticado |
| `IAuditService` | Registro de auditoría |
| `IPdfGenerator` | Generación de reportes PDF |

### Servicios Globales (HostedServices)

```csharp
// Program.cs
builder.Services.AddHostedService<ClimateMonitoringHostedService>();
```

**ClimateMonitoringHostedService**
- Ejecuta periódicamente (ej: cada 1 hora)
- Consulta OpenMeteo para datos climáticos
- Evalúa riesgos climáticos
- Genera alertas si es necesario
- Almacena eventos en BD

---

## Base de Datos

### Contexto de EF Core

```csharp
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<AppUser> Users { get; set; }
    public DbSet<Pallet> Pallets { get; set; }
    public DbSet<Stock> Stock { get; set; }
    public DbSet<EventoClimaticoDto> EventosClimaticos { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    // ... más DbSets
}
```

### Tablas Principales

```mermaid
erDiagram
    APPUSER ||--o{ AUDITLOG : "realiza"
    APPUSER {
        int Id
        string Email
        string PasswordHash
        string[] Roles
        bool IsActive
        datetime CreatedAt
        datetime LastLogin
    }

    PALLET ||--o{ STOCK : "pertenece"
    PALLET {
        int Id
        string Numero
        int ProductoId
        datetime FechaCreacion
        decimal Peso
    }

    STOCK {
        int Id
        int PalletId
        string Ubicacion
        int Cantidad
    }

    EVENTOCLIMATICO {
        int Id
        string Tipo
        decimal Intensidad
        datetime Fecha
        string Ubicacion
    }

    AUDITLOG {
        int Id
        int UserId
        string Accion
        string Entidad
        datetime Timestamp
        string Cambios
    }
```

### Configuración de Conexión

```csharp
// Program.cs - Infrastructure/DependencyInjection.cs
var connectionString = configuration.GetConnectionString("DefaultConnection");
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
);
```

---

## Auditoría

### Sistema de Auditoría Integral

```mermaid
graph TB
    AC["Acción del Usuario"]
    BH["Behavior/Pipeline<br/>MediatR"]
    AS["IAuditService"]
    LA["LogActionCommand"]
    DB[(Base de Datos<br/>AuditLog)]

    AC -->|"Trigger"| BH
    BH -->|"Registra"| AS
    AS -->|"Send"| LA
    LA -->|"Persist"| DB

    DB -->|"Query"| AU["Auditoria.razor"]

    style AC fill:#ffccbc
    style BH fill:#fff9c4
    style AS fill:#c8e6c9
    style LA fill:#bbdefb
    style DB fill:#f8bbd0
```

### Información Registrada

Cada entrada de auditoría incluye:
- `UserId` - ID del usuario
- `Accion` - Tipo de operación (Create, Update, Delete, Query)
- `Entidad` - Nombre de la entidad afectada
- `Id Entidad` - ID del registro
- `Cambios` - JSON con cambios antes/después
- `Timestamp` - Momento exacto
- `Dirección IP` - Del cliente
- `User Agent` - Navegador/app

### Consulta de Auditoría

```razor
@page "/auditoria"
@attribute [Authorize(Roles = "Admin, Gerencia")]

@* Página que lista todos los cambios del sistema *@
<MudDataGrid Items="logs" Filterable="true" Sortable="true">
    <!-- Mostrar Usuario, Acción, Entidad, Timestamp, Cambios -->
</MudDataGrid>
```

---

## Despliegue

### Diagrama de Despliegue

```mermaid
graph TB
    subgraph Dev["🖥️ Desarrollo"]
        VS["Visual Studio"]
        SQL["LocalDB"]
    end

    subgraph Prod["🌐 Producción"]
        IIS["IIS/Servidor"]
        SQLP["SQL Server"]
        Cache["Cache en Memoria"]
    end

    subgraph Ext["🔗 Externos"]
        OM["OpenMeteo API"]
        OL["Ollama Server"]
    end

    Dev -->|"Deploy"| Prod
    IIS -->|"Conexión"| SQLP
    IIS -->|"Cache"| Cache
    IIS -->|"API"| OM
    IIS -->|"Llamadas"| OL

    style Dev fill:#e3f2fd
    style Prod fill:#f3e5f5
    style Ext fill:#fff9c4
```

### Checklist de Despliegue

- [ ] Compilación sin errores (`dotnet build`)
- [ ] Tests unitarios OK (`dotnet test`)
- [ ] Migraciones EF actualizadas (`dotnet ef database update`)
- [ ] Variables de entorno configuradas
  - `ConnectionStrings:DefaultConnection`
  - `ClimateRisk:RegionalContext`
  - `Ollama:Url`
  - `Email:SmtpServer`
  - `IaWeb:ApiKey` (opcional: ruta web gratuita de IA)
  - `Notificaciones:EmailAdmin` (reportes de operación al administrador)
- [ ] HTTPS habilitado
- [ ] Logs centralizados (ej: Application Insights)
- [ ] Backups automáticos de BD
- [ ] Monitoreo activo

### Configuración por Entorno

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": { "Default": "Debug" }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)..."
  }
}

// appsettings.Production.json
{
  "Logging": {
    "LogLevel": { "Default": "Warning" }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server..."
  }
}
```

---

## Resumen Técnico

| Aspecto | Detalle |
|--------|---------|
| **Lenguaje** | C# 12 |
| **Framework** | ASP.NET Core 8 + Blazor Server |
| **BD** | SQL Server (EF Core 8) |
| **Patrones** | Clean Architecture, CQRS, DI, Repository |
| **Seguridad** | Windows Auth, RBAC, Input Sanitization |
| **Escalabilidad** | Caché en memoria, async/await |
| **Testing** | Unit tests (xUnit/NUnit) |
| **DevOps** | Git, CI/CD ready |

---

**Última actualización**: 2024
**Responsable**: Documentación Técnica
**Versión**: 1.0
