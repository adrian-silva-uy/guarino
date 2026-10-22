# 🎨 Componentes y Servicios Blazor - GuarinoApp.Web

## Índice
1. [Estructura de Componentes](#estructura-de-componentes)
2. [Componentes Compartidos](#componentes-compartidos)
3. [Páginas Principales](#páginas-principales)
4. [Patrones de Componentes](#patrones-de-componentes)
5. [Inyección de Dependencias](#inyección-de-dependencias)
6. [Buenas Prácticas](#buenas-prácticas)

---

## Estructura de Componentes

### Jerarquía de Carpetas

```
GuarinoApp.Web/
├── Pages/                          # Componentes de página (~30 páginas)
│   ├── Index.razor                # Página de inicio
│   ├── Dashboard.razor            # Dashboard ejecutivo
│   ├── Login.razor                # Autenticación
│   ├── Register.razor             # Registro
│   ├── Error.cshtml               # Página de error
│   └── [Módulos]/
│       ├── StockGeneral.razor
│       ├── StockPlaya.razor
│       ├── Empaque.razor
│       ├── ProyeccionZafra.razor
│       ├── IAChat.razor
│       └── ... (más páginas)
│
├── Shared/                         # Componentes reutilizables
│   ├── MainLayout.razor           # Layout principal
│   ├── NavMenu.razor              # Menú de navegación
│   ├── CurrentUserDisplay.razor   # Muestra usuario actual
│   ├── ExportToolbar.razor        # Botones de exportación
│   ├── InteractiveChart.razor     # Gráficos reutilizables
│   ├── SimpleDialog.razor         # Diálogos genéricos
│   ├── SendEmailDialog.razor      # Envío de emails
│   ├── SurveyPrompt.razor         # Feedback
│   └── DataSourceFooter.razor     # Info de datos
│
├── Themes/                         # Temas y colores
│   └── PowerBIColors.cs           # Paleta de colores
│
├── Helpers/                        # Métodos de utilidad
│   └── FileDownloadExtensions.cs  # Descarga de archivos
│
├── Extensions/                     # Extensiones de datos
│   └── TableExtensions.cs         # Formatos para tablas
│
├── Validation/                     # Validadores custom
│   └── ValidateOllamaOptions.cs   # Validación de Ollama
│
├── App.razor                       # Componente raíz
├── _Imports.razor                 # Imports globales
└── Program.cs                      # Configuración startup
```

---

## Componentes Compartidos

### 1. MainLayout.razor

**Responsabilidad**: Layout principal de la aplicación

```razor
@inherits LayoutComponentBase
@inject AuthenticationStateProvider AuthProvider
@inject NavigationManager Navigation

<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar>
        @if (_isAuthenticated)
        {
            <MudIconButton Icon="@Icons.Material.Filled.Menu" 
                OnClick="@((e) => DrawerToggle())" />
        }
        <MudText Typo="Typo.h6">Guarino App</MudText>
        <MudSpacer />
        <CurrentUserDisplay />
    </MudAppBar>

    @if (_isAuthenticated)
    {
        <MudDrawer @bind-Open="_drawerOpen" Variant="DrawerVariant.Responsive">
            <NavMenu />
        </MudDrawer>
    }

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="my-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>
```

**Características**:
- 🔐 Gate de autenticación global
- 📱 Responsive (drawer se contrae en mobile)
- 🎨 MudBlazor theming
- 🚪 Gestión de sesión

---

### 2. NavMenu.razor

**Responsabilidad**: Menú de navegación contextual por roles

```razor
@using GuarinoApp.Application.Common.Interfaces
@inject IAuthorizationService AuthService

<MudNavMenu>
    <!-- INICIO -->
    <MudText Typo="Typo.subtitle2" Class="px-4 py-2" Color="Color.Primary">
        🏠 INICIO
    </MudText>
    <MudNavLink Href="/" Match="NavLinkMatch.All">Inicio</MudNavLink>
    <MudNavLink Href="/dashboard">Dashboard Ejecutivo</MudNavLink>

    <!-- OPERACIÓN DIARIA -->
    <MudDivider Class="my-2" />
    <MudText Typo="Typo.subtitle2" Class="px-4 py-2" Color="Color.Success">
        📦 OPERACIÓN DIARIA
    </MudText>
    <MudNavLink Href="/stockgeneral">Stock General</MudNavLink>
    <MudNavLink Href="/stockplaya">Stock en Playa</MudNavLink>

    <!-- INTELIGENCIA ARTIFICIAL - con @attribute [Authorize] -->
    <AuthorizeView Roles="Admin, Gerencia, Contador, Operador">
        <Authorized>
            <MudDivider Class="my-2" />
            <MudText Typo="Typo.subtitle2" Color="Color.Secondary">
                🤖 INTELIGENCIA ARTIFICIAL
            </MudText>
            <MudNavLink Href="/iachat">Asistente IA</MudNavLink>
        </Authorized>
    </AuthorizeView>

    <!-- ADMINISTRACIÓN - Solo Admin y Gerencia -->
    <AuthorizeView Roles="Admin, Gerencia">
        <Authorized>
            <MudDivider Class="my-2" />
            <MudText Typo="Typo.subtitle2" Color="Color.Error">
                ⚙️ ADMINISTRACIÓN
            </MudText>
            <MudNavLink Href="/usuarios">Usuarios</MudNavLink>
            <MudNavLink Href="/auditoria">Auditoría</MudNavLink>
        </Authorized>
    </AuthorizeView>
</MudNavMenu>
```

**Características**:
- 🎯 Menú dinámico basado en `AuthorizeView`
- 🏷️ Emojis para identificación visual
- 📑 Secciones colapsables
- ♿ Accesible (navegación por teclado)

---

### 3. ExportToolbar.razor

**Responsabilidad**: Botones reutilizables de exportación

```razor
@using GuarinoApp.Application.Common.Interfaces
@inject IExportService ExportService
@inject ISnackbar Snackbar
@inject IJSRuntime JS

<div class="d-flex gap-2 mb-3">
    <MudButton Variant="Variant.Filled" 
        Color="Color.Primary"
        StartIcon="@Icons.Material.Filled.Download"
        OnClick="@ExportToExcelAsync">
        📥 Descargar Excel
    </MudButton>

    <MudButton Variant="Variant.Filled" 
        Color="Color.Secondary"
        StartIcon="@Icons.Material.Filled.PictureAsPdf"
        OnClick="@ExportToPdfAsync">
        📄 Descargar PDF
    </MudButton>

    <MudButton Variant="Variant.Outlined" 
        Color="Color.Info"
        StartIcon="@Icons.Material.Filled.Email"
        OnClick="@OpenEmailDialog">
        📧 Enviar por Email
    </MudButton>
</div>

@code {
    [Parameter]
    public List<object> Data { get; set; } = new();

    [Parameter]
    public string FileName { get; set; } = "export";

    private async Task ExportToExcelAsync()
    {
        try
        {
            var bytes = await ExportService.ExportToExcelAsync(Data, FileName);
            await JS.InvokeVoidAsync("downloadFile", bytes, $"{FileName}.xlsx");
            Snackbar.Add("Exportado correctamente", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
    }

    private async Task ExportToPdfAsync()
    {
        try
        {
            var bytes = await ExportService.ExportToPdfAsync(Data, FileName);
            await JS.InvokeVoidAsync("downloadFile", bytes, $"{FileName}.pdf");
            Snackbar.Add("PDF generado", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenEmailDialog()
    {
        // Abre SendEmailDialog.razor
    }
}
```

**Características**:
- 🔄 Reutilizable en múltiples páginas
- 📦 Parámetros genéricos
- ✨ Retroalimentación visual (Snackbar)
- 📊 Manejo de errores

---

### 4. InteractiveChart.razor

**Responsabilidad**: Gráficos interactivos reutilizables

```razor
@using ApexCharts
@using GuarinoApp.Web.Shared

<MudCard>
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">@Title</MudText>
        </CardHeaderContent>
    </MudCardHeader>
    <MudCardContent>
        <ApexChart TItem="ChartSeriesItem"
            @ref="chart"
            Options="options"
            Height="400">

            @foreach (var series in ChartSeries)
            {
                <ApexPointSeries TItem="ChartSeriesItem"
                    Items="series.Items.ToList()"
                    Name="@series.Name"
                    XValue="@(e => e.X)"
                    YValue="@(e => e.Y)" />
            }

        </ApexChart>
    </MudCardContent>
    <MudCardActions>
        <MudButton Size="Size.Small" Color="Color.Primary">Ver detalles</MudButton>
    </MudCardActions>
</MudCard>

@code {
    [Parameter]
    public string Title { get; set; } = "Gráfico";

    [Parameter]
    public List<ChartSeries> ChartSeries { get; set; } = new();

    [Parameter]
    public ChartType Type { get; set; } = ChartType.Line;

    private ApexChart<ChartSeriesItem> chart;
    private ApexChartOptions<ChartSeriesItem> options;

    protected override void OnInitialized()
    {
        options = new ApexChartOptions<ChartSeriesItem>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = true } },
            Title = new Title { Text = Title },
            XAxis = new XAxis { Type = XAxisType.Datetime },
            Responsive = new List<Responsive>
            {
                new Responsive
                {
                    Breakpoint = 480,
                    Options = new ApexChartOptions<ChartSeriesItem>
                    {
                        Chart = new Chart { Height = 250 }
                    }
                }
            }
        };
    }
}
```

**Características**:
- 📊 ApexCharts integrado
- 🔄 Dinámico y actualizable
- 📱 Responsive
- 🎨 Temas personalizables

---

### 5. SimpleDialog.razor

**Responsabilidad**: Diálogos genéricos reutilizables

```razor
@using MudBlazor
@inject IDialogService DialogService

@code {
    // Ejemplo de uso:
    // var parameters = new DialogParameters
    // {
    //     { "ContentText", "¿Estás seguro?" },
    //     { "ButtonText", "Borrar" },
    //     { "Color", Color.Error }
    // };
    // 
    // var dialog = await DialogService.ShowAsync<SimpleDialog>
    //     ("Confirmar", parameters);
    // var result = await dialog.Result;
    //
    // if (!result.Canceled)
    //     await DeleteAsync();

    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; }

    [Parameter]
    public string ContentText { get; set; } = "";

    [Parameter]
    public string ButtonText { get; set; } = "Aceptar";

    [Parameter]
    public Color Color { get; set; } = Color.Primary;

    private void OnConfirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void OnCancel()
    {
        MudDialog.Cancel();
    }
}
```

---

### 6. CurrentUserDisplay.razor

**Responsabilidad**: Muestra información del usuario autenticado

```razor
@using System.Security.Claims
@inject AuthenticationStateProvider AuthProvider

<div class="d-flex align-items-center gap-2">
    <MudIcon Icon="@Icons.Material.Filled.Person" />
    <MudText Typo="Typo.body2">@UserName</MudText>
    <MudMenu AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight">
        <ActivatorContent>
            <MudIconButton Icon="@Icons.Material.Filled.MoreVert" Size="Size.Small" />
        </ActivatorContent>
        <ChildContent>
            <MudMenuItem Href="/perfil">👤 Mi Perfil</MudMenuItem>
            <MudDivider />
            <MudMenuItem OnClick="@LogoutAsync">🚪 Cerrar Sesión</MudMenuItem>
        </ChildContent>
    </MudMenu>
</div>

@code {
    private string UserName = "Cargando...";
    private ClaimsPrincipal user;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        user = authState.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            UserName = user.FindFirst(ClaimTypes.Name)?.Value 
                ?? user.FindFirst(ClaimTypes.Email)?.Value 
                ?? "Usuario";
        }
        else
        {
            UserName = "Anónimo";
        }
    }

    private async Task LogoutAsync()
    {
        // Implementar logout
    }
}
```

---

## Páginas Principales

### Dashboard.razor (Página Compleja)

```razor
@page "/dashboard"
@attribute [Authorize]
@using MediatR
@using GuarinoApp.Application.Common.Interfaces

@inject IMediator Mediator
@inject IDashboardDataService DashboardService
@inject ILogger<Dashboard> Logger

<PageTitle>Dashboard - Guarino</PageTitle>

<div class="mb-4">
    <MudText Typo="Typo.h4" GutterBottom="true">
        📊 Dashboard Ejecutivo
    </MudText>
    <MudText Typo="Typo.body1" Color="Color.TextSecondary">
        KPIs y métricas en tiempo real
    </MudText>
</div>

@if (IsLoading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (HasError)
{
    <MudAlert Severity="Severity.Error">
        Error al cargar datos: @ErrorMessage
    </MudAlert>
}
else
{
    <!-- KPIs -->
    <MudGrid>
        <MudItem xs="12" sm="6" md="3">
            <MudCard Class="h-100">
                <MudCardContent>
                    <MudText Typo="Typo.body2" Color="Color.TextSecondary">
                        Total Pallets
                    </MudText>
                    <MudText Typo="Typo.h4" Color="Color.Primary">
                        @KPIs?.TotalPallets
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>

        <MudItem xs="12" sm="6" md="3">
            <MudCard Class="h-100">
                <MudCardContent>
                    <MudText Typo="Typo.body2" Color="Color.TextSecondary">
                        Stock Disponible
                    </MudText>
                    <MudText Typo="Typo.h4" Color="Color.Success">
                        @KPIs?.AvailableStock kg
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>

        <MudItem xs="12" sm="6" md="3">
            <MudCard Class="h-100">
                <MudCardContent>
                    <MudText Typo="Typo.body2" Color="Color.TextSecondary">
                        Fruta a Volcar
                    </MudText>
                    <MudText Typo="Typo.h4" Color="Color.Warning">
                        @KPIs?.FruitToDump kg
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>

        <MudItem xs="12" sm="6" md="3">
            <MudCard Class="h-100">
                <MudCardContent>
                    <MudText Typo="Typo.body2" Color="Color.TextSecondary">
                        Riesgo Climático
                    </MudText>
                    <MudText Typo="Typo.h4" 
                        Color="@(KPIs?.ClimateRiskLevel == "Alto" 
                            ? Color.Error 
                            : Color.Info)">
                        @KPIs?.ClimateRiskLevel
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- Gráficos -->
    <MudGrid Class="mt-4">
        <MudItem xs="12" md="6">
            <InteractiveChart Title="Tendencia de Stock" 
                ChartSeries="StockTrendData" />
        </MudItem>
        <MudItem xs="12" md="6">
            <InteractiveChart Title="Volatilidad Climática" 
                ChartSeries="ClimateRiskData" />
        </MudItem>
    </MudGrid>
}

@code {
    private bool IsLoading = true;
    private bool HasError = false;
    private string ErrorMessage = "";

    private DashboardKpisDto KPIs;
    private List<ChartSeries> StockTrendData = new();
    private List<ChartSeries> ClimateRiskData = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;

            // Cargar KPIs
            KPIs = await DashboardService.GetKpisAsync();

            // Cargar datos de gráficos
            StockTrendData = await DashboardService.GetStockTrendAsync();
            ClimateRiskData = await DashboardService.GetClimateRiskTrendAsync();

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading dashboard");
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

---

## Patrones de Componentes

### Patrón: Componente de Tabla con Filtros

```razor
@page "/stockgeneral"
@attribute [Authorize]
@using MediatR
@using GuarinoApp.Application.Modules.Stock.Queries.GetPallets

@inject IMediator Mediator
@inject ILogger<StockGeneral> Logger

<MudText Typo="Typo.h5" Class="mb-3">📦 Stock General</MudText>

<!-- Barra de filtros -->
<MudPaper Class="d-flex gap-3 p-4 mb-4">
    <MudTextField T="string" @bind-Value="FilterNumero" 
        Label="Número de Pallet" />
    <MudSelect T="string" @bind-Value="FilterCliente" 
        Label="Cliente" MultiSelection="false">
        @foreach (var cliente in Clientes)
        {
            <MudSelectItem Value="@cliente">@cliente</MudSelectItem>
        }
    </MudSelect>
    <MudButton Variant="Variant.Filled" Color="Color.Primary" 
        OnClick="@ApplyFiltersAsync">
        🔍 Filtrar
    </MudButton>
    <MudButton Variant="Variant.Outlined" Color="Color.Default" 
        OnClick="@ResetFiltersAsync">
        ↻ Limpiar
    </MudButton>
</MudPaper>

<!-- Tabla -->
@if (IsLoading)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (Pallets == null || !Pallets.Any())
{
    <MudAlert Severity="Severity.Info">
        No se encontraron pallets
    </MudAlert>
}
else
{
    <MudDataGrid Items="@Pallets.AsReadOnly()" Filterable="true" 
        Sortable="true" Striped="true" Dense="true">
        <Columns>
            <PropertyColumn Property="x => x.Numero" Title="Número" />
            <PropertyColumn Property="x => x.Cliente" Title="Cliente" />
            <PropertyColumn Property="x => x.Peso" Title="Peso (kg)" />
            <PropertyColumn Property="x => x.FechaCreacion" Title="Fecha" />
            <TemplateColumn Title="Acciones">
                <CellTemplate>
                    <MudButton Size="Size.Small" 
                        OnClick="@(() => EditPalletAsync(context.Item.Id))">
                        ✏️ Editar
                    </MudButton>
                </CellTemplate>
            </TemplateColumn>
        </Columns>
    </MudDataGrid>
}

@code {
    private List<PalletDto> Pallets;
    private bool IsLoading = false;

    private string FilterNumero = "";
    private string FilterCliente = "";
    private List<string> Clientes = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadClientesAsync();
        await LoadPalletsAsync();
    }

    private async Task LoadPalletsAsync()
    {
        try
        {
            IsLoading = true;
            var query = new GetPalletsQuery 
            { 
                Numero = FilterNumero,
                Cliente = FilterCliente
            };

            var result = await Mediator.Send(query);
            Pallets = result.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading pallets");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyFiltersAsync()
    {
        await LoadPalletsAsync();
    }

    private async Task ResetFiltersAsync()
    {
        FilterNumero = "";
        FilterCliente = "";
        await LoadPalletsAsync();
    }
}
```

### Patrón: Componente con Diálogo Modal

```razor
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudButton Color="Color.Primary" 
    OnClick="@OpenCreateDialogAsync">
    ➕ Nuevo Usuario
</MudButton>

@code {
    private async Task OpenCreateDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<CreateUserDialog>
            ("Crear Usuario");

        var result = await dialog.Result;
        if (!result.Canceled)
        {
            Snackbar.Add("Usuario creado", Severity.Success);
            await ReloadAsync();
        }
    }
}
```

---

## Inyección de Dependencias

### Registro en Program.cs

```csharp
// Servicios de Presentación
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();

// Servicios de Aplicación
builder.Services.AddApplication();

// Servicios de Infraestructura
builder.Services.AddInfrastructure(builder.Configuration);

// Caché
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Servicios específicos
builder.Services.AddScoped<IClimateService, OpenMeteoClimateService>();
builder.Services.AddScoped<IClimateRiskService, ClimateRiskService>();
builder.Services.AddHostedService<ClimateMonitoringHostedService>();
```

### Uso en Componentes

```razor
@using GuarinoApp.Application.Common.Interfaces
@inject IMediator Mediator              <!-- CQRS -->
@inject IEmailService EmailService      <!-- Envío de emails -->
@inject IDialogService DialogService    <!-- Diálogos -->
@inject ISnackbar Snackbar              <!-- Notificaciones -->
@inject NavigationManager Navigation    <!-- Navegación -->
@inject IJSRuntime JS                   <!-- Interop JS -->
@inject ILogger<Component> Logger       <!-- Logging -->

@code {
    private async Task SendEmailAsync()
    {
        await EmailService.SendEmailAsync(new EmailRequest 
        { 
            To = "user@example.com",
            Subject = "Reporte",
            Body = "Contenido"
        });
    }
}
```

---

## Buenas Prácticas

### ✅ DO's

```razor
<!-- 1. Usar @attribute [Authorize] en páginas protegidas -->
@page "/admin"
@attribute [Authorize(Roles = "Admin")]

<!-- 2. Usar AuthorizeView para UI condicional -->
<AuthorizeView Roles="Admin">
    <Authorized>
        <!-- Contenido admin -->
    </Authorized>
    <NotAuthorized>
        <MudAlert Severity="Severity.Warning">
            No autorizado
        </MudAlert>
    </NotAuthorized>
</AuthorizeView>

<!-- 3. Usar parameters tipados -->
@code {
    [Parameter]
    [EditorRequired]
    public List<PalletDto> Pallets { get; set; }
}

<!-- 4. Usar CascadingParameters para layouts -->
@code {
    [CascadingParameter]
    public MainLayout Layout { get; set; }
}

<!-- 5. Disposer en componentes con recursos -->
@code {
    private IDisposable registration;

    protected override void OnInitialized()
    {
        registration = AuthProvider.AuthenticationStateChanged.Subscribe(_ => 
            StateHasChanged());
    }

    void IDisposable.Dispose()
    {
        registration?.Dispose();
    }
}
```

### ❌ DON'Ts

```razor
<!-- 1. No usar @onclick sin async Task -->
<!-- ❌ EVITAR -->
<MudButton OnClick="DeleteAsync">Borrar</MudButton>

<!-- ✅ CORRECTO -->
<MudButton OnClick="@DeleteAsync">Borrar</MudButton>

<!-- 2. No confundir StateHasChanged con re-renders -->
<!-- ❌ EVITAR: Llamar StateHasChanged múltiples veces -->
for (int i = 0; i < 1000; i++)
    StateHasChanged();

<!-- ✅ CORRECTO: Una sola llamada -->
await Task.Delay(100);
StateHasChanged();

<!-- 3. No pasar listas mutables como parámetros -->
<!-- ❌ EVITAR -->
@code {
    public List<Item> Items { get; set; } = new();
}

<!-- ✅ CORRECTO: ReadOnly -->
@code {
    public IReadOnlyList<Item> Items { get; set; } = new List<Item>();
}

<!-- 4. No usar strings para @key en listas -->
<!-- ❌ EVITAR -->
@foreach (var item in items)
{
    <Component @key="item.Name" Item="item" />
}

<!-- ✅ CORRECTO -->
@foreach (var item in items)
{
    <Component @key="item.Id" Item="item" />
}
```

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