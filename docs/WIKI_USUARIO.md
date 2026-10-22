# Manual de usuario - GuarinoApp

Bienvenido al manual de usuario de **GuarinoApp**, el sistema de gestión de operaciones citrícolas. Esta guía explica cómo usar cada módulo de la aplicación, paso a paso, con capturas de pantalla.

---

## 1. Acceso al sistema

### 1.1 Iniciar sesión

1. Abre la aplicación en el navegador (ejemplo: `http://guarino.company.com`).
2. Se mostrara la pantalla de **Ingresar**.
3. Ingresa tu **Usuario** y **Contrasena**.
4. Pulsa el boton **INGRESAR** (o presiona Enter).

![Pantalla de login](img/login.png)

> **Nota**: Tras 5 intentos fallidos, la cuenta se bloquea por 15 minutos.

### 1.2 Roles y permisos

Cada usuario tiene un rol que determina que modulos puede ver y usar:

| Rol | Que puede hacer |
|---|---|
| **Admin** | Todo: usuarios, contactos, auditoria, contaduria, gerencia, operaciones. |
| **Gerencia** | Indicadores, proyecciones, reportes, stock, operaciones. |
| **Contador** | Reportes, liquidaciones, contaduria, resumenes, proyecciones. |
| **Operador** | Operaciones diarias: stock, pesadas, empaque, lotes, fruta a volcar, pallets. |

### 1.3 Menu e Inicio filtrados por rol

- En el **menu lateral** y en **Inicio**, los modulos que tu rol no permite se muestran **bloqueados** con un candado y un mensaje: "Requiere: [rol] - tu rol actual: [tu rol]".
- Si haces clic en uno bloqueado, **no se abre** (la pantalla no cambia).
- La seguridad real la impone el servidor: aunque intentes acceder por URL directa, el sistema denegara el acceso.

![Menu lateral con modulos bloqueados](img/menu-bloqueado.png)

---

## 2. Navegacion general

### 2.1 Menu lateral

El menu lateral (izquierda) organiza los modulos por categorias:

- **INICIO**: Inicio, Dashboard Ejecutivo, Panel de Gerencia
- **OPERACION DIARIA**: Stock General, Stock en Playa, Fruta a Volcar, Pallets Sin Clientes, Lotes Volcados, Registro de Pesadas, Gestion de Empaque
- **INTELIGENCIA ARTIFICIAL**: Proyeccion de Volcados, Proyeccion de Zafra Siguiente, Asistente IA
- **REPORTES Y ANALISIS**: Resumen de Liquidaciones, Liquidaciones de Quinta, Stock de Pallets Sin Cliente, Resumen Mensual, Grafica de Procesos, Pallets por Contramarca, Pallets Definitivos, Gestion de Riesgos Climaticos
- **LOGISTICA Y RECURSOS**: Transportistas, Camiones y Zorras, Lugares de Origen y Destino, Basculas
- **COMERCIAL**: Contactos
- **ADMINISTRACION**: Contaduria, Usuarios, Auditoria
- **DOCUMENTACION**: Wiki (este manual)

### 2.2 Paginacion

Todas las grillas (tablas) muestran **25 filas por pagina**.

- Usa los botones anterior/siguiente en el pie de la tabla para navegar.
- El pie muestra: "Mostrando X-Y de Z registros".

![Pie de tabla con paginacion](img/paginacion.png)

### 2.3 Exportar datos

1. En cualquier grilla, busca la barra **ExportToolbar** (arriba de la tabla).
2. Pulsa el boton del formato deseado: **PDF**, **Excel** o **CSV**.
3. El archivo se descarga automaticamente.

![Barra de exportacion](img/export-toolbar.png)

### 2.4 Filtrar datos

1. Usa los selectores de filtro (Variedad, Productor, etc.) en la barra de filtros.
2. Al cambiar un valor, la grilla se actualiza automaticamente.
3. Para limpiar un filtro, selecciona la opcion vacia.

---

## 3. Modulos principales

### 3.1 Inicio

Pantalla principal con accesos rapidos a los modulos disponibles segun tu rol.

![Pantalla de inicio](img/inicio.png)

### 3.2 Dashboard Ejecutivo

Muestra KPIs clave de la zafra: kilos, bins, pallets, tendencias.

![Dashboard ejecutivo](img/dashboard.png)

### 3.3 Stock General

Lista de pallets por variedad, estado y zafra.

**Como usarlo:**
1. Selecciona una **Zafra** del filtro (opcional).
2. Selecciona un **Estado** (opcional): En Frio, Despachado, etc.
3. La grilla muestra los pallets que cumplen los filtros.
4. Usa la paginacion para navegar.
5. Exporta con los botones PDF/Excel/CSV.

![Stock General](img/stock-general.png)

### 3.4 Stock en Playa

Pallets en playa (sin cliente asignado), agrupados por variedad.

### 3.5 Fruta a Volcar

Fruta pendiente de volcar por lote y productor.

### 3.6 Lotes Volcados

Lotes volcados agrupados por lote y fecha.

**Como usarlo:**
1. Ajusta el rango de fechas (por defecto: ultimos 3 meses).
2. Filtra por **Variedad** o **Productor** (opcional).
3. La grilla muestra los lotes volcados que cumplen los filtros.
4. Exporta si necesitas.

![Lotes Volcados con filtros](img/lotes-volcados.png)

### 3.7 Registro de Pesadas

Registro de pesadas (fuente: LotesEmboquille).

**Como usarlo:**
1. Ajusta el rango de fechas (por defecto: ultimos 3 meses).
2. Navega con la paginacion (25 por pagina).
3. Exporta si necesitas.

### 3.8 Gestion de Empaque

Catalogo y gestion de empaque.

### 3.9 Pallets por Contramarca / Definitivos / Sin Clientes

Vistas especializadas de pallets segun su estado.

### 3.10 Gestion de Riesgos Climaticos

Mapa interactivo de lotes con eventos climaticos.

**Como usarlo:**
1. El mapa muestra lotes con marcadores de riesgo.
2. Haz clic en un marcador para ver detalles.
3. Usa el boton **"Ver en la tabla"** para filtrar la tabla por ese lote.
4. Los KPIs muestran el total exacto (no solo los visibles en la tabla).

![Mapa de riesgos climaticos](img/riesgos-climaticos.png)

### 3.11 Asistente IA

Consultas de inteligencia artificial seleccionables segun tu rol.

**Como usarlo:**
1. Selecciona una consulta del catalogo (disponible segun tu rol).
2. Pulsa **Ejecutar**.
3. Espera la respuesta (hasta ~12 segundos para analisis de Contaduria).

![Asistente IA](img/ia-chat.png)

### 3.12 Proyeccion de Volcados / Zafra Siguiente

Proyecciones de volcados futuros y de la proxima zafra.

### 3.13 Reportes

- **Resumen de Liquidaciones**: resumen de liquidaciones.
- **Liquidaciones de Quinta**: liquidaciones por quinta.
- **Resumen Mensual**: resumen mensual.
- **Grafica de Procesos**: grafica de procesos.

### 3.14 Logistica y Recursos

- **Transportistas**: catalogo de transportistas.
- **Camiones y Zorras**: catalogo de camiones y zorras.
- **Lugares**: catalogo de lugares de origen y destino.
- **Basculas**: catalogo de basculas.

### 3.15 Contactos (solo Administracion)

Gestion de contactos con importacion masiva.

**Crear un contacto:**
1. Pulsa **NUEVO CONTACTO**.
2. Completa: Nombre, Empresa, Correo, Telefono.
3. Pulsa **GUARDAR**.

**Importar contactos:**
1. Pulsa **IMPORTAR**.
2. Selecciona un archivo CSV.
3. Revisa la vista previa.
4. Pulsa **IMPORTAR**.

> Validacion: correo con formato valido, telefono de 8 a 15 digitos.

### 3.16 Usuarios (solo Administracion)

**Crear un usuario:**
1. Pulsa **NUEVO USUARIO**.
2. Completa: Usuario (minimo 3 caracteres), Nombre completo, Correo (opcional), Contrasena (minimo 6 caracteres), Rol.
3. Pulsa **CREAR USUARIO**.
4. Si hay error, se muestra un mensaje rojo y el formulario permanece abierto.
5. En exito: mensaje verde "Usuario creado correctamente." y la grilla se actualiza.

**Desactivar un usuario:**
1. Busca la fila del usuario en la grilla.
2. Desactiva el switch **Activo**.

![Formulario de nuevo usuario](img/nuevo-usuario.png)

### 3.17 Contaduria (solo Financieros)

Datos financieros + contexto operativo. El Asistente IA puede analizar los datos (tarda ~12 segundos).

### 3.18 Gerencia (solo Administracion)

Indicadores de gerencia.

### 3.19 Auditoria (solo Administracion)

Log de todas las acciones del sistema.

**Como usarlo:**
1. Filtra por **Usuario**, **Accion**, **Modulo** o **Rango de fechas**.
2. La grilla muestra las acciones que cumplen los filtros.
3. Exporta si necesitas.

![Auditoria con filtros](img/auditoria.png)

---

## 4. Preguntas frecuentes

**No veo un modulo en el menu?**
Tu rol no tiene acceso. Los modulos bloqueados se muestran con candado. Contacta al administrador si necesitas acceso.

**La grilla tarda mucho en cargar?**
Algunas paginas (Contaduria, Dashboard) ejecutan analisis pesados. Espera a que aparezca el indicador de carga.

**Como cambio mi contrasena?**
Contacta al administrador. El sistema no permite cambio de contrasena por el usuario.

**Los numeros no coinciden con lo que espero?**
Todas las cifras se calculan directamente de la base de datos. Verifica los filtros de fecha y variedad/productor.

---

*Manual de usuario de GuarinoApp. Ultima actualizacion: Octubre 2026.*