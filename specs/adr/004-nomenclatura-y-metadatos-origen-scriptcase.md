# ADR-004: Nomenclatura de módulos y trazabilidad desde los respaldos ScriptCase

**Estado**: ✅ Aceptada — decisión del usuario ("la aplicación se fue creando en base a
los archivos .bkp obtenidos de ScriptCase… analizalo y defínelo vos"), aplicada 2026-10-01

## Contexto

GuarinoApp se reconstruyó sobre ScriptCase (proyecto NORIDEL, "SISTEMA DE TRAZABILIDAD").
Ese legado está disponible en tres artefactos:

1. **Respaldos `.bkp`** (`sc9_NORIDEL_20260624-132802/apl/*.bkp`): incluyen el árbol del
   menú original (`app_menu.bkp`) y el título/SQL de cada aplicación.
2. **Catálogo de metadatos** `NORIDEL_catalogo_metadata.sql` (en la raíz del repo):
   generado desde los 67 `.bkp`; guarda aplicaciones, tablas, relaciones de
   navegación (drill-down) y JOINs inferidos de los SELECT reales.
3. **Dump de la DB real** `GuarinoAppDb.sql` (en la raíz del repo): esquema de
   `GuarinoAppDb` (Pallets, Lotes, LotesEmboquille, Volcados, vRitmoCalibradora, …).

Durante la revisión del menú aparecieron: nombres distintos para la misma ruta
(home vs. menú vs. título), un posible duplicado ("Pallets sin Cliente" vs.
"Stock Pallets sin Cliente") y la pantalla `/facturacion` sin linkar.

## Decisión

1. **Fuente de verdad de los nombres**: las **etiquetas del menú original**
   (`app_menu.bkp`) y los títulos de cada app (grilla). Los módulos **nuevos**
   (que no existían en ScriptCase) conservan el nombre definido en su spec.
2. **Regla de duplicados**: si dos pantallas muestran **exactamente lo mismo**, se
   conserva la cuya descripción es más fiel al `.bkp`. Análisis realizado: **no hay
   duplicados exactos** — `Pallets_Definitivos_Sin_Clientes` (grilla, detalle fila por
   fila sobre `Catalogo_Pallets_Definitivos`) y `pwbi_Stock_Sin_Clientes` (reporte Power
   BI, agregado) eran dos pantallas complementarias en el sistema original. Se
   mantienen ambas, con sus nombres originales. Duplicados que **sí existían en
   ScriptCase** (3× `blank_dashboard*`, 2× `Resumen_Men*_Volcado`,
   `concentrador_ejecutor` + `_copy`) ya habían quedado unificados en una sola
   pantalla cada uno.
3. **Una ruta = un nombre**: menú lateral, home y título de página usan la misma
   etiqueta.
4. **Secciones del menú actual ↔ carpetas originales**:

   | Sección actual | Origen en ScriptCase |
   |---|---|
   | INICIO (Dashboard, Panel de Gerencia) | `root/dashboard` (`blank_dashboard*`) + `pwbi_Indicadores_Gerencia` (menú "Indicadores") |
   | OPERACIÓN DIARIA | INFORMES ▸ Operativos + BALANZAS ▸ Registro de Pesadas |
   | INTELIGENCIA ARTIFICIAL | nuevo (no existía en ScriptCase) |
   | REPORTES Y ANÁLISIS | INFORMES ▸ Gerencia + ESTADISTICAS POWERBI |
   | LOGÍSTICA Y RECURSOS | BALANZAS ▸ Archivos Varios (Transportistas, Camiones y Zorras, Lugares) + Básculas |
   | ADMINISTRACIÓN | root/Security (Usuarios, Auditoría) + Contaduría (nueva) |

5. **`/facturacion`**: fuera del menú hasta que se implemente (decisión del usuario,
   2026-10-01).

## Renombres aplicados (2026-10-01)

| Ubicación | Antes | Ahora | Fuente .bkp |
|---|---|---|---|
| Menú / home / `/palletssincliente` | Pallets sin Cliente | **Pallets Sin Clientes** | menú "Pallets Sin Clientes" |
| Menú / home / `/stockpalletssincliente` | Stock Pallets sin Cliente | **Stock de Pallets Sin Cliente** | menú "Stock de Pallets Sin Cliente" |
| Menú / `/lugares` | Lugares de Origen/Destino | **Lugares de Origen y Destino** | menú "Lugares de Origen y Destino" |
| Título `/stockgeneral` | Gestión de Stock | **Stock General** | coherencia con menú y home |
| Home | Panel Gerencial | **Panel de Gerencia** | = menú lateral |
| Home | Proyección de Fruta | **Proyección de Volcados** | = menú lateral |
| Home | Proyección de Zafra | **Proyección de Zafra Siguiente** | = menú lateral |
| Home | Línea de Empaque | **Gestión de Empaque** | = menú lateral |
| Home | Control de Básculas | **Básculas** | = menú lateral |
| Home | Pallets Contramarca | **Pallets por Contramarca** | = menú lateral |
| Home (card Control de Stock) | "Ver stock en playa" (→ /stockgeneral) | **Ver stock general** | corrige link engañoso |

## Consecuencias

- Home, menú lateral y títulos de página dicen lo mismo → menos confusión en demo.
- Los dos reportes "sin cliente" quedan diferenciados por subtítulo: *detalle un
  registro por pallet* vs. *resumen por variedad y estado*.
- Los artefactos de linaje viven en la raíz del repo (`NORIDEL_catalogo_metadata.sql`,
  `GuarinoAppDb.sql`); los `.bkp` originales quedan fuera del repo (respaldo del
  cliente, ruta local).
- **PA-5**: cargar las tablas `Cat_*` del catálogo en `GuarinoAppDb` y exponerlas al
  Asistente IA como herramienta de linaje ("¿de dónde sale este dato?").
