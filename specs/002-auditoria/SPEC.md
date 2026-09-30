# SPEC 002 — Auditoría

**Estado**: ✅ implementado y verificado (backfill formal)

## Objetivo

Rastro incontestable de **quién**, **cuándo** y **qué** hizo el sistema y los
usuarios, consultable desde Administración.

## Alcance

Tabla de auditoría, escritura en accesos y cambios de usuario, y pantalla
`/auditoria`.

## Requisitos funcionales

- **FR-1** Cada evento registra: usuario, rol, acción, módulo, descripción
  legible, timestamp, IP, datos adicionales y marcas
  `CreatedBy/UpdatedAt/IsDeleted` (borrado lógico).
- **FR-2** Login y registro se auditan con `UserName = "Sistema"`; el usuario
  real y su rol quedan en la descripción.
- **FR-3** Cambios de usuario: acción `UpdateUser`, módulo `Usuarios`, con
  detalle legible ("inactivo desde 30/09/2026 17:01", "rol Gerencia →
  Operador"), `CreatedBy` = actor real.
- **FR-4** La pantalla de auditoría es visible solo para Admin y Gerencia.
- **FR-5** Los eventos de ingesta externa (clima; futuro: cosecha con bins)
  quedan auditados o en su bitácora equivalente.

## Requisitos no funcionales

- **NFR-1** Nunca se borra ni se edita un evento (solo borrado lógico
  controlado).
- **NFR-2** Índice por `Timestamp` y `UserName` para consulta ágil.

## Criterios de aceptación

- **AC-1** Dado un cambio de estado o de rol de usuario, cuando se produce,
  entonces aparece el evento con actor, detalle y hora exactos.
- **AC-2** Dado un Operador, cuando entra a `/auditoria`, entonces ve la
  denegación.

## Preguntas abiertas

- **PA-1** Retención y exportación (CSV) de eventos.
- **PA-2** Alertas ante eventos sensibles (p. ej. otorgar el rol Admin).
