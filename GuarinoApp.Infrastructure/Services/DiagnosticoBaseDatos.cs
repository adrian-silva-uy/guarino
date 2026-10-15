namespace GuarinoApp.Infrastructure.Services;

/// <summary>Diagnóstico de la verificación de conexión a la base de datos —
/// análogo a <see cref="Diagnostico"/> (pero para Ollama) en
/// BaseDatosRecuperacionService.</summary>
public enum DiagnosticoBaseDatos
    {
    /// <summary>La conexión responde: no hay incidente.</summary>
    Ok,

    /// <summary>Fallo confirmado y el destino NO es local (otra máquina): desde
    /// acá no hay nada que gestionar — solo verificar y avisar.</summary>
    RemotoNoGestionable,

    /// <summary>Fallo confirmado, destino local, pero no es cuestión de reiniciar:
    /// gestión deshabilitada/mal configurada (Motor=SoloAvisar / servicio sin
    /// nombre) o la base solicitada no existe (SQL 4060) — solo verificar y avisar.</summary>
    SoloAviso,

    /// <summary>Fallo confirmado con destino local gestionable: se libera memoria
    /// si hace falta y se levanta/relevanta la instancia.</summary>
    Reparable,

    /// <summary>La verificación no pudo concluir (error ajeno a la base de datos):
    /// puede ser pico de carga — NO se actúa.</summary>
    Indeciso
    }
