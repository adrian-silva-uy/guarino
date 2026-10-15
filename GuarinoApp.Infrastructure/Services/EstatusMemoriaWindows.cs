using System.Runtime.InteropServices;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>Buffer nativo para MEMORYSTATUSEX (GlobalMemoryStatusEx, kernel32):
/// estado de memoria del sistema en Windows. Se usa para detectar presión de RAM
/// antes de levantar la instancia de base de datos (el desencadenante real del
/// fallo de arranque: un modelo de IA residente dejó a SQL sin memoria).</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class EstatusMemoriaWindows
    {
    /// <summary>Tamaño del struct en bytes (debe inicializarse antes de la llamada).</summary>
    public uint DwLength;

    /// <summary>Porcentaje aproximado de memoria en uso.</summary>
    public uint DwMemoryLoad;

    /// <summary>Memoria física total (bytes).</summary>
    public ulong UllTotalPhys;

    /// <summary>Memoria física disponible (bytes) — el dato que consulta el sanador.</summary>
    public ulong UllAvailPhys;

    /// <summary>Tamaño del archivo de página (bytes).</summary>
    public ulong UllTotalPageFile;

    /// <summary>Archivo de página disponible (bytes).</summary>
    public ulong UllAvailPageFile;

    /// <summary>Tamaño total del espacio de direcciones virtual (bytes).</summary>
    public ulong UllTotalVirtual;

    /// <summary>Virtual disponible (bytes).</summary>
    public ulong UllAvailVirtual;

    /// <summary>Virtual disponible y no reservado (bytes).</summary>
    public ulong UllAvailExtendedVirtual;

    public EstatusMemoriaWindows()
        {
        DwLength = (uint)Marshal.SizeOf(typeof(EstatusMemoriaWindows));
        }
    }
