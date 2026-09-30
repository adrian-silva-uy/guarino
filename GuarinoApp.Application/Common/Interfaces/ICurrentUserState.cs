using System.Security.Claims;

namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Estado de autenticación del circuito actual. Es la única fuente de verdad
/// del usuario autenticado durante la vida del circuito de Blazor Server:
/// el proveedor de autenticación lo escribe al iniciar y el servicio de
/// usuario actual lo lee para auditoría y autorización.
/// </summary>
public interface ICurrentUserState
{
    /// <summary>Usuario autenticado del circuito; null si aún no hay sesión.</summary>
    ClaimsPrincipal? User { get; set; }
}
