# GuarinoApp — Resumen y Guía de Despliegue

## Resumen de cambios aplicados
- Migración de consultas SQL crudo a EF Core / LINQ para mejorar compatibilidad y mantenimiento.
- `EfDashboardDataService` implementado con logging (`ILogger<T>`) y manejo de errores.
- Generación de meses faltantes en reportes mensuales (meses sin volcados devuelven `Kgs = 0`); etiquetas en formato `MMM yyyy` (es-ES).
- Añadida entidad keyless `RitmoCalibradora` para mapear `RITMOCALIBRADORA` (solo lectura).
- Reescrito `GetGraficaProcesosQueryHandler` para usar LINQ; eliminado SQL crudo en handlers principales.
- `.gitattributes` añadido para normalizar encoding a UTF‑8 (sin BOM) en ficheros de código.
- Script `database/CreateSynonyms.sql` incluido para crear sinónimo en producción cuando la tabla viva en otra BD.

## Archivos importantes agregados/modificados
- `Domain/Entities/RitmoCalibradora.cs`
- `Infrastructure/Data/ApplicationDbContext.cs`
- `Infrastructure/Services/EfDashboardDataService.cs`
- `Application/Modules/GraficaProcesos/.../GetGraficaProcesosQueryHandler.cs`
- `Web/Pages/Dashboard.razor`, `Web/Pages/IAChat.razor` (mejoras IA y logging)
- `database/CreateSynonyms.sql`
- `.gitattributes`
- `README.md` (este archivo)

## Notas de despliegue en producción
1. Si `RITMOCALIBRADORA` está en otra base (`informes`), pedir al DBA ejecutar `database/CreateSynonyms.sql` en la BD de la aplicación para crear:
   `CREATE SYNONYM dbo.RITMOCALIBRADORA FOR informes.dbo.RITMOCALIBRADORA;`
   Esto permite que EF Core lea la fuente remota sin usar SQL crudo.
2. Configurar un provider de logs en `appsettings.Production.json` (archivo, App Insights, ELK, etc.) para capturar `Error` y `Warning`.
3. Verificar la `DefaultConnection` y permisos DB (lectura sobre tablas/vistas requeridas).

## QA / Validaciones mínimas
- Compilar: `dotnet build`.
- En Dashboard: seleccionar varios periodos y confirmar etiquetas y valores.
- Validar Gráfica de Procesos y Resumen Mensual (datos reales o fallback).
- Probar prompts IA para asegurarse de que las sugerencias referencian métricas reales.

## Encoding
- El repositorio está preparado para UTF‑8 sin BOM mediante `.gitattributes`. Asegurate de que tu editor guarde archivos como UTF‑8 (sin BOM).

## Cómo guardar y enviar los cambios (sugerido)
Desde `C:\Guarino\`:
- git add -A
- git commit -m "feat: migrar SQL crudo a EF/LINQ; añadir RitmoCalibradora keyless; logging; meses faltantes; README y .gitattributes; CreateSynonyms.sql"
- git push origin <tu-rama>

## Contacto
Para ajustes o incidencias: `devops@guarino.local`.

*Fin del resumen.*
