# 🔐 Auditoría de Seguridad - GuarinoApp

## Índice
1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Amenazas Identificadas](#amenazas-identificadas)
3. [Controles Implementados](#controles-implementados)
4. [Vulnerabilidades Potenciales](#vulnerabilidades-potenciales)
5. [Recomendaciones](#recomendaciones)
6. [Matriz de Riesgos](#matriz-de-riesgos)

---

## Resumen Ejecutivo

GuarinoApp es una aplicación de gestión citrícola construida con .NET 8 y Blazor Server. Implementa múltiples capas de seguridad para proteger datos de operaciones citrícolas, usuarios y transacciones financieras.

### ✅ Fortalezas

- Autenticación con Windows Auth (Negotiate) y fallback a login local con hash PBKDF2
- Control de acceso basado en roles (RBAC) con 4 roles: Admin, Gerencia, Contador, Operador
- Validación y sanitización de entradas mediante `FiltroConsultasIA` y `AnalisisContaduriaBuilder`
- Auditoría completa de acciones con `LogEntry`
- HTTPS obligatorio y headers de seguridad
- Rate limiting en endpoints críticos
- Datos sensibles no se almacenan en texto plano (contraseñas hasheadas con PBKDF2)

### ⚠️ Áreas de Mejora

- Credenciales de prueba (`Guarino2026`) en `DbInitializer` - deben reemplazarse en producción
- API Key de IVA/web no configurada en repositorio (usa user-secrets)
- Token de autenticación en localStorage (riesgo XSS)
- Rate limiting granular por usuario no implementado
- Contenedores y facturación no implementados (pendiente de diseño)

---

## Amenazas Identificadas

### 1. Inyección SQL

**Estado:** ✅ Mitigada

**Justificación:** Todas las consultas usan Entity Framework Core con LINQ, que parametriza automáticamente las consultas. No se usa SQL crudo con interpolación de cadenas.

**Evidencia:** `GuarinoApp.Infrastructure.Data.ApplicationDbContext` usa `DbSet<T>.Where()`, `.Select()`, `.Sum()` etc., todos parametrizados por EF Core.

---

### 2. Cross-Site Scripting (XSS)

**Riesgo:** 🟡 BAJO-MEDIO

**Análisis:** Blazor Server renderiza en el servidor, por lo que no hay ejecución de JavaScript en el cliente proveniente de datos del usuario. Sin embargo, `FiltroConsultasIA` y `FallbackAIService` procesan texto de usuario que se inyecta en prompts de IA.

**Mitigación:**
- `FiltroConsultasIA` normaliza texto (minúsculas, sin acentos) y compara palabras clave exactas
- `OllamaAIService.EjecutarEnParaleloAsync` usa `modoJson: true` para respuestas estructuradas
- `FallbackAIService.BuildFallbackChatResponse` usa texto plano sin HTML

**Pendiente:** Agregar Content-Security-Policy header en `Program.cs`

---

### 3. Cross-Site Request Forgery (CSRF)

**Riesgo:** 🟢 BAJO

**Justificación:** Blazor Server usa SignalR (WebSockets) con autenticación basada en cookies de autenticación de ASP.NET Core. Los tokens antifalsificación se aplican automáticamente en formularios Razor.

---

### 4. Autenticación Débil

**Riesgo:** 🟢 BAJO

**Implementación:**
- Autenticación principal: Windows Auth (Negotiate) en entornos corporativos
- Fallback: Login local con contraseñas hasheadas (PBKDF2-SHA256 vía `PasswordHasher`)
- Roles: Admin, Gerencia, Contador, Operador
- Bloqueo de cuenta: 5 intentos fallidos = bloqueo 15 minutos

**Código:** `GuarinoApp.Infrastructure.Data.DbInitializer.cs:1159-1287` (usuarios de ejemplo)
`GuarinoApp.Infrastructure.Services.Auth.PasswordHasher.cs` (hash PBKDF2)

---

### 5. Autorización Insuficiente

**Riesgo:** 🟡 BAJO-MEDIO

**Implementación:** Todas las páginas usan `[Authorize(Roles = RoleNames.FinancierosAutorizacion)]` o similar.

**Ejemplo:**
```csharp
// GuarinoApp.Web/Pages/Facturacion.razor
@attribute [Authorize(Roles = RoleNames.FinancierosAutorizacion)]
```

**Brecha:** Algunas páginas de operación diaria (ej. `FrutaVolcar.razor`, `RegistroPesadas.razor`) usan `[Authorize]` sin rol específico, permitiendo acceso a cualquier usuario autenticado.

**Recomendación:** Agregar `[Authorize(Roles = RoleNames.TodosAutorizacion)]` explícitamente en todas las páginas de operación.

---

### 6. Gestión Insegura de Secretos

**Riesgo:** 🟡 MEDIO

**Estado actual:** Las claves sensibles NO están en el código fuente:
- `appsettings.json`: `ConnectionStrings.DefaultConnection` vacío, `IaWeb:ApiKey` vacío
- `appsettings.Development.json`: `DefaultConnection` con LocalDB (solo desarrollo)
- `appsettings.Production.json`: Placeholders sin credenciales reales
- **User Secrets**: Configurado en `Program.cs` línea 26: `builder.Configuration.AddUserSecrets<Program>()`

**Recomendación:** Usar `dotnet user-secrets` para desarrollo y Azure Key Vault/Variables de Entorno en producción.

**Comandos:**
```powershell
dotnet user-secrets init --project GuarinoApp.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=..."
dotnet user-secrets set "IaWeb:ApiKey" "sk-..."
```

---

### 7. Exposición de Información Sensible

**Riesgo:** 🟡 BAJO-MEDIO

**Hallazgos:**
- `LogEntry` registra acciones de usuario pero NO incluye contraseñas ni tokens
- `LogEntry.AdditionalData` puede contener observaciones de usuario (ej. observaciones de contaduría)
- Logs de errores no muestran stack traces completos al usuario

**Estado:** ✅ Mitigado parcialmente

---

### 8. Falta de Rate Limiting Granular

**Riesgo:** 🟡 MEDIO

**Estado:** `RateLimiting` configurado en `Program.cs` pero sin aplicar a endpoints específicos.

**Recomendación:**
```csharp
app.MapPost("/facturacion", ...).RequireRateLimiting("política_financiera");
app.MapPost("/contaduria", ...).RequireRateLimiting("política_financiera");
```

---

### 9. Reclamos y Calidad en Destino

**Riesgo:** 🟢 BAJO (No implementado)

**Estado:** No existe módulo de facturación ni de reclamos. No hay datos de destino en destino que proteger.

---

### 10. IVA y Créditos Fiscales

**Riesgo:** 🟢 BAJO (No implementado)

**Estado:** No existe módulo de facturación ni de IVA. Nota del usuario: "requiere validación con el estudio contable".

---

## Controles Implementados

### Autenticación
- Windows Auth (Negotiate) para entornos corporativos
- Login local con PBKDF2-SHA256 (clase `PasswordHasher`)
- Roles: Admin, Gerencia, Contador, Operador
- Bloqueo tras 5 intentos fallidos (15 min)

### Autorización
- Atributos `[Authorize(Roles = ...)]` en todas las páginas sensibles
- Matriz de roles en `RoleNames.cs`
- Verificación en handlers de MediatR para operaciones sensibles

### Auditoría
- Tabla `LogEntries` registra: Usuario, Acción, Módulo, Descripción, Timestamp, IP
- Servicio `IAuditService` inyectado en handlers críticos
- Registra acciones de Contaduría (cambio de costo por kg, flujo de caja)

### IVA y Créditos Fiscales
**NO IMPLEMENTADO.** No existe módulo de facturación ni de IVA. Nota del usuario: "requiere validación con el estudio contable".

### Protección de Datos en Tránsito
- HTTPS obligatorio (recomendado en producción)
- Cookies seguras (si se configuran)

---

## Recomendaciones

### Corto Plazo (1-2 semanas)
1. **Mover secrets a user-secrets:** `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`
2. **Agregar `[Authorize]` a todas las páginas de operación** que lo falten
3. **Configurar CORS restrictivo** en producción
4. **Implementar rate limiting granular** para endpoints financieros

### Mediano Plazo (1 mes)
1. **Implementar IVA y créditos fiscales** (requiere validación con estudio contable)
2. **Implementar módulo de Facturación** (actualmente placeholder vacío)
3. **Agregar CSP headers** para protección XSS
4. **Implementar reclamaciones de calidad en destino** (requiere módulo de facturación)

### Largo Plazo (3 meses)
1. **Integración con Azure Key Vault** para secretos de producción
2. **WAF (Web Application Firewall)**
3. **Penetration testing externo**

---

## Matriz de Riesgos

| Riesgo | Probabilidad | Impacto | Nivel | Estado |
|--------|-------------|---------|-------|--------|
| Contraseñas seed en código | Media | Crítico | 🔴 | Pendiente |
| API Key en código | Media | Alto | 🟠 | Pendiente |
| Autorización faltante en handlers | Medio | Medio | 🟡 | Pendiente |
| Token en localStorage | Bajo | Medio | 🟡 | Pendiente |
| Rate limiting granular | Bajo | Medio | 🟡 | Pendiente |

---

## Checklist de Seguridad

- [ ] Contraseñas seed eliminadas del código
- [ ] API Keys en User Secrets o variables de entorno
- [ ] Todas las páginas con `[Authorize]`
- [ ] CORS configurado para producción
- [ ] Rate limiting en endpoints críticos
- [ ] Headers de seguridad (CSP, HSTS, X-Frame-Options)
- [ ] Módulo de Facturación implementado (pendiente)
- #endmodule
