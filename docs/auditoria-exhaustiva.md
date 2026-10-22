# Auditoría exhaustiva — GuarinoApp (OWASP + UX + Deuda técnica)

**Proyecto:** GuarinoApp — .NET 8 + Blazor Server (GuarinoApp.Web) + EF Core + MediatR
**Base de auditoría:** código fuente del repo (HEAD `26f0301`), excluyendo `Propuesta/` y `csv Propuesta/`.
**Alcance:** Seguridad (OWASP Top 10), UX/UI en Blazor, deuda técnica y resiliencia.

> Cada hallazgo incluye: Severidad · Descripción exacta · Archivo y línea · Ejemplo de código corregido.
> Solo se reportan hallazgos verificados contra el código (file:line).

---

## 1. Seguridad avanzada y OWASP

### 1.1 Autenticación y autorización

**[CRÍTICA] Contraseña por defecto conocida "Guarino2026" para 3 cuentas**
- `GuarinoApp.Infrastructure/Data/DbInitializer.cs:1136-1137` (comentario "contraseña \"Guarino2026\"") y `:1176` `PasswordHash = hasher.Hash("Guarino2026")` para `gerencia`/`contador`/`operador`.
- Si la BD se siembra (DbInitializer), esas 3 cuentas quedan con credencial pública. Cualquier persona con acceso a la app entra como esos roles.
- Corrección:
```csharp
// DbInitializer: generar contraseña aleatoria por entorno y forzar cambio al primer login
var seedPwd = Environment.GetEnvironmentVariable("GUARINO_SEED_PWD")
              ?? throw new InvalidOperationException("GUARINO_SEED_PWD no configurado");
user.PasswordHash = hasher.Hash(seedPwd);
user.MustChangePassword = true; // nueva flag en AppUser
```

**[CRÍTICA] Primer registro se convierte en Admin + registro habilitado por defecto**
- `GuarinoApp.Application/Modules/Auth/Commands/Register/RegisterCommandHandler.cs:58-59` `var roleName = hasUsers ? _authSettings.DefaultRole : RoleNames.Admin;`
- `GuarinoApp.Application/Settings/AuthSettings.cs:19` `AllowRegistration` default `true`, aunque `appsettings.json:41` lo pone `false`. Si se activa en producción, el primer visitante obtiene Admin.
- Corrección: deshabilitar registro público en producción (bloquear endpoint) y no otorgar Admin automáticamente; crear el primer admin por script/seed controlado.

**[ALTA] Token de sesión en `localStorage` sin revocación + sin CSP**
- `GuarinoApp.Web/Pages/Login.razor:109` `localStorage.setItem("auth_token", token)` (duración 12 h, `appsettings.json:42`).
- `GuarinoApp.Infrastructure/Services/CustomAuthenticationStateProvider.cs:181` lee el token; `GuarinoApp.Infrastructure/Services/Auth/AuthTokenService.cs:22-46` firma con Data Protection (`ProtectorPurpose = "GuarinoApp.Auth.v1"`).
- No hay revocación server-side ni CSP → un XSS roba el token y lo usa hasta 12 h.
- Corrección: migrar a cookie `HttpOnly`/`Secure`/`SameSite=Strict` + agregar CSP (ver 1.2).

**[ALTA] Rate limiter definido pero no aplicado**
- `GuarinoApp.Web/Program.cs:159-168` política `"fixed"` (100 req/min, queue 10); `:257` `UseRateLimiter()`.
- No hay `RequireRateLimiting`/`EnableRateLimiting`/`GlobalLimiter` (solo docs) → login y `/mcp` sin límite HTTP; solo lockout en memoria tras 5 fallos por usuario.
- Corrección:
```csharp
app.UseRateLimiter();
// en el endpoint de login / MCP:
app.MapPost("/api/login", ...).RequireRateLimiting("fixed");
```

**[ALTA] MCP `/mcp` valida solo token/expiración, no estado del usuario en BD**
- `GuarinoApp.Web/Mcp/McpAuthMiddleware.cs:31-32` valida token y expiración; `:51-56` responde 401 si no válido.
- Usuario desactivado/eliminado con token válido sigue consultando hasta 12 h; rol del token queda stale.
- Corrección: verificar en el middleware que el usuario siga activo en BD (consulta rápida por UserName) y refrescar el rol.

**[MEDIA] Identidad de respaldo habilitada con rol por defecto**
- `appsettings.json:32-38` `EnableFallbackIdentity: true`, `DefaultRole: "Gerencia"`, `UserRoles: { "ADRIA": "Admin" }`.
- `CustomAuthenticationStateProvider.cs:86-91,159-174` asigna rol por `Environment.UserName` en modo Windows sin identidad AD.
- Corrección: deshabilitar `EnableFallbackIdentity` en producción (`false`).

**[MEDIA] Sin Content-Security-Policy + Google Fonts sin SRI**
- `Program.cs:240-250` solo headers legacy (`X-Content-Type-Options`, `X-Frame-Options`, etc.); no hay `Content-Security-Policy`.
- `_Host.cshtml:21-22` carga Google Fonts por CDN sin SRI.
- Corrección: agregar CSP con `default-src 'self'; script-src 'self' 'unsafe-inline' (Blazor requiere); font-src 'self' https://fonts.gstatic.com` + SRI en el link.

**[MEDIA] CORS en Development permite cualquier origen**
- `Program.cs:198-203` `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` si `IsDevelopment`.
- Si se despliega con `ASPNETCORE_ENVIRONMENT=Development` (launchSettings), se abre cross-origin.
- Corrección: validar en arranque que en producción no se use Development.

**[MEDIA] Auditoría silenciosa (catch vacío)**
- `GuarinoApp.Infrastructure/Services/AuditService.cs:52-55` catch vacío → fallos de auditoría no se detectan.
- Corrección: loguear el error (`_logger.LogError`) aunque no se propague.

**[MEDIA] Detalles de error habilitados + AllowedHosts "*" + health públicos**
- `appsettings.Development.json:6` `"DetailedErrors": true`; `appsettings.json:9` `"AllowedHosts": "*"`.
- `Program.cs:275-279` health checks públicos sin autenticación.
- Corrección: restringir health checks a red interna o requerir auth en producción.

**[BAJA] InputSanitizer regex puede romper datos legítimos**
- `GuarinoApp.Application/Common/Security/InputSanitizer.cs:37-38` regex `(--|/\*|\*/|;|`) bloquea cualquier string con `;` o `--` (incluye comentarios, textos con punto y coma).
- `InputSanitizationBehavior.cs:74` aplica a toda propiedad string de requests MediatR (profundidad 3); excluye `Password/Hash/Token/Secret/Key`.
- Corrección: usar validación contextual (parámetros EF ya son seguros) y reservar el sanitizer para campos de texto libre; quitar el bloqueo de `;`/`--` o hacerlo opt-in por propiedad.

**[BAJA] MCP `/mcp` sin rate limiting**
- `Program.cs:286` `MapMcp("/mcp")` sin `RequireRateLimiting`.
- Corrección: aplicar la política `"fixed"` al endpoint.

### 1.2 Inyección SQL

- **Sin SQL nativo interpolado**: solo `ExecuteSqlRaw` con strings constantes en `DbInitializer.cs:388, 982` (sin `$`).
- **Queries parametrizadas**: handlers usan EF Core LINQ (`LoginCommandHandler.cs:52-54` `FirstOrDefaultAsync(u => u.UserName == userName && !u.IsDeleted)`). ✅
- **MCP tools** son de solo lectura (`GuarinoMcpTools.cs:20` comentario) y revalidan matriz de roles (`:160-162`), auditan `McpCall` (`:188-193`).

### 1.3 XSS

- Sanitización central: `InputSanitizer.cs` + `InputSanitizationBehavior.cs`.
- `WebUtility.HtmlEncode` en `ProyeccionZafra.razor:1149-1186` (valores de IA codificados antes de `MarkupString`).
- `@((MarkupString)...)` solo en `ProyeccionZafra.razor:1160, 1193` — con HtmlEncode previo. ✅
- No se encontró `HtmlSanitizer`, `[AllowHtml]` ni `Html.Raw`. ✅

---

## 2. Usabilidad y UX en Blazor

### 2.1 Feedback visual durante operaciones asíncronas

**[MEDIA] 14 de 28 páginas con `_loading` no muestran spinner/skeleton**
- 28 páginas usan `_loading` (`grep _loading`), pero solo 14 usan `MudProgressLinear`/`MudProgressCircular`.
- Las otras 14 (p. ej. `Contaduria.razor`, `Facturacion.razor`) bloquean la UI sin indicador visual mientras cargan datos pesados (IA hasta 12 s).
- Corrección: agregar en el layout de cada página:
```razor
@if (_loading) { <MudProgressLinear Indeterminate="true" Color="Color.Primary" /> }
```

**[BAJA] Operaciones largas sin progreso granular**
- `OllamaAIService` consume hasta 12 s; no hay indicador de "analizando..." en los módulos de IA.
- Corrección: mostrar `MudProgressCircular` con texto "Analizando..." mientras `_analizando`.

### 2.2 Manejo de errores en pantalla

**[MEDIA] 25 páginas exponen `ex.Message` directamente al usuario**
- 25 de 35 páginas .razor usan `ex.Message`/`ex.ToString` en `Snackbar.Add(..., Severity.Error)` o texto.
- Puede filtrar detalles internos (rutas, errores de BD) al usuario final.
- Corrección: mostrar mensaje genérico ("Ocurrió un error. Intente nuevamente.") y loguear `ex` con `_logger.LogError(ex, ...)`.

### 2.3 Validación reactiva

- `Usuarios.razor` valida server-side (CreateUserCommandHandler: userName ≥3, password ≥6, email válido) y muestra el error en Snackbar. ✅
- `Contactos.razor` valida correo/teléfono (8-15 dígitos) en servidor. ✅
- `Data Annotations`/`FluentValidation`: no se encontró uso de `[Required]`/`[EmailAddress]` con `EditForm`+`DataAnnotationsValidator` en las páginas (validación manual en handlers). Oportunidad de mejora: agregar `EditForm` con `DataAnnotationsValidator` para feedback inmediato en cliente.

### 2.4 Accesibilidad básica

- Uso de `MudTextField Label="..."` (MudBlazor genera `aria-label` automáticamente). ✅
- `for` en labels manuales: no se detectaron etiquetas `<label for="...">` manuales; se confía en MudBlazor. Aceptable.
- Navegación por teclado: `MudTablePager` y `MudSelect` soportan teclado. ✅

---

## 3. Criterios de Líder Técnico

### 3.1 Deuda técnica

**[MEDIA] Archivos gigantes (>500 líneas)**
- Top 10: `OllamaAIService.cs` (1604), `ProyeccionZafra.razor` (1209), `IAChat.razor` (762), `OllamaRecuperacionService.cs` (758), `BaseDatosRecuperacionService.cs` (725), `Contaduria.razor` (613), `Dashboard.razor` (590), `GestionRiesgosClimaticos.razor` (561), `FallbackAIService.cs` (551), `OpenMeteoClimateService.cs` (522).
- Corrección: extraer servicios parciales / componentes Blazor hijos (subcomponentes con `@code` propio).

**[MEDIA] Patrón `LoadData` duplicado en 23 páginas**
- 23 páginas `.razor` implementan `async Task LoadData()` con la misma estructura (`_loading = true; try { _data = await Mediator.Send(...); } finally { _loading = false; }`).
- Corrección: extraer un helper/composable `PageDataLoader<T>` o un componente base `DataPageBase<T>`.

**[BAJA] Sin TODO/FIXME/HACK en código fuente** (solo en `wwwroot`/`_site` generados). ✅

### 3.2 Resiliencia y concurrencia

**[MEDIA] `AuditService` con catch vacío + sin propagación**
- `AuditService.cs:52-55` → fallos de auditoría silenciosos.
- Corrección: loguear + reintentar o al menos no ocultar.

**[BAJA] Transacciones concurrentes en EF Core**
- No se detectaron `BeginTransaction`/`SaveChanges` concurrentes problemáticos; handlers usan DbContext Scoped (1 por circuito Blazor) → sin race conditions evidentes. ✅
- `ControlIntentosLogin` es `Singleton` (`Application/DependencyInjection.cs:36`) con estado en memoria → se pierde al reiniciar y no escala horizontalmente. Corrección: persistir intentos en BD o usar cache distribuido.

### 3.3 Ciclo de vida de componentes Blazor

- `OnInitializedAsync` en 32 páginas; `LoadData` en 23. Patrón consistente. ✅
- No se detectaron `IDisposable`/`IAsyncDisposable` faltantes en componentes con suscripciones (no hay event handlers manuales evidentes). ✅
- `CustomAuthenticationStateProvider` es Scoped → se recrea por circuito. ✅

---

## Resumen ejecutivo

**Postura general:** La app tiene una base sólida (auth dual, lockout PBKDF2 100k iter, EF parametrizado, headers de seguridad, auditoría completa, rate limiter definido). Los riesgos más serios son operativos: credenciales seed públicas, registro público que otorga Admin, token en localStorage sin CSP, y rate limiter no aplicado. La UX es funcional pero le falta feedback visual en 14 páginas y expone mensajes de excepción en 25. La deuda técnica principal son archivos gigantes y `LoadData` duplicado.

**Prioridad de acción (top 5):**
1. Rotar/eliminar credenciales seed "Guarino2026" y forzar cambio de contraseña.
2. Deshabilitar registro público o no otorgar Admin automáticamente.
3. Migrar token a cookie HttpOnly + agregar CSP.
4. Aplicar rate limiting a login y `/mcp`.
5. Verificar estado del usuario en BD en `McpAuthMiddleware`.

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