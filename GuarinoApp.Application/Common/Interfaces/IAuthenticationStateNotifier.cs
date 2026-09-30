namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Permite avisar a la interfaz que el estado de autenticación cambió
/// (login o cierre de sesión) para que se actualice el circuito.
/// </summary>
public interface IAuthenticationStateNotifier
{
    /// <summary>Invalida la sesión en memoria y notifica a los componentes.</summary>
    void NotifyChanged();
}
