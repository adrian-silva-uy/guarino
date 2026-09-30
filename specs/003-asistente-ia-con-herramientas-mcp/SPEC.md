# SPEC 003 — Asistente IA con herramientas MCP externas

**Estado**: ✅ implementada y verificada en runtime (2026-09-30)

## Contexto

**MCP (Model Context Protocol)** es el estándar abierto para que sistemas de IA
usen herramientas y datos externos de forma uniforme. En este proyecto la app
es **cliente MCP**: el Asistente IA consulta servidores MCP externos
permitidos (allowlist) para enriquecer sus respuestas con datos en vivo, y los
resultados "vuelan la cabeza del cliente".

> **Fuera de alcance por ahora**: exponer nuestras skills como servidor MCP
> (que agentes externos consulten la app). Queda anotado como complemento
> futuro.

## Objetivo

Que el Asistente IA (`/iachat`) responda con **datos internos + datos externos
en vivo**, de forma confiable, segura y degradando con elegancia.

## Arquitectura (visión)

```
Usuario → /iachat → Asistente IA
   ├─ Skills internas (BD propia)           → datos duros garantizados
   ├─ Servidores MCP externos (allowlist)   → datos vivos (FX, noticias, …)
   └─ Modelo IA redacta la respuesta final
```

## Requisitos funcionales

- **FR-1** Los servidores MCP externos se definen en **configuración**
  (allowlist: URL, credencial, timeout, herramientas habilitadas). Sin
  allowlist no hay conexión; añadir un servidor no exige recompilar.
- **FR-2** Cliente MCP con el SDK oficial .NET (`ModelContextProtocol`),
  transporte HTTP con TLS.
- **FR-3** La estrategia de orquestación está definida en **ADR-003**
  (A determinista / B agéntica / C híbrida). El modelo actual
  (1B, contexto 1024) **no** soporta selección de herramientas confiable;
  para B se requiere modelo más grande.
- **FR-4** Degradación elegante: si un servidor externo cae o hace timeout, el
  asistente responde con las skills internas y nunca falla el chat.
- **FR-5** Auditoría de cada llamada saliente: servidor, herramienta,
  argumentos, resultado (ok/error) y latencia.
- **FR-6** Seguridad: solo servidores de la allowlist; credenciales en
  configuración protegida; **nunca** enviar datos comerciales ni personales
  en argumentos a servidores externos.
- **FR-7** Rate limit y cache corta para datos volátiles (reutilizar el rate
  limiter existente).
- **FR-8** Inventario de candidatos: evaluar el ecosistema MCP (clima, tipo de
  cambio, noticias, búsqueda…). Lo específico de citricultura es escaso → para
  datos únicos sigue conviniendo la integración directa (como ya se hizo con
  el clima) o nuestras skills internas.

## Datos buscados (valor para el cliente)

- Clima / riesgo agrícola (ya integrado directo; MCP sólo si aporta más fuentes).
- Tipo de cambio para exportación; referencias de mercado y noticias del sector.

## Criterios de aceptación

- **AC-1** Dado un servidor externo respondiendo, cuando el usuario consulta
  algo que lo requiere, entonces la respuesta incluye el dato externo indicando
  la fuente.
- **AC-2** Dado el servidor externo caído, cuando el usuario repite la consulta,
  entonces responde con datos internos, sin error.
- **AC-3** Dado un servidor fuera de la allowlist, entonces la app jamás se
  conecta a él.
- **AC-4** Dada una llamada saliente, entonces queda auditada con resultado y
  latencia.

## Verificación (2026-09-30, runtime local: Ollama + DeepWiki/Context7)

- **AC-1 ✅** Consulta trigger "¿Qué es MudSelect y cómo se usa?" → el cliente
  MCP ejecutó `DeepWiki/ask_wiki_question` (**OK, 23 903 ms**) y la respuesta
  del asistente termina con el pie determinista
  **"Fuente de datos externos: DeepWiki."** (verificado en el DOM de la chat).
- **AC-2 ✅** Con `Mcp:TimeoutSeconds` reducido a 15 s, la llamada agotó el
  tiempo (fila **ERROR, 15 037 ms**) y el asistente respondió con datos
  internos, sin error visible para el usuario (degradación elegante).
- **AC-3 ✅** Consulta "tipo de cambio dólar" (fuera de allowlist:
  AlphaVantage deshabilitado) → **0 llamadas** al servidor no permitido y
  respuesta interna sin error.
- **AC-4 ✅** Filas `McpCall` en `LogEntries` con servidor, resultado y
  latencia: **OK** (21 241 ms, 17 143 ms, 23 903 ms) y **ERROR** (15 037 ms).

## Preguntas abiertas

- **PA-1** ~~¿Orquestación A, B o C?~~ Resuelta: **A** (ADR-003, aceptada 2026-09-30; camino a C.)
- **PA-2** ¿Qué servidores candidatos priorizar? Evaluado 2026-09-30:
  Context7 y DeepWiki responden sin clave (habilitados como muestra viva);
  tipo de cambio (AlphaVantage) y noticias/búsqueda existen como MCP pero
  requieren clave de proveedor → pendiente decidir proveedor y habilitar.
- **PA-3** ¿Habrá consumo externo de nuestras skills (modo servidor) en el
  futuro del cliente?
