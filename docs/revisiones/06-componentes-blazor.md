# Revisión de componentes Blazor — GuarinoApp.Web

**Fecha:** 2026-10-20
**Alcance:** Los 47 archivos `.razor` de `GuarinoApp.Web/Pages/` y `GuarinoApp.Web/Shared/` (+ `App.razor`, `_Imports.razor`), ~9.000 líneas. Revisión de claridad del código, manejo del estado, eficiencia de renderizado, uso de servicios/DI y buenas prácticas. Solo lectura: no se modificó ningún archivo, no se ejecutaron builds ni se tocó git o la base de datos.

---

## (A) Evaluación por componente

### Páginas (`GuarinoApp.Web/Pages/`)

| Archivo | Nota | Hallazgos breves |
|---|---|---|
| `Dashboard.razor` (528 l.) | **8** | Muy buen patrón: KPIs + gráficos pintados YA, IA en background con `InvokeAsync(StateHasChanged)` y catch de `ObjectDisposedException` (L464-474). Mejorar: `Console.WriteLine` en L365 pese a tener `Logger` inyectado; lambdas `@(e => ExportToExcel())` con parámetro inútil (L39-41); 7 `await` secuenciales de servicio (L287-294, justificados por DbContext escopado pero unificables en un DTO agregado). |
| `ProyeccionZafra.razor` (1047 l.) | **7** | Arquitectura de datos excelente (cálculo local determinista + refinamiento IA con token anti-carrera, L981-1064). Problemas: 1047 líneas en un solo archivo (markup + lógica + estilos de impresión); `OnPorcentajeCambio` lanza `_ = LoadData()` **por cada tecla** (L742-745, `Immediate="true"`) sin guard de concurrencia; reset manual de ~15 campos (L757-775); LINQ (`OrderBy`, `GroupBy`) dentro del markup que re-ejecuta por render (L449, L594-600). |
| `GestionRiesgosClimaticos.razor` (530 l.) | **8,5** | Ejemplo correcto de interop JS: import de módulo en `OnAfterRenderAsync(firstRender)` con cierre de carrera (L432-454) e `IAsyncDisposable` que libera el módulo (L557-564). Filtros vía input nativo `@bind:event=oninput` bien razonado. Mejorar: inyección por `[Inject]` dentro de `@code` (L426-428) en vez de `@inject` (inconsistente); dos tablas casi idénticas (L239-285 vs L306-348) → componente parametrizado. |
| `IAChat.razor` (641 l.) | **7,5** | Nota de arquitectura documentada (L388-430), auditoría de consultas, `IDisposable` para el timer. Mejorar: timer de 5 s que refresca el chip aunque nadie mira (L490-494) — el callback en vuelo puede llamar `InvokeAsync` tras `Dispose`; `GenerarSugerencias()` (4 queries + llamada IA) se re-ejecuta después de **cada** mensaje (L566); los toggles `_enabledExtras` (L349) nunca viajan al request (UI muerta); sin auto-scroll del historial (L161, `max-height:400px`). |
| `Contaduria.razor` (566 l.) | **8** | Muy bien: validaciones antes de guardar (L423-447), KPIs con la misma calculadora que el tablero, análisis local + IA background. Menores: `_analizando = false` sin chequear token en `finally` (L582) — una tarea vieja puede apagar el spinner de una nueva; `LimpiarFormulario` pone 0 en campos mientras el tooltip dice "deja los campos en blanco" (L164 vs L604-612). |
| `Gerencia.razor` (272 l.) | **8** | Mismo patrón híbrido local/IA bien ejecutado; `VerPrioridad` con diálogo explicativo. Mismo menor de `_analizando` (L238) y `Console.WriteLine` en el catch externo (L181) con `Logger` disponible. |
| `ProyeccionVolcados.razor` (276 l.) | **7** | Gráficos con `@key` para forzar remount (L35, bueno). Problemas: el catch (L200-203) solo hace `Console.WriteLine` y la UI muestra "No se encontraron datos" (L160) **mintiendo en un error real**; navega a `/iachat?mensaje=...` con prompts potencialmente largos (L300) — riesgo de URL > límite. |
| `Contactos.razor` (339 l.) | **8,5** | CRUD impecable: comandos MediatR con resultado `Success/Error`, diálogos, sanitización, filtro sin acentos propio (L242-252). Mejorar: **dos consultas por carga** (`_todos` + `_data`, L165-166) cuando el filtrado podría hacerse en cliente sobre `_todos`; doble escritura de `_busqueda` (`@bind-Value` + `TextChanged`, L37-38). |
| `RegistroPesadas.razor` (167 l.) | **6,5** | Tabla por reflexión `GetProperties()` en cada celda y render (L33-45): sin formato de fechas/decimales y nombres crudos; error de carga tragado (L95-98) → "No se encontraron registros" en vez de avisar; bloques de exportación copiados de otras páginas. |
| `LotesVolcados.razor` (173 l.) | **6,5** | Clon de RegistroPesadas (misma reflexión L39-51, mismo catch de consola L100-103). |
| `ResumenLiquidaciones.razor` (190 l.) | **6,5** | Mismo clon (reflexión L52-64, catch consola). |
| `LiquidacionesQuinta.razor` (191 l.) | **6,5** | Mismo clon (reflexión L53-65). |
| `StockGeneral.razor` (202 l.) | **7** | Carga con comentarios útiles. Mejorar: doble gestión de flags — `OnInitializedAsync` (L112-127) repite lo que ya hizo el `finally` de `LoadData`; `_chartOptions` declarado y nunca usado (L103). |
| `StockPlaya.razor` (124 l.) | **5,5** | **Bug funcional:** los datepickers (L15-18) disparan `LoadData` pero la query ignora las fechas: `GetStockPlayaQuery(new List<Filter>())` (L90). Además artifact `` `n` `` visible en la UI (L66). |
| `FrutaVolcar.razor` (237 l.) | **6,5** | Mismo bug: `_fechaDesde/_fechaHasta` vinculados al toolbar pero la query recibe `new List<Filter>()` vacío (L118). Exportaciones copiadas. |
| `ResumenMensual.razor` (91 l.) | **6** | Las fechas SÍ se pasan (L78), pero artifact `` `n` `` en L57 y `ClearFilters` no recarga (L92-97). |
| `PalletsContramarca.razor` (71 l.) | **6,5** | Simple y correcto salvo artifact `` `n` `` (L46) y catch de consola. |
| `PalletsDefinitivos.razor` (394 l.) | **7** | Buena UI y exports con columnas acotadas para PDF. **Código muerto**: `SendPalletEmail` (L407-461) con `"destinatario@example.com"` (L445), `StatusMessage/CreatedPdfPath` (L165-167) y bloque de render L138-148; inyecta `GuarinoApp.Infrastructure.Services.IPdfGenerator` en una página (L14, fuga de capa); el CSV manual (L240-259) no escapa `;`. |
| `PalletsSinCliente.razor` (72 l.) | **6,5** | Sin `@attribute [Authorize(Roles=...)]` — solo el gate genérico del layout (inconsistente con el resto); reflexión en tablas. |
| `Basculas.razor` (73 l.) | **6** | Parte del grupo de 6 clones idénticos (verificado por diff: solo cambian DTO/query/título). Reflexión por celda, `Console.WriteLine`, mismo estado. |
| `CamionesZorras.razor` (73 l.) | **6** | Clon exacto de Basculas (`CamionZorraDto` / `GetCamionesZorrasQuery`). |
| `Lugares.razor` (73 l.) | **6** | Clon exacto (`LugarDto` / `GetLugaresQuery`). |
| `LugaresOrigen.razor` (73 l.) | **6** | Clon exacto (`LugaresOrigenDto` / `GetLugaresOrigenQuery`). |
| `LugaresDestino.razor` (73 l.) | **6** | Clon exacto (`LugaresDestinoDto` / `GetLugaresDestinoQuery`). |
| `Transportistas.razor` (73 l.) | **6** | Clon exacto (`TransportistaDto` / `GetTransportistasQuery`). |
| `Empaque.razor` (109 l.) | **7** | Correcto; `_chartOptions` sin usar (L77); catch de consola. |
| `GraficaProcesos.razor` (192 l.) | **7** | Comentarios contradichos por el DTO: dice "Fecha es DateTime?" (L152) pero `GraficaProcesosDto.Fecha` es `DateTime` no anulable → los `?? ""` son muertos; 3 gráficos con las mismas series repetidas (L159-173). |
| `Auditoria.razor` (287 l.) | **9** | **La mejor página**: paginación server-side (`ServerData`), comentarios que documentan la carrera del DbContext escopado (L155-163), manejo de `ObjectDisposedException` (L218-229), un solo reintento automático (L241-250). |
| `Usuarios.razor` (342 l.) | **7,5** | UI clara con `AuthorizeView` por celda. Riesgo: `LoadAsync` y `CrearUsuarioAsync` sin `catch` (L273-284, L217-244) — una excepción tumba el circuito. |
| `Login.razor` (122 l.) | **8,5** | Sólido: validación de `returnUrl` anti-open-redirect (L119-141), `AllowRegistration` por settings, Enter también ingresa, estado de carga correcto. Nota security: JWT en `localStorage` (XSS). |
| `Register.razor` (124 l.) | **8,5** | Equivalente a Login con confirmación de contraseña y auto-login tras registro (L132-135). |
| `Index.razor` (123 l.) | **8** | Portal estático con `AuthorizeView` por acceso directo; correcto. |
| `Facturacion.razor` (4 l.) | **5** | Placeholder "en construcción" (aceptable, sin estado). |
| `Counter.razor` (12 l.) | **2** | Template de Blazor sin usar (solo routable en `/counter`, sin enlaces). |

### Compartidos (`GuarinoApp.Web/Shared/`)

| Archivo | Nota | Hallazgos breves |
|---|---|---|
| `MainLayout.razor` (65 l.) | **7,5** | Gate de autenticación en el layout con comentario correcto en `App.razor` de por qué no se duplica el layout. **Fuga de evento**: suscribe `AuthenticationStateChanged` (L41) y nunca se desuscribe (sin `IDisposable`). |
| `NavMenu.razor` (98 l.) | **8,5** | Menú claro, agrupado y con roles anidados bien usados (`Context=` nombrado). Inyecta `AuthService` (L4) **sin usarlo**. |
| `CurrentUserDisplay.razor` (76 l.) | **8** | Bien resuelto con `IAuthenticationStateNotifier`. Misma fuga: suscribe sin desuscribir (L51). |
| `ContactoDialog.razor` (281 l.) | **9** | Ejemplar: validación por campo + re-validación al guardar, sanitización antes de validar (L251-257), E.164 y canonicalización de países, modelo de resultado tipado. |
| `ContactoImportDialog.razor` (198 l.) | **9** | Límite de 2 MB con `OpenReadStream` (L126-133), logging, previsualización, resultado detallado, previene doble import (L191-193). |
| `ExportToolbar.razor` (110 l.) | **8** | Buen componente con EventCallbacks y fallback de impresión. Los consumidores lo usan de 2 formas distintas (`@bind-FechaDesde` vs `Value`+`Changed`). |
| `InteractiveChart.razor` (68 l.) | **7** | Envuelve ApexCharts con soporte de NaN (bien). `_options` es propiedad que crea `new` **en cada acceso** (L29-36) y `_points` se reconstruye en cada `OnParametersSet` (L38-68) aunque los parámetros no cambien; series con Name repetido haría fallar `ToDictionary` (L58). |
| `DataSourceFooter.razor` (55 l.) | **8** | Bueno, pero el default `FechaActualizacion = DateTime.Now` (L54) hace que varias páginas muestren "última actualización = ahora" sin ser el timestamp real del dato. |
| `SendEmailDialog.razor` (58 l.) | **8,5** | Correcto; validación de email con `MailAddress` duplicada en 3 diálogos. |
| `EditUserEmailDialog.razor` (63 l.) | **8,5** | Correcto; misma validación duplicada; permite vacío para quitar el correo. |
| `SimpleDialog.razor` (18 l.) | **8,5** | Mínimo y correcto. |
| `SurveyPrompt.razor` (14 l.) | **1** | Basura del template, sin referencias en todo el proyecto → eliminar. |
| `App.razor` (30 l.) | **9** | `AuthorizeRouteView` bien configurado con comentarios que explican por qué no se envuelve otro `LayoutView` (evita "Duplicate MudPopoverProvider"). |
| `_Imports.razor` (19 l.) | **8** | Adecuado; incluye namespaces de módulos específicos (`GetPallets`, `PalletsDefinitivos`) que filtrarían mejor por página. |

---

## (B) Análisis extendido de los 10 principales

### 1. `Pages/ProyeccionZafra.razor`

**1.1 — [ALTA] Recarga completa por cada tecla del campo %, sin guard de concurrencia.** L665-677 + L742-745: `Immediate="true"` + `@bind-Value:after="OnPorcentajeCambio"` dispara `_ = LoadData()` en cada pulsación. Varios `LoadData` corren en paralelo sobre los mismos campos (`_zafras`, `_kpiFinanciero`, `_series`…), cada uno consulta la base y lanza su propia tarea de IA (solo las respuestas IA se invalidan con `_correlativoIA`; los datos y las queries, no). Se pueden apilar 10+ queries mientras el usuario tipea "12,5".

```csharp
private CancellationTokenSource? _reloadCts;

private async Task OnPorcentajeCambio()
{
    _reloadCts?.Cancel();
    _reloadCts = new CancellationTokenSource();
    var token = _reloadCts.Token;
    try
    {
        await Task.Delay(400, token);          // debounce
        await LoadData();
    }
    catch (OperationCanceledException) { }
}
```

**1.2 — [MEDIA] Reset manual de ~15 campos en `LoadData`** (L757-775): una omisión al agregar un campo nuevo deja datos de la corrida anterior en pantalla. Extraer a un método `LimpiarEstado()` o a un record con `with`.

**1.3 — [MEDIA] LINQ en el markup que se re-ejecuta en cada render**: `Items="@_zafras.OrderBy(z => z.Año)"` (L449) crea un enumerable nuevo por render; el `GroupBy` del plan de acción (L594-600) re-agrupa en cada pintado. Pre-calcular `_zafrasOrdenadas` y `_gruposEstrategia` tras la carga.

**1.4 — [BAJA] `CalcularEstilos()` se invoca dos veces por corrida** (dentro de `CalcularKPIsLocales` L928 y otra vez en L876). Inofensivo pero confuso.

**Lo bueno a destacar**: cálculo local determinista + IA solo como refinamiento de texto (L1007-1038), token `_correlativoIA` (L997), `HtmlEncode` de todo texto IA antes de meterlo en `MarkupString` (L1080-1087) — manejo de inyección correcto.

### 2. `Pages/Dashboard.razor`

**2.1 — [MEDIA] Mezcla `Console.WriteLine` con `ILogger`.** L365 loguea a consola pese a tener `Logger` inyectado (L17) y usarlo correctamente en L410/L487. En producción la consola de Blazor Server no es visible.

```csharp
catch (Exception ex)
{
    Logger.LogError(ex, "Error cargando el Dashboard periodo {Desde}-{Hasta}", fechaDesde, fechaHasta);
    Snackbar.Add("No se pudieron cargar los indicadores. Intente nuevamente.", Severity.Error);
}
```

**2.2 — [BAJA] Lambdas con parámetro fantasma en los EventCallback.** L39-41: `OnExportExcel="@(e => ExportToExcel())"` compila por la sobrecarga `MulticastDelegate` pero `e` no significa nada; en el resto de la app se pasan method groups (`OnExportPdf="ExportToPdf"`, RegistroPesadas L25-27). Unificar a method groups.

**2.3 — [BAJA] Alias innecesario**: `private DateTime? _selectedDate => _fechaHasta;` (L225) usado solo en el nombre del adjunto (L567); agrega una forma más de leer el mismo estado.

**2.4 — [BAJA] 7 consultas secuenciales** (L287-294). Es intencional y está comentado (DbContext único por circuito — correcto), pero un `DashboardSnapshotDto GetSnapshotAsync(desde, hasta)` en el servicio reduciría 7 round-trips de latencia a 1 y haría la página inmune a futuras llamadas paralelas.

### 3. `Pages/GestionRiesgosClimaticos.razor`

**3.1 — [OK verificado] Ciclo de vida JS correcto.** Import en `OnAfterRenderAsync(firstRender)` (L438), cierre de la carrera marcadores/módulo (L444-447), `setMarkers` al recargar (L465-468) y `DisposeAsync` que llama `dispose` del mapa y dispone el módulo (L557-564). Es el único componente con este nivel de cuidado.

**3.2 — [MEDIA] Duplicación de dos tablas casi idénticas** (pronóstico L239-285 vs recientes L306-348): mismas columnas, mismo pager, mismo buscador; solo cambia la fuente y el formato de fecha. Extraer `ClimaEventosTable` con parámetros `Items`, `Titulo`, `Filtro`/`FiltroChanged` y `FormatearFecha`.

**3.3 — [BAJA] Inyección por `[Inject]` dentro de `@code`** (L426-428) convive con el estilo `@inject` del resto del proyecto → mover arriba.

**3.4 — [BAJA] Filtrado LINQ en propiedad computada** (L411-415): con `@bind:event="oninput"`, cada tecla re-filtra hasta 500 eventos y re-renderiza la tabla. Aceptable hoy; si crece, cachear el resultado filtrado y recalcular solo al cambiar `_filtro*`.

### 4. `Pages/IAChat.razor`

**4.1 — [MEDIA] El timer de 5 s refresca la página entera aunque esté idle.** L490-494: `_ = InvokeAsync(StateHasChanged)` cada 5 s. Además, al hacer `Dispose()` (L507) un callback en vuelo puede invocar `InvokeAsync` sobre el componente ya dispuesto → excepción no observada. Mejor:

```csharp
_refrescoEstadoIa = new Timer(_ => _ = InvokeAsync(() =>
{
    if (_disposed) return;          // bandera seteada en Dispose()
    StateHasChanged();
}), null, 5s, 5s);
```

**4.2 — [MEDIA] Regenera sugerencias (4 queries al dashboard + llamada IA) después de cada mensaje** (L566). Con chats de 10 preguntas son 40 queries extra. Suficiente con regenerar al iniciar y con el botón "RECALCULAR".

**4.3 — [MEDIA] Panel "Más parámetros recomendados" es UI muerta**: los `_enabledExtras` (L349) nunca se leen al armar el `BusinessSuggestionRequest` (L604-615). O se conectan o se elimina el panel (confunde al usuario).

**4.4 — [BAJA] Sin auto-scroll** del historial (L161-186): al enviar un mensaje largo el usuario queda sin ver la respuesta. Agregar `@ref` + `ScrollToBottom` en `OnAfterRenderAsync` cuando `_messages` cambió.

### 5. `Pages/Contaduria.razor`

**5.1 — [MEDIA] Condición de carrera del spinner de IA.** L580-594: `_analizando = false` se pone **sin** chequear el token. Si el usuario guarda dos veces rápido, la tarea vieja termina después de que la nueva arrancó y apaga el indicador "Refinando con IA…" mientras la nueva sigue corriendo. Corrección mínima:

```csharp
finally
{
    if (token == _correlativoIA)
    {
        _analizando = false;
        try { await InvokeAsync(StateHasChanged); }
        catch (ObjectDisposedException) { }
    }
}
```
(Mismo fix en `Gerencia.razor` L236-250.)

**5.2 — [BAJA] Contradicción UI/código en "Limpiar"**: el tooltip promete "deja los campos en blanco" (L164) pero `LimpiarFormulario` (L604-612) pone 0/`null`, y los `MudNumericField` son `decimal` no anulables. Cambiar a `decimal?` o ajustar el texto.

**5.3 — [BAJA] Indentación rota en el bloque de validación** (L423-427, llave corrida) — legibilidad.

### 6. `Pages/ProyeccionVolcados.razor`

**6.1 — [MEDIA] El error se muestra como "sin datos".** L200-203: `catch { Console.WriteLine(...) }` y luego `_cargado = true` → la UI entra en `else if` (L158-161) y muestra *"No se encontraron datos"*. El usuario no sabe que falló. Agregar `_error` como en `GestionRiesgosClimaticos.razor` L91-97 (ese patrón está bien hecho y debería ser el estándar).

**6.2 — [MEDIA] Prompt completo por query string** (L300): `NavigateTo($"/iachat?mensaje={Uri.EscapeDataString(prompt)}")`. Con templates largos la URL supera límites de servidor/browser (~2000-8000 chars). Alternativa: almacenar el prompt en un servicio scoped/caché por circuito y pasar solo un id.

**6.3 — [BAJA] La confianza de la tabla es hardcodeada** (L105-106: siempre 80% o 100%) — "Confianza" no proviene del dato; mejor mostrar el valor real o quitar la columna.

### 7. `Pages/Contactos.razor`

**7.1 — [MEDIA] Dos consultas a la base por cada carga.** L165-166: `GetContactosQuery()` completo + `GetContactosQuery(_busqueda, _estado)` filtrado. Como `_todos` ya contiene todo, el filtrado puede hacerse en memoria (además ya existe `SinAcentos` para coincidencia tolerante):

```csharp
_todos = await Mediator.Send(new GetContactosQuery());
_data = _todos
    .Where(c => (string.IsNullOrWhiteSpace(_busqueda) || Coincide(c, _busqueda))
             && (string.IsNullOrWhiteSpace(_estado) || c.Estado == _estado))
    .ToList();
```
(O un solo query filtrado server-side y el autocomplete limitado a valores del filtro.)

**7.2 — [BAJA] Doble asignación de `_busqueda`**: `@bind-Value="_busqueda"` + `TextChanged="@((string? t) => _busqueda = t)"` (L37-38). Está comentado como workaround de MudBlazor; si se mantiene, dejar solo el comentario ya escrito como única fuente.

### 8. `Pages/RegistroPesadas.razor` (+ clones)

**8.1 — [MEDIA] Reflexión en cada celda, cada render.** L33-45: `typeof(RegistroPesadaDto).GetProperties()` se ejecuta 1× por celda de encabezado + N× por fila × columnas en **cada** render (tipear un filtro, cambiar de página, etc.), y muestra nombres crudos sin formato (fechas sin `dd/MM/yyyy`, decimales sin `N2`). Cacheo mínimo:

```csharp
private static readonly PropertyInfo[] Props = typeof(RegistroPesadaDto).GetProperties();
```
Pero la mejora real son columnas explícitas con `DataLabel`/formato (como hace `PalletsDefinitivos.razor` L60-123). Vale lo mismo para `LotesVolcados` L39-51, `ResumenLiquidaciones` L52-64, `LiquidacionesQuinta` L53-65, `PalletsSinCliente` L26-38 y las 6 páginas maestras.

**8.2 — [MEDIA] Error tragado → estado vacío engañoso** (L95-98, igual que en ~15 páginas). Ver Top-5 #3.

### 9. `Pages/StockPlaya.razor` / `StockGeneral.razor`

**9.1 — [ALTA] Filtros de fecha no funcionales en StockPlaya.** L15-18 los pickers disparan `LoadData` con `OnChange`, pero L90 ignora `_fechaDesde/_fechaHasta` y consulta con `new List<Filter>()`. El usuario cree que filtra y no lo hace:

```csharp
var filters = new List<Filter>();
if (_fechaDesde.HasValue) filters.Add(new Filter("FechaDesde", _fechaDesde.Value));
if (_fechaHasta.HasValue) filters.Add(new Filter("FechaHasta", _fechaHasta.Value));
_data = await Mediator.Send(new GetStockPlayaQuery(filters));
```
(Igual situación, aunque sin auto-load, en `FrutaVolcar.razor` L118.)

**9.2 — [BAJA] Doble gestión de estado de carga en StockGeneral**: `OnInitializedAsync` (L112-127) envuelve a `LoadData` que ya setea `_loading/_cargado` y ya llama `StateHasChanged` en su `finally` (L175-180). Simplificar a `OnInitializedAsync() => LoadData();`.

**9.3 — [MEDIA] Artefacto `` `n` `` visible en 3 páginas**: `StockPlaya.razor` L66, `ResumenMensual.razor` L57 y `PalletsContramarca.razor` L46 tienen `... />\`n\`</MudCardContent>` — se renderiza texto basura en pantalla (falla de search/replace).

### 10. `Shared/MainLayout.razor` + `CurrentUserDisplay.razor` + `Pages/PalletsDefinitivos.razor`

**10.1 — [MEDIA] Suscripción a `AuthenticationStateChanged` sin baja** — `MainLayout.razor` L41 y `CurrentUserDisplay.razor` L51: el layout persiste toda la vida del circuito y `CurrentUserDisplay` vive en el AppBar; aunque el alcance es por circuito, es el patrón clásico de fuga. Agregar:

```csharp
@implements IDisposable
...
public void Dispose() => AuthProvider.AuthenticationStateChanged -= OnAuthStateChanged;
```

**10.2 — [MEDIA] Código muerto con dirección de correo hardcodeada en PalletsDefinitivos**: `SendPalletEmail` (L407-461) con `"destinatario@example.com"` (L445), su bloque de render (L143-148 con enlace `file:///`), `StatusMessage/StatusClass/CreatedPdfPath` (L165-167, L138-141) y el botón comentado (L136). Nada de eso es alcanzable en la UI → borrar ~80 líneas.

**10.3 — [MEDIA] Fuga de capas**: L14 inyecta `GuarinoApp.Infrastructure.Services.IPdfGenerator` directamente en una página; el resto de la app llega a infra vía `Application.Common.Interfaces` (`IExportService`). Mover a una interfaz de Application.

**10.4 — [BAJA] CSV manual sin escaping** (L240-259): un `Cliente` con `;` rompe columnas; `ExportService.ExportToCsv` (usado para Excel/CSV del resto) ya maneja esto — reutilizarlo.

---

## (C) Lo que está bien (transversal)

1. **Patrón de IA en segundo plano, bien hecho y replicado**: contenido local determinista visible de inmediato + refinamiento IA con token anti-carrera (`_correlativoIA`) y `catch (ObjectDisposedException)` — `ProyeccionZafra.razor` L981-1064, `Contaduria.razor` L546-595, `Gerencia.razor` L196-251, `Dashboard.razor` L375-474. El usuario nunca espera mirando una pantalla vacía por la IA.
2. **Arquitectura limpia de UI**: las páginas no tocan EF directamente; todo va por `IMediator`/`MediatR` con DTOs tipados, y los comandos devuelven `Success/Error` que la UI traduce a Snackbar (ej. `Contactos.razor` L267-276).
3. **Documentación en el código orientada al lector humano**: notas de por qué se hacen las cosas (carrera del DbContext escopado en `Auditoria.razor` L155-163; por qué no se duplica el layout en `App.razor` L8-11; por qué NaN en series `ProyeccionZafra.razor` L815-819; arquitectura de IA en `IAChat.razor` L388-430).
4. **Seguridad y saneamiento en formularios**: `ContactoDialog` sanitiza antes de validar (L251-257), cierra países con lista canónica y normaliza teléfonos E.164; `ContactoImportDialog` acota el stream a 2 MB (L126-133); el texto de la IA se codifica con `HtmlEncode` antes de `MarkupString` (`ProyeccionZafra.razor` L1080-1087); `Login.GetReturnUrl` valida rutas locales (L133-140).
5. **Componentes compartidos que evitan duplicación real**: `ExportToolbar`, `DataSourceFooter`, `InteractiveChart`, `DescargaHelper`, `ReportMailRouter` y los diálogos reutilizables están bien dimensionados y con parámetros/`EventCallback` correctos.
6. **Buen manejo de `OnAfterRender`/`Dispose` en el único punto con JS real**: `GestionRiesgosClimaticos` importa módulo JS en `firstRender`, cierra carreras y dispone correctamente (L432-454, L557-564); `IAChat` dispone su timer.
7. **Autorización consistente en la mayoría de páginas**: `@attribute [Authorize(Roles=...)]` + `AuthorizeView` por celda para acciones sensibles (`Usuarios.razor` L140-149, L171-184).
8. **`Auditoria.razor` es el modelo a seguir** para tablas grandes: `ServerData` con Skip/Take, `MudTablePager` y reintento único ante carrera transitoria.

---

## (D) Top 5 mejoras transversales

**1. Eliminar la copia masiva: una base de página de reporte + una tabla reflexiva.**
Hay **6 clones idénticos** (Basculas/CamionesZorras/Lugares/LugaresOrigen/LugaresDestino/Transportistas) y **4 casi idénticos** con exportación (RegistroPesadas/LotesVolcados/ResumenLiquidaciones/LiquidacionesQuinta), más el mismo bloque `LoadData/EnsureDataForExport/ExportAsync/ExportarEmail` copiado en 8+ páginas (~60 líneas × 8 ≈ 500 líneas duplicadas). Propuesta: un `ReportPageBase<TItem, TResult>` o componente `ReporteSimple`:

```razor
@* Shared/ReporteSimple.razor *@
<MudTable T="TItem" Items="Items" Dense="true" Hover="true" Striped="true" Breakpoint="Breakpoint.Sm">
    <HeaderContent>@Header</HeaderContent>
    <RowTemplate>@RowTemplate(context)</RowTemplate>
    <PagerContent><MudTablePager PageSizeOptions="new int[]{10,25,50,100}" /></PagerContent>
</MudTable>
```
más un servicio `IReportExportService` que concentre `EnsureData + Export + Email` (una sola copia del switch de MIME types y del manejo de errores). Cada página quedará en ~30 líneas.

**2. Resolver los filtros ignorados y los artefactos `` `n` `` (calidad funcional visible).** `StockPlaya.razor` L90 y `FrutaVolcar.razor` L118 ignoran el rango de fechas elegido (el usuario cree que filtra); `` `n` `` se ve renderizado en `StockPlaya` L66, `ResumenMensual` L57 y `PalletsContramarca` L46. Son cambios de una línea con impacto inmediato.

**3. Patrón estándar de "error vs. vacío" + ILogger en todas las páginas.** Hoy 42 usos de `Console.WriteLine` (15+ páginas) y ~10 páginas muestran *"No se encontraron datos*" cuando la consulta falló. Adoptar el patrón ya probado en `GestionRiesgosClimaticos.razor` L91-97:

```csharp
catch (Exception ex)
{
    Logger.LogError(ex, "Error cargando {Pagina}", nameof(MiPagina));
    errorCarga = ex.Message;   // MudAlert de error en la UI
}
finally { cargado = true; ... }
```
con `ILogger<T>` inyectado (ya disponible en los proyectos, solo falta usarlo en las páginas chicas).

**4. Debounce/guard de recargas por interacción y arreglar las condiciones de carrera menores.** `ProyeccionZafra.OnPorcentajeCambio` (L742) recarga por tecla sin cancelación (ver 1.1); `Contaduria` L582 y `Gerencia` L238 apagan el spinner sin chequear token; `IAChat` regenera sugerencias completas por mensaje (L566). Un solo patrón de `CancellationTokenSource` + chequeo de token unifica los tres.

**5. Limpieza y consistencia de la capa compartida.**
- Borrar código muerto: `PalletsDefinitivos.SendPalletEmail` + estado asociado (~80 l.), `Counter.razor`, `SurveyPrompt.razor`, `_chartOptions` sin usar en `StockGeneral` L103 / `FrutaVolcar` L105 / `StockPlaya` L77 / `Empaque` L77, y el `AuthService` sin usar en `NavMenu.razor` L4.
- Desuscribir `AuthenticationStateChanged` en `MainLayout` y `CurrentUserDisplay` (`IDisposable`).
- `InteractiveChart`: cachear `_options`/`_points` (hoy se reconstruyen en cada render del padre, L29-68) y validar nombres de serie duplicados.
- Unificar validación de email (triplicada en 3 diálogos) y el uso de `ExportToolbar` (`@bind-*` vs `Value`+`Changed`).
- Agregar `[Authorize(Roles=...)]` a `PalletsSinCliente.razor` (hoy protegida solo por el gate genérico).

---

**Resumen ejecutivo**: la base es sólida — separación UI/servicios vía MediatR, patrón IA híbrido bien pensado, manejo serio de interop JS en el mapa y formularios con saneamiento cuidado. Los riesgos se concentran en (a) la **duplicación masiva** que multiplica el costo de cada fix futuro, (b) **filtros que no llegan a la query** y artefactos de render visibles, y (c) **errores tragados que se disfrazan de "sin datos"** en ~15 páginas. Atacando el Top-5 se reducirían unas 700-800 líneas duplicadas y se eliminarían los bugs funcionales más confusos para el usuario final.

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