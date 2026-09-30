# SPEC 001 — Autenticación y roles

**Estado**: ✅ implementado y verificado (backfill formal)

## Objetivo

Acceso seguro con dos modalidades y control de acceso coherente en toda la
app: cada usuario ve solo lo que su rol le corresponde, sin mensajes técnicos.

## Alcance

- Modalidad de acceso configurable (`Auth:Mode`): `Local` (default) o `Windows`.
- Gestión de usuarios y roles; autorización en páginas, menú y home.

## Requisitos funcionales

- **FR-1** Login y registro Local: usuario + contraseña con hash
  PBKDF2-SHA256 (nunca en texto plano ni recuperable).
- **FR-2** Sesión Local: token firmado (Data Protection) en localStorage;
  se revalida contra la BD al iniciar cada circuito (habilita revocación).
- **FR-3** Modalidad Windows: autenticación por Negotiate (Active Directory)
  sin login manual.
- **FR-4** Control de acceso global: sin sesión, solo `/login` y `/register`;
  acceso denegado muestra "No tenés permiso para acceder a esta sección.
  Consultá a un Administrador." (sin detalles técnicos).
- **FR-5** Autorización por página con `[Authorize(Roles=...)]` visible desde
  `AuthorizeRouteView` (fragmentos de denegación sin layout propio).
- **FR-6** Menú y tarjetas del home coherentes con el rol (`AuthorizeView`
  anidados por sección): lo que el rol no puede abrir, no se muestra.
- **FR-7** Roles válidos: `Admin`, `Gerencia`, `Contador`, `Operador`.
- **FR-8** El primer usuario registrado queda como `Admin`.
- **FR-9** Reglas de gestión de usuarios (server-side, en
  `UpdateUserCommandHandler`, en este orden):
  1. nadie se desactiva a sí mismo ni cambia su propio rol;
  2. el estado (activo/inactivo) solo lo cambia Admin;
  3. cambiar roles solo Admin/Gerencia;
  4. otorgar o quitar el rol Admin solo Admin;
  5. nunca se desactiva ni se degrada al último Admin activo.
- **FR-10** Concurrencia optimista con `RowVersion`; el conflicto de edición
  simultánea se informa con un mensaje claro, sin jerga.

## Requisitos no funcionales

- **NFR-1** Toda regla de autorización se valida en el servidor; el cliente es
  solo experiencia de usuario.
- **NFR-2** Textos de usuario sin jerga técnica (nada de infraestructura,
  modelos ni proveedores).
- **NFR-3** Un usuario con sesión viva desactivado pierde el acceso al
  revalidar (F5) y cae en `/login?returnUrl=...`.

## Criterios de aceptación (resumen)

- **AC-1** Dado un Operador, cuando entra a `/usuarios`, entonces ve la
  denegación.
- **AC-2** Dado un usuario desactivado, cuando intenta iniciar sesión, entonces
  ve "Tu usuario está deshabilitado. Contactá al administrador."
- **AC-3** Dada una sesión viva de un usuario desactivado desde otra sesión,
  cuando presiona F5, entonces cae en `/login?returnUrl=...`.
- **AC-4** Dado Gerencia, cuando intenta quitar el rol Admin a un Admin,
  entonces el servidor rechaza y la BD no cambia.
- **AC-5** Dado el único Admin activo, cuando alguien intenta desactivarlo,
  entonces se rechaza (el invariante se mantiene).

## Preguntas abiertas

- **PA-1** ¿Se agregan roles más allá de los 4 actuales?
- **PA-2** Política de contraseñas: expiración, complejidad, segundo factor.
- **PA-3** ¿En el on-premise del cliente el default será `Windows`?
