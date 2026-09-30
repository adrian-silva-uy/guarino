# Especificaciones — GuarinoApp (Spec-Driven Development)

Esta carpeta es la **fuente de verdad** del qué y el por qué del producto.
El código implementa lo que acá dice; si un requisito cambia, cambia la spec
primero (o en el mismo commit).

## Flujo

1. **Spec**: se escribe o actualiza `NNN-nombre/SPEC.md`.
2. **Revisión**: el usuario aprueba la spec (criterios de aceptación incluidos).
3. **Tareas**: se descompone en tareas pequeñas.
4. **Implementación**: código que cumple los criterios, respetando Clean
   Architecture (Domain / Application / Infrastructure / Web).
5. **Verificación**: cada criterio de aceptación (AC) se prueba y queda asentado.
6. **Commit**: `feat|fix|docs: ... (spec NNN)`.

## Convenciones

- **FR-x**: requisito funcional. **NFR-x**: no funcional. **AC-x**: criterio de
  aceptación (Dado / Cuando / Entonces).
- **Preguntas abiertas (PA-x)**: todo lo no decidido; se cierra en revisión y
  pasa a FR o a un ADR.
- **Estados**: 🟡 borrador (pendiente revisión) · 🚧 en desarrollo ·
  ✅ implementado y verificado.
- Decisiones con impacto transversal → **ADR** en `specs/adr/`.

## Índice de specs

| Spec | Estado | Resumen |
|---|---|---|
| [001-autenticacion-y-roles](001-autenticacion-y-roles/SPEC.md) | ✅ | Login/registro (Local) o Windows, roles, autorización por página/menú/home, reglas del Admin |
| [002-auditoria](002-auditoria/SPEC.md) | ✅ | Quién / hora / tarea: accesos y cambios de usuario |
| [003-asistente-ia-con-herramientas-mcp](003-asistente-ia-con-herramientas-mcp/SPEC.md) | ✅ | La app como **cliente MCP**: el Asistente IA consulta servidores MCP externos y enriquece las respuestas |
| [004-trazabilidad-bins-con-chip](004-trazabilidad-bins-con-chip/SPEC.md) | 🟡 | Cosecha por operario con bins con chip → trazabilidad punta a punta |

## Índice de ADRs

| ADR | Estado | Decisión |
|---|---|---|
| [001-autenticacion-dual-local-windows](adr/001-autenticacion-dual-local-windows.md) | Aceptada | `Auth:Mode` Local / Windows |
| [002-matriz-de-roles](adr/002-matriz-de-roles.md) | Aceptada | Qué ve cada rol, derivado de los perfiles de usuario |
| [003-estrategia-de-herramientas-del-asistente](adr/003-estrategia-de-herramientas-del-asistente.md) | ✅ Aceptada | Orquestación **A** (determinista en código) con camino a C; ADR-003 |
