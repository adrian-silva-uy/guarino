# Revisión de rendimiento — GuarinoApp (Blazor Server + MudBlazor + EF Core)

**Fecha**: 2026-10-20
**Alcance**: solo lectura de código (`GuarinoApp.Web`, `GuarinoApp.Application`, `GuarinoApp.Infrastructure`), sin builds ni tocar git/BD. Busqué activamente: `.Result/.Wait/Task.Run`, sync-over-async, `async` sin await, fire-and-forget, tracking/SELECT *, N+1, predicados no traducibles, materialización sin límite, `StateHasChanged` excesivos, y trabajo pesado en el hilo del circuito.

---

## HALLAZGOS

### 1. [ALTO] Generación de PDF/Excel/CSV 100 % síncrona *dentro* del hilo del circuito, y descarga enviada por SignalR como Base64

- **Archivo**: `GuarinoApp.Infrastructure\Services\ExportService.cs:12-36` (Excel) y `:38-142` (PDF), `GuarinoApp.Infrastructure\Services\QuestPdfGenerator.cs:8-47, 50-87, 90-138`, descarga en `GuarinoApp.Web\Helpers\DescargaHelper.cs:18`.
- **Por qué es costoso (mecánica concreta)**: los métodos son `Task` pero devuelven `Task.FromResult(...)`, o sea que **todo el trabajo CPU puro se ejecuta sincrónicamente en el hilo que los invoca** — en Blazor Server, eso es el hilo del circuito. ClosedXML en `ExportService.cs:32` ejecuta `worksheet.Columns().AdjustToContents()`, que mide el ancho del texto de *todas* las celdas (caro en O(filas × columnas)); QuestPDF (`GeneratePdf()` en `ExportService.cs:139` y `QuestPdfGenerator.cs:45/85/136`) es igual de intensivo. Mientras corre, el *render loop* del circuito está congelado: el usuario no puede clickear nada de su propia página. Después, `DescargaHelper.cs:18` hace `Convert.ToBase64String(bytes)` (＋33 % de tamaño) y lo manda como **un único argumento de JS interop** por el circuito SignalR — con Pallets Definitivos (28 columnas × miles de filas) eso es un mensaje de megabytes que viaja serializado.
- **Optimización**: mover la generación fuera del hilo del circuito y enviar el archivo como stream:

```csharp
// ExportService.cs — el trabajo pesado sale del hilo del circuito
public Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName)
    => Task.Run(() => { /* XLWorkbook + AdjustToContents acá */ });

// DescargaHelper.cs — stream en vez de Base64 intermedio
var streamRef = new DotNetStreamReference(new MemoryStream(bytes));
await js.InvokeVoidAsync("saveAsFromStream", fileName, streamRef, mimeType);
// js: const stream = await streamRef.value(); await navigator.saveFile... (o blazor.downloadFileFromStream)
```

### 2. [ALTO] La query más pesada de la app se ejecuta DOS veces en cada carga de Proyección de Zafra

- **Archivo**: `GuarinoApp.Web\Pages\ProyeccionZafra.razor:780` (`await Mediator.Send(new GetProyeccionZafraQuery(...))`) + `:894` (`GetImpactoClimaticoEnProyeccionQuery`), y `GuarinoApp.Application\Modules\ClimateRisk\Queries\GetImpactoClimaticoEnProyeccionQuery.cs:26` (internamente vuelve a enviar `GetProyeccionZafraQuery`).
- **Por qué es costoso**: `GetProyeccionZafraQueryHandler` no es una query: son **4 a 6 round-trips** — histórico por año (`GetProyeccionZafraQueryHandler.cs:38-43`), volcados por mes (`:64-68`), kg/bin, flota (`:328-331`), rendimiento de línea con `GroupBy(FechaIngreso.Date)` sobre toda la historia (`:351-356`). Todo eso se paga **dos veces** por carga de página (y otra vez si el usuario mueve el slider de %), porque el resultado de la segunda llamada solo necesita `proyectada.TotalKgs`, que la página ya tiene en `_proyectadaRef`.
- **Optimización**: pasarle el resultado ya calculado en vez de recomputarlo:

```csharp
// GetImpactoClimaticoEnProyeccionQuery.cs
public record GetImpactoClimaticoEnProyeccionQuery(List<ZafraDto>? ZafrasPreCalculadas = null)
    : IRequest<ImpactoClimaticoProyeccionDto?>;

// Handler:
var zafras = request.ZafrasPreCalculadas
    ?? await _mediator.Send(new GetProyeccionZafraQuery(), cancellationToken);
```

```csharp
// ProyeccionZafra.razor:894
_impactoClimatico = await Mediator.Send(new GetImpactoClimaticoEnProyeccionQuery(_zafras));
```

### 3. [ALTO] Contactos: la misma consulta completa se ejecuta dos veces por carga

- **Archivo**: `GuarinoApp.Web\Pages\Contactos.razor:165-166`.
- **Por qué es costoso**: `_todos = await Mediator.Send(new GetContactosQuery());` y luego `_data = await Mediator.Send(new GetContactosQuery(_busqueda, _estado));`. Sin filtros (que es el caso inicial), **ambas devuelven la tabla entera** con 13 columnas proyectadas (`GetContactosQueryHandler.cs:54-72`): doble round-trip, doble materialización, doble lista en memoria del circuito. Además cada `Send` atraviesa el `InputSanitizationBehavior` y la puerta de serialización (ver hallazgo 6).
- **Optimización**: una sola consulta y derivar las dos vistas:

```csharp
_todos = await Mediator.Send(new GetContactosQuery());           // fuente única
_data = AplicarFiltroEnMemoria(_todos, _busqueda, _estado);      // Where() sobre DTOs, barato
```

### 4. [ALTO] Queries de listados grandes sin `Take`/paginación en servidor, con predicados no sargables

- **`GuarinoApp.Application\Modules\StockPlaya\Queries\GetStockPlaya\GetStockPlayaQueryHandler.cs:39-66`** — materializa **toda** la tabla `LotesEmboquille` en estado "E" con proyección de 15 campos. Sin `Take` ni server-side paging; la página (`StockPlaya.razor:90`) lo manda todo a memoria y arma gráficos con `GroupBy` en cliente (`:107-127`). Con 3 zafras de volcados esto crece linealmente.
- **`GuarinoApp.Application\Modules\Stock\Queries\GetPallets\GetPalletsQueryHandler.cs:62`** — `await query.ToListAsync(...)` carga **entidades completas** (todas las columnas de `Pallets`) y recién ahí mapea a DTO en memoria (`:71-80`). Es la tabla que alimenta Stock General; el filtrado por Zafra/Estado podría quedarse en SQL y el mapa hacerse con `Select`.
- **`GuarinoApp.Application\Modules\RegistroPesadas\Queries\GetRegistroPesadas\GetRegistroPesadasQueryHandler.cs:35-36, 49`** — `le.FechaProduccion.Date >= request.FechaDesde.Value.Date` traduce a `CONVERT(date, [FechaProduccion]) >= @p`: **no puede usar un índice** sobre `FechaProduccion` (scan completo), y el resultado va sin límite a la tabla (aunque MudTable pagine en cliente, la lista completa vive en el circuito).
- **`GuarinoApp.Application\Modules\PalletsDefinitivos\Queries\GetPalletsDefinitivos\GetPalletsDefinitivosQueryHandler.cs:39-75`** — 28 columnas proyectadas, rango de 30 días por defecto (`PalletsDefinitivos.razor:159-160`) pero **sin techo de filas**; abrir el rango a un año materializa decenas de miles de filas, y luego el PDF de esas filas cae en el hallazgo 1.
- **Optimización** (patrón ya usado y probado en `GetLogsQuery.cs:60-74`):

```csharp
// rango sargable + límite explícito
var desde = request.FechaDesde?.Date;                 // parámetro ya normalizado
var hastaExclusivo = request.FechaHasta?.Date.AddDays(1);
var query = _context.LotesEmboquille.AsNoTracking()
    .Where(le => le.Estado == "E"
        && (desde == null || le.FechaProduccion >= desde)          // sin .Date en la COLUMNA
        && (hastaExclusivo == null || le.FechaProduccion < hastaExclusivo));
```

### 5. [MEDIO] `GetIndicadoresGerenciaQueryHandler`: 7 consultas secuenciales + filtro por `.Year` no sargable

- **Archivo**: `GuarinoApp.Application\Modules\Gerencia\Queries\GetIndicadoresGerencia\GetIndicadoresGerenciaQueryHandler.cs:22-131` (siete `await` seguidos: total volcados, zafra actual, total pallets, despachados, en frío, stock playa, ritmos).
- **Por qué es costoso**: 7 round-trips a LocalDB en serie en cada entrada a Gerencia (y Gerencia después llama además a `GetProyeccionZafraQuery` y `GetFinancialDataQuery`, `Gerencia.razor:157,164`). Cada ida y vuelta suma latencia que se paga siempre en cascada. En `:43`, `le.FechaProduccion.Year == anioActual` obliga a `DATEPART(year, …)` sobre toda la tabla.
- **Optimización respetando lo ya migrado a agregados SQL**: al menos arreglar el predicado y consolidar los conteos de Pallets en una sola query con agregación condicional:

```csharp
// rango sargable en vez de .Year
var inicio = new DateTime(anioActual, 1, 1);
var fin = inicio.AddYears(1);
query.Where(le => le.FechaProduccion >= inicio && le.FechaProduccion < fin && !le.Lote.IsDeleted);

// los 3 indicadores de Pallets en UNA query:
var pallets = await _context.Pallets.AsNoTracking()
    .Where(p => !p.IsDeleted)
    .GroupBy(p => 1)
    .Select(g => new {
        Total = g.Count(),
        Despachados = g.Count(p => p.Estado == EstadoPallet.Despachado),
        EnFrio = g.Count(p => p.Estado == EstadoPallet.EnFrio)
    }).FirstOrDefaultAsync(ct);
```

### 6. [MEDIO] `PuertaSerializacionDbContext` serializa *todos* los requests MediatR del circuito

- **Archivo**: `GuarinoApp.Application\Common\Behaviors\PuertaSerializacionDbContext.cs:30-58` + `SerializacionDbContextBehavior.cs:30-36`.
- **Por qué es costoso**: un `SemaphoreSlim(1,1)` por circuito encola **toda** la actividad de datos. Es una solución válida al problema real "A second operation was started on this context instance" (documentado en el propio archivo), pero el efecto colateral es que **una query pesada en vuelo bloquea la carga de la siguiente página** tras una navegación (el handler nuevo espera en `_sem.WaitAsync()` sin timeout). El código ya prueba la salida correcta en otros lados: `DashboardDataService.cs:22` y `OpenMeteoClimateService.cs:109` usan `IServiceScopeFactory` + scope propio por operación.
- **Optimización**: migrar los handlers a `IDbContextFactory<ApplicationDbContext>` (scope/contexto propio por operación) y dejar la puerta solo para los pocos handlers que usan el contexto escopado del circuito; al menos agregar timeout para que una espera larga falle visible en vez de congelar la navegación:

```csharp
if (!await _sem.WaitAsync(TimeSpan.FromSeconds(15)))
    throw new TimeoutException("Otra operación de datos sigue en curso en este circuito.");
```

### 7. [MEDIO] Dashboard: 8 awaits secuenciales y una consulta duplicada

- **Archivo**: `GuarinoApp.Web\Pages\Dashboard.razor:287-294, 354` — ocho `await` en fila (`GetLastStockDateAsync`, `GetTotalStockAsync`, `GetStockByVarietyAsync`, `GetMonthlyVolcadosAsync`, `GetPalletsWithoutClientAsync`, `GetProjectedGrowthAsync`, `GetVolcadosDetailAsync`, `GetProyeccionZafraQuery`).
- **Por qué es costoso**: `DashboardDataService` (la implementación registrada, `DependencyInjection.cs:44`) **crea un scope y un DbContext propio por método** (`DashboardDataService.cs:22,42,71`), así que no hay impedimento técnico para paralelizar — hoy, en cambio, se suman 8 latencias. Peor: `GetTotalStockAsync` (`:66-67`) internamente llama otra vez a `GetStockByVarietyAsync`, y la página **también** lo llama en la línea 289 — la misma query sobre `StocksVariedad` corre dos veces por carga (línea 25 de `DashboardDataService` incluye un `x.Fecha.Year == … && x.Fecha.Month == …` no sargable, igual que en `EfDashboardDataService.cs:25`).
- **Optimización**:

```csharp
var fechaTarea = DashboardService.GetLastStockDateAsync(fechaHasta);
var stockTarea = DashboardService.GetStockByVarietyAsync(fechaHasta);   // ← fuente única del stock
var volcTarea  = DashboardService.GetMonthlyVolcadosAsync(fechaDesde, fechaHasta);
var palTarea   = DashboardService.GetPalletsWithoutClientAsync(fechaHasta);
var crecTarea  = DashboardService.GetProjectedGrowthAsync(fechaDesde, fechaHasta);
var detTarea   = DashboardService.GetVolcadosDetailAsync(fechaDesde, fechaHasta, 20);
await Task.WhenAll(fechaTarea, stockTarea, volcTarea, palTarea, crecTarea, detTarea);

var stockPorVariedad = await stockTarea;
var totalStock = stockPorVariedad.Values.Sum();   // elimina el segundo GetAll de stock
```

### 8. [MEDIO] Cada mensaje de chat IA dispara 4 queries secuenciales + escaneo de disco para el prompt

- **Archivo**: `GuarinoApp.Infrastructure\Services\OllamaAIService.cs:433-437` — `GetTotalStockAsync`, `GetStockByVarietyAsync`, `GetProjectedGrowthAsync`, `GetPalletsWithoutClientAsync` en serie, cada uno abriendo scope+DbContext propio (`DashboardDataService`); `GetTotalStockAsync` **vuelve a consultar stock por variedad** (duplicado igual que el hallazgo 7, acá por cada mensaje del chat).
- **Archivo**: `GuarinoApp.Infrastructure\Services\PromptProvider.cs:37-49` — `GetPromptAsync` hace `Directory.EnumerateFiles` + comparación case-insensitive **y lee el archivo de disco en cada llamada**, sin caché (está `AddMemoryCache()` disponible desde `Program.cs:53`). Cada turno de chat y cada sugerencia IA pagan ese I/O.
- **Optimización**:

```csharp
// OllamaAIService.ChatAsync: paralelizar (scopes independientes) y reutilizar
var stockTarea = _dashboardService.GetStockByVarietyAsync(fecha);
var crecTarea  = _dashboardService.GetProjectedGrowthAsync(fecha);
var palTarea   = _dashboardService.GetPalletsWithoutClientAsync(fecha);
await Task.WhenAll(stockTarea, crecTarea, palTarea);
var stockVariedad = await stockTarea;
var totalStock = stockVariedad.Values.Sum();   // sin query extra

// PromptProvider: cachear plantillas (IMemoryCache ya registrado)
var file = _cache.GetOrCreate("prompts:" + key, e => {
    e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
    return Directory.EnumerateFiles(_promptsPath).FirstOrDefault(...);
});
return await File.ReadAllTextAsync(file!);
```

### 9. [MEDIO] Doce páginas renderizan sus tablas con reflexión en cada render

- **Archivo**: `GuarinoApp.Web\Pages\RegistroPesadas.razor:34-44` (idéntico patrón en `Basculas.razor:28-36`, `CamionesZorras.razor:28-36`, `Transportistas.razor:28-36`, `Lugares.razor`, `LugaresOrigen.razor`, `LugaresDestino.razor`, `LotesVolcados.razor:40-48`, `PalletsSinCliente.razor:27-35`, `LiquidacionesQuinta.razor:54-62`, `ResumenLiquidaciones.razor:53-61`).
- **Por qué es costoso**: `typeof(T).GetProperties()` se evalúa **en cada render del markup** (devuelve un arreglo nuevo de `PropertyInfo`) y `prop.GetValue(context)` corre por reflexión **por celda, por fila, por render**. MudTable pagina en cliente, así que son 25-100 filas × N columnas de reflection en cada `StateHasChanged` (filtros, spinners, snackbars). En Blazor Server ese CPU se paga en el servidor, en el hilo del circuito.
- **Optimización** (mínimo cambio, mismo resultado):

```razor
@code {
    // una sola vez por tipo, no por render
    private static readonly System.Reflection.PropertyInfo[] Columnas =
        typeof(RegistroPesadaDto).GetProperties();
}
@foreach (var prop in Columnas) { <MudTh>@prop.Name</MudTh> }
@foreach (var prop in Columnas) { <MudTd>@prop.GetValue(context)</MudTd> }
```

(Ideal: getters compilados con `Expression.Lambda` cacheados por tipo, o columnas explícitas en las tablas chicas.)

### 10. [BAJO] `IAChat` repinta el circuito cada 5 segundos sin cambiar nada

- **Archivo**: `GuarinoApp.Web\Pages\IAChat.razor:483-494` — `new System.Threading.Timer(_ => _ = InvokeAsync(StateHasChanged), null, 5 s, 5 s)`.
- **Por qué es costoso**: por cada pestaña de chat abierta se fuerza un ciclo de render y un mensaje de batch por SignalR cada 5 s aunque el chip de estado no haya cambiado (comentario propio en `:479-482` admite que es solo un diff). Con varios usuarios es tráfico constante e inútil.
- **Optimización**: repintar solo cuando el estado observado cambie y bajar la frecuencia:

```csharp
private bool _ultimoEstadoIa;
_refrescoEstadoIa = new System.Threading.Timer(_ =>
{
    var actual = AiService.IsAvailable;
    if (actual != _ultimoEstadoIa)
    {
        _ultimoEstadoIa = actual;
        _ = InvokeAsync(StateHasChanged);
    }
}, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
```

### 11. [BAJO] `ProyeccionZafra`: fire-and-forget de `LoadData` sin guarda en el slider de %

- **Archivo**: `GuarinoApp.Web\Pages\ProyeccionZafra.razor:742-745` (`private void OnPorcentajeCambio() => _ = LoadData();`), enlazado a `@bind-Value:after` en `:666-667`.
- **Por qué es costoso**: cada cambio del picker dispara una `LoadData` completa (query de zafra de 4-6 round-trips + financieros + impacto climático + stock) **sin esperar a la anterior** y **sobre el mismo DbContext escopado del circuito** — dos cargas solapadas chocan con la puerta del hallazgo 6 (una espera a la otra) y pueden cruzar el pintado final. Además es la ruta que más se repite del hallazgo 2.
- **Optimización**:

```csharp
private CancellationTokenSource? _ctsCarga;
private void OnPorcentajeCambio()
{
    _ctsCarga?.Cancel();
    _ctsCarga = new CancellationTokenSource();
    _ = LoadData(_ctsCarga.Token);   // y chequear token / descartar si otro LoadData arrancó
}
```

### 12. [BAJO] `Include`s muertos en `GetStockPlayaQueryHandler` y en `GetLotesVolcadosQueryHandler`

- **Archivo**: `GetStockPlayaQueryHandler.cs:27-33` (6 `Include`/`ThenInclude`) y `GetLotesVolcadosQueryHandler.cs:30-31`.
- **Por qué es costoso**: la query termina en `.Select(proyección)` (línea 39 y 43 respectivamente), y EF Core **ignora los `Include` cuando hay proyección final**. No generan costo de ejecución, pero engañan al lector (parece que hay 6 joins extra) y esconde que los joins reales los pide la proyección. Además en `GetLotesVolcadosQueryHandler.cs:33-34` el `.Date` en la columna vuelve no sargable el filtro de fechas (mecánica del hallazgo 4).
- **Optimización**: borrar los `Include` y normalizar fechas a rangos (snippet del hallazgo 4).

### 13. [BAJO] Construcción de `Regex` por iteración en el skill de stock

- **Archivo**: `GuarinoApp.Infrastructure\Services\Skills\StockPlantaSkill.cs:86-91` — `Regex.IsMatch(mensaje, $@"\b{Regex.Escape(clave)}\b", IgnoreCase)` compilando un regex nuevo por variedad conocida en **cada** mensaje de chat.
- **Optimización**: sin regex — `mensaje.Contains(clave, OrdinalIgnoreCase)` sobre los tokens normalizados, o cachear el `Regex` por clave en un `Dictionary<string, Regex>` con `RegexOptions.Compiled`.

### 14. [BAJO] Código muerto y detalles menores

- `GuarinoApp.Infrastructure\Services\EfDashboardDataService.cs` completo: **no está registrado en ningún lado** (`DependencyInjection.cs:44` registra `DashboardDataService`); duplica la lógica (incluido el `Fecha.Year/Month` no sargable de la línea 25) y confunde al que busque cuál corre.
- `GuarinoApp.Web\Pages\Auditoria.razor:211` y `:240`: `StateHasChanged()` al final de `ServerReload` — MudTable ya repinta al recibir el `TableData`, el call extra provoca un segundo render por cada cambio de página.
- `GuarinoApp.Infrastructure\Services\AiResultCache.cs:81-90`: la evicción hace `_datos.OrderBy(...)` sobre todo el diccionario en cada `Set` cuando está lleno; con `MaxEntradas = 256` es insignificante, pero si sube el techo conviene un `PriorityQueue` o barrido periódico.
- `GetContactosQueryHandler.cs:27-28` y `:45-52`: el filtro por `Estado` se aplica dos veces (segundo con allowlist). No cuesta nada en SQL, pero el primero anula la intención del segundo — conviene dejar solo el validado.

---

## Lo que está bien (y no hay que tocar)

1. **`AsNoTracking` + proyección a DTO en el camino de lectura**: la gran mayoría de los handlers (`GetContactosQueryHandler.cs:25`, `GetPalletsDefinitivosQueryHandler.cs:40`, `GetRegistroPesadasQueryHandler.cs:30-33`, `GetLugares/GetBasculas/GetTransportistas/GetCamionesZorras/...`) leen sin tracking y con `Select`, que es exactamente lo que evita el overhead del change tracker y reducir el payload.
2. **Paginación real en servidor en Auditoría**: `GetLogsQuery.cs:59-74` (`CountAsync` + `Skip/Take`) y el comentario de `:29-38` sobre el `ORDER BY Id DESC` que evita el Sort con memory grant (RESOURCE_SEMAPHORE de 25 s) es un diagnóstico y una solución ejemplares — el mismo patrón es el que recomiendo en el hallazgo 4.
3. **`GetClimateRiskSummaryQuery.cs:55-130`**: agregados scalar en SQL, Top N seleccionado por índice de fecha y orden por severidad en memoria sobre ≤500 filas. Está hecho pensando en el plan de SQL Server Express y **respetado en estas recomendaciones** (no pido volver a ordenar en SQL).
4. **`ImportarContactosCommandHandler.cs:43-119`**: una única lectura de la tabla + `HashSet` para deduplicar en O(1) + un solo `SaveChanges` por lote (límite 300) — opuesto al N+1 clásico de `SaveChanges` por fila.
5. **Escaneo climático**: `OpenMeteoClimateService.cs:104-208` con scope propio, `SemaphoreSlim` de concurrencia, deduplicación de eventos contra BD, timeout global y `CalcularKilosPerdidosAsync` con `ToDictionaryAsync` — bien diseñado y sin cuellos de botella evidentes.
6. **Patrón de "IA en segundo plano"**: `Dashboard.razor:359, 464-474`, `Gerencia.razor:177, 196-251`, `ProyeccionZafra.razor:887` y `Contaduria.razor:516` usan token correlativo (`_correlativoIA`), `InvokeAsync(StateHasChanged)` y capturan `ObjectDisposedException` — correcto y sin fugas hacia circuitos muertos.
7. **Sin sync-over-async clásico**: no encontré `.Result`, `.Wait()`, `GetAwaiter().GetResult()` ni `Task.Run` mal usados; los `dialogRef.Result` de `Usuarios.razor:307`, `Dashboard.razor:553` y `Contactos.razor:257/286/354` son el `Task` del diálogo de MudBlazor, correctamente `await`-eados. Los `async` sin await no excesivos (p. ej. `Auditoria.razor:155-163`) devuelven `Task.CompletedTask` a propósito.
8. **Autenticación**: `CustomAuthenticationStateProvider.cs:59-72` con *single-flight* (`_pendingState`) y cacheo en `ICurrentUserState`, y revalidación en scope propio (`:196-200`) — evita que los múltiples componentes que piden estado al arrancar el circuito disparen consultas concurrentes.
9. **Infra HTTP**: clientes con nombre y pooling (`Program.cs:97-116`), Polly con retry + circuit breaker en OpenMeteo (`OpenMeteoClimateService.cs:49-81`) y caché de resultados IA con TTL (`AiResultCache`) que evita repetir llamadas caras en picos de usuarios.

---

## Top 5 optimizaciones con mayor impacto

1. **Sacar la generación de archivos del hilo del circuito y enviarlos por stream** (hallazgo 1 — `ExportService.cs`, `QuestPdfGenerator.cs`, `DescargaHelper.cs`): elimina los peores congelamientos del circuito (segundos de CPU en el render loop) y reduce el tráfico SignalR un 33 % al quitar Base64. Es lo primero que notará el usuario en Pallets Definitivos y Registro de Pesadas.
2. **Eliminar la doble ejecución de `GetProyeccionZafraQuery`** (hallazgo 2 — `ProyeccionZafra.razor:780/894` + `GetImpactoClimaticoEnProyeccionQuery.cs:26`): mitad del costo de la carga de la pantalla más pesada de la app, con un cambio de firma de un command/query.
3. **Contactos: una sola consulta en vez de dos idénticas** (hallazgo 3 — `Contactos.razor:165-166`): doble round-trip + doble materialización eliminados en la pantalla comercial, cambio de 3 líneas.
4. **Límites y rangos sargables en las queries de listados grandes** (hallazgo 4 — `GetStockPlayaQueryHandler.cs:39-66`, `GetPalletsQueryHandler.cs:62`, `GetRegistroPesadasQueryHandler.cs:35-36`, `GetPalletsDefinitivosQueryHandler.cs:39-75`): copiar el patrón ya probado de `GetLogsQuery` (`.Date` → rango `[desde, hasta)` y `Take`/paging server-side) ataca el consumo de memoria y los timeouts a medida que la tabla crece con cada zafra.
5. **Paralelizar donde hay scopes propios, y desdoblar queries duplicadas** (hallazgos 7 y 8 — `Dashboard.razor:287-294` y `OllamaAIService.cs:433-437`): `Task.WhenAll` sobre los métodos de `DashboardDataService` (que ya usan `IServiceScopeFactory`) recorta ~60-70 % de la latencia de entrada al Dashboard y de cada mensaje del chat, y el `totalStock` derivado de `stockPorVariedad` elimina una query completa en ambos flujos.
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