using System.ComponentModel.DataAnnotations;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>Sección BaseDatos de la configuración (auto-reparación de la base de datos).
/// La cadena de conexión en sí vive en ConnectionStrings:DefaultConnection.</summary>
public class BaseDatosOptions
    {
    public const string SectionName = "BaseDatos";

    /// <summary>Auto-reparación silenciosa de la base de datos
    /// (BaseDatosRecuperacionService).</summary>
    public BaseDatosSanacionOptions Sanacion { get; set; } = new();
    }

/// <summary>Opciones de BaseDatos:Sanacion (auto-reparación de la base de datos).
///
/// Filosofía idéntica a la sanación de Ollama: verificar con reintentos antes de
/// actuar, limpiar por INSTANCIA (nunca matar PIDs aislados — da derechos
/// insuficientes y puede afectar a otras bases del servidor), un solo correo por
/// incidente a Notificaciones:EmailAdmin y solo se gestiona cuando el destino de
/// la conexión es local (loopback); un servidor remoto solo se verifica y se avisa.</summary>
public class BaseDatosSanacionOptions
    {
    public bool Habilitada { get; set; } = true;

    /// <summary>Cada cuánto se verifica la conexión a la base de datos.</summary>
    [Range(15, 3600)]
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Ciclos consecutivos de fallo antes de declarar incidente y actuar.
    /// Con 2 (≈2 min) los cortes transitorios — p. ej. un pico de ~20 usuarios en
    /// el asistente IA — se disuelven solos sin que el sanador toque nada.</summary>
    [Range(1, 10)]
    public int CiclosDeConfirmacion { get; set; } = 2;

    /// <summary>Tope de reparaciones en una hora: si no se recupera, se avisa una
    /// sola vez que requiere intervención manual y se sigue verificando en silencio
    /// (evita un bucle de arranques contra un problema de fondo).</summary>
    [Range(1, 20)]
    public int MaxReparacionesPorHora { get; set; } = 3;

    /// <summary>False = solo detecta y avisa por correo (p. ej. si el gestor de
    /// servicios del servidor es quien debe levantar la instancia).</summary>
    public bool GestionarInstancia { get; set; } = true;

    /// <summary>Cómo se levanta la instancia cuando el destino es local:
    /// "LocalDb" (sqllocaldb, por instancia), "ServicioWindows" (net/sc del
    /// servicio de SQL) o "SoloAvisar". Si el destino de la conexión NO es local,
    /// nunca se gestiona aunque esto diga lo contrario.</summary>
    public string Motor { get; set; } = "LocalDb";

    /// <summary>Instancia de LocalDB a gestionar (cuando Motor=LocalDb).</summary>
    public string NombreInstanciaLocalDb { get; set; } = "mssqllocaldb";

    /// <summary>Servicio de Windows de SQL (cuando Motor=ServicioWindows), p. ej.
    /// "MSSQLSERVER" o "MSSQL$SQLEXPRESS". Vacío = no se puede gestionar.</summary>
    public string NombreServicio { get; set; } = "";

    /// <summary>true = si el servicio local está corriendo pero no responde,
    /// además de intentar iniciarlo se lo reinicia (sc stop + sc start). Dejar en
    /// false si otros sistemas comparten la misma instancia de SQL.</summary>
    public bool ReiniciarServicio { get; set; } = false;

    /// <summary>Antes de levantar la instancia, si la memoria libre está por debajo
    /// del mínimo, se descargan los modelos de IA locales (fueron el
    /// desencadenante real: el modelo residente dejó a SQL sin memoria para
    /// arrancar). Solo aplica si Ollama apunta a loopback.</summary>
    public bool LiberarMemoria { get; set; } = true;

    /// <summary>Umbral de memoria libre (MB) por debajo del cual se descargan los
    /// modelos de IA antes de intentar levantar la instancia.</summary>
    [Range(64, 65536)]
    public int MemoriaMinimaLibreMB { get; set; } = 512;

    /// <summary>Timeout de cada verificación de conexión (por intento).</summary>
    [Range(3, 120)]
    public int TimeoutVerificacionSegundos { get; set; } = 15;
    }
