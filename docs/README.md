# 📚 Wiki GuarinoApp - Documentación Arquitectónica Completa

## 🎯 Introducción

Bienvenido a la **Wiki Arquitectónica de GuarinoApp**. Este conjunto de documentos proporciona una visión completa y detallada de la arquitectura, seguridad, componentes y procesos de la aplicación web **GuarinoApp.Web** - Sistema de Gestión de Operaciones Citríolas.

### 📋 Stack Tecnológico
```
┌─────────────────────────────────────┐
│ ASP.NET Core 8 + Blazor Server      │ ← Web Framework
├─────────────────────────────────────┤
│ MudBlazor + Bootstrap               │ ← UI Components
├─────────────────────────────────────┤
│ Entity Framework Core 8              │ ← ORM
├─────────────────────────────────────┤
│ SQL Server 2019+                    │ ← Database
├─────────────────────────────────────┤
│ MediatR (CQRS) + FluentValidation   │ ← Patterns
├─────────────────────────────────────┤
│ Windows Auth (Negotiate)             │ ← Authentication
├─────────────────────────────────────┤
│ OpenMeteo + Ollama                  │ ← External Services
└─────────────────────────────────────┘
```

### 👥 Público Objetivo
- **Desarrolladores**: Entienden la estructura y cómo contribuir
- **Arquitectos**: Validan decisiones de diseño
- **DevOps/SRE**: Despliegan y mantienen la aplicación
- **Security/Compliance**: Realizan auditorías
- **Stakeholders**: Comprenden capacidades y limitaciones

---

## 📖 Documentos Disponibles

### 1. **[ARQUITECTURA.md](./ARQUITECTURA.md)** 🏗️
Visión general de la arquitectura del sistema

**Incluye**:
- Descripción general y stack tecnológico
- Arquitectura de capas (Clean Architecture)
- Jerarquía de componentes Blazor
- Flujo CQRS principal
- Diagrama de inyección de dependencias
- Modelo de autenticación y autorización
- Jerarquía de roles
- Protecciones de seguridad
- Servicios de dominio
- Schema de base de datos
- Sistema de auditoría
- Diagrama de despliegue
- Checklist de despliegue

**Audiencia**: Todos  
**Tiempo de lectura**: 20-30 min  
**Prerequisitos**: Conocimiento básico de .NET/Blazor

---

### 2. **[SEGURIDAD.md](./SEGURIDAD.md)** 🔐
Auditoría de seguridad integral y análisis de vulnerabilidades

**Incluye**:
- Resumen ejecutivo de seguridad
- Análisis de 10 amenazas principales
- Estado actual de mitigaciones
- Controles implementados (AC, EN, IV, LM, SM)
- Vulnerabilidades potenciales (crítica, alta, media)
- Recomendaciones priorizadas
- Matriz de riesgos
- Checklist de hardening de seguridad
- Procedimientos de escalación

**Riesgos Identificados**:
- 🔴 CRÍTICA (1): Secrets en código
- 🟠 ALTA (4): Falta de autorización backend, encrypting data at rest
- 🟡 MEDIA (5): CORS, rate limiting, sanitización

**Audiencia**: Arquitectos, Security, DevOps  
**Tiempo de lectura**: 25-35 min  
**Prerequisitos**: Experiencia en seguridad de aplicaciones

---

### 3. **[FLUJOS_CASOS_USO.md](./FLUJOS_CASOS_USO.md)** 📊
Flujos de datos y casos de uso detallados

**Incluye**:
- 5 flujos principales con diagramas
  1. Flujo de autenticación
  2. Flujo de consulta (GetPallets)
  3. Flujo de creación (CreatePallet)
  4. Flujo de monitoreo climático
  5. Flujo de exportación a Excel
- Módulos por rol (Admin, Gerencia, Contador, Operador)
- 5 casos de uso detallados (UC-001 a UC-005)
- Diagrama de secuencia integrado
- Integraciones externas (OpenMeteo, Ollama, Email)

**Audiencia**: Desarrolladores, Analistas, QA  
**Tiempo de lectura**: 20-25 min  
**Prerequisitos**: Comprensión de CQRS

---

### 4. **[COMPONENTES_BLAZOR.md](./COMPONENTES_BLAZOR.md)** 🎨
Guía de componentes Blazor y patrones de desarrollo

**Incluye**:
- Estructura de carpetas
- 6 componentes compartidos principales:
  - MainLayout.razor
  - NavMenu.razor
  - ExportToolbar.razor
  - InteractiveChart.razor
  - SimpleDialog.razor
  - CurrentUserDisplay.razor
- Páginas principales (~30 páginas)
- 2 patrones de componentes reutilizables
- Inyección de dependencias
- Buenas prácticas (DO's/DON'Ts)

**Audiencia**: Desarrolladores frontend  
**Tiempo de lectura**: 15-20 min  
**Prerequisitos**: Experiencia con Blazor

---

### 5. **[DESPLIEGUE_MANTENIMIENTO.md](./DESPLIEGUE_MANTENIMIENTO.md)** 🚀
Guía completa de despliegue y mantenimiento

**Incluye**:
- Requisitos previos (hardware, software, credenciales)
- Despliegue en desarrollo (5 pasos)
- Despliegue en producción (7 pasos)
  - Arquitectura de despliegue (Load Balancer, 3 servidores IIS, BD Replicada)
  - Configuración por entorno
  - Migraciones seguras
- Monitoreo y mantenimiento
- Troubleshooting (4 problemas comunes)
- Procedimientos de rollback
- Checklist pre/durante/post despliegue

**Audiencia**: DevOps, SRE, Release Managers  
**Tiempo de lectura**: 30-40 min  
**Prerequisitos**: Experiencia con IIS, SQL Server

---

## 🗺️ Mapa de Navegación

```
┌─────────────────────────────────────────────┐
│          WIKI GUARINO                       │
└─────────────┬───────────────────────────────┘
              │
    ┌─────────┼─────────┬──────────┬──────────────┐
    │         │         │          │              │
    v         v         v          v              v
  [1]       [2]       [3]        [4]            [5]
  ARQU      SECU      FLUJOS     COMPO         DESPL

  Visión   Riesgos y  Datos y    UI y          Deploy &
  General  Mitigación Procesos   Patrones      Ops

         ↓ Lee primero esto ↓

  1. Comienza aquí (15 min)
  2. Si trabajas en seguridad (25 min)
  3. Si necesitas entender flujos (20 min)
  4. Si desarrollas frontend (15 min)
  5. Si despliegas/mantienes (30 min)
```

---

## 🎓 Guías de Lectura por Rol

### 👨‍💻 Desarrollador Backend

**Ruta recomendada**: ARQUITECTURA → FLUJOS_CASOS_USO → SEGURIDAD → COMPONENTES_BLAZOR

1. Lee ARQUITECTURA (30 min)
   - Entiende capas y flujo CQRS
   - Familiarízate con DI
   - Revisa modelo de autenticación

2. Lee FLUJOS_CASOS_USO (20 min)
   - Casos de uso relevantes a tu módulo
   - Diagramas de secuencia
   - Integraciones externas

3. Lee SEGURIDAD (25 min)
   - Entradas validadas correctamente
   - Autorización en handlers
   - Rate limiting configurado

4. Consulta COMPONENTES_BLAZOR (10 min)
   - Entiende cómo consumes datos desde UI

**Tiempo total**: ~85 min | **Foco**: Lógica de negocio y seguridad

---

### 🎨 Desarrollador Frontend

**Ruta recomendada**: COMPONENTES_BLAZOR → ARQUITECTURA → FLUJOS_CASOS_USO

1. Lee COMPONENTES_BLAZOR (20 min)
   - Estructura de proyectos y carpetas
   - Componentes compartidos
   - Patrones de desarrollo
   - DO's y DON'Ts

2. Lee ARQUITECTURA - Sección Componentes (10 min)
   - Jerarquía de componentes
   - MudBlazor theming

3. Lee FLUJOS_CASOS_USO (15 min)
   - Entiende qué datos necesitas
   - Cómo se comunica con backend

**Tiempo total**: ~45 min | **Foco**: UI/UX y usabilidad

---

### 🔐 Arquitecto/Security Engineer

**Ruta recomendada**: SEGURIDAD → ARQUITECTURA → DESPLIEGUE_MANTENIMIENTO

1. Lee SEGURIDAD completo (35 min)
   - Identifica brechas
   - Revisa matriz de riesgos
   - Valida mitigaciones

2. Lee ARQUITECTURA - Sección Seguridad (15 min)
   - Controles implementados
   - Jerarquía de roles
   - Protecciones

3. Lee DESPLIEGUE_MANTENIMIENTO (15 min)
   - Hardening del entorno
   - Monitoreo

**Tiempo total**: ~65 min | **Foco**: Postura de seguridad

---

### 🚀 DevOps/SRE

**Ruta recomendada**: DESPLIEGUE_MANTENIMIENTO → ARQUITECTURA → MONITOREO

1. Lee DESPLIEGUE_MANTENIMIENTO completo (40 min)
   - Pasos de despliegue
   - Troubleshooting
   - Rollback procedures

2. Lee ARQUITECTURA - Secciones BD y Despliegue (15 min)
   - Schema de BD
   - Diagrama de despliegue

3. Lee MONITOREO Y MANTENIMIENTO (10 min)
   - Health checks
   - Logs centralizados
   - Métricas

**Tiempo total**: ~65 min | **Foco**: Confiabilidad y operaciones

---

## 📊 Matriz de Referencia Rápida

| Documento | Secciones Clave | Personas | Tiempo |
|-----------|-----------------|----------|--------|
| ARQUITECTURA | Capas, CQRS, Autenticación, BD | Todos | 30 min |
| SEGURIDAD | Vulnerabilidades, Recomendaciones, Riesgos | Security, Arquitecto | 35 min |
| FLUJOS_CASOS_USO | UC-001 a UC-005, Integraciones | Dev, QA, Analista | 25 min |
| COMPONENTES_BLAZOR | Componentes, Patrones, Buenas Prácticas | Dev Frontend | 20 min |
| DESPLIEGUE_MANTENIMIENTO | Deploy, Troubleshooting, Rollback | DevOps, SRE | 40 min |

---

## 🔍 Índice Temático

### Autenticación y Autorización
- ARQUITECTURA: "Seguridad - Modelo de Autenticación"
- SEGURIDAD: "Autenticación Débil", "Autorización Insuficiente"
- DESPLIEGUE: "Problema: Error de autenticación"

### Base de Datos
- ARQUITECTURA: "Base de Datos"
- DESPLIEGUE: "Preparar Base de Datos", "Migraciones de BD", "Problema: BD no accesible"

### Componentes UI
- COMPONENTES_BLAZOR: "Estructura de Componentes", "Componentes Compartidos"
- ARQUITECTURA: "Componentes Blazor"

### Despliegue
- DESPLIEGUE_MANTENIMIENTO: Completo
- ARQUITECTURA: "Despliegue"

### Flujos de Datos
- FLUJOS_CASOS_USO: "Flujos Principales"
- ARQUITECTURA: "Flujo de Datos"

### Monitoreo
- DESPLIEGUE_MANTENIMIENTO: "Monitoreo y Mantenimiento"
- SEGURIDAD: Auditoría

### Seguridad
- SEGURIDAD: Completo
- ARQUITECTURA: "Seguridad"
- DESPLIEGUE: Hardening

---

## ✅ Checklist de Onboarding Nuevo Developer

- [ ] Clonado el repositorio
- [ ] Ambiente de desarrollo configurado
- [ ] Leído ARQUITECTURA.md
- [ ] Leído documento relevante para su rol
- [ ] Ejecutó la aplicación localmente
- [ ] Realizó primer commit
- [ ] Revisó PR de un compañero
- [ ] Desplegó a staging (con ayuda)

---

## 📞 Contacto y Soporte

### Responsables de Documentación
- **Arquitecto Principal**: [Nombre]
- **Security Lead**: [Nombre]
- **DevOps Lead**: [Nombre]

### Canales de Comunicación
- 🐛 **Issues**: GitHub Issues
- 💬 **Preguntas**: Slack #guarino-tech
- 📧 **Escalaciones**: tech-leads@company.com

### Reportar Errores en Documentación
1. Crear issue en GitHub con etiqueta `documentation`
2. Describir qué documento, sección y qué falta/es incorrecto
3. Proponer solución si es posible

---

## 🔄 Mantenimiento de la Wiki

### Actualización de Documentos
- **Frecuencia**: Cada cambio arquitectónico o al desplegar major version
- **Responsable**: Arquitecto + autor del cambio
- **Proceso**: 
  1. Crear branch `docs/update-xxx`
  2. Editar documentos
  3. PR con revisión de mínimo 2 personas
  4. Merge a master

### Versionado de Documentos
```
Versión 1.0 - Base
├─ Versión 1.1 - Agregado Security Audit
├─ Versión 1.2 - Agregado Componentes Blazor
└─ Versión 2.0 - Refactor completo (Q2 2024)
```

**Última revisión**: 2024-01-XX  
**Próxima revisión**: 2024-04-XX  
**Versión**: 1.0

---

## 📈 Estadísticas de la Documentación

| Métrica | Valor |
|---------|-------|
| **Documentos** | 5 |
| **Palabras totales** | ~15,000 |
| **Diagramas** | 30+ |
| **Casos de uso** | 5 |
| **Recomendaciones** | 12 |
| **Riesgos identificados** | 10 |

---

## 🎓 Recursos Adicionales

### Documentación Externa
- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [MudBlazor Components](https://www.mudblazor.com)
- [OWASP Top 10](https://owasp.org/www-project-top-ten)

### Herramientas Útiles
- **Visual Studio 2022** (IDE)
- **SQL Server Management Studio** (BD)
- **Postman** (Testing API)
- **Git Bash** (Control de versiones)
- **Application Insights** (Monitoreo)

### Videos y Tutoriales
- [Clean Architecture in .NET](https://www.youtube.com/...)
- [Blazor Server Deep Dive](https://www.youtube.com/...)
- [CQRS Pattern](https://www.youtube.com/...)

---

## ⭐ Notas Importantes

> ⚠️ **Esta documentación es viva**  
> Se actualiza continuamente. Siempre revisa la fecha de última actualización.

> 🔐 **Información Sensible**  
> Los ejemplos de connection strings y credenciales son ficticios.  
> Usar valores reales solo en ambientes seguros.

> 📋 **Confidencialidad**  
> Distribuir solo a personal autorizado de GuarinoApp.

> 🚀 **Versión de Aplicación Documentada**  
> GuarinoApp v1.0 - ASP.NET Core 8 - Blazor Server

---

## 🎉 Conclusión

Esta Wiki proporciona una **documentación arquitectónica completa y actualizada** de GuarinoApp. Cada documento está diseñado para ser:

- ✅ **Específico**: Enfocado en un aspecto particular
- ✅ **Detallado**: Con ejemplos y diagramas
- ✅ **Accesible**: Para diferentes roles
- ✅ **Mantenible**: Fácil de actualizar
- ✅ **Práctico**: Listo para aplicar

**¡Bienvenido al equipo GuarinoApp!** 🚀

---

**Documento**: README - Wiki Index  
**Última actualización**: 2024-01-XX  
**Versión**: 1.0  
**Responsable**: Documentación Técnica  
**Distribución**: Internal Only

---

## Actualizaciones recientes (Oct 2026)

Cambios aplicados al código y reflejados en la documentación:

- **Filtro de módulos por rol (UI)**: `NavMenu.razor` y `Index.razor` muestran los módulos bloqueados con 🔒 + tooltip "Requiere: X — tu rol actual: Y" en lugar de ocultarlos. Componentes nuevos: `Shared/NavLinkSeguro.razor` y `Shared/EnlaceSeguro.razor`. Matriz de acceso por reflexión en `Services/MapaAccesosRutas.cs` (lee `[Authorize]` de las páginas → ruta→roles). Helper `Services/RolUsuario.cs` resuelve el rol actual con `RoleNames`. Registrado como singleton en `Program.cs`.
- **RoleNames como fuente única**: constantes `Admin`/`Gerencia`/`Contador`/`Operador` + grupos `Todos`/`Financieros`/`Administracion` + cadenas `*Autorizacion` para `[Authorize]`. Cero literales de rol en `.razor`.
- **Paginación fija de 25 filas** en las 30 grillas (`RowsPerPage="25"` + `MudTablePager PageSizeOptions="new int[] { 25 }"`). Auditoría usa paginación server-side (`ServerData` + `state.Page/PageSize`).
- **Filtro IA por rol**: `ConsultaIArestringida` (log) + matriz de roles por tool MCP.
- **Servidor MCP** (`/mcp`) con `WithHttpTransport()` + middleware Bearer propio (`McpAuthMiddleware`); tools solo lectura; auditoría `McpCall`.
- **Cero datos inventados**: `GetDummyData`/`GenerarZafrasDummy`/fallbacks eliminados; `EsDummy` siempre `false`; "En nuestro poder" = sin cliente y Estado != Bajado/Despachado.
- **Autenticación**: Negotiate + Data Protection tokens (`GuarinoApp.Auth.v1`, 12 h, localStorage `auth_token`); lockout 5 fallos/15 min.
- **Grillas sin paginador** reemplazado por paginación de 25 (instrucción vigente).
- **Build**: 0 errores / 0 advertencias. UI en español.