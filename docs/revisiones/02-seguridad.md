# Revisión de seguridad — GuarinoApp (Blazor Server + MudBlazor)

**Fecha:** 2026-10-20
**Alcance:** `GuarinoApp.Web`, `GuarinoApp.Application`, `GuarinoApp.Infrastructure` (revisión de solo lectura; se ignoraron `Propuesta/`, `bin/`, `obj/`).
**Enfoque:** autenticación, autorización, manejo de roles, validación de datos, sanitización, protección XSS/CSRF, manejo de excepciones y acceso indebido a componentes.

---

## HALLAZGOS

### 1. 🔴 ALTA — Páginas sin `[Authorize]`: el único control es un gate client-side que no es frontera de seguridad

**Archivos:**
- `GuarinoApp.Web\Pages\Dashboard.razor:1`, `StockPlaya.razor:1`, `StockGeneral.razor:1`, `FrutaVolcar.razor:1`, `LotesVolcados.razor:1`, `PalletsSinCliente.razor:1`, `RegistroPesadas.razor:1`, `Counter.razor:1`, `Index.razor:1` — ninguno declara `@attribute [Authorize]`.
- `GuarinoApp.Web\Shared\MainLayout.razor:49-64` — el "gate" solo hace `Navigation.NavigateTo("/login?returnUrl=...")`.
- `GuarinoApp.Web\App.razor:4-6` — el comentario asume que sin atributo la página "queda protegida por MainLayout".
- `GuarinoApp.Web\Program.cs:242` — `app.MapBlazorHub()` sin `.RequireAuthorization()`.

**Riesgo:** en Blazor Server, el hub SignalR (`/_blazor`) acepta circuitos sin autenticación y `AuthorizeRouteView` renderiza cualquier página sin atributo. El redirect del layout ocurre *después* de instanciar el componente: el `OnInitializedAsync` de `Dashboard.razor:249` (`LoadData()` → `DashboardService.GetTotalStockAsync`, etc.) o `StockPlaya.razor:79` **ya se ejecuta en el servidor y manda los datos al circuito** antes de que el redirect pinte. Un atacante sin sesión que apunte al circuito puede disparar consultas a la BD y, en la práctica, recibir datos de negocio (stock, KPIs, volcados). Además, cualquier usuario autenticado (incluso `Operador`) entra a esas pantallas porque no hay política de rol.

**Corrección:**
```csharp
// Program.cs — fallback: toda ruta exige sesión salvo [AllowAnonymous]
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
// ...
app.MapBlazorHub().RequireAuthorization();
```
Y agregar `@attribute [Authorize(Roles = "Admin, Gerencia, Contador, Operador")]` (o la política que corresponda) a cada página que hoy depende del layout.

---

### 2. 🔴 ALTA — Registro abierto en producción y el primer usuario registrado se auto-nombra Admin

**Archivos:**
- `GuarinoApp.Web\appsettings.json:41` — `"AllowRegistration": true`.
- `GuarinoApp.Web\appsettings.Production.json` — **no define la sección `Auth`**, así que en producción también rige `true`.
- `GuarinoApp.Web\Pages\Register.razor:2` — `[AllowAnonymous]`.
- `GuarinoApp.Application\Modules\Auth\Commands\Register\RegisterCommandHandler.cs:59-61` — `hasUsers ? DefaultRole : AdminRole`.

**Riesgo:** cualquiera que alcance la URL `/register` crea cuentas en producción. Peor: si la base queda vacía (restore fallido, migración en instancia nueva, etc.), **el primero en registrarse se convierte en Admin** y tiene control total (Usuarios, Auditoría, Contactos, exportación de reportes).

**Corrección:**
```json
// appsettings.Production.json
"Auth": { "Mode": "Local", "AllowRegistration": false, "TokenValidityHours": 12, "DefaultRole": "Operador" }
```
Y en `RegisterCommandHandler`, eliminar la auto-promoción a Admin (sembrar el Admin por seed controlado/CLI, no por endpoint anónimo).

---

### 3. 🔴 ALTA — El chat de IA bypasea la autorización por rol: un Operador lee datos financieros, liquidaciones, gerencia y auditoría

**Archivos:**
- `GuarinoApp.Web\Pages\IAChat.razor:2` — `[Authorize(Roles = "Admin, Gerencia, Contador, Operador")]` (accesible a todos).
- `GuarinoApp.Infrastructure\Services\Skills\FinancialDataSkill.cs:35-59` — sin chequeo de rol; expone costo/kg, flujo de caja, margen. La página equivalente `/contaduria` exige `Admin, Gerencia, Contador` (`Contaduria.razor:2`).
- `GuarinoApp.Infrastructure\Services\Skills\ActividadAuditoriaSkill.cs:35-55` — devuelve logs de auditoría (quién hizo qué). `/auditoria` exige `Admin, Gerencia` (`Auditoria.razor:2`).
- `GuarinoApp.Infrastructure\Services\Skills\IndicadoresGerenciaSkill.cs:34-54` — KPIs de `/gerencia` (que exige `Admin, Gerencia`, `Gerencia.razor:2`).
- `GuarinoApp.Infrastructure\Services\Skills\ResumenLiquidacionesSkill.cs:34-73` — montos a pagar a productores; `/resumenliquidaciones` exige `Admin, Gerencia, Contador` (`ResumenLiquidaciones.razor:2`).

**Riesgo:** un Operador escribe "¿quién hizo qué últimamente?" o "¿cómo viene el flujo de caja?" en `/iachat` y recibe información que las pantallas le prohíben. Es escalada de privilegios por inconsistencia de roles. Nota: `ContactosImportSkill.cs:69-70` **sí** hace el chequeo — el patrón existe, falta aplicarlo al resto.

**Corrección:**
```csharp
public async Task<SkillOutput> ExecuteAsync(CancellationToken ct)
{
    if (!_currentUser.IsInRole("Admin") && !_currentUser.IsInRole("Contador"))
        return SkillOutput.Error("Requiere perfil Admin o Contador.");
    // ...
}
```
(Repetir en `ActividadAuditoriaSkill` (Admin/Gerencia), `IndicadoresGerenciaSkill` (Admin/Gerencia), `ResumenLiquidacionesSkill` (Admin/Gerencia/Contador), inyectando `ICurrentUserService`.)

---

### 4. 🔴 ALTA — Login sin rate limiting ni bloqueo; la política de rate limiting registrada nunca se aplica

**Archivos:**
- `GuarinoApp.Web\Program.cs:130-139` — se define la ventana fija `"fixed"` (100/min), pero **ningún endpoint la usa**: no hay `RequireRateLimiting("fixed")`, ni `EnableRateLimiting`, ni `GlobalLimiter` en todo el código (única referencia es documental en `docs\SEGURIDAD.md:303`).
- `GuarinoApp.Application\Modules\Auth\Commands\Login\LoginCommandHandler.cs:32-46` — solo audita el fallo; no hay contador de intentos, delay ni lockout.

**Riesgo:** el login viaja por el circuito Blazor (no es un POST HTTP tradicional), así que ni siquiera el rate limiter HTTP lo cubriría. Un atacante puede fuerza-brutear contraseñas indefinidamente; la política "mínimo 6 caracteres" (hallazgo 11) lo hace viable. Recordar además que `DbInitializer` crea usuarios `gerencia`/`contador`/`operador` con contraseña conocida (hallazgo 5).

**Corrección:**
```csharp
// LoginCommandHandler: lockout progresivo basado en intentos recientes
var intentos = await _context.LogEntries.CountAsync(l =>
    l.Action == "LoginFailed" && l.Description.Contains(userName)
    && l.Timestamp > DateTime.UtcNow.AddMinutes(-15), ct);
if (intentos >= 5)
    return AuthSessionDto.Fail("Demasiados intentos. Esperá 15 minutos.");
```
Más rate limiting por IP en el hub: `services.AddRateLimiter(o => o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(...))`.

---

### 5. 🟠 MEDIA — Credenciales por defecto y mapeos de rol hardcodeados en el repositorio

**Archivos:**
- `GuarinoApp.Infrastructure\Data\DbInitializer.cs:1158` — `PasswordHash = hasher.Hash("Guarino2026")` para `gerencia`, `contador`, `operador` (líneas 1138-1143). Aunque `Initialize` corre solo en Development (`Program.cs:289`), si la base de desarrollo se copia a producción (patrón común con LocalDB→servidor) esas cuentas quedan activas con credencial pública.
- `GuarinoApp.Web\appsettings.json:35-37` — `"UserRoles": { "ADRIA": "Admin" }`: mapeo de administrador por máquina/usuario en texto plano, versionado.

**Riesgo:** credencial por defecto conocida = acceso directo a roles operativos; el mapeo `ADRIA→Admin` revela identidad del admin y, combinado con la fallback identity (hallazgo 6), escala en modo Windows.

**Corrección:** no siembra contraseñas fijas — forzar cambio en primer ingreso (`MustChangePassword` en `AppUser` y chequeo en `CustomAuthenticationStateProvider`), y sacar `Security:UserRoles` a user-secrets / variables de entorno (`Security__UserRoles__ADRIA`).

---

### 6. 🟠 MEDIA — Fallback identity: en modo Windows, cualquier conexión sin identidad recibe rol Gerencia (o el usuario del servidor)

**Archivos:**
- `GuarinoApp.Infrastructure\Services\CustomAuthenticationStateProvider.cs:86-91` y `160-174` — si Windows Auth no entrega identidad y `EnableFallbackIdentity=true`, construye una identidad autenticada con `Environment.UserName` (**el usuario del proceso servidor, no del cliente**) y `SecuritySettings.DefaultRole`.
- `GuarinoApp.Application\Settings\SecuritySettings.cs:15-18` — default `EnableFallbackIdentity = true`, `DefaultRole = "Gerencia"`.
- `GuarinoApp.Web\appsettings.json:32-38` — `"EnableFallbackIdentity": true`, `"DefaultRole": "Gerencia"`.

**Riesgo:** al pasar a `Auth:Mode=Windows`, todo cliente que no reciba token Negotiate (browser sin credenciales, bots, escáneres) queda **autenticado como el usuario del servidor con rol Gerencia**, que autoriza Gerencia/Contactos/Auditoría/Usuarios. Es una puerta trasera de autenticación.

**Corrección:**
```json
"Security": { "EnableFallbackIdentity": false, "DefaultRole": "Operador", "UserRoles": {} }
```
Y en `ResolveAuthenticationStateAsync`, devolver `ClaimsPrincipal` vacío cuando falla Windows Auth en vez de una identidad fabricada.

---

### 7. 🟠 MEDIA — Token de sesión en `localStorage` y sin revocación server-side

**Archivos:**
- `GuarinoApp.Web\Pages\Login.razor:109` y `Register.razor:133` — `JS.InvokeVoidAsync("localStorage.setItem", AuthStorage.TokenKey, result.Token)`.
- `GuarinoApp.Infrastructure\Services\CustomAuthenticationStateProvider.cs:181` — la sesión se lee desde `localStorage` vía JS interop.
- `GuarinoApp.Infrastructure\Services\Auth\AuthTokenService.cs:36` — la única invalidación es `ExpiresAtUtc`; no hay lista de revocación.
- `GuarinoApp.Web\Shared\CurrentUserDisplay.razor:81` — el logout solo borra el `localStorage` del navegador.

**Riesgo:** cualquier XSS (ver hallazgo 9: no hay CSP) roba el token y la sesión sigue válida hasta 12 h (`appsettings.json:42`) aunque el usuario real "cierre sesión", porque nada la revoca en el servidor. El token además viaja por el interop JS en claro dentro del circuito.

**Corrección (mínima):** incluir un `SessionId`/`Jti` en el payload y persistirlo en BD (`AppUserSessions`), validándolo en `BuildLocalIdentityAsync` y borrándolo en logout; ideal: cookie `HttpOnly; Secure; SameSite=Strict` para la sesión en vez de `localStorage`. Acelerar `TokenValidityHours` a 1-2 h para app interna.

---

### 8. 🟠 MEDIA — Endpoints de health sin autorizar que filtran información interna

**Archivos:**
- `GuarinoApp.Web\Program.cs:235-239` — `app.MapHealthChecks("/health")` sin `RequireAuthorization`.
- `GuarinoApp.Infrastructure\Services\SqlServerHealthCheck.cs:42` — `Unhealthy("Error al verificar SQL Server: " + ex.Message)`.
- `GuarinoApp.Infrastructure\Services\OllamaHealthCheck.cs:41` — expone `Ollama:Url` y modelo en el cuerpo.

**Riesgo:** cualquiera infiere stack interno (URL de Ollama, modelo, mensajes de error de SQL que pueden incluir nombres de instancia/basedatos) — información de valor para un ataque dirigido.

**Corrección:**
```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,                 // solo agregado, sin detalle
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).RequireAuthorization("Admin");           // o restringir por red en el proxy
// y en SqlServerHealthCheck: HealthCheckResult.Unhealthy("SQL Server no disponible")
```

---

### 9. 🟠 MEDIA — Sin Content-Security-Policy ni `require-trusted-types`; scripts inline y CDN sin SRI

**Archivos:**
- `GuarinoApp.Web\Program.cs:211-221` — agrega `X-Content-Type-Options`, `X-Frame-Options`, etc., pero **no CSP**.
- `GuarinoApp.Web\Pages\_Host.cshtml:41-46, 49` — `<script>` inline (`Blazor.start()`, helpers de descarga) y `apexcharts.min.js`.
- `GuarinoApp.Web\wwwroot\js\climateMap.js:42-47` — carga Leaflet desde `unpkg.com` **sin SRI ni versión integrity**, y `bindingPopup` con HTML construido (línea 128).

**Riesgo:** sin CSP, un XSS (vía dato persistido o script de terceros comprometido) ejecuta con libertad — y roba el token del hallazgo 7. El CDN sin SRI es vector de supply-chain.

**Corrección:**
```csharp
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; img-src 'self' data: https://server.arcgisonline.com; " +
        "connect-src 'self' wss:");
    await next();
});
```
Mover los `<script>` inline de `_Host.cshtml` a archivos `.js` en `wwwroot/js` (hoy ya existen `download.js`, `climateMap.js`; el duplicado en `_Host.cshtml:59-94` de `download.js` es candidato a eliminar).

---

### 10. 🟡 BAJA — Enumeración de usuarios en mensajes de login y registro

**Archivos:**
- `GuarinoApp.Application\Modules\Auth\Commands\Login\LoginCommandHandler.cs:48-55` — usuario inexistente → "Usuario o contraseña incorrectos"; usuario deshabilitado → **"Tu usuario está deshabilitado"**.
- `RegisterCommandHandler.cs:56-57` — "Ese usuario ya existe".

**Riesgo:** un atacante arma la lista de cuentas válidas para el fuerza-bruta del hallazgo 4.

**Corrección:** unificar el mensaje ("Usuario o contraseña incorrectos." para todos los casos fallidos salvo el propio usuario) y mantener el detalle solo en la auditoría (`_audit.LogAsync` ya lo registra).

---

### 11. 🟡 BAJA — Política de contraseñas mínima (6 caracteres, sin complejidad)

**Archivos:**
- `RegisterCommandHandler.cs:51` y `CreateUserCommandHandler.cs:58` — `password.Length < 6`.

**Riesgo:** contraseñas débiles + sin lockout (hallazgo 4) = compromiso factible por diccionario.

**Corrección:**
```csharp
if (password.Length < 12 || !Regex.IsMatch(password, @"[A-Z]") || !Regex.IsMatch(password, @"[0-9]"))
    return AuthResultDto.Fail("La contraseña debe tener al menos 12 caracteres, con mayúscula y número.");
```
(Mejor aún: validar contra la lista de passwords comprometidos de Have I Been Pwned vía k-anonymity.)

---

### 12. 🟡 BAJA — `ex.Message` propagada hacia la interfaz en el servicio de correo

**Archivos:**
- `GuarinoApp.Infrastructure\Services\EmailService.cs:89` y `:266` — `EmailResult.Fail($"Error al enviar email: {ex.Message} | Detalle: {ex.InnerException.Message}")`.

**Riesgo:** si el SMTP rechaza con un error interno (host, credencial, ruta de red), el detalle llega al diálogo de la pantalla y expone topología interna.

**Corrección:** devolver mensaje genérico al llamador y dejar el detalle solo en `_logger.LogError(ex, ...)` (que ya existe en las líneas 88/265).

---

### 13. 🟡 BAJA — Secretos e ingredientes de conexión en appsettings versionados

**Archivos:**
- `GuarinoApp.Web\appsettings.json:11` — `"Trusted_Connection=true;Encrypt=false"`.
- `GuarinoApp.Web\appsettings.Development.json:3` — `TrustServerCertificate=True`.
- `GuarinoApp.Web\appsettings.Production.json:3` — `User Id=PROD_USER;Password=PROD_PASSWORD;TrustServerCertificate=True` (placeholder, pero el patrón invita a commitear el real) y `:83` — `"Password": ""` de SMTP.

**Riesgo:** `Encrypt=false`/`TrustServerCertificate=True` permite MITM entre app y SQL; el patrón de config en repo termina con credenciales reales commiteadas (el historial git no se olvida).

**Corrección:** secretos en user-secrets (dev) o variables de entorno/Key Vault (prod): `ConnectionStrings__DefaultConnection`, `Email__Password`; y `Encrypt=True;TrustServerCertificate=False` con certificado válido.

---

### 14. 🟡 BAJA — La auditoría traga todos los errores en silencio

**Archivos:**
- `GuarinoApp.Infrastructure\Services\AuditService.cs:51-54` — `catch { }` vacío.

**Riesgo:** si el log de auditoría falla (BD llena, columna mal tipada), el sistema sigue operando sin rastro de quién hizo qué — un atacante puede ejecutar acciones sin dejar evidencia, y nadie se entera.

**Corrección:**
```csharp
catch (Exception ex)
{
    _logger.LogCritical(ex, "FALLO CRÍTICO al persistir auditoría: {Action}/{Module}", action, module);
    // opcional: buffer en memoria + reintento
}
```
(inyectando `ILogger<AuditService>`).

---

### 15. 🟡 BAJA — Handlers de lectura/edición de usuarios sin verificación de rol server-side (defensa en profundidad)

**Archivos:**
- `GuarinoApp.Application\Modules\Auth\Queries\GetUsers\GetUsersQueryHandler.cs:15-32` — devuelve todos los usuarios (email, rol, fechas de acceso) sin chequear quién pregunta; solo el atributo de `Usuarios.razor:2` protege la pantalla.
- `UpdateUserCommandHandler.cs:36-96` — valida reglas de negocio pero **nunca verifica que el llamador sea Admin o Gerencia**; un `Operador` solo no llega porque la UI no lo muestra.

**Riesgo:** cualquier componente nuevo, skill o refactor que envíe estos comandos/heroes sin pasar por la pantalla habilita lectura del directorio de usuarios o edición de roles. En la app ya existe el precedente de la IA invocando handlers (`SkillRegistry`) — el riesgo es real, no teórico.

**Corrección:**
```csharp
// GetUsersQueryHandler.Handle — primera línea
if (!_currentUser.IsInRole("Admin") && !_currentUser.IsInRole("Gerencia"))
    throw new UnauthorizedAccessException();
// UpdateUserCommandHandler: if (!isAdmin && !isGerencia) return Fail(...);
```

---

### 16. 🔵 INFO — Consultas de usuario salen crudas a servidores MCP externos

**Archivo:** `GuarinoApp.Infrastructure\Services\McpDataService.cs:170-176` — `BuildArgs` interpola la consulta del usuario tal cual hacia `deepwiki.com` / `context7.com` (`appsettings.json:85-87`).

**Riesgo:** un usuario puede pegar en el chat un dato interno (nombre de productor, monto) con la palabra "documentación del repositorio" y ese texto viaja a un tercero. La allowlist por configuración es buena; falta controlar *qué* sale.

**Corrección:** pasar por `SanitizadorTexto` + un filtro de patrones sensibles (CUIT, montos, `@empresa`) antes de `CallAsync`, y documentarlo en la política de privacidad interna.

---

## Lo que está bien

- **Hash de contraseñas sólido**: PBKDF2-SHA256 100k iteraciones con `CryptographicOperations.FixedTimeEquals` (`PasswordHasher.cs:21,50`) — sin timing attack.
- **Tokens firmados con Data Protection** de ASP.NET Core (`AuthTokenService.cs:23,32`), no JWT self-made; la sesión se revalida contra BD (usuario borrado/desactivado corta el circuito) en `CustomAuthenticationStateProvider.cs:196-203`.
- **XSS en HTML dinámico bien manejado**: `ProyeccionZafra.razor:1080-1117` aplica `WebUtility.HtmlEncode` a todo valor antes de `(MarkupString)`; `climateMap.js:23-29,109-111` escapa labels/info/código de popups; `SanitizadorTexto.cs` elimina controles y pseudoetiquetas HTML en formularios.
- **Sin SQL injection**: las únicas `ExecuteSqlRaw` (`DbInitializer.cs:380,964`) son strings constantes; los filtros migraron a LINQ tipado (`GetStockPlayaQueryHandler.cs:79-94`).
- **Open-redirect prevenido** en el returnUrl del login (`Login.razor:133-140`, descarta absolutas y `//`).
- **Reglas de negocio de usuarios server-side correctas**: último Admin nunca se desactiva, nadie se edita a sí mismo, asignación de rol Admin exclusiva de Admin (`UpdateUserCommandHandler.cs:58-96`); creación y cambio de correo exigidos a Admin en el handler (`CreateUserCommandHandler.cs:39`, `UpdateUserEmailCommandHandler.cs:36`).
- **Headers de seguridad y HSTS** presentes (`Program.cs:188-221`), HTTPS redirect y CORS restrictivo en producción (lista blanca por origen, `Program.cs:159-183`).
- **Auditoría de accesos y acciones** con usuario y rol (`AuditService.cs`, `LoginCommandHandler.cs:41-44,77-80`), incluida la actividad de la IA (`IAChat.razor:543`, `SkillRegistry.cs:110-120`, `McpDataService.cs:178-203`).
- **Configuración validada con `ValidateOnStart`** para Ollama, Email, IA web, etc. (`Program.cs:60-95`).

---

## Top 5 prioridades de remediación

1. **Cerrar el perímetro de autorización** (hallazgo 1): `FallbackPolicy = RequireAuthenticatedUser` + `MapBlazorHub().RequireAuthorization()` + `@attribute [Authorize]` explícito en las 9 páginas huérfanas (Dashboard, StockGeneral, StockPlaya, FrutaVolcar, LotesVolcados, PalletsSinCliente, RegistroPesadas, Counter, Index). Es la brecha que permite leer datos sin sesión.
2. **Deshabilitar el registro abierto en producción y eliminar la auto-promoción a Admin** (hallazgo 2): `AllowRegistration=false` en `appsettings.Production.json` y quitar `hasUsers ? DefaultRole : AdminRole` del `RegisterCommandHandler`.
3. **Aplicar autorización por rol dentro de las skills de IA** (hallazgo 3): hoy un Operador lee datos financieros, liquidaciones y auditoría por el chat, saltándose los `[Authorize]` de las pantallas.
4. **Proteger el login** (hallazgos 4 y 11): lockout por intentos fallidos en `LoginCommandHandler` + rate limiting real (la política `"fixed"` está registrada pero sin usar) + subir la política de contraseña a 12 caracteres.
5. **Endurecer sesión y configuración** (hallazgos 5, 6, 7): revocación server-side del token + cookie HttpOnly en vez de `localStorage`, `EnableFallbackIdentity=false` para cualquier despliegue Windows, y sacar del repo la contraseña sembrada `Guarino2026` y los mapeos `Security:UserRoles`.

---

*Revisión basada exclusivamente en lectura del código del repositorio. No se modificó ningún archivo, no se ejecutaron builds ni se tocó git o la base de datos durante el análisis.*
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