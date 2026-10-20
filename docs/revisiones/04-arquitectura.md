# Revisión de arquitectura — GuarinoApp

> Informe generado por revisión de código (solo lectura del repositorio).
> Alcance leído: `GuarinoApp.sln`, los 4 proyectos + `BenchmarkSuite1`, `Program.cs`, ambos `DependencyInjection.cs`, ~40 handlers MediatR, ~40 páginas `.razor`, `Shared/`, `Infrastructure/Services/`. Ignorados `Propuesta/`, `csv Propuesta/`, `bin/`, `obj/`. No se ejecutó build ni se tocó ningún archivo de código.

**Mapa de dependencias real (verificado en los `.csproj`):**

```
Web ──► Application ──► Domain
 │          ▲
 └──► Infrastructure ──┘
```

No hay referencias cruzadas inversas (Application nunca referencia Infrastructure; Infrastructure solo cita `GuarinoApp.Web` como *ruta de texto* en `ApplicationDbContextFactory.cs:12`, no como referencia de proyecto).

---

## 🔴 HALLAZGOS DE ALTA SEVERIDAD

### 1. La autorización vive en la UI, no en el servidor — y 7 páginas de negocio no tienen `[Authorize]`
**`GuarinoApp.Web/Program.cs:25`** · **`GuarinoApp.Web/Shared/MainLayout.razor:49-64`** · **`GuarinoApp.Web/App.razor:7`**

`builder.Services.AddAuthorization()` se llama **sin `FallbackPolicy`**. En Blazor Server eso significa: toda página **sin** `@attribute [Authorize]` es navegable por cualquier circuito autenticado (y el "gate" que existe es un `NavigateTo("/login")` dentro de `MainLayout.EnforceAuthenticationAsync`, es decir, un redirect de cliente que corre *después* de que el componente ya arrancó).

Páginas de negocio sin `[Authorize]` (verificado sobre los 40 `@page`):
`/dashboard`, `/frutavolcar`, `/lotesvolcados`, `/registropesadas`, `/stockgeneral`, `/stockplaya`, `/palletssincliente` — más `/counter` (plantilla de Blazor, sin relación con el negocio) y `/`, `/login`, `/register` (estos últimos esperables).

Contraste: las otras 24 páginas **sí** lo tienen y coinciden con los `AuthorizeView` del `NavMenu.razor` (verificado una por una: Transportistas, Contactos, Gerencia, Usuarios, etc. → mismos roles en menú y página). El problema es solo la ausencia, no la inconsistencia.

**Mejora (composition root, 3 líneas):**

```csharp
// Program.cs:25
builder.Services.AddAuthorization(options =>
{
    // Toda página sin [Authorize] exige sesión: defensa por defecto.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

Y agregar `@attribute [Authorize(Roles = "Admin, Gerencia, Contador, Operador")]` a las 7 páginas de operación (o `[Authorize]` simple si son para todos los roles autenticados). Mientras tanto el redirect de `MainLayout` queda como redundancia, no como única barrera.

---

### 2. FluentValidation está registrado pero **nunca se ejecuta** — no existe `ValidationBehavior`
**`GuarinoApp.Application/DependencyInjection.cs:15`** · **`GuarinoApp.Application/Modules/Stock/Commands/CrearPallet/CrearPalletCommandValidator.cs:4`** · **`GuarinoApp.Application/Modules/Stock/Commands/CrearPallet/CrearPalletCommandHandler.cs:13-30`**

```csharp
services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly); // ← registra...
```

...pero **no hay ningún `IPipelineBehavior` de validación** en el proyecto (busqué `ValidationBehavior`/`IValidator<`: 0 matches en código, solo en `docs/`). Existe **un solo validador en todo el repositorio** (`CrearPalletCommandValidator`) y ningún handler inyecta `IValidator<T>` → ese validador es código muerto. `CrearPalletCommandHandler` hace `Add(entity); SaveChangesAsync()` sin validar nada.

Resultado: tres estrategias de validación distintas y una muerta.

- Contactos: validación manual estática (`CrearContactoCommandHandler.cs:35` → `ContactoValidacion.Validar(...)`) ✅ funciona pero es artesanal.
- CrearPallet: FluentValidation ❌ nunca corre.
- Resto de commands (`UpdateUserCommand`, `ActualizarContacto`, `LogAction`...): ❌ sin validación explícita.

**Mejora — comportamiento faltante en `Application/Common/Behaviors/`:**

```csharp
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest req, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();
        var ctx = new ValidationContext<TRequest>(req);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(ctx, ct))))
                                  .SelectMany(r => r.Errors)
                                  .Where(f => f is not null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
        return await next();
    }
}
// DependencyInjection.cs: tras AddValidatorsFromAssembly
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

Y elegir **una** estrategia: FluentValidation para todos los commands (o borrar FluentValidation y quedarse con `ContactoValidacion`, pero no las tres).

---

### 3. Los errores de carga se tragan con `Console.WriteLine` → una caída de BD se muestra como "0 registros"
**42 ocurrencias en 25 páginas** — p. ej. `Pages/Transportistas.razor:69-78`, `Basculas.razor:71`, `Lugares.razor:71`, `PalletsSinCliente.razor:70`, `Dashboard.razor:365`, `LiquidacionesQuinta.razor:114`.

El patrón copiado en todas las páginas de listado:

```csharp
catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
finally { _loading = false; _cargado = true; StateHasChanged(); }
```

`_cargado = true` + `_data` vacío ⇒ el usuario ve **`MudAlert: "No se encontraron registros."`** (`Transportistas.razor:46`) cuando en realidad falló la consulta. Es un bug silencioso de UX/diagnóstico (el propio `PuertaSerializacionDbContext.cs:10` documenta que ya les pasó con Auditoría mostrando "0 registros").

Solo 8 páginas inyectan `ILogger<T>` (Contaduria, Usuarios, Gerencia, Dashboard, IAChat, ProyeccionZafra, Auditoria, ContactoImportDialog); el resto no tiene logger inyectado.

**Mejora:** extraer el ciclo de carga a un componente/clase base y loguear con `ILogger`:

```razor
@* Shared/LoadedList.razor — un solo lugar para try/catch + estado *@
@typeparam T
@inject ILogger<LoadedList<T>> Logger
@inject ISnackbar Snackbar
@code {
    [Parameter] public required Func<Task<List<T>>> Load { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    private bool _error;
    // ... catch (Exception ex) { _error = true; Logger.LogError(ex, "Carga de listado");
    //      Snackbar.Add("No se pudo cargar la información.", Severity.Error); }
}
```

Alternativa mínima: inyectar `ILogger<T>` en las 25 páginas y reemplazar los 42 `Console.WriteLine` por `Logger.LogError(ex, ...)` + `Snackbar.Add(...)`.

---

## 🟠 HALLAZGOS DE MEDIA SEVERIDAD

### 4. Copy-paste sistemático de páginas de listado + tablas por reflexión
**11 páginas** con el mismo bloque `@foreach (var prop in typeof(XxxDto).GetProperties())`:
`Transportistas.razor:28/34`, `Basculas.razor:28/34`, `CamionesZorras.razor:28/34`, `Lugares.razor`, `LugaresOrigen.razor:28/34`, `LugaresDestino.razor:28/34`, `PalletsSinCliente.razor:27/33`, `ResumenLiquidaciones.razor:53/59`, `RegistroPesadas.razor:34/40`, `LiquidacionesQuinta.razor:54/60`, `LotesVolcados.razor:40/46`.

Compará `Transportistas.razor` con `LugaresOrigen.razor`: **son el mismo archivo con 4 tokens cambiados** (DTO, query, rótulo, tooltip). Diferencias no intencionadas ya introducidas: `@value` vs `@value?.ToString()`, `EsDummy="false"` presente en unas y ausente en otras (por eso `DataSourceFooter` difiere entre páginas).

Problemas concretos del render por reflexión:

- Los encabezados salen como `MudTh>@prop.Name` → **"FechaAlta", "KgsVolcados"** en vez de rótulos en español.
- Sin formato por tipo (fechas/decimales), sin `Numeric="true"`, sin columna de acciones.
- Reflection evaluada en cada render.

**Mejora — componente genérico en `GuarinoApp.Web/Shared/`:**

```razor
@* Shared/ReportTable.razor *@
@typeparam T
<MudTable Items="@Items" Hover="true" Striped="true" Dense="true" Breakpoint="Breakpoint.Sm">
    <HeaderContent>
        @foreach (var c in Columns) { <MudTh>@c.Title</MudTh> }
    </HeaderContent>
    <RowTemplate>
        @foreach (var c in Columns) { <MudTd>@c.Format(context)</MudTd> }
    </RowTemplate>
</MudTable>

@code {
    [Parameter] public IEnumerable<T> Items { get; set; } = [];
    [Parameter] public IReadOnlyList<Column<T>> Columns { get; set; } = [];
}
```

Con `Column<T>.Format` = `x => x.FechaAlta.ToString("dd/MM/yyyy")`, etc. Ahorrás ~11 × 40 líneas y ganás rótulos/formato consistentes.

---

### 5. Exportación y envío por email duplicados en 7 páginas (~70 líneas cada una)
**`LiquidacionesQuinta.razor:124-189`**, `ResumenLiquidaciones.razor:123-196`, `RegistroPesadas.razor:107-181`, `FrutaVolcar.razor:184-257`, `LotesVolcados.razor:112-185`, `GraficaProcesos.razor:177-213`, `Dashboard.razor:492-539`.

`ExportToPdf` + `ExportToExcel` + `ExportToCsv` + `ExportarEmail` son idénticos salvo el nombre del archivo y el título; incluso el patrón `EnsureDataForExportAsync()` está repetido 7 veces.

**Mejora:** un servicio de aplicación `IReportExportService` (o componente `ReportActions`) que reciba *datos + metadatos del reporte*:

```csharp
public interface IReportActions
{
    Task PdfAsync<T>(IEnumerable<T> rows, string title, string fileName);
    Task ExcelAsync<T>(IEnumerable<T> rows, string fileName);
    Task CsvAsync<T>(IEnumerable<T> rows, string fileName);
    Task EmailAsync<T>(IEnumerable<T> rows, string title, string fileName, string body);
}
```

Implementación = el código actual de `LiquidacionesQuinta.razor:124-189` movido una sola vez; cada página queda con 4 una-líneas. Estimado: **~450 líneas eliminadas**.

---

### 6. `GetFrutaVolcarQueryHandler`: carga total en memoria, proyección duplicada y una rama de filtros muerta con cast frágil
**`GuarinoApp.Application/Modules/FrutaVolcar/Queries/GetFrutaVolcar/GetFrutaVolcarQueryHandler.cs`**

- **Líneas 22-32**: `Include × 8` + `ToListAsync()` sobre **toda la tabla `Lotes`**, y después la proyección a DTO se hace **en memoria** (líneas 87-114). Sin `Select()` server-side, EF trae grafos completos que después se descartan.
- **Líneas 54-83 vs 87-114**: el mismo bloque de mapeo a `FrutaVolcarDto` está **copiado byte a byte dos veces** (~30 líneas ×2). Cualquier cambio de DTO hay que hacerlo en dos lugares.
- **Línea 47**:

  ```csharp
  query = (Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<Lote, Domain.Entities.Empaque>)
          LinqDynamicQueryBuilder.ApplyFilters(query, request.Filters);
  ```

  Un *downcast* duro al tipo interno de EF sobre el resultado de un `.Where()` genérico. Funciona solo si el proveedor devuelve exactamente ese tipo de envoltorio; si EF lo cambia, revienta con `InvalidCastException` en runtime.
- **La rama nunca se ejecuta**: `FrutaVolcar.razor:118` envía siempre `new GetFrutaVolcarQuery(new List<Filter>())` → el `if (request.Filters.Any())` (línea 35) es código muerto en producción.

**Mejora:**

```csharp
private static readonly Expression<Func<Lote, FrutaVolcarDto>> Proyectar = l => new FrutaVolcarDto
{
    Lote = l.Id,
    Playa = l.Empaque!.Descripcion,
    Especie = l.Variedad!.Especie!.Descripcion ?? string.Empty,
    // ... una sola vez
    KgSaldo = l.CantidadBins > 0 ? (int)((l.KilosNetos / l.CantidadBins) *
              (l.CantidadBins - l.CantidadBinsDesp - l.CantidadBinsEmb)) : 0
};

// sin filtros:
var result = await _context.Lotes.AsNoTracking()
    .Where(l => !l.IsDeleted)
    .Select(Proyectar).ToListAsync(ct);
```

y, si los filtros no se usan, **borrar** `List<Filter>` del command (hoy solo fuerza a mantener tres mecanismos de filtro). Si se van a usar, eliminar el cast y tipar como `IQueryable<Lote>`.

---

### 7. Tres implementaciones paralelas de "filtros dinámicos", dos de ellas muertas

| Implementación | Archivo | Estado |
|---|---|---|
| `BuildWhereClause` (SQL + `SqlParameter`) | `Application/Common/Builders/DynamicQueryBuilder.cs:19` | **0 usos** |
| `ApplyFilters` (Expression trees por reflexión) | `Application/Common/Builders/LinqDynamicQueryBuilder.cs:8` | solo la rama muerta del hallazgo 6 |
| `ApplyFilters` tipada por campo | `Application/Modules/StockPlaya/.../GetStockPlayaQueryHandler.cs:82-187` | la única viva, y los filtros llegan vacíos (`StockPlaya.razor:90`) |
| `FilterPersistenceService` (serializa `List<Filter>`) | `Application/Common/Services/FilterPersistenceService.cs:5` | **0 usos** |

Efecto colateral de `DynamicQueryBuilder`: **`GuarinoApp.Application` referencia `Microsoft.Data.SqlClient`** (`GuarinoApp.Application.csproj:13`) — una dependencia de acceso a datos **dentro de la capa de aplicación**, solo para una clase que no se usa. Lo mismo con `System.Linq.Dynamic.Core` (`:19`, 0 `using` en el proyecto) y `Microsoft.JSInterop` (`:18`, 0 usos).

**Mejora:** borrar `DynamicQueryBuilder.cs`, `LinqDynamicQueryBuilder.cs`, `FilterPersistenceService.cs` y los 3 paquetes; si algún día se necesitan filtros, una sola abstracción declarativa:

```csharp
// Application/Common/Filters/IFilterApplier.cs
public interface IFilterApplier<T>
{
    IQueryable<T> Apply(IQueryable<T> query, IReadOnlyList<Filter> filters);
}
```

---

### 8. `IDashboardDataService` es un segundo camino de datos fuera de MediatR, con scope propio por llamada
**`GuarinoApp.Infrastructure/Services/DashboardDataService.cs:22, 42, 71, 84, 110, 137, 144`** — 7 `IServiceScopeFactory.CreateScope()`.

Cada método crea un scope y **un `ApplicationDbContext` nuevo**. Una carga de dashboard puede abrir ~7 contextos distintos (identidad map y caché de consultas de EF no se aprovechan) y el servicio **no pasa por el pipeline MediatR** ni por la puerta de serialización: lo consumen `Dashboard.razor:11`, `IAChat.razor:12`, `ProyeccionZafra.razor:17`, `StockGeneral.razor:7` y las skills (`StockPlantaSkill.cs:16`, `IndicadoresGerenciaSkill`, etc.).

El comentario de `DashboardDataService.cs:9-15` explica *por qué* existe (colisiones con el DbContext del circuito), pero el síntoma correcto es de diseño: **el DbContext de circuito es un recurso compartido que todos tienen que esquivar a su manera** (la puerta en MediatR, scopes propios en AuditService/DashboardDataService/CustomAuthenticationStateProvider).

**Mejora (dos pasos, en orden):**

1. Corto plazo — mover las lecturas del dashboard a queries MediatR reales, que ya corren bajo la puerta:

   ```
   Modules/Dashboard/Queries/GetStockPorVariedad/...
   Modules/Dashboard/Queries/GetVolcadosMensuales/...
   ```

   y que `StockPlantaSkill` use `IMediator.Send` en vez de `IDashboardDataService`.
2. Medio plazo — eliminar la puerta usando factory y un contexto por operación:

   ```csharp
   // Infrastructure/DependencyInjection.cs:27
   services.AddDbContextFactory<ApplicationDbContext>(o => o.UseSqlServer(...));
   services.AddScoped<IApplicationDbContext>(sp =>
       sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
   ```

   Con esto desaparecen `PuertaSerializacionDbContext`, `SerializacionDbContextBehavior` y los `CreateScope()` por método — que hoy **serializan todas las cargas de una página** (el propio comentario de `SerializacionDbContextBehavior.cs:6-7` admite: "retener la puerta frenaría todas las cargas de la página").

---

### 9. Registros duplicados/dispersos en el composition root
- **HttpClient "Ollama" configurado dos veces**: `Program.cs:97-116` e `Infrastructure/DependencyInjection.cs:70-78`, con reglas de timeout **distintas** (`Math.Max(opts.TimeoutSeconds, 300)` vs `opts.TimeoutSeconds`). Ambos `Configure<HttpClientFactoryOptions>(nombre, …)` corren en orden de registro → hoy gana `Program.cs` por ir después. Si alguien borra el bloque de `Program.cs` pensando que está duplicado, el timeout pasa a poder ser 30 s y los timeouts de cold start vuelven.
- **`ClimateMonitoringHostedService` registrado dos veces**: `Program.cs:82` e `Infrastructure/DependencyInjection.cs:144`. No duplica la instancia (`AddHostedService` hace `TryAddEnumerable` por tipo), pero **dos dueños** del mismo registro ⇒ confusión y riesgo si se cambia por `AddSingleton<IHostedService, T>()`.
- **Servicios "de dominio" en `Program.cs:73-74`** (`IClimateService`, `IClimateRiskService`) mientras el resto (50+ registros) vive en `AddInfrastructure`. El propio comentario de `DependencyInjection.cs:146-149` dice que los prompts se movieron "junto con el resto de los servicios de Infrastructure" — faltó mover el clima.
- **`ValidateOllamaOptions.cs:6`** (`Web/Validation/`) **nunca se registra** (`IValidateOptions<T>` no se auto-descubre): es código muerto, y encima es la única pieza de validación de `Ollama` condicional a `Enabled` (los `[Required]` de `OllamaOptions.cs:14-18` fallan aunque `Enabled=false`).

**Mejora:** dejar `Program.cs` solo con pipeline + middleware; mover `IClimateService`/`IClimateRiskService` a `AddInfrastructure`; borrar un bloque de `AddHttpClient("Ollama")` (quedarse con el de Infrastructure + el handler de `Program.cs:109` movido ahí); registrar `IValidateOptions<OllamaOptions>` o borrarlo.

---

### 10. Dependencias de implementación en lugar de abstracciones (fugas de capa)
- **`Pages/PalletsDefinitivos.razor:14`** → `@inject GuarinoApp.Infrastructure.Services.IPdfGenerator PdfGenerator`: la UI conoce un tipo de Infrastructure (la interfaz vive en `Infrastructure/Services/IPdfGenerator.cs`, no en `Application/Common/Interfaces` como el resto: `IExportService`, `IEmailService`, `IAIService`...). Además está duplicada: `IExportService.ExportToPdfAsync` (Application) hace lo mismo.
- **`Infrastructure/Services/FallbackAIService.cs:19`** → `private readonly OllamaAIService _ollamaService;`: un servicio de Application-facing depende de la **clase concreta** de otro servicio, no de una interfaz `IOllamaClient`. No se puede sustituir ni testear.
- **`Web/Shared/ContactoDialogModelo.cs:1,15`**, **`Shared/ContactoDialog.razor:6`**, **`Pages/Contactos.razor:12`** → `using GuarinoApp.Domain.Entities` y `ContactoEstados.Nuevo`. La capa Web **se salta el contrato de Application** usando tipos de Domain (que encima llegan por *referencia transitiva*, `Web.csproj` no declara `GuarinoApp.Domain`).
- **`Pages/Counter.razor`** (ruta `/counter`, sin autorizar) y **`Shared/SurveyPrompt.razor`**: restos de la plantilla Blazor, publicados.

**Mejora:**

```csharp
// Application/Common/Interfaces/IPdfGenerator.cs  (mover desde Infrastructure)
// Application/Common/Interfaces/IOllamaClient.cs  (extraer de OllamaAIService)
// Application/Modules/Contactos/Dtos/ContactoEstadosDto.cs  (en vez de Domain.Entities)
```

y borrar `Counter.razor`/`SurveyPrompt.razor`.

---

### 11. Auditoría: falla en silencio y escribe en otra transacción que la operación
**`Infrastructure/Services/AuditService.cs:46-49` y `:51-54`**

```csharp
using var scope = _scopeFactory.CreateScope();   // ← DbContext distinto al del command
var context = ...; context.LogEntries.Add(logEntry); await context.SaveChangesAsync();
...
catch { /* No lanzar excepciones ... */ }         // ← 0 diagnóstico
```

Dos consecuencias: (a) si `SaveChangesAsync` del command falla, el log igual quedó escrito (auditoría de algo que no ocurrió); (b) si el log falla, **nadie se entera** — exactamente el síntoma "Auditoría en 0 registros" que `PuertaSerializacionDbContext.cs:10` documenta, esta vez sin rastro.

**Mejora:**

```csharp
catch (Exception ex) { _logger.LogError(ex, "Auditoría no registrada: {Action}/{Module}", action, module); }
```

y, idealmente, registrar el `LogEntry` en el mismo `IApplicationDbContext` del command antes de su `SaveChangesAsync` (una transacción = operación + auditoría atómicos), dejando el scope propio solo para los flujos de background.

---

### 12. El sanitizador global borra `;`, `--`, `/*`, `*/`, `` ` `` de **todos** los textos
**`Application/Common/Security/InputSanitizer.cs:37`** (`SqlInjectionSequences`) usado por **`Behaviors/InputSanitizationBehavior.cs:74`** en *todas* las queries/commands.

Con EF Core parametrizado (la defensa real, y así está implementada toda la app — lo dice el propio XML de la clase), esta regla **corrompe contenido legítimo sin avisar**: `"Revisar stock; llamar al productor"` → pierde el `;`; una búsqueda `Lote -- control` pierde guiones; un texto con `/*` se trunca. Y convive con **`Application/Common/SanitizadorTexto.cs:8`**, que se declara "una sola fuente de verdad para toda la app" con reglas **distintas** (colapsa `&lt;script&gt;`, no toca `;`) — los handlers de Contactos aplican ambos, en distinto orden.

**Mejora:** en el behavior, dejar solo lo inocuo (caracteres de control, espacios repetidos, longitud); **quitar la limpieza "SQL"** (la defensa es el LINQ parametrizado) y unificar `InputSanitizer` + `SanitizadorTexto` en una sola clase con dos perfiles (`UnaLinea` / `Multilinea`).

---

### 13. Rate limiting declarado pero nunca aplicado → efecto cero
**`Program.cs:130-139`** define la política `"fixed"` (100 req/min) y **`:228`** llama `app.UseRateLimiter()`, pero **no existe ningún `.RequireRateLimiting("fixed")`** en el código (0 matches en el repo; solo aparece en `docs/SEGURIDAD.md:303`). El middleware corre y no limita nada.

**Mejora:** aplicarlo al hub de SignalR, que es la superficie real de Blazor Server:

```csharp
app.MapBlazorHub().RequireRateLimiting("fixed");
```

(o `options.GlobalLimiter` si se quiere para todo, entendiendo que SignalR tiene su propio throttling).

---

### 14. `AuditService`, `DashboardDataService` y las skills replican lógica que ya existe en handlers
Ej.: `StockPlantaSkill.cs:43-49` recalcula stock por variedad vía `IDashboardDataService` en vez de reutilizar `GetPalletsQuery`/una query de stock; `SkillRegistry.cs:93-108` y `StockPlantaSkill.cs:97-107` tienen **la misma función `Normalize` duplicada** (el comentario lo admite: *"misma receta que SkillRegistry"*). Igual `Normalizar`/`Normalize` sin acentos.

**Mejora:** `Application/Common/Helpers/TextoNormalizador.cs` con `public static string SinAcentos(string)` — una sola receta, usada por skills y por cualquier búsqueda futura.

---

## 🟡 HALLAZGOS DE BAJA SEVERIDAD

**15. Código muerto y carpetas fantasma**
- `Infrastructure/Services/EfDashboardDataService.cs` **excluido del build** (`GuarinoApp.Infrastructure.csproj:10-11`) pero sigue en el repo **y en el README:149** como si estuviera vivo.
- `GuarinoApp.Web/Extensions/TableExtensions.cs:7-18` → `ExportToCsv` sin escape de comillas ni protección de CSV-injection, y `ExportToPdf:20-37` que devuelve **HTML con nombre `.pdf`**; 0 usos en toda la app (todo el mundo usa `ExportService`). Es una trampa lista para activarse.
- Carpetas vacías: `GuarinoApp.Web/{Components, Services, Data}` (`Data` además declarada en `GuarinoApp.Web.csproj:40`), `GuarinoApp.Application/Domain/Entities`.
- **Archivos huérfanos fuera de la solución**: `C:\Claude-Proyectos\Guarino\Pages\EmailTest.razor` y `C:\Claude-Proyectos\Guarino\Shared\AIParametersPanel.razor` (no pertenecen a ningún `.csproj` → no compilan, solo ensucian).

**16. Desalineación de paquetes** — `GuarinoApp.Web.csproj:26`: `Microsoft.Extensions.DependencyInjection.Abstractions` **10.0.10** (paquete de .NET 10) en un proyecto `net8.0`, mientras todo el resto usa `8.0.x` (`Application.csproj:16` = 8.0.2). Riesgo de unificación de ensamblado rara. También `Microsoft.EntityFrameworkCore.Design` duplicado en `Web:22` e `Infrastructure:20` (design-time solo necesita estar en Infrastructure).

**17. Estructura CQRS inconsistente entre módulos**
- Handler en archivo aparte: `Transportistas/.../GetTransportistasQuery.cs` + `GetTransportistasQueryHandler.cs` ✅.
- Handler **dentro** del archivo del query/command: `Contaduria/.../GetFinancialDataQuery.cs:19`, `GetRolesQuery.cs:20`, `Auditoria/.../LogActionCommand.cs:15`, `Contaduria/.../UpdateFinancialDataCommand.cs:30`.
- `ClimateRisk` ni siquiera tiene subcarpetas `Queries/GetX/`: `Modules/ClimateRisk/Queries/GetClimateRiskSummaryQuery.cs` y `GetClimateMapMarkersQueryHandler.cs` colgando sueltos.
- Dos carpetas de migraciones en el mismo ensamblado: `Infrastructure/Migrations/` e `Infrastructure/Data/Migrations/`.

**18. `NoWarn` que tapa problemas reales de nullabilidad** — `GuarinoApp.Infrastructure.csproj:7` suprime `CS8613;CS8603` (asignabilidad/retorno posiblemente null) y `GuarinoApp.Domain.csproj:7` suprime `CS8618` (propiedades no inicializadas). Preferible `= null!` explícito o `required` en vez de silenciar a nivel de proyecto junto con `TreatWarningsAsErrors=false`.

**19. `Facturacion.razor`** es una página "en construcción" publicada y navegable (con `[Authorize]`, al menos) — candidata a `#if DEBUG` o a quitar la ruta.

---

## ✅ LO QUE ESTÁ BIEN

1. **La separación de capas es real y se respeta.** Ninguna página inyecta EF: busqué `ApplicationDbContext|DbSet<|EntityFrameworkCore` en `GuarinoApp.Web` y los únicos hits son `Program.cs:292/303` (bloque de seed en Development, `Program.cs:289-320`, correctamente con `CreateScope`) y el paquete design-time. Los 87 handlers usan **`IApplicationDbContext`** (`Application/Common/Interfaces/IApplicationDbContext.cs`), nunca `ApplicationDbContext` concreto.
2. **No hay referencias cruzadas entre capas**: Application no referencia Infrastructure (verificado en ambos `.csproj`), Infrastructure no referencia Web, Domain no tiene dependencias (csproj de 9 líneas, solo `BaseEntity`, enums y `IAggregateRoot`).
3. **CQRS con MediatR está bien implementado**: command/query `record` + handler + DTO de salida en 30+ módulos (`Modules/<Modulo>/{Queries,Commands,Dtos}`), y **todas las queries devuelven DTOs planos, nunca entidades** (revisé `ContactoDto`, `PalletDto`, y grepé `GuarinoApp.Domain.Entities` en `**/Dtos/*.cs` → 0 matches). No hay navegaciones ni ciclos en los DTOs; las listas anidadas (`ClimateRiskSummaryDto.Eventos`) son de otros DTOs.
4. **Patrones pensados y documentados en el código**: `InputSanitizationBehavior` (sanitización central OWASP), `SerializacionDbContextBehavior` + `PuertaSerializacionDbContext` con reentrancia por `AsyncLocal` y manejo de la carrera de apagado (`PuertaSerializacionDbContext.cs:34-57`) — este último es un trabajo fino, no un parche a ciegas.
5. **Tiempos de vida bien elegidos en general**: `AddDbContext` **Scoped** (uno por circuito, correcto en Blazor Server), singletons solo para estado compartido/inmutable (`AiResultCache` con `ConcurrentDictionary` + TTL + límite de 256 entradas, `IPromptProvider`, `IIaWebClient` con candado de cupo, `ErrorLogBuffer`), y **cero dependencias cautivas**: todos los `IHostedService` resuelven scoped vía `IServiceScopeFactory` (`ClimateMonitoringHostedService.cs:62`, `NotificadorAdminService.cs:237`, `OllamaRecuperacionService.cs:703`, `AuditService.cs:46`, `OpenMeteoClimateService.cs:109`).
6. **`EnableRetryOnFailure` registrado en un solo lugar** con comentario explícito advirtiendo no duplicarlo (`Infrastructure/DependencyInjection.cs:24-33`).
7. **Reutilización de componentes donde importa**: `InteractiveChart` (10 páginas), `DataSourceFooter` (~22 páginas), `ExportToolbar` (7 páginas), `DescargaHelper` (descarga con BOM UTF-8, unificado), `SimpleDialog`, `CurrentUserDisplay`. Y `FinancialKpiCalculator` (Application) compartido por `Contaduria.razor:496` y `ProyeccionZafra.razor:871/924` → **misma fuente de KPIs en dos pantallas**, exactamente lo que se busca.
8. **`ExportService` es sólido**: escape CSV + neutralización de CSV-injection con la salvedad de los negativos (`ExportService.cs:181-189`), PDF con QuestPDF y paginación, fechas sin hora fantasma.
9. **Coherencia menú ↔ páginas**: los `AuthorizeView` de `NavMenu.razor` coinciden con los `[Authorize(Roles=...)]` de las 24 páginas que sí lo tienen (cotejado una por una).
10. **Sin SQL crudo ni `HttpClient` en la UI**: 0 matches de `FromSqlRaw|ExecuteSqlRaw|SqlConnection` en handlers (la única cita de `SqlConnection` es la clase muerta del hallazgo 7) y el único `AddHttpClient` está en `Program.cs`.

---

## 🏆 TOP 5 MEJORAS DE ARQUITECTURA (mayor impacto)

| # | Mejora | Impacto | Esfuerzo | Archivos clave |
|---|---|---|---|---|
| **1** | **`FallbackPolicy` de autorización + `[Authorize]` en las 7 páginas de negocio sin atributo + borrar `Counter`/`SurveyPrompt`** | Cierra la brecha de seguridad más concreta: hoy `/dashboard`, `/stockgeneral`, `/frutavolcar`... dependen de un redirect de cliente | Bajo (medio día) | `Program.cs:25`, 7 `Pages/*.razor`, `MainLayout.razor:49` |
| **2** | **`ValidationBehavior<,>` real + una sola estrategia de validación** | Hoy FluentValidation está *instalado pero apagado* y los commands entran sin reglas; elimina 3 enfoques distintos | Bajo (1 día) | `Application/DependencyInjection.cs`, `Modules/Stock/Commands/CrearPallet/*`, `Modules/Contactos/*` |
| **3** | **Componente genérico de listado + manejo de errores con `ILogger`/`Snackbar`** | Elimina ~11 × 40 líneas de copy-paste, los **42 `Console.WriteLine`** y el bug "error de BD = 0 registros"; agrega rótulos y formato en español a las tablas | Medio (2-3 días) | `Transportistas.razor`, `Basculas`, `CamionesZorras`, `Lugares*`, `PalletsSinCliente`, `ResumenLiquidaciones`, `RegistroPesadas`, `LiquidacionesQuinta`, `LotesVolcados` |
| **4** | **Servicio único de export/email por reporte** (o mover export a handlers MediatR) | ~450 líneas eliminadas; PDF/Excel/CSV/email dejan de divergir entre páginas | Medio (1-2 días) | `LiquidacionesQuinta.razor:124-189` (prototipo a mover) → `Application/Common/Services/ReportActions.cs` |
| **5** | **Un solo camino de datos: queries MediatR para el dashboard + `IDbContextFactory` para eliminar la puerta de serialización** | Ataca la causa raíz de los bloqueos de página y de los 7 contextos por carga; después se simplifican `AuditService`, `DashboardDataService` y las skills | Alto (3-5 días, incremental) | `Infrastructure/DependencyInjection.cs:27`, `DashboardDataService.cs`, `Behaviors/SerializacionDbContextBehavior.cs`, `Modules/Dashboard/Queries/*` |

**Orden sugerido:** 1 → 2 → 4 → 3 → 5 (la 5 es la que más arquitectura cambia, pero 1 y 2 son valor inmediato con poco código).

---

### Resumen de conteo

- Hallazgos de **alta** severidad: **3** (1-3)
- Hallazgos de **media** severidad: **11** (4-14)
- Hallazgos de **baja** severidad: **5** (15-19)
- **Total de hallazgos: 19**
- Ítems en "Lo que está bien": **10**
- Mejoras en el Top 5: **5**
