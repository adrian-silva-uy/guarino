# 📊 Flujos de Datos y Casos de Uso - GuarinoApp

## Índice
1. [Flujos Principales](#flujos-principales)
2. [Módulos por Rol](#módulos-por-rol)
3. [Casos de Uso Detallados](#casos-de-uso-detallados)
4. [Diagrama de Secuencia](#diagrama-de-secuencia)
5. [Integraciones Externas](#integraciones-externas)

---

## Flujos Principales

### 1️⃣ Flujo de Autenticación

```mermaid
sequenceDiagram
    participant U as Usuario
    participant B as Navegador
    participant S as Servidor
    participant AD as Active Directory

    U->>B: Accede a guarino.company.com
    B->>S: GET /
    S->>S: Detecta no autenticado
    S->>B: Redirige a /login
    B->>U: Muestra Login

    U->>B: Ingresa credenciales Windows
    B->>S: POST /api/auth/login
    S->>AD: Negocia Kerberos
    AD->>S: Token válido
    S->>B: Set-Cookie (SessionId)
    B->>S: Redirige a Dashboard
    S->>B: AuthenticationState establecido
    B->>U: Dashboard cargado
```

### 2️⃣ Flujo de Consulta de Datos (GetPallets)

```mermaid
sequenceDiagram
    participant U as Usuario<br/>(Operador)
    participant R as Página Razor<br/>(PalletsDefinitivos)
    participant M as MediatR
    participant H as QueryHandler
    participant DB as SQL Server

    U->>R: Click en "Stock General"
    R->>R: OnInitializedAsync()
    R->>M: Send(GetPalletsQuery)

    activate M
    M->>M: Busca Handler registrado
    M->>H: Invoca GetPalletsQueryHandler
    deactivate M

    activate H
    H->>H: Verifica autorización
    H->>H: Obtiene usuario actual
    H->>DB: SELECT * FROM Pallets WHERE...
    DB->>H: IAsyncEnumerable<Pallet>
    H->>H: Mapea a PalletDto
    H->>M: Retorna List<PalletDto>
    deactivate H

    M->>R: Devuelve datos
    R->>R: Setea @Pallets = datos
    R->>R: StateHasChanged()
    R->>U: Renderiza DataGrid
    U->>U: Ve tabla de pallets
```

### 3️⃣ Flujo de Creación de Pallet

```mermaid
sequenceDiagram
    participant U as Usuario<br/>(Admin)
    participant F as Formulario
    participant V as Validador
    participant C as Command
    participant DB as Base de Datos
    participant A as AuditLog

    U->>F: Completa formulario de Pallet
    U->>F: Click en "Guardar"

    activate F
    F->>V: Valida (FluentValidation)
    alt Validación falla
        V->>F: Errores
        F->>U: Muestra errores
    else Validación OK
        V->>F: OK
    end
    deactivate F

    activate C
    F->>C: Send(CreatePalletCommand)
    C->>C: Sanitiza entrada
    C->>C: Valida reglas de negocio
    C->>DB: INSERT Pallet
    DB->>DB: Genera ID
    DB->>C: ID creado
    C->>A: LogActionCommand
    A->>DB: INSERT AuditLog
    DB->>A: OK
    C->>F: Resultado exitoso
    deactivate C

    F->>U: Mostrar confirmación
    F->>U: Redirige a lista
    U->>U: Ve nuevo pallet
```

### 4️⃣ Flujo de Monitoreo Climático (Hosted Service)

```mermaid
sequenceDiagram
    participant T as Timer
    participant HS as ClimateMonitoringHostedService
    participant CS as ClimateService
    participant OM as OpenMeteo API
    participant RS as ClimateRiskService
    participant DB as SQL Server

    T->>HS: Cada 1 hora
    activate HS

    HS->>CS: GetCurrentWeather(lat, lon)
    CS->>OM: GET /forecast?...
    OM->>CS: JSON con datos
    CS->>HS: WeatherData

    HS->>RS: EvaluateRisks(weather)
    RS->>RS: Analiza temperatura, humedad, etc.
    RS->>RS: Calcula índice de riesgo
    RS->>HS: RiskAssessment

    alt Riesgo > Umbral
        HS->>DB: INSERT EventoClimaticoDto
        HS->>DB: INSERT Alert
        HS->>HS: Notificar usuarios (opcional)
    end

    deactivate HS
```

### 5️⃣ Flujo de Exportación a Excel

```mermaid
sequenceDiagram
    participant U as Usuario
    participant P as Página
    participant S as ExportService
    participant DB as Base de Datos
    participant X as ClosedXML
    participant JS as JavaScript

    U->>P: Click en "Descargar Excel"
    P->>S: ExportToExcel(data)

    activate S
    S->>DB: Consulta datos (si es necesario)
    DB->>S: IEnumerable<T>

    S->>X: Crear workbook
    X->>X: Crear hoja
    X->>X: Escribir encabezados
    X->>X: Escribir filas de datos
    X->>X: Formatear (ancho, colores)

    X->>S: MemoryStream (XLSX)
    S->>P: byte[]
    deactivate S

    P->>JS: InvokeAsync("downloadFile", data)
    JS->>U: Trigger descarga
    U->>U: Abre Excel
```

---

## Módulos por Rol

### 👨‍💼 Admin (Acceso Total)

```mermaid
graph TB
    Admin["👤 Admin"]

    Admin -->|Gestión Total| Users["👥 Usuarios"]
    Admin -->|Auditoría| Audit["📋 Logs"]
    Admin -->|Configuración| Config["⚙️ Configuración"]
    Admin -->|Análisis| Reports["📊 Reportes"]
    Admin -->|Operaciones| Ops["📦 Operaciones"]

    Users --> U1["Crear usuario"]
    Users --> U2["Editar permisos"]
    Users --> U3["Activar/Desactivar"]

    Audit --> A1["Ver todos los cambios"]
    Audit --> A2["Filtrar por usuario"]
    Audit --> A3["Exportar logs"]

    Config --> C1["Contaduría"]
    Config --> C2["Parámetros"]
    Config --> C3["Integraciones"]

    style Admin fill:#ffcdd2
    style Users fill:#ef9a9a
    style Audit fill:#ef9a9a
```

### 👔 Gerencia (Ejecutiva)

```mermaid
graph TB
    Ger["👔 Gerencia"]

    Ger -->|Dashboards| Dash["📊 Dashboard Ejecutivo"]
    Ger -->|Análisis| Analysis["🔍 Análisis Estratégico"]
    Ger -->|Usuarios| UL["👥 Listar Usuarios"]
    Ger -->|Operaciones| Ops["📦 Stock"]

    Dash --> D1["KPIs generales"]
    Dash --> D2["Gráficos de tendencias"]
    Dash --> D3["Proyecciones"]

    Analysis --> A1["Proyección de zafra"]
    Analysis --> A2["Análisis financiero"]
    Analysis --> A3["Riesgos climáticos"]

    UL --> U1["Ver lista"]
    UL --> U2["Sin crear/editar"]

    style Ger fill:#c8e6c9
    style Dash fill:#a5d6a7
    style Analysis fill:#81c784
```

### 💰 Contador (Finanzas)

```mermaid
graph TB
    Cont["💰 Contador"]

    Cont -->|Contabilidad| Acc["🏦 Contaduría"]
    Cont -->|Liquidaciones| Liq["💵 Liquidaciones"]
    Cont -->|Análisis| Ana["📈 Análisis"]

    Acc --> A1["Ingresos/Gastos"]
    Acc --> A2["Balance"]

    Liq --> L1["Liquidaciones operarios"]
    Liq --> L2["Liquidaciones quinta"]
    Liq --> L3["Exportar nómina"]

    Ana --> AN1["Proyección zafra"]
    Ana --> AN2["Rentabilidad"]

    style Cont fill:#fff9c4
    style Acc fill:#f9f176
    style Liq fill:#f57f17
```

### 👷 Operador (Operativo)

```mermaid
graph TB
    Op["👷 Operador"]

    Op -->|Diario| Day["📅 Operación Diaria"]
    Op -->|Reportes| Reports["📊 Reportes"]
    Op -->|IA| IA["🤖 Herramientas IA"]

    Day --> D1["Stock General"]
    Day --> D2["Stock en Playa"]
    Day --> D3["Fruta a volcar"]
    Day --> D4["Pallets sin cliente"]
    Day --> D5["Registro de pesadas"]

    Reports --> R1["Resumen mensual"]
    Reports --> R2["Gráficos"]
    Reports --> R3["Stock definitivos"]

    IA --> I1["Proyección volcados"]
    IA --> I2["Asistente IA"]

    style Op fill:#b3e5fc
    style Day fill:#81d4fa
    style Reports fill:#4fc3f7
```

---

## Casos de Uso Detallados

### UC-001: Consultar Stock de Pallets

**Actor**: Operador  
**Precondiciones**: Usuario autenticado con rol Operador  
**Flujo Básico**:

1. Actor navega a `/stockgeneral`
2. Sistema carga datos de BD
3. Presenta tabla con filtros
4. Actor aplica filtros (cliente, fecha, etc.)
5. Sistema re-consulta y actualiza tabla
6. Actor selecciona un pallet
7. Sistema muestra detalles
8. Actor cierra detalles o exporta

**Flujo Alternativo - Sin datos**:
- Si BD vacía: mostrar mensaje "No hay datos"

**Flujo Alternativo - Error BD**:
- Si error SQL: mostrar error genérico y loguear

**Postcondiciones**:
- Datos consultados quedan en log de auditoría
- Sin modificaciones en BD

### UC-002: Crear Usuario

**Actor**: Admin  
**Precondiciones**: Usuario Admin autenticado  
**Flujo Básico**:

1. Actor navega a `/usuarios`
2. Sistema carga lista de usuarios
3. Actor click en "Nuevo usuario"
4. Sistema abre diálogo
5. Actor completa:
   - Email (validado)
   - Nombre
   - Roles (checkboxes)
6. Actor click en "Guardar"
7. Sistema valida datos
8. Sistema crea usuario en BD
9. Sistema genera contraseña temporal
10. Sistema envía email con credenciales
11. Retorna a lista

**Validaciones**:
- Email única
- Email válido
- Al menos 1 rol
- Nombre no vacío

**Postcondiciones**:
- Usuario creado en BD
- Email enviado
- Evento en auditoría
- Notificación a usuario Admin

### UC-003: Generar Proyección de Zafra

**Actor**: Gerencia/Admin  
**Precondiciones**:
- Usuario autenticado
- Datos históricos de al menos 2 años
- OpenMeteo disponible  
**Flujo Básico**:

1. Actor navega a `/proyeccion-zafra`
2. Sistema carga datos históricos
3. Actor selecciona escenarios
   - Optimista
   - Probable
   - Pesimista
4. Sistema consulta Ollama para análisis IA
5. Sistema analiza:
   - Tendencias históricas
   - Proyecciones climáticas
   - Factores económicos
6. Sistema genera reportes con:
   - Gráficos
   - Tablas de datos
   - Recomendaciones
7. Actor puede exportar o enviar por email

**Postcondiciones**:
- Proyección guardada en BD
- Auditoría registrada
- Email enviado si solicita

### UC-004: Monitorar Riesgos Climáticos

**Actor**: Sistema (Hosted Service)  
**Precondiciones**: Nada  
**Flujo Básico**:

1. Cada hora se ejecuta ClimateMonitoringHostedService
2. Consulta coordenadas de cultivos
3. Llama a OpenMeteo API
4. Evalúa:
   - Temperatura (> 35°C?)
   - Humedad (< 30%?)
   - Lluvia inesperada
   - Helada (< 0°C?)
5. Si hay riesgo:
   - Crea EventoClimaticoDto
   - Crea AlertaDto
   - Notifica usuarios Gerencia/Admin
6. Registra evento en BD

**Postcondiciones**:
- Evento guardado
- Usuarios notificados si aplica

### UC-005: Exportar Datos a Excel

**Actor**: Operador/Gerencia  
**Precondiciones**: Usuario autenticado, datos disponibles  
**Flujo Básico**:

1. Actor está en página de reportes
2. Actor click en "Descargar Excel"
3. Sistema:
   - Consulta BD según filtros
   - Genera XLSX con ClosedXML
   - Formatea colores, ancho de columnas
   - Calcula totales si aplica
4. Sistema dispara descarga en navegador
5. Actor recibe archivo

**Postcondiciones**:
- Descarga registrada en logs
- Auditoría sin detalles de datos (privacidad)

---

## Diagrama de Secuencia

### Integración Completa: Usuario consulta Pallets y Exporta

```mermaid
sequenceDiagram
    participant User as Usuario
    participant Blazor as Página Blazor
    participant MediatR as MediatR Bus
    participant Handler as QueryHandler
    participant Context as DbContext
    participant SQL as SQL Server
    participant Export as ExportService
    participant ClosedXML as ClosedXML
    participant JS as JavaScript

    User->>Blazor: Accede a página
    Blazor->>Blazor: OnInitializedAsync

    Note over Blazor: PASO 1: Consulta datos
    Blazor->>MediatR: Send(GetPalletsQuery)
    MediatR->>Handler: Invoke
    Handler->>Context: dbContext.Pallets.Where(...)
    Context->>SQL: SELECT * FROM Pallets
    SQL-->>Context: IAsyncEnumerable
    Context-->>Handler: Mapea a DTO
    Handler-->>MediatR: List<PalletDto>
    MediatR-->>Blazor: Datos cargados
    Blazor->>Blazor: StateHasChanged()
    Blazor-->>User: Muestra tabla

    User->>Blazor: Aplica filtro
    Blazor->>MediatR: Send(GetPalletsQuery con filtro)
    Note over MediatR,SQL: Mismo flujo con WHERE modificado
    MediatR-->>Blazor: Datos filtrados
    Blazor-->>User: Tabla actualizada

    Note over Blazor: PASO 2: Exporta a Excel
    User->>Blazor: Click "Descargar Excel"
    Blazor->>Export: ExportToExcel(pallets)
    Export->>ClosedXML: CreateWorkbook()
    ClosedXML->>ClosedXML: Agregar datos
    ClosedXML->>ClosedXML: Formatear
    ClosedXML->>Export: MemoryStream (XLSX)
    Export-->>Blazor: byte[]
    Blazor->>JS: InvokeAsync("downloadFile")
    JS-->>User: Descarga archivo
```

---

## Integraciones Externas

### 🌍 OpenMeteo API

```mermaid
graph TB
    App["Aplicación<br/>GuarinoApp"]

    subgraph OM["OpenMeteo"]
        FC["Forecast"]
        HC["Historical"]
    end

    App -->|GET /forecast| FC
    App -->|GET /historical| HC

    FC -->|Datos futuros| App
    HC -->|Datos históricos| App

    App -->|"Cada 1h"| Monitor["ClimateMonitoringHostedService"]
    Monitor -->|Procesa| Risk["ClimateRiskService"]
    Risk -->|"Guarda"| BD[("Base de Datos<br/>EventosClimaticos")]

    style OM fill:#ffe0b2
    style App fill:#c8e6c9
    style Monitor fill:#fff9c4
    style BD fill:#f8bbd0
```

**Endpoint Utilizado**:
```
GET https://api.open-meteo.com/v1/forecast
?latitude=...&longitude=...
&hourly=temperature_2m,relative_humidity_2m,precipitation
&timezone=America/Montevideo
```

**Datos Consultados**:
- Temperatura (°C)
- Humedad relativa (%)
- Precipitación (mm)
- Velocidad del viento (km/h)

### 🤖 Ollama API

```mermaid
graph TB
    App["Aplicación<br/>GuarinoApp"]

    subgraph OL["Ollama Server"]
        M1["Modelo LLM<br/>(ej: llama2)"]
    end

    App -->|POST /api/generate| OL
    OL -->|Procesa prompt| M1
    M1 -->|Respuesta| OL
    OL -->|JSON| App

    App -->|Casos de Uso| CU["🤖 Asistente IA<br/>🌾 Análisis Zafra<br/>📊 Insights"]

    style OL fill:#e1f5fe
    style App fill:#c8e6c9
    style CU fill:#a5d6a7
```

**Endpoint**:
```
POST http://localhost:11434/api/generate
{
  "model": "llama2",
  "prompt": "Análisis de proyección de zafra...",
  "stream": false
}
```

**Casos de Uso**:
- Análisis de Proyección de Zafra
- Generación de planes de mitigación
- Chat asistente general
- Análisis de datos

### 📧 Servicio de Email

```mermaid
graph TB
    App["Aplicación"]

    ES["IEmailService"]
    SMTP["Servidor SMTP<br/>(ej: Gmail, Office365)"]

    App -->|"SendEmailAsync"| ES
    ES -->|"SMTP"| SMTP
    SMTP -->|"Entrega"| User["📬 Usuario"]

    App -->|Eventos que generan email:| Events["Casos"]

    Events --> E1["Usuario creado"]
    Events --> E2["Contraseña reseteada"]
    Events --> E3["Reporte exportado"]
    Events --> E4["Alerta climática"]

    style App fill:#c8e6c9
    style ES fill:#a5d6a7
    style SMTP fill:#fff9c4
    style User fill:#bbdefb
```

**Configuración** (appsettings.json):
```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "reportes@guarinoapp.com",
    "SmtpUser": "(usuario SMTP autenticado)",
    "Password": "(contraseña de aplicación)"
  }
}
```

> Los reportes automáticos de operación para el administrador (sección
> `Notificaciones`) usan esta misma configuración SMTP.

---

**Última actualización**: 2024-01-XX  
**Versión**: 1.0
---

## Actualizaciones recientes (Oct 2026)

Cambios aplicados al código y reflejados en la documentación:

- **Filtro de módulos por rol (UI)**: `NavMenu.razor` y `Index.razor` muestran los módulos bloqueados con candado y tooltip "Requiere: X — tu rol actual: Y" en lugar de ocultarlos. Componentes nuevos: `Shared/NavLinkSeguro.razor` y `Shared/EnlaceSeguro.razor`. Matriz de acceso por reflexión en `Services/MapaAccesosRutas.cs` (lee `[Authorize]` de las páginas → ruta→ roles). Helper `Services/RolUsuario.cs` resuelve el rol actual con `RoleNames`. Registrado como singleton en `Program.cs`.
- **RoleNames como fuente única**: constantes `Admin`/`Gerencia`/`Contador`/`Operador` + grupos `Todos`/`Financieros`/`Administracion` + cadenas `*Autorizacion` para `[Authorize]`. Cero literales de rol en `.razor`.
- **Paginación fija de 25 filas** en las 30 grillas (`RowsPerPage="25"` + `MudTablePager PageSizeOptions="new int[] { 25 }"`). Auditoría usa paginación server-side (`ServerData` + `state.Page/PageSize`).
- **Filtro IA por rol**: `ConsultaIArestringida` (log) + matriz de roles por tool MCP.
- **Servidor MCP** (`/mcp`) con `WithHttpTransport()` + middleware Bearer propio (`McpAuthMiddleware`); tools solo lectura; auditoría `McpCall`.
- **Cero datos inventados**: `GetDummyData`/`GenerarZafrasDummy`/fallbacks eliminados; `EsDummy` siempre `false`; "En nuestro poder" = sin cliente y Estado != Bajado/Despachado.
- **Autenticación**: Negotiate + Data Protection tokens (`GuarinoApp.Auth.v1`, 12 h, localStorage `auth_token`); lockout 5 fallos/15 min.
- **Grillas sin paginador** reemplazado por paginación de 25 (instrucción vigente).
- **Build**: 0 errores / 0 advertencias. UI en español.
- **Diagramas Mermaid**: 40 bloques en `docs/diagramas.md` y otros archivos (arquitectura, flujos, seguridad, servicios). Renderizan en GitHub, VS Code (con extensión), Obsidian.
- **Wiki de usuario**: `docs/WIKI_USUARIO.md` con manual funcional por módulo y rol.
- **Auditoría exhaustiva**: `docs/auditoria-exhaustiva.md` (OWASP + UX + deuda técnica).
- **Documentación inversa**: `docs/documentacion-inversa.md` (arquitectura, módulos, decisiones, trampas).