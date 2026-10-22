# Revisión de arquitectura — GuarinoApp

**Fecha:** 2026-10-20
**Alcance:** solución `C:\Claude-Proyectos\Guarino` (proyectos `GuarinoApp.Domain`, `GuarinoApp.Application`, `GuarinoApp.Infrastructure`, `GuarinoApp.Web`, `BenchmarkSuite1`).
**Excluido:** carpetas `Propuesta/`, `csv Propuesta/`, `bin/`, `obj/`.
**Método:** revisión de código 100 % lectura (sin builds, sin git, sin tocar la base de datos).
**Criterio de severidad:**
- **Alta** — riesgo de seguridad o de incorrecto funcionamiento en producción.
- **Media** — deuda que ya causa (o causará) problemas concretos: duplicación, inconsistencia, mantenibilidad, diagnóstico.
- **Baja** — limpieza, documentación y buenas prácticas.

**Total: 28 hallazgos (3 Altos, 18 Medios, 7 Bajos).**

---

## Hallazgos de severidad ALTA

### 1. [ALTA] Registro de usuarios abierto y login sin bloqueo por intentos fallidos

**Archivo:** `GuarinoApp.Application\Modules\Auth\Commands\Register\RegisterCommandHandler.cs:38-61`
**Archivo (config):** `GuarinoApp.Web\appsettings.json:41` → `"AllowRegistration": true`
**Archivo (login):** `GuarinoApp.Application\Modules\Auth\Commands\Login\LoginCommandHandler.cs:32-46`

**Por qué es un problema:**
Cualquier visitante puede crear una cuenta desde `/register` (la página está habilitada porque `MainLayout.EnforceAuthenticationAsync` permite `/login` y `/register` sin sesión — `Shared\MainLayout.razor:57-59`). El rol asignado a cada nuevo registro es `Auth:DefaultRole = "Operador"` (`appsettings.json:43`), un rol con acceso a casi todas las pantillas operativas. Además, `LoginCommandHandler` no tiene ningún contador de intentos fallidos: solo loguea el fracaso (`LoginCommandHandler.cs:41-45`) y responde. Un ataque de fuerza bruta contra la pantalla de login puede probar contraseñas sin límite alguno (la contraseña mínima admitida por registro es de solo 6 caracteres — `RegisterCommandHandler.cs:51-52`).

**Corrección propuesta:**

```jsonc
// appsettings.json → sección "Auth"
"AllowRegistration": false,   // alta de usuarios solo desde Usuarios.razor (rol Admin/Gerencia)
"MinPasswordLength": 8
```

```csharp
// AppUser.cs (Domain)
public int FailedLoginAttempts { get; set; }
public DateTime? LockedUntilUtc { get; set; }

// LoginCommandHandler.Handle, apenas se carga el user:
if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc > DateTime.UtcNow)
    return AuthSessionDto.Fail("Cuenta bloqueada por intentos fallidos. Probá en unos minutos.");

if (user == null || !_passwordHasher.Verify(...))          // rama existente, línea 39
{
    user.FailedLoginAttempts++;
    if (user.FailedLoginAttempts >= 5)
    {
        user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
        user.FailedLoginAttempts = 0;
    }
    await _context.SaveChangesAsync(ct);
    await _audit.LogAsync("LoginFailed", "Acceso", $"...");
    return AuthSessionDto.Fail("Usuario o contraseña incorrectos.");
}

user.FailedLoginAttempts = 0;
user.LockedUntilUtc = null;
```

Como alternativa mínima (sin tocar el modelo), aplicar rate limiting al login: ver hallazgo 4.

---

### 2. [ALTA] Fallback de identidad Windows que otorga el rol "Gerencia" a cualquier usuario desconocido

**Archivo:** `GuarinoApp.Infrastructure\Services\CustomAuthenticationStateProvider.cs:86-91`, `:143-144`, `:160-174`
**Archivo (settings):** `GuarinoApp.Application\Settings\SecuritySettings.cs:15-18`
**Archivo (config):** `GuarinoApp.Web\appsettings.json:33-34` → `"EnableFallbackIdentity": true`, `"DefaultRole": "Gerencia"`

**Por qué es un problema:**
Cuando la app corre en modo Windows (`Auth:Mode = "Windows"`) y no se obtiene identidad (equipo fuera del dominio, Negotiate no responde), el código **no rechaza el acceso**: construye una identidad de respaldo con `Environment.UserName` (`CustomAuthenticationStateProvider.cs:160-174`) y le asigna `SecuritySettings.DefaultRole`, que por configuración y por defecto en código es **"Gerencia"** (`SecuritySettings.cs:18`, `appsettings.json:34`). El mismo camino ocurre en `BuildWindowsIdentity`: si el usuario de Windows no tiene roles conocidos, recibe `DefaultRole` (`:143-144`). Resultado: cualquier usuario que loguee la sesión de Windows en esa máquina queda identificado como Gerencia — el segundo rol con más privilegios de la matriz (`Admin` es el único por encima). Este es el privilegio más peligroso que puede otorgar un fallback silencioso.

**Corrección propuesta:**

```jsonc
// appsettings.json → sección "Security"
"EnableFallbackIdentity": false,   // sin identidad Windows -> no sesión (ya es el comportamiento de la rama else)
"DefaultRole": "Operador"          // y nunca un rol de supervisión como default
```

```csharp
// CustomAuthenticationStateProvider.BuildWindowsIdentity, líneas 143-144:
if (roles.Count == 0)
    return null;   // sin rol resuelto -> no autenticar, en lugar de:
// roles.Add(_securitySettings.DefaultRole);
```

Y en `BuildFallbackIdentity` (`:160-174`), eliminar el método o hacer que devuelva `null` si `UserRoles` no contiene a `Environment.UserName` (el mapa ya existe en `appsettings.json:35-37` y es la vía legítima de asignación).

---

### 3. [ALTA] Fallback silencioso a datos dummy en ~15 handlers: la UI puede mostrar datos inventados en producción

**Archivos (muestra representativa):**
- `GuarinoApp.Application\Modules\Proyeccion\Queries\GetProyeccionZafra\GetProyeccionZafraQueryHandler.cs:51-55` — `catch (Exception) { return await GenerarZafrasDummy(...); }`
- `GuarinoApp.Application\Modules\Stock\Queries\GetPallets\GetPalletsQueryHandler.cs:68, 89`
- `GuarinoApp.Application\Modules\RegistroPesadas\Queries\GetRegistroPesadas\GetRegistroPesadasQueryHandler.cs:59, 69`
- `GuarinoApp.Application\Modules\ResumenLiquidaciones\Queries\GetResumenLiquidaciones\GetResumenLiquidacionesQueryHandler.cs:51-61`
- `GuarinoApp.Application\Modules\PalletsDefinitivos\Queries\GetPalletsDefinitivos\GetPalletsDefinitivosQueryHandler.cs:31, 83, 93`
- `GuarinoApp.Application\Modules\LotesVolcados\Queries\GetLotesVolcados\GetLotesVolcadosQueryHandler.cs:65, 73`
- `GuarinoApp.Application\Modules\Transportistas\...GetTransportistasQueryHandler.cs:42`, `Basculas`, `Lugares`, `LugaresOrigen`, `LugaresDestino`, `Empaque`, `GraficaProcesos`, `ResumenMensual`, `StockPlaya`, `StockPalletsSinCliente`, `PalletsSinCliente`, `PalletsContramarca`, `CamionesZorras` (todos con `GetDummyData()`).

**Por qué es un problema:**
Cuando la tabla está vacía **o** —peor— cuando **la consulta lanza una excepción**, los handlers devuelven filas inventadas en lugar de fallar. `GetProyeccionZafraQueryHandler.cs:53` captura `Exception` sin distinguir causa: una BD caída, un timeout o un error de esquema se traduce en una proyección de zafra fabricada presentada como si fuera del sistema. Aunque varios DTO ya exponen la bandera `EsDummy` (ej. `PalletsResultDto.cs:9`, `LotesVolcadosResultDto.cs:7`), varios caminos la emiten solo para "tabla vacía" y no para "error" (ej. `RegistroPesadasQueryHandler.cs:68-69` entra al mismo `GetDummyData` tras un `catch`), y otros handlers ni siquiera la tienen. En un sistema que alimenta decisiones de gerencia y contaduría, que un fallo de base de datos se disfracce de "datos reales" es peligroso.

**Corrección propuesta:**

```csharp
// Patrón recomendado en todos los handlers:
catch (Exception ex)
{
    _logger.LogError(ex, "Consulta {Query} falló", nameof(GetProyeccionZafraQuery));
    throw;                                   // propagar: la página ya tiene try/catch y muestra MudAlert
}

// "Tabla vacía" -> lista vacía + EsDummy=true (no filas inventadas),
// y eliminar los GetDummyData() de producción:
if (!hayRegistros)
    return new XResultDto(new List<XDto>(), EsDummy: true);
```

Si se conserva el modo demo, acotarlo con una bandera explícita de configuración:

```csharp
if (_env.IsDevelopment() && _config.GetValue<bool>("Features:DatosDemo"))
    return ...GetDummyData();
throw new InvalidOperationException("Sin datos reales para la consulta X.");
```

---

## Hallazgos de severidad MEDIA

### 4. [MEDIA] Política de rate limiting definida pero jamás aplicada

**Archivo:** `GuarinoApp.Web\Program.cs:130-139` (política `"fixed"`), `Program.cs:228` (`app.UseRateLimiter()`)

**Por qué es un problema:**
Se registra `AddFixedWindowLimiter("fixed", ...)` con 100 req/min, pero **ningún endpoint la referencia**: un grep en toda la solución no encuentra ni `[EnableRateLimiting]` ni `.RequireRateLimiting("fixed")` ni `RateLimitPartition`. El middleware `UseRateLimiter()` corre y nunca rechaza nada. Es protección de papel: el login (hallazgo 1) y los endpoints `/health*` quedan sin límite.

**Corrección propuesta:**

```csharp
// Program.cs:130 — además de la política concreta, límite global por IP:
using Microsoft.AspNetCore.RateLimiting;

options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 10
        }));

// y/o política más estricta para la señal de login:
// app.MapBlazorHub().RequireRateLimiting("auth-strict");
```

---

### 5. [MEDIA] `/health/ready` no tiene ningún check con tag "ready": siempre responde sin checks

**Archivo:** `GuarinoApp.Web\Program.cs:122-124` (alta de checks), `Program.cs:236-239` (predicate `Tags.Contains("ready")`)

**Por qué es un problema:**
Los checks se registran sin tags (`AddCheck<SqlServerHealthCheck>("sql-server")`, `AddCheck<OllamaHealthCheck>("ollama")`), pero `/health/ready` filtra por el tag `"ready"`. El predicate no matchea a ningún check, así que el endpoint de "readiness" no evalúa ni SQL ni Ollama: un orquestador o monitor que use `/health/ready` para decidir si el servicio está listo recibirá siempre la misma respuesta (sin entradas), incluso con la base de datos caída. `/health` (línea 235) sí evalúa todo — pero es el de "liveness", no el que se usa para readiness.

**Corrección propuesta:**

```csharp
// Program.cs:122
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql-server",
        failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "db" })
    .AddCheck<OllamaHealthCheck>("ollama",
        failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "ia" });
```

---

### 6. [MEDIA] 9 páginas sin `[Authorize]`: cualquier usuario autenticado (incluido Operador) puede abrirlas por URL

**Archivos:**
`GuarinoApp.Web\Pages\Counter.razor`, `Dashboard.razor`, `FrutaVolcar.razor`, `Index.razor`, `LotesVolcados.razor`, `PalletsSinCliente.razor`, `RegistroPesadas.razor`, `StockGeneral.razor`, `StockPlaya.razor`

**Contexto:** 35 páginas con `@page`, solo 24 con `@attribute [Authorize(...)]` (verificado por grep). `App.razor:4-7` lo admite explícitamente: "Sin atributo, la página queda protegida por el gate de MainLayout (cualquier usuario autenticado)". El gate de `MainLayout.razor:49-64` solo exige **autenticación**, nunca roles.

**Por qué es un problema:**
La matriz de roles está declarada en `NavMenu.razor` (ocultando links) y en los atributos de las páginas, pero esas 9 rutas quedan fuera de ambas. Cualquier usuario autenticado puede escribir la URL directamente y acceder a `RegistroPesadas`, `LotesVolcados`, `StockGeneral`, `Dashboard`, etc., ignorando la separación de roles que el resto de la app respeta (por ejemplo `StockPalletsSinCliente.razor:2` sí exige los 4 roles mientras su hermana `PalletsSinCliente.razor` no exige ninguno). Ocultar el link no es autorización.

**Corrección propuesta:** agregar la línea 2 a cada página, con el rol consistente con su hermana en `NavMenu.razor`:

```razor
@* Ejemplos *@
@attribute [Authorize(Roles = "Admin, Gerencia, Contador, Operador")]   @* Dashboard, RegistroPesadas, LotesVolcados, StockGeneral, StockPlaya, FrutaVolcar *@
@* Counter.razor: eliminar la página (es la plantilla por defecto de Blazor, no aporta nada) *@
```

---

### 7. [MEDIA] Commands de Contactos sin verificación de rol en la capa de aplicación

**Archivos:** `GuarinoApp.Application\Modules\Contactos\Commands\CrearContacto\CrearContactoCommandHandler.cs` (no contiene ningún `IsInRole`/check), ídem `ActualizarContacto`, `EliminarContacto`, `ImportarContactos` (grep sobre la carpeta `Contactos\Commands`: sin coincidencias de rol).

**Por qué es un problema:**
La única barrera es el atributo de la página (`Pages\Contactos.razor:2` → `[Authorize(Roles = "Admin, Gerencia")]`). Los commands aceptan la operación de cualquier caller. Contactos alimenta alarmas y notificaciones por teléfono (validación E.164 en `TelefonoE164.cs`, importación masiva en `ImportarContactosCommandHandler`), y **también son invocables desde otros caminos** (los skills de IA — `ContactosImportSkill`/`ContactosResumenSkill` están registrados en `Infrastructure\DependencyInjection.cs:167,176`), que no pasan por el atributo de la página. La regla de negocio "solo Admin/Gerencia tocan contactos" vive únicamente en la capa de presentación.

**Corrección propuesta:** comportamiento pipeline de autorización, declarado por atributo en cada command:

```csharp
[RequiresRole("Admin,Gerencia")]
public record CrearContactoCommand(...) : IRequest<...>;

// AuthorizationBehavior<TRequest,TResponse> en Application\DependencyInjection.cs:
// resuelve ICurrentUserService, lee RequiresRoleAttribute y aborta con UnauthorizedAccessException si no coincide.
// Registrar en: services.AddTransient<IPipelineBehavior<,>, AuthorizationBehavior<,>>();
```

---

### 8. [MEDIA] Registros DI duplicados: hosted service de clima y cliente HttpClient "Ollama"

**Archivos:**
- `GuarinoApp.Web\Program.cs:82` → `AddHostedService<ClimateMonitoringHostedService>()` **y** `GuarinoApp.Infrastructure\DependencyInjection.cs:144` → idem.
- `GuarinoApp.Web\Program.cs:97` → `AddHttpClient("Ollama", ...)` **y** `GuarinoApp.Infrastructure\DependencyInjection.cs:70` → `AddHttpClient("Ollama", ...)`.

**Por qué es un problema:**
En el hosted service, `AddHostedService` usa `TryAddEnumerable` y deduplica: no hay doble ejecución, pero el duplicado engaña a quien relea el código (dos "fuentes de verdad" para el ciclo de vida de un servicio). Con el cliente nombrado "Ollama" es peor: `AddHttpClient(name, configure)` **acumula** los `configure` en vez de reemplazarlos, y como `Program.cs:35` (`AddInfrastructure`) corre **antes** de `Program.cs:97`, el `BaseAddress`/`Timeout` del segundo registro pisan (y en futuras ediciones pueden pisarse entre sí de forma no obvia) a los del primero. Hoy la configuración vive duplicada en dos archivos con valores distintos (en `Program.cs:97-116` se configura `MaxConnectionsPerServer = 8`; en `DependencyInjection.cs:70` no).

**Corrección propuesta:**
- Elegir **un** lugar: borrar `Program.cs:82` (dejarlo en `Infrastructure\DependencyInjection.cs`, que es donde viven los demás hosted services) o viceversa.
- Para "Ollama": dejar el `AddHttpClient("Ollama")` completo en `Infrastructure\DependencyInjection.cs:70` (con `PooledConnectionLifetime` y `MaxConnectionsPerServer`) y eliminar el bloque `Program.cs:97-116`, o al revés — nunca ambos.

---

### 9. [MEDIA] `IpAddress` de auditoría nunca se escribe: la columna existe, la UI la muestra, siempre está vacía

**Archivos:**
- `GuarinoApp.Infrastructure\Services\AuditService.cs:26-49` — `LogAsync` construye el `LogEntry` sin asignar `IpAddress` (propiedad existe: `Domain\Entities\LogEntry.cs:43`).
- `GuarinoApp.Infrastructure\Data\DbInitializer.cs:1256` — el seed sí rellena `IpAddress = $"10.0.0.{random.Next(2, 254)}"`, es decir **IPs inventadas**.
- Consumidores: `Application\Modules\Auditoria\Dtos\LogDto.cs:12` y `GetLogsQuery.cs:72` la exponen; la migración `20260930151058_AddFinancialDataAndLogEntries.cs:50` la creó.

**Por qué es un problema:**
El log de auditoría es la pista forense ante incidentes: "desde qué IP se modificó X" es lo primero que se consulta. Hoy toda entrada real tiene la columna en `NULL` (el `LogAsync` de `AuditService` es el único escritor en runtime), mientras que el seed rellena IPs ficticias que además **ensucian el dato**: al mirar la tabla parece haber registros con IP y sin IP, sin distinción posible entre "IP real ausente" e "IP de demostración". `LogActionCommand` tampoco la setea (ver hallazgo 10).

**Corrección propuesta:**

```csharp
// AuditService: inyectar IHttpContextAccessor (ya registrado por AddServerSideBlazor)
private readonly IHttpContextAccessor _httpContext;

var logEntry = new LogEntry
{
    // ...
    IpAddress = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
    // ...
};

// DbInitializer.cs:1256 — dejar de inventar IPs en el seed:
IpAddress = null,   // dato desconocido, no dato falso
```

---

### 10. [MEDIA] `LogActionCommand` es código muerto y su mapeo de rol omite "Admin"

**Archivo:** `GuarinoApp.Application\Modules\Auditoria\Commands\LogAction\LogActionCommand.cs:13-45`

**Por qué es un problema:**
Un grep de `LogActionCommand` en toda la solución solo lo encuentra a él mismo: ningún caller lo envía. Es un command entero (registro + handler) que nadie usa — y que además es un segundo camino de escritura de auditoría paralelo a `IAuditService`, con lógica duplicada y **más pobre**: su ternario de rol (`:31-33`) evalúa `Gerencia`, `Contador`, `Operador`… pero **no `Admin`**, así que un administrador quedaría registrado como `"Usuario"` en `LogEntries.Role` si algún día se usara. Doble mantenimiento y bug latente.

**Corrección propuesta:**

```csharp
// Opción A (recomendada): borrar LogActionCommand.cs completo.
//   IAuditService.LogAsync ya es el camino único (y sí contempla Admin: AuditService.cs:33-36).

// Opción B: si se quiere conservar, delegar en el servicio único:
public async Task Handle(LogActionCommand request, CancellationToken ct)
    => await _audit.LogAsync(request.Action, request.Module, request.Description, request.AdditionalData);
```

---

### 11. [MEDIA] Sin `HasQueryFilter` global de soft-delete: cada handler recuerda filtrar `IsDeleted` a mano

**Archivo:** `GuarinoApp.Infrastructure\Data\ApplicationDbContext.cs` (verificado: cero `HasQueryFilter` en toda la solución)

**Por qué es un problema:**
El borrado lógico está en la columna (`IsDeleted` de `BaseEntity`), pero el filtro **no está en el modelo**: cada consulta debe recordar `&& !x.IsDeleted`. Se hace en algunos lados (ej. `LoginCommandHandler.cs:37`, `RegisterCommandHandler.cs:55,60,65`, `CustomAuthenticationStateProvider.cs:200`) pero basta un `FirstOrDefault` nuevo sin el predicado para que un usuario borrado siga apareciendo — o peor, siga siendo válido para login. Es un contrato que depende de la disciplina de cada desarrollador, y ya hay ~40 handlers.

**Corrección propuesta:**

```csharp
// ApplicationDbContext.OnModelCreating, al final:
foreach (var entityType in modelBuilder.Model.GetEntityTypes()
    .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
{
    var param = Expression.Parameter(entityType.ClrType, "e");
    var body = Expression.Equal(
        Expression.Property(param, nameof(BaseEntity.IsDeleted)),
        Expression.Constant(false));
    modelBuilder.Entity(entityType.ClrType)
        .HasQueryFilter(Expression.Lambda(body, param));
}
// Nota: luego revisar los "&& !IsDeleted" explícitos (son redundantes, se pueden quitar)
// y las consultas que necesiten ver borrados (rehabilitación) usando IgnoreQueryFilters().
```

---

### 12. [MEDIA] `GetFrutaVolcarQueryHandler` carga la tabla completa siempre y la rama filtrada es inalcanzable (con `InvalidCastException` latente)

**Archivos:** `GuarinoApp.Application\Modules\FrutaVolcar\Queries\GetFrutaVolcar\GetFrutaVolcarQueryHandler.cs:22-47` · `GuarinoApp.Web\Pages\FrutaVolcar.razor:118`

**Por qué es un problema:**
1. Las líneas 22-32 ejecutan **siempre** la consulta completa con 9 `Include` sobre `Lotes`, aunque después vaya a usarse la rama filtrada: el filtrado ocurre sobre una segunda consulta (líneas 37-50), así que con filtros se carga la tabla dos veces. Con el volumen de una zafra completa esto es memoria y tiempo de consulta desperdiciados en cada render.
2. La rama filtrada está **inalcanzable hoy**: `FrutaVolcar.razor:118` envía `new GetFrutaVolcarQuery(new List<Filter>())`, una lista vacía, por lo que `request.Filters.Any()` (línea 35) siempre es false — el `ApplyFilters` nunca corre, y nadie lo prueba.
3. Cuando alguien finalmente la use, la línea 47 hace un cast duro:
   `(IIncludableQueryable<Lote, Empaque>)LinqDynamicQueryBuilder.ApplyFilters(query, request.Filters)` — `ApplyFilters` devuelve un `IQueryable`/`IIncludableQueryable` de otra jerarquía y ese cast revienta con `InvalidCastException` en runtime (no compila el problema: el cast es explícito).

**Corrección propuesta:**

```csharp
public async Task<List<FrutaVolcarDto>> Handle(GetFrutaVolcarQuery request, CancellationToken ct)
{
    IQueryable<Lote> query = _context.Lotes
        .AsNoTracking()
        .Include(l => l.Variedad).ThenInclude(v => v.Especie)
        // ... demases Include ...
        ;

    if (request.Filters is { Count: > 0 })
        query = LinqDynamicQueryBuilder.ApplyFilters(query, request.Filters).Cast<Lote>();
        // o mejor: cambiar ApplyFilters a genérico ApplyFilters<T>(IQueryable<T>, ...) -> IQueryable<T>
        // y eliminar el cast por completo

    var lotes = await query.ToListAsync(ct);   // UNA sola consulta
    return lotes.Select(l => new FrutaVolcarDto { ... }).ToList();
}
```

Y corregir el caller para que realmente filtre (`FrutaVolcar.razor:118`) o eliminar la rama muerta.

---

### 13. [MEDIA] `InputSanitizer` global corrompe textos legítimos en todos los requests MediatR

**Archivos:** `GuarinoApp.Application\Common\Security\InputSanitizer.cs:37-38` (regex), `GuarinoApp.Application\Common\Behaviors\InputSanitizationBehavior.cs:74` (aplicación global), alta en `Application\DependencyInjection.cs:20-21`.

**Por qué es un problema:**
El behavior recorre **todas** las propiedades `string` de **todas** las queries y commands y aplica `InputSanitizer.Sanitize`, que con la regex `(--|/\*|\*/|;|`)` **borra** esos caracteres de cualquier texto. No es teoría: cualquier comentario, dirección, descripción o mensaje de email que contenga `--` (una anotación tipo "nota -- pendiente"), `;` (listas "A;B;C") o `` ` `` se silencia y se reemplaza por un espacio **sin avisar**. El propio comentario del archivo (`InputSanitizer.cs:9-12`) reconoce que la defensa real contra SQL injection es el SQL parametrizado (que es como está escrita toda la app); esta capa agrega corrupción de datos de negocio sin agregar seguridad medible. Además, truncar a 2000 caracteres (`MaxLength`) de forma global afecta a campos que podrían ser más largos.

**Corrección propuesta:**

```csharp
// 1) Quitar el stripping de secuencias SQL (el SQL parametrizado ya protege):
private static readonly Regex SqlInjectionSequences =>
    new(@"(;|--|/\*|\*/|`)", RegexOptions.Compiled);   // <-- ELIMINAR este regex y su uso en Sanitize (línea 62)

// 2) O, como mínimo, dejar de aplicarlo globalmente: en InputSanitizationBehavior,
//    procesar solo propiedades marcadas explícitamente:
[Sanitize] public string Descripcion { get; set; }   // opt-in por atributo en vez de opt-out
```

Mantener el saneo de caracteres de control (`\u0000-\u001F`) y de saltos repetidos, que no dañan datos legítimos.

---

### 14. [MEDIA] Tres estilos de acceso a datos coexistiendo

**Archivos:**
1. EF Core LINQ parametrizado vía `IApplicationDbContext` — todos los handlers (ej. `LoginCommandHandler.cs:35-37`).
2. SQL crudo con `ExecuteSqlRaw` — `GuarinoApp.Infrastructure\Data\DbInitializer.cs:380` (`DBCC CHECKIDENT`), `DbInitializer.cs:964` (creación de `vRitmoCalibradora`).
3. Conexión `SqlConnection` cruda — `GuarinoApp.Infrastructure\Services\BaseDatosRecuperacionService.cs:237` (y consultas de diagnóstico en `VerificacionBaseDatos.cs`/`DiagnosticoBaseDatos.cs`).

**Por qué es un problema:**
Tres maneras de hablar con la misma base de datos conviven sin una frontera clara: si mañana cambia el nombre de una columna o se cifran datos, hay que rastrear LINQ + SQL embebido + ADO.NET manual. Los casos 2 y 3 son "operaciones de administración" legítimas, pero están dispersos y sin abstracción, y el SQL embebido es el más propenso a romperse en silencio ante cambios de esquema (no hay compilador que lo verifique).

**Corrección propuesta:**

```csharp
// Encapsular los caminos crudos detrás de interfaces concretas en Infrastructure:
public interface IAdministraEsquema {
    Task CrearTablaRitmoCalibradoraAsync(CancellationToken ct);
    Task ReseedAsync(string tabla, CancellationToken ct);
}
// DbInitializer y BaseDatosRecuperacionService pasan a ser sus implementaciones;
// los handlers NUNCA ven SqlConnection/ExecuteSqlRaw, solo IApplicationDbContext.
```

---

### 15. [MEDIA] 42 `Console.WriteLine` de diagnóstico repartidos en páginas `.razor`

**Archivos (muestra):** `Pages\PalletsDefinitivos.razor:215,270,305,350,398` · `Pages\LiquidacionesQuinta.razor:114,134,149,165,196` · `Pages\ResumenLiquidaciones.razor:113,133,148,164,195` · `Pages\FrutaVolcar.razor:123,235,257` · `Pages\RegistroPesadas.razor:97,158,180` · y otros 15 archivos (verificados por grep: 42 coincidencias).

**Por qué es un problema:**
`Console.WriteLine` en Blazor Server escribe en la **consola del servidor**, no en el navegador, y sin nivel, categoría ni estructura. No se filtra por `LogLevel` (`appsettings.json` no puede silenciarlo), no aparece correlacionado con el circuito, no va al buffer de errores que consume `NotificadorAdminService` (`Program.cs:43-45`), y en la mayoría de los casos captura `ex.Message` perdiendo el stack trace. Son 42 puntos donde un error real queda reducido a una línea de consola que nadie ve en producción.

**Corrección propuesta:**

```csharp
// @inject ILogger<PalletsDefinitivos> Logger   // en cada página con try/catch
catch (Exception ex)
{
    Logger.LogError(ex, "Error exportando pallets definitivos");
    // + mantener el Snackbar/MudAlert que ya muestra el usuario
}
// Regla: cero Console.WriteLine en .razor (agregar guard en CI: grep -r "Console.WriteLine" --include=*.razor)
```

---

### 16. [MEDIA] Exportación y envío de emails por copiar-y-pegar en 7 páginas, ignorando los servicios ya registrados

**Archivos:**
- Helper privado idéntico `ExportAsync(Func<Task<byte[]>>, ...)` duplicado en: `FrutaVolcar.razor:249`, `GraficaProcesos.razor:203`, `LotesVolcados.razor:177`, `RegistroPesadas.razor:172`.
- `ExportToCsv` reimplementado por página en 8 páginas (`RegistroPesadas:126`, `Dashboard`, `ResumenLiquidaciones`, `LotesVolcados`, `FrutaVolcar`, `LiquidacionesQuinta`, `PalletsDefinitivos` (`ExportarCsv`/`ExportarExcel`), `GraficaProcesos`).
- Flujo de email duplicado en 7 páginas: `Dashboard.razor:544`, `RegistroPesadas.razor:144`, `LotesVolcados.razor:149`, `FrutaVolcar.razor:221`, `ResumenLiquidaciones.razor:180`, `PalletsDefinitivos.razor:382,444`, `LiquidacionesQuinta.razor:181`.
- Ya existen `IExportService` (`Infrastructure\DependencyInjection.cs:48`), `IEmailService` (`:49`), `IReportMailRouter` (`:53`), y helpers `Web\Helpers\DescargaHelper.cs` y `Web\Helpers\FileDownloadExtensions.cs`.

**Por qué es un problema:**
Un fix en el formato CSV, en el nombre del archivo o en el manejo de errores de descarga hay que aplicarlo en 7-8 archivos con alta probabilidad de que se escape en alguno (de hecho hoy `PalletsDefinitivos` usa `ExportarCsv/ExportarExcel` y las demás `ExportToCsv`: dos convenciones conviviendo). El comportamiento de email ya divergió: `PalletsDefinitivos.razor:444` usa `SendEmailWithTemplateAndAttachmentAsync` y el resto `SendEmailAsync`.

**Corrección propuesta:**

```csharp
// 1) Mover ExportAsync a un servicio único:
public interface IExportadorWeb {
    Task DescargarAsync(IBrowserFile/byte[] contenido, string fileName, string mimeType);
}
// (o usar el ya existente DescargaHelper/FileDownloadExtensions y borrar los 4 copies)

// 2) Un solo componente <ExportButtons OnCsv= OnExcel= OnEmail= /> que agrupe
//    los botones que hoy se repiten en la cabecera de cada página (ver Gazetteer de
//    OnExportCsv/OnExportEmail en 8 páginas) y delegue en IExportService/IEmailService.
```

---

### 17. [MEDIA] 4 DTOs duplicados/muertos en las carpetas Dtos

**Archivos:**
- `GuarinoApp.Application\Modules\Transportistas\Dtos\TransportistasDto.cs` (duplica a `TransportistaDto.cs`)
- `GuarinoApp.Application\Modules\ResumenLiquidaciones\Dtos\ResumenLiquidacionesDto.cs` (duplica a `ResumenLiquidacionDto.cs`)
- `GuarinoApp.Application\Modules\RegistroPesadas\Dtos\RegistroPesadasDto.cs` (duplica a `RegistroPesadaDto.cs`)
- `GuarinoApp.Application\Modules\StockPalletsSinCliente\Dtos\StockPalletsSinClienteDto.cs` (duplica a `StockPalletSinClienteDto.cs`)

**Por qué es un problema:**
Verificado por grep: cada uno de los 4 solo se referencia a sí mismo (su declaración). Son clases huérfanas con casi el mismo nombre que el DTO real (plural vs singular), lo que hace que una búsqueda rápida por tipo (o un "go to definition") pueda llevarte al equivocado, y que cualquiera asuma que es el tipo que se usa. Además todos en `Dtos/` están en el namespace de la carpeta, no hay contrato compartido con la capa Web.

**Corrección propuesta:** borrar los 4 archivos (`TransportistasDto.cs`, `ResumenLiquidacionesDto.cs`, `RegistroPesadasDto.cs`, `StockPalletsSinClienteDto.cs`). Si el compilador no queja, no se usan.

---

### 18. [MEDIA] Referencias de paquetes sin uso, archivo con doble extensión e interfaz mal ubicada

**Archivos:**
- `GuarinoApp.Web\GuarinoApp.Web.csproj:17` → `DocumentFormat.OpenXml` (grep en `.cs`: cero usos).
- `GuarinoApp.Web\GuarinoApp.Web.csproj:18` → `MailKit` (los únicos "usos" son comentarios: `EmailService.cs:214-216`; el código usa `System.Net.Mail.SmtpClient`).
- `GuarinoApp.Web\GuarinoApp.Web.csproj:26` → `Microsoft.Extensions.DependencyInjection.Abstractions` **10.0.10** en una app .NET 8: paquete de otra línea mayor de runtime.
- `GuarinoApp.Application\Common\Interfaces\IClimateService.cs.cs` — doble extensión.
- `GuarinoApp.Infrastructure\Services\IPdfGenerator.cs:3` — la interfaz vive junto a su implementación en Infrastructure, a diferencia del resto de las abstracciones (`IEmailService`, `IExportService`, etc. están en `Application\Common\Interfaces`), lo que obligaría a Application a referenciar Infrastructure si algún handler la necesitara (hoy no lo hace, pero rompe la convención de la solución).

**Por qué es un problema:**
Paquetes sin uso = superficie de supply-chain y tiempo de restore sin beneficio; la versión 10.0.10 de DI.Abstractions en .NET 8 puede introducir ensamblados que no corresponden al TFM; el doble `.cs` confunde búsqueda y tooling; la interfaz mal ubicada es una bomba de referencia invertida a futuro.

**Corrección propuesta:**

```bash
# 1) Quitar de GuarinoApp.Web.csproj:
#    <PackageReference Include="DocumentFormat.OpenXml" ... />
#    <PackageReference Include="MailKit" ... />
#    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.10" />
# 2) git mv GuarinoApp.Application/Common/Interfaces/IClimateService.cs.cs .../IClimateService.cs
# 3) git mv GuarinoApp.Infrastructure/Services/IPdfGenerator.cs GuarinoApp.Application/Common/Interfaces/IPdfGenerator.cs
#    (y actualizar namespaces)
```

---

### 19. [MEDIA] Migraciones huérfanas y hueco de esquema en `vRitmoCalibradora`

**Archivos:**
- `GuarinoApp.Infrastructure\Data\Migrations\AddLatitudeLongitudeToLotes.cs:6` y `...\AddClimateDataSeeding.cs:5` — clases `: Migration` **sin atributo `[Migration(...)]`, sin `[DbContext(...)]` y sin archivo `.Designer`**, y fuera de la carpeta real de migraciones (`GuarinoApp.Infrastructure\Migrations\`, donde sí está el snapshot).
- `GuarinoApp.Infrastructure\Data\ApplicationDbContext.cs:255` → `eb.ToView("vRitmoCalibradora")`.
- `GuarinoApp.Infrastructure\Data\DbInitializer.cs:951-966` → `CREATE TABLE [dbo].[vRitmoCalibradora]` en SQL crudo (invocado desde `DbInitializer.cs:324`).

**Por qué es un problema:**
Las dos clases de `Data\Migrations` nunca serán descubiertas por EF (sin el atributo `[Migration]` ni el Designer, no entran en el conjunto de migraciones): son código muerto que parece migraciones pero no aplican nada — quien las lea puede creer que "agregar lat/long" está manejado por migración cuando no lo está. Peor es `vRitmoCalibradora`: EF la mapea como vista (`ToView`), por lo que **nunca la crea**, y la creación vive en `DbInitializer` con SQL crudo; el seed de `DbInitializer` solo corre en Development (`Program.cs:289`), así que un entorno obtenido solo con `dotnet ef database update` queda **sin esa tabla** y la página `GraficaProcesos` falla. Es un hueco de esquema entre entornos.

**Corrección propuesta:**

```bash
# 1) Borrar Data\Migrations\AddLatitudeLongitudeToLotes.cs y AddClimateDataSeeding.cs
#    (si el cambio ya se aplicó a mano en BD, no reproducirlo; si no, recrearlos con:
#     dotnet ef migrations add AddLatitudeLongitudeToLotes)
# 2) Convertir la creación de vRitmoCalibradora en migración real:
#    dotnet ef migrations add AddTablaRitmoCalibradora
#    (Up(): migrationBuilder.Sql("CREATE TABLE [dbo].[vRitmoCalibradora](...)"))
#    y eliminar el bloque DbInitializer.cs:951-1064, o al menos dejar de depender de él
#    para que Production tenga el esquema completo solo con migraciones.
```

---

### 20. [MEDIA] Archivos huérfanos en la raíz del repositorio

**Archivos:** `C:\Claude-Proyectos\Guarino\IApplicationDbContext.cs` (línea 1: artefacto de edición `csharp C:\Guarino\...`), `...\Pages\EmailTest.razor`, `...\Shared\AIParametersPanel.razor`, más scripts de parches puntuales (`apply-fixes.ps1`, `fix-remove-duplicates.ps1`, `fix-ia-duplicates-and-fallback.ps1`, `fix-csproj-remove-xmlns.ps1`, `GenerarResumenClases.ps1`, etc.) y `GuarinoAppDb.sql`/`NORIDEL_catalogo_metadata.sql` en la raíz.

**Por qué es un problema:**
La raíz tiene carpetas `Pages/` y `Shared/` **fuera** de `GuarinoApp.Web` con un `.razor` cada una: parecen parte del proyecto y no lo son (`EmailTest.razor` y `AIParametersPanel.razor` no compilan ni se sirven). `IApplicationDbContext.cs` en la raíz es una copia vieja de la interfaz real (`GuarinoApp.Application\Common\Interfaces\`) con una primera línea corrupta: cualquiera que busque `IApplicationDbContext` puede abrir la copia y "corregir" la versión equivocada. Los `.ps1` son parches ya aplicados que quedaron como reliquias.

**Corrección propuesta:**

```bash
# Verificar que nada los referencia y eliminar:
#   IApplicationDbContext.cs, Pages\, Shared\ (raíz), los apply-*/fix-*.ps1 ya ejecutados
# Mover los .sql a docs/ o scripts/ (ya existe la carpeta scripts/)
# grep -r "EmailTest\|AIParametersPanel" --include=*.{cs,razor,csproj}  -> sin referencias = borrar
```

---

### 21. [MEDIA] Sin proyecto de tests ni integración CI

**Archivos:** `GuarinoApp.sln:6-14` — solo `GuarinoApp.Domain`, `GuarinoApp.Application`, `GuarinoApp.Infrastructure`, `GuarinoApp.Web` y `BenchmarkSuite1`; `.github\` contiene únicamente `copilot-instructions.md` (sin workflows).

**Por qué es un problema:**
Una solución con ~40 handlers de MediatR, pipeline de behaviors (sanitización, serialización), lógica de negocio financiera (proyección de zafra, liquidaciones, contaduría) y seguridad (auth/roles) no tiene **una sola prueba automatizada**, y ningún workflow que compile o valide en PR. Los cambios en behaviors globales como `InputSanitizationBehavior` (hallazgo 13) o `SerializacionDbContextBehavior` pueden romper todos los módulos sin que nada lo advierto hasta probar a mano en el navegador. `BenchmarkSuite1` está en la solución pero no es un test: es un proyecto de benchmarks.

**Corrección propuesta:**

```bash
# 1) Crear GuarinoApp.Tests (xUnit) e integrarlo:
#    dotnet new xunit -n GuarinoApp.Tests
#    dotnet add GuarinoApp.Tests reference GuarinoApp.Application GuarinoApp.Infrastructure
#    dotnet sln GuarinoApp.sln add GuarinoApp.Tests
#    Pruebas mínimas de arranque: RegisterCommandHandler (AllowRegistration on/off),
#    LoginCommandHandler (credencial mala/bloqueo), InputSanitizer.Sanitize, GetProyeccionZafra con BD vacía.
# 2) .github/workflows/ci.yml: dotnet build + dotnet test en cada push/PR.
```

---

## Hallazgos de severidad BAJA

### 22. [BAJA] `appsettings.Production.json` versionado con placeholders de conexión y SMTP

**Archivo:** `GuarinoApp.Web\appsettings.Production.json:3` (`Server=PROD_SERVER;...Password=PROD_PASSWORD`), `:83` (`"Password": ""` de SMTP), `:38` (`IaWeb:ApiKey: ""`).

**Por qué es un problema:** aunque los valores actuales son placeholders (no credenciales reales), el patrón invita a pegar credenciales en el archivo y commitearlas — la historia de esta clase de archivos es esa. La config de secrets no debería viajar en el repo.

**Corrección:** usar variables de entorno (`ConnectionStrings__DefaultConnection`, `Email__Password`) o `dotnet user-secrets` en desarrollo; mantener en el JSON solo estructura y valores no sensibles; agregar el archivo a `.gitignore` si en algún momento tuvo credenciales reales (y rotarlas).

### 23. [BAJA] `docs/ARQUITECTURA.md` describe protecciones que el código no aplica

**Archivo:** `docs\ARQUITECTURA.md:299-311` — documenta "Rate Limiting" (con el código de la política `"fixed"`) como protección de seguridad activa de la app.

**Por qué es un problema:** documenta como existente el rate limiting que nunca se aplica (hallazgo 4). La arquitectura documentada y la real divergieron: un lector nuevo da por cubierto un riesgo que sigue abierto.

**Corrección:** al resolver el hallazgo 4, actualizar la sección; si no se resuelve, marcar la sección como "configurada, pendiente de aplicar a endpoints".

### 24. [BAJA] `NavMenu.razor` duplica la matriz de roles con los atributos de las páginas

**Archivo:** `GuarinoApp.Web\Shared\NavMenu.razor:11-109` (AutorizeViews anidados: `11`, `34`, `38`, `51`, `54`, `74`, `88`, `99`, `103`).

**Por qué es un problema:** la matriz de acceso existe en dos lugares (menú y `@attribute [Authorize]` de 24 páginas). Cada cambio de rol exige tocar ambos; si divergen, el usuario ve un link que le da "No tenés permiso" (`App.razor:14`) o no ve un link a una página que sí podría usar. Ya hay señales de esa duplicación (hallazgo 6).

**Corrección:** definir la matriz en un único origen —por ejemplo una clase `Roles` con listados constantes (`Roles.AdminGerencia = "Admin, Gerencia"`)— y usarla tanto en los `AuthorizeView Roles=` del menú como en los `@attribute [Authorize(Roles = Roles...)]` de cada página.

### 25. [BAJA] `BenchmarkSuite1` incluido en la solución principal

**Archivo:** `GuarinoApp.sln:14`.

**Por qué es un problema:** un proyecto de benchmarks (sin tests) en la solución de producción infla la compilación y confunde el alcance de la app; además, al no haber proyecto de tests (hallazgo 21), da la impresión de que "la solución ya tiene algo de validación".

**Corrección:** moverlo a una solución aparte (`Benchmarks.sln`) o, si se conserva, marcarlo con `<IsPackable>false</IsPackable>` y documentarlo en README.

### 26. [BAJA] `EmailService` usa `System.Net.Mail.SmtpClient` (obsoleto) mientras MailKit queda instalado sin usarse

**Archivo:** `GuarinoApp.Infrastructure\Services\EmailService.cs:194-216` (el propio código lo dice en `:212-216`: no soporta XOAUTH2 y sugiere "migrar a MailKit").

**Por qué es un problema:** `SmtpClient` está marcado obsoleto (SYSLIB0014) y es sincrónico/bloqueante; el paquete MailKit ya está en el csproj (hallazgo 18) pero nadie lo usa: ni la migración se hizo ni el paquete se quitó.

**Corrección:** elegir uno: migrar a `MailKit` (`MailKit.Net.Smtp.SmtpClient`, soporta XOAUTH2) **o** quitar `MailKit` del csproj y dejar documentada la deuda con la obsolescencia.

### 27. [BAJA] Comentario incorrecto en `AuthSettings.DefaultRole`

**Archivo:** `GuarinoApp.Application\Settings\AuthSettings.cs:22-23` — dice *"Rol que se asigna a los usuarios nuevos (el primero siempre es Gerencia)"*.

**Por qué es un problema:** el código real hace que el **primer** usuario sea **Admin** (`RegisterCommandHandler.cs:14` `AdminRole = "Admin"`; `:59-61` `hasUsers ? DefaultRole : AdminRole`). El comentario (y el de la clase, que sigue a la config vieja) desorienta: alguien puede "arreglar" el código creyendo que respeta la documentación.

**Corrección:** actualizar el XML doc: *"Rol por defecto para registros nuevos; el primer usuario de la organización se crea como Admin"*.

### 28. [BAJA] Auditoría con `catch` vacío: fallas de escritura invisibles

**Archivo:** `GuarinoApp.Infrastructure\Services\AuditService.cs:51-54` — `catch { /* No lanzar excepciones... */ }`.

**Por qué es un problema:** el comentario explica bien la intención (no interrumpir el flujo), pero al ser `catch` **sin siquiera loguear**, si `SaveChangesAsync` falla (BD en readonly, constraint, timeout) la entrada de auditoría desaparece para siempre y nadie se entera — incluido el caso de un fallo sistemático que dejaría el log completo sin registrar nada.

**Corrección:**

```csharp
catch (Exception ex)
{
    // No interrumpir el flujo principal, pero la falla NO puede ser muda:
    _logger.LogError(ex, "No se pudo escribir la auditoría {Action}/{Module}", action, module);
}
// (inyectar ILogger<AuditService> en el constructor)
```

---

## Lo que está bien

1. **Arquitectura en capas real y respetada.** `Domain` no depende de nadie, `Application` solo de `Domain`, `Infrastructure` implementa `Application` y `Web` compone todo. No hay referencias invertidas (Application no referencia Infrastructure, salvo la deuda cosmética del hallazgo 18).
2. **CQRS con MediatR y behaviors globales** (`Application\DependencyInjection.cs:12-31`): `InputSanitizationBehavior` y `SerializacionDbContextBehavior` están bien pensados como puntos únicos de cross-cutting, aunque el primero tenga el problema del hallazgo 13.
3. **SQL parametrizado por defecto.** Todo el acceso de handlers es LINQ de EF Core; el único SQL crudo es administrativo y está confinado (hallazgo 14). El comentario de `InputSanitizer.cs:9-12` documenta correctamente que la defensa primaria es el parametrizado.
4. **Revalidación de sesión contra BD al iniciar el circuito** (`CustomAuthenticationStateProvider.cs:189-203`): si el usuario fue desactivado o borrado, la sesión local se corta aunque el token siga vigente, y el rol se toma de la BD, no del token.
5. **Auditoría centralizada y con scope propio** (`AuditService.cs:46-49`): usa `IServiceScopeFactory` para no compartir el `DbContext` del circuito, con comentario que explica el motivo (solapamiento y pérdida de entradas). Buena defensa contra bugs de concurrencia de Blazor Server.
6. **Headers de seguridad y HSTS en el pipeline** (`Program.cs:188-221`): `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, con comentario explicando por qué `unload` queda habilitado para SignalR.
7. **Health checks de las dependencias críticas** (`Program.cs:122-124, 235-239`): existen checks de SQL Server y Ollama; el problema es solo el tag del hallazgo 5.
8. **Opciones validadas con `ValidateOnStart`** (`Program.cs:60-68`): configuración inválida impide arrancar en lugar de fallar en caliente, y el log de arranque (`Program.cs:257`) resume la configuración aplicada.
9. **Buen punto de partida para eliminar los datos dummy:** varios DTOs ya exponen `EsDummy` de forma honesta (`PalletsResultDto.cs:9`, `LotesVolcadosResultDto.cs:7`, `GetProyeccionQueryHandler.cs:68-77` ya reemplazó su fallback por "lista vacía + aviso"): hay patrón probado en la codebase para estandarizar el hallazgo 3.

---

## Top 5 de acciones prioritarias

1. **Cerrar la puerta de autenticación (hallazgo 1):** `AllowRegistration: false` en `appsettings.json:41` + bloqueo por intentos fallidos en `LoginCommandHandler` (5 intentos → 15 min). Es un cambio de horas que elimina el riesgo más explotable.
2. **Desactivar el fallback privilegiado de Windows (hallazgo 2):** `EnableFallbackIdentity: false` y no asignar `DefaultRole` cuando no hay roles resueltos (`CustomAuthenticationStateProvider.cs:143-144`). Nadie debería entrar como "Gerencia" por accidente.
3. **Eliminar los datos dummy silenciosos (hallazgo 3):** en los ~15 handlers, el `catch` debe loguear y propagar (o devolver vacío + `EsDummy`), nunca filas inventadas; acotar `GetDummyData()` a Development con flag explícito. La integridad de los números de gerencia/contaduría depende de esto.
4. **Completar la autorización en la capa de presentación y aplicación (hallazgos 6 y 7):** `@attribute [Authorize(Roles=...)]` en las 9 páginas faltantes + `AuthorizationBehavior` con `[RequiresRole]` sobre los commands de Contactos. La matriz de roles debe aplicarse en un solo lugar por request, no solo en el menú.
5. **Hacer reales las protecciones ya declaradas (hallazgos 4 y 5) y empezar a testear (hallazgo 21):** aplicar la política de rate limiting (global o a login), taggear los health checks con `"ready"`, y crear `GuarinoApp.Tests` + workflow de CI para que los cambios futuros en behaviors y handlers tengan red de seguridad.

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