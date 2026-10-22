# Diagramas de arquitectura y flujo â€” GuarinoApp

> Todos los diagramas estÃ¡n en formato **Mermaid** (renderizan en GitHub, VS Code, Obsidian y la mayorÃ­a de wikis). Pueden copiarse a cualquier `.md`.

---

## 1. Arquitectura de capas

```mermaid
graph TB
    subgraph Web["GuarinoApp.Web (Blazor Server)"]
        Pages["Pages/*.razor"]
        Shared["Shared/* (NavMenu, ExportToolbar, NavLinkSeguro, EnlaceSeguro)"]
        Mcp["Mcp/ (GuarinoMcpTools, McpAuthMiddleware)"]
        Program["Program.cs (pipeline, DI)"]
    end
    subgraph App["GuarinoApp.Application"]
        MediatR["MediatR (CQRS)"]
        Cmds["Commands (13)"]
        Qrys["Queries (32)"]
        Beh["Pipeline Behaviors (InputSanitization, SerializacionDbContext)"]
        RoleNames["RoleNames (fuente Ãºnica)"]
        Mapa["MapaAccesosRutas (reflexiÃ³n [Authorize])"]
    end
    subgraph Infra["GuarinoApp.Infrastructure"]
        EF["EF Core / ApplicationDbContext"]
        Svc["Servicios (Email, Export, IA, Clima, MCP data, Audit, Auth)"]
        Hosted["Hosted Services (Clima 6h, Notificador, Ollama, BaseDatos)"]
    end
    subgraph Dom["GuarinoApp.Domain"]
        Ent["Entities / Enums (EstadoPallet, RoleNames)"]
    end
    DB[("SQL Server")]
    EXT["Externos: OpenMeteo, Ollama/Groq, MCP clients"]

    Pages --> MediatR
    Shared --> Mapa
    Mcp --> Svc
    MediatR --> Cmds
    MediatR --> Qrys
    Cmds --> Beh
    Qrys --> Beh
    Beh --> EF
    EF --> DB
    Svc --> EXT
    App --> Dom
    Infra --> Dom
```

---

## 2. Flujo de autenticaciÃ³n (Local)

```mermaid
sequenceDiagram
    participant U as Usuario
    participant B as Navegador
    participant W as Blazor Server
    participant L as LoginCommandHandler
    participant CI as ControlIntentosLogin
    participant DB as SQL Server

    U->>B: Abre /login
    B->>W: GET /login
    W->>B: Render Login.razor
    U->>B: Ingresa usuario/contraseÃ±a
    B->>W: Mediator.Send(LoginCommand)
    W->>L: Handle
    L->>CI: EstaBloqueado(usuario)?
    alt Bloqueado (5 fallos en 30 min)
        CI-->>L: true
        L->>DB: Audit Log "LoginBlocked"
        L-->>W: Fail
        W-->>B: Snackbar error
    else No bloqueado
        L->>DB: SELECT AppUser WHERE UserName = @u
        alt Password invÃ¡lido
            L->>CI: RegistrarFallo(usuario)
            L->>DB: Audit Log "LoginFailed"
            L-->>W: Fail
        else Password vÃ¡lido
            L->>DB: Audit Log "Login"
            L->>W: AuthTokenService.Issue (DP, 12 h)
            W-->>B: token â†’ localStorage "auth_token"
            B->>W: Redirige a /
        end
    end
```

---

## 3. Flujo de consulta de datos (ej. GetPallets / Stock General)

```mermaid
sequenceDiagram
    participant U as Usuario
    participant P as PÃ¡gina (StockGeneral.razor)
    participant M as MediatR
    participant H as GetPalletsQueryHandler
    participant DB as SQL Server

    U->>P: Navega a /stockgeneral
    P->>P: OnInitializedAsync â†’ LoadData()
    P->>M: Send(GetPalletsQuery(zafra, estado))
    M->>H: Handle
    H->>DB: SELECT Pallets WHERE Estado != Bajado (LINQ parametrizado)
    DB-->>H: List<Pallet>
    H-->>M: PalletsResultDto (EsDummy=false)
    M-->>P: resultado
    P->>P: _data = resultado
    P->>P: _loading = false
    P-->>U: Render MudTable 25 filas + pager
```

---

## 4. Flujo de creaciÃ³n de usuario (validaciÃ³n + auditorÃ­a)

```mermaid
sequenceDiagram
    participant A as Admin
    participant P as Usuarios.razor
    participant M as MediatR
    participant H as CreateUserCommandHandler
    participant DB as SQL Server

    A->>P: Click "NUEVO USUARIO"
    P->>P: Muestra formulario (Usuario, Nombre, Correo, ContraseÃ±a, Rol)
    A->>P: Rellena campos y "CREAR USUARIO"
    P->>M: Send(CreateUserCommand)
    M->>H: Handle
    H->>DB: Â¿rol activo en Roles?
    alt Rol invÃ¡lido
        H-->>M: Fail("El rol seleccionado no es vÃ¡lido.")
    else Rol vÃ¡lido
        H->>H: userName.Length<3? / password.Length<6? / email invÃ¡lido?
        alt ValidaciÃ³n falla
            H-->>M: Fail(mensaje)
        else OK
            H->>DB: Â¿UserName existe?
            alt Duplicado
                H-->>M: Fail("Ese usuario ya existe.")
            else
                H->>DB: INSERT AppUser (password hasheada PBKDF2)
                H->>DB: Audit Log "CreateUser"
                H-->>M: Success
            end
        end
    end
    M-->>P: resultado
    alt Success
        P->>P: Snackbar "Usuario creado correctamente." + CancelarForm + LoadAsync
    else Fail
        P->>P: Snackbar error (auto-hide 5s) + formulario permanece abierto
    end
```

---

## 5. Flujo de paginaciÃ³n (25 filas fijas)

```mermaid
stateDiagram-v2
    [*] --> Pagina1: OnInitializedAsync â†’ LoadData()
    Pagina1 --> Cargando: _loading=true
    Cargando --> Render25: MudTable Items=_data, RowsPerPage=25
    Render25 --> Pager: "Mostrando 1â€“25 de N"
    Pager --> Pagina2: Click "siguiente" (MudTablePager)
    Pagina2 --> Render25b: "Mostrando 26â€“50 de N"
    Render25b --> Pagina1: Click "anterior"
    Note right of Render25: PageSizeOptions = {25} (Ãºnico valor, no modificable)
```

Para AuditorÃ­a (server-side): el pager dispara `ServerReload(state)` â†’ handler con `Page=state.Page+1, PageSize=state.PageSize` â†’ SQL `Skip/Take` â†’ devuelve `TotalCount`.

---

## 6. Matriz de acceso por rol (menÃº e Inicio)

```mermaid
flowchart LR
    subgraph Roles
        Admin["Admin"]
        Gerencia["Gerencia"]
        Contador["Contador"]
        Operador["Operador"]
    end
    subgraph MÃ³dulos
        M1["Stock General / Playa / Pesadas / Empaque / Lotes Volcados"]
        M2["Gerencia (panel)"]
        M3["ProyecciÃ³n Volcados / Zafra"]
        M4["Liquidaciones / Resumen Mensual / ContadurÃ­a"]
        M5["Contactos / Usuarios / AuditorÃ­a"]
        M6["IA Chat / Riesgos ClimÃ¡ticos / Reportes"]
    end
    Admin --> M1 & M2 & M3 & M4 & M5 & M6
    Gerencia --> M1 & M2 & M3 & M4 & M6
    Contador --> M1 & M4 & M6
    Operador --> M1 & M6
    M5 -. "bloqueado ðŸ”’" .- Operador
    M2 -. "bloqueado ðŸ”’" .- Contador
    M4 -. "bloqueado ðŸ”’" .- Operador
```

Los mÃ³dulos bloqueados se **muestran** con ðŸ”’ + tooltip "Requiere: X â€” tu rol actual: Y" (no se ocultan). La seguridad real la impone cada `[Authorize(Roles=RoleNames.*Autorizacion)]` en la pÃ¡gina.

---

## 7. Flujo MCP (Model Context Protocol)

```mermaid
sequenceDiagram
    participant C as Cliente MCP
    participant MW as McpAuthMiddleware
    participant T as GuarinoMcpTools
    participant D as McpDataService
    participant DB as SQL Server
    participant AU as AuditService

    C->>MW: POST /mcp (Authorization: Bearer <token>)
    MW->>MW: Unprotect token (DP) + validar expiraciÃ³n
    alt Token invÃ¡lido/expirado
        MW-->>C: 401 Unauthorized
    else Token vÃ¡lido
        MW->>MW: Componer HttpContext.User (rol del token)
        MW->>T: tools/call <nombre>
        T->>T: Â¿rol en matriz de la tool?
        alt No autorizado
            T->>AU: Log "McpCall" (restringido)
            T-->>C: error (rol insuficiente)
        else Autorizado
            T->>D: Consulta datos reales (LINQ)
            D->>DB: SELECT ...
            DB-->>D: datos
            D-->>T: resultado
            T->>AU: Log "McpCall" (Ã©xito)
            T-->>C: resultado (JSON)
        end
    end
```

Todas las tools MCP son de **solo lectura** (la importaciÃ³n de contactos queda fuera).

---

## 8. Flujo de filtro IA por rol

```mermaid
flowchart TD
    U[Usuario envÃ­a consulta IA] --> F1{FiltroConsultasIA: Â¿tool permitida para el rol?}
    F1 -- No --> L1[Log "ConsultaIArestringida" + respuesta bloqueada]
    F1 -- SÃ­ --> F2{McpAuthMiddleware: token vÃ¡lido?}
    F2 -- No --> L2[401]
    F2 -- SÃ­ --> Tool[Ejecuta tool MCP de solo lectura]
    Tool --> Audit[Log "Consulta IA" / "McpCall"]
    Audit --> Resp[Respuesta al chat]
```

---

## 9. Flujo de auditorÃ­a

```mermaid
sequenceDiagram
    participant H as Handler (Command/Query)
    participant A as IAuditService
    participant DB as SQL Server (LogEntries)

    H->>A: LogAsync(action, module, description, additionalData)
    A->>A: Obtener usuario actual (ICurrentUserService) + rol
    A->>DB: INSERT LogEntry (UserName, Role, Action, Module, Description, Timestamp, IpAddress)
    Note over A: catch vacÃ­o â†’ no propaga errores (trampa: fallos silenciosos)
```

---

## 10. Modelo de datos (entidades clave)

```mermaid
erDiagram
    AppUser ||--o{ LogEntry : "genera"
    AppUser }o--|| Role : "tiene"
    Lote ||--o{ LoteEmboquille : "tiene"
    Lote }o--|| Variedad : "es"
    Lote }o--|| Productor : "pertenece"
    LoteEmboquille }o--|| Emboquille : "de"
    Pallet }o--|| Lote : "del"
    Pallet }o--o| Productor : "cliente"
    Volcado }o--|| Lote : "de"
    Contacto ||--o{ LogEntry : "auditado"
```

---

## 11. Ciclo de vida de una pÃ¡gina Blazor (patrÃ³n estÃ¡ndar)

```mermaid
stateDiagram-v2
    [*] --> OnInitializedAsync
    OnInitializedAsync --> CargarRoles: CargarRolesAsync (Usuarios)
    OnInitializedAsync --> LoadData
    LoadData --> Loading: _loading=true
    Loading --> MediatR: Mediator.Send(Query/Command)
    MediatR --> DB: EF Core
    DB --> Result: DTO
    Result --> Render: _data=resultado, _loading=false
    Render --> [*]: MudTable (25 filas + pager)
    Render --> Filtro: usuario cambia filtro
    Filtro --> LoadData
```

---

*Diagramas generados a partir del cÃ³digo fuente verificado. Formato Mermaid (compatible con GitHub, VS Code, Obsidian).*

---

## Actualizaciones recientes (Oct 2026)

Cambios aplicados al cÃ³digo y reflejados en la documentaciÃ³n:

- **Filtro de mÃ³dulos por rol (UI)**: `NavMenu.razor` y `Index.razor` muestran los mÃ³dulos bloqueados con ðŸ”’ + tooltip "Requiere: X â€” tu rol actual: Y" en lugar de ocultarlos. Componentes nuevos: `Shared/NavLinkSeguro.razor` y `Shared/EnlaceSeguro.razor`. Matriz de acceso por reflexiÃ³n en `Services/MapaAccesosRutas.cs` (lee `[Authorize]` de las pÃ¡ginas â†’ rutaâ†’roles). Helper `Services/RolUsuario.cs` resuelve el rol actual con `RoleNames`. Registrado como singleton en `Program.cs`.
- **RoleNames como fuente Ãºnica**: constantes `Admin`/`Gerencia`/`Contador`/`Operador` + grupos `Todos`/`Financieros`/`Administracion` + cadenas `*Autorizacion` para `[Authorize]`. Cero literales de rol en `.razor`.
- **PaginaciÃ³n fija de 25 filas** en las 30 grillas (`RowsPerPage="25"` + `MudTablePager PageSizeOptions="new int[] { 25 }"`). AuditorÃ­a usa paginaciÃ³n server-side (`ServerData` + `state.Page/PageSize`).
- **Filtro IA por rol**: `ConsultaIArestringida` (log) + matriz de roles por tool MCP.
- **Servidor MCP** (`/mcp`) con `WithHttpTransport()` + middleware Bearer propio (`McpAuthMiddleware`); tools solo lectura; auditorÃ­a `McpCall`.
- **Cero datos inventados**: `GetDummyData`/`GenerarZafrasDummy`/fallbacks eliminados; `EsDummy` siempre `false`; "En nuestro poder" = sin cliente y Estado != Bajado/Despachado.
- **AutenticaciÃ³n**: Negotiate + Data Protection tokens (`GuarinoApp.Auth.v1`, 12 h, localStorage `auth_token`); lockout 5 fallos/15 min.
- **Grillas sin paginador** reemplazado por paginaciÃ³n de 25 (instrucciÃ³n vigente).
- **Build**: 0 errores / 0 advertencias. UI en espaÃ±ol.
