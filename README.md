# GuarinoApp — Resumen y Guía de Despliegue

Aplicación **Blazor Server** de gestión citrícola con Clean Architecture (`Domain` / `Application` / `Infrastructure` / `Web`), asistente de IA y proceso de Spec-Driven Development (ver carpeta `specs/`).

## Resumen de cambios aplicados

### Recientes (2026)
- **Roles como tabla (`Roles`)** — `AppUser.RoleId` (FK) apunta a la tabla `Roles`; antes cada fila guardaba el rol como texto duplicado (violación de forma normal). Validación de rol por lookup, selects dinámicos desde BD en el panel de Usuarios y migración con seed + backfill (`f4770d2`).
- **Auditoría de normalización (FN)** — eliminadas 11 columnas redundantes o muertas con su migración `EliminarColumnasDesnormalizadas` (backfill en Up y Down) (`ba1bae9`):
  - 7 maestras (Especie, Empaque, Báscula, TipoLote, DestinoInterno, Emboquille, Responsable): columna `IdXxx` siempre en 0 junto a la PK identity; queries corregidos (`GetBasculas` devolvía `Id=0`; el join de báscula de Registro Pesadas nunca matcheaba).
  - `LoteEmboquille`: 4 columnas para 2 relaciones → solo las FKs reales `LoteId` / `EmboquilleId` (mapeo explícito en el DbContext).
  - `Pallet.EstadoTexto` eliminado: fuente única = enum `Estado` (+ miembro `Bajado`). Impacto real: los filtros `EstadoTexto <> 'B'` excluían 50 de 62 pallets por NULL en SQL; hoy Stock General muestra los 62 y el filtro por estado devuelve los correctos. `EstadoPalletExtensions` es la única tabla de equivalencias (etiqueta legible y código legado `D/F/P/R/B`).
  - `Volcado.IdBascula`: legacy sin FK ni uso.
  - `EventoClimatico.KilosPerdidosEstimados`: ahora se calcula al persistir (porcentaje estimado × kilos del lote, snapshot); antes ningún escritor lo llenaba y el KPI del dashboard sumaba 0.
- **Seguridad** — sanitización OWASP central de textos + filtro de Auditoría por lista de usuarios (`133f9c2`); login/registro local, alta de usuarios solo Admin, matriz de permisos por perfil y autorización por página (`d4d5f45`, `3a88e59`, `afd88f5`).
- **SPEC 003 (asistente IA)** — cliente MCP, UX de chat y piso de 3 sugerencias (`a492b9b`).
- **Spec-Driven Development** — estructura `specs/` con ADRs: 001 autenticación y roles, 002 auditoría, 003 asistente IA con MCP, 004 trazabilidad de bins con chip (pendiente, requiere acceso a la base de datos de dispositivos) (`c72d8d5`).

### Históricos
- Migración de consultas SQL crudo a EF Core / LINQ para mejorar compatibilidad y mantenimiento.
- `EfDashboardDataService` implementado con logging (`ILogger<T>`) y manejo de errores.
- Generación de meses faltantes en reportes mensuales (meses sin volcados devuelven `Kgs = 0`); etiquetas en formato `MMM yyyy` (es-ES).
- Añadida entidad keyless `RitmoCalibradora` para mapear `RITMOCALIBRADORA` (solo lectura).
- Reescrito `GetGraficaProcesosQueryHandler` para usar LINQ; eliminado SQL crudo en handlers principales.
- `.gitattributes` añadido para normalizar encoding a UTF-8 (sin BOM) en ficheros de código.
- Script `database/CreateSynonyms.sql` incluido para crear sinónimo en producción cuando la tabla viva en otra BD.

## Archivos/carpetas que conviene conocer
- `GuarinoApp.Web/wwwroot/css/open-iconic/` — librería de iconos **Open Iconic v1.1.1**, incluida por la plantilla oficial de Blazor. `site.css` importa su CSS y se usa en componentes (p. ej. `SurveyPrompt.razor`). Solo se distribuyen las fuentes y sus licencias (`FONT-LICENSE`, `ICON-LICENSE`); la documentación upstream (`README.md`) fue eliminada por no usarse en runtime.
- `_site/` — salida generada de docfx (committeada); se regenera en el próximo build de documentación.
- `specs/` — especificaciones SDD y ADRs del proyecto.

## Notas de despliegue en producción
1. Si `RITMOCALIBRADORA` está en otra BD (`informes`), pedir al DBA ejecutar `database/CreateSynonyms.sql` en la BD de la aplicación para crear:
   `CREATE SYNONYM dbo.RITMOCALIBRADORA FOR informes.dbo.RITMOCALIBRADORA;`
   Esto permite que EF Core lea la fuente remota sin usar SQL crudo.
2. Configurar un provider de logs en `appsettings.Production.json` (archivo, App Insights, ELK, etc.) para capturar `Error` y `Warning`.
3. Verificar la `DefaultConnection` y permisos DB (lectura sobre tablas/vistas requeridas).
4. Las migraciones EF se aplican automáticamente al arrancar (`Database.Migrate()`); revisar el historial de migraciones antes de cada despliegue.

## QA / Validaciones mínimas
- Compilar: `dotnet build "GuarinoApp.sln" --no-restore` (debe terminar 0 errores / 0 advertencias).
- Login con los perfiles Admin / Gerencia / Contador / Operador y verificar menú según permisos.
- Stock General: filtrar por estado y confirmar conteos; Registro Pesadas: joins de lote y báscula.
- En Dashboard: seleccionar varios periodos y confirmar etiquetas y valores.
- Validar Gráfica de Procesos y Resumen Mensual (datos reales o fallback).
- Probar prompts IA para asegurarse de que las sugerencias referencian métricas reales.

## Encoding
- El repositorio está preparado para UTF-8 sin BOM mediante `.gitattributes`. Asegurate de que tu editor guarde archivos como UTF-8 (sin BOM).

## Flujo de trabajo de cambios (sugerido)
Para cada mejora: build correcto → commit convencional → push a `master` → **actualizar este README** con lo nuevo.
```
git add -A
git commit -m "tipo(alcance): descripción concreta"
git push origin master
```

## Contacto
Para ajustes o incidencias: `devops@guarino.local`.

*Fin del resumen.*
