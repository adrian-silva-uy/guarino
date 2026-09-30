# SPEC 004 — Trazabilidad de cosecha (bins con chip)

**Estado**: 🟡 borrador (pendiente acceso a la base del proveedor)

## Contexto

Dispositivos chilenos leen **bins con chip** en la chacra y registran lo
cosechado **por operario**. Hoy no hay acceso a esa base de datos; cuando se
tenga, se extraerán los datos. La app debe estar lista igual: primero la spec
y el modelo, después el adaptador real.

## Objetivo

Trazabilidad de punta a punta:

```
pallet → volcado → planta/empaque → bin (chip) → operario → chacra
```

## Requisitos funcionales

- **FR-1** Modelo de dominio del evento de cosecha: { chip del bin, operario,
  chacra/lote, fecha/hora, cantidad, unidad, dispositivo origen }.
- **FR-2** Puerto en Application (`IOrigenCosecha`): qué necesitamos — eventos
  normalizados. El esquema del proveedor **no** se filtra fuera de
  Infrastructure (anti-corruption layer).
- **FR-3** Adaptador *fake* con fixtures mientras no hay acceso real: permite
  construir vistas y reportes hoy; el adaptador real reemplaza solo ese archivo.
- **FR-4** Ingesta con staging: guardar el payload crudo primero, normalizar
  después.
- **FR-5** Idempotencia: clave natural (chip + timestamp + dispositivo); las
  recargas no duplican eventos.
- **FR-6** Vistas de trazabilidad: desde un pallet, ver bins / operario(s) /
  chacra de origen; desde un operario, lo cosechado en un período.
- **FR-7** Auditoría de ingesta: eventos nuevos, errores y duplicados omitidos.
- **FR-8** Skill futura para el Asistente IA (p. ej. "¿qué entregó el operario X?").

## Requisitos no funcionales

- **NFR-1** Los datos del proveedor viven solo en el adaptador de Infrastructure.
- **NFR-2** Reintentos con backoff ante caídas; la app no depende de la base
  externa para arrancar.
- **NFR-3** Clean Architecture intacta; textos de usuario sin jerga técnica.

## Desconocidos (a confirmar con el proveedor)

- **D-1** Esquema/tablas y medio de acceso (DB directa, réplica, API o archivo).
- **D-2** Identificador real del chip (serial RFID/UHF) y si es único global.
- **D-3** Unidad de medida: si el peso lo da el dispositivo o se calcula.
- **D-4** Frecuencia de sincronización y comportamiento offline de los
  dispositivos.
- **D-5** Codificación de operarios y chacras (¿legajos propios o del proveedor?).
- **D-6** Zona horaria y reglas de duplicados / correcciones.

## Criterios de aceptación

- **AC-1** Dado un pallet, cuando se consulta su trazabilidad, entonces se
  listan sus bins, operario(s) y chacra de origen (end-to-end con datos fake).
- **AC-2** Dada una re-ingesta del mismo lote de eventos, entonces no se
  duplican.
- **AC-3** Dado un evento crudo, entonces es consultable tal cual en staging.
- **AC-4** Con el adaptador real conectado, entonces AC-1..AC-3 siguen
  cumpliéndose sin cambios fuera de Infrastructure.

## Preguntas abiertas

- **PA-1** ¿Cuál es el proveedor y el mecanismo exacto de acceso? (D-1)
- **PA-2** ¿Se factura/paga por cosecha en la app o solo trazabilidad?
- **PA-3** ¿Cómo se puentea con Registro de Pesadas de la planta
  (bin → volcado)?
