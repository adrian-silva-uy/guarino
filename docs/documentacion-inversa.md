# Documentación inversa y reconstrucción del razonamiento — GuarinoApp

**Proyecto:** GuarinoApp — .NET 8 + Blazor Server (GuarinoApp.Web) + EF Core + MediatR
**Base:** código fuente del repo (HEAD `26f0301`), excluyendo `Propuesta/` y `csv Propuesta/`.
**Objetivo:** recuperar el mapa mental del proyecto en 5 minutos y documentar arquitectura, módulos, decisiones y trampas.

---

## 1. Mapa de arquitectura y flujo de datos (End-to-End)

### 1.1 Capas

```
┌─────────────────────────────────────────────────────────────┐
│  GuarinoApp.Web (Blazor Server, net8.0 Web SDK, WinExe)     │
│  Pages/*.razor, Shared/*, Mcp/, Program.cs, Services/        │
├─────────────────────────────────────────────────────────────┤
│  GuarinoApp.Infrastructure (net8.0, FrameworkReference AspNet)│
│  Data/ApplicationDbContext, Services/*, Migrations/, DI      │
├─────────────────────────────────────────────────────────────┤
│  GuarinoApp.Application (net8.0)                             │
│  Modules/*/{Commands,Queries}, Common/*, Settings/*, DI      │
├─────────────────────────────────────────────────────────────┤
│  GuarinoApp.Domain (net8.0)                                  │
│  Entities/, Enums/, Shared/ (BaseEntity, IAggregateRoot)     │
└─────────────────────────────────────────────────────────────┘
BenchmarkSuite1 (net8.0) referencia solo Domain.
```

Dependencias: Web → Application + Infrastructure; Infrastructure → Application → Domain. Application no depende de Infrastructure (inversión de dependencias vía interfaces en Common/Interfaces).

### 1.2 Flujo típico de una petición (ej. "Listar basculas")

```
1. Usuario abre /basculas → Blazor Server renderiza Pages/Basculas.razor
2. OnInitializedAsync() → LoadData() (Basculas.razor:60-82)
3. _data = await Mediator.Send(new GetBasculasQuery())   (línea 71)
4. MediatR resuelve GetBasculasQueryHandler (Application/Modules/Basculas/Queries/GetBasculas/)
   - InputSanitizationBehavior (transient, Application/DependencyInjection.cs:22) sanitiza el request
   - Handler inyecta IApplicationDbContext
5. Handler consulta _context.Basculas (DbSet, Infrastructure/Data/ApplicationDbContext.cs:22) con LINQ
6. EF Core genera SQL parametrizado → SQL Server (localdb en dev, PROD_SERVER en prod)
7. Resultado mapeado a BasculasResultDto → retorna a la página
8. _data = resultado; _loading = false; StateHasChanged → render de la grilla MudTable (paginada 25)
9. Auditoría: si la acción lo requiere, IAuditService.LogAsync persiste LogEntry (Infrastructure/Services/AuditService.cs:14-57)
```

### 1.3 Patrones detectados

- **CQRS con MediatR**: 13 Commands + 32 Queries (handlers en `Application/Modules/*/Commands|Queries`). Comandos = escritura (Login, CreateUser, UpdateUser, contactos CRUD, contaduría), Queries = lectura.
- **Sin Repository puro**: los handlers inyectan `IApplicationDbContext` directamente (ej. `Application/Modules/Basculas/Queries/GetBasculas/GetBasculasQueryHandler.cs:9-11`). Simplicidad sobre abstracción.
- **Options Pattern**: `ClimateRiskSettings`, `EmailOptions`, `OllamaOptions`, `IaWebOptions`, `McpOptions`, `NotificacionesOptions`, `BaseDatosOptions`, `SecuritySettings`, `AuthSettings` (todos con `AddOptions<...>().Bind(...).ValidateOnStart()`).
- **Pipeline Behaviors**: `InputSanitizationBehavior` (transient) + `SerializacionDbContextBehavior` (transient) sobre MediatR.
- **Hosted Services**: ClimateMonitoringHostedService (cada 6 h), NotificadorAdminService (cada N min), OllamaStartupService, OllamaRecuperacionService, BaseDatosRecuperacionService.
- **State**: sin store global; estado local en cada página (`_data`, `_loading`, `_fechaDesde`); `CustomAuthenticationStateProvider` (Scoped) para auth.
- **Data Protection**: `AddDataProtection()` default (sin persistencia explícita de claves) en `Infrastructure/DependencyInjection.cs:198`.

### 1.4 Comunicación entre capas

- Web → Application: `IMediator.Send(request)` (request = Command/Query).
- Application → Infrastructure: interfaces (`IApplicationDbContext`, `IAuditService`, `IEmailService`, `IExportService`, `IPasswordHasher`, `ICurrentUserService`, `IAIService`).
- Infrastructure → Domain: entidades + enums.
- MCP: `Web/Mcp/GuarinoMcpTools.cs` (McpServerToolType) → `IMcpDataService` (Infrastructure) → DbContext.

---

## 2. Inventario y propósito de módulos

### 2.1 Proyectos

| Proyecto | Rol |
|---|---|
| GuarinoApp.Domain | Entidades, enums, agregados (net8.0, sin deps) |
| GuarinoApp.Application | Casos de uso MediatR, validadores, interfaces, RoleNames, InputSanitizer |
| GuarinoApp.Infrastructure | EF Core, DbContext, servicios, hosted services, migraciones |
| GuarinoApp.Web | Blazor Server UI, MCP endpoint, Program.cs, páginas |
| BenchmarkSuite1 | Benchmarks (referencia Domain) |

### 2.2 Módulos de negocio (Application/Modules — 27)

| Módulo | Responsabilidad |
|---|---|
| Auditoria | Logs paginados (GetLogs) + LogAction |
| Auth | Login, Register, CRUD usuarios/roles |
| Basculas | Catálogo de básculas |
| CamionesZorras | Catálogo camiones/zorras |
| ClimateRisk | Eventos climáticos, mapa, resumen, impacto en proyección, mitigación |
| Contaduria | Datos financieros + contexto operativo |
| Contactos | CRUD + importación |
| Empaque | Catálogo de empaque |
| FrutaVolcar | Fruta pendiente de volcar |
| Gerencia | Indicadores de gerencia |
| GraficaProcesos | Gráfica de procesos |
| LiquidacionesQuinta | Liquidaciones por quinta |
| Lugares / LugaresOrigen / LugaresDestino | Catálogos de lugares |
| LotesVolcados | Lotes volcados (fuente LotesEmboquille agrupado) |
| PalletsContramarca / PalletsDefinitivos / PalletsSinCliente | Pallets por estado/contramarca |
| Proyeccion | Proyección de zafra/volcados |
| RegistroPesadas | Registro de pesadas (fuente LotesEmboquille) |
| ResumenLiquidaciones / ResumenMensual | Resúmenes |
| Stock / StockPalletsSinCliente / StockPlaya | Stock por variedad/playa/pallets |
| Transportistas | Catálogo transportistas |

### 2.3 Entidades de dominio (Domain/Entities)

Lote, LoteEmboquille, Emboquille, Pallet, Productor, ProductorChacra, Variedad, Especie, Quinta, TipoLote, Bascula, Volcado, VolcadoHistorico, StockVariedad, Empaque, Lugar, LugarOrigen, LugarDestino, DestinoInterno, Responsable, EventoClimatico, FinancialData, LogEntry, AppUser, Role, Contacto, RitmoCalibradora. Todas heredan de `BaseEntity` (Domain/Shared/BaseEntity.cs).

### 2.4 Enums clave

- `EstadoPallet` (`Domain/Enums/EstadoPallet.cs:2-13`): Despachado=1, EnFrio=2, PreFrio=3, Rearmado=4, Bajado=5.
- `EstadoPalletExtensions` mapea etiquetas/códigos legados ('D'/'F'/'P'/'R'/'B').

### 2.5 RoleNames (fuente única de roles) — `Application/Common/RoleNames.cs`

- Constantes: `Admin="Admin"` (12), `Gerencia` (13), `Contador` (14), `Operador` (15).
- Grupos: `Todos` (18) = {Admin, Gerencia, Contador, Operador}; `Financieros` (24) = {Admin, Gerencia, Contador}; `Administracion` (30) = {Admin, Gerencia}.
- Cadenas de autorización: `TodosAutorizacion` (34-35), `FinancierosAutorizacion` (37-38), `AdministracionAutorizacion` (40-41).

### 2.6 Componentes compartidos (Web/Shared)

ExportToolbar (export PDF/Excel/CSV + filtros de fecha), NavMenu (menú lateral con NavLinkSeguro), NavLinkSeguro (link con candado+tooltip por rol), EnlaceSeguro (MudLink equivalente para Inicio), ContactoImportDialog, ContactoDialog, SendEmailDialog, EditUserEmailDialog, DataSourceFooter, CurrentUserDisplay, InteractiveChart, SimpleDialog, MainLayout, CurrentUserDisplay.

### 2.7 Páginas (Web/Pages) por grupo de autorización

- **TodosAutorizacion** (22 páginas): Index "/", Dashboard, CamionesZorras, Basculas, Counter, Empaque, GestionRiesgosClimaticos, IAChat, FrutaVolcar, GraficaProcesos, PalletsSinCliente, StockPalletsSinCliente, Transportistas, StockPlaya, LugaresDestino, LotesVolcados, RegistroPesadas, StockGeneral, PalletsDefinitivos, LugaresOrigen, Lugares, PalletsContramarca, ProyeccionVolcados.
- **FinancierosAutorizacion** (6): Facturacion, Contaduria, ResumenLiquidaciones, ResumenMensual, ProyeccionZafra, LiquidacionesQuinta.
- **AdministracionAutorizacion** (4): Auditoria, Contactos, Gerencia, Usuarios.
- **Sin Authorize**: Login "/login", Register "/register".

### 2.8 DTOs y modelos principales

- `BasculasResultDto`, `LotesVolcadosResultDto` (lista + `EsDummy`), `PalletsResultDto`, `LotesPesadasResultDto`, `ContactosResultDto`, `UsersResultDto`, `LogEntriesResultDto` (con `TotalCount` para paginación server-side).
- `AuthResultDto` (Success/Error/Token).
- `NuevoUsuario` (form de alta).
- `EstadoPallet` + `EstadoPalletExtensions`.

### 2.9 Infraestructura

- **Servicios**: EmailService (IEmailService), OpenMeteoClimateService (IClimateService), OllamaAIService (IAIService) + FallbackAIService, ExportService (IExportService, PDF/Excel/CSV), McpDataService (IMcpDataService), AuditService (IAuditService), PasswordHasher, AuthTokenService, CustomAuthenticationStateProvider.
- **Migraciones EF**: carpeta `Infrastructure/Migrations/` (24+ migraciones) + `Data/Migrations/`; en Development `Program.cs:351` ejecuta `context.Database.Migrate()`. No se usa `EnsureCreated`.

---

## 3. Guía de razonamiento y decisiones técnicas

### 3.1 Decisiones detectadas

1. **Auth dual (Windows Negotiate + Local con tokens Data Protection)**: `Program.cs:26-27` registra Negotiate, pero `AuthSettings.Mode` default `"Local"` (`AuthSettings.cs:16`) usa usuario/contraseña + token DP (`ProtectorPurpose = "GuarinoApp.Auth.v1"`, `AuthTokenService.cs:15`). Permite desplegar en entornos sin AD (local/dev) y con AD (prod).
2. **Token en localStorage 12 h** (`appsettings.json:42`): simplicidad para Blazor Server (no requiere cookie handling); trade-off: vulnerable a XSS (mitigado por InputSanitizer + headers, pero sin CSP).
3. **Sin Repository puro** (handlers usan DbContext directo): reduce capas para un equipo pequeño; trade-off: lógica de query dispersa en handlers.
4. **CQRS con MediatR + pipeline behaviors**: sanitización y serialización centralizadas, handlers delgados.
5. **Estados de pallet como enum con constantes con nombre** (`EstadoPallet`): evita strings mágicos ('D'/'F'/'P'/'R'/'B') y permite traducción legible.
6. **RoleNames como fuente única**: constantes + grupos (`Todos`/`Financieros`/`Administracion`) + cadenas `*Autorizacion` para `[Authorize(Roles=...)]` de páginas. Evita literales de rol en `.razor`.
7. **Mapa de accesos por reflexión**: `MapaAccesosRutas` (Web/Services) lee `RouteAttribute`+`AuthorizeAttribute` de las páginas en startup → matriz ruta→roles canónica, sin duplicar reglas. Módulos restringidos se muestran bloqueados (🔒 + tooltip "Requiere: X — tu rol actual: Y") en lugar de ocultarse — solo UX, la seguridad real la impone `[Authorize]`.
8. **`RowsPerPage="25"` fijo en todas las grillas**: evita que el usuario cambie a 100/500 y vuelva al problema de "demasiadas filas". PageSizeOptions `{25}` (un solo valor).
9. **Paginación server-side en Auditoría** (`ServerData="@ServerReload"` + `state.Page/PageSize`): el handler consulta con Skip/Take y devuelve `TotalCount` para el pie "Mostrando X–Y de Z".
10. **Cero datos inventados**: handlers devuelven lista vacía real ante BD vacía/error (`EsDummy: false`); nunca fallbacks de ejemplo. `GetRegistroPesadas` lee de `LotesEmboquille`.
11. **ControlIntentosLogin Singleton en memoria**: lockout 5 fallos/15 min; trade-off: se pierde al reiniciar y no escala horizontalmente.
12. **InputSanitizationBehavior excluye campos sensibles** (Password/Hash/Token/Secret/Key) para no corromper hashes/tokens.

### 3.2 Trampas lógicas / puntos oscuros (generados por IA)

1. **`InputSanitizer` regex bloquea `;` y `--`**: rompe datos legítimos (comentarios, textos con punto y coma). Falso positivo de seguridad.
2. **`ControlIntentosLogin` Singleton en memoria**: no escala, se pierde al reiniciar. Para multi-instancia, migrar a BD/cache distribuido.
3. **`AuditService` con catch vacío**: fallos de auditoría silenciosos; puede ocultar problemas.
4. **`EnableFallbackIdentity` con `UserRoles.ADRIA=Admin`**: en modo Windows, si no hay identidad AD, se asigna Admin por nombre de máquina. Riesgo de escalación.
5. **`RegisterCommandHandler` otorga Admin al primer usuario** + `AllowRegistration` default `true`: si se habilita, el primer visitante es Admin.
6. **Contraseñas seed "Guarino2026"** en `DbInitializer`: credencial pública en código.
7. **Token en localStorage sin CSP**: XSS = robo de sesión hasta 12 h.
8. **Rate limiter definido pero no aplicado** (0 `RequireRateLimiting`): solo lockout en memoria.
9. **MCP `/mcp` no verifica estado del usuario en BD**: token válido de usuario desactivado sigue funcionando.
10. **`Program.cs` duplica el registro de `ClimateMonitoringHostedService`** (línea 107 en Web + línea 144 en Infrastructure) → se registra dos veces; el hosted service corre dos instancias.
11. **`AllowedHosts: "*"`** + health endpoints públicos sin auth.
12. **`appsettings.Development.json` con `DetailedErrors: true`** + `launchSettings` fija `ASPNETCORE_ENVIRONMENT=Development` → riesgo si se despliega sin cambiar.
13. **`OllamaAIService` de 1604 líneas** y `ProyeccionZafra.razor` de 1209 líneas → mantenibilidad baja.
14. **`LoadData` duplicado en 23 páginas** → cualquier cambio de patrón requiere tocar 23 archivos.
15. **`MapaAccesosRutas` resuelve roles por reflexión de `[Authorize]`** — si una página no tiene el atributo, se considera pública (sin bloqueo). Verificar que todas las páginas tengan `[Authorize]` (solo Login/Register no lo tienen, correcto).

---

## 4. Resumen ejecutivo para el desarrollador

GuarinoApp es una aplicación Blazor Server (.NET 8) para la gestión de una zafra citrícola: pesadas, lotes, stock de pallets, empaque, liquidaciones, contaduría, gerencia y riesgos climáticos. La arquitectura es por capas (Domain → Application → Infrastructure → Web) con CQRS vía MediatR: 13 Commands (escritura) y 32 Queries (lectura), handlers delgados que usan EF Core directamente sobre un DbContext Scoped. La autenticación es dual (Windows Negotiate o Local con PBKDF2 + tokens Data Protection de 12 h en localStorage), con lockout de 5 fallos/15 min. Los roles (Admin/Gerencia/Contador/Operador) se centralizan en `RoleNames` y se aplican tanto en `[Authorize]` de páginas como en la matriz de menú (por reflexión). La auditoría persiste cada acción en `LogEntries`. El sistema está diseñado para cero datos inventados: las grillas leen directamente de la BD (LotesEmboquille, Pallets, LogEntries, etc.) y devuelven vacío real ante error.

Los riesgos principales a tener en cuenta antes de tocar código: (1) las credenciales seed "Guarino2026" y el registro público que otorga Admin; (2) el token en localStorage sin CSP; (3) el rate limiter no aplicado; (4) el MCP que no verifica el estado del usuario en BD. La deuda técnica más visible son los archivos gigantes (OllamaAIService 1604 líneas, ProyeccionZafra.razor 1209) y el patrón `LoadData` duplicado en 23 páginas. Para recuperar el mapa mental: empezar por `Program.cs` (líneas 26-288) para el pipeline, `RoleNames.cs` para los roles, `ApplicationDbContext.cs` para el modelo, y `NavMenu.razor` + `Index.razor` para la navegación por rol.

---

*Generado a partir del código fuente verificado (file:line). No se inventaron datos.*

---

## Actualizaciones recientes (Oct 2026)

Cambios aplicados al código y reflejados en la documentación:

- **Filtro de módulos por rol (UI)**: `NavMenu.razor` y `Index.razor` muestran los módulos bloqueados con 🔒 + tooltip "Requiere: X — tu rol actual: Y" en lugar de ocultarlos. Componentes nuevos: `Shared/NavLinkSeguro.razor` y `Shared/EnlaceSeguro.razor`. Matriz de acceso por reflexión en `Services/MapaAccesosRutas.cs` (lee `[Authorize]` de las páginas → ruta→roles). Helper `Services/RolUsuario.cs` resuelve el rol actual con `RoleNames`. Registrado como singleton en `Program.cs`.
- **RoleNames como fuente única**: constantes `Admin`/`Gerencia`/`Contador`/`Operador` + grupos `Todos`/`Financieros`/`Administracion` + cadenas `*Autorizacion` para `[Authorize]`. Cero literales de rol en `.razor`.
- **Paginación fija de 25 filas** en las 30 grillas (`RowsPerPage="25"` + `MudTablePager PageSizeOptions="new int[] { 25 }"`). Auditoría usa paginación server-side (`ServerData` + `state.Page/PageSize`).
- **Filtro IA por rol**: `ConsultaIArestringida` (log) + matriz de roles por tool MCP.
- **Servidor MCP** (`/mcp`) con `WithHttpTransport()` + middleware Bearer propio (`McpAuthMiddleware`); tools solo lectura; auditoría `McpCall`.
- **Cero datos inventados**: `GetDummyData`/`GenerarZafrasDummy`/fallbacks eliminados; `EsDummy` siempre `false`; "En nuestro poder" = sin cliente y Estado != Bajado/Despachado.
- **Autenticación**: Negotiate + Data Protection tokens (`GuarinoApp.Auth.v1`, 12 h, localStorage `auth_token`); lockout 5 fallos/15 min.
- **Grillas sin paginador** reemplazado por paginación de 25 (instrucción vigente).
- **Build**: 0 errores / 0 advertencias. UI en español.