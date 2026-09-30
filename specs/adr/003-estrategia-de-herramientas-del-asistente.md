# ADR-003: Estrategia de herramientas para el Asistente IA (MCP)

**Estado**: ✅ Aceptada — decisión **A** (orquestación determinista), aprobada en la revisión de SPEC 003 (2026-09-30)

## Contexto

Queremos que el Asistente IA consulte **servidores MCP externos** (app como
cliente MCP) para dar respuestas con datos en vivo y "volar la cabeza del
cliente". El modelo local actual (1B, contexto 1024 tokens) **no selecciona
herramientas de forma confiable**: el tool-calling agéntico necesita un modelo
mayor.

## Opciones

- **A — Orquestación determinista (recomendada hoy)**: el código decide, según
  la intención de la consulta, qué datos externos y qué skills traer; el modelo
  solo redacta la respuesta final. Funciona con el modelo y el hardware actuales.
- **B — Agéntica**: el modelo elige y encadena herramientas. Requiere modelo
  ≥ 8B con contexto ≥ 8k (más RAM/VRAM o servicio externo).
- **C — Híbrida escalonada**: A ahora; migrar a B cuando el hardware lo
  permita, sin reescribir las skills.

## Decisión

**A**, dejando el contrato de orquestación preparado para evolucionar a C.

## Consecuencias

- Resultados ricos sin depender de un modelo grande: datos duros (skills) +
  datos vivos (MCP) + redacción.
- Añadir un servidor MCP externo = configuración (allowlist), no código.
- El salto a B exige inversión de infraestructura; queda registrado aquí para
  que sea una decisión consciente, no un efecto secundario.
