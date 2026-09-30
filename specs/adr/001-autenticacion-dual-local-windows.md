# ADR-001: Autenticación dual (Local / Windows)

**Estado**: Aceptada

## Contexto

La app se despliega on-premise en el cliente, donde puede existir Active
Directory; también debe funcionar con usuarios locales (desarrollo, demo, sin
AD).

## Decisión

Modo por configuración `Auth:Mode`:

- **`Local` (default)**: login y registro con contraseña PBKDF2-SHA256; token
  de Data Protection en localStorage, revalidado contra la BD al iniciar cada
  circuito.
- **`Windows`**: autenticación Negotiate/AD, sin login manual.

## Consecuencias

- Un solo punto de control de acceso; las reglas de autorización son
  idénticas en ambos modos.
- En modo Local, la revocación depende de la revalidación por circuito (F5 o
  navegación pierde el acceso si el usuario fue desactivado).
