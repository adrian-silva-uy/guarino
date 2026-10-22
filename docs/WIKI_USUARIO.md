# Wiki de usuario — GuarinoApp

> Manual funcional para usuarios finales. Describe qué hace cada módulo, quién puede usarlo y cómo realizar las tareas más comunes.

---

## 1. Acceso y roles

### 1.1 Inicio de sesión
- **Modo Local** (por defecto en desarrollo): usuario/contraseña en `/login`. Tras 5 intentos fallidos la cuenta se bloquea 15 minutos.
- **Modo Windows** (producción): autenticación integrada con Active Directory (Negotiate).
- El token de sesión dura **12 horas** y se guarda en el navegador.

### 1.2 Roles y permisos

| Rol | Alcance |
|---|---|
| **Admin** | Todo: usuarios, contactos, auditoría, contaduría, gerencia, operaciones. |
| **Gerencia** | Lectura de indicadores, proyecciones, reportes, stock, operaciones. |
| **Contador** | Reportes, liquidaciones, contaduría, resúmenes, proyecciones. |
| **Operador** | Operaciones diarias: stock, pesadas, empaque, lotes, fruta a volcar, pallets. |

### 1.3 Menú e Inicio filtrados por rol
- En el **menú lateral** y en **Inicio**, los módulos que tu rol no permite se muestran **bloqueados** con 🔒 y un tooltip: *"Requiere: X — tu rol actual: Y"*.
- Al hacer clic en uno bloqueado, **no se navega** (la URL permanece en la página actual).
- La seguridad real la impone el servidor: aunque se fuerce la URL, el acceso se deniega.

---

## 2. Módulos del sistema

### 2.1 Inicio (`/`)
- KPIs del día, accesos rápidos a módulos (filtrados por rol) y estado general.

### 2.2 Dashboard ejecutivo (`/dashboard`)
- Indicadores clave de la zafra: kilos, bins, pallets, tendencias.

### 2.3 Stock General (`/stockgeneral`)
- Stock de pallets por variedad, estado y zafra. Filtros por zafra y estado. Exportable.

### 2.4 Stock en Playa (`/stockplaya`)
- Pallets en playa (sin cliente asignado) por variedad.

### 2.5 Fruta a Volcar (`/frutavolcar`)
- Fruta pendiente de volcar por lote/productor.

### 2.6 Pallets Sin Clientes (`/palletssincliente`)
- Pallets sin cliente asignado.

### 2.7 Lotes Volcados (`/lotesvolcados`)
- Lotes volcados agrupados por lote/fecha. Filtros por variedad, productor y rango de fechas (por defecto últimos 3 meses). Exportable.

### 2.8 Registro de Pesadas (`/registropesadas`)
- Registro de pesadas (fuente: LotesEmboquille). Filtros por fecha (por defecto últimos 3 meses). Exportable.

### 2.9 Gestión de Empaque (`/empaque`)
- Catálogo y gestión de empaque.

### 2.10 Pallets por Contramarca (`/palletscontramarca`)
- Pallets agrupados por contramarca.

### 2.11 Pallets Definitivos (`/palletsdefinitivos`)
- Pallets definitivos.

### 2.12 Proyección de Volcados (`/proyeccionvolcados`)
- Proyección de volcados futuros.

### 2.13 Proyección de Zafra Siguiente (`/proyeccionzafra`)
- Proyección de la próxima zafra (solo Financieros).

### 2.14 Gestión de Riesgos Climáticos (`/gestionriesgosclimaticos`)
- Mapa de lotes con eventos climáticos, KPIs de riesgo, planes de mitigación. Filtros por lote/productor.

### 2.15 IA Chat (`/iachat`)
- Asistente IA con consultas seleccionables por rol. Las consultas disponibles dependen del rol (admin ve más).

### 2.16 Resumen de Liquidaciones (`/resumenliquidaciones`)
- Resumen de liquidaciones (Financieros).

### 2.17 Liquidaciones de Quinta (`/liquidacionesquinta`)
- Liquidaciones por quinta (Financieros).

### 2.18 Stock de Pallets Sin Cliente (`/stockpalletssincliente`)
- Stock de pallets sin cliente.

### 2.19 Resumen Mensual (`/resumenmensual`)
- Resumen mensual (Financieros).

### 2.20 Gráfica de Procesos (`/graficaprocesos`)
- Gráfica de procesos.

### 2.21 Transportistas (`/transportistas`)
- Catálogo de transportistas.

### 2.22 Camiones y Zorras (`/camioneszorras`)
- Catálogo de camiones y zorras.

### 2.23 Lugares de Origen y Destino (`/lugaresorigen`, `/lugaresdestino`, `/lugares`)
- Catálogos de lugares.

### 2.24 Básculas (`/basculas`)
- Catálogo de básculas.

### 2.25 Contactos (`/contactos`) — solo Administración
- CRUD de contactos + importación masiva (CSV). Validación de correo (formato) y teléfono (8-15 dígitos).

### 2.26 Usuarios (`/usuarios`) — solo Administración
- Alta de usuarios (usuario ≥3 chars, contraseña ≥6, correo válido opcional), asignación de rol (Admin/Operador/Contador/Gerencia), activar/desactivar, editar correo.

### 2.27 Contaduría (`/contaduria`) — solo Financieros
- Datos financieros + contexto operativo (IA analiza en ~12 s).

### 2.28 Gerencia (`/gerencia`) — solo Administración
- Indicadores de gerencia.

### 2.29 Auditoría (`/auditoria`) — solo Administración
- Log de todas las acciones (filtros por usuario, acción, módulo, fecha). Exportable.

---

## 3. Tareas comunes

### 3.1 Exportar datos
1. En cualquier grilla, usar la barra `ExportToolbar` (arriba de la tabla).
2. Elegir formato: **PDF**, **Excel** o **CSV**.
3. Opcional: ajustar rango de fechas (`Fecha desde` / `Fecha hasta`) y pulsar **Actualizar**.
4. El archivo se descarga automáticamente.

### 3.2 Filtrar una grilla
- Usar los selectores de la barra de filtros (Variedad, Productor, etc.).
- Los filtros se aplican al cambiar el valor (reactivo).
- Para limpiar, seleccionar la opción vacía.

### 3.3 Paginación
- Todas las grillas muestran **25 filas por página**.
- Usar los botones ◀ ▶ del pie de tabla para navegar.
- El pie muestra "Mostrando X–Y de Z registros".

### 3.4 Crear un usuario (Admin)
1. Ir a **Usuarios** → **NUEVO USUARIO**.
2. Completar: Usuario (≥3 chars), Nombre completo, Correo (opcional), Contraseña (≥6 chars), Rol.
3. Pulsar **CREAR USUARIO**.
4. Si hay error de validación, se muestra un mensaje rojo (Snackbar) y el formulario permanece abierto.
5. En éxito: Snackbar verde "Usuario creado correctamente." y la grilla se actualiza.

### 3.5 Desactivar un usuario (Admin)
1. En **Usuarios**, buscar la fila del usuario.
2. Desactivar el switch **Activo** (se registra en auditoría).

### 3.6 Importar contactos (Admin)
1. En **Contactos** → **Importar**.
2. Seleccionar archivo CSV con formato (Nombre, Empresa, Correo, Teléfono).
3. Vista previa de las filas; pulsar **Importar**.
4. Las filas con correo/teléfono inválidos se rechazan (validación server-side).

### 3.7 Consultar el Asistente IA
1. Ir a **Asistente IA**.
2. Seleccionar una consulta del catálogo (disponible según tu rol).
3. Pulsar **Ejecutar** y esperar la respuesta (hasta ~12 s para análisis de Contaduría).

---

## 4. Notas para administradores

- **Credenciales iniciales**: el seed crea `gerencia`/`contador`/`operador` con contraseña `Guarino2026` (solo primer arranque). **Cambiarlas inmediatamente**.
- **Primer registro** crea un usuario Admin si el registro está habilitado (`AllowRegistration`).
- **Auditoría**: todas las acciones quedan en `/auditoria` (solo Admin).
- **Backups**: la BD es SQL Server; programar backups regulares.
- **MCP**: el endpoint `/mcp` acepta tokens Bearer (12 h) y expone solo consultas de solo lectura, filtradas por rol.

---

*Wiki generada a partir del código fuente verificado. Para detalles técnicos, ver `docs/diagramas.md` y `docs/documentacion-inversa.md`.*
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