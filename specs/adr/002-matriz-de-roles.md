# ADR-002: Matriz de roles por perfil de usuario

**Estado**: Aceptada

## Contexto

Los usuarios reales de la app son: **Contaduría**, **Gerencia** y
**Supervisores y Encargados de Áreas** (más el Admin técnico). La matriz de
permisos se derivó de esos perfiles, no de caprichos del código. Los
Supervisores/Encargados de Áreas son el rol `Operador` (personas con cargos
altos, por lo que su acceso es amplio en lo operativo).

## Decisión

| Módulo | Admin | Gerencia | Contador | Operador |
|---|:-:|:-:|:-:|:-:|
| Inicio, Dashboard Ejecutivo, Operación Diaria (incluye Gestión de Empaque) | ✅ | ✅ | ✅ | ✅ |
| Asistente IA, Proyección de Volcados | ✅ | ✅ | ✅ | ✅ |
| Proyección de Zafra Siguiente (planificación) | ✅ | ✅ | ✅ | — |
| Reportes operativos: Pallets Definitivos, Stock Pallets sin Cliente, Resumen Mensual de Volcado, Gráfica de Procesos, Pallets por Contramarca, Riesgos Climáticos | ✅ | ✅ | ✅ | ✅ |
| Liquidaciones (Resumen de Liquidaciones, Quinta) | ✅ | ✅ | ✅ | — |
| Logística: Transportistas, Camiones y Zorras, Lugares, Básculas | ✅ | ✅ | ✅ | ✅ |
| Contaduría (datos financieros) | ✅ | ✅ | ✅ | — |
| Facturación (página sin entrada de menú) | ✅ | ✅ | ✅ | — |
| Usuarios, Auditoría, Panel Gerencial | ✅ | ✅ | — | — |

## Consecuencias

- El menú y las tarjetas del home **ocultan** lo que el rol no puede abrir
  (coherencia visual con la denegación server-side).
- Las reglas críticas (activo/inactivo, otorgar/quitar Admin, último Admin
  activo) viven en el servidor — ver SPEC 001, FR-9.
- Gestión de Empaque se movió a 📦 Operación Diaria: es un reporte operativo
  (pallets por empaque y ocupación), no una pantalla de administración.
