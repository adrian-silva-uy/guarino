# Revisión de superficie de endpoints — GuarinoApp

> Fecha de la revisión: 2026-10-20 · Alcance: solo lectura de código (sin builds, sin git, sin BD).
> Rama: `master` · Solución: `GuarinoApp.sln`
> IGNORADO: `Propuesta/`, `csv Propuesta/`, `bin/`, `obj/`.

## Conclusión principal: NO existe ninguna API REST

Barrí todo el repositorio buscando las marcas habituales de una API:

- `[Route(`, `[HttpGet/HttpPost/Put/Delete]`, `ApiController`, `ControllerBase`, `IActionResult` → **0 coincidencias**.
- `MapGet/MapPost/MapGroup` (Minimal APIs) → **0 coincidencias**.
- `UseEndpoints` → **0 coincidencias**.

La app es **Blazor Server + MediatR**. La única superficie HTTP real es esta:

| Superficie | Ubicación | Cantidad |
|---|---|---|
| Health endpoints (`/health`, `/health/ready`) | `GuarinoApp.Web\Program.cs:235-239` | 2 |
| Razor Pages (`/` → `_Host`, `/Error`) | `GuarinoApp.Web\Pages\_Host.cshtml`, `Pages\Error.cshtml` | 3 |
| Páginas Blazor Server (`@page`) | `GuarinoApp.Web\Pages\*.razor` | 35 rutas |
| JS invocable (`[JSInvokable]`) | — | 0 (solo `IJSRuntime` de bajada hacia el navegador) |

Por lo tanto, el análisis se hace sobre **esa superficie equivalente**: rutas Blazor + health checks + el flujo MediatR/DTOs que las alimenta.

---

## Hallazgos

### Severidad ALTA

**1. `/health/ready` responde siempre 200 "Healthy": ningún health check tiene el tag `ready`**
- `GuarinoApp.Web\Program.cs:236-239` — `Predicate = check => check.Tags.Contains("ready")`.
- `GuarinoApp.Infrastructure\Services\SqlServerHealthCheck.cs:11` y `OllamaHealthCheck.cs:10` — se registran sin tags (`Program.cs:122-124`).
- **Desviación:** el filtro no matchea a ningún check, así que el endpoint de "readiness" ejecuta **cero comprobaciones** y siempre reporta sano. Es un endpoint mentiroso para cualquier orquestador/monitoreo.
- **Corrección:** registrarlos con tags, p. ej. `.AddCheck<SqlServerHealthCheck>("sql-server", tags: new[] { "ready", "db" })`, o cambiar el predicado a `Predicate = _ => true` si se quiere que `/health/ready` sea un liveness total.

**2. Rate limiting configurado pero NO aplicado a ningún endpoint**
- `GuarinoApp.Web\Program.cs:130-139` define la política `"fixed"` (100 req/min, cola 10); `Program.cs:228` llama `app.UseRateLimiter()`.
- `RequireRateLimiting` / `DisableRateLimiting` / `GlobalLimiter` → **0 coincidencias** en todo el repo.
- **Desviación:** el middleware corre pero nunca limita nada. El comentario de `Program.cs:128` ("Protección contra abuso de la API, especialmente endpoints de IA") no se cumple. Los equivalentes a `POST /login` y `POST /register` (`Pages\Login.razor`, `Pages\Register.razor`) quedan sin protección contra fuerza bruta, e `IAChat.razor` sin freno a abuso.
- **Corrección:** `options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(...)` o al menos `.RequireRateLimiting("fixed")` sobre `/login`, `/register` e IA; política más estricta (p. ej. 5/min) para autenticación.

**3. Identidad de respaldo que otorga rol "Gerencia" a cualquier visitante sin AD**
- `GuarinoApp.Web\appsettings.json:32-38` — `"EnableFallbackIdentity": true`, `"DefaultRole": "Gerencia"`.
- `GuarinoApp.Infrastructure\Services\CustomAuthenticationStateProvider.cs:86-91` y `:143-144` — si Windows no autentica, se construye una identidad con `SecuritySettings.DefaultRole`.
- **Desviación:** en modo Windows, cualquier usuario de red que llegue sin credenciales válidas queda identificado como "Gerencia", y "Gerencia" da acceso a `Auditoria`, `Contactos` y `Usuarios` (`Pages\Auditoria.razor:2`, `Pages\Contactos.razor:2`, `Pages\Usuarios.razor:2`). La autorización efectiva queda delegada a una pieza de UI.
- **Corrección:** `EnableFallbackIdentity: false` en producción (solo Development), o bajar `DefaultRole` a un rol sin acceso a datos sensibles y dejarlo explícito por entorno.

**4. 9 páginas de negocio sin `[Authorize]` pese a exponer datos**
- Sin atributo: `Pages\Counter.razor:1`, `Dashboard.razor:1`, `FrutaVolcar.razor:1`, `Index.razor:1`, `LotesVolcados.razor:1`, `PalletsSinCliente.razor:1`, `RegistroPesadas.razor:1`, `StockGeneral.razor:1`, `StockPlaya.razor:1`.
- El único control es el gate del layout: `Shared\MainLayout.razor:49-63` (redirige a `/login` si no hay sesión).
- **Desviación:** inconsistencia de política — 24 páginas sí declaran `[Authorize(Roles=...)]` y 9 dependen de un redirect en el componente de layout. Un cambio de layout, una página con otro layout o un render directo deja la ruta "pública" en el sentido de autorización por rol (solo exige *alguna* sesión, no un rol). Además `Dashboard` envía emails y exporta datos (`Pages\Dashboard.razor:565-578`).
- **Corrección:** agregar `@attribute [Authorize(Roles = "...")]` a las 9 (consistente con NavMenu), dejando el gate de MainLayout solo como UX.

**5. `/register` abierto al público sin fricción**
- `Pages\Register.razor:2` — `@attribute [AllowAnonymous]`; `appsettings.json:41` — `"AllowRegistration": true`; rol por defecto `Operador` (`appsettings.json:43`, `AuthSettings.cs:23`); primera registración = `Admin` (`RegisterCommandHandler.cs:13-14, 59-61`).
- **Desviación:** sin rate limit (hallazgo 2) ni invitación/verificación, cualquier persona que alcance la URL crea cuentas activas. La única salvaguarda es la flag de configuración.
- **Corrección:** `AllowRegistration: false` por defecto en producción, alta solo por `CreateUserCommand` desde `Usuarios.razor`, o registro por token de invitación; y aplicarle política de rate limiting estricta.

### Severidad MEDIA

**6. Validación declarada pero nunca ejecutada (validador FluentValidation muerto)**
- `GuarinoApp.Application\DependencyInjection.cs:15` — `AddValidatorsFromAssembly(...)`, pero **no existe ningún `ValidationBehavior<,>`** (`IValidator<` y `ValidationBehavior`: 0 coincidencias en el repo).
- `Modules\Stock\Commands\CrearPallet\CrearPalletCommandValidator.cs:4-11` define reglas que jamás corren; `CrearPalletCommandHandler.cs` no inyecta validador alguno.
- Peor: `CrearPalletCommand` **no se usa en ninguna página** (0 referencias en `*.razor`) — comando + handler + validador huérfanos.
- **Desviación:** "las validaciones existen" es una ilusión; además hay un endpoint-interno muerto.
- **Corrección:** registrar `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))` y decidir el contrato (lanzar `ValidationException` → mensaje 400-equivalente o `Result.Fail`); eliminar `CrearPallet*` si no se usa, o enchufarlo a una página.

**7. Validación ad-hoc por módulo, sin patrón único**
- `Modules\Contactos\ContactoValidacion.cs:7-8` lo documenta explícitamente: *"La app valida dentro de los handlers y devuelve Fail(mensaje) (no hay pipeline de FluentValidation registrado)"*.
- `Modules\Auth\Commands\Register\RegisterCommandHandler.cs:44-52` valida inline (longitudes) sin DataAnnotations ni validador.
- **Desviación:** cada módulo reinventa la rueda; los límites de longitud viven repartidos entre UI, sanitizador y handler.
- **Corrección:** un solo mecanismo (FluentValidation + pipeline) y mover `ContactoValidacion` a `ContactoCommandValidator`.

**8. Manejo de errores inconsistente y con fuga de detalle técnico al usuario**
- Fuga explícita: `Pages\Dashboard.razor:577` muestra `ex.Message | Detalle: {ex.InnerException?.Message}` en el Snackbar; igual `Pages\PalletsDefinitivos.razor:273,308,353,401,419`, `GestionRiesgosClimaticos.razor:451,483-501,523`, `ResumenLiquidaciones.razor:134,149,165,196`, `RegistroPesadas.razor:159,181`, `LotesVolcados.razor:164,186`, `LiquidacionesQuinta.razor:135,150,166,197`, `FrutaVolcar.razor:236,258`, `GraficaProcesos.razor:125,213`, `Contactos.razor:171`, `Shared\ContactoImportDialog.razor:148,198`.
- Silenciamiento: ~20 páginas hacen `catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }` sin avisar al usuario (el `Console` de Blazor Server es del servidor, no del navegador): `Basculas.razor:69-72`, `Transportistas.razor:71`, `StockPlaya.razor:95`, `StockGeneral.razor:173`, `StockPalletsSinCliente.razor:114`, `ResumenMensual.razor:82`, `ProyeccionVolcados.razor:202`, `PalletsSinCliente.razor:70`, `PalletsContramarca.razor:69`, `Lugares.razor:71`, `LugaresOrigen.razor:71`, `LugaresDestino.razor:71`, `Gerencia.razor:181`, `Empaque.razor:95`, `CamionesZorras.razor:71`, `Dashboard.razor:365`, `FrutaVolcar.razor:123`, `LotesVolcados.razor:102`, `RegistroPesadas.razor:97`, `ResumenLiquidaciones.razor:113`.
- **Desviación:** dos extremos opuestos (fuga de detalle interno vs. error invisible) y cero consistencia, equivalente a devolver 500 con stack trace en unos casos y 200 vacío en otros.
- **Corrección:** mensaje genérico al usuario ("No se pudo cargar X. Revisá el detalle en el log") + `Logger.LogError` (ya disponible en varias páginas), centralizado en un helper o en un `ErrorBoundary` por página.

**9. Health endpoints públicos y con posibilidad de filtrar excepciones**
- `Program.cs:235-239` — ni `RequireAuthorization()` ni AllowAnonymous explícito; `/health` expone estado de infraestructura a cualquiera que alcance el host.
- `Infrastructure\Services\SqlServerHealthCheck.cs:42` — `HealthCheckResult.Unhealthy("... " + ex.Message)`.
- **Desviación:** el mensaje de la excepción viaja dentro del resultado del check (hoy el writer por defecto no lo serializa, pero basta personalizar `ResponseWriter` para exponerlo).
- **Corrección:** loguear la excepción (ya se hace, línea 41) y devolver descripción genérica; proteger `/health` o moverlo a puerto/pipeline interno.

**10. Dos esquemas de autenticación paralelos y comentario contradictorio**
- `Program.cs:21-22` afirma: *"JWT sería para APIs públicas o SPAs, no aplica aquí"*, pero en modo Local la app emite su propio token (`LoginCommandHandler.cs:69-89`, `RegisterCommandHandler.cs:82-88`) y lo guarda en `localStorage` (`CustomAuthenticationStateProvider.cs:181`), además del Negotiate configurado en `Program.cs:24`.
- **Desviación:** coexisten autenticación por Windows y por token propio almacenado en `localStorage` (superficie XSS), con la documentación de `Program.cs` negando lo segundo.
- **Corrección:** decidir el modo por entorno (Local solo en dev), o guardar la sesión en cookie `HttpOnly`/`SameSite` en vez de `localStorage`, y corregir el comentario de `Program.cs:19-23`.

**11. Huérfanos fuera del proyecto y rutas muertas**
- `Pages\EmailTest.razor:1` (`@page "/email-test"`) y `Shared\AIParametersPanel.razor` están en la **raíz del repo**, fuera de `GuarinoApp.Web`, por lo que no se compilan. Es código muero engañoso que además envía correos a destinatario arbitrario (`EmailTest.razor:86`) y muestra `ex.Message` (`EmailTest.razor:93`).
- `Pages\Counter.razor:1` (`/counter`) es plantilla de Blazor sin usar (0 referencias en `NavMenu.razor`), sin `[Authorize]` y con markup ajeno a MudBlazor; `Shared\SurveyPrompt.razor` tampoco se referencia.
- **Corrección:** borrar los huérfanos de la raíz, `/counter` y `SurveyPrompt`, o moverlos a `Propuesta/` con su documentación.

**12. Inconsistencia en la nomenclatura de rutas**
- Kebab-case: `Pages\PalletsDefinitivos.razor:1` `/pallets-definitivos`, `GestionRiesgosClimaticos.razor:1` `/gestion-riesgos-climaticos`, `ProyeccionVolcados.razor:1` `/proyeccion-volcados`, `ProyeccionZafra.razor:1` `/proyeccion-zafra`.
- Concatenado: `PalletsSinCliente.razor:1` `/palletssincliente`, `PalletsContramarca.razor:1` `/palletscontramarca`, `StockPalletsSinCliente.razor:1` `/stockpalletssincliente`, `RegistroPesadas.razor:1` `/registropesadas`, `ResumenMensual.razor:1` `/resumenmensual`, `LiquidacionesQuinta.razor:1` `/liquidacionesquinta`, `LotesVolcados.razor:1` `/lotesvolcados`, etc.
- **Desviación:** dos convenciones para el mismo concepto (equivalente a mezclar `/pallets-definitivos` y `/palletsDefinitivos` en una API).
- **Corrección:** estandarizar kebab-case en todo y mantener rutas viejas con redirect 301 si hay enlaces externos.

### Severidad BAJA

**13. Envelopes de resultado sin contrato único** — coexisten `AuthResultDto` (`Modules\Auth\Dtos\AuthResultDto.cs:4`), `AuthSessionDto` (`AuthSessionDto.cs:4`), `ContactoResultDto` (mismo patrón `Success`/`Error`), `ImportarContactosResultDto` y `CrearPalletCommandHandler.cs:6` que devuelve un `int` crudo sin envelope. Unificar en un `Result<T>` de `Common` (o `IResult`) para que toda operación tenga la misma forma de éxito/error.

**14. Desalineación carpeta/comando** — `Modules\Auth\Commands\UpdateUser\UpdateUserEmailCommand.cs:11` vive en la carpeta `UpdateUser` mientras su nombre corresponde a `UpdateUserEmail`. Renombrar carpeta o comando.

**15. `GetGraficaProcesosQuery` es `class` en vez de `record`** — `Modules\GraficaProcesos\Queries\GetGraficaProcesos\GetGraficaProcesosQuery.cs:6`, cuando los otros 91 requests/queries son `record`. Dejarlo como `record` por inmutabilidad y consistencia.

**16. Identidades/secretos en configuración** — `appsettings.json:36` (`"ADRIA": "Admin"`) y `appsettings.json:74` (email del admin) hardcodeados en el repo; mover a `UserSecrets`/variables de entorno por entorno.

**17. Headers** — `Program.cs:215` envía `X-XSS-Protection: 1; mode=block`, header deprecado por los navegadores; y no hay `Content-Security-Policy` pese a que `_Host.cshtml:21-22` carga Google Fonts por CDN. Reemplazar el primero por CSP estricto.

---

## Lo que está bien

- **Arquitectura de datos a la UI:** ninguna página inyecta `ApplicationDbContext` directamente (solo menciones en comentarios, `Pages\ProyeccionZafra.razor:793`, `Auditoria.razor:159`); todo pasa por MediatR.
- **DTOs, no entidades:** los 92 requests/queries de `GuarinoApp.Application\Modules` devuelven DTOs (`List<BasculaDto>`, `PagedResult<LogDto>`, `ContactoResultDto`…); no hay respuestas crudas de EF hacia la UI. Ejemplo representativo: `Modules\Basculas\Queries\GetBasculas\GetBasculasQuery.cs:5`.
- **Sanitización global de entrada:** `InputSanitizationBehavior` registrado como pipeline behavior (`DependencyInjection.cs:19-21`, `Common\Behaviors\InputSanitizationBehavior.cs:32-39`) con exclusiones correctas para `Password/Hash/Token`.
- **Envelopes con mensaje apto para el usuario:** `AuthResultDto.Fail`/`AuthSessionDto.Fail` (`AuthResultDto.cs:13`, `AuthSessionDto.cs:25`) evitan exponer texto técnico en auth.
- **Validación de returnUrl (sin open redirect):** `Pages\Login.razor:134-139` fuerza ruta relativa y descarta `//`.
- **Autorización por rol en 24 páginas** vía `@attribute [Authorize(Roles=...)]` + `AuthorizeRouteView` y `CascadingAuthenticationState` (`App.razor:1-20`).
- **Seguridad perimetral del pipeline:** headers en `Program.cs:211-221`, HSTS (`:188-193`), HTTPS redirect (`:223`), CORS por entorno con lista configurable (`:159-183`), y `ValidateOnStart` de opciones (`:60-68`, `:92-95`).
- **Auditoría de accesos:** login exitoso/fallido y altas de usuario registradas (`LoginCommandHandler.cs:41-45, 77-80`, `RegisterCommandHandler.cs:90-93`).
- **Página de error sin fuga de detalle:** `Pages\Error.cshtml.cs:22-25` solo expone `RequestId`, con `NoStore` (`:7`).
- **Reintentos y degradación razonables** en el arranque (`Program.cs:289-317`) y en `OllamaHealthCheck.cs:29-46` (Degraded en vez de Unhealthy cuando la IA está deshabilitada).

---

## Top 5 correcciones

1. **Arreglar o retirar `/health/ready`** — agregar tags `"ready"` a `SqlServerHealthCheck`/`OllamaHealthCheck` en `Program.cs:122-124`; hoy devuelve 200 sin comprobar nada (hallazgo 1).
2. **Aplicar el rate limiting ya configurado** — `GlobalLimiter` o `.RequireRateLimiting("fixed")` en `/login`, `/register` e IA; la política `"fixed"` de `Program.cs:133` hoy no protege a ningún endpoint (hallazgo 2).
3. **Cerrar el acceso por rol** — `@attribute [Authorize(Roles=...)]` en las 9 páginas sin atributo (`Dashboard`, `StockPlaya`, `StockGeneral`, `RegistroPesadas`, `LotesVolcados`, `FrutaVolcar`, `PalletsSinCliente`, `Index`, `Counter`) y `EnableFallbackIdentity: false` en producción (`appsettings.json:33`) para que nadie herede "Gerencia" (hallazgos 3 y 4).
4. **Hacer efectiva la validación** — registrar `ValidationBehavior<,>` en `Application\DependencyInjection.cs` (o borrar el validador muerto `CrearPalletCommandValidator.cs` y el comando huérfano `CrearPalletCommand`), y unificar todo en FluentValidation + envelope `Result<T>` (hallazgos 6, 7 y 13).
5. **Unificar el manejo de errores de las páginas** — eliminar `ex.Message`/`InnerException` del UI (`Dashboard.razor:577` y 12 ubicaciones más) y sustituir los `catch` silenciosos con `Console.WriteLine` (20 páginas) por un helper común: mensaje genérico al usuario + `Logger.LogError` (hallazgo 8); de paso, estandarizar rutas en kebab-case (hallazgo 12).
